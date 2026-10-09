using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

// The prestige tree: six tiers of one-point talents, each gated by the points spent in
// the tiers above. Low tiers hold simple seeds; higher tiers hold the combos that tie
// weapons and systems together, and the last tier the run-changing capstones. Each tier
// also has free rewards that are claimed by reaching it: tiers two to five unlock Command
// Center, ship systems, the Shipyard and signals, one at a time so the first extraction
// adds no new system, every tier but the last expands space, and every tier's Gem Lore
// makes its talents raise gem value (CoreExtraction). A talent that needs one of those
// systems sits no higher than the tier that unlocks it.
public static class PrestigeTalentLayout
{
  public sealed record Tier(int RequiredEarlierPoints, int Y, string[] Talents, string[] FreeRewards);

  public static readonly Tier[] Tiers =
  [
    new(0, 320, ["TR1", "MHF1", "PRL1", "CAT1", "RCH1", "DEY1"], ["XSP1", "GLO1"]),
    new(3, 575, ["LR1", "ODP1", "SYF1", "KNH1", "MCSN1", "HSK1"], ["CC1", "XSP2", "GLO2"]),
    new(5, 830, ["BR1", "MD1", "IOM1", "EP1", "KNB1", "GVS1"], [ShipSystems.UnlockTalent, "XSP3", "GLO3"]),
    new(10, 1085, ["MBR1", "DG1", "KD1", "FSD1", "AE1", "CRM1"], ["SYU1", "XSP4", "GLO4"]),
    new(16, 1340, ["CN1", "HICM1", "CHR1", "SR1", "MRS1", "EXE1"], ["SGU1", "XSP5", "GLO5"]),
    new(20, 1595, ["SGR1", "OVC1", "HVO1", "LOP1", "RCO1", "JKP1"], ["CMY1", "GLO6"]),
  ];

  // Talents with run-changing tradeoffs that progression presets never buy.
  public static readonly string[] PlaystyleTalents = ["LOP1"];

  // Talents that rule each other out: owning one locks the others in its group until it is
  // unlearned. Two members side by side in a tier are drawn as an either/or pair.
  public static readonly string[][] ExclusiveGroups =
  [
    // Magma Detonation spends molten spots; Ionized Magma keeps them burning.
    ["MD1", "IOM1"],
    // Chain Reaction only spreads detonations.
    ["IOM1", "CHR1"],
    // Overclock trades damage per hit for fire rate; Heavy Ordnance the other way round.
    ["OVC1", "HVO1"],
  ];

  private static readonly HashSet<string> activeTalents = Tiers
    .SelectMany(tier => tier.Talents).ToHashSet();
  private static readonly HashSet<string> freeRewards = Tiers
    .SelectMany(tier => tier.FreeRewards).ToHashSet();

  // Each talent's rivals, looked up every frame while the tree is open.
  private static readonly Dictionary<string, string[]> rivals = ExclusiveGroups
    .SelectMany(group => group).Distinct()
    .ToDictionary(id => id, id => ExclusiveGroups.Where(group => group.Contains(id))
      .SelectMany(group => group).Where(other => other != id).Distinct().ToArray());

  // One side of every either/or choice, for builds that take everything: walking the tree
  // in order, a talent ruled out by one taken before it is left out.
  public static readonly HashSet<string> CompatibleTalents = BuildCompatibleTalents();

  private static HashSet<string> BuildCompatibleTalents()
  {
    var taken = new HashSet<string>();
    foreach (var id in Tiers.SelectMany(tier => tier.Talents))
      if (!ExclusiveWith(id).Any(taken.Contains)) taken.Add(id);
    return taken;
  }

  // Talents that can be bought with prestige points.
  public static bool IsInTree(string id) => activeTalents.Contains(id);

  // The talents this one rules out.
  public static string[] ExclusiveWith(string id) => rivals.TryGetValue(id, out var others) ? others : [];

  public static bool IsEitherOrPair(string left, string right)
    => left != right && ExclusiveGroups.Any(group => group.Contains(left) && group.Contains(right));

  // Neighbours in a tier that rule each other out, left to right.
  public static readonly (string Left, string Right)[] EitherOrPairs = Tiers
    .SelectMany(tier => tier.Talents.Skip(1).Select((right, index) => (Left: tier.Talents[index], Right: right)))
    .Where(pair => IsEitherOrPair(pair.Left, pair.Right)).ToArray();

  public static bool AreEitherOrNeighbours(string a, string b)
    => EitherOrPairs.Contains((a, b)) || EitherOrPairs.Contains((b, a));

  // The owned talent that rules this one out, if any.
  public static string ExcludedBy(Dictionary<string, UpgradeButton> buttons, string id)
  {
    foreach (string other in ExclusiveWith(id))
      if (buttons.TryGetValue(other, out var button) && button.CurrentLevel > 0) return other;
    return null;
  }

  // Learnable now: its tier is reached and no owned talent rules it out.
  public static bool CanLearn(Dictionary<string, UpgradeButton> buttons, string id)
    => IsInTree(id) && IsUnlocked(buttons, id) && ExcludedBy(buttons, id) == null;

  // An owned talent can be unlearned while every later tier holding talents keeps the
  // points it needs above it.
  public static bool CanUnlearn(Dictionary<string, UpgradeButton> buttons, string id)
  {
    if (!IsInTree(id) || !buttons.TryGetValue(id, out var button) || button.CurrentLevel == 0) return false;
    ulong cost = button.Data.LevelInfo[button.CurrentLevel - 1].Cost;
    for (int tier = TierIndex(id) + 1; tier < Tiers.Length; tier++)
      if (Tiers[tier].Talents.Any(other => buttons.TryGetValue(other, out var owned) && owned.CurrentLevel > 0)
        && SpentPoints(buttons, tier) - cost < (ulong)Tiers[tier].RequiredEarlierPoints)
        return false;
    return true;
  }

  // Rewards claimed for free by reaching their tier; shown in the tree but never bought.
  public static bool IsFreeReward(string id) => freeRewards.Contains(id);

  public static bool IsShown(string id) => IsInTree(id) || IsFreeReward(id);

  // Every tier's talents sit on one grid, so the columns line up; rows fill from the left.
  // The grid narrows when a tier holds more talents than fit at full spacing.
  private const int TalentGridCenter = 1690;
  private const int TalentGridMaxWidth = 1860;
  private const int MaxTalentSpacing = 340;
  // Half a talent button's width at its 1.7 size scale.
  private const int TalentHalfWidth = 43;
  public static readonly int TalentColumns = Tiers.Max(tier => tier.Talents.Length);
  public static readonly int TalentSpacing = Math.Min(MaxTalentSpacing, TalentGridMaxWidth / Math.Max(1, TalentColumns - 1));
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
      int firstX = TalentGridCenter - (TalentColumns - 1) * TalentSpacing / 2 - TalentHalfWidth;
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

  private static readonly Dictionary<string, int> tierById = BuildTierIndex();

  private static Dictionary<string, int> BuildTierIndex()
  {
    var index = new Dictionary<string, int>();
    for (int tier = 0; tier < Tiers.Length; tier++)
      foreach (var id in Tiers[tier].Talents.Concat(Tiers[tier].FreeRewards))
        index.TryAdd(id, tier);
    return index;
  }

  public static int TierIndex(string id) => tierById.TryGetValue(id, out int tier) ? tier : -1;

  // Points spent per tier, recomputed only when a level in the tree changes: menus ask for
  // tier and expand-space state once per button every frame.
  private static Dictionary<string, UpgradeButton> spentFor;
  private static long spentFingerprint;
  private static readonly ulong[] spentByTier = new ulong[Tiers.Length];

  private static ulong[] SpentByTier(Dictionary<string, UpgradeButton> buttons)
  {
    long fingerprint = buttons.Count;
    foreach (var button in buttons.Values)
      fingerprint = fingerprint * 31 + button.CurrentLevel * 7919 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(button);
    if (spentFor == buttons && spentFingerprint == fingerprint) return spentByTier;
    Array.Clear(spentByTier);
    foreach (var (id, button) in buttons)
    {
      int tier = TierIndex(id);
      if (tier < 0 || IsFreeReward(id)) continue;
      var levels = button.Data.LevelInfo;
      for (int level = 0; level < button.CurrentLevel && level < levels.Count; level++)
        spentByTier[tier] = PrestigeProgression.AddSaturating(spentByTier[tier], levels[level].Cost);
    }
    spentFor = buttons;
    spentFingerprint = fingerprint;
    return spentByTier;
  }

  public static ulong SpentPoints(Dictionary<string, UpgradeButton> buttons, int beforeTier = int.MaxValue)
  {
    var spent = SpentByTier(buttons);
    ulong total = 0;
    for (int tier = 0; tier < spent.Length && tier < beforeTier; tier++)
      total = PrestigeProgression.AddSaturating(total, spent[tier]);
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
  private static readonly bool[] HasExpandSpace = Tiers
    .Select(tier => tier.FreeRewards.Any(id => id.StartsWith("XSP", StringComparison.Ordinal))).ToArray();

  public static int ReachedExpandSpace(Dictionary<string, UpgradeButton> buttons)
  {
    int reached = 0;
    for (int tier = 0; tier < Tiers.Length; tier++)
      if (HasExpandSpace[tier] && IsTierReached(buttons, tier))
        reached++;
    return reached;
  }
}
