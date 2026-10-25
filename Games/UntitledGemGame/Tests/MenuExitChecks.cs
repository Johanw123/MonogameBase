using System.Diagnostics;
using System.Reflection;
using Gum.Forms.DefaultFromFileVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Screens;

// Runs the actual pause-menu button, screen lifecycle and background preparation.
// Supply an empty temporary XDG profile; refuse to touch the player's save directory.
internal sealed class MenuExitChecks : GameMain
{
  private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  private readonly Stopwatch elapsed = Stopwatch.StartNew();
  private bool starting, leaving;
  private int exits;
  private ulong expectedBalance;

  public static void Run(string content, string profile)
  {
    profile = Path.GetFullPath(profile);
    Directory.CreateDirectory(profile);
    if (Directory.EnumerateFileSystemEntries(profile).Any())
      throw new Exception("Menu exit checks require an empty temporary profile.");
    Environment.SetEnvironmentVariable("XDG_DATA_HOME", profile);
    if (!Path.GetFullPath(GameSaveStore.DefaultPath).StartsWith(profile + Path.DirectorySeparatorChar,
      StringComparison.Ordinal))
      throw new Exception("This platform does not redirect LocalApplicationData with XDG_DATA_HOME; no saves were changed.");

    string settings = GetPath("Settings.json");
    string? originalSettings = File.Exists(settings) ? File.ReadAllText(settings) : null;
    try
    {
      File.WriteAllText(settings, """
        {"width":1280,"height":720,"isFullscreen":false,"isBorderless":false,
         "isFixedTimeStep":false,"isVSync":false,"musicVolume":0,"sfxVolume":0}
        """);
      using var check = new MenuExitChecks(content);
      ((Game)check).Run();
    }
    finally
    {
      if (originalSettings == null) File.Delete(settings);
      else File.WriteAllText(settings, originalSettings);
    }
  }

  private MenuExitChecks(string content) => Content.RootDirectory = Path.GetFullPath(content);

  protected override void Update(GameTime gameTime)
  {
    if (elapsed.Elapsed.TotalSeconds > 180) throw new Exception("Menu exit checks timed out.");

    if (_screenManager.ActiveScreen is UntitledGemGameGameScreen outgoing && !leaving
      && (bool)typeof(UntitledGemGameGameScreen).GetField("GameStarted", PrivateInstance)!.GetValue(outgoing)!)
    {
      // Leave income undelivered so teardown must include it in the save exactly once.
      outgoing.State.CurrentRedGemCount = (ulong)(1000 * (exits + 1));
      UntitledGemGameGameScreen.DeliveredUncounted = (ulong)(17 + exits);
      expectedBalance = outgoing.State.CurrentRedGemCount + UntitledGemGameGameScreen.DeliveredUncounted;
      PauseGame();
      var pauseMenu = (GraphicalUiElement)typeof(GameMain)
        .GetField("m_gameMenu", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
      var exit = (DefaultFromFileButtonRuntime)pauseMenu.GetChildByNameRecursively("ButtonExitMainMenu");
      typeof(Gum.Forms.Controls.Primitives.ButtonBase)
        .GetMethod("PerformClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
        .Invoke(exit.FormsControl, [null]);

      // The click is dispatched inside gameplay's update in the real UI. That caller
      // must still have its screen, singleton and income until the update has returned.
      if (!ReferenceEquals(_screenManager.ActiveScreen, outgoing)
        || !ReferenceEquals(UntitledGemGameGameScreen.Instance, outgoing) || !IsPaused)
        throw new Exception("Exit unloaded or resumed gameplay inside the click callback.");
      if (UntitledGemGameGameScreen.DeliveredUncounted != (ulong)(17 + exits))
        throw new Exception("Exit cleared pending income before saving.");
      leaving = true;
    }

    base.Update(gameTime);

    if (_screenManager.ActiveScreen is not MainMenu menu) return;
    if (leaving)
    {
      var save = new GameSaveStore(GameSaveStore.DefaultPath).Load();
      if (save?.RedGems != expectedBalance)
        throw new Exception("Exit did not save pending income exactly once.");
      if (IsPaused) throw new Exception("The main menu retained gameplay pause state.");
      exits++;
      starting = false;
      leaving = false;
    }
    bool ready = (bool)typeof(MainMenu).GetProperty("GameplayReady", PrivateInstance)!.GetValue(menu)!;
    if (!ready || starting) return;
    if (exits == 2)
    {
      Console.WriteLine("Menu exit checks passed: deferred exit, pending income saved, Continue and repeated menu preparation.");
      Exit();
      return;
    }
    if (exits > 0 && UntitledGemGameGameScreen.Instance?.State.CurrentRedGemCount != expectedBalance)
      throw new Exception("Continue preparation did not restore the saved balance.");
    typeof(MainMenu).GetMethod("StartGame", PrivateInstance)!.Invoke(menu, [exits == 0]);
    starting = true;
  }
}
