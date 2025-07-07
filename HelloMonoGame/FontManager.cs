using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using AsyncContent;
using FontStashSharp;

public static class FontManager
{
  private static Dictionary<string, FontSystem> fontSystems = [];
  private static Dictionary<(string, float), DynamicSpriteFont> fontCache = new();

  public static void InitFont(Expression<Func<string>> property)
  {
    var name = ((MemberExpression)property.Body).Member.Name;
    var value = property.Compile()();
    InitFont(name, value);
  }

  public static void InitFont(string name, string path)
  {
    var _fontSystem = new FontSystem();
    _fontSystem.AddFont(AssetManager.GetFileBytes(path));
    fontSystems.Add(name, _fontSystem);
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
        Console.WriteLine("Error loading font");
      }
      font = system.GetFont(size);

      fontCache.Add((name, size), font);
    }

    return font;
  }
}
