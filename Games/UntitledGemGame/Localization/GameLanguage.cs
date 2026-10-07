using System;
using System.Globalization;

namespace UntitledGemGame.Localization;

// A language the game ships. Code names the string table (Content/Localization/<Code>.json),
// SteamName is what SteamApps.GetCurrentGameLanguage reports for it, and Font replaces Roboto
// for scripts Roboto has no glyphs for (its atlas lists only the characters the table uses,
// so rebuild it with tools/localization/loc.py fonts after changing that table).
public sealed record GameLanguage(string Code, string NativeName, string EnglishName, string SteamName, string Font = null)
{
  public static readonly GameLanguage English = new("en", "English", "English", "english");

  public static readonly GameLanguage[] All =
  [
    English,
    new("fr", "Français", "French", "french"),
    new("de", "Deutsch", "German", "german"),
    new("it", "Italiano", "Italian", "italian"),
    new("es-ES", "Español (España)", "Spanish - Spain", "spanish"),
    new("es-419", "Español (Latinoamérica)", "Spanish - Latin America", "latam"),
    new("pt-BR", "Português (Brasil)", "Portuguese - Brazil", "brazilian"),
    new("pt-PT", "Português (Portugal)", "Portuguese - Portugal", "portuguese"),
    new("ca", "Català", "Catalan", "catalan"),
    new("pl", "Polski", "Polish", "polish"),
    new("cs", "Čeština", "Czech", "czech"),
    new("ru", "Русский", "Russian", "russian"),
    new("tr", "Türkçe", "Turkish", "turkish"),
    new("id", "Bahasa Indonesia", "Indonesian", "indonesian"),
    new("vi", "Tiếng Việt", "Vietnamese", "vietnamese"),
    new("th", "ไทย", "Thai", "thai", "Fonts/Sarabun-Regular.ttf"),
    new("ja", "日本語", "Japanese", "japanese", "Fonts/NotoSansJP-Regular.otf"),
    new("ko", "한국어", "Korean", "koreana", "Fonts/NotoSansKR-Regular.otf"),
    new("zh-Hans", "简体中文", "Simplified Chinese", "schinese", "Fonts/NotoSansSC-Regular.otf"),
  ];

  // The plural forms a counted phrase takes in this language, in the order Loc.P expects
  // its translations (CLDR categories for whole numbers).
  public string PluralForms => Code switch
  {
    "ru" or "pl" => "one|few|many",
    "cs" => "one|few|other",
    "ja" or "ko" or "zh-Hans" or "th" or "vi" or "id" => "other",
    _ => "one|other",
  };

  public int PluralForm(long count)
  {
    long n = Math.Abs(count), mod10 = n % 10, mod100 = n % 100;
    bool few = mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14;
    return Code switch
    {
      "ru" => mod10 == 1 && mod100 != 11 ? 0 : few ? 1 : 2,
      "pl" => n == 1 ? 0 : few ? 1 : 2,
      "cs" => n == 1 ? 0 : n is >= 2 and <= 4 ? 1 : 2,
      "ja" or "ko" or "zh-Hans" or "th" or "vi" or "id" => 0,
      "fr" or "pt-BR" => n <= 1 ? 0 : 1,
      _ => n == 1 ? 0 : 1,
    };
  }

  public static GameLanguage FromCode(string code)
  {
    foreach (var language in All)
      if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
        return language;
    return null;
  }

  public static GameLanguage FromSteam(string steamName)
  {
    foreach (var language in All)
      if (string.Equals(language.SteamName, steamName, StringComparison.OrdinalIgnoreCase))
        return language;
    return null;
  }

  // The closest shipped language to an operating system culture, or null.
  public static GameLanguage FromCulture(CultureInfo culture)
  {
    for (; culture != null && !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
    {
      string name = culture.Name;
      if (FromCode(name) is { } exact)
        return exact;
      switch (culture.TwoLetterISOLanguageName)
      {
        case "es": return FromCode(name == "es" || name.StartsWith("es-ES") ? "es-ES" : "es-419");
        case "pt": return FromCode(name.StartsWith("pt-BR") ? "pt-BR" : "pt-PT");
        case "zh": return name.Contains("Hant") || name is "zh-TW" or "zh-HK" or "zh-MO" ? null : FromCode("zh-Hans");
      }
      if (FromCode(culture.TwoLetterISOLanguageName) is { } language)
        return language;
    }
    return null;
  }
}
