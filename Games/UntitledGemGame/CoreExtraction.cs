using System;
using System.Collections.Generic;

namespace UntitledGemGame;

// Prestige is an attempt to extract the planet's core: the core collapses into a
// black hole that pulls the ship into a new time loop. The player triggers it by
// holding the HUD's extraction panel, never through the upgrade tree.
public static class CoreExtraction
{
  public static string Name => Loc.T("Extract Core");
  public const float HoldSeconds = 1.5f;
  // Expand Space is a free reward for every talent tier reached, one level per tier.
  public const string ExpandSpaceStat = "CZS";
  public const float ExpandSpaceZoomStep = 0.5f;

  // Gem Lore, a free reward of every talent tier: each talent learned in the tier multiplies
  // gem value by the tier's factor, so every point spent makes the next loop a little richer
  // (and able to afford more of the regular tree's inflated prices, PriceInflation). The
  // factors grow tier by tier: slow at first, steep at the end. A first run has none.
  public const string GemLoreStat = "GLO";
  public static readonly double[] GemLore = [1.25, 1.3, 1.35, 2, 2, 2.2];

  // Claimed like every free reward: from the first extraction on.
  public static double GemLoreMultiplier(Dictionary<string, UpgradeButton> talents, ulong extractions)
  {
    double multiplier = 1;
    if (extractions == 0) return multiplier;
    for (int tier = 0; tier < PrestigeTalentLayout.Tiers.Length; tier++)
      foreach (string id in PrestigeTalentLayout.Tiers[tier].Talents)
        if (talents.TryGetValue(id, out var talent) && talent.CurrentLevel > 0) multiplier *= GemLore[tier];
    return multiplier;
  }

  // The first extraction must pay a prestige point, so a new player cannot end their first
  // run by accident. After that any run can end: one that earned no point still leaves an echo.
  public static bool CanExtract(ulong pendingPoints, ulong extractions) => pendingPoints > 0 || extractions > 0;

  // The talent tree is reached by extracting, so a run that never extracted has no tiers.
  public static int ExpandSpaceLevel(Dictionary<string, UpgradeButton> talents, ulong extractions)
    => extractions == 0 ? 0 : PrestigeTalentLayout.ReachedExpandSpace(talents);

  public static void ApplyExpandSpace(UpgradesGeneratorUpgrades upgrades, int level)
  {
    upgrades.Reset(ExpandSpaceStat);
    upgrades.Increment(ExpandSpaceStat, -ExpandSpaceZoomStep * level);
  }
}
