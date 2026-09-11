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
  public float WrapRightPadding = 20;

  public Color StrokeColor = Color.Transparent;
  public Color FillColor = Color.White;

  public float GetWrapWidth()
  {
    if (Parent == null)
      return 0;

    float leftInset = Math.Max(0, this.GetAbsoluteLeft() - Parent.GetAbsoluteLeft());
    return Math.Max(0, Parent.Width - leftInset - WrapRightPadding);
  }

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
    _spriteBatch.Begin();
  }

#if !KNI_WEB
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
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize,
      Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown,
      r.PositionByBaseline, 0, Vector2.Zero, true, -1, WrapText, GetWrapWidth());
    return measure;
  }
#endif

  public Vector2 Measure()
  {
#if KNI_WEB
    return new Vector2(Text.Length * 15.0f, 55.0f);
#else
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
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize,
      Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown,
      r.PositionByBaseline, 0, Vector2.Zero, true, -1, WrapText,
      GetWrapWidth() * camera.Zoom);
    return measure;
#endif
  }

  public override void Render(ISystemManagers managers)
  {
#if KNI_WEB
    return;
#endif
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

    // position.Y -= measure.Y * 2.0f;

    //TODO: Optimize for tiny text if text is small
    if (fontSize < 22)
      r.OptimizeForTinyText = true;
    else
      r.OptimizeForTinyText = false;

    float horizontalAlignment = TextAlignment switch
    {
      TextAlignment.Center => 0.5f,
      TextAlignment.Right => 1f,
      _ => 0f,
    };
    r.LayoutText(Text, position, FillColor, StrokeColor, fontSize, 0, Vector2.Zero,
      -1, WrapText, GetWrapWidth() * camera.Zoom, horizontalAlignment);
    // r.SimpleLayoutText(text, position, color, strokeColor, scale, -1, wrap, wrapAt);
    // r.RenderStroke();
    //
    // r.RenderStroke();
    // r.RenderText();
    // r.RenderStrokedText();

    r.RenderStroke();
    r.RenderText();

    r.DrawSprites(_spriteBatch);
  }

  public override void EndBatch(ISystemManagers systemManagers)
  {
    _spriteBatch.End();
  }
}
