using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using RenderingLibrary;
using RenderingLibrary.Graphics;

public enum TextAlignment
{
  Left,
  Center,
  Right
}

public class FontStashSharpText : RenderableBase
{
  static GraphicsDevice _graphicsDevice;
  static SpriteBatch _spriteBatch;

  public static OrthographicCamera m_camera;

  public TextAlignment TextAlignment = TextAlignment.Left;
  public string Text;
  public bool WrapText = false;

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

    var camera = SystemManagers.Default.Renderer.Camera;

    camera.WorldToScreen(position.X, position.Y, out var x, out var y);
    position = new Vector2(x, y);

    // Console.WriteLine(this.Width * camera.Zoom);
    // Console.WriteLine(this.Parent.Width * camera.Zoom);

    // FontManager.RenderFieldFont("Roboto_Regular_ttf", Text, position, Color.White, Color.Transparent, 18 * camera.Zoom, false, this.Parent.Width * camera.Zoom);
    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");

    // r.PositionByBaseline = true;

    r.ResetLayout();
    //TODO : add measure text method
    var fontSize = 18 * camera.Zoom;
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize, Color.White, Color.White, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);

    if (TextAlignment == TextAlignment.Left)
    {
      // Console.WriteLine(this.Parent.Width);
      // r.LayoutText(Text, position, Color.White, Color.Transparent, fontSize, 0, new Vector2(0, 0), -1);
      r.SimpleLayoutText(Text, position, Color.White, Color.Transparent, fontSize, -1, WrapText, (Parent.Width - 60) * camera.Zoom);
    }
    else if (TextAlignment == TextAlignment.Right)
    {
      r.LayoutText(Text, new Vector2(position.X - measure.X, position.Y), Color.White, Color.Transparent, fontSize, 0, new Vector2(0, 0), -1);
    }
    else //center
      r.LayoutText(Text, new Vector2(position.X - measure.X / 2.0f, position.Y), Color.White, Color.Transparent, fontSize, 0, new Vector2(0, 0), -1);
    // r.SimpleLayoutText(text, position, color, strokeColor, scale, -1, wrap, wrapAt);
    // r.RenderStroke();
    r.RenderText();
  }


  public override void EndBatch(ISystemManagers systemManagers)
  {
    // _spriteBatch.End();
  }
}
