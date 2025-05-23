﻿using Microsoft.Xna.Framework.Content;
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
    private static List<Task> m_loadingTasks = [];
    private static AssetsLoader m_assetsLoader;
    private static List<FileSystemWatcher> m_fileWatchers = [];

    private static bool m_debug = true;

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

    private static T CreateSmallDefaultAsset<T>()
    {
      // Create a small sized default value of each asset type for quick loading while the real asset is loading in the background
      // Effect, Model, SoundEffect, Song, SpriteFont, Texture, Texture2D, and TextureCube
      return typeof(T) switch
      {
        { } texType when texType == typeof(Texture2D) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, 1, 1]),
        { } texType when texType == typeof(Effect) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, Array.Empty<byte>()]),
        { } texType when texType == typeof(Model) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, new List<ModelBone>(), new List<ModelMesh>()]),
        { } texType when texType == typeof(SoundEffect) => (T)Activator.CreateInstance(typeof(T), [Array.Empty<byte>(), 1, AudioChannels.Mono]),

        { } texType when texType == typeof(Song) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(SpriteFont) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(Texture) => (T)Activator.CreateInstance(typeof(T), []),
        { } texType when texType == typeof(TextureCube) => (T)Activator.CreateInstance(typeof(T), []),
        _ => default
      };
    }


    public static AsyncAsset<T> Load<T>(string asset)
    {
      var assetContainer = new AsyncAsset<T>
      {
        Value = CreateSmallDefaultAsset<T>()
      };

      var task = Task.Factory.StartNew(() =>
      {
        try
        {
          asset = GetContentPath(asset);

          if (m_debug)
          {
            FileSystemWatcher watcher = new();
            watcher.Changed += (s, e) =>
            {
              assetContainer.IsLoaded = false;

              Task.Factory.StartNew(() =>
              {
                var loadedAsset = (T)Convert.ChangeType(m_assetsLoader.LoadTexture(asset, true), typeof(T));
                assetContainer.Value = loadedAsset;
                assetContainer.IsLoaded = true;
              });

            };

            watcher.Path = Path.GetDirectoryName(asset);
            watcher.Filter = Path.GetFileName(asset);
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            m_fileWatchers.Add(watcher);
          }
          var loadedAsset = (T)Convert.ChangeType(m_assetsLoader.LoadTexture(asset), typeof(T));
          assetContainer.Value = loadedAsset;
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex.Message);
        }
        assetContainer.IsLoaded = true;
      }).ContinueWith(task =>
      {
        m_loadingTasks.Remove(task);
      });

      m_loadingTasks.Add(task);

      return assetContainer;
    }

    public static bool IsLoadingContent()
    {
      if (m_loadingTasks.Count == 0)
        return false;

      return m_loadingTasks.Any(t => t.Status != TaskStatus.RanToCompletion);
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
