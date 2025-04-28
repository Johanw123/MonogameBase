using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    public static implicit operator T(AsyncAsset<T> myClassInstance)
    {
      return myClassInstance.Value;
    }
  }

  public class AssetManager
  {
    private static ContentManager m_content;
    private static GraphicsDevice m_graphicsDevice;
    private static List<Task> m_loadingTasks = [];

    public static void Initialize(ContentManager content, GraphicsDevice graphicsDevice)
    {
      m_content = content;
      m_graphicsDevice = graphicsDevice;
    }

    private static T CreateSmallDefaultAsset<T>()
    {
      // Create a small sized default value of each asset type for quick loading while the real asset is loading in the background
      // Effect, Model, SoundEffect, Song, SpriteFont, Texture, Texture2D, and TextureCube
      return typeof(T) switch
      {
        { } texType when texType == typeof(Texture2D) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, 1, 1]),
        { } texType when texType == typeof(Effect) => (T)Activator.CreateInstance(typeof(T), [m_graphicsDevice, Array.Empty<byte>()]),
        _ => default
      };
    }

    public static AsyncAsset<T> Load<T>(string asset)
    {
      var assetContainer = new AsyncAsset<T>
      {
        Value = CreateSmallDefaultAsset<T>()
      };

      var task = Task.Factory.StartNew(async () =>
      {
        Debug.WriteLine("Task started");
        var loadedAsset = m_content.Load<T>(asset);

        await Task.Delay(5000);
        assetContainer.Value = loadedAsset;
        assetContainer.IsLoaded = true;
      });
      //}).ContinueWith(task =>
      //{
      //  m_loadingTasks.Remove(task);
      //  Debug.WriteLine("Task finished");
      //});

      //task.Wait(10000);

      m_loadingTasks.Add(task);
      //task.Wait(10000);

      //task.RunSynchronously();

      return assetContainer;
    }

    public static bool IsLoadingContent()
    {
      return m_loadingTasks.Any(t => t.Status == TaskStatus.Running);
    }

    public static void WaitForAllLoadingTasks()
    {
      Debug.WriteLine("pre WaitForAllLoadingTasks");
      var t = Task.WhenAll(m_loadingTasks).Wait(5000000);
      Debug.WriteLine("post WaitForAllLoadingTasks");
    }

    public static void Unload()
    {
      m_content.Unload();
    }
  }
}
