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
  public class GameMain() : BaseGame("UntitledGemGame", targetFps: 60.0f, fixedTimeStep: true, fullscreen: false)
  {
    public static event Action ImGuiContent;
    public static event Action HudContent;

    private static GameMain m_instance;
    public static BaseGame Instance => m_instance;
    GumService Gum => GumService.Default;
    public static GumService GumServiceUpgrades = new();
    public static GumProjectSave GumProject;

    private GraphicalUiElement m_menuScreen;

    protected override void Initialize()
    {
      m_instance = this;

      GumProject = Gum.Initialize(
        this,
        "GumProject/BeyondTheBelt.gumx");

      var screen = GumProject.GetScreenSave("MainMenu");
      m_menuScreen = screen.ToGraphicalUiElement();
      m_menuScreen.AddToRoot();

      base.Initialize();
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
