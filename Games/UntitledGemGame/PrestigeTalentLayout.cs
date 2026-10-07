using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

// The prestige tree: six tiers of one-point talents, each gated by the points spent in
// the tiers above. Low tiers hold simple seeds and system unlocks; higher tiers hold the
// combos that tie weapons and systems together, and the last tier the run-changing
// capstones. Each tier also has free rewards that are claimed by reaching it.
public static class PrestigeTalentLayout
{
  public sealed record Tier(string Name, int RequiredEarlierPoints, int Y, string[] Talents, string[] FreeRewards);

  public static readonly Tier[] Tiers =
  [
    new("Directives", 0, 320, ["CC1", "TR1", "MHF1", "PRL1", "SHS1", "CAT1"], ["XSP1"]),
    new("Infrastructure", 3, 575, ["SSU1", "LR1", "ODP1", "SYF1", "KNH1", "MCSN1"], ["XSP2", "SPC1"]),
    new("Reactions", 5, 830, ["SYU1", "BR1", "MD1", "EP1", "AE1", "VPL1"], ["XSP3", "HST1"]),
    new("Convergence", 10, 1085, ["SGU1", "MBR1", "DG1", "KD1", "FSD1", "DSP1"], ["XSP4"]),
    new("Transcendence", 16, 1340, ["CN1", "HICM1", "CHR1", "MLC1", "SR1", "PO1"], ["XSP5", "SCH1"]),
    new("Singularity", 24, 1595, ["SGR1", "OVC1", "LOP1", "RCO1", "CBK1"], ["CMY1"]),
  ];

  // Talents with run-changing tradeoffs that progression presets never buy.
  public static readonly string[] PlaystyleTalents = ["LOP1"];

  private static readonly HashSet<string> activeTalents = Tiers
    .SelectMany(tier => tier.Talents).ToHashSet();
  private static readonly HashSet<string> freeRewards = Tiers
    .SelectMany(tier => tier.FreeRewards).ToHashSet();

  // Talents that can be bought with prestige points.
  public static bool IsInTree(string id) => activeTalents.Contains(id);

  // Rewards claimed for free by reaching their tier; shown in the tree but never bought.
  public static bool IsFreeReward(string id) => freeRewards.Contains(id);

  public static bool IsShown(string id) => IsInTree(id) || IsFreeReward(id);

  private const int TalentCenterX = 1800;
  private const int TalentSpacing = 340;
  // Each tier row ends in its own Free Rewards section, set off by a divider.
  public const int FreeSectionLeft = 2790;
  public const int FreeSectionRight = 3440;
  private const int FreeRewardSpacing = 230;
  // Half a free reward button's width at its 1.35 size scale.
  private const int FreeRewardHalfWidth = 34;

  public static void ApplyPrototypeLayout(Dictionary<string, UpgradeButton> buttons)
  {
    foreach (var (id, button) in buttons)
      if (!IsShown(id))
      {
        button.Data.PosX = -10_000;
        button.Data.PosY = -10_000;
      }

    foreach (var tier in Tiers)
    {
      int firstX = TalentCenterX - (tier.Talents.Length - 1) * TalentSpacing / 2 - 43;
      for (int index = 0; index < tier.Talents.Length; index++)
      {
        if (!buttons.TryGetValue(tier.Talents[index], out var button)) continue;
        button.Data.PosX = firstX + index * TalentSpacing;
        button.Data.PosY = tier.Y;
        button.Data.ButtonSizeScale = 1.7f;
        // The legacy dependency metadata remains in the temporary definitions so
        // existing effect-focused tooling can still identify related talents.
        // Meta rendering and purchasing deliberately ignore those links.
      }
      int freeCenterX = (FreeSectionLeft + FreeSectionRight) / 2;
      int firstFreeX = freeCenterX - (tier.FreeRewards.Length - 1) * FreeRewardSpacing / 2 - FreeRewardHalfWidth;
      for (int index = 0; index < tier.FreeRewards.Length; index++)
      {
        if (!buttons.TryGetValue(tier.FreeRewards[index], out var button)) continue;
        button.Data.PosX = firstFreeX + index * FreeRewardSpacing;
        button.Data.PosY = tier.Y + 8;
        button.Data.ButtonSizeScale = 1.35f;
      }
    }
  }

  public static int TierIndex(string id)
  {
    for (int tier = 0; tier < Tiers.Length; tier++)
      if (Array.IndexOf(Tiers[tier].Talents, id) >= 0 || Array.IndexOf(Tiers[tier].FreeRewards, id) >= 0) return tier;
    return -1;
  }

  public static ulong SpentPoints(Dictionary<string, UpgradeButton> buttons, int beforeTier = int.MaxValue)
  {
    ulong total = 0;
    foreach (var (id, button) in buttons)
    {
      int tier = TierIndex(id);
      if (tier < 0 || tier >= beforeTier || IsFreeReward(id)) continue;
      foreach (var level in button.Data.LevelInfo.Take(button.CurrentLevel))
        total = PrestigeProgression.AddSaturating(total, level.Cost);
    }
    return total;
  }

  public static bool IsUnlocked(Dictionary<string, UpgradeButton> buttons, string id)
  {
    int tier = TierIndex(id);
    return tier >= 0 && IsTierReached(buttons, tier);
  }

  public static bool IsTierReached(Dictionary<string, UpgradeButton> buttons, int tier)
    => tier == 0 || SpentPoints(buttons, tier) >= (ulong)Tiers[tier].RequiredEarlierPoints;

  public static int ReachedTiers(Dictionary<string, UpgradeButton> buttons)
  {
    int reached = 0;
    for (int tier = 0; tier < Tiers.Length; tier++)
      if (IsTierReached(buttons, tier)) reached++;
    return reached;
  }

  // Expand Space is a free reward of every tier that has one (see CoreExtraction).
  public static int ReachedExpandSpace(Dictionary<string, UpgradeButton> buttons)
  {
    int reached = 0;
    for (int tier = 0; tier < Tiers.Length; tier++)
      if (IsTierReached(buttons, tier) && Tiers[tier].FreeRewards.Any(id => id.StartsWith("XSP", StringComparison.Ordinal)))
        reached++;
    return reached;
  }
}
