using Gum.Forms;
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
using RenderingLibrary;
using System.Linq;

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

    protected override void Initialize()
    {
      m_instance = this;

      GumProject = Gum.Initialize(
        this,
        "GumProject/BeyondTheBelt.gumx");

      var screen = GumProject.GetScreenSave("MainMenu");
      m_menuScreen = screen.ToGraphicalUiElement();
      m_menuScreen.AddToRoot();

      var settingsScreen = GumProject.GetScreenSave("SettingsMenu");
      m_settingsMenu = settingsScreen.ToGraphicalUiElement();

      var back = m_settingsMenu.GetChildByNameRecursively("ButtonBack") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      back.Click += (_, _) =>
      {
        SwapMenu("MainMenu");
        AudioManager.Instance.MenuClickButtonSoundEffect.Play();
      };

      var combo = m_settingsMenu.GetChildByNameRecursively("ComboBoxResolution") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileComboBoxRuntime;

      var uniqueResolutions = GraphicsDevice.Adapter.SupportedDisplayModes
          .ToArray()
          .Select(m => new { m.Width, m.Height })
          .Distinct()
          .OrderByDescending(m => m.Width)
          .ToList();

      foreach (var mode in uniqueResolutions)
      {
        combo.FormsControl.ListBox.Items.Add($"{mode.Width} x {mode.Height}");
      }


      var sliderMusicVolume = m_settingsMenu.GetChildByNameRecursively("SliderMusicVolume") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime;
      sliderMusicVolume.FormsControl.ValueChanged += (_, _) =>
      {
        Console.WriteLine($"Music Volume: {sliderMusicVolume.FormsControl.Value}");
      };

      var sliderSfxVolume = m_settingsMenu.GetChildByNameRecursively("SliderSfxVolume") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime;
      sliderSfxVolume.FormsControl.ValueChanged += (_, _) =>
      {
        Console.WriteLine($"Sfx Volume: {sliderSfxVolume.FormsControl.Value}");
      };



      var buttonApply = m_settingsMenu.GetChildByNameRecursively("ButtonApply") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var checkboxFullscreen = m_settingsMenu.GetChildByNameRecursively("CheckBoxFullscreen") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileCheckBoxRuntime;

      var buttonReset = m_settingsMenu.GetChildByNameRecursively("ButtonReset") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;

      buttonApply.Click += (_, _) =>
      {
        Console.WriteLine("Apply Settings");
      };
      buttonReset.Click += (_, _) =>
      {
        Console.WriteLine("Reset Settings to Default");
      };
      checkboxFullscreen.FormsControl.Checked += (_, _) =>
      {
        Console.WriteLine($"Fullscreen: checked");
      };
      checkboxFullscreen.FormsControl.Unchecked += (_, _) =>
      {
        Console.WriteLine($"Fullscreen: unchecked");
      };

      base.Initialize();
    }

    public static void SwapMenu(string menu)
    {
      if (menu == "MainMenu")
      {
        GumService.Default.Root.Children.Clear();
        m_menuScreen.AddToRoot();
      }

      if (menu == "SettingsMenu")
      {

        GumService.Default.Root.Children.Clear();
        m_settingsMenu.AddToRoot();
      }
    }

    protected override void Update(GameTime gameTime)
    {
      base.Update(gameTime);

      TweenHelper.UpdateSetup(gameTime);
    }

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this, m_menuScreen));

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
  }
}
