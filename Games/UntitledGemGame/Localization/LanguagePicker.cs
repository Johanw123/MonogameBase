using System;
using Gum.DataTypes;
using Gum.Forms;
using Gum.Forms.DefaultFromFileVisuals;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;

namespace UntitledGemGame.Localization;

// Gum's side of localization: its text is translated through Loc, and fonts follow the
// language (GumMenuFonts). Install before creating the menus.
public static class GumText
{
  public static void Install(GraphicsDevice device, GumProjectSave project)
  {
    CustomSetPropertyOnRenderable.LocalizationService = new GumLocalization();
    GumMenuFonts.Install(device, project);
  }

  public static void Track(GraphicalUiElement root) => GumMenuFonts.Track(root);
}

// The settings menu's language combo box: "Automatic" first, then GameLanguage.All, each
// name drawn in its own language's font.
public static class LanguagePicker
{
  public static void Fill(DefaultFromFileComboBoxRuntime combo, GumProjectSave project, string setting)
  {
    combo.FormsControl.ListBox.VisualTemplate = new VisualTemplate(() =>
      project.GetComponentSave("Controls/ListBoxItem").ToGraphicalUiElement());
    combo.FormsControl.ListBox.Items.Add(Loc.N("Automatic"));
    foreach (var language in GameLanguage.All)
      combo.FormsControl.ListBox.Items.Add(language.NativeName);
    var items = combo.FormsControl.ListBox.ListBoxItems;
    // Gum does not translate list items; set through Text, this one follows the language.
    if (TextOf(items[0].Visual) is { } automatic)
      automatic.Text = Loc.N("Automatic");
    for (int i = 1; i < items.Count; i++)
      GumMenuFonts.PinLanguageName(TextOf(items[i].Visual), GameLanguage.All[i - 1]);
    combo.FormsControl.SelectedIndex = Index(setting);
    PinSelection(combo, setting);
  }

  public static int Index(string setting) =>
    GameLanguage.FromCode(setting) is { } chosen ? Array.IndexOf(GameLanguage.All, chosen) + 1 : 0;

  // The setting for the picker's selection: a language code, or empty for automatic.
  public static string Setting(int index) => index > 0 ? GameLanguage.All[index - 1].Code : "";

  // The closed picker shows the chosen language's name in that language's font;
  // "Automatic" follows the menu font.
  public static void PinSelection(DefaultFromFileComboBoxRuntime combo, string setting)
  {
    var text = TextOf(combo);
    if (GameLanguage.FromCode(setting) is { } chosen)
      GumMenuFonts.PinLanguageName(text, chosen);
    else
      GumMenuFonts.Unpin(text);
  }

  private static TextRuntime TextOf(GraphicalUiElement element) =>
    element?.GetGraphicalUiElementByName("TextInstance") as TextRuntime;
}
