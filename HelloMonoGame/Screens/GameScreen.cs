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
    private FontSystem _fontSystem;
    private SpriteFont _font;

    public HelloMonoGameGameScreen(Game game)
: base(game)
    {
      game.IsMouseVisible = true;
    }

    public override void LoadContent()
    {
      base.LoadContent();
      _spriteBatch = new SpriteBatch(GraphicsDevice);
      _fontSystem = new FontSystem();

      _font = Content.Load<SpriteFont>("font");
      _fontSystem.AddFont(AssetManager.GetFileBytes(ContentDirectory.Fonts.RandomWednesday));
    }

    public override void Update(GameTime gameTime)
    {
      // Update your game logic here
    }

    public override void Draw(GameTime gameTime)
    {
      _spriteBatch.Begin();

      _spriteBatch.DrawString(_font, "Hello World", new Vector2(10, 10), Color.Black);

      SpriteFontBase font30 = _fontSystem.GetFont(70);
      _spriteBatch.DrawString(font30, "Hello World2", new Vector2(0, 80), Color.Yellow);

      _spriteBatch.End();
    }
  }
}
