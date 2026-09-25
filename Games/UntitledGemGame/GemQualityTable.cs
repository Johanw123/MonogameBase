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
    // Initial quality plus ten upgrades. Each new color already has a chance
    // in the row used when its tree node becomes available. Locked-color rolls
    // fall back to red; quality also improves colors already unlocked.
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

    // Shared by normal spawns and abilities so quality, value, and luck stay consistent.
    public static GemSpawnData RollCurrent(GemSpawnData? sharedQuality = null, float valueMultiplier = 1.0f)
    {
      var upgrades = UpgradeManager.Instance.UG;
      GemSpawnData gemSpawn = sharedQuality
        ?? Roll(upgrades.GemSpawnQuality, BaseStats.GetCurrentGemValue());

      if (upgrades.LuckyGems
        && Random.Shared.NextSingle() < Math.Clamp(upgrades.LuckyGemChance, 0.0f, 1.0f))
      {
        valueMultiplier *= SignalStats.LuckyValue;
        gemSpawn.IsLucky = true;
      }

      double multipliedValue = gemSpawn.BaseValue * Math.Max(1.0, valueMultiplier);
      gemSpawn.BaseValue = (uint)Math.Min(Math.Round(multipliedValue), uint.MaxValue);
      return gemSpawn;
    }

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
