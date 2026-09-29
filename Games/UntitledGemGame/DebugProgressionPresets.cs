using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

public static class DebugProgressionPresets
{
  public static readonly string[] Names = ["Beginning", "Early game", "Mid game", "Late game", "Endgame"];

  // Per-level price ceilings keep each snapshot tied to the current upgrade data.
  public static GameSave Create(int stage, Upgrades upgrades)
  {
    if (stage < 0 || stage >= Names.Length) throw new ArgumentOutOfRangeException(nameof(stage));
    ulong[] red = [100, 100_000, 10_000_000, 10_000_000_000, 1_000_000_000_000];
    ulong[] blue = [0, 5, 25, 100, 1_000];
    ulong[] purple = [0, 3, 30, 300, 10_000];
    var save = new GameSave { RedGems = red[stage], BlueGems = blue[stage], PurpleGems = purple[stage] };
    if (stage == 0)
    {
      save.Upgrades["HB"] = 1;
      save.Upgrades["HU1"] = 1;
      RecordHarvesterDiscoveries(save, upgrades);
      return save;
    }

    Fill(upgrades.UpgradeButtons, save.Upgrades, red[stage], stage == 4, int.MaxValue);
    RecordHarvesterDiscoveries(save, upgrades);
    int expansion = upgrades.UpgradeButtons.Values.Where(b => b.Data.UpgradeDefinition.ShortName == "CZS")
      .Sum(b => save.Upgrades.GetValueOrDefault(b.Data.ShortName));
    Fill(upgrades.UpgradeButtonsAbilities, save.Abilities, blue[stage], stage == 4, expansion);
    Fill(upgrades.UpgradeButtonsMeta, save.Meta, purple[stage], stage == 4, expansion);
    save.RedGemsEarnedThisRun = red[stage];
    save.PeakGemsPerMinute = red[stage] / 10.0;
    save.AbilityPointsPurchased = save.BlueGems;
    foreach (var (id, level) in save.Abilities)
      foreach (var info in upgrades.UpgradeButtonsAbilities[id].Data.LevelInfo.Take(level))
        save.AbilityPointsPurchased += info.Cost;
    save.EquippedAbilities = new[] { "GS1", "Speed1", "HBM1", "Drones1", "CM1" }
      .Where(save.Abilities.ContainsKey).ToList();

    bool HasMeta(string name) => upgrades.UpgradeButtonsMeta.Values.Any(b =>
      b.Data.UpgradeDefinition.ShortName == name && save.Meta.ContainsKey(b.Data.ShortName));
    if (HasMeta("SYU"))
    {
      if (stage == 4) save.Modules.DiscoverAllModules();
      else
      {
        save.Modules.StartSalvage(new Random(1234));
        foreach (var module in ModuleCatalog.InventoryOrder.Where(m => (int)ModuleCatalog.Rarities[(int)m] < stage))
          save.Modules.Owned.Add(module);
        save.Modules.DiscoveryRarity = ModuleCatalog.Rarities[(int)ModuleCatalog.InventoryOrder
          .First(m => !save.Modules.Owned.Contains(m))];
        save.Modules.DiscoveryThresholdSeconds = ShipyardModules.MinimumDiscoverySeconds(save.Modules.DiscoveryRarity.Value);
      }
      // Modules are unique across the fleet. Fill the two base bays per class.
      var available = new Queue<ShipModule>(save.Modules.GetAvailableModules());
      for (int type = 0; type < ModuleCatalog.Types.Length; type++)
        for (int slot = 0; slot < ModuleCatalog.BaseSlotsPerType && available.Count > 0; slot++)
          save.Modules.Slots[type * ModuleCatalog.MaxSlotsPerType + slot] = available.Dequeue();
    }
    // Signals stack indefinitely. Endgame supplies one of every rarity for every signal.
    if (stage >= 2 && HasMeta("SGU"))
      for (int signal = 0; signal < SignalProgression.SignalCount; signal++)
        for (int rarity = 0; rarity < (stage == 4 ? SignalProgression.RarityCount : stage - 1); rarity++)
        {
          save.Signals.Counts[signal * SignalProgression.RarityCount + rarity] = 1;
          save.Signals.ScansPurchased++;
        }
    return save;
  }

  private static void RecordHarvesterDiscoveries(GameSave save, Upgrades upgrades)
  {
    foreach (var (id, level) in save.Upgrades)
      if (level > 0 && upgrades.UpgradeButtons.TryGetValue(id, out var button)
        && UpgradeManager.GetHarvesterUnlockAchievementId(button.Data.UpgradeDefinition.ShortName) is { } achievement)
        save.HarvesterUnlockAchievements.Add(achievement);
  }

  private static void Fill(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels,
    ulong ceiling, bool max, int expansion)
  {
    // Iterate to a fixed point so JSON ordering does not affect prerequisite resolution.
    bool changed;
    do
    {
      changed = false;
      foreach (var (id, button) in buttons)
      {
        if (id is "P1" or "ResetAbilities1") continue; // Repeatable actions, not progression.
        var data = button.Data;
        if (!string.IsNullOrEmpty(data.BlockedBy) && !levels.ContainsKey(data.BlockedBy)) continue;
        int level = levels.GetValueOrDefault(id);
        int previous = level;
        while (level < Math.Min(data.NumLevels, data.LevelInfo.Count)
          && (max || (data.LevelInfo[level].Cost <= ceiling
            && data.LevelInfo[level].RequiredExpandSpaceLevel <= expansion))) level++;
        if (level > previous) { levels[id] = level; changed = true; }
      }
    } while (changed);
  }
}
