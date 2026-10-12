using System;
using System.Collections.Generic;
using AsyncContent;

namespace UntitledGemGame;

// Registered during startup; pumped only after the menu has rendered.
// Completion assignments run on the game thread, including audio dictionaries.
internal static class GameplayPreloader
{
  private static readonly Queue<Action> pending = new();
  private static Func<bool> finish;
  private static int queued, finished;
  public static double Progress => queued == 0 ? 1 : (double)finished / queued;
  public static string Error { get; private set; }
  public static bool Ready => pending.Count == 0 && finish == null && Error == null;
  public static void Queue<T>(string path, Action<AsyncAsset<T>> assign, bool optional = false)
  {
    queued++;
    pending.Enqueue(() => finish = BeginLoad(path, assign, optional, out _));
  }

  // Menu assets start immediately, but must also finish before entering gameplay.
  public static AsyncAsset<T> Load<T>(string path)
  {
    var completion = BeginLoad<T>(path, _ => { }, false, out var asset);
    queued++;
    pending.Enqueue(() => finish = completion);
    return asset;
  }

  private static Func<bool> BeginLoad<T>(string path, Action<AsyncAsset<T>> assign,
    bool optional, out AsyncAsset<T> asset)
  {
    int completed = 0;
    var loadingAsset = AssetManager.LoadAsync<T>(path,
      callbackDone: _ => System.Threading.Interlocked.Exchange(ref completed, 1));
    asset = loadingAsset;
#if KNI_WEB
    completed = 1;
#endif
    return () =>
    {
      if (System.Threading.Volatile.Read(ref completed) == 0) return false;
      if (!loadingAsset.IsLoaded || loadingAsset.IsFailed)
      {
        if (!optional) Error = path;
        return true;
      }
      assign(loadingAsset);
      return true;
    };
  }

#if KNI_WEB
  private static long lastBrowserFrame = -1;
#endif
  public static void Update()
  {
#if KNI_WEB
    // Fixed-step catch-up may call Update repeatedly before the next browser paint.
    if (lastBrowserFrame == Web.WebBootstrap.FrameIndex) return;
    lastBrowserFrame = Web.WebBootstrap.FrameIndex;
#endif
    if (finish != null)
    {
      if (!finish()) return;
      finish = null;
      finished++;
    }
    if (pending.Count > 0) pending.Dequeue()();
  }
}
