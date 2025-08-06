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
    private AsyncAsset<Effect> effect3;

    public override void LoadContent()
    {
      base.LoadContent();

      _spriteBatch = new SpriteBatch(GraphicsDevice);
      _background = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MainMenu.background_mainmenu);
      effect = AssetManager.Load<Effect>(ContentDirectory.Shaders.effect);
      effect3 = AssetManager.Load<Effect>(ContentDirectory.Shaders.MoreShaders.effect);

      FontManager.InitFieldFont(() => ContentDirectory.Fonts.Consolas);
      FontManager.InitFieldFont(() => ContentDirectory.Fonts.RandomWednesday);
      FontManager.InitFieldFont(() => ContentDirectory.Fonts.MoreFonts.Freedom_10eM);
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

    public override void Draw(GameTime gameTime)
    {
      _spriteBatch.Begin();
      _spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.RandomWednesday, "Hello World", new Vector2(10, 10), Color.Gold, Color.Black, 128);

      _spriteBatch.End();
    }
  }
}
