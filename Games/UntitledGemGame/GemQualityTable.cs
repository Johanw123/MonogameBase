using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame
{
  public readonly struct GemQualityEntry
  {
    public GemTypes Type { get; }
    public float ChancePercent { get; }
    public uint ValueMultiplier { get; }

    public GemQualityEntry(GemTypes type, float chancePercent, uint valueMultiplier)
    {
      Type = type;
      ChancePercent = chancePercent;
      ValueMultiplier = valueMultiplier;
    }
  }

  public static class GemQualityTable
  {
    // Harder hits reach deeper planet layers: a hit's colors depend only on the
    // fire power of the weapon that made it (signals included). Each color appears
    // once fire power reaches its threshold.
    public static readonly (GemTypes Type, int FirePower)[] ColorFirePower =
    {
      (GemTypes.LightGreen, 3),
      (GemTypes.Blue, 6),
      (GemTypes.Teal, 10),
      (GemTypes.Lilac, 14),
      (GemTypes.Purple, 20),
      (GemTypes.Gold, 27),
      (GemTypes.DarkBlue, 35),
    };

    public static int RequiredFirePower(GemTypes type)
    {
      if (type == GemTypes.Red) return 0;
      foreach (var (color, firePower) in ColorFirePower)
        if (color == type) return firePower;
      return int.MaxValue;
    }

    public static bool IsUnlocked(GemTypes type, int firePower) => firePower >= RequiredFirePower(type);

    // Fire power past the last color keeps improving the odds, one row per step.
    public const int FirePowerPerExtraRow = 8;

    public static string FirePowerTooltip(int firePower)
    {
      foreach (var (color, required) in ColorFirePower)
        if (firePower < required)
          return $"Fire power {firePower}. {ColorName(color)} gems appear at {required}.";
      return $"Fire power {firePower}. Every gem color is unlocked.";
    }

    private static string ColorName(GemTypes type) => type switch
    {
      GemTypes.LightGreen => "Light green",
      GemTypes.DarkBlue => "Dark blue",
      _ => type.ToString(),
    };

    // Drop tables, best last. Row k lists the first k + 1 colors after red, so a
    // hit that has unlocked k colors uses row k (the next color drops as red until
    // unlocked); fire power beyond the last color moves on through the final rows.
    public static readonly GemQualityEntry[][] Levels =
    {
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 92.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 8.00f, 2),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 80.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 15.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 5.00f, 4),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 65.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 20.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 10.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 5.00f, 8),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 55.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 23.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 13.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 7.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 2.00f, 16),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 45.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 25.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 16.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 9.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 4.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 1.00f, 32),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 38.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 25.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 18.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 11.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 5.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 2.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 1.00f, 64),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 32.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 25.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 20.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 12.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 6.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 3.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 1.50f, 64),
        new GemQualityEntry(GemTypes.DarkBlue, 0.50f, 128),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 28.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 24.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 21.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 13.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 7.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 4.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 2.00f, 64),
        new GemQualityEntry(GemTypes.DarkBlue, 1.00f, 128),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 23.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 23.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 22.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 15.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 8.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 5.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 2.50f, 64),
        new GemQualityEntry(GemTypes.DarkBlue, 1.50f, 128),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 19.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 22.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 22.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 16.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 10.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 6.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 3.25f, 64),
        new GemQualityEntry(GemTypes.DarkBlue, 1.75f, 128),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red, 15.00f, 1),
        new GemQualityEntry(GemTypes.LightGreen, 20.00f, 2),
        new GemQualityEntry(GemTypes.Blue, 22.00f, 4),
        new GemQualityEntry(GemTypes.Teal, 18.00f, 8),
        new GemQualityEntry(GemTypes.Lilac, 12.00f, 16),
        new GemQualityEntry(GemTypes.Purple, 7.00f, 32),
        new GemQualityEntry(GemTypes.Gold, 4.00f, 64),
        new GemQualityEntry(GemTypes.DarkBlue, 2.00f, 128),
      },
    };

    // Shared by weapons and abilities so color and value rules stay consistent.
    public static GemSpawnData Roll(int firePower, float valueMultiplier = 1.0f)
    {
      Span<GemQualityEntry> outcomes = stackalloc GemQualityEntry[MaxOutcomes];
      int count = GetOutcomes(firePower, outcomes);
      float roll = Random.Shared.NextSingle() * 100.0f;
      float cumulativeChance = 0.0f;
      int chosen = count - 1; // Protects against tiny floating-point gaps.
      for (int i = 0; i < count; i++)
      {
        cumulativeChance += outcomes[i].ChancePercent;
        if (roll < cumulativeChance)
        {
          chosen = i;
          break;
        }
      }
      var spawn = CreateSpawnData(outcomes[chosen], BaseStats.GetCurrentGemValue());
      double value = spawn.BaseValue * Math.Max(1.0, valueMultiplier);
      spawn.BaseValue = (uint)Math.Min(Math.Round(value), uint.MaxValue);
      return spawn;
    }

    public static int RowFor(int firePower)
    {
      int unlocked = 0;
      foreach (var (_, required) in ColorFirePower)
        if (firePower >= required) unlocked++;
      int extra = unlocked == ColorFirePower.Length
        ? (firePower - ColorFirePower[^1].FirePower) / FirePowerPerExtraRow
        : 0;
      return Math.Clamp(unlocked + extra, 0, Levels.Length - 1);
    }

    public static double ExpectedValueMultiplier(int firePower)
    {
      Span<GemQualityEntry> outcomes = stackalloc GemQualityEntry[MaxOutcomes];
      int count = GetOutcomes(firePower, outcomes);
      double value = 0;
      for (int i = 0; i < count; i++)
        value += outcomes[i].ChancePercent / 100.0 * outcomes[i].ValueMultiplier;
      return value;
    }

    private const int MaxOutcomes = 8;

    // The row for this fire power, with any still-locked color dropping as red.
    private static int GetOutcomes(int firePower, Span<GemQualityEntry> outcomes)
    {
      float red = 0f;
      int count = 1;
      foreach (var entry in Levels[RowFor(firePower)])
      {
        if (entry.Type != GemTypes.Red && IsUnlocked(entry.Type, firePower))
          outcomes[count++] = entry;
        else
          red += entry.ChancePercent;
      }
      outcomes[0] = new GemQualityEntry(GemTypes.Red, red, 1);
      return count;
    }

    public static Color GetColor(GemTypes type)
    {
      return type switch
      {
        GemTypes.Red => new Color(255, 0, 20, 0),
        GemTypes.LightGreen => new Color(20, 150, 45, 0),
        GemTypes.Blue => new Color(45, 70, 255, 0),
        GemTypes.Teal => new Color(0, 145, 125, 0),
        GemTypes.Lilac => new Color(180, 80, 255, 0),
        GemTypes.Purple => new Color(110, 20, 220, 0),
        GemTypes.Gold => new Color(205, 115, 0, 0),
        GemTypes.DarkBlue => new Color(15, 20, 130, 0),
        _ => new Color(255, 0, 20, 0),
      };
    }

    private static GemSpawnData CreateSpawnData(GemQualityEntry entry, uint baseValue)
    {
      ulong scaledValue = (ulong)baseValue * entry.ValueMultiplier;
      return new GemSpawnData
      {
        Type = entry.Type,
        BaseValue = (uint)Math.Min(scaledValue, uint.MaxValue),
      };
    }
  }
}
