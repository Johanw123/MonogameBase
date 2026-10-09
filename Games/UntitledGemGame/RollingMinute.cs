using System;

namespace UntitledGemGame;

// An amount over the last minute (planet damage, gem income), in one-second buckets:
// a full minute of whole seconds plus the second in progress.
public sealed class RollingMinute
{
  public const int WindowSeconds = 60;
  private readonly double[] buckets = new double[WindowSeconds + 1];
  private int current;
  private float bucketTime;
  private double total;

  public double PerMinute => total;

  public void Record(double amount)
  {
    if (!(amount > 0) || double.IsInfinity(amount)) return;
    buckets[current] += amount;
    total += amount;
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
