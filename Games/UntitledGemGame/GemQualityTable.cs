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
    // Each row totals 100%. GemSpawnQuality starts at 1 and has five upgrades,
    // so the normal progression uses rows 1 through 6.
    public static readonly GemQualityEntry[][] Levels =
    {
      new[]
      {
        new GemQualityEntry(GemTypes.Red,        100.0f,   1),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red,         92.0f,   1),
        new GemQualityEntry(GemTypes.LightGreen,   8.0f,   2),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red,         80.0f,   1),
        new GemQualityEntry(GemTypes.LightGreen,  15.0f,   2),
        new GemQualityEntry(GemTypes.Blue,         5.0f,   4),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red,         65.0f,   1),
        new GemQualityEntry(GemTypes.LightGreen,  20.0f,   2),
        new GemQualityEntry(GemTypes.Blue,        10.0f,   4),
        new GemQualityEntry(GemTypes.Teal,         4.0f,   8),
        new GemQualityEntry(GemTypes.Lilac,        1.0f,  16),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red,         50.0f,   1),
        new GemQualityEntry(GemTypes.LightGreen,  23.0f,   2),
        new GemQualityEntry(GemTypes.Blue,        14.0f,   4),
        new GemQualityEntry(GemTypes.Teal,         8.0f,   8),
        new GemQualityEntry(GemTypes.Lilac,        3.0f,  16),
        new GemQualityEntry(GemTypes.Purple,       1.2f,  32),
        new GemQualityEntry(GemTypes.Gold,         0.6f,  64),
        new GemQualityEntry(GemTypes.DarkBlue,     0.2f, 128),
      },
      new[]
      {
        new GemQualityEntry(GemTypes.Red,         35.0f,   1),
        new GemQualityEntry(GemTypes.LightGreen,  25.0f,   2),
        new GemQualityEntry(GemTypes.Blue,        18.0f,   4),
        new GemQualityEntry(GemTypes.Teal,        11.0f,   8),
        new GemQualityEntry(GemTypes.Lilac,        6.0f,  16),
        new GemQualityEntry(GemTypes.Purple,       3.0f,  32),
        new GemQualityEntry(GemTypes.Gold,         1.5f,  64),
        new GemQualityEntry(GemTypes.DarkBlue,     0.5f, 128),
      },
    };

    public static GemSpawnData Roll(int qualityLevel, uint baseValue)
    {
      int levelIndex = Math.Clamp(qualityLevel - 1, 0, Levels.Length - 1);
      GemQualityEntry[] entries = Levels[levelIndex];
      float roll = Random.Shared.NextSingle() * 100.0f;
      float cumulativeChance = 0.0f;

      for (int i = 0; i < entries.Length; i++)
      {
        cumulativeChance += entries[i].ChancePercent;
        if (roll < cumulativeChance)
        {
          if (IsUnlocked(entries[i].Type))
            return CreateSpawnData(entries[i], baseValue);

          return CreateRedSpawnData(baseValue);
        }
      }

      // Protect against tiny floating-point gaps if the table is edited later.
      GemQualityEntry fallbackEntry = entries[entries.Length - 1];
      return IsUnlocked(fallbackEntry.Type)
        ? CreateSpawnData(fallbackEntry, baseValue)
        : CreateRedSpawnData(baseValue);
    }

    private static bool IsUnlocked(GemTypes type)
    {
      var upgrades = UpgradeManager.Instance.UG;
      return type switch
      {
        GemTypes.Red => true,
        GemTypes.LightGreen => upgrades.LightGreenGemUnlocked,
        GemTypes.Blue => upgrades.BlueGemUnlocked,
        GemTypes.Teal => upgrades.TealGemUnlocked,
        GemTypes.Lilac => upgrades.LilacGemUnlocked,
        GemTypes.Purple => upgrades.PurpleGemUnlocked,
        GemTypes.Gold => upgrades.GoldGemUnlocked,
        GemTypes.DarkBlue => upgrades.DarkBlueGemUnlocked,
        _ => false,
      };
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

    private static GemSpawnData CreateRedSpawnData(uint baseValue)
    {
      return new GemSpawnData
      {
        Type = GemTypes.Red,
        BaseValue = baseValue,
      };
    }
  }
}
