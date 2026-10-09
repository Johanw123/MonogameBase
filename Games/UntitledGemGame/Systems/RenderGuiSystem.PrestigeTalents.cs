using JapeFramework;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private static readonly Color PrestigeAccent = OrbitSkin.EpicRarity;
  // Free rewards are gold, apart from the purple of talents bought with points.
  private static readonly Color FreeAccent = OrbitSkin.LegendaryRarity;

  private void DrawPrestigeTalentPanel(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    ulong allocated = PrestigeTalentLayout.SpentPoints(buttons);
    var panel = new Rectangle(300, 150, 3240, 1640);

    batch.Begin();
    OrbitSkin.Panel(batch, panel);
    foreach (var tier in PrestigeTalentLayout.Tiers)
    {
      int index = Array.IndexOf(PrestigeTalentLayout.Tiers, tier);
      bool unlocked = index == 0 || PrestigeTalentLayout.IsUnlocked(buttons, tier.Talents[0]);
      var row = new Rectangle(panel.X + 100, tier.Y - 58, panel.Width - 200, 230);
      batch.Draw(AssetManager.DefaultTexture, row,
        (unlocked ? new Color(24, 18, 38, 205) : new Color(10, 13, 20, 205)));
      OrbitSkin.NineSlice(batch, "modal_info_complete", row, 8, unlocked ? 0.72f : 0.3f);
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(row.X, row.Y, HudLayout.ButtonBorderThickness, row.Height),
        unlocked ? PrestigeAccent * 0.8f : OrbitSkin.BorderColor * 0.45f);

      // The Free Rewards section: a gold-tinted panel behind a divider.
      var free = FreeSection(row);
      batch.Draw(AssetManager.DefaultTexture, free, unlocked ? FreeAccent * 0.12f : Color.Black * 0.3f);
      OrbitSkin.NineSlice(batch, "modal_info_complete", free, 8, unlocked ? 0.35f : 0.15f);
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(free.X - 14, row.Y + 16, HudLayout.ButtonBorderThickness * 2, row.Height - 32),
        unlocked ? FreeAccent * 0.9f : OrbitSkin.BorderColor * 0.6f);
    }
    // Either/or pairs share a plate, split by a slash drawn with the labels.
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
      batch.Draw(AssetManager.DefaultTexture, plate, open ? PrestigeAccent * 0.1f : Color.Black * 0.25f);
      OrbitSkin.NineSlice(batch, "modal_info_complete", plate, 8, open ? 0.6f : 0.25f);
    }
    batch.End();

    string points = Loc.F("{0} available   •   {1} allocated",
      NumberFormatter.AbbreviateBigNumber(UpgradeManager.Instance.CurrentPrestigePoints), NumberFormatter.AbbreviateBigNumber(allocated));
    DrawCenteredPrestigeText(points, 1920, 184, PrestigeAccent, 34);
    DrawCenteredPrestigeText(TalentsReadOnly
        ? Loc.T("This run's talents. You choose talents between runs, after extracting the core.")
        : Loc.T("Spend points in upper tiers to unlock the tiers below. Each tier you reach grants free rewards. Right-click a talent to unlearn it."),
      1920, 224, OrbitSkin.MutedTextColor, 24);

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
        unlocked ? PrestigeAccent : OrbitSkin.MutedTextColor, 28);
      DrawPrestigeText(requirement, new Vector2(448, tier.Y + 18),
        unlocked ? OrbitSkin.ButtonTextColor : OrbitSkin.LockedTextColor, 18);

      // Free tier rewards: reaching the tier is enough, no points are spent (hover them).
      var free = FreeSection(new Rectangle(panel.X + 100, tier.Y - 58, panel.Width - 200, 230));
      DrawCenteredPrestigeText(Loc.T("FREE REWARDS"), free.Center.X, free.Y + 6,
        unlocked ? FreeAccent : OrbitSkin.MutedTextColor, 22);
    }
  }

  private static Rectangle FreeSection(Rectangle row)
    => new(PrestigeTalentLayout.FreeSectionLeft, row.Y + 10,
      row.Right - 10 - PrestigeTalentLayout.FreeSectionLeft, row.Height - 20);

  private void DrawPrestigeTalentLabels(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    batch.Begin();
    foreach (var button in buttons.Values)
    {
      if (!PrestigeTalentLayout.IsShown(button.Data.ShortName)) continue;
      if (button.State != UpgradeButton.UnlockState.Revealed) continue;
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(button.Data.PosX, button.Data.PosY,
          (int)button.Button.Width, (int)button.Button.Height), new Color(3, 7, 12, 175));
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
          ? OrbitSkin.MutedTextColor : OrbitSkin.ButtonTextColor, Color.Black, size);

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
      var rankColor = free ? (claimed ? FreeAccent : OrbitSkin.MutedTextColor)
        : button.State == UpgradeButton.UnlockState.Revealed ? OrbitSkin.LockedTextColor : PrestigeAccent;
      FontManager.LayoutFieldFont(PrestigeFont, rank, new Vector2(center - measured.X / 2, labelTop + 33), rankColor,
        Color.Black, rankSize);
    }

    foreach (var (left, right) in PrestigeTalentLayout.EitherOrPairs)
    {
      if (!buttons.TryGetValue(left, out var first) || !buttons.TryGetValue(right, out var second)) continue;
      const float slashSize = 64f;
      var measured = Measure2("/", Vector2.Zero, slashSize);
      float between = (first.Data.PosX + first.Button.Width + second.Data.PosX) / 2f;
      FontManager.LayoutFieldFont(PrestigeFont, "/",
        new Vector2(between - measured.X / 2, first.Data.PosY + first.Button.Height / 2f - measured.Y / 2),
        PrestigeTalentLayout.IsUnlocked(buttons, left) ? PrestigeAccent : OrbitSkin.MutedTextColor, Color.Black, slashSize);
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
