using System;
using System.Collections.Generic;
using System.Linq;
using Gum.Localization;

namespace UntitledGemGame.Localization;

// Gum translates the text of its elements (the menu screens made in the Gum tool, and text
// the code assigns to TextRuntime.Text) through this, and re-translates live text when the
// language changes. The strings come from Loc's tables.
internal sealed class GumLocalization : ILocalizationService
{
  public GumLocalization() => Loc.Changed += () => CurrentLanguageChanged?.Invoke();

  public int CurrentLanguage
  {
    get => Array.IndexOf(GameLanguage.All, Loc.Current);
    set => Loc.SetLanguage(GameLanguage.All[value]);
  }

  public IReadOnlyList<string> Languages { get; } = GameLanguage.All.Select(l => l.Code).ToArray();

  public event Action CurrentLanguageChanged;

  public string Translate(string stringId)
  {
    // Gum's bitmap fonts have no zero-width space (the wrap points of Thai text).
    string translated = Loc.T(stringId)?.Replace("\u200B", "");
    if (!string.IsNullOrEmpty(translated))
      GumMenuFonts.Shown(stringId, translated);
    return translated;
  }

  // The tables are Loc's; Gum's own databases are not used.
  public void AddDatabase(Dictionary<string, string[]> entryDictionary, List<string> headerList) { }

  public void Clear() { }
}
