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

      var labelMusicVolume = m_settingsMenu.GetChildByNameRecursively("LabelMusicVolume") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileLabelRuntime;
      var labelSfxVolume = m_settingsMenu.GetChildByNameRecursively("LabelSfxVolume") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileLabelRuntime;
      var labelResolution = m_settingsMenu.GetChildByNameRecursively("LabelResolution") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileLabelRuntime;





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
