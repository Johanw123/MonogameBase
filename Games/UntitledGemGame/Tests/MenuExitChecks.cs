using System.Diagnostics;
using System.Reflection;
using Gum.Forms.DefaultFromFileVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoGameGum;
using UntitledGemGame;
using UntitledGemGame.Screens;

// Runs the actual pause-menu button, screen lifecycle and background preparation.
// Supply an empty temporary XDG profile; refuse to touch the player's save directory.
internal sealed class MenuExitChecks : GameMain
{
  private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  private readonly Stopwatch elapsed = Stopwatch.StartNew();
  private bool starting, leaving;
  private bool feedbackChecked;
  private bool volumePreviewChecked;
  private int exits;
  private ulong expectedBalance;

  public static void Run(string content, string profile)
  {
    content = Path.GetFullPath(content);
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
    string originalDirectory = Environment.CurrentDirectory;
    try
    {
      // The game's raw asset loader expects a relative Content root. Fill out the
      // test output with links, preserving the menu textures already copied by MSBuild.
      string staged = Path.Combine(AppContext.BaseDirectory, "Content");
      Directory.CreateDirectory(staged);
      foreach (string directory in Directory.EnumerateDirectories(content))
      {
        string target = Path.Combine(staged, Path.GetFileName(directory));
        if (!Directory.Exists(target)) Directory.CreateSymbolicLink(target, directory);
      }
      foreach (string file in Directory.EnumerateFiles(content))
      {
        string target = Path.Combine(staged, Path.GetFileName(file));
        if (!File.Exists(target)) File.CreateSymbolicLink(target, file);
      }
      Environment.CurrentDirectory = AppContext.BaseDirectory;
      File.WriteAllText(settings, """
        {"width":1280,"height":720,"isFullscreen":false,"isBorderless":false,
         "isFixedTimeStep":false,"isVSync":false,"musicVolume":0,"sfxVolume":0}
        """);
      using var check = new MenuExitChecks();
      ((Game)check).Run();
    }
    finally
    {
      Environment.CurrentDirectory = originalDirectory;
      if (originalSettings == null) File.Delete(settings);
      else File.WriteAllText(settings, originalSettings);
    }
  }

  private MenuExitChecks() { }

  private static DynamicSoundEffectInstance? RadioOutput()
  {
    object? radio = typeof(AudioManager).GetField("music", PrivateInstance)!.GetValue(AudioManager.Instance);
    return (DynamicSoundEffectInstance?)radio?.GetType().GetField("output", PrivateInstance)!.GetValue(radio);
  }

  // Holding either volume slider plays music through the pause, joined by a repeating effect.
  private void CheckVolumeSliderPreview(GameTime gameTime)
  {
    var output = RadioOutput()!;
    var settings = (GraphicalUiElement)typeof(GameMain)
      .GetField("m_settingsMenu", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    var music = (DefaultFromFileSliderRuntime)settings.GetChildByNameRecursively("SliderMusicVolume");
    var sfx = (DefaultFromFileSliderRuntime)settings.GetChildByNameRecursively("SliderSfxVolume");
    InteractiveGue Thumb(DefaultFromFileSliderRuntime slider) => (InteractiveGue)slider.GetGraphicalUiElementByName("ThumbInstance");
    var cursor = GumService.Default.Cursor;
    var time = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(0.1));
    var played = new List<float>();
    void Played(SoundEffect effect, float volume, float pitch, float pan)
    {
      if (effect.Name == "SFX/gem.wav") played.Add(volume);
    }
    void Frame(InteractiveGue? pushed)
    {
      cursor.WindowPushed = pushed;
      time.TotalGameTime += time.ElapsedGameTime;
      base.Update(time);
      cursor.WindowPushed = null;
    }
    // Moves the slider during a frame of holding it. Its release event only fires after that frame.
    void Drag(DefaultFromFileSliderRuntime slider, double value)
    {
      cursor.WindowPushed = Thumb(slider);
      slider.FormsControl.Value = value;
      Frame(Thumb(slider));
    }
    void Held(DefaultFromFileSliderRuntime slider, InteractiveGue pushed, string name)
    {
      // A real press records the value, so Gum only reports a release if it changed.
      typeof(Gum.Forms.Controls.Slider).GetField("ValueOnThumbOrTrackPush", PrivateInstance)!
        .SetValue(slider.FormsControl, slider.FormsControl.Value);
      played.Clear();
      for (int frame = 0; frame < 13; frame++)
      {
        Frame(pushed);
        if (output.State != SoundState.Playing) throw new Exception($"Holding {name} did not play the paused music.");
      }
      if (played.Count != 3) throw new Exception($"Holding {name} for 1.3s played {played.Count} effects, not one on grab and every 0.5s.");
      Frame(null);
      Frame(null);
      if (output.State != SoundState.Paused) throw new Exception($"Releasing {name} did not pause the music again.");
      if (played.Count != 3) throw new Exception($"Effects kept repeating after releasing {name}.");
    }

    AudioManager.SoundPlayed += Played;
    try
    {
      PauseGame();
      Frame(null);
      if (output.State != SoundState.Paused) throw new Exception("The pause menu did not pause the music.");
      Frame(Thumb(music));
      if (output.State != SoundState.Paused) throw new Exception("A volume slider played music outside the settings page.");
      SwapMenu("SettingsMenu");
      Frame(null);
      if (output.State != SoundState.Paused) throw new Exception("Opening settings from pause played the music.");

      Drag(sfx, 60);
      Frame(null);
      Held(music, Thumb(music), "the music thumb");
      Held(music, music.FormsControl.Track!, "the music track");
      Held(sfx, Thumb(sfx), "the effects thumb");
      Held(sfx, sfx.FormsControl.Track!, "the effects track");

      Drag(music, 40);
      if (output.Volume != 0.4f) throw new Exception("A held slider did not play the music at its volume.");
      Frame(null);

      played.Clear();
      Drag(sfx, 20);
      Frame(null);
      Drag(sfx, 80);
      Frame(null);
      if (played.Count != 2 || Math.Abs(played[0] - 0.2f) > 0.001f || Math.Abs(played[1] - 0.8f) > 0.001f)
        throw new Exception($"Effects did not follow the slider during a drag: {string.Join(", ", played)}.");

      ResumeGame();
      Frame(null);
      if (output.State != SoundState.Playing) throw new Exception("Resuming after the slider preview did not play the music.");
    }
    finally { AudioManager.SoundPlayed -= Played; }
  }

  protected override void Update(GameTime gameTime)
  {
    if (elapsed.Elapsed.TotalSeconds > 180) throw new Exception("Menu exit checks timed out.");

    if (_screenManager.ActiveScreen is UntitledGemGameGameScreen outgoing && !leaving
      && (bool)typeof(UntitledGemGameGameScreen).GetField("GameStarted", PrivateInstance)!.GetValue(outgoing)!)
    {
      if (!volumePreviewChecked)
      {
        if (RadioOutput() == null)
        {
          base.Update(gameTime);
          return;
        }
        CheckVolumeSliderPreview(gameTime);
        volumePreviewChecked = true;
      }
      // Leave income undelivered so teardown must include it in the save exactly once.
      outgoing.State.CurrentRedGemCount = (ulong)(1000 * (exits + 1));
      UntitledGemGameGameScreen.DeliveredUncounted = (ulong)(17 + exits);
      expectedBalance = outgoing.State.CurrentRedGemCount + UntitledGemGameGameScreen.DeliveredUncounted;
      PauseGame();
      var pauseMenu = (GraphicalUiElement)typeof(GameMain)
        .GetField("m_gameMenu", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
      var exit = (DefaultFromFileButtonRuntime)pauseMenu.GetChildByNameRecursively("ButtonExitMainMenu");
      bool clicked = false;
      EventHandler recordClick = (_, _) => clicked = true;
      exit.Click += recordClick;
      // Use Gum's visual click entry point, as its input dispatcher does.
      typeof(InteractiveGue)
        .GetMethod("CallClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
        .Invoke(exit, null);
      exit.Click -= recordClick;
      if (!clicked) throw new Exception("The pause-menu exit click was not dispatched.");

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
    if (!feedbackChecked && (bool)typeof(MainMenu).GetField("menuFrameDrawn", PrivateInstance)!.GetValue(menu)!)
    {
      FeedbackHoverChecks.Run(GraphicsDevice, _spriteBatch);
      feedbackChecked = true;
    }
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
      Console.WriteLine("Menu exit checks passed: music and effects preview on held volume sliders, deferred exit, pending income saved, Continue and repeated menu preparation.");
      Exit();
      return;
    }
    if (exits > 0 && UntitledGemGameGameScreen.Instance?.State.CurrentRedGemCount != expectedBalance)
      throw new Exception("Continue preparation did not restore the saved balance.");
    typeof(MainMenu).GetMethod("StartGame", PrivateInstance)!.Invoke(menu, [exits == 0]);
    starting = true;
  }
}
