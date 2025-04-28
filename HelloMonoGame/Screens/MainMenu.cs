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
    private FontSystem _fontSystem;

    public MainMenu(Game game)
    : base(game)
    {
      game.IsMouseVisible = true;
    }

    public override void LoadContent()
    {
      base.LoadContent();
      _spriteBatch = new SpriteBatch(GraphicsDevice);

      _background = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MainMenu.background_mainmenu);

      _fontSystem = new FontSystem();
      _fontSystem.AddFont(File.ReadAllBytes(ContentDirectory.Fonts.RandomWednesday));
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

    private void DrawText(SpriteBatch spriteBatch)
    {
      SpriteFontBase font30 = _fontSystem.GetFont(70);
      string text = "Press any key to start";
      var text_size = font30.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font30, text, new Vector2(pos_x, pos_y), Color.Yellow);
    }

    public override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.Magenta);

      if (AssetManager.IsLoadingContent())
        return;

      _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
      _spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
      DrawText(_spriteBatch);
      _spriteBatch.End();
    }
  }
}
