using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using RenderingLibrary;
using RenderingLibrary.Graphics;

public class FontStashSharpText : RenderableBase
{
  static GraphicsDevice _graphicsDevice;
  static SpriteBatch _spriteBatch;

  public static OrthographicCamera m_camera;

  public static void Initialize(GraphicsDevice graphicsDevice)
  {
    _graphicsDevice = graphicsDevice;

    _spriteBatch = new SpriteBatch(graphicsDevice);

    // _fontSystem = new FontSystem();
    // _fontSystem.AddFont(System.IO.File.ReadAllBytes(@"Content/BROADW.TTF"));

    // FontManager.InitFieldFont("roboto", "JFContent/Fonts/Roboto-Reguar.ttf");
    // var ff = AssetManager.LoadAsync<FieldFont>("Content/Fonts/Roboto-Regular.ttf");
    // FontManager.InitFieldFont("roboto", ff);
  }

  public override string BatchKey => "FontStashSharp";

  public override void StartBatch(ISystemManagers systemManagers)
  {
    // _spriteBatch.Begin(rasterizerState: _graphicsDevice.RasterizerState);
  }
  public override void Render(ISystemManagers managers)
  {
    var position = new Vector2(
        this.GetAbsoluteLeft(),
        this.GetAbsoluteTop());

    position = m_camera.WorldToScreen(position);

    // var font = _fontSystem.GetFont(24);
    // _spriteBatch.DrawString(font,
    //     "Hi I am FontStashSharp",
    //     position,
    //     Color.White);

    FontManager.RenderFieldFont("Roboto_Regular_ttf", "test", position, Color.Red, Color.White, 74);
  }

  public override void EndBatch(ISystemManagers systemManagers)
  {
    // _spriteBatch.End();
  }
}
