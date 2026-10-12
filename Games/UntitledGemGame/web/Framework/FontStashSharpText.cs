// Browser version of the shared Gum field-font adapter, with rendering and measurement enabled.
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
    _spriteBatch.Begin(transformMatrix: Matrix.CreateScale(0.5f));
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
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize,
      Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown,
      r.PositionByBaseline, 0, Vector2.Zero, true, -1, WrapText, GetWrapWidth());
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
    var measure = r.MeasureText(Text, position, 0, r.Font.LineHeight, fontSize,
      Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown,
      r.PositionByBaseline, 0, Vector2.Zero, true, -1, WrapText,
      GetWrapWidth() * camera.Zoom);
    return measure;
  }

  private string measuredText;
  private object measuredFont;
  private float measuredSize, measuredWrapWidth;
  private bool measuredWrap;
  private Vector2 measuredBounds;

  public override void Render(ISystemManagers managers)
  {
    var position = new Vector2(
        this.GetAbsoluteLeft(),
        this.GetAbsoluteTop());

    var camera = SystemManagers.Default.Renderer.Camera;

    camera.WorldToScreen(position.X, position.Y, out var x, out var y);
    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    float wrapWidth = GetWrapWidth();
    if (measuredText != Text || measuredSize != FontSize || measuredWrap != WrapText
        || measuredWrapWidth != wrapWidth || !ReferenceEquals(measuredFont, r.Font))
    {
      measuredBounds = r.MeasureText(Text, Vector2.Zero, 0, r.Font.LineHeight, FontSize,
        Color.Transparent, Color.Transparent, r.EnableKerning, true,
        r.PositionByBaseline, 0, Vector2.Zero, true, -1, WrapText, wrapWidth);
      measuredText = Text;
      measuredFont = r.Font;
      measuredSize = FontSize;
      measuredWrap = WrapText;
      measuredWrapWidth = wrapWidth;
    }
    float width = measuredBounds.X * camera.Zoom;
    float height = measuredBounds.Y * camera.Zoom;
    float left = x - (TextAlignment == TextAlignment.Center ? width * 0.5f
      : TextAlignment == TextAlignment.Right ? width : 0);
    float margin = FontSize * camera.Zoom;
    var viewport = _graphicsDevice.Viewport;
    if (left + width + margin < 0 || left - margin > viewport.Width
        || y + height + margin < 0 || y - margin > viewport.Height) return;

    // Field-font drawing uses the virtual HUD projection, while Gum's camera
    // is temporarily scaled to the physical target for this draw.
    position = new Vector2(x, y) * 2f;
    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize * camera.Zoom * 2f;

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
      -1, WrapText, GetWrapWidth() * camera.Zoom * 2f, horizontalAlignment);
    // r.SimpleLayoutText(text, position, color, strokeColor, scale, -1, wrap, wrapAt);
    // r.RenderStroke();
    //
    // r.RenderStroke();
    // r.RenderText();
    // r.RenderStrokedText();

    FontManager.DrawFieldFontLayout(r);

    r.DrawSprites(_spriteBatch);
  }

  public override void EndBatch(ISystemManagers systemManagers)
  {
    _spriteBatch.End();
  }
}
