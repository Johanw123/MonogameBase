using System;
using System.IO;
using System.Threading;
using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using StbImageSharp;

namespace UntitledGemGame.Screens
{
  public class MainMenu : GameScreen
  {
    private SpriteBatch _spriteBatch;

    public MainMenu(Game game)
    : base(game)
    {
      game.IsMouseVisible = true;
    }


    public override void LoadContent()
    {
      base.LoadContent();

      _spriteBatch = new SpriteBatch(GraphicsDevice);

      FontManager.InitFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf);
    }

    public override void Update(GameTime gameTime)
    {
      var mouseState = MouseExtended.GetState();
      var keyboardState = KeyboardExtended.GetState();

      if (keyboardState.WasKeyReleased(Keys.Escape))
        Game.Exit();

      if (mouseState.LeftButton == ButtonState.Pressed || keyboardState.WasAnyKeyJustDown())
        ScreenManager.LoadScreen(new UntitledGemGameGameScreen(Game), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
    }

    public override void Draw(GameTime gameTime)
    {
      _spriteBatch.Begin();
      //_spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Hello World", new Vector2(10, 10), Color.Gold, Color.Black, 128);

      _spriteBatch.End();
    }
  }
}
