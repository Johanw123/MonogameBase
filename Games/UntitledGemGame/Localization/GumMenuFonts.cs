using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsyncContent;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using KernSmith.Gum;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using RenderingLibrary.Graphics.Fonts;
using Serilog;

namespace UntitledGemGame.Localization;

// Gum's menu text uses the prebaked Arial bitmap fonts in Content/GumProject/FontCache, which
// hold Latin-1 only. When the current language's menu text needs more, every Gum text
// switches to a font family named after the language, and TryCreateFont bakes those fonts
// in memory (KernSmith) from the language's font with just the characters the menus use.
// Language names in the language picker are each drawn in their own language's font
// (PinLanguageName), so every name is readable whatever the current language is.
internal sealed class GumMenuFonts : IInMemoryFontCreator
{
  private const string DiskFamily = "Arial";
  private const string MenuPrefix = "Gum-";
  private const string NamePrefix = "Name-";

  private static GumMenuFonts instance;

  private readonly KernSmithFontCreator creator;
  private readonly HashSet<string> registered = new();
  // English text Gum shows: Text values in the Gum project, plus what the code assigns.
  private readonly HashSet<string> menuKeys = new(StringComparer.Ordinal);
  private readonly List<GraphicalUiElement> roots = new();
  private readonly Dictionary<TextRuntime, string> pinned = new();
  // The characters the current family's fonts hold; a text needing more bumps the family.
  private readonly HashSet<char> covered = new();
  private string family = DiskFamily;
  private int generation;
  private bool refreshPending;

  private GumMenuFonts(GraphicsDevice device) => creator = new KernSmithFontCreator(device);

  public static void Install(GraphicsDevice device, GumProjectSave project)
  {
    instance = new GumMenuFonts(device);
    CustomSetPropertyOnRenderable.InMemoryFontCreator = instance;
    foreach (var element in project.Screens.Cast<ElementSave>().Concat(project.Components))
      foreach (var variable in element.AllStates.SelectMany(s => s.Variables))
        if (variable.Name.EndsWith("Text") && variable.Value is string text && text.Length > 0)
          instance.menuKeys.Add(text);
    Loc.Changed += instance.OnLanguageChanged;
    instance.OnLanguageChanged();
  }

  // Menus that are not always attached to Gum's root still follow language changes.
  public static void Track(GraphicalUiElement root)
  {
    if (instance == null || root == null)
      return;
    instance.roots.Add(root);
    instance.Apply(root);
  }

  // Called with every text Gum translates: one with characters the fonts lack makes them
  // be baked again (on the next Update) with those characters too.
  public static void Shown(string english, string translated)
  {
    if (instance == null || instance.family == DiskFamily && IsLatin1(translated)
      || Array.Exists(GameLanguage.All, l => l.NativeName == english))
      return;
    if (instance.menuKeys.Add(english) && translated.Any(c => !char.IsWhiteSpace(c) && !instance.covered.Contains(c)))
      instance.refreshPending = true;
  }

  // A language picker entry: its name is drawn in its own language's font.
  public static void PinLanguageName(TextRuntime text, GameLanguage language)
  {
    if (instance == null || text == null)
      return;
    string name = IsLatin1(language.NativeName) ? DiskFamily : NamePrefix + language.Code;
    instance.pinned[text] = name;
    if (text.Font != name)
      text.Font = name;
  }

  public static void Unpin(TextRuntime text)
  {
    if (instance == null || text == null || !instance.pinned.Remove(text))
      return;
    instance.Apply(text);
  }

  public static void Update()
  {
    if (instance is not { refreshPending: true })
      return;
    instance.refreshPending = false;
    instance.OnLanguageChanged();
  }

  private void OnLanguageChanged()
  {
    var characters = new StringBuilder();
    foreach (var key in menuKeys)
      characters.Append(Loc.T(key));
    string text = characters.ToString();
    string next = IsLatin1(text) || Loc.Current == GameLanguage.English
      ? DiskFamily : $"{MenuPrefix}{Loc.Current.Code}-{++generation}";
    covered.Clear();
    covered.UnionWith(text);
    for (char c = ' '; c <= '\u00FF'; c++)
      covered.Add(c);
    if (next == family)
      return;
    family = next;
    foreach (var root in roots)
      Apply(root);
    foreach (var root in new[] { Gum.GumService.Default.Root, Gum.GumService.Default.PopupRoot, Gum.GumService.Default.ModalRoot })
      Apply(root);
  }

  private void Apply(GraphicalUiElement element)
  {
    if (element == null)
      return;
    if (element is TextRuntime text && !pinned.ContainsKey(text) && text.Font != family)
      text.Font = family;
    if (element.Children == null)
      return;
    foreach (var child in element.Children)
      if (child is GraphicalUiElement gue)
        Apply(gue);
  }

  public BitmapFont TryCreateFont(BmfcSave save)
  {
    GameLanguage language;
    string characters;
    if (save.FontName.StartsWith(MenuPrefix))
    {
      language = Loc.Current;
      characters = new string(covered.ToArray());
    }
    else if (save.FontName.StartsWith(NamePrefix))
    {
      language = GameLanguage.FromCode(save.FontName.Substring(NamePrefix.Length));
      characters = language?.NativeName;
    }
    else
      return null;
    if (language == null)
      return null;
    try
    {
      if (registered.Add(save.FontName))
        KernSmithFontCreator.RegisterFont(save.FontName, Loc.ContentBytes(language.Font ?? GameFonts.TextPath));
      var codepoints = new SortedSet<int>(characters.Where(c => !char.IsSurrogate(c)).Select(c => (int)c));
      for (int c = 32; c <= 255; c++)
        codepoints.Add(c);
      save.Ranges = Ranges(codepoints);
      return creator.TryCreateFont(save);
    }
    catch (Exception e)
    {
      Log.Error(e, "Could not bake the Gum font {Font}", save.FontName);
      return null;
    }
  }

  // BMFont character ranges ("32-126,160-255,12354").
  private static string Ranges(SortedSet<int> codepoints)
  {
    var ranges = new StringBuilder();
    int start = -1, previous = -1;
    foreach (int c in codepoints.Append(int.MaxValue))
    {
      if (c == previous + 1)
      {
        previous = c;
        continue;
      }
      if (start >= 0)
        ranges.Append(ranges.Length > 0 ? "," : "").Append(start == previous ? $"{start}" : $"{start}-{previous}");
      start = previous = c;
    }
    return ranges.ToString();
  }

  private static bool IsLatin1(string text)
  {
    foreach (char c in text)
      if (c > 'ÿ')
        return false;
    return true;
  }
}
