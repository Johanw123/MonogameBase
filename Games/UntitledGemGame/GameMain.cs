using Base;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Screens;
using MonoGame.ImGuiNet;
using MonoGameGum;
using System;
using UntitledGemGame.Screens;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace UntitledGemGame
{
  public class GameMain() : BaseGame("UntitledGemGame", targetFps: 165.0f, fixedTimeStep: true, fullscreen: false)
  {
    public static event Action ImGuiContent;

    GumService Gum => GumService.Default;

    protected override void Initialize()
    {
      Gum.Initialize(this);

      // Window.AllowUserResizing = true;
      // base._graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
      // base._graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
      // base._graphics.IsFullScreen = true;
      // base._graphics.ApplyChanges();
      base.Initialize();
    }

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this));

      base.LoadInitialScreen(screenManager);
    }

    public static void AddCustomImGuiContent(Action ation)
    {
      ImGuiContent += ation;
    }

    public override void DrawCustomImGuiContent(ImGuiRenderer _imGuiRenderer, GameTime gameTime)
    {
      _imGuiRenderer.BeginLayout(gameTime);
      ImGuiContent?.Invoke();
      _imGuiRenderer.EndLayout();
    }
  }
}
