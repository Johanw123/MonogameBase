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
  public float FontSize = 18;
  public bool WrapText = false;

  public Color StrokeColor = Color.Transparent;
  public Color FillColor = Color.White;

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

  public Vector2 Measure2()
  {
    var position = new Vector2(
        this.GetAbsoluteLeft(),
        this.GetAbsoluteTop());

    // var camera = SystemManagers.Default.Renderer.Camera;

    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize;
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
    return measure;
  }

  public Vector2 Measure()
  {
    var position = new Vector2(
        this.GetAbsoluteLeft(),
        this.GetAbsoluteTop());

    var camera = SystemManagers.Default.Renderer.Camera;

    camera.WorldToScreen(position.X, position.Y, out var x, out var y);
    position = new Vector2(x, y);

    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize * camera.Zoom;
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
    return measure;
  }

  public override void Render(ISystemManagers managers)
  {
    var position = new Vector2(
        this.GetAbsoluteLeft(),
        this.GetAbsoluteTop());

    var camera = SystemManagers.Default.Renderer.Camera;

    camera.WorldToScreen(position.X, position.Y, out var x, out var y);
    position = new Vector2(x, y);

    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");

    var measure = Measure();

    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize * camera.Zoom;

    // position.Y -= measure.Y * 2.0f;

    if (TextAlignment == TextAlignment.Left)
    {
      // Console.WriteLine(this.Parent.Width);
      // r.LayoutText(Text, position, Color.White, Color.Transparent, fontSize, 0, new Vector2(0, 0), -1);
      r.SimpleLayoutText(Text, position, FillColor, StrokeColor, fontSize, -1, WrapText, (Parent.Width - 80) * camera.Zoom);

      // r.LayoutText(Text, position, FillColor, StrokeColor, fontSize, 0, new Vector2(0, 0), -1);
    }
    else if (TextAlignment == TextAlignment.Right)
    {
      r.LayoutText(Text, new Vector2(position.X - measure.X, position.Y), FillColor, StrokeColor, fontSize, 0, new Vector2(0, 0), -1);
    }
    else //center
      r.LayoutText(Text, new Vector2(position.X - measure.X / 2.0f, position.Y), FillColor, StrokeColor, fontSize, 0, new Vector2(0, 0), -1);
    // r.SimpleLayoutText(text, position, color, strokeColor, scale, -1, wrap, wrapAt);
    // r.RenderStroke();
    //
    // r.RenderStroke();
    // r.RenderText();
    r.RenderStrokedText();


  }


  public override void EndBatch(ISystemManagers systemManagers)
  {
    // _spriteBatch.End();
  }
}
