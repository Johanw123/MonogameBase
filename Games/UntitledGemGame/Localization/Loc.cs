using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AsyncContent;
using Serilog;

namespace UntitledGemGame.Localization;

// Player-facing text in the current language. The English text is its own key:
// Loc.T("Settings") looks it up in Content/Localization/<code>.json and falls back to the
// English when a language has no entry for it. Sentences with values use .NET placeholders:
// Loc.F("Requires {0} points.", points). Counted nouns use Loc.P, whose translation lists
// the language's plural forms (GameLanguage.PluralForms) separated by '|'.
//
// tools/localization/loc.py collects every key into Content/Localization/en.json: literals
// passed to T, F and N in the code, plus the names and tooltips in Content/Data. Text that is
// stored first and translated when shown (definition tables, enum names) is marked with
// Loc.N at its definition so the tool finds it. Run the tool after changing English text:
// it reports each language's missing and stale entries.
public static class Loc
{
  private static Dictionary<string, string> table;

  public static GameLanguage Current { get; private set; } = GameLanguage.English;
  // Number formatting of the current language. SetLanguage makes it the current culture,
  // so interpolated numbers follow the language too (data is parsed with InvariantCulture).
  public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

  // Raised after the language changes, for UI that caches translated text.
  public static event Action Changed;

  public static string T(string english)
  {
    if (table == null || english == null)
      return english;
    return table.TryGetValue(english, out var translated) ? translated : english;
  }

  public static string F(string english, object arg0) => Format(english, arg0);
  public static string F(string english, object arg0, object arg1) => Format(english, arg0, arg1);
  public static string F(string english, object arg0, object arg1, object arg2) => Format(english, arg0, arg1, arg2);
  public static string F(string english, params object[] args) => Format(english, args);

  // Marks English text for the string table without translating it yet.
  public static string N(string english) => english;

  // Upper case by the rules of the current language (Turkish dotted i, for example).
  public static string Upper(string text) => text?.ToUpper(Culture);

  // A counted noun phrase: Loc.P(cells, "{0} power cell", "{0} power cells"). The key is
  // "one|other"; a translation lists its language's forms in GameLanguage.PluralForms order.
  public static string P(long count, string one, string other)
  {
    string key = one + "|" + other;
    string forms = T(key);
    string form;
    if (ReferenceEquals(forms, key))
      form = count == 1 ? one : other;
    else
    {
      var parts = forms.Split('|');
      form = parts[Math.Min(Current.PluralForm(count), parts.Length - 1)];
    }
    return Format(form, count);
  }

  private static string Format(string english, params object[] args)
  {
    string template = T(english);
    try
    {
      return string.Format(Culture, template, args);
    }
    catch (FormatException)
    {
      // A translation with broken placeholders shows the English instead of crashing.
      Log.Warning("Bad placeholders in {Language} translation of \"{Text}\"", Current.Code, english);
      return string.Format(Culture, english, args);
    }
  }

  // The pieces a line of text may wrap between, each with the text joining it to the piece
  // before: words after spaces, single characters of Chinese and Japanese (except closing
  // punctuation, which stays with the character before it), and the words Thai marks with
  // zero-width spaces. Join the pieces of a line as line + Join + Text.
  public static IEnumerable<(string Text, string Join)> WrapPieces(string text)
  {
    var piece = new StringBuilder();
    string join = "";
    char previous = '\0';
    foreach (char c in text)
    {
      if (c is ' ' or '\u200B')
      {
        if (piece.Length > 0)
        {
          yield return (piece.ToString(), join);
          piece.Clear();
          join = "";
        }
        if (c == ' ')
          join = " ";
        previous = c;
        continue;
      }
      if (piece.Length > 0 && !NoBreakBefore(c) && (IsCjk(c) || IsCjk(previous)))
      {
        yield return (piece.ToString(), join);
        piece.Clear();
        join = "";
      }
      piece.Append(c);
      previous = c;
    }
    if (piece.Length > 0)
      yield return (piece.ToString(), join);
  }

  private static bool IsCjk(char c) =>
    c >= '\u2E80' && c <= '\u9FFF' || c >= '\uF900' && c <= '\uFAFF' || c >= '\uFF00' && c <= '\uFFEF';

  private static bool NoBreakBefore(char c) =>
    "、。，．・：；？！ー）」』】〕〉》｝］〜…％,.;:!?)]}%".IndexOf(c) >= 0;

  // The language a setting asks for: a language code, or empty for automatic (the game
  // language chosen in Steam, else the operating system's, else English).
  public static GameLanguage Resolve(string setting, string steamLanguage)
  {
    if (!string.IsNullOrEmpty(setting) && GameLanguage.FromCode(setting) is { } chosen)
      return chosen;
    if (!string.IsNullOrEmpty(steamLanguage) && GameLanguage.FromSteam(steamLanguage) is { } steam)
      return steam;
    return GameLanguage.FromCulture(CultureInfo.CurrentUICulture) ?? GameLanguage.English;
  }

  public static void SetLanguage(GameLanguage language)
  {
    language ??= GameLanguage.English;
    table = language == GameLanguage.English ? null : LoadTable(language);
    Current = language;
    Culture = SpecificCulture(language.Code);
    CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = Culture;
    CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = Culture;
    FontManager.SetFieldFontFace(GameFonts.Text, language.Font ?? GameFonts.TextPath);
    Changed?.Invoke();
  }

  private static CultureInfo SpecificCulture(string code)
  {
    try
    {
      return CultureInfo.CreateSpecificCulture(code);
    }
    catch (CultureNotFoundException)
    {
      return CultureInfo.InvariantCulture;
    }
  }

  // A content file, readable at startup before the content manager is set up.
  public static byte[] ContentBytes(string path) => AssetManager.GetFileBytes(Path.Combine("Content", path));

  public static Dictionary<string, string> LoadTable(GameLanguage language)
  {
    var strings = new Dictionary<string, string>(StringComparer.Ordinal);
    try
    {
      using var document = JsonDocument.Parse(ContentBytes($"Localization/{language.Code}.json"));
      foreach (var entry in document.RootElement.EnumerateObject())
      {
        var value = entry.Value.GetString();
        if (!string.IsNullOrEmpty(value))
          strings[entry.Name] = value;
      }
    }
    catch (Exception e)
    {
      Log.Error(e, "Could not load the {Language} string table", language.Code);
    }
    return strings;
  }
}

// The field font every player-facing text draws with, and its default face.
public static class GameFonts
{
  public const string Text = "Roboto_Regular_ttf";
  public const string TextPath = "Fonts/Roboto-Regular.ttf";
}
