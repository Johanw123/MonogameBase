using System;
using System.Diagnostics;
using System.IO;
using AsyncContent;
using FontStashSharp;
using HelloMonoGame.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;

// https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace HelloMonoGame
{
  public class GameMain : Game
  {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private readonly ScreenManager _screenManager;
    private bool showLoadingScreen = false;

    public GameMain()
    {
      _graphics = new GraphicsDeviceManager(this)
      {
        PreferredBackBufferWidth = 800,
        PreferredBackBufferHeight = 480,
        SynchronizeWithVerticalRetrace = false
      };

      Content.RootDirectory = "Content";

      IsFixedTimeStep = true;
      TargetElapsedTime = TimeSpan.FromSeconds(1f / 60f);
      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
    }

    protected override void Initialize()
    {
      AssetManager.Initialize(Content, GraphicsDevice);
      base.Initialize();
    }

    protected override void LoadContent()
    {
      _spriteBatch = new SpriteBatch(GraphicsDevice);
      FontManager.InitFont(() => ContentDirectory.Fonts.RandomWednesday);

      showLoadingScreen = true;
      _screenManager.LoadScreen(new MainMenu(this));

      AssetManager.BatchLoaded += () =>
      {
        showLoadingScreen = false;
      };
    }

    protected override void Update(GameTime gameTime)
    {
      KeyboardExtended.Update();
      MouseExtended.Update();

      base.Update(gameTime);
    }

    private void DrawText(SpriteBatch spriteBatch, string text)
    {
      var font30 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 40);
      var text_size = font30.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font30, text, new Vector2(pos_x, pos_y), Color.Yellow);
    }


    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.Black);

      if (showLoadingScreen && AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawText(_spriteBatch, "Loading...");
        _spriteBatch.End();
        return;
      }

      //Game itself is drawn here
      base.Draw(gameTime);

      DrawLoadingAssets();
    }

    private void DrawLoadingAssets()
    {
      if (!showLoadingScreen && AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        var text = "Loading Additional Assets...";
        var font30 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 30);
        var text_size = font30.MeasureString(text);
        var pos_x = 0;
        var pos_y = GraphicsDevice.Viewport.Height - text_size.Y;
        _spriteBatch.DrawString(font30, text, new Vector2(pos_x, pos_y), Color.Yellow);
        _spriteBatch.End();
      }
    }
  }
}
