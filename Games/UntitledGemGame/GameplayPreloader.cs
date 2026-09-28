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
  public static string Error { get; private set; }
  public static bool Ready => pending.Count == 0 && finish == null && Error == null;
  public static void Queue<T>(string path, Action<AsyncAsset<T>> assign, bool optional = false)
  {
    pending.Enqueue(() =>
    {
      int completed = 0;
      var asset = AssetManager.LoadAsync<T>(path,
        callbackDone: _ => System.Threading.Interlocked.Exchange(ref completed, 1));
#if KNI_WEB
      completed = 1;
#endif
      finish = () =>
      {
        if (System.Threading.Volatile.Read(ref completed) == 0) return false;
        if (!asset.IsLoaded || asset.IsFailed)
        {
          if (!optional) Error = path;
          return true;
        }
        assign(asset);
        return true;
      };
    });
  }

  public static void Update()
  {
    if (finish != null)
    {
      if (!finish()) return;
      finish = null;
    }
    if (pending.Count > 0) pending.Dequeue()();
  }
}
