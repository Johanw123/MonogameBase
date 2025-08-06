using System;
using System.IO;
using AsyncContent;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Screens;

namespace FrogFight.Screens
{
  public class FrogFightGameScreen : GameScreen
  {
    private SpriteBatch _spriteBatch;

    public FrogFightGameScreen(Game game)
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

      var textRenderer = FontManager.GetTextRenderer(() => ContentDirectory.Fonts.RandomWednesday);

      textRenderer.ResetLayout();
      textRenderer.SimpleLayoutText("Hello World", new Vector2(10, 10), Color.Green, Color.Black, 30);
      textRenderer.SimpleLayoutText("Hello World", new Vector2(0, 80), Color.Yellow, Color.Black, 70);
      textRenderer.RenderText();

      _spriteBatch.End();
    }
  }
}
