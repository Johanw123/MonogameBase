using System;

namespace UntitledGemGame;

// Weapons keep dealing damage when the field is full, and those gems used to be lost.
// They are now buried in the debris: a per-run pool that collectors with dig upgrades
// (Deep Scoops, Deep Grip, Deep Rails) draw from, bringing extra gems home with each
// pickup. Collection can therefore grow with the weapons without more gems on screen.
public static class BuriedGems
{
  // Enough to ride out a lull in the firing, not a bank to save up for hours.
  public const double PoolLimitPerFieldGem = 20;

  public static double Pool { get; private set; }

  private static double Limit => PoolLimitPerFieldGem * Math.Max(1, SignalStats.GemLimit);

  // A point of planet damage: one gem, loose on the field or buried.
  public static void OnDamage(double damage)
  {
    if (damage > 0) Pool = Math.Min(Limit, Pool + damage);
  }

  // The gem made it onto the field after all.
  public static void OnGemLoose()
  {
    if (Pool >= 1) Pool -= 1;
    else Pool = 0;
  }

  // Up to `wanted` buried gems, dug up beside a pickup.
  public static int Dig(int wanted)
  {
    if (wanted <= 0 || Pool < 1) return 0;
    int dug = (int)Math.Min(wanted, Math.Floor(Pool));
    Pool -= dug;
    return dug;
  }

  public static void Reset() => Pool = 0;
}
