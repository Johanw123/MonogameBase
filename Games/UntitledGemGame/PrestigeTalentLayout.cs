using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

// Prototype presentation/rules for the prestige tree. The final talents can be
// replaced without changing the fixed, tiered screen that is being playtested.
internal static class PrestigeTalentLayout
{
  internal sealed record Tier(string Name, int RequiredEarlierPoints, int Y, string[] Talents);

  public static readonly Tier[] Tiers =
  [
    new("Directives", 0, 315, ["CC1", "OH1", "FLR1", "DCM1", "TPM1"]),
    new("Infrastructure", 3, 610, ["GM1", "MHF1", "CAT1"]),
    new("Reactions", 5, 905, ["MA1", "RCM1", "JHM1", "MGD1", "CA1"]),
    new("Convergence", 10, 1200, ["QEM1", "MGS1", "MCSN1", "PCO1", "CCN1"]),
    new("Transcendence", 16, 1495, ["HICM1", "MGF1", "MM1", "CN1", "WCM1"]),
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
