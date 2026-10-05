using System.Collections.Generic;

namespace UntitledGemGame;

// Prestige is an attempt to extract the planet's core: the core collapses into a
// black hole that pulls the ship into a new time loop. The player triggers it by
// holding the HUD's extraction panel, never through the upgrade tree.
public static class CoreExtraction
{
  public const string Name = "Extract Core";
  public const float HoldSeconds = 1.5f;
  // Expand Space is a free reward for every talent tier reached, one level per tier.
  public const string ExpandSpaceStat = "CZS";
  public const float ExpandSpaceZoomStep = 0.5f;

  // An extraction must pay at least one prestige point, so holding the panel early does nothing.
  public static bool CanExtract(ulong reward) => reward > 0;

  // The talent tree is reached by extracting, so a run that never extracted has no tiers.
  public static int ExpandSpaceLevel(Dictionary<string, UpgradeButton> talents, ulong extractions)
    => extractions == 0 ? 0 : PrestigeTalentLayout.ReachedTiers(talents);

  public static void ApplyExpandSpace(UpgradesGeneratorUpgrades upgrades, int level)
  {
    upgrades.Reset(ExpandSpaceStat);
    upgrades.Increment(ExpandSpaceStat, -ExpandSpaceZoomStep * level);
  }
}
