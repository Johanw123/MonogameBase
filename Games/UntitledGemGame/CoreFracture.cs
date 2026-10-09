using System;

namespace UntitledGemGame;

// Weapons damage the planet, and every point of damage knocks one gem loose while
// the field has room. Damage dealt to a full field still counts.
//
// Once the planet's shell is gone (PlanetShell), sustained damage cracks the core:
// whenever the damage dealt over the last minute reaches the next threshold, the
// planet fractures (GameScreen.CoreFracture.cs). It shakes, swallows every loose gem,
// erupts with far more, and a Core Shard flies out of the new crack. Each fracture
// raises the next threshold; all reset at extraction. The HUD's Damage panel shows
// the damage per minute, but never the thresholds: each fracture is a surprise.
public static class CoreFracture
{
  public const double FirstThreshold = 1_000;
  public const double ThresholdGrowth = 4;
  // Each fracture's step is this much steeper than the one before, so late shards in a run
  // come harder and harder even as the weapons grow (as on the prestige ladder).
  public const double GrowthSteepening = 1.25;

  // Damage per minute needed for the next fracture, after this many this run.
  public static double Threshold(int fractures)
  {
    int n = Math.Max(0, fractures);
    return FirstThreshold * Math.Pow(ThresholdGrowth, n) * Math.Pow(GrowthSteepening, n * (n - 1) / 2.0);
  }

  // The eruption returns the swallowed gems twice over, with at least a few seconds
  // of the damage that caused it, from layers below the strongest weapon's reach.
  public const float EruptionMultiplier = 2f;
  public const float EruptionMinimumSeconds = 5f;
  public const int EruptionLayers = 2;

  public static long EruptionGems(long swallowed, double damagePerMinute)
    => (long)Math.Max(swallowed * (double)EruptionMultiplier, damagePerMinute / 60 * EruptionMinimumSeconds);

  // The planet swells with every fracture, up to a limit: late in a run, with the
  // field full of gems and effects, it stays easy to see.
  public const float PlanetGrowthPerFracture = 0.07f;
  public const int MaxGrowthFractures = 8;

  public static float PlanetSize(int fractures)
    => 1f + Math.Clamp(fractures, 0, MaxGrowthFractures) * PlanetGrowthPerFracture;

  // An uncollected shard flies to the homebase on its own after this long.
  public const float ShardAutoCollectSeconds = 12f;
}
