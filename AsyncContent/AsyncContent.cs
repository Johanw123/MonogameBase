using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;


namespace AsyncContent
{
  public class AsyncAsset<T>
  {
    public bool IsLoaded { get; set; } = false;
    public T Value;

    //public static implicit operator AsyncAsset<T>(T someValue)
    //{
    //  return new AsyncAsset<T>(someValue);
    //}

    public static implicit operator T(AsyncAsset<T> classInstance)
    {
      return classInstance.Value;
    }
  }

  public class AssetManager
  {
    private static ContentManager m_content;
    private static GraphicsDevice m_graphicsDevice;
    private static readonly List<Task> m_loadingTasks = [];
    private static AssetsLoader m_assetsLoader;
    private static readonly List<FileSystemWatcher> m_fileWatchers = [];

    private static readonly bool m_debug = true;

    public static void Initialize(ContentManager content, GraphicsDevice graphicsDevice)
    {
      m_content = content;
      m_graphicsDevice = graphicsDevice;
      m_assetsLoader = new AssetsLoader(m_graphicsDevice);
    }

    private static string GetContentPath(string path)
    {
      if (Path.Exists(path))
        return path;

      var newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
      if (File.Exists(newPath))
      {
        return newPath;
      }

      newPath = Path.Combine(m_content.RootDirectory, path);
      if (File.Exists(newPath))
      {
        return newPath;
      }

      var filename = Path.GetFileName(path);
      var dir = Path.GetDirectoryName(path);
      var contentRoot = m_content.RootDirectory;
      string currentDir = Environment.CurrentDirectory;

      var p = Path.Combine(currentDir, contentRoot, dir);

      var files = Directory.GetFiles(p);
      foreach (var file in files)
      {
        var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
        if (fileWithoutExtension == filename)
        {
          return file;
        }
      }

      return path;
    }

    public static Stream GetFileStream(string path)
    {
      path = GetContentPath(path);

      return TitleContainer.OpenStream(path);
    }

    public static byte[] GetFileBytes(string path)
    {
      path = GetContentPath(path);

      return File.ReadAllBytes(path);
    }

    // Create a small sized default value of each asset type for quick loading while the real asset is loading in the background
    // Effect, Model, SoundEffect, Song, SpriteFont, Texture, Texture2D, and TextureCube
    private static T CreateSmallDefaultAsset<T>()
    {
      return typeof(T) switch
      {
        { } texType when texType == typeof(Texture2D) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, 1, 1]),
        //TODO: Effect -> save simple shader as byte array and use as fallback
        // { } texType when texType == typeof(Effect) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, new byte[10]]),
        { } texType when texType == typeof(Model) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, new List<ModelBone>(), new List<ModelMesh>()]),
        { } texType when texType == typeof(SoundEffect) => (T)Activator.CreateInstance(typeof(T), [Array.Empty<byte>(), 1, AudioChannels.Mono]),

        { } texType when texType == typeof(Song) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(SpriteFont) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(Texture) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(TextureCube) => (T)Activator.CreateInstance(typeof(T), []),
        _ => default
      };
    }

    private static void LoadAsset<T>(AsyncAsset<T> assetContainer, string asset, bool forceReload)
    {
      try
      {
        T loadedAsset = assetContainer.Value;

        switch (typeof(T))
        {
          case Type texType when texType == typeof(Texture2D):
            loadedAsset = (T)Convert.ChangeType(m_assetsLoader.LoadTexture(asset, forceReload), typeof(T));
            break;
          case Type effectType when effectType == typeof(Effect):
            loadedAsset = (T)Convert.ChangeType(m_assetsLoader.LoadEffect(asset, forceReload), typeof(T));
            break;
        }

        assetContainer.Value = loadedAsset;
        assetContainer.IsLoaded = true;
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public static void FakeMinimumLoadingTime(int milliseconds = 2000)
    {
      var task = Task.Run(async () =>
          {
            await Task.Delay(milliseconds);
          }).ContinueWith(task => { m_loadingTasks.Remove(task); });
      m_loadingTasks.Add(task);
    }

    public static event Action BatchLoaded;

    // muvm -- FEXBash ./mgfxc_wine_setup.sr
    // Exec=/usr/bin/muvm -- FEXBash -c "$HOME/Downloads/wine-10.4-amd64/bin/wine $HOME/Downloads/browsinghistoryview-x64/BrowsingHistoryView.exe"
    public static AsyncAsset<T> Load<T>(string asset)
    {
      var assetContainer = new AsyncAsset<T>
      {
        Value = CreateSmallDefaultAsset<T>()
      };

      var task = Task.Run(() =>
      {
        try
        {
          Console.WriteLine(Directory.GetCurrentDirectory());
          Console.WriteLine("Loading asset..." + asset);
          asset = GetContentPath(asset);

          Console.WriteLine("modded path: " + asset);

          if (m_debug)
          {
            //TODO: Breakout watcher in handler class to handle multiple assets pointing to same file on disk
            // Or Dictionary for m_fileWatchers
            FileSystemWatcher watcher = new();
            watcher.Changed += (s, e) =>
            {
              assetContainer.IsLoaded = false;

              var task = Task.Factory.StartNew(() =>
              {
                LoadAsset(assetContainer, asset, true);
              });

              m_loadingTasks.Add(task);
            };

            watcher.Path = Path.GetDirectoryName(asset);
            watcher.Filter = Path.GetFileName(asset);
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            m_fileWatchers.Add(watcher);
          }

          LoadAsset(assetContainer, asset, false);
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex.Message);
        }
      }).ContinueWith(task =>
      {
        m_loadingTasks.Remove(task);
      });

      m_loadingTasks.Add(task);

      return assetContainer;
    }

    private static bool wasLoadingContent = false;

    public static bool IsLoadingContent()
    {
      bool isLoadingContent = false;

      // if (m_loadingTasks.Count == 0)
      //   isLoadingContent = false;

      isLoadingContent = m_loadingTasks.Any(t => t.Status != TaskStatus.RanToCompletion);

      if (!isLoadingContent && wasLoadingContent)
      {
        //Should we actually batch loading based on an object? like MainMenu for example
        BatchLoaded?.Invoke();
        BatchLoaded = null;
      }

      wasLoadingContent = isLoadingContent;
      return isLoadingContent;
    }

    public static void WaitForAllLoadingTasks()
    {
      var t = Task.WhenAll(m_loadingTasks);
      t.ConfigureAwait(false);
    }

    public static void Unload()
    {
      m_content.Unload();
    }
  }
}
