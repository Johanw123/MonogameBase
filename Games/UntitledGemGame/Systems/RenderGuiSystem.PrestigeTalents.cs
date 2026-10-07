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
    batch.End();

    string points = $"{NumberFormatter.AbbreviateBigNumber(UpgradeManager.Instance.CurrentPrestigePoints)} available"
      + $"   •   {NumberFormatter.AbbreviateBigNumber(allocated)} allocated";
    DrawCenteredPrestigeText(points, 1920, 184, PrestigeAccent, 34);
    DrawCenteredPrestigeText("Spend in the upper tiers to unlock stronger choices below. Every tier you reach also grants its free rewards.",
      1920, 224, OrbitSkin.MutedTextColor, 24);

    for (int index = 0; index < PrestigeTalentLayout.Tiers.Length; index++)
    {
      var tier = PrestigeTalentLayout.Tiers[index];
      ulong earlier = PrestigeTalentLayout.SpentPoints(buttons, index);
      bool unlocked = index == 0 || earlier >= (ulong)tier.RequiredEarlierPoints;
      string title = $"TIER {index + 1}  •  {tier.Name.ToUpperInvariant()}";
      string requirement = index == 0 ? "OPEN"
        : unlocked ? $"UNLOCKED  •  {earlier}/{tier.RequiredEarlierPoints}"
        : $"SPEND {tier.RequiredEarlierPoints} ABOVE  •  {earlier}/{tier.RequiredEarlierPoints}";
      DrawPrestigeText(title, new Vector2(448, tier.Y - 25),
        unlocked ? PrestigeAccent : OrbitSkin.MutedTextColor, 28);
      DrawPrestigeText(requirement, new Vector2(448, tier.Y + 18),
        unlocked ? OrbitSkin.ButtonTextColor : OrbitSkin.LockedTextColor, 18);

      // Free tier rewards: reaching the tier is enough, no points are spent (hover them).
      var free = FreeSection(new Rectangle(panel.X + 100, tier.Y - 58, panel.Width - 200, 230));
      DrawCenteredPrestigeText("FREE REWARDS", free.Center.X, free.Y + 6,
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

    foreach (var button in buttons.Values)
    {
      if (!PrestigeTalentLayout.IsShown(button.Data.ShortName)) continue;
      bool free = PrestigeTalentLayout.IsFreeReward(button.Data.ShortName);
      float center = button.Data.PosX + button.Button.Width / 2f;
      float labelTop = button.Data.PosY + button.Button.Height + 8;
      string name = button.Data.UpgradeDefinition.Name;
      float size = free ? 19f : 21f, width = free ? 210f : 310f;
      var measured = Measure2(name, Vector2.Zero, size);
      if (measured.X > width) size *= width / measured.X;
      measured = Measure2(name, Vector2.Zero, size);
      DrawPrestigeText(name, new Vector2(center - measured.X / 2, labelTop),
        button.State == UpgradeButton.UnlockState.Revealed
          ? OrbitSkin.MutedTextColor : OrbitSkin.ButtonTextColor, size);

      bool claimed = free && button.CurrentLevel > 0;
      string rank = free
          ? claimed ? "CLAIMED"
          : PrestigeTalentLayout.IsUnlocked(buttons, button.Data.ShortName) ? "AFTER EXTRACTION" : "REACH TIER"
        : button.IsMaxLevel ? $"MAX  {button.CurrentLevel}/{button.Data.NumLevels}"
        : $"{button.GetNextLevelCost()} point{(button.GetNextLevelCost() == 1 ? "" : "s")}  •  {button.CurrentLevel}/{button.Data.NumLevels}";
      measured = Measure2(rank, Vector2.Zero, 18);
      var rankColor = free ? (claimed ? FreeAccent : OrbitSkin.MutedTextColor)
        : button.State == UpgradeButton.UnlockState.Revealed ? OrbitSkin.LockedTextColor : PrestigeAccent;
      DrawPrestigeText(rank, new Vector2(center - measured.X / 2, labelTop + 33), rankColor, 18);
    }
  }

  private void DrawCenteredPrestigeText(string text, float centerX, float y, Color color, float size)
  {
    var measured = Measure2(text, Vector2.Zero, size);
    DrawPrestigeText(text, new Vector2(centerX - measured.X / 2, y), color, size);
  }

  private void DrawRightPrestigeText(string text, float right, float y, Color color, float size)
    => DrawPrestigeText(text, new Vector2(right - Measure2(text, Vector2.Zero, size).X, y), color, size);

  private static void DrawPrestigeText(string text, Vector2 position, Color color, float size)
    => FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
      text, position, color, Color.Black, size);
}
