using System.Text.Json;
using System.Text.RegularExpressions;
using UntitledGemGame.Localization;

// String tables: every shipped translation formats with its key's arguments, plural rules,
// wrapping of scripts without spaces, and English passing through untouched.
internal static class LocalizationChecks
{
  private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^{}]*)?\}");

  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    Check(Loc.Current == GameLanguage.English && Loc.T("Upgrades") == "Upgrades", "English must pass keys through");
    Check(Loc.P(1, "{0} cell", "{0} cells") == "1 cell" && Loc.P(3, "{0} cell", "{0} cells") == "3 cells",
      "English plurals must pick one/other");

    int[] Forms(string code, params long[] counts) =>
      counts.Select(c => GameLanguage.FromCode(code).PluralForm(c)).ToArray();
    Check(Forms("ru", 1, 2, 5, 11, 21, 22, 25).SequenceEqual(new[] { 0, 1, 2, 2, 0, 1, 2 }), "Russian plural rules");
    Check(Forms("pl", 1, 2, 5, 12, 21, 22).SequenceEqual(new[] { 0, 1, 2, 2, 2, 1 }), "Polish plural rules");
    Check(Forms("cs", 1, 3, 5).SequenceEqual(new[] { 0, 1, 2 }), "Czech plural rules");
    Check(Forms("fr", 0, 1, 2).SequenceEqual(new[] { 0, 0, 1 }), "French plural rules");
    Check(Forms("ja", 1, 2).SequenceEqual(new[] { 0, 0 }), "Japanese has one form");

    Check(Loc.WrapPieces("Gems fly home").SequenceEqual(new[] { ("Gems", ""), ("fly", " "), ("home", " ") }),
      "Words wrap at spaces");
    Check(Loc.WrapPieces("ジェム。集め").Select(p => p.Text).SequenceEqual(new[] { "ジ", "ェ", "ム。", "集", "め" }),
      "Japanese wraps between characters, keeping closing punctuation");
    Check(Loc.WrapPieces("ก​ข").SequenceEqual(new[] { ("ก", ""), ("ข", "") }), "Thai wraps at zero-width spaces");

    Check(Loc.Resolve("de", "japanese") == GameLanguage.FromCode("de"), "A chosen language wins over Steam");
    Check(Loc.Resolve("", "koreana") == GameLanguage.FromCode("ko"), "Automatic follows the Steam language");
    Check(Loc.Resolve("", "latam") == GameLanguage.FromCode("es-419"), "Steam's latam is Latin American Spanish");

    // Numbers follow the language: German shows decimal commas, English points.
    Loc.SetLanguage(GameLanguage.FromCode("de"));
    Check(Loc.F("{0:0.##}x VALUE", 1.5).StartsWith("1,5") && NumberFormatter.AbbreviateBigNumber(1_234_567) == "1,23M",
      "German formats numbers with decimal commas");
    Loc.SetLanguage(GameLanguage.English);
    Check(Loc.F("{0:0.##}x VALUE", 1.5) == "1.5x VALUE" && NumberFormatter.AbbreviateBigNumber(1_234_567) == "1.23M",
      "English formats numbers with decimal points");

    var english = JsonSerializer.Deserialize(File.ReadAllText("Content/Localization/en.json"),
      LocalizationJson.Default.DictionaryStringString)!;
    int tables = 0;
    foreach (var language in GameLanguage.All.Where(l => l != GameLanguage.English))
    {
      if (!File.Exists($"Content/Localization/{language.Code}.json"))
        continue;
      tables++;
      var table = Loc.LoadTable(language);
      Check(table.Count > 0, $"{language.Code}: the string table must load");
      foreach (var (key, value) in table)
      {
        Check(english.ContainsKey(key), $"{language.Code}: stale key {key}");
        string[] forms = key.Contains('|') ? value.Split('|') : new[] { value };
        string template = key.Contains('|') ? key.Split('|')[^1] : key;
        int arguments = Placeholder.Matches(template).Select(m => int.Parse(m.Groups[1].Value) + 1).DefaultIfEmpty(0).Max();
        if (arguments == 0)
          continue;
        var args = Enumerable.Range(0, arguments).Select(i => (object)(i + 2)).ToArray();
        foreach (string form in forms)
        {
          try
          {
            string.Format(System.Globalization.CultureInfo.InvariantCulture, form, args);
          }
          catch (FormatException)
          {
            throw new Exception($"{language.Code}: bad placeholders in {value} (for {key})");
          }
        }
      }
    }
    Console.WriteLine($"Localization checks passed: plurals, wrapping, language choice and {tables} string tables.");
  }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocalizationJson : System.Text.Json.Serialization.JsonSerializerContext { }
