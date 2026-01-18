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

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace UntitledGemGame
{
  public class GameMain() : BaseGame("UntitledGemGame", 3840, 2160, targetFps: 60.0f, fixedTimeStep: true, fullscreen: false)
  {
    public static event Action ImGuiContent;
    public static event Action HudContent;

    private static GameMain m_instance;
    public static BaseGame Instance => m_instance;
    GumService Gum => GumService.Default;
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

    public static string CurrentMenu = "MainMenu";
    private Settings _settings;

    public static bool IsPaused = false;

    protected override void Initialize()
    {
      m_instance = this;

      EnsureJson("Settings.json", SettingsContext.Default.Settings);
      _settings = LoadJson("Settings.json", SettingsContext.Default.Settings);

      IsFixedTimeStep = _settings.IsFixedTimeStep;
      _graphics.SynchronizeWithVerticalRetrace = _settings.IsVSync;
      // _graphics.HardwareModeSwitch = !_settings.IsBorderless;

      // if (_settings.IsFullscreen)
      // {
      //   ApplyFullscreenChange(false);
      // }

      GumProject = Gum.Initialize(
        this,
        "GumProject/BeyondTheBelt.gumx");

      var screen = GumProject.GetScreenSave("MainMenu");
      m_menuScreen = screen.ToGraphicalUiElement();
      m_menuScreen.AddToRoot();

      var settingsScreen = GumProject.GetScreenSave("SettingsMenu");
      m_settingsMenu = settingsScreen.ToGraphicalUiElement();

      var creditsScreen = GumProject.GetScreenSave("CreditsMenu");
      m_creditsMenu = creditsScreen.ToGraphicalUiElement();

      var gameScreen = GumProject.GetScreenSave("GameMenu");
      m_gameMenu = gameScreen.ToGraphicalUiElement();

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

        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      };

      backSettings.Click += back;
      backCredits.Click += back;

      m_comboBoxResolution = m_settingsMenu.GetChildByNameRecursively("ComboBoxResolution") as DefaultFromFileComboBoxRuntime;

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

      m_sliderMusicVolume = m_settingsMenu.GetChildByNameRecursively("SliderMusicVolume") as DefaultFromFileSliderRuntime;
      m_sliderMusicVolume.FormsControl.ValueChanged += OnVolumeChanged;
      m_sliderMusicVolume.FormsControl.ValueChangeCompleted += OnVolumeChangedCompleted;

      m_sliderSfxVolume = m_settingsMenu.GetChildByNameRecursively("SliderSfxVolume") as DefaultFromFileSliderRuntime;
      m_sliderSfxVolume.FormsControl.ValueChangeCompleted += OnSfxVolumeChangedCompleted;

      GraphicalUiElement graphicalUiElementByName = m_sliderSfxVolume.GetGraphicalUiElementByName("ThumbInstance");
      InteractiveGue sliderThumb = graphicalUiElementByName as InteractiveGue;
      sliderThumb.Click += (_, _) =>
      {
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
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

      var buttonReset = m_settingsMenu.GetChildByNameRecursively("ButtonReset") as DefaultFromFileButtonRuntime;

      buttonApply.Click += (_, _) =>
      {
        Console.WriteLine("Apply Settings");
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      };

      buttonReset.Click += (_, _) =>
      {
        Console.WriteLine("Reset Settings to Default");

        _settings = new Settings();
        AudioManager.Instance.SetSettings(_settings);

        RefreshSettingsGuiValues();

        OnResolutionChanged(this, null);

        AudioManager.Instance.MusicVolumeUpdated();
        AudioManager.Instance.SfxVolumeUpdated();
      };

      var continueButton = m_gameMenu.GetChildByNameRecursively("ButtonContinue") as DefaultFromFileButtonRuntime;
      continueButton.Click += (_, _) =>
      {
        Console.WriteLine("Continue Game Clicked");
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
        ResumeGame();
      };

      var exitToMainMenuButton = m_gameMenu.GetChildByNameRecursively("ButtonExitMainMenu") as DefaultFromFileButtonRuntime;
      exitToMainMenuButton.Click += (_, _) =>
      {
        Console.WriteLine("Exit to Main Menu Clicked");

        GumService.Default.Root.Children.Clear();

        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
        ResumeGame();

        LoadInitialScreen(_screenManager);
      };

      var settingsButton = m_gameMenu.GetChildByNameRecursively("ButtonSettings") as DefaultFromFileButtonRuntime;
      settingsButton.Click += (_, _) =>
      {
        Console.WriteLine("Settings Clicked");
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
        SwapMenu("SettingsMenu");
      };

      var creditsButton = m_gameMenu.GetChildByNameRecursively("ButtonCredits") as DefaultFromFileButtonRuntime;
      creditsButton.Click += (_, _) =>
      {
        Console.WriteLine("Credits Clicked");
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
        SwapMenu("CreditsMenu");
      };

      m_comboBoxResolution.FormsControl.IsEnabled = !_settings.IsFullscreen;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;

      AudioManager.Instance.SetSettings(_settings);
      AudioManager.Instance.MusicVolumeUpdated();

      ApplyResolutionChanged();
      base.Initialize();
    }

    void ApplyResolutionChanged()
    {
      _graphics.IsFullScreen = _settings.IsFullscreen;
      _graphics.HardwareModeSwitch = !_settings.IsBorderless;

      if (!_settings.IsFullscreen)
      {
        // int windowWidth = _graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
        // int windowHeight = _graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;
        // if (windowWidth == _settings.Width && windowHeight == _settings.Height)
        // {
        //   _graphics.PreferredBackBufferWidth = 100;
        //   _graphics.PreferredBackBufferHeight = 100;
        //   _graphics.ApplyChanges();
        //   _graphics.PreferredBackBufferWidth = _settings.Width;
        //   _graphics.PreferredBackBufferHeight = _settings.Height;
        //   return;
        // }

        _graphics.PreferredBackBufferWidth = _settings.Width;
        _graphics.PreferredBackBufferHeight = _settings.Height;
        // _graphics.PreferredBackBufferWidth = _settings.Width;
        // _graphics.PreferredBackBufferHeight = _settings.Height;
      }

      Log.Information($"Applying Resolution Change: {_settings.Width}x{_settings.Height}, Fullscreen={_settings.IsFullscreen}, Borderless={_settings.IsBorderless}");

      _graphics.ApplyChanges();
    }

    private void OnResolutionChanged(object arg1, SelectionChangedEventArgs args)
    {
      //TODO: check errors parsing etc
      var selectedText = m_comboBoxResolution.FormsControl.ListBox.SelectedObject as string;
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
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      _settings.IsFixedTimeStep = m_checkboxFixedTimeStep.FormsControl.IsChecked.Value;
      SaveSettings();

      IsFixedTimeStep = _settings.IsFixedTimeStep;
    }

    private void OnVSyncCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      _settings.IsVSync = m_checkboxVSync.FormsControl.IsChecked.Value;
      SaveSettings();

      _graphics.SynchronizeWithVerticalRetrace = _settings.IsVSync;
      _graphics.ApplyChanges();
    }

    private void OnBorderlessCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      _settings.IsBorderless = m_checkboxBorderless.FormsControl.IsChecked.Value;
      SaveSettings();

      ApplyHardwareMode();
    }

    private void OnFullscreenCheckboxClicked(object sender, EventArgs e)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      _settings.IsFullscreen = m_checkboxFullscreen.FormsControl.IsChecked.Value;
      SaveSettings();

      m_comboBoxResolution.FormsControl.IsEnabled = !_settings.IsFullscreen;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;

      ApplyFullscreenChange(!_settings.IsFullscreen);
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

    private void SaveSettings()
    {
      SaveJson("Settings.json", _settings, SettingsContext.Default.Settings);
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

      Console.WriteLine($"Refreshing GUI values: MusicVolume={settings.MusicVolume}, SfxVolume={settings.SfxVolume}");

      m_sliderMusicVolume.FormsControl.Value = 1.1337;
      m_sliderSfxVolume.FormsControl.Value = 1.1337;

      m_sliderMusicVolume.FormsControl.Value = settings.MusicVolume * 100.0f;
      m_sliderSfxVolume.FormsControl.Value = settings.SfxVolume * 100.0f;

      m_checkboxFullscreen.FormsControl.IsChecked = settings.IsFullscreen;
      m_checkboxBorderless.FormsControl.IsChecked = settings.IsBorderless;
      m_checkboxVSync.FormsControl.IsChecked = settings.IsVSync;
      m_checkboxFixedTimeStep.FormsControl.IsChecked = settings.IsFixedTimeStep;

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

      m_comboBoxResolution.FormsControl.IsEnabled = !_settings.IsFullscreen;
      m_checkboxBorderless.FormsControl.IsEnabled = _settings.IsFullscreen;

      m_sliderMusicVolume.FormsControl.ValueChanged += OnVolumeChanged;
      m_sliderMusicVolume.FormsControl.ValueChangeCompleted += OnVolumeChangedCompleted;
      m_sliderSfxVolume.FormsControl.ValueChangeCompleted += OnSfxVolumeChangedCompleted;
      m_checkboxFullscreen.Click += OnFullscreenCheckboxClicked;
      m_checkboxBorderless.Click += OnBorderlessCheckboxClicked;
      m_checkboxVSync.Click += OnVSyncCheckboxClicked;
      m_checkboxFixedTimeStep.Click += OnFixedTimeStepCheckboxClicked;
      m_comboBoxResolution.FormsControl.SelectionChanged += OnResolutionChanged;

      Console.WriteLine($"Refreshed GUI values: MusicVolume={m_sliderMusicVolume.FormsControl.Value}, SfxVolume={m_sliderSfxVolume.FormsControl.Value}");
    }

    public static void PauseGame()
    {
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

    public static void SwapMenu(string menu)
    {
      GumService.Default.Root.Children.Clear();
      RenderGuiSystem.Instance?.gameMenuItems?.Clear();
      m_gameMenu?.RemoveFromManagers();
      m_creditsMenu?.RemoveFromManagers();
      m_settingsMenu?.RemoveFromManagers();

      Console.WriteLine($"Swapping to menu: {menu}");

      if (menu == "")
      {
        return;
      }

      if (menu == "MainMenu")
      {
        var camera = SystemManagers.Default.Renderer.Camera;
        camera.Zoom = 1.0f;
        camera.Position = System.Numerics.Vector2.Zero;
        m_menuScreen.AddToRoot();
      }

      if (menu == "SettingsMenu")
      {
        if (CurrentMenu == "GameMenu")
        {
          m_gameMenu.RemoveFromManagers();
          m_settingsMenu.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_gameMenuLayer);
          RenderGuiSystem.Instance.gameMenuItems.Add(m_settingsMenu);
        }
        else
        {
          m_settingsMenu.AddToRoot();
        }
      }

      if (menu == "CreditsMenu")
      {
        if (CurrentMenu == "GameMenu")
        {
          m_creditsMenu.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_gameMenuLayer);
          RenderGuiSystem.Instance.gameMenuItems.Add(m_creditsMenu);
        }
        else
        {
          m_creditsMenu.AddToRoot();
        }
      }

      if (menu == "GameMenu")
      {
        m_gameMenu.RemoveFromManagers();
        m_creditsMenu.RemoveFromManagers();
        m_settingsMenu.RemoveFromManagers();

        var camera = SystemManagers.Default.Renderer.Camera;
        Renderer.UseBasicEffectRendering = true;
        camera.Zoom = 1.0f;
        camera.Position = System.Numerics.Vector2.Zero;

        m_gameMenu.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_gameMenuLayer);
        RenderGuiSystem.Instance.gameMenuItems.Add(m_gameMenu);
      }
    }

    private void ClearMenus()
    {
      GumService.Default.Root.Children.Clear();
      RenderGuiSystem.Instance.gameMenuItems.Clear();
      m_gameMenu.RemoveFromManagers();
      m_creditsMenu.RemoveFromManagers();
      m_settingsMenu.RemoveFromManagers();
    }

    protected override void Update(GameTime gameTime)
    {
      base.Update(gameTime);

      TweenHelper.UpdateSetup(gameTime);


      // var camera = SystemManagers.Default.Renderer.Camera;
      // Console.WriteLine($"Camera Position: {camera.Position.X}, {camera.Position.Y}, Zoom: {camera.Zoom}");
    }

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this, m_menuScreen));

      CurrentMenu = "MainMenu";
      IsPaused = false;

      RefreshSettingsGuiValues();
      // _graphics.ApplyChanges();

      base.LoadInitialScreen(screenManager);
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

    public override void DrawHudLayer()
    {
      HudContent?.Invoke();
      base.DrawHudLayer();
    }

    public override void DrawCustomImGuiContent(ImGuiRenderer _imGuiRenderer, GameTime gameTime)
    {
      _imGuiRenderer.BeginLayout(gameTime);
      ImGuiContent?.Invoke();
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
      // SaveWindow();
      //

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
