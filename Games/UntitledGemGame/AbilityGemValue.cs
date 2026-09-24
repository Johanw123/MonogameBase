using System;

namespace UntitledGemGame;

public static class AbilityGemValue
{
  // Preserve the expected bonus even for one-value gems, without rounding every
  // pickup up or discarding its fractional bonus. Integer arithmetic avoids drift.
  public static uint AddBonus(uint value, int percent)
    => AddBonus(value, percent, Random.Shared.Next(100));

  public static uint AddBonus(uint value, int percent, int roll)
  {
    ulong bonus = (ulong)value * (uint)Math.Max(0, percent);
    ulong result = value + bonus / 100 + ((ulong)roll < bonus % 100 ? 1UL : 0UL);
    return (uint)Math.Min(uint.MaxValue, result);
  }
}
