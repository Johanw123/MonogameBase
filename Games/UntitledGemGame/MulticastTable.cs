using System;

namespace UntitledGemGame
{
  public readonly record struct MulticastChances(int Double, int Triple, int Quadruple, int Quintuple)
  {
    public int Single => 100 - Double - Triple - Quadruple - Quintuple;

    public string Describe() => $"1x: {Single}% | 2x: {Double}% | 3x: {Triple}%\n4x: {Quadruple}% | 5x: {Quintuple}%";
    // public string Describe() => $"1x: {Single}% | 2x: {Double}% | 3x: {Triple}%\n4x: {Quadruple}% | 5x: {Quintuple}%";
  }

  public static class MulticastTable
  {
    // Each row contains mutually exclusive chances, not independent rolls.
    // The remaining percentage casts once. Row 0 is the boolean unlock.
    private static readonly MulticastChances[] Levels =
    {
      new(10,  0, 0, 0),
      new(16,  4,  0,  0),
      new(22,  7,  2,  1),
      new(26, 10,  4,  3),
      new(28, 13,  7,  5),
      new(30, 15, 10,  8),
      new(30, 18, 12, 10),
      new(30, 20, 14, 12),
      new(30, 22, 16, 15),
      new(30, 24, 18, 18),
      new(30, 25, 20, 20),
    };

    public static int MaxLevel => Levels.Length - 1;

    public static MulticastChances GetChances(int level) => Levels[Math.Clamp(level, 0, MaxLevel)];

    // rollPercent is uniform in [0, 100). Extra casts do not reroll multicast.
    public static int GetCastCount(bool unlocked, int level, double rollPercent)
    {
      if (!unlocked) return 1;

      var chances = GetChances(level);
      if (rollPercent < chances.Quintuple) return 5;
      rollPercent -= chances.Quintuple;
      if (rollPercent < chances.Quadruple) return 4;
      rollPercent -= chances.Quadruple;
      if (rollPercent < chances.Triple) return 3;
      rollPercent -= chances.Triple;
      if (rollPercent < chances.Double) return 2;
      return 1;
    }
  }
}
