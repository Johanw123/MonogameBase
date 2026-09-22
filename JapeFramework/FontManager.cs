using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
using System.Xml.Linq;
using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;
using Serilog.Core;
using static System.Net.Mime.MediaTypeNames;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

public static class FontManager
{
  private static Dictionary<string, FontSystem> fontSystems = [];
  private static Dictionary<(string, float), DynamicSpriteFont> fontCache = new();
  private static Dictionary<string, AsyncAsset<FieldFont>> fieldFontCache = new();
  private static Dictionary<string, TextRenderer> fieldFontrenderers = new();

  private static GraphicsDevice m_graphicsDevice;
  private static bool initialized = false;
#if KNI_WEB
  private static SpriteBatch browserTextBatch;
  // PNG glyphs contain straight-alpha white RGB, including transparent pixels.
  // Convert to premultiplied output while compositing into the HUD target.
  public static readonly BlendState BrowserTextBlendState = new()
  {
    ColorSourceBlend = Blend.SourceAlpha,
    ColorDestinationBlend = Blend.InverseSourceAlpha,
    AlphaSourceBlend = Blend.One,
    AlphaDestinationBlend = Blend.InverseSourceAlpha
  };

  // Reuse Gum's browser-compatible atlas rather than the desktop field-font shader.
  private static RenderingLibrary.Graphics.BitmapFont browserFont;
  private static RenderingLibrary.Graphics.BitmapFont BrowserFont => browserFont ??= LoadBrowserFont();

  private static RenderingLibrary.Graphics.BitmapFont LoadBrowserFont()
  {
    // Read the current descriptor and its matching straight-alpha PNG directly.
    const string root = "Content/GumProject/FontCache/Font40Arial";
    using var descriptor = new StreamReader(TitleContainer.OpenStream(root + ".fnt"));
    using var image = TitleContainer.OpenStream(root + "_0.png");
    var texture = Texture2D.FromStream(m_graphicsDevice, image);
    return new RenderingLibrary.Graphics.BitmapFont(texture, descriptor.ReadToEnd());
  }
  private static float BrowserFontScale(float size) => Math.Max(1f, size) / 40f;

  private static string LayoutBrowserText(string text, float size, bool wrap, float width)
  {
    text ??= "";
    if (!wrap || width <= 0) return text;
    var font = BrowserFont;
    var lines = new List<string>();
    foreach (var paragraph in text.Replace("\r", "").Split('\n'))
    {
      var line = "";
      foreach (var word in paragraph.Split(' '))
      {
        var next = line.Length == 0 ? word : line + " " + word;
        if (line.Length > 0 && font.MeasureString(next, RenderingLibrary.Graphics.HorizontalMeasurementStyle.Full) * BrowserFontScale(size) > width)
        {
          lines.Add(line);
          line = word;
        }
        else line = next;
      }
      lines.Add(line);
    }
    return string.Join("\n", lines);
  }

  public static Vector2 MeasureBrowserText(string text, float size, bool wrap = false, float width = 0)
  {
    var lines = LayoutBrowserText(text, size, wrap, width).Split('\n');
    BrowserFont.GetRequiredWidthAndHeight(lines, out int measuredWidth, out int measuredHeight);
    return new Vector2(measuredWidth, measuredHeight) * BrowserFontScale(size);
  }

  public static void DrawBrowserText(SpriteBatch batch, string text, Vector2 position,
    Color color, Color strokeColor, float size, bool wrap = false, float width = 0,
    float alignment = 0)
  {
    var font = BrowserFont;
    var scale = BrowserFontScale(size);
    var lines = LayoutBrowserText(text, size, wrap, width).Split('\n');
    for (int line = 0; line < lines.Length; line++)
    {
      var lineWidth = font.MeasureString(lines[line], RenderingLibrary.Graphics.HorizontalMeasurementStyle.Full) * scale;
      var cursor = new System.Numerics.Vector2(position.X - lineWidth * alignment, position.Y);
      foreach (char character in lines[line])
      {
        var source = font.GetCharacterRect(character, line, ref cursor, out var destination,
          out int page, scale, 1f);
        var sourceRect = new Rectangle(source.X, source.Y, source.Width, source.Height);
        // Gum returns a line-relative Y, but advances the absolute X cursor.
        var target = new Rectangle((int)MathF.Round(destination.X), (int)MathF.Round(position.Y + destination.Y),
          (int)MathF.Round(destination.Width), (int)MathF.Round(destination.Height));
        batch.Draw(font.Textures[page], target, sourceRect, color);
      }
    }
  }


#endif

  public static void InitFontManager(GraphicsDevice graphicsDevice)
  {
    if (initialized)
      return;

    initialized = true;

    m_graphicsDevice = graphicsDevice;
#if KNI_WEB
    browserTextBatch = new SpriteBatch(graphicsDevice);
#else
    var _fontSystem = new FontSystem();
    _fontSystem.AddFont(DefaultFont.Font);
    fontSystems.Add("default", _fontSystem);
#endif
  }

  public static void InitFont(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    var value = property.Compile()();
    InitFont(name, value);
  }

  public static void InitFont(string name, string path)
  {
    if (fontSystems.ContainsKey(name))
      return;

    var _fontSystem = new FontSystem();
    var bytes = AssetManager.GetFileBytes(path);
    _fontSystem.AddFont(bytes);

    //File.WriteAllText("C:\\Users\\Johan\\source\\repos\\HelloMonoGame\\test.txt", "");
    //foreach (var b in bytes)
    //{
    //  File.AppendAllText("C:\\Users\\Johan\\source\\repos\\HelloMonoGame\\test.txt", b.ToString());
    //  File.AppendAllText("C:\\Users\\Johan\\source\\repos\\HelloMonoGame\\test.txt", ",");
    //}

    fontSystems.Add(name, _fontSystem);
  }

  //public static void InitFieldFont(Expression<Func<string>> property)
  //{
  //  var name = ((MemberExpression)property.Body).Member.Name;
  //  var value = property.Compile()();

  //  InitFieldFont(name, value);
  //}

  //Maybe add a way to send in effect here for customized shader
  public static void InitFieldFont(string name, string path)
  {
    //Roboto_Regular_ttf
    //Fonts/Roboto-Regular.ttf
    // if (fieldFontCache.ContainsKey(name))
    //  return;

    if(fieldFontrenderers.ContainsKey(name))
      return;

    // Console.WriteLine("InitFieldFont");
    //var font = AssetManager.LoadAsync<FieldFont>(path, true);
    //var textEffect = AssetManager.LoadAsync<Effect>("Shaders/DefaultFieldFontEffect.fx", true);

    var font = AssetManager.Load<FieldFont>(path);
    var textEffect = AssetManager.Load<Effect>("Shaders/DefaultFieldFontEffect.fx");
    //AssetManager.Load<FieldFont>(path);

    // Console.WriteLine("Resources got");
    // Console.WriteLine(font?.ToString() ?? "null");
    // Console.WriteLine(textEffect?.ToString() ?? "null");

    //fieldFontCache.Add(name, font);

    // Console.WriteLine("Textrenderer ctor");
    var textRenderer = new TextRenderer(font, m_graphicsDevice, textEffect);
    // Console.WriteLine("Textrenderer ctor fin");
    // var textRenderer = new TextRenderer(font, m_graphicsDevice, null);
    fieldFontrenderers.Add(name, textRenderer);
  }

  public static void InitFieldFont(string name, AsyncAsset<FieldFont> font)
  {
    if (fieldFontCache.ContainsKey(name))
      return;

    if(fieldFontrenderers.ContainsKey(name))
      return;


    var textEffect = AssetManager.LoadAsync<Effect>("Shaders/DefaultFieldFontEffect.fx", true);
    fieldFontCache.Add(name, font);
    // var textRenderer = new TextRenderer(font, m_graphicsDevice, null);
    var textRenderer = new TextRenderer(font, m_graphicsDevice, textEffect);
    fieldFontrenderers.Add(name, textRenderer);
  }

  public static void RenderFieldFont(Expression<Func<string>> property, string text, Vector2 position, Color color, Color strokeColor, float scale)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    //var value = property.Compile()();
    RenderFieldFont(name, text, position, color, strokeColor, scale);
  }

  public static void RenderFieldFont(string name, string text, Vector2 position, Color color, Color strokeColor, float scale, bool wrap = false, float wrapAt = 0)
  {
#if KNI_WEB
    browserTextBatch.Begin(blendState: BrowserTextBlendState);
    DrawBrowserText(browserTextBatch, text, position, color, strokeColor, scale, wrap, wrapAt);
    browserTextBatch.End();
#else
    fieldFontrenderers.TryGetValue(name, out var textRenderer);

    if (textRenderer?.Font == null || textRenderer?.Effect == null)
    {
      Utility.CallOnce(() =>
      {
        Log.Logger.Warning($"Font ({name}) cannot be rendered! Have you initialized it?");
      }); ;

      return;
    }

    textRenderer.ResetLayout();
    textRenderer.SimpleLayoutText(text, position, color, strokeColor, scale, -1, wrap, wrapAt);
    textRenderer.RenderStroke();
    textRenderer.RenderText();
#endif
  }

  // public static void PushText

  public static TextRenderer GetTextRenderer(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    //var value = property.Compile()();
    return GetTextRenderer(name);
  }

  public static TextRenderer GetTextRenderer(string name)
  {
    fieldFontrenderers.TryGetValue(name, out var textRenderer);
    return textRenderer;
  }

  public static AsyncAsset<FieldFont> GetFieldFont(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    return GetFieldFont(name);
  }

  public static AsyncAsset<FieldFont> GetFieldFont(string name)
  {
    fieldFontCache.TryGetValue(name, out var font);

    //TODO: error handling

    return font;
  }


  public static DynamicSpriteFont GetDefaultFont(float size = 30)
  {
    return GetFont("default", size);
  }

  public static DynamicSpriteFont GetFont(Expression<Func<string>> property, float size)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    return GetFont(name, size);
  }

  public static DynamicSpriteFont GetFont(string name, float size)
  {
    var cachFetched = fontCache.TryGetValue((name, size), out var font);

    if (!cachFetched)
    {
      var fontExists = fontSystems.TryGetValue(name, out var system);
      if (!fontExists)
      {
        Log.Error("Error loading font");
      }
      font = system.GetFont(size);

      fontCache.Add((name, size), font);
    }

    return font;
  }
}
