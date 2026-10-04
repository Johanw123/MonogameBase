using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

// Prototype presentation/rules for the prestige tree. The final talents can be
// replaced without changing the fixed, tiered screen that is being playtested.
internal static class PrestigeTalentLayout
{
  internal sealed record Tier(int RequiredEarlierPoints, int Y, string[] Talents);

  public static readonly Tier[] Tiers =
  [
    new(0, 315, ["CC1", "OH1", "FLR1", "DCM1", "TPM1"]),
    new(3, 610, ["AR1", "AHCM1", "AHMFM1", "MA1", "MCR1", "SYU1"]),
    new(8, 905, ["GVMM1", "AHCRM1", "AHFEM1", "AACM1", "MHF1", "MS1", "SGU1"]),
    new(15, 1200, ["AHVM1", "AHRSM1", "MAC1", "MGS1", "MCSN1", "QEM1"]),
    new(24, 1495, ["JHM1", "RCM1", "MGD1", "MGF1"]),
  ];

  private static readonly HashSet<string> activeTalents = Tiers
    .SelectMany(tier => tier.Talents).ToHashSet();

  public static bool IsInTree(string id) => activeTalents.Contains(id);

  public static void ApplyPrototypeLayout(Dictionary<string, UpgradeButton> buttons)
  {
    foreach (var (id, button) in buttons)
      if (!IsInTree(id))
      {
        button.Data.PosX = -10_000;
        button.Data.PosY = -10_000;
      }

    foreach (var tier in Tiers)
    {
      const int centerX = 1920;
      const int spacing = 360;
      int firstX = centerX - (tier.Talents.Length - 1) * spacing / 2 - 43;
      for (int index = 0; index < tier.Talents.Length; index++)
      {
        if (!buttons.TryGetValue(tier.Talents[index], out var button)) continue;
        button.Data.PosX = firstX + index * spacing;
        button.Data.PosY = tier.Y;
        button.Data.ButtonSizeScale = 1.7f;
        // The legacy dependency metadata remains in the temporary definitions so
        // existing effect-focused tooling can still identify related talents.
        // Meta rendering and purchasing deliberately ignore those links.
      }
    }
  }

  public static int TierIndex(string id)
  {
    for (int tier = 0; tier < Tiers.Length; tier++)
      if (Array.IndexOf(Tiers[tier].Talents, id) >= 0) return tier;
    return -1;
  }

  public static ulong SpentPoints(Dictionary<string, UpgradeButton> buttons, int beforeTier = int.MaxValue)
  {
    ulong total = 0;
    foreach (var (id, button) in buttons)
    {
      int tier = TierIndex(id);
      if (tier < 0 || tier >= beforeTier) continue;
      foreach (var level in button.Data.LevelInfo.Take(button.CurrentLevel))
        total = PrestigeProgression.AddSaturating(total, level.Cost);
    }
    return total;
  }

  public static bool IsUnlocked(Dictionary<string, UpgradeButton> buttons, string id)
  {
    int tier = TierIndex(id);
    return tier >= 0 && (tier == 0
      || SpentPoints(buttons, tier) >= (ulong)Tiers[tier].RequiredEarlierPoints);
  }
}
