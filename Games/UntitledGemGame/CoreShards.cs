using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public enum ObjectiveMetric { GemsEarned, PeakGemsPerMinute, FleetSize, HarvesterClasses, WeaponsOnline }

// How far the current run has come, measured the same way by the game and the simulator.
public readonly record struct RunObjectiveStats(double GemsEarned, double PeakGemsPerMinute,
  int FleetSize, int HarvesterClasses, int WeaponsOnline)
{
  public double Value(ObjectiveMetric metric) => metric switch
  {
    ObjectiveMetric.GemsEarned => GemsEarned,
    ObjectiveMetric.PeakGemsPerMinute => PeakGemsPerMinute,
    ObjectiveMetric.FleetSize => FleetSize,
    ObjectiveMetric.HarvesterClasses => HarvesterClasses,
    ObjectiveMetric.WeaponsOnline => WeaponsOnline,
    _ => 0,
  };
}

public sealed record RunObjective(string Id, string Title, ObjectiveMetric Metric, double Target, ulong Reward);

// Core Shards are a rare per-run currency paid by run objectives and spent on the
// powerful upgrades in the regular tree. A complete run cannot fund every powerful
// upgrade, so each run commits to a build. Shards, completed objectives and the
// upgrades bought with them all reset at prestige.
public static class CoreShards
{
  public const string Currency = "gold";
  public const string Name = "Core Shards";
  public static readonly Color Color = new(255, 200, 90);
  public const string IconPath = "Textures/Gems/Gem4/GEM 4 - GOLD - Spritesheet.png";
  public const int IconFrames = 11;

  // Powerful upgrades outside the weapons; weapon ones are tuned in MainShipWeapons.
  public const float MidasTouchValueMultiplier = 3f;
  public const float GoldenHoldsValueMultiplier = 2f;

  // One shard each, early to late. Every Core Shard upgrade costs one shard and there
  // are more of them than a perfect run's objectives, so no run can buy them all.
  public static readonly RunObjective[] Objectives =
  [
    new("earn_10k", "Earn 10K gems", ObjectiveMetric.GemsEarned, 10_000, 1),
    new("fleet_10", "Command 10 harvesters", ObjectiveMetric.FleetSize, 10, 1),
    new("income_1m", "Reach 1M gems / min", ObjectiveMetric.PeakGemsPerMinute, 1_000_000, 1),
    new("weapons_5", "Bring all 5 weapons online", ObjectiveMetric.WeaponsOnline, 5, 1),
    new("earn_10b", "Earn 10B gems", ObjectiveMetric.GemsEarned, 10_000_000_000, 1),
    new("classes_5", "Field all 5 harvester classes", ObjectiveMetric.HarvesterClasses, 5, 1),
  ];

  public static ulong TotalReward
  {
    get
    {
      ulong total = 0;
      foreach (var objective in Objectives) total += objective.Reward;
      return total;
    }
  }

  public static bool IsComplete(RunObjective objective, RunObjectiveStats stats)
    => stats.Value(objective.Metric) >= objective.Target;

  public static float Progress(RunObjective objective, RunObjectiveStats stats)
    => (float)Math.Clamp(stats.Value(objective.Metric) / objective.Target, 0, 1);

  public static RunObjectiveStats Measure(UpgradesGeneratorUpgrades ug, ulong gemsEarned, double peakGemsPerMinute)
  {
    int fleet = PrestigeTalentEffects.FleetCount(ug.HarvesterCount)
      + PrestigeTalentEffects.FleetCount(ug.AdvancedHarvesterCount)
      + PrestigeTalentEffects.FleetCount(ug.PerimeterHarvesterCount)
      + PrestigeTalentEffects.FleetCount(ug.ExpertHarvesterCount)
      + PrestigeTalentEffects.FleetCount(ug.UltimateHarvesterCount);
    int classes = (ug.HarvesterUnlocked ? 1 : 0) + (ug.AdvancedHarvesterUnlocked ? 1 : 0)
      + (ug.PerimeterHarvesterUnlocked ? 1 : 0) + (ug.ExpertHarvesterUnlocked ? 1 : 0)
      + (ug.UltimateHarvesterUnlocked ? 1 : 0);
    int weapons = 0;
    foreach (var weapon in MainShipWeapons.All)
      if (MainShipWeapons.IsAutomatic(ug, weapon)) weapons++;
    return new(gemsEarned, peakGemsPerMinute, fleet, classes, weapons);
  }

  public static float ClickValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.MidasTouch ? MidasTouchValueMultiplier : 1f;

  public static float FleetValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.GoldenHolds ? GoldenHoldsValueMultiplier : 1f;
}
