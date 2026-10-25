using JapeFramework;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Screens;
using MonoGameGum;
using System;
using UntitledGemGame.Screens;
using JapeFramework.ImGUI;
using Gum.DataTypes;
using Gum.Wireframe;
using Apos.Tweens;
using System.Linq;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using Serilog;
using Gum.Forms.DefaultFromFileVisuals;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using Gum.Forms;
using Gum.Forms.DefaultVisuals;
using Gum.Converters;
using GUI.Shared.Helpers;
using FontStashSharp;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace UntitledGemGame
{
  public class GameMain : BaseGame
  {
    public Platform.IPlatformServices PlatformServices { get; internal set; } = Platform.LocalPlatformServices.Instance;

    public static bool MultiThreadingEnabled = !OperatingSystem.IsBrowser();

    public static event Action ImGuiContent;
    public static event Action HudContent;

    private static GameMain m_instance;
    public static BaseGame Instance => m_instance;

    public static GumService GumServiceUpgrades = new();
    public static GumProjectSave GumProject;

    private static GraphicalUiElement m_menuScreen;
    private static GraphicalUiElement m_settingsMenu;
    private static GraphicalUiElement m_creditsMenu;
    private static GraphicalUiElement m_gameMenu;

    private DefaultFromFileSliderRuntime m_sliderMusicVolume;
    private DefaultFromFileSliderRuntime m_sliderSfxVolume;

    private DefaultFromFileCheckBoxRuntime m_checkboxFullscreen;
    private DefaultFromFileCheckBoxRuntime m_checkboxBorderless;
    private DefaultFromFileCheckBoxRuntime m_checkboxVSync;
    private DefaultFromFileCheckBoxRuntime m_checkboxFixedTimeStep;

    private DefaultFromFileComboBoxRuntime m_comboBoxResolution;
    private DefaultFromFileComboBoxRuntime m_comboBoxLanguage;

    public static string CurrentMenu = "MainMenu";
    private Settings _settings;

    public static bool IsPaused = false;
    public bool MaximizeFramefrate = false;

    // The HUD's Damage panel stays open or closed across runs and new games.
    internal static bool DamagePanelOpen
    {
      get => m_instance?._settings?.DamagePanelOpen == true;
      set
      {
        if (m_instance?._settings == null) return;
        m_instance._settings.DamagePanelOpen = value;
#if !KNI_WEB
        // Capture sessions stage their own settings; never write over the player's.
        if (Capture.CaptureSession.Active) return;
#endif
        m_instance.SaveSettings();
      }
    }

    public override bool ShouldDrawFramerateCounter => Demo.IsDev;
    protected override bool ShouldCaptureTextInput => PlaytestFeedback.IsOpen || SoundEffectsLabCapturesInput;

    internal static bool SoundEffectsLabCapturesInput =>
#if !KNI_WEB
      Demo.IsDev && (m_instance?.soundEffectsLab?.CapturesInput == true || DebugGuiSprites.CapturesInput);
#else
      false;
#endif

#if !KNI_WEB
    private DebugSoundEffects soundEffectsLab;
    internal static void OpenSoundEffectsLab()
    {
      if (Demo.IsDev && m_instance?.soundEffectsLab != null) m_instance.soundEffectsLab.Open = true;
    }
    internal static void OpenGuiSpritesLab()
    {
      if (Demo.IsDev) DebugGuiSprites.StandaloneOpen = true;
    }
#endif

    static GameMain()
    {
#if !KNI_WEB
      UpgradePopoutWindow.ConfigureTransparency();
#endif
    }

    public GameMain()
    {
      Exiting += CloseUpgradePopout;
#if !KNI_WEB
      if (Capture.CaptureSession.Active)
      {
        // Offscreen capture: fixed window, no frame limiting, never reads or writes Settings.json.
        _settings = Capture.CaptureSession.CreateSettings();
        Init("UntitledGemGame", _settings.Width, _settings.Height, targetFps: 120.0f, fixedTimeStep: false, fullscreen: false, false);
        InactiveSleepTime = TimeSpan.Zero;
        return;
      }
      try
      {
        EnsureJson("Settings.json", SettingsContext.Default.Settings);
        _settings = LoadJson("Settings.json", SettingsContext.Default.Settings);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
        _settings = new Settings();
      }

      var width = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
      var height = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
      
      // width = GraphicsDevice.Adapter.CurrentDisplayMode.Width; 
      // height = GraphicsDevice.Adapter.CurrentDisplayMode.Height; 

      if (!_settings.IsFullscreen)
      {
        width = _settings.Width;
        height = _settings.Height;
      }

      bool isFixedTimeStep = _settings.IsFixedTimeStep;
      bool isFullscreen = _settings.IsFullscreen;
      bool isBorderless = _settings.IsBorderless;
#else
      _settings = LoadJson("Settings.json", SettingsContext.Default.Settings);
      var width = Web.WebBootstrap.CanvasWidth;
      var height = Web.WebBootstrap.CanvasHeight;
      bool isFixedTimeStep = _settings.IsFixedTimeStep;
      bool isFullscreen = false;
      bool isBorderless = false;
#endif

      Init("UntitledGemGame", width, height, targetFps: 120.0f, fixedTimeStep: isFixedTimeStep, fullscreen: isFullscreen, isBorderless);
#if KNI_WEB
      // KNI's HiDef profile caps shader metadata at SM3. Our browser effects
      // use SM4 metadata, supported by its WebGL2 FL10_0 device profile.
      _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
#endif
    }

#if KNI_WEB
    internal void ResizeBrowser(int width, int height)
    {
      if (_graphics.PreferredBackBufferWidth == width && _graphics.PreferredBackBufferHeight == height) return;
      _graphics.PreferredBackBufferWidth = width;
      _graphics.PreferredBackBufferHeight = height;
      _graphics.ApplyChanges();
    }
#endif

    private bool returnToMainMenuPending;
    private bool initialMenuPending;
    private bool startupFrameDrawn;
    internal bool StartupReady { get; private set; }

    protected override void LoadContent()
    {
#if KNI_WEB
      FontManager.InitFontManager(GraphicsDevice);
#endif
      base.LoadContent();
      // Startup is gated by the menu batch, not BaseGame's cosmetic loading timer.
      showLoadingScreen = false;
    }

    protected override void Initialize()
    {
      m_instance = this;
#if !KNI_WEB
      sealWindowAlpha = UpgradePopoutWindow.UsesTransparentSurfaces;
#endif

      base.Initialize();
      PlaytestFeedback.BindWindow(Window);
#if !KNI_WEB
      if (Demo.IsDev && !Capture.CaptureSession.Active)
      {
        soundEffectsLab = new DebugSoundEffects(Window);
        DebugGuiSprites.RegisterFileDrop(Window);
      }
      // Captures render the world at the shot's own size and aspect, e.g. native 9:16.
      if (Capture.CaptureSession.Active)
        SetVirtualResolution(Capture.CaptureSession.RenderWidth, Capture.CaptureSession.RenderHeight);
#endif
    }

    private void InitializeMenus()
    {
      // IsFixedTimeStep = _settings.IsFixedTimeStep;
      // _graphics.SynchronizeWithVerticalRetrace = _settings.IsVSync;
      // if (!_settings.IsFullscreen)
      // {
      //   _graphics.PreferredBackBufferWidth = _settings.Width;
      //   _graphics.PreferredBackBufferHeight = _settings.Height;
      // }
      // _graphics.IsFullScreen = _settings.IsFullscreen;
      // _graphics.ApplyChanges();
      //
      // Log.Information($"Initializing from settings, back buffer size: {_settings.Width}x{_settings.Height}");

      // SetVirtualResolutionGui(_settings.Width, _settings.Height);

      // _graphics.HardwareModeSwitch = !_settings.IsBorderless;

      // if (_settings.IsFullscreen)
      // {
      //   ApplyFullscreenChange(false);
      // }
      //

      // This forces the AoT compiler to keep the constructor and metadata intact
      var forceInstantiation = new Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime();
      // This forces the compiler to keep the constructor metadata intact for AOT
      //_ = new MonoGame.Extended.Tweening.LinearTween<Vector2>(null, 0, 0, null, Vector2.One);
      // Denna rad tvingar kompilatorn att inte trimma bort Add/Subtract operatorerna för Vector2
      var dummy = Microsoft.Xna.Framework.Vector2.Zero + Microsoft.Xna.Framework.Vector2.One;


      Loc.SetLanguage(Loc.Resolve(_settings.Language, PlatformServices.GameLanguage));

      GumProject = GumService.Default.Initialize(
        this,
        "GumProject/BeyondTheBelt.gumx");

      MenuTheme.Apply(GumProject);
      GuiThemeRuntime.CaptureStates(GumProject);
      GuiPalette.LoadDefaults();
      GuiThemeRuntime.Refresh();
      GumText.Install(GraphicsDevice, GumProject);

      var screen = GumProject.GetScreenSave("MainMenu");
      m_menuScreen = screen.ToGraphicalUiElement();
      m_menuScreen.AddToRoot();


      var settingsScreen = GumProject.GetScreenSave("SettingsMenu");
      m_settingsMenu = settingsScreen.ToGraphicalUiElement();

      var creditsScreen = GumProject.GetScreenSave("CreditsMenu");
      m_creditsMenu = creditsScreen.ToGraphicalUiElement();

      var gameScreen = GumProject.GetScreenSave("GameMenu");
      m_gameMenu = gameScreen.ToGraphicalUiElement();

      MenuHoverShimmer.Attach(m_menuScreen, screen);
      MenuHoverShimmer.Attach(m_settingsMenu, settingsScreen);
      MenuHoverShimmer.Attach(m_creditsMenu, creditsScreen);
      MenuHoverShimmer.Attach(m_gameMenu, gameScreen);

      GuiThemeRuntime.TrackMenu(m_menuScreen);
      GuiThemeRuntime.TrackMenu(m_settingsMenu);
      GuiThemeRuntime.TrackMenu(m_creditsMenu);
      GuiThemeRuntime.TrackMenu(m_gameMenu);
      GuiThemeRuntime.Refresh();
      GumText.Track(m_menuScreen);
      GumText.Track(m_settingsMenu);
      GumText.Track(m_creditsMenu);
      GumText.Track(m_gameMenu);


      var backSettings = m_settingsMenu.GetChildByNameRecursively("ButtonBack") as DefaultFromFileButtonRuntime;
      var backCredits = m_creditsMenu.GetChildByNameRecursively("ButtonBack") as DefaultFromFileButtonRuntime;

      EventHandler back = (_, _) =>
      {
        if (CurrentMenu == "GameMenu")
        {
          SwapMenu("GameMenu");
        }
        else
        {
          SwapMenu("MainMenu");
        }

        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      };

      backSettings.Click += back;
      backCredits.Click += back;

      m_comboBoxResolution = m_settingsMenu.GetChildByNameRecursively("ComboBoxResolution") as DefaultFromFileComboBoxRuntime;
      m_comboBoxResolution.FormsControl.ListBox.VisualTemplate = new VisualTemplate(() =>
        GumProject.GetComponentSave("Controls/ListBoxItem").ToGraphicalUiElement());
#if !KNI_WEB
      var uniqueResolutions = GraphicsDevice.Adapter.SupportedDisplayModes
          .ToArray()
          .Select(m => new { m.Width, m.Height })
          .Distinct()
          .OrderByDescending(m => m.Width)
          .ToList();

      int index = 0;
      int count = 0;

      foreach (var mode in uniqueResolutions)
      {
        if (mode.Width == _settings.Width && mode.Height == _settings.Height)
        {
          index = count;
        }

        m_comboBoxResolution.FormsControl.ListBox.Items.Add($"{mode.Width} x {mode.Height}");
        ++count;
      }

      m_comboBoxResolution.FormsControl.SelectedIndex = index;

      if (index == 0 && _settings.Width == -1 && _settings.Height == -1)
      {
        _settings.Width = GraphicsDevice.Adapter.SupportedDisplayModes.Last().Width;
        _settings.Height = GraphicsDevice.Adapter.SupportedDisplayModes.Last().Height;
        SaveSettings();
      }

      m_comboBoxResolution.FormsControl.SelectionChanged += OnResolutionChanged;
#endif

      m_comboBoxLanguage = m_settingsMenu.GetChildByNameRecursively("ComboBoxLanguage") as DefaultFromFileComboBoxRuntime;
      LanguagePicker.Fill(m_comboBoxLanguage, GumProject, _settings.Language);
      m_comboBoxLanguage.FormsControl.SelectionChanged += OnLanguageChanged;

      m_sliderMusicVolume = m_settingsMenu.GetChildByNameRecursively("SliderMusicVolume") as DefaultFromFileSliderRuntime;
      m_sliderMusicVolume.FormsControl.ValueChanged += OnVolumeChanged;
      m_sliderMusicVolume.FormsControl.ValueChangeCompleted += OnVolumeChangedCompleted;

      m_sliderSfxVolume = m_settingsMenu.GetChildByNameRecursively("SliderSfxVolume") as DefaultFromFileSliderRuntime;
      MenuTheme.BindSlider(m_sliderMusicVolume);
      MenuTheme.BindSlider(m_sliderSfxVolume);
      m_sliderSfxVolume.FormsControl.ValueChangeCompleted += OnSfxVolumeChangedCompleted;

      GraphicalUiElement graphicalUiElementByName = m_sliderSfxVolume.GetGraphicalUiElementByName("ThumbInstance");
      InteractiveGue sliderThumb = graphicalUiElementByName as InteractiveGue;
      sliderThumb.Click += (_, _) =>
      {
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      };

      var buttonApply = m_settingsMenu.GetChildByNameRecursively("ButtonApply") as DefaultFromFileButtonRuntime;
      m_checkboxFullscreen = m_settingsMenu.GetChildByNameRecursively("CheckBoxFullscreen") as DefaultFromFileCheckBoxRuntime;
      m_checkboxBorderless = m_settingsMenu.GetChildByNameRecursively("CheckBoxBorderless") as DefaultFromFileCheckBoxRuntime;
      m_checkboxVSync = m_settingsMenu.GetChildByNameRecursively("CheckBoxVsync") as DefaultFromFileCheckBoxRuntime;
      m_checkboxFixedTimeStep = m_settingsMenu.GetChildByNameRecursively("CheckBoxFixedTimeStep") as DefaultFromFileCheckBoxRuntime;

      m_checkboxFullscreen.Click += OnFullscreenCheckboxClicked;
      m_checkboxBorderless.Click += OnBorderlessCheckboxClicked;
      m_checkboxVSync.Click += OnVSyncCheckboxClicked;
      m_checkboxFixedTimeStep.Click += OnFixedTimeStepCheckboxClicked;
#if KNI_WEB
      m_comboBoxResolution.FormsControl.ListBox.Items.Add($"{Web.WebBootstrap.CanvasWidth} x {Web.WebBootstrap.CanvasHeight}");
      m_comboBoxResolution.FormsControl.SelectedIndex = 0;
      m_comboBoxResolution.FormsControl.IsEnabled = false;
      m_checkboxFullscreen.FormsControl.IsEnabled = false;
      m_checkboxBorderless.FormsControl.IsEnabled = false;
      m_checkboxVSync.FormsControl.IsEnabled = false;
#endif

      var buttonReset = m_settingsMenu.GetChildByNameRecursively("ButtonReset") as DefaultFromFileButtonRuntime;

      buttonApply.Click += (_, _) =>
      {
        Console.WriteLine("Apply Settings");
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      };

      buttonReset.Click += (_, _) =>
      {
        Console.WriteLine("Reset Settings to Default");

        _settings = new Settings();
        AudioManager.Instance.SetSettings(_settings);

        RefreshSettingsGuiValues();

#if !KNI_WEB
        OnResolutionChanged(this, null);
#else
        IsFixedTimeStep = _settings.IsFixedTimeStep;
        SaveSettings();
#endif

        AudioManager.Instance.MusicVolumeUpdated();
        AudioManager.Instance.SfxVolumeUpdated();
      };

      var continueButton = m_gameMenu.GetChildByNameRecursively("ButtonContinue") as DefaultFromFileButtonRuntime;
      continueButton.Click += (_, _) =>
      {
        Console.WriteLine("Continue Game Clicked");
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        ResumeGame();
      };

      var exitToMainMenuButton = m_gameMenu.GetChildByNameRecursively("ButtonExitMainMenu") as DefaultFromFileButtonRuntime;
      exitToMainMenuButton.Click += (_, _) =>
      {
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        // Gum dispatches this from inside gameplay's Update. Leave that screen alive
        // until the update returns, including its singletons and pending delivery income.
        returnToMainMenuPending = true;
      };

      var settingsButton = m_gameMenu.GetChildByNameRecursively("ButtonSettings") as DefaultFromFileButtonRuntime;
      settingsButton.Click += (_, _) =>
      {
        Console.WriteLine("Settings Clicked");
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        SwapMenu("SettingsMenu");
      };

      var creditsButton = m_gameMenu.GetChildByNameRecursively("ButtonCredits") as DefaultFromFileButtonRuntime;
      creditsButton.Click += (_, _) =>
      {
        Console.WriteLine("Credits Clicked");
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        SwapMenu("CreditsMenu");
      };

#if !KNI_WEB
      m_comboBoxResolution.FormsControl.IsEnabled = true;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;
#endif
      AudioManager.Instance.SetSettings(_settings);
      AudioManager.Instance.MusicVolumeUpdated();

      ApplyResolutionChanged();
    }

#if !KNI_WEB
    internal ScreenManager CaptureScreens => _screenManager;
#endif

    void ApplyResolutionChanged()
    {
      // Fullscreen uses the desktop mode. Keep the windowed selection editable
      // so it can be chosen before leaving fullscreen as well.
      if (!_settings.IsFullscreen && _settings.Width > 0 && _settings.Height > 0)
      {
        _graphics.PreferredBackBufferWidth = _settings.Width;
        _graphics.PreferredBackBufferHeight = _settings.Height;
        _graphics.ApplyChanges();
      }
    }

    private void OnResolutionChanged(object arg1, SelectionChangedEventArgs args)
    {
      //TODO: check errors parsing etc
      var selectedText = SelectedResolutionText();
      if (string.IsNullOrEmpty(selectedText)) return;
      var dimensions = selectedText.Split('x').Select(s => s.Trim()).ToArray();

      int width = int.Parse(dimensions[0]);
      int height = int.Parse(dimensions[1]);

      Console.WriteLine($"Resolution changed to: {width}x{height}");

      _settings.Width = width;
      _settings.Height = height;

      ApplyResolutionChanged();

      RefreshedSize();

      SaveSettings();
    }

    private void OnFixedTimeStepCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      _settings.IsFixedTimeStep = m_checkboxFixedTimeStep.FormsControl.IsChecked.Value;
      SaveSettings();

      IsFixedTimeStep = _settings.IsFixedTimeStep;
    }

    private void OnVSyncCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      _settings.IsVSync = m_checkboxVSync.FormsControl.IsChecked.Value;
      SaveSettings();

      _graphics.SynchronizeWithVerticalRetrace = _settings.IsVSync;
      _graphics.ApplyChanges();
    }

    private void OnBorderlessCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      _settings.IsBorderless = m_checkboxBorderless.FormsControl.IsChecked.Value;
      SaveSettings();

      ApplyHardwareMode();
    }

    private void OnFullscreenCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      _settings.IsFullscreen = m_checkboxFullscreen.FormsControl.IsChecked.Value;
      SaveSettings();

      m_comboBoxResolution.FormsControl.IsEnabled = true;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;

      ApplyFullscreenChange(!_settings.IsFullscreen);
      OnResolutionChanged(this, null);
    }

    private void OnVolumeChanged(object sender, EventArgs e)
    {
      Console.WriteLine($"Music Volume: {m_sliderMusicVolume.FormsControl.Value}");
      AudioManager.Instance.MusicVolumeUpdated();

      _settings.MusicVolume = (float)m_sliderMusicVolume.FormsControl.Value / 100.0f;
      SaveSettings();
    }

    private void OnVolumeChangedCompleted(object sender, EventArgs e)
    {
      Console.WriteLine($"Music Volume: {m_sliderMusicVolume.FormsControl.Value}");
      _settings.MusicVolume = (float)m_sliderMusicVolume.FormsControl.Value / 100.0f;

      SaveSettings();
      AudioManager.Instance.MusicVolumeUpdated();
    }

    private void OnSfxVolumeChangedCompleted(object sender, EventArgs e)
    {
      Console.WriteLine($"Sfx Volume: {m_sliderSfxVolume.FormsControl.Value}");

      _settings.SfxVolume = (float)m_sliderSfxVolume.FormsControl.Value / 100.0f;

      SaveSettings();
      AudioManager.Instance.SfxVolumeUpdated();
    }

    internal Settings PlayerSettings => _settings;

    internal void SavePopoutBounds(int? x, int? y, int width, int height)
    {
      _settings.PopoutX = x;
      _settings.PopoutY = y;
      _settings.PopoutWidth = width;
      _settings.PopoutHeight = height;
      SaveSettings();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs args)
    {
      _settings.Language = LanguagePicker.Setting(m_comboBoxLanguage.FormsControl.SelectedIndex);
      SaveSettings();
      ApplyLanguage();
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
    }

    // Automatic follows the game language chosen in Steam, else the operating system's.
    private void ApplyLanguage()
    {
      var language = Loc.Resolve(_settings.Language, PlatformServices.GameLanguage);
      if (language != Loc.Current)
        Loc.SetLanguage(language);
      LanguagePicker.PinSelection(m_comboBoxLanguage, _settings.Language);
    }

    private void SaveSettings()
    {
      try
      {
        SaveJson("Settings.json", _settings, SettingsContext.Default.Settings);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void RefreshSettingsGuiValues()
    {
      var settings = _settings;

      m_sliderMusicVolume.FormsControl.ValueChanged -= OnVolumeChanged;
      m_sliderMusicVolume.FormsControl.ValueChangeCompleted -= OnVolumeChangedCompleted;
      m_sliderSfxVolume.FormsControl.ValueChangeCompleted -= OnSfxVolumeChangedCompleted;
      m_checkboxFullscreen.Click -= OnFullscreenCheckboxClicked;
      m_checkboxBorderless.Click -= OnBorderlessCheckboxClicked;
      m_checkboxVSync.Click -= OnVSyncCheckboxClicked;
      m_checkboxFixedTimeStep.Click -= OnFixedTimeStepCheckboxClicked;
      m_comboBoxResolution.FormsControl.SelectionChanged -= OnResolutionChanged;
      m_comboBoxLanguage.FormsControl.SelectionChanged -= OnLanguageChanged;

      Console.WriteLine($"Refreshing GUI values: MusicVolume={settings.MusicVolume}, SfxVolume={settings.SfxVolume}");

      m_sliderMusicVolume.FormsControl.Value = 1.1337;
      m_sliderSfxVolume.FormsControl.Value = 1.1337;

      m_sliderMusicVolume.FormsControl.Value = settings.MusicVolume * 100.0f;
      m_sliderSfxVolume.FormsControl.Value = settings.SfxVolume * 100.0f;

      m_checkboxFullscreen.FormsControl.IsChecked = settings.IsFullscreen;
      m_checkboxBorderless.FormsControl.IsChecked = settings.IsBorderless;
      m_checkboxVSync.FormsControl.IsChecked = settings.IsVSync;
      m_checkboxFixedTimeStep.FormsControl.IsChecked = settings.IsFixedTimeStep;

#if !KNI_WEB
      var uniqueResolutions = GraphicsDevice.Adapter.SupportedDisplayModes
          .ToArray()
          .Select(m => new { m.Width, m.Height })
          .Distinct()
          .OrderByDescending(m => m.Width)
          .ToList();

      int index = 0;
      int count = 0;

      foreach (var mode in uniqueResolutions)
      {
        if (mode.Width == _settings.Width && mode.Height == _settings.Height)
        {
          index = count;
        }

        ++count;
      }

      m_comboBoxResolution.FormsControl.SelectedIndex = index;

      if (index == 0 && _settings.Width == -1 && _settings.Height == -1)
      {
        _settings.Width = GraphicsDevice.Adapter.SupportedDisplayModes.Last().Width;
        _settings.Height = GraphicsDevice.Adapter.SupportedDisplayModes.Last().Height;
        SaveSettings();
      }

      m_comboBoxResolution.FormsControl.IsEnabled = true;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;
#endif

      m_sliderMusicVolume.FormsControl.ValueChanged += OnVolumeChanged;
      m_sliderMusicVolume.FormsControl.ValueChangeCompleted += OnVolumeChangedCompleted;
      m_sliderSfxVolume.FormsControl.ValueChangeCompleted += OnSfxVolumeChangedCompleted;
      m_checkboxFullscreen.Click += OnFullscreenCheckboxClicked;
      m_checkboxBorderless.Click += OnBorderlessCheckboxClicked;
      m_checkboxVSync.Click += OnVSyncCheckboxClicked;
      m_checkboxFixedTimeStep.Click += OnFixedTimeStepCheckboxClicked;
      m_comboBoxResolution.FormsControl.SelectionChanged += OnResolutionChanged;

      m_comboBoxLanguage.FormsControl.SelectedIndex = LanguagePicker.Index(_settings.Language);
      ApplyLanguage();
      m_comboBoxLanguage.FormsControl.SelectionChanged += OnLanguageChanged;

      Console.WriteLine($"Refreshed GUI values: MusicVolume={m_sliderMusicVolume.FormsControl.Value}, SfxVolume={m_sliderSfxVolume.FormsControl.Value}");
    }

    public static void PauseGame()
    {
      CurrentMenu = "GameMenu";
      IsPaused = true;
      DimmingFactor = 0.5f;
      DrawBlurFilter = true;

      SwapMenu("GameMenu");
    }

    public static void ResumeGame()
    {
      IsPaused = false;
      DimmingFactor = 0.0f;
      DrawBlurFilter = false;
      SwapMenu("");
    }

    public static bool TogglePauseGame()
    {
      if (IsPaused)
      {
        ResumeGame();
      }
      else
      {
        PauseGame();
      }

      return IsPaused;
    }

    public static string ActiveMenuScreen { get; private set; } = "MainMenu";

    private static GraphicalUiElement MenuPage(string menu) => menu switch
    {
      "MainMenu" => m_menuScreen,
      "GameMenu" => m_gameMenu,
      "SettingsMenu" => m_settingsMenu,
      "CreditsMenu" => m_creditsMenu,
      _ => null
    };

    // Pages that change into each other in place: the title screen's and the pause menu's.
    private static bool SameMenu(string page) => page switch
    {
      "MainMenu" => CurrentMenu == "MainMenu",
      "GameMenu" => CurrentMenu == "GameMenu",
      "SettingsMenu" or "CreditsMenu" => true,
      _ => false
    };

    private static void DetachMenuPages()
    {
      GumService.Default.Root.Children.Clear();
      RenderGuiSystem.Instance?.gameMenuItems?.Clear();
      m_gameMenu?.RemoveFromManagers();
      m_creditsMenu?.RemoveFromManagers();
      m_settingsMenu?.RemoveFromManagers();
    }

    // Over gameplay, menu pages draw in the paused game's own layer.
    private static void AttachMenuPage(GraphicalUiElement page)
    {
      if (CurrentMenu == "GameMenu")
      {
        page.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_gameMenuLayer);
        RenderGuiSystem.Instance.gameMenuItems.Add(page);
      }
      page.AddToRoot();
    }

    public static void SwapMenu(string menu)
    {
      PlaytestFeedback.Close();
      // The dropdown lives in Gum's popup root, outside the settings screen.
      // Close it explicitly so it cannot remain over another menu.
      if (m_settingsMenu?.GetChildByNameRecursively("ComboBoxResolution") is DefaultFromFileComboBoxRuntime resolution)
        resolution.FormsControl.IsDropDownOpen = false;
      if (m_settingsMenu?.GetChildByNameRecursively("ComboBoxLanguage") is DefaultFromFileComboBoxRuntime language)
        language.FormsControl.IsDropDownOpen = false;

      MenuTerminalTransition.Cancel();

      string previousActive = ActiveMenuScreen;
      ActiveMenuScreen = menu;
      DetachMenuPages();

      Console.WriteLine($"Swapping to menu: {menu}");

      var page = MenuPage(menu);
      if (page == null)
      {
        return;
      }

      if (page == m_menuScreen || page == m_gameMenu)
      {
        var camera = SystemManagers.Default.Renderer.Camera;
        camera.Zoom = 1.0f;
        camera.Position = System.Numerics.Vector2.Zero;
      }
      else
      {
        ConfigureSubmenuPosition(page, leftColumn: CurrentMenu != "GameMenu");
      }

      // The old page stays until the transition powers it down and swaps in the new one.
      var outgoingPage = previousActive != menu && SameMenu(previousActive) && SameMenu(menu)
        ? MenuPage(previousActive) : null;
      if (outgoingPage != null)
      {
        AttachMenuPage(outgoingPage);
        MenuTerminalTransition.Begin(outgoingPage, page, () =>
        {
          DetachMenuPages();
          MenuHoverShimmer.ResetPage(page);
          AttachMenuPage(page);
        });
        return;
      }

      MenuHoverShimmer.ResetPage(page);
      AttachMenuPage(page);
      // Opening the pause menu scans it in.
      if (page == m_gameMenu)
        MenuTerminalTransition.Begin(null, page, null);
    }

    private static void ConfigureSubmenuPosition(GraphicalUiElement submenu, bool leftColumn)
    {
      if (submenu == null) return;
      bool isSettings = ReferenceEquals(submenu, m_settingsMenu);
      if (submenu.GetChildByNameRecursively("LeftBackdrop") is GraphicalUiElement backdrop)
        backdrop.Visible = leftColumn;
      if (submenu.GetChildByNameRecursively("GuiDecorationTopRule") is GraphicalUiElement topRule)
        topRule.Visible = leftColumn;
      if (submenu.GetChildByNameRecursively("GuiDecorationCalibrationTicks") is GraphicalUiElement calTicks)
        calTicks.Visible = leftColumn;

      if (isSettings)
      {
        foreach (string modalPart in new[]
        {
          "NineSliceInstance", "WindowBrackets", "PanelTitle",
          "GuiDecorationDial", "GuiDecorationInnerDial", "GuiDecorationTicks"
        })
        {
          if (submenu.GetChildByNameRecursively(modalPart) is GraphicalUiElement part)
            part.Visible = !leftColumn;
        }
        if (submenu.GetChildByNameRecursively("PanelInstance2") is GraphicalUiElement panel2)
        {
          panel2.X = leftColumn ? 0f : 60f;
          panel2.Y = leftColumn ? 0f : 122f;
          panel2.Width = leftColumn ? 0f : -120f;
          panel2.Height = leftColumn ? 0f : -172f;
        }
        if (submenu.GetChildByNameRecursively("PanelButtons") is GraphicalUiElement panelButtons)
          panelButtons.Y = leftColumn ? 1148f : 1000f;
      }

      if (submenu.GetChildByNameRecursively("PanelInstance") is GraphicalUiElement panel)
      {
        if (leftColumn)
        {
          panel.Width = isSettings ? 680f : 960f;
          panel.Height = isSettings ? 1280f : 1200f;
          panel.X = 180f;
          panel.XOrigin = HorizontalAlignment.Left;
          panel.XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;
          panel.Y = isSettings ? 660f : 680f;
          panel.YOrigin = VerticalAlignment.Top;
          panel.YUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;
        }
        else
        {
          panel.Width = isSettings ? 800f : 960f;
          panel.Height = isSettings ? 1280f : 1200f;
          panel.X = 0f;
          panel.XOrigin = HorizontalAlignment.Center;
          panel.XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle;
          panel.Y = 0f;
          panel.YOrigin = VerticalAlignment.Center;
          panel.YUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle;
        }
        submenu.UpdateLayout();
      }
    }

    protected override void Update(GameTime gameTime)
    {
#if !KNI_WEB
      if (Capture.CaptureSession.Active)
        gameTime = captureTime = Capture.CaptureSession.BeginUpdate(this);
#endif
      MenuTerminalTransition.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
      PlaytestFeedback.RecordSessionTime(gameTime);
      PlatformServices.Update();
#if !KNI_WEB
      if (Demo.IsDev) soundEffectsLab?.Update();
#endif
      if (initialMenuPending && startupFrameDrawn)
      {
        initialMenuPending = false;
        InitializeMenus();
        LoadMainMenu();
      }
      GameplayPreloader.UpdateMenuLoads();
      if (GumProject != null) GumMenuFonts.Update();
      base.Update(gameTime);
      if (returnToMainMenuPending)
      {
        returnToMainMenuPending = false;
        // UnloadContent saves the outgoing world before the menu prepares a new one.
        ResumeGame();
        GumService.Default.ModalRoot.Children.Clear();
        LoadInitialScreen(_screenManager);
      }

#if !KNI_WEB
      TweenHelper.UpdateSetup(gameTime);
#endif
      //TODO: disabled for web
      //var time = gameTime.TotalGameTime.Ticks / TimeSpan.TicksPerMillisecond;
      //TweenHelper.UpdateSetup(time);
    }

    internal static GraphicalUiElement SettingsMenuScreen => m_settingsMenu;

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      if (GumProject == null)
        initialMenuPending = true;
      else
        LoadMainMenu();
    }

    private void LoadMainMenu()
    {
      _screenManager.ReplaceScreen(new MainMenu(this, m_menuScreen));

      // _screenManager.LoadScreen(new MainMenu(this, m_menuScreen));
      //_screenManager.ShowScreen(new MainMenu(this, m_menuScreen));

      CurrentMenu = "MainMenu";
      ActiveMenuScreen = "MainMenu";
      IsPaused = false;

      RefreshSettingsGuiValues();
    }

    // Captures: the title screen without touching the settings GUI (it would apply the capture's window size).
    internal void ShowMainMenu()
    {
      _screenManager.ReplaceScreen(new MainMenu(this, m_menuScreen));
      CurrentMenu = "MainMenu";
      ActiveMenuScreen = "MainMenu";
    }

    public static void AddCustomImGuiContent(Action ation)
    {
      ImGuiContent += ation;
    }

    public static void RemoveCustomImGuiContent(Action action)
    {
      ImGuiContent -= action;
    }

    public static void AddCustomHudContent(Action action)
    {
      HudContent += action;
    }

    public static void RemoveCustomHudContent(Action action)
    {
      HudContent -= action;
    }

    // Filter the entire 4K HUD when presenting it in a smaller window, including
    // Gum controls, tooltips, text, and custom SpriteBatch borders.
    protected override bool UseHudMipMaps => true;

    private string SelectedResolutionText()
    {
      var combo = m_comboBoxResolution.FormsControl;
      int index = combo.SelectedIndex;
      // File-defined item templates need not expose the source object through
      // ListBox.SelectedObject. The selected index maps to the source collection.
      return index >= 0 && index < combo.Items.Count ? combo.Items[index] as string : null;
    }

    private void PositionResolutionDropdown()
      => PositionResolutionDropdown(m_comboBoxResolution);

    internal static void PositionResolutionDropdown(DefaultFromFileComboBoxRuntime comboVisual)
    {
      if (comboVisual == null) return;
      var combo = comboVisual.FormsControl;
      // Keep the collapsed value tied to the source item, just like resolution changes.
      var selectionText = comboVisual.GetChildByNameRecursively("TextInstance");
      selectionText.Visible = true;
      selectionText.SetProperty("Text", combo.SelectedIndex >= 0 && combo.SelectedIndex < combo.Items.Count
        ? combo.Items[combo.SelectedIndex] as string ?? "" : "");
      if (!combo.IsDropDownOpen) return;
      // Forms positions popups in display pixels; this game draws Gum into a
      // virtual HUD target. Keep the popup in the same coordinates as its owner.
      var list = combo.ListBox.Visual;
      list.WidthUnits = list.HeightUnits = DimensionUnitType.Absolute;
      list.XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;
      list.YUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;
      list.XOrigin = HorizontalAlignment.Left;
      list.YOrigin = VerticalAlignment.Top;
      list.Width = comboVisual.AbsoluteRight - comboVisual.AbsoluteLeft;
      list.Height = 450;
      list.X = comboVisual.AbsoluteLeft - (list.Parent?.AbsoluteLeft ?? 0);
      list.Y = Math.Min(comboVisual.AbsoluteBottom + 8,
        GumService.Default.CanvasHeight - list.Height - 24) - (list.Parent?.AbsoluteTop ?? 0);
    }

#if !KNI_WEB
    private bool sealWindowAlpha;
    private BlendState opaqueWindowAlpha;
#endif

#if !KNI_WEB
    // Draw-time animation must follow the capture clock, not the wall clock.
    private GameTime captureTime;
#endif

    protected override void Draw(GameTime gameTime)
    {
#if !KNI_WEB
      if (Capture.CaptureSession.Active && captureTime != null)
        gameTime = captureTime;
      if (Capture.CaptureSession.Active)
        Capture.CaptureSession.BeginDraw(GraphicsDevice);
#endif
      MenuHoverShimmer.Time = (float)gameTime.TotalGameTime.TotalSeconds;
      if (initialMenuPending || GumProject == null || !GameplayPreloader.MenuReady)
      {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var font = FontManager.GetDefaultFont(150);
        string text = GameplayPreloader.MenuError == null ? "Loading..."
          : "Unable to load menu assets. Please restart the game.";
        var size = font.MeasureString(text);
        _spriteBatch.Begin();
        _spriteBatch.DrawString(font, text,
          new Vector2((GraphicsDevice.Viewport.Width - size.X) / 2f,
            (GraphicsDevice.Viewport.Height - size.Y) / 2f), Color.Yellow);
        _spriteBatch.End();
        startupFrameDrawn = true;
        return;
      }
      StartupReady = true;
      GuiThemeRuntime.Refresh();
      base.Draw(gameTime);
#if !KNI_WEB
      if (sealWindowAlpha)
      {
        // EGL transparency is enabled for SDL's whole video backend. Preserve
        // the main game's RGB image while keeping its desktop surface opaque,
        // including frames where the blur effect subtracts destination alpha.
        opaqueWindowAlpha ??= new BlendState
        {
          ColorWriteChannels = ColorWriteChannels.Alpha,
          AlphaSourceBlend = Blend.One,
          AlphaDestinationBlend = Blend.Zero
        };
        var viewport = GraphicsDevice.Viewport;
        var bounds = GraphicsDevice.PresentationParameters.Bounds;
        GraphicsDevice.Viewport = new Viewport(bounds);
        _spriteBatch.Begin(blendState: opaqueWindowAlpha);
        _spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, bounds, Color.White);
        _spriteBatch.End();
        GraphicsDevice.Viewport = viewport;
      }
      if (Capture.CaptureSession.Active)
        Capture.CaptureSession.EndDraw(this);
#endif
    }

    private void CloseUpgradePopout(object sender, EventArgs args)
    {
      AudioManager.Instance.StopMusic();
#if !KNI_WEB
      RenderGuiSystem.Instance?.DockUpgrades();
      popoutBatch?.Dispose();
      opaqueWindowAlpha?.Dispose();
      soundEffectsLab?.Dispose();
#endif
    }

    private SpriteBatch popoutBatch;

    public override void DrawHudLayer()
    {
      // Uses its own render target, so it runs while the just-cleared HUD target is still empty.
      RenderGuiSystem.Instance?.PrepareShipShowcase(GraphicsDevice);
#if KNI_WEB
      // The HUD is drawn into a render target, whose viewport can differ from
      // the startup window and change when the browser resizes.
      var textRenderer = FontManager.GetTextRenderer(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf));
      if (textRenderer != null)
      {
        textRenderer.SetOrtographicProjection(HudLayout.Width, HudLayout.Bottom);
        textRenderer.UseScreenSpace = false;
      }
#endif
#if !KNI_WEB
      if (RenderGuiSystem.Instance?.IsDetached == true)
      {
        popoutBatch ??= new SpriteBatch(GraphicsDevice);
        RenderGuiSystem.Instance.DrawDetachedHud(() => HudContent?.Invoke(), GraphicsDevice, popoutBatch);
      }
#endif
      PositionResolutionDropdown();
      PositionResolutionDropdown(m_comboBoxLanguage);
#if KNI_WEB
      using (Web.WebHudRenderScope.Begin())
#endif
      {
        RenderGuiSystem.Instance?.PrepareMenuTransition(GraphicsDevice);
        HudContent?.Invoke();
        base.DrawHudLayer();
      }
    }

    public override void DrawCustomImGuiContent(ImGuiRenderer _imGuiRenderer, GameTime gameTime)
    {
      _imGuiRenderer.BeginLayout(gameTime);
      ImGuiContent?.Invoke();
#if !KNI_WEB
      if (Demo.IsDev)
      {
        soundEffectsLab?.Draw();
        DebugGuiSprites.DrawStandalone();
      }
#endif
      _imGuiRenderer.EndLayout();

      base.DrawCustomImGuiContent(_imGuiRenderer, gameTime);
    }


    public static string GetPath(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
    public static T LoadJson<T>(string name, JsonTypeInfo<T> typeInfo) where T : new()
    {
      T json;
      string jsonPath = GetPath(name);

      Log.Information("Loading settings from: " + jsonPath);

      if (File.Exists(jsonPath))
      {
        json = JsonSerializer.Deserialize(File.ReadAllText(jsonPath), typeInfo)!;
      }
      else
      {
        json = new T();
      }

      return json;
    }

    public static void SaveJson<T>(string name, T json, JsonTypeInfo<T> typeInfo)
    {
      string jsonPath = GetPath(name);
      Log.Information("Saving settings to: " + jsonPath);
      Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
      string jsonString = JsonSerializer.Serialize(json, typeInfo);
      File.WriteAllText(jsonPath, jsonString);
    }

    public static T EnsureJson<T>(string name, JsonTypeInfo<T> typeInfo) where T : new()
    {
      T json;
      string jsonPath = GetPath(name);

      if (File.Exists(jsonPath))
      {
        json = JsonSerializer.Deserialize(File.ReadAllText(jsonPath), typeInfo)!;
      }
      else
      {
        json = new T();
        string jsonString = JsonSerializer.Serialize(json, typeInfo);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(jsonPath, jsonString);
      }

      return json;
    }

    private void SetFullscreen()
    {
      Log.Information("Setting Fullscreen Mode");

      _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
      _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
      _graphics.HardwareModeSwitch = !_settings.IsBorderless;

      _graphics.IsFullScreen = true;
      _graphics.ApplyChanges();
    }

    private void UnsetFullscreen()
    {
      Log.Information("Unsetting Fullscreen Mode");
      _graphics.IsFullScreen = false;
      _graphics.PreferredBackBufferWidth = _settings.Width;
      _graphics.PreferredBackBufferHeight = _settings.Height;
      _graphics.ApplyChanges();
      // RestoreWindow();
    }

    private void ApplyHardwareMode()
    {
      Log.Information("Applying Hardware Mode Switch (Borderless): " + (!_settings.IsBorderless).ToString());
      _graphics.HardwareModeSwitch = !_settings.IsBorderless;
      _graphics.ApplyChanges();
    }

    private void ApplyFullscreenChange(bool oldIsFullscreen)
    {
      if (_settings.IsFullscreen)
      {
        if (oldIsFullscreen)
        {
          ApplyHardwareMode();
        }
        else
        {
          SetFullscreen();
        }
      }
      else
      {
        UnsetFullscreen();
      }
    }

    public void ToggleFullscreen()
    {
      bool oldIsFullscreen = _settings.IsFullscreen;

      if (_settings.IsBorderless)
      {
        _settings.IsBorderless = false;
      }
      else
      {
        _settings.IsFullscreen = !_settings.IsFullscreen;
      }

      ApplyFullscreenChange(oldIsFullscreen);
    }

    public void ToggleBorderless()
    {
      bool oldIsFullscreen = _settings.IsFullscreen;

      _settings.IsBorderless = !_settings.IsBorderless;
      _settings.IsFullscreen = _settings.IsBorderless;

      ApplyFullscreenChange(oldIsFullscreen);
    }
  }
}
