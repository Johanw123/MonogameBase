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

namespace HelloMonoGame
{
  public class GameMain : Game
  {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private readonly ScreenManager _screenManager;

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

      _screenManager.LoadScreen(new MainMenu(this), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
    }

    protected override void Update(GameTime gameTime)
    {
      KeyboardExtended.Update();
      MouseExtended.Update();

      base.Update(gameTime);
    }

    private void DrawText(SpriteBatch spriteBatch, string text)
    {
      var font30 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 10);
      var text_size = font30.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font30, text, new Vector2(pos_x, pos_y), Color.Yellow);
    }

    private bool showLoadingScreen = false;

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


      base.Draw(gameTime);



      if (AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawText(_spriteBatch, "Loading assets....");
        _spriteBatch.End();
      }
    }
  }
}
