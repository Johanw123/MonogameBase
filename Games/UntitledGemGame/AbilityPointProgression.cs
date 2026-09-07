using System;

public static class AbilityPointProgression
{
  // Tune these independently of PrestigeProgression.
  public const ulong RedGemsPerFirstPoint = 100;
  // Price is approximately first-point cost × (purchases + 1)^(1 / exponent).
  // A larger exponent makes prices grow more slowly.
  public const double EarningsExponent = 0.3;

  public static ulong? GetPrice(ulong pointsPurchased)
  {
    if (pointsPurchased == ulong.MaxValue) return null;
    ulong point = pointsPurchased + 1;
    if (point > GetPointAtPrice(ulong.MaxValue)) return null;

    // Match the curve's integer thresholds, including floating-point boundaries.
    ulong low = 0;
    ulong high = ulong.MaxValue;
    while (low < high)
    {
      ulong middle = low + (high - low) / 2;
      if (GetPointAtPrice(middle) >= point)
        high = middle;
      else
        low = middle + 1;
    }
    return low;
  }

  private static ulong GetPointAtPrice(ulong price)
    => price < RedGemsPerFirstPoint ? 0
      : (ulong)Math.Floor(Math.Pow((double)price / RedGemsPerFirstPoint, EarningsExponent));
}
