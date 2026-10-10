using System;

// Prestige points come from sustained gem income, as in The Gnorp Apologue: the
// value of the gems delivered over the last minute charges the extraction panel's
// bar, and each time it fills the run earns a point, paid at extraction. Every point
// ever earned raises the next one's bar, so a run only pays when it beats the runs
// before it; repeating a short run pays nothing. Damage is the other half: it cracks
// the core for Core Shards (CoreFracture), and only gems that get collected count here.
//
// A run that falls short still counts a little. At extraction the loop remembers the
// square of the run's best progress toward the next point (the echo), and the next
// run's bar starts that full: halfway there banks a quarter of a point.
public static class PrestigeProgression
{
  // Gem income per minute for the first point; each point after needs ThresholdGrowth times
  // more, and each step is steeper than the last. Tuned with autoplay playthroughs
  // (Simulation/BALANCE.md, 2026-10-10, after the weapon baseline buff): a first run levels
  // off around 350K gems a minute and earns two points. After that each loop earns a point
  // or two from the talents' Gem Lore (CoreExtraction).
  public const double FirstThreshold = 150_000;
  public const double ThresholdGrowth = 1.8;
  // Each point's step is this much steeper than the one before, so points come harder and
  // harder while gem income keeps climbing into huge numbers.
  public const double GrowthSteepening = 1.01;
  // ...but never steeper than the best talent's Gem Lore: late in the game income grows by
  // Gem Lore alone (the weapons fill the field and collection sets the pace), so a steeper
  // step would stall progress before the talent tree is full.
  public const double MaxThresholdStep = 2.2;
  // The first point whose step is capped.
  private static readonly double CappedFrom
    = Math.Ceiling(Math.Log(MaxThresholdStep / ThresholdGrowth) / Math.Log(GrowthSteepening));

  // Gem income per minute needed for the next point, after this many ever.
  public static double Threshold(ulong earned)
  {
    double steepening = Math.Min(earned, CappedFrom);
    return FirstThreshold * Math.Exp(steepening * Math.Log(ThresholdGrowth)
      + steepening * (steepening - 1) / 2 * Math.Log(GrowthSteepening)
      + (earned - steepening) * Math.Log(MaxThresholdStep));
  }

  // The share of the next point's bar this income fills, from 0.
  public static double Progress(double incomePerMinute, ulong earned)
  {
    double progress = incomePerMinute / Threshold(earned);
    return double.IsFinite(progress) && progress > 0 ? progress : 0;
  }

  public static double SanitizeEcho(double echo) => double.IsFinite(echo) ? Math.Clamp(echo, 0, 1) : 0;

  // The echo the next loop starts with, after a run that got this far toward the next point.
  public static double BankEcho(double echo, double bestProgress)
  {
    double best = SanitizeEcho(bestProgress);
    return SanitizeEcho(SanitizeEcho(echo) + best * best);
  }

  public static ulong AddSaturating(ulong current, ulong amount)
    => amount > ulong.MaxValue - current ? ulong.MaxValue : current + amount;
}
