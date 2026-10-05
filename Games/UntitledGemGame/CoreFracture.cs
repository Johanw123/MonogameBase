using System;

namespace UntitledGemGame;

// Weapons damage the planet, and every point of damage knocks one gem loose while
// the field has room. Damage dealt to a full field still counts.
//
// Sustained damage cracks the core: whenever the damage dealt over the last minute
// reaches the next threshold, the planet fractures (GameScreen.CoreFracture.cs). It
// shakes, swallows every loose gem, erupts with far more, and a Core Shard flies out
// of the new crack. Each fracture raises the next threshold; all reset at extraction.
// The player is never shown the damage or the thresholds: each fracture is a surprise.
public static class CoreFracture
{
  public const double FirstThreshold = 1_000;
  public const double ThresholdGrowth = 4;

  // Damage per minute needed for the next fracture, after this many this run.
  public static double Threshold(int fractures)
    => FirstThreshold * Math.Pow(ThresholdGrowth, Math.Max(0, fractures));

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

// Damage dealt over the last minute, in one-second buckets: a full minute of whole
// seconds plus the second in progress.
public sealed class PlanetDamageTracker
{
  public const int WindowSeconds = 60;
  private readonly double[] buckets = new double[WindowSeconds + 1];
  private int current;
  private float bucketTime;
  private double total;

  public double PerMinute => total;

  public void Record(double damage)
  {
    if (!(damage > 0) || double.IsInfinity(damage)) return;
    buckets[current] += damage;
    total += damage;
  }

  public void Update(float dt)
  {
    if (!(dt > 0) || float.IsInfinity(dt)) return;
    bucketTime += dt;
    if (bucketTime < 1f) return;
    // A long frame can skip several seconds; a minute or more empties the window.
    int steps = (int)Math.Min(bucketTime, buckets.Length);
    bucketTime -= (float)Math.Floor(bucketTime);
    for (int i = 0; i < steps; i++)
    {
      current = (current + 1) % buckets.Length;
      buckets[current] = 0;
    }
    // Re-sum instead of subtracting, so rounding never drifts.
    total = 0;
    foreach (double bucket in buckets) total += bucket;
  }

  public void Reset()
  {
    Array.Clear(buckets);
    current = 0;
    bucketTime = 0f;
    total = 0;
  }
}
