using System;
using System.IO;
using AsyncContent;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;

namespace HelloMonoGame.Screens
{
  public class MainMenu : GameScreen
  {
    private SpriteBatch _spriteBatch;
    private AsyncAsset<Texture2D> _background;

    public MainMenu(Game game)
    : base(game)
    {
      game.IsMouseVisible = true;
    }

    private AsyncAsset<Effect> effect;
    private AsyncAsset<Effect> effect2;
    public override void LoadContent()
    {
      base.LoadContent();

      _spriteBatch = new SpriteBatch(GraphicsDevice);
      _background = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MainMenu.background_mainmenu);
      effect = AssetManager.Load<Effect>(ContentDirectory.Shaders.effect);
      effect2 = AssetManager.Load<Effect>(ContentDirectory.Shaders.effect2);
    }

    public override void Update(GameTime gameTime)
    {
      var mouseState = MouseExtended.GetState();
      var keyboardState = KeyboardExtended.GetState();

      if (keyboardState.WasKeyReleased(Keys.Escape))
        Game.Exit();

      if (mouseState.LeftButton == ButtonState.Pressed || keyboardState.WasAnyKeyJustDown())
        ScreenManager.LoadScreen(new HelloMonoGameGameScreen(Game), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
    }

    private void DrawText(SpriteBatch spriteBatch, string text)
    {
      SpriteFontBase font30 = FontManager.GetFont(() => ContentDirectory.Fonts.RandomWednesday, 70);
      var text_size = font30.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font30, text, new Vector2(pos_x, pos_y), Color.Yellow);
    }

    public override void Draw(GameTime gameTime)
    {
      _spriteBatch.Begin(samplerState: SamplerState.PointClamp, effect: effect);
      _spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
      DrawText(_spriteBatch, "Press any key to start");
      _spriteBatch.End();
    }
  }
}
