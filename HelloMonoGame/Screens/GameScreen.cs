using System;
using System.IO;
using AsyncContent;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Screens;

namespace HelloMonoGame.Screens
{
  public class HelloMonoGameGameScreen : GameScreen
  {
    private SpriteBatch _spriteBatch;

    public HelloMonoGameGameScreen(Game game)
: base(game)
    {
      game.IsMouseVisible = true;
    }

    public override void LoadContent()
    {
      base.LoadContent();
      _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    public override void Update(GameTime gameTime)
    {
      // Update your game logic here
    }

    public override void Draw(GameTime gameTime)
    {
      _spriteBatch.Begin();

      SpriteFontBase font30 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 30);
      _spriteBatch.DrawString(font30, "Hello World", new Vector2(10, 10), Color.Green);

      SpriteFontBase font70 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 70);
      _spriteBatch.DrawString(font70, "Hello World2", new Vector2(0, 80), Color.Yellow);

      // font70.DrawText(_spriteBatch, "allu", new Vector2(0, 200), Color.Yellow);

      _spriteBatch.End();
    }
  }
}
