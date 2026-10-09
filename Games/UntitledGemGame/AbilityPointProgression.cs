using System;
using UntitledGemGame;

public static class AbilityPointProgression
{
  // Keep the first few purchases accessible, then compound the late-game cost.
  public const ulong RedGemsPerFirstPoint = 50;
  public const int EarlyPointCount = 8;
  public const double LatePointGrowthMultiplier = 1.4;

  public static ulong? GetPrice(ulong pointsPurchased)
  {
    if (pointsPurchased == ulong.MaxValue) return null;
    double point = pointsPurchased + 1;
    // Late cells inflate like the regular tree's prices (PriceInflation).
    double price = Math.Ceiling(PriceInflation.Apply(RedGemsPerFirstPoint * point * point
      * Math.Pow(LatePointGrowthMultiplier, Math.Max(0, point - EarlyPointCount))));

    // ulong.MaxValue rounds up to 2^64 as a double. Reject that boundary before
    // conversion so exhausted prices cannot overflow into cheap/free purchases.
    if (!double.IsFinite(price) || price >= (double)ulong.MaxValue) return null;
    return (ulong)price;
  }
}
