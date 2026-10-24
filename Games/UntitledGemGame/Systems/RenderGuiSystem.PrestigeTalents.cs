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

  private Rectangle PrestigeTalentPanelBounds => new(240, 150, 3360, ChoosingTalents ? 1760 : 1650);
  private Rectangle PrestigeBeginRunButton => new(
    PrestigeTalentPanelBounds.Right - 364, PrestigeTalentPanelBounds.Bottom - 100, 300, 72);
  private Rectangle PrestigeRefundAllButton => new(
    PrestigeBeginRunButton.X - 320, PrestigeBeginRunButton.Y, 300, 72);

  private void DrawPrestigeTalentPanel(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    ulong allocated = PrestigeTalentLayout.SpentPoints(buttons);
    var panel = PrestigeTalentPanelBounds;

    const int spineX = 368;
    const int labelX = 404;
    const int leftBracketX = 748;
    const int rightBracketX = 2632;
    const int freeDividerX = 2716;
    const int freeHeaderX = PrestigeTalentLayout.FreeSectionLeft - 16;
    int freeWidth = PrestigeTalentLayout.FreeSectionRight - freeHeaderX + 16;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Panel(batch, panel, header: false, PrestigeAccent);
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    batch.Draw(GemSurfaceTexture.PrestigeOrbIcon(), new Rectangle(spineX, 170, 48, 48), Color.White);

    if (ChoosingTalents)
      DrawCleanHorizDivider(batch, spineX, PrestigeBeginRunButton.Y - 24,
        panel.Right - 64 - spineX, PrestigeAccent);

    // Top header horizontal dividers (main talent grid on left, Free Rewards column on right)
    DrawCleanHorizDivider(batch, spineX, 262, rightBracketX - spineX, PrestigeAccent, leadingPip: true);
    DrawCleanHorizDivider(batch, freeHeaderX, 262, freeWidth, FreeAccent, leadingPip: true);

    // Clean vertical divider separating the 6 talent columns from the Free Rewards column
    DrawCleanVertDivider(batch, freeDividerX, 286, 1430, FreeAccent * 0.72f);

    // Vertical spine connecting the 6 tiers on the left
    for (int i = 0; i < PrestigeTalentLayout.Tiers.Length - 1; i++)
    {
      int y0 = PrestigeTalentLayout.Tiers[i].Y + 42;
      int y1 = PrestigeTalentLayout.Tiers[i + 1].Y + 42;
      bool nextOpen = PrestigeTalentLayout.IsUnlocked(buttons, PrestigeTalentLayout.Tiers[i + 1].Talents[0]);
      Color spineCol = nextOpen ? PrestigeAccent * 0.58f : GuiSkin.BorderColor * 0.24f;
      batch.Draw(pixel, new Rectangle(spineX, y0 + 12, 2, Math.Max(0, y1 - y0 - 24)), spineCol);
    }

    for (int index = 0; index < PrestigeTalentLayout.Tiers.Length; index++)
    {
      var tier = PrestigeTalentLayout.Tiers[index];
      bool unlocked = index == 0 || PrestigeTalentLayout.IsUnlocked(buttons, tier.Talents[0]);
      int centerY = tier.Y + 42;
      Color rowAccent = unlocked ? PrestigeAccent : GuiSkin.BorderColor * 0.45f;

      // Left spine node pip
      GuiSkin.DrawHexPip(batch, new Rectangle(spineX - 10, centerY - 11, 22, 22), rowAccent, unlocked);

      // Subtle inter-tier separator line below the talent labels
      if (index < PrestigeTalentLayout.Tiers.Length - 1)
      {
        int sepY = tier.Y + 174;
        batch.Draw(pixel, new Rectangle(leftBracketX + 20, sepY, rightBracketX - leftBracketX - 40, 1),
          GuiSkin.BorderColor * 0.15f);
        batch.Draw(pixel, new Rectangle(freeHeaderX, sepY, freeWidth, 1),
          FreeAccent * 0.14f);
      }
    }

    // Either/or pairs: unboxed horizontal link bar + compact hex choice socket between the two nodes
    foreach (var (left, right) in PrestigeTalentLayout.EitherOrPairs)
    {
      if (!buttons.TryGetValue(left, out var first) || !buttons.TryGetValue(right, out var second)) continue;
      bool open = PrestigeTalentLayout.IsUnlocked(buttons, left);
      int leftRightEdge = first.Data.PosX + (int)first.Button.Width;
      int rightLeftEdge = second.Data.PosX;
      int betweenX = (leftRightEdge + rightLeftEdge) / 2;
      int betweenY = first.Data.PosY + (int)(first.Button.Height / 2f);
      Color pairCol = open ? PrestigeAccent : GuiSkin.BorderColor * 0.45f;

      // Dual horizontal connector rails linking the pair directly to the central choice socket
      int gapLeftW = Math.Max(0, betweenX - 28 - (leftRightEdge + 6));
      int gapRightW = Math.Max(0, (rightLeftEdge - 6) - (betweenX + 28));
      if (gapLeftW > 0)
      {
        batch.Draw(pixel, new Rectangle(leftRightEdge + 6, betweenY - 3, gapLeftW, 2), pairCol * 0.75f);
        batch.Draw(pixel, new Rectangle(leftRightEdge + 6, betweenY + 3, gapLeftW, 2), pairCol * 0.45f);
      }
      if (gapRightW > 0)
      {
        batch.Draw(pixel, new Rectangle(betweenX + 28, betweenY - 3, gapRightW, 2), pairCol * 0.75f);
        batch.Draw(pixel, new Rectangle(betweenX + 28, betweenY + 3, gapRightW, 2), pairCol * 0.45f);
      }

      // Subtle top connection bracket above the two mutually exclusive icons
      int firstCenter = first.Data.PosX + (int)(first.Button.Width / 2f);
      int secondCenter = second.Data.PosX + (int)(second.Button.Width / 2f);
      int topBracketY = first.Data.PosY - 12;
      batch.Draw(pixel, new Rectangle(firstCenter, topBracketY, secondCenter - firstCenter, 2), pairCol * 0.55f);
      batch.Draw(pixel, new Rectangle(firstCenter, topBracketY, 2, 8), pairCol * 0.55f);
      batch.Draw(pixel, new Rectangle(secondCenter - 2, topBracketY, 2, 8), pairCol * 0.55f);

      GuiSkin.DrawHexSocket(batch, new Rectangle(betweenX - 28, betweenY - 28, 56, 56),
        open ? PrestigeAccent : GuiSkin.BorderColor * 0.55f, active: open);
    }
    batch.End();

    // Top-left header: Points summary + instruction subtitle
    string points = Loc.F("{0} available   •   {1} allocated",
      NumberFormatter.AbbreviateBigNumber(UpgradeManager.Instance.CurrentPrestigePoints),
      NumberFormatter.AbbreviateBigNumber(allocated));
    DrawPrestigeText(Loc.Upper(points), new Vector2(spineX + 60, 174), GuiSkin.StatHeadingColor, 34);
    DrawPrestigeText(TalentsReadOnly
        ? Loc.T("This run's talents. You choose talents between runs, after extracting the core.")
        : Loc.T("Spend points in upper tiers to unlock the tiers below. Each tier you reach grants free rewards. Right-click a talent to unlearn it."),
      new Vector2(spineX, 220), GuiSkin.MutedTextColor, 22);

    // Top-right header: Single column heading for Free Rewards (instead of repeating on all 6 rows)
    DrawPrestigeText(Loc.Upper(Loc.T("Free rewards")), new Vector2(freeHeaderX, 178), FreeAccent, 30);
    DrawPrestigeText(Loc.T("Claimed on tier unlock"), new Vector2(freeHeaderX, 220), GuiSkin.MutedTextColor, 21);

    for (int index = 0; index < PrestigeTalentLayout.Tiers.Length; index++)
    {
      var tier = PrestigeTalentLayout.Tiers[index];
      ulong earlier = PrestigeTalentLayout.SpentPoints(buttons, index);
      bool unlocked = index == 0 || earlier >= (ulong)tier.RequiredEarlierPoints;
      string title = Loc.F("TIER {0}", index + 1);
      string requirement = index == 0 ? Loc.T("OPEN")
        : unlocked ? Loc.F("UNLOCKED  •  {0}/{1}", earlier, tier.RequiredEarlierPoints)
        : Loc.F("SPEND {0} ABOVE  •  {1}/{0}", tier.RequiredEarlierPoints, earlier);
      DrawPrestigeText(title, new Vector2(labelX, tier.Y + 12),
        unlocked ? GuiSkin.StatHeadingColor : GuiSkin.MutedTextColor, 28);
      DrawPrestigeText(requirement, new Vector2(labelX, tier.Y + 48),
        unlocked ? GuiSkin.ConfirmAccent : GuiSkin.MutedTextColor, 18);
    }
  }

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
      float labelTop = button.Data.PosY + button.Button.Height + 15;
      string name = Loc.T(button.Data.UpgradeDefinition.Name);
      float size = free ? 19f : 21f;
      float width = free ? 210f : Math.Min(310f, PrestigeTalentLayout.TalentSpacing - 30f);
      var measured = Measure2(name, Vector2.Zero, size);
      if (measured.X > width) size *= width / measured.X;
      measured = Measure2(name, Vector2.Zero, size);
      bool learned = button.CurrentLevel > 0;
      Color nameColor = button.State == UpgradeButton.UnlockState.Revealed
        ? GuiSkin.MutedTextColor
        : learned ? (free ? FreeAccent : GuiSkin.StatHeadingColor) : GuiSkin.ButtonTextColor;
      FontManager.LayoutFieldFont(PrestigeFont, name, new Vector2(center - measured.X / 2, labelTop),
        nameColor, Color.Black, size);

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
      float rankSize = 17f;
      measured = Measure2(rank, Vector2.Zero, rankSize);
      if (measured.X > width) rankSize *= width / measured.X;
      measured = Measure2(rank, Vector2.Zero, rankSize);
      var rankColor = free ? (claimed ? FreeAccent : GuiSkin.MutedTextColor)
        : excludedBy != null ? GuiSkin.LockedTextColor
        : button.State == UpgradeButton.UnlockState.Revealed ? GuiSkin.MutedTextColor
        : button.IsMaxLevel ? GuiSkin.ConfirmAccent : PrestigeAccent * 0.88f;
      FontManager.LayoutFieldFont(PrestigeFont, rank, new Vector2(center - measured.X / 2, labelTop + 29), rankColor,
        Color.Black, rankSize);
    }

    foreach (var (left, right) in PrestigeTalentLayout.EitherOrPairs)
    {
      if (!buttons.TryGetValue(left, out var first) || !buttons.TryGetValue(right, out var second)) continue;
      const float slashSize = 34f;
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
