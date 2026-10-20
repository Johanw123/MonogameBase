using JapeFramework;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private static Color PrestigeAccent => GuiSkin.PanelAccent;
  // Free rewards are gold, apart from the purple of talents bought with points.
  private static Color FreeAccent => GuiSkin.LegendaryRarity;

  private void DrawPrestigeTalentPanel(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    ulong allocated = PrestigeTalentLayout.SpentPoints(buttons);
    var panel = new Rectangle(300, 150, 3240, 1640);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Panel(batch, panel, header: false, PrestigeAccent);
    GuiSkin.TechDivider(batch, new Rectangle(panel.Center.X - 520, 254, 1040, 4), PrestigeAccent * 0.65f);
    foreach (var tier in PrestigeTalentLayout.Tiers)
    {
      int index = Array.IndexOf(PrestigeTalentLayout.Tiers, tier);
      bool unlocked = index == 0 || PrestigeTalentLayout.IsUnlocked(buttons, tier.Talents[0]);
      var row = new Rectangle(panel.X + 100, tier.Y - 58, panel.Width - 200, 230);
      GuiSkin.TierBand(batch, row, 272,
        unlocked ? GuiSkin.SurfaceColor : GuiSkin.LockedSurfaceColor,
        unlocked ? PrestigeAccent : GuiSkin.BorderColor * 0.65f, locked: !unlocked);
      GuiSkin.DrawHexPip(batch, new Rectangle(418, tier.Y - 21, 22, 22), PrestigeAccent, unlocked);

      // Free rewards sit in a distinct octagonal gold TechCard compartment on the right of each tier.
      var free = FreeSection(row);
      GuiSkin.TechCard(batch, free,
        unlocked ? GuiPalette.Base(new Color(20, 26, 38, 242)) : GuiSkin.LockedSurfaceColor,
        unlocked ? FreeAccent : GuiSkin.BorderColor * 0.5f,
        cornerGraphic: true, hexWatermark: false);
    }
    // Either/or pairs share a TechCard plate with a hexagonal choice emblem behind the slash.
    foreach (var (left, right) in PrestigeTalentLayout.EitherOrPairs)
    {
      if (!buttons.TryGetValue(left, out var first) || !buttons.TryGetValue(right, out var second)) continue;
      bool open = PrestigeTalentLayout.IsUnlocked(buttons, left);
      // The plate covers both columns, stopping short of the neighbours' labels.
      int reach = PrestigeTalentLayout.TalentSpacing / 2 - 12;
      int firstCenter = first.Data.PosX + (int)(first.Button.Width / 2);
      int secondCenter = second.Data.PosX + (int)(second.Button.Width / 2);
      var plate = new Rectangle(firstCenter - reach, first.Data.PosY - 18,
        secondCenter - firstCenter + 2 * reach, (int)first.Button.Height + 86);
      GuiSkin.TechCard(batch, plate,
        open ? GuiSkin.ButtonHoverColor : GuiSkin.LockedSurfaceColor,
        open ? PrestigeAccent * 0.85f : GuiSkin.BorderColor * 0.45f,
        hovered: open, cornerGraphic: true);
      int betweenX = (int)((first.Data.PosX + first.Button.Width + second.Data.PosX) / 2f);
      int betweenY = (int)(first.Data.PosY + first.Button.Height / 2f);
      GuiSkin.DrawHexSocket(batch, new Rectangle(betweenX - 34, betweenY - 34, 68, 68),
        open ? PrestigeAccent : GuiSkin.BorderColor * 0.55f, active: open);
    }
    batch.End();

    string points = Loc.F("{0} available   •   {1} allocated",
      NumberFormatter.AbbreviateBigNumber(UpgradeManager.Instance.CurrentPrestigePoints), NumberFormatter.AbbreviateBigNumber(allocated));
    DrawCenteredPrestigeText(points, 1920, 180, PrestigeAccent, 34);
    DrawCenteredPrestigeText(TalentsReadOnly
        ? Loc.T("This run's talents. You choose talents between runs, after extracting the core.")
        : Loc.T("Spend points in upper tiers to unlock the tiers below. Each tier you reach grants free rewards. Right-click a talent to unlearn it."),
      1920, 220, GuiSkin.MutedTextColor, 24);

    for (int index = 0; index < PrestigeTalentLayout.Tiers.Length; index++)
    {
      var tier = PrestigeTalentLayout.Tiers[index];
      ulong earlier = PrestigeTalentLayout.SpentPoints(buttons, index);
      bool unlocked = index == 0 || earlier >= (ulong)tier.RequiredEarlierPoints;
      string title = Loc.F("TIER {0}", index + 1);
      string requirement = index == 0 ? Loc.T("OPEN")
        : unlocked ? Loc.F("UNLOCKED  •  {0}/{1}", earlier, tier.RequiredEarlierPoints)
        : Loc.F("SPEND {0} ABOVE  •  {1}/{0}", tier.RequiredEarlierPoints, earlier);
      DrawPrestigeText(title, new Vector2(448, tier.Y - 25),
        unlocked ? PrestigeAccent : GuiSkin.MutedTextColor, 28);
      DrawPrestigeText(requirement, new Vector2(448, tier.Y + 18),
        unlocked ? GuiSkin.ConfirmAccent : GuiSkin.MutedTextColor, 18);

      // Free tier rewards: reaching the tier is enough, no points are spent (hover them).
      var free = FreeSection(new Rectangle(panel.X + 100, tier.Y - 58, panel.Width - 200, 230));
      DrawCenteredPrestigeText(Loc.T("FREE REWARDS"), free.Center.X, free.Y + 6,
        unlocked ? FreeAccent : GuiSkin.MutedTextColor, 22);
    }
  }

  private static Rectangle FreeSection(Rectangle row)
    => new(PrestigeTalentLayout.FreeSectionLeft, row.Y + 10,
      row.Right - 10 - PrestigeTalentLayout.FreeSectionLeft, row.Height - 20);

  private void DrawPrestigeTalentLabels(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    foreach (var button in buttons.Values)
    {
      if (!PrestigeTalentLayout.IsShown(button.Data.ShortName)) continue;
      if (button.State != UpgradeButton.UnlockState.Revealed) continue;
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(button.Data.PosX, button.Data.PosY,
          (int)button.Button.Width, (int)button.Button.Height), GuiPalette.Base(new Color(3, 7, 12, 175)));
    }
    batch.End();

    // Labels never overlap: lay them all out and draw them in one stroke and one fill pass.
    FontManager.BeginFieldFonts(PrestigeFont);
    foreach (var button in buttons.Values)
    {
      if (!PrestigeTalentLayout.IsShown(button.Data.ShortName)) continue;
      bool free = PrestigeTalentLayout.IsFreeReward(button.Data.ShortName);
      float center = button.Data.PosX + button.Button.Width / 2f;
      float labelTop = button.Data.PosY + button.Button.Height + 8;
      string name = Loc.T(button.Data.UpgradeDefinition.Name);
      float size = free ? 19f : 21f;
      float width = free ? 210f : Math.Min(310f, PrestigeTalentLayout.TalentSpacing - 30f);
      var measured = Measure2(name, Vector2.Zero, size);
      if (measured.X > width) size *= width / measured.X;
      measured = Measure2(name, Vector2.Zero, size);
      FontManager.LayoutFieldFont(PrestigeFont, name, new Vector2(center - measured.X / 2, labelTop),
        button.State == UpgradeButton.UnlockState.Revealed
          ? GuiSkin.MutedTextColor : GuiSkin.ButtonTextColor, Color.Black, size);

      bool claimed = free && button.CurrentLevel > 0;
      string excludedBy = free ? null : PrestigeTalentLayout.ExcludedBy(buttons, button.Data.ShortName);
      string rank = free
          ? claimed ? Loc.T("CLAIMED")
          : PrestigeTalentLayout.IsUnlocked(buttons, button.Data.ShortName) ? Loc.T("AFTER EXTRACTION") : Loc.T("REACH TIER")
        // The neighbour across an either/or slash needs no name.
        : excludedBy != null && PrestigeTalentLayout.AreEitherOrNeighbours(excludedBy, button.Data.ShortName)
          ? Loc.T("RULED OUT")
        : excludedBy != null ? Loc.F("RULED OUT BY {0}", Loc.Upper(Loc.T(buttons[excludedBy].Data.UpgradeDefinition.Name)))
        : button.IsMaxLevel ? Loc.F("MAX  {0}/{1}", button.CurrentLevel, button.Data.NumLevels)
        : Loc.P((long)button.GetNextLevelCost(), "{0} point", "{0} points") + $"  •  {button.CurrentLevel}/{button.Data.NumLevels}";
      float rankSize = 18f;
      measured = Measure2(rank, Vector2.Zero, rankSize);
      if (measured.X > width) rankSize *= width / measured.X;
      measured = Measure2(rank, Vector2.Zero, rankSize);
      var rankColor = free ? (claimed ? FreeAccent : GuiSkin.MutedTextColor)
        : excludedBy != null ? GuiSkin.LockedTextColor
        : button.State == UpgradeButton.UnlockState.Revealed ? GuiSkin.MutedTextColor
        : button.IsMaxLevel ? GuiSkin.ConfirmAccent : PrestigeAccent;
      FontManager.LayoutFieldFont(PrestigeFont, rank, new Vector2(center - measured.X / 2, labelTop + 33), rankColor,
        Color.Black, rankSize);
    }

    foreach (var (left, right) in PrestigeTalentLayout.EitherOrPairs)
    {
      if (!buttons.TryGetValue(left, out var first) || !buttons.TryGetValue(right, out var second)) continue;
      const float slashSize = 42f;
      var measured = Measure2("/", Vector2.Zero, slashSize);
      float between = (first.Data.PosX + first.Button.Width + second.Data.PosX) / 2f;
      FontManager.LayoutFieldFont(PrestigeFont, "/",
        new Vector2(between - measured.X / 2, first.Data.PosY + first.Button.Height / 2f - measured.Y / 2),
        PrestigeTalentLayout.IsUnlocked(buttons, left) ? PrestigeAccent : GuiSkin.MutedTextColor, Color.Black, slashSize);
    }
    FontManager.EndFieldFonts(PrestigeFont);
  }

  private void DrawCenteredPrestigeText(string text, float centerX, float y, Color color, float size)
  {
    var measured = Measure2(text, Vector2.Zero, size);
    DrawPrestigeText(text, new Vector2(centerX - measured.X / 2, y), color, size);
  }

  private void DrawRightPrestigeText(string text, float right, float y, Color color, float size)
    => DrawPrestigeText(text, new Vector2(right - Measure2(text, Vector2.Zero, size).X, y), color, size);

  private const string PrestigeFont = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);

  private static void DrawPrestigeText(string text, Vector2 position, Color color, float size)
    => FontManager.RenderFieldFont(PrestigeFont, text, position, color, Color.Black, size);
}
