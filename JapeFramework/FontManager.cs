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

public static class FontManager
{
  private static Dictionary<string, FontSystem> fontSystems = [];
  private static Dictionary<(string, float), DynamicSpriteFont> fontCache = new();
  private static Dictionary<string, AsyncAsset<FieldFont>> fieldFontCache = new();
  private static Dictionary<string, TextRenderer> fieldFontrenderers = new();

  private static GraphicsDevice m_graphicsDevice;

  public static void InitFontManager(GraphicsDevice graphicsDevice)
  {
    m_graphicsDevice = graphicsDevice;

    var _fontSystem = new FontSystem();
    _fontSystem.AddFont(DefaultFont.Font);
    fontSystems.Add("default", _fontSystem);
  }

  public static void InitFont(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    var value = property.Compile()();
    InitFont(name, value);
  }

  public static void InitFont(string name, string path)
  {
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

  public static void InitFieldFont(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    var value = property.Compile()();

    InitFieldFont(name, value);
  }

  //Maybe add a way to send in effect here for customized shader
  public static void InitFieldFont(string name, string path)
  {
    var font = AssetManager.LoadAsync<FieldFont>(path, true);
    var textEffect = AssetManager.LoadAsync<Effect>("JFContent/Shaders/DefaultFieldFontEffect.mgfx", true);

    fieldFontCache.Add(name, font);
    var textRenderer = new TextRenderer(font, m_graphicsDevice, textEffect);
    fieldFontrenderers.Add(name, textRenderer);
  }

  public static void RenderFieldFont(Expression<Func<string>> property, string text, Vector2 position, Color color, Color strokeColor, float scale)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    //var value = property.Compile()();
    RenderFieldFont(name, text, position, color, strokeColor, scale);
  }

  public static void RenderFieldFont(string name, string text, Vector2 position, Color color, Color strokeColor, float scale)
  {
    fieldFontrenderers.TryGetValue(name, out var textRenderer);

    if (textRenderer?.Font == null || textRenderer?.Effect == null)
    {
      Utility.CallOnce(() =>
      {
        Log.Logger.Warning($"Font ({name}) cannot be rendered! Have you initialized it?");
      });
      
      return;
    }

    textRenderer.ResetLayout();
    textRenderer.SimpleLayoutText(text, position, color, strokeColor, scale);
    textRenderer.RenderText();
  }

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
