using UntitledGemGame;

void Check(bool condition, string message)
{
  if (!condition) throw new Exception(message);
}

void Pump(int frames = 5)
{
  for (int i = 0; i < frames; i++) GameplayPreloader.Update();
}

Check(GameplayPreloader.Ready, "An empty preloader should be ready.");
var fleet = GameplayPreloader.LoadMenu<string>("fleet");
Check(AsyncContent.AssetManager.Pending.ContainsKey("fleet"), "Menu assets must start immediately.");
Check(!GameplayPreloader.Ready, "An immediate menu load must block gameplay readiness.");
Pump();
Check(!GameplayPreloader.Ready, "Pumping frames must not bypass an incomplete menu load.");
AsyncContent.AssetManager.Complete("fleet");
Check(fleet.IsLoaded, "The menu should receive its loaded asset.");
Pump();
Check(GameplayPreloader.Ready, "Completed menu assets should release the start gate.");

GameplayPreloader.Queue<string>("deferred-game", _ => { });
GameplayPreloader.LoadMenu<string>("logo");
GameplayPreloader.UpdateMenuLoads();
Check(!GameplayPreloader.MenuReady, "Startup must wait for all menu assets.");
Check(!AsyncContent.AssetManager.Pending.ContainsKey("deferred-game"), "Startup must not pump gameplay loads.");
AsyncContent.AssetManager.Complete("logo");
GameplayPreloader.UpdateMenuLoads();
Check(GameplayPreloader.MenuReady && !GameplayPreloader.Ready, "The menu may appear while gameplay is still queued.");
Pump();
AsyncContent.AssetManager.Complete("deferred-game");
Pump();

int gameThread = Environment.CurrentManagedThreadId;
bool assigned = false;
GameplayPreloader.Queue<string>("effect", asset =>
{
  Check(Environment.CurrentManagedThreadId == gameThread, "Assignment must run on the game thread.");
  assigned = asset.Value == "effect";
});
GameplayPreloader.Queue<string>("icons", _ => { });
Check(!AsyncContent.AssetManager.Pending.ContainsKey("effect"), "Queued uploads must wait for the pump.");
Pump();
Check(!assigned && !GameplayPreloader.Ready, "Gameplay must wait while an effect is loading.");
Task.Run(() => AsyncContent.AssetManager.Complete("effect")).GetAwaiter().GetResult();
Check(!assigned, "The worker callback must not assign gameplay state.");
Pump();
Check(assigned, "The next frame must assign the completed effect.");
Check(AsyncContent.AssetManager.Pending.ContainsKey("icons"), "The next queued upload must start.");
Check(!GameplayPreloader.Ready, "The icon upload must keep Continue waiting after other assets finish.");
Pump();
Check(!GameplayPreloader.Ready, "A slow icon upload must not release Continue.");
AsyncContent.AssetManager.Complete("icons");
Pump();
Check(GameplayPreloader.Ready, "Continue may proceed after every upload and assignment finishes.");

GameplayPreloader.Queue<string>("optional", _ => throw new Exception("Failed assets must not be assigned."), optional: true);
Pump();
AsyncContent.AssetManager.Complete("optional", fail: true);
Pump();
Check(GameplayPreloader.Ready && GameplayPreloader.Error == null, "Missing optional audio must not block Continue.");

GameplayPreloader.Queue<string>("required", _ => throw new Exception("Failed assets must not be assigned."));
Pump();
AsyncContent.AssetManager.Complete("required", fail: true);
Pump();
Check(!GameplayPreloader.Ready && GameplayPreloader.Error == "required", "A failed required asset must prevent gameplay from starting.");
Check(GameplayPreloader.MenuReady, "A gameplay load failure must leave the menu visible.");
GameplayPreloader.LoadMenu<string>("missing-menu");
AsyncContent.AssetManager.Complete("missing-menu", fail: true);
GameplayPreloader.UpdateMenuLoads();
Check(!GameplayPreloader.MenuReady && GameplayPreloader.MenuError == "missing-menu", "A menu failure must be reported during startup.");
Console.WriteLine("Preload checks passed: separate menu/gameplay readiness, delayed loads, serial uploads, game-thread assignment and failures.");

namespace AsyncContent
{
  // Controllable completion replaces graphics/audio I/O while exercising the real preloader.
  public sealed class AsyncAsset<T>
  {
    public bool IsLoaded { get; set; }
    public bool IsFailed { get; set; }
    public T Value;
  }

  public static class AssetManager
  {
    public static readonly Dictionary<string, Action<bool>> Pending = new();

    public static AsyncAsset<T> LoadAsync<T>(string path, Action<T> callbackDone)
    {
      var asset = new AsyncAsset<T>();
      Pending.Add(path, fail =>
      {
        asset.IsLoaded = true;
        asset.IsFailed = fail;
        asset.Value = fail ? default : (T)(object)path;
        callbackDone(asset.Value);
      });
      return asset;
    }

    public static void Complete(string path, bool fail = false)
    {
      var complete = Pending[path];
      Pending.Remove(path);
      complete(fail);
    }
  }
}
