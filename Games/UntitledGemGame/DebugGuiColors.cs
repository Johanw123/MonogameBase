using System;
using System.IO;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

internal sealed class DebugGuiColors
{
  private string status;
  private static readonly (GuiColor Role, string Label)[] controls =
  [
    (GuiColor.Accent, "Panel interaction / panel and button frames"),
    (GuiColor.MenuInteraction, "Menu interaction / menus and settings"),
    (GuiColor.Hover, "Button hover / focus"),
    (GuiColor.Progress, "Progress bars / slider fills"),
    (GuiColor.NodeProgress, "Upgrade node progress bars"),
    (GuiColor.Selection, "Selected tab text"),
    (GuiColor.NodeBorder, "Upgrade node inner borders"),
    (GuiColor.NodeAffordableBorder, "Upgrade button border / affordable"),
    (GuiColor.NodeAffordableBadgeGlyph, "Upgrade affordable badge / plus sign"),
    (GuiColor.NodeAffordableBadgeBorder, "Upgrade affordable badge / border"),
    (GuiColor.NodePurchasedBorder, "Upgrade button border / purchased"),
    (GuiColor.NodeMaxedBorder, "Upgrade button border / maxed out"),
    (GuiColor.NodeLockedBorder, "Upgrade button border / locked"),
    (GuiColor.NodeUnaffordableBorder, "Upgrade button border / unaffordable"),
    (GuiColor.NodeHoverBorder, "Upgrade button border / hover"),
    (GuiColor.Ability, "Power cells / rare rarity"),
    (GuiColor.Upgrade, "All panels / upgrades / values"), (GuiColor.Prestige, "Prestige currency / epic rarity"),
    (GuiColor.Confirm, "Confirm / success"), (GuiColor.Restricted, "Locked / restricted"),
    (GuiColor.Text, "Primary text"), (GuiColor.MutedText, "Muted text"),
    (GuiColor.DisabledText, "Disabled text"), (GuiColor.Uncommon, "Uncommon rarity"),
    (GuiColor.Mythic, "Mythic rarity")
  ];

  public void Draw()
  {
    if (!ImGui.CollapsingHeader("HUD / GUI colors")) return;
    ImGui.PushID("gui-colors");
    ImGui.TextWrapped("Changes preview live. Save as default keeps this palette for future launches and builds. Revert restores your last saved palette; Original palette previews the game's original colors.");
#if !KNI_WEB
    string content = Demo.IsDev ? DebugSoundEffects.FindSourceContentDirectory() : null;
    ImGui.BeginDisabled(content == null);
    if (ImGui.Button("Save as default"))
    {
      try { GuiPalette.Save(Path.Combine(content, GuiPalette.ManifestPath)); status = "Saved as default."; }
      catch (Exception error) { status = "Save failed: " + error.Message; }
    }
    ImGui.EndDisabled();
    if (content == null) ImGui.TextWrapped("Saving defaults requires a developer launch from the source project.");
    else if (ImGui.IsItemHovered()) ImGui.SetTooltip(Path.Combine(content, GuiPalette.ManifestPath));
#else
    ImGui.TextDisabled("Save defaults from the desktop developer build.");
#endif
    ImGui.SameLine();
    if (ImGui.Button("Revert to saved")) { GuiPalette.Revert(); status = "Saved palette restored."; }
    ImGui.SameLine();
    if (ImGui.Button("Original palette")) { GuiPalette.ResetOriginal(); status = "Original palette previewed. Save to make it the default."; }
#if !KNI_WEB
    if (Demo.IsDev)
    {
      ImGui.SameLine();
      if (ImGui.Button(DebugGuiSprites.StandaloneOpen ? "Close GUI sprites lab (F9)" : "GUI sprites lab (F9)"))
        DebugGuiSprites.StandaloneOpen = !DebugGuiSprites.StandaloneOpen;
    }
#endif
    ImGui.TextUnformatted(GuiPalette.HasChanges ? "Unsaved palette changes" : "Matches saved default");
    if (status != null) ImGui.TextWrapped(status);
    if (GuiPalette.LoadError != null) ImGui.TextWrapped(GuiPalette.LoadError);

    ImGui.SeparatorText("Base blue surfaces and artwork");
    var data = GuiPalette.Snapshot();
    float hue = data.BaseHueShift, saturation = data.BaseSaturation, brightness = data.BaseBrightness;
    bool changed = ImGui.SliderFloat("Hue shift", ref hue, -180, 180, "%.0f deg", ImGuiSliderFlags.AlwaysClamp);
    changed |= ImGui.SliderFloat("Saturation", ref saturation, 0, 2, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    changed |= ImGui.SliderFloat("Brightness", ref brightness, 0.1f, 2, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    if (changed)
    {
      data.BaseHueShift = hue; data.BaseSaturation = saturation; data.BaseBrightness = brightness;
      GuiPalette.Apply(data);
    }
    if (ImGui.Button("Revert base colors"))
    {
      var saved = GuiPalette.SavedSnapshot();
      data = GuiPalette.Snapshot();
      data.BaseHueShift = saved.BaseHueShift; data.BaseSaturation = saved.BaseSaturation;
      data.BaseBrightness = saved.BaseBrightness;
      GuiPalette.Apply(data);
    }
    ImGui.SeparatorText("Accents and text");
    ImGui.TextWrapped("Click a swatch for the color picker. RGB, HSV and hex entry are available. Each Revert restores that saved color.");
    foreach (var (role, label) in controls)
    {
      ImGui.PushID((int)role);
      var rgb = GuiPalette.Get(role).ToVector3();
      var value = new System.Numerics.Vector3(rgb.X, rgb.Y, rgb.Z);
      ImGui.SetNextItemWidth(210);
      if (ImGui.ColorEdit3(label, ref value, ImGuiColorEditFlags.DisplayHex))
        GuiPalette.Set(role, new Color(value.X, value.Y, value.Z));
      ImGui.SameLine();
      if (ImGui.SmallButton("Revert"))
      {
        data = GuiPalette.Snapshot();
        data.Colors[role.ToString()] = GuiPalette.SavedSnapshot().Colors[role.ToString()];
        GuiPalette.Apply(data);
      }
      ImGui.PopID();
    }
#if !KNI_WEB
    if (Demo.IsDev)
    {
      ImGui.SeparatorText("Borders & background sprites");
      if (ImGui.TreeNodeEx("Experiment with borders & background sprites (drag & drop)", ImGuiTreeNodeFlags.DefaultOpen))
      {
        DebugGuiSprites.DrawInline();
        ImGui.TreePop();
      }
    }
#endif
    ImGui.PopID();
  }
}

