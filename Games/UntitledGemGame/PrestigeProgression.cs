using System;

public static class PrestigeProgression
{
  public const ulong RedGemsPerFirstPoint = 100_000;
  public const double EarningsExponent = 0.4;

  public static ulong GetReward(ulong runEarnings)
  {
    if (runEarnings < RedGemsPerFirstPoint)
      return 0;

    return (ulong)Math.Floor(Math.Pow(
      (double)runEarnings / RedGemsPerFirstPoint, EarningsExponent));
  }

  public static ulong AddSaturating(ulong current, ulong amount)
    => amount > ulong.MaxValue - current ? ulong.MaxValue : current + amount;

  // Find the first integer earnings value that actually pays this reward.
  // Using GetReward keeps the HUD consistent at floating-point boundaries.
  public static ulong? GetRequiredEarnings(ulong reward)
  {
    if (reward == 0) return 0;
    if (reward > GetReward(ulong.MaxValue)) return null;

    ulong low = 0;
    ulong high = ulong.MaxValue;
    while (low < high)
    {
      ulong middle = low + (high - low) / 2;
      if (GetReward(middle) >= reward)
        high = middle;
      else
        low = middle + 1;
    }
    return low;
  }
}
