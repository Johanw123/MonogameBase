using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HelloMonoGame
{
  public class Game1 : Game
  {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FontSystem _fontSystem;

    private SpriteFont _font;

    public Game1()
    {
      _graphics = new GraphicsDeviceManager(this);
      Content.RootDirectory = "Content";
      IsMouseVisible = true;
    }

    protected override void Initialize()
    {
      // TODO: Add your initialization logic here

      base.Initialize();
    }

    protected override void LoadContent()
    {
      _spriteBatch = new SpriteBatch(GraphicsDevice);

      _fontSystem = new FontSystem();

      _font = Content.Load<SpriteFont>("font");
      _fontSystem.AddFont(File.ReadAllBytes(@"Content/Random Wednesday.ttf"));
    }

    protected override void Update(GameTime gameTime)
    {
      if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
          Keyboard.GetState().IsKeyDown(Keys.Escape))
        Exit();

     
      base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.CornflowerBlue);

      _spriteBatch.Begin();
      
      _spriteBatch.DrawString(_font, "Hello World", new Vector2(10, 10), Color.Black);

      SpriteFontBase font30 = _fontSystem.GetFont(70);
      _spriteBatch.DrawString(font30, "Hello World2", new Vector2(0, 80), Color.Yellow);

      _spriteBatch.End();

      base.Draw(gameTime);
    }
  }
}
