using System;
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
      AsyncContent.AssetManager.Initialize(Content, GraphicsDevice);
      base.Initialize();
    }

    protected override void LoadContent()
    {
      _spriteBatch = new SpriteBatch(GraphicsDevice);

      _screenManager.LoadScreen(new MainMenu(this), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
    }

    protected override void Update(GameTime gameTime)
    {
      KeyboardExtended.Update();
      MouseExtended.Update();

      base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.CornflowerBlue);
      base.Draw(gameTime);
    }
  }
}
