using UntitledGemGame;
using UntitledGemGame.Web;

static class PreloaderChecks
{
  public static void Run()
  {
    GameplayPreloader.LoadMenu<string>("menu");
    if (GameplayPreloader.MenuReady) throw new Exception("Menu completion was not observed on the game thread");
    GameplayPreloader.UpdateMenuLoads();
    if (!GameplayPreloader.MenuReady) throw new Exception("Completed menu assets should release startup");
    int menuLoads = AsyncContent.AssetManager.Loads;
    int assigned = 0;
    for (int i = 0; i < 20; i++) GameplayPreloader.Queue<string>("asset-" + i, _ => assigned++);
    if (AsyncContent.AssetManager.Loads != menuLoads) throw new Exception("Queued assets loaded before a browser paint");
    if (!GameplayPreloader.MenuReady) throw new Exception("Gameplay loading should not hide the menu");
    WebBootstrap.FrameIndex = 1;
    GameplayPreloader.Update();
    GameplayPreloader.Update();
    if (AsyncContent.AssetManager.Loads != menuLoads + 1) throw new Exception("Catch-up updates loaded multiple assets in one browser frame");
    if (GameplayPreloader.Ready) throw new Exception("Gameplay started before preload completed");
    for (int i = 0; i < 25; i++) { WebBootstrap.FrameIndex++; GameplayPreloader.Update(); }
    if (!GameplayPreloader.Ready || assigned != 20 || GameplayPreloader.Progress != 1)
      throw new Exception("Preload readiness/progress did not include every assignment");
    Console.WriteLine("Preloader checks passed: deferred loading, one asset per browser paint, complete assignment before gameplay.");
  }
}

namespace AsyncContent
{
  // CPU-only loader substitute; the real queue and frame gate are compiled above.
  sealed class AsyncAsset<T> { public bool IsLoaded => true; public bool IsFailed => false; }
  static class AssetManager
  {
    public static int Loads;
    public static AsyncAsset<T> LoadAsync<T>(string path, Action<T> callbackDone)
    {
      Loads++;
      callbackDone(default);
      return new AsyncAsset<T>();
    }
  }
}
