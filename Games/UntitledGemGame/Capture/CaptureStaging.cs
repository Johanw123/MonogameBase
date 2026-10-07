#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UntitledGemGame.Capture;

// Builds the staged save and applies stat overrides, using the same data the
// debug presets, feature tools and stat sliders use.
internal static class Staging
{
  public static readonly string[] Rarities = ["Common", "Uncommon", "Rare", "Epic", "Legendary"];
  private static readonly Dictionary<string, JsonElement> appliedStats = new(StringComparer.OrdinalIgnoreCase);

  public static GameSave BuildSave(CaptureScene scene, Upgrades upgrades)
  {
    var save = Preset(scene.Preset, upgrades);
    var wanted = scene.Save ?? new SceneSave();
    if (wanted.Gems is double gems) save.RedGems = Amount(gems);
    // Gems in hand were earned this run; manual ability unlocks and prestige read this.
    save.RedGemsEarnedThisRun = wanted.EarnedThisRun is double earned ? Amount(earned)
      : Math.Max(save.RedGemsEarnedThisRun, save.RedGems);
    if (wanted.AbilityPoints is double blue) save.BlueGems = Amount(blue);
    if (wanted.PrestigePoints is double purple) save.PurpleGems = Amount(purple);
    if (wanted.ActiveGems is int active)
    {
      save.ActiveGemCount = Math.Max(0, active);
      save.CreatedInitialGems = true;
    }
    SetLevels(upgrades.UpgradeButtons, save.Upgrades, wanted.Upgrades);
    SetLevels(upgrades.UpgradeButtonsAbilities, save.Abilities, wanted.Abilities);
    SetLevels(upgrades.UpgradeButtonsMeta, save.Meta, wanted.Meta);
    if (wanted.Equip != null)
    {
      save.EquippedAbilities = wanted.Equip.Select(Actions.AbilityId).ToList();
      foreach (var id in save.EquippedAbilities)
        if (save.Abilities.GetValueOrDefault(id) <= 0)
          throw new ArgumentException($"Ability {id} is not unlocked in this save; add it to save.abilities");
      // Every online system fills a HUD slot, so systems left out of the list go offline.
      foreach (var tab in ShipSystems.Tabs.Where(tab => !save.EquippedAbilities.Contains(tab.Root)))
        foreach (string id in tab.Rows.SelectMany(row => row))
          save.Abilities.Remove(id);
    }
    // Like the feature tools: system talents imply the Auxiliary Power talent that unlocks them,
    // and Kamikaze Wing talents the Kamikaze Drones talent that swaps it in.
    if (save.Abilities.Count > 0) UnlockMeta(upgrades, save, ShipSystems.UnlockTalent);
    if (save.Abilities.Any(pair => pair.Value > 0 && ShipSystems.TabOf(pair.Key) == ShipSystems.KamikazeWingTab))
      UnlockMeta(upgrades, save, PrestigeTalentEffects.KamikazeDronesTalent);
    if (wanted.Modules is JsonElement modules)
    {
      // Like the feature tools: owning modules implies the shipyard.
      UnlockMeta(upgrades, save, "SYU1");
      save.Modules.StartSalvage(new Random(42));
      if (modules.ValueKind == JsonValueKind.String && modules.GetString() == "all") save.Modules.DiscoverAllModules();
      else
        foreach (var module in modules.EnumerateArray())
        {
          int index = Array.FindIndex(ModuleCatalog.Names, n => Actions.Slug(n) == Actions.Slug(module.GetString()));
          if (index <= 0) throw new ArgumentException($"Unknown module {module} (see --capture-list)");
          save.Modules.Owned.Add((ShipModule)index);
        }
      DebugProgressionPresets.RepairDiscoveryTarget(save.Modules);
    }
    if (wanted.Reveal is { Count: > 0 })
    {
      UnlockMeta(upgrades, save, "SYU1");
      save.Modules.StartSalvage(new Random(42));
      foreach (var name in wanted.Reveal)
      {
        int index = Array.FindIndex(ModuleCatalog.Names, n => Actions.Slug(n) == Actions.Slug(name));
        if (index <= 0) throw new ArgumentException($"Unknown module {name} (see --capture-list)");
        var module = (ShipModule)index;
        save.Modules.Owned.Add(module);
        if (!save.Modules.PendingReveals.Contains(module)) save.Modules.PendingReveals.Add(module);
      }
      DebugProgressionPresets.RepairDiscoveryTarget(save.Modules);
    }
    if (wanted.Fit is { Count: > 0 })
    {
      UnlockMeta(upgrades, save, "SYU1");
      save.Modules.StartSalvage(new Random(42));
      foreach (var (type, names) in wanted.Fit)
      {
        int typeIndex = Array.FindIndex(ModuleCatalog.Types, t => Actions.Slug(t.ToString()).StartsWith(Actions.Slug(type)));
        if (typeIndex < 0) throw new ArgumentException($"Unknown fleet class {type}: {string.Join(", ", ModuleCatalog.Types)}");
        if (names.Count > ModuleCatalog.MaxSlotsPerType)
          throw new ArgumentException($"At most {ModuleCatalog.MaxSlotsPerType} modules fit a class");
        for (int slot = 0; slot < names.Count; slot++)
        {
          int index = Array.FindIndex(ModuleCatalog.Names, n => Actions.Slug(n) == Actions.Slug(names[slot]));
          if (index <= 0) throw new ArgumentException($"Unknown module {names[slot]} (see --capture-list)");
          if (save.Modules.Slots.Contains((ShipModule)index))
            throw new ArgumentException($"{names[slot]} is fitted twice: each module is unique");
          save.Modules.Owned.Add((ShipModule)index);
          save.Modules.Slots[typeIndex * ModuleCatalog.MaxSlotsPerType + slot] = (ShipModule)index;
        }
        // Slots past the base two need the Module Bays ranks.
        if (names.Count > ModuleCatalog.BaseSlotsPerType)
          save.Meta["MS1"] = Math.Max(save.Meta.GetValueOrDefault("MS1"), names.Count - ModuleCatalog.BaseSlotsPerType);
      }
      DebugProgressionPresets.RepairDiscoveryTarget(save.Modules);
    }
    if (wanted.Signals is { Count: > 0 })
    {
      UnlockMeta(upgrades, save, "SYU1", "SGU1");
      foreach (var signal in wanted.Signals)
      {
        int kind = Array.FindIndex(SignalCatalog.Definitions, d => Actions.Slug(d.Name) == Actions.Slug(signal.Name));
        int rarity = Array.FindIndex(Rarities, r => Actions.Slug(r) == Actions.Slug(signal.Rarity));
        if (kind < 0 || rarity < 0) throw new ArgumentException($"Unknown signal {signal.Name} ({signal.Rarity}); see --capture-list");
        save.Signals.Counts[kind * SignalProgression.RarityCount + rarity] += Math.Max(0, signal.Count);
      }
    }
    if (wanted.CoreFractures is int fractures) save.CoreFractures = Math.Max(0, fractures);
    save.ShellDamage = wanted.Shell is double left
      ? PlanetShell.Health * (1 - Math.Clamp(left, 0, 1))
      : PlanetShell.Health;
    return save;
  }

  private static GameSave Preset(string name, Upgrades upgrades)
  {
    int index = int.TryParse(name, out int number) ? number
      : Array.FindIndex(DebugProgressionPresets.Names, n => Actions.Slug(n) == Actions.Slug(name));
    if (index >= 0 && index < DebugProgressionPresets.Names.Length) return DebugProgressionPresets.Create(index, upgrades);
    index = Array.FindIndex(DebugProgressionPresets.FeatureNames, n => Actions.Slug(n) == Actions.Slug(name));
    if (index >= 0) return DebugProgressionPresets.CreateFeature(index, upgrades);
    throw new ArgumentException($"Unknown preset {name}. Presets: {string.Join(", ", DebugProgressionPresets.Names)}. " +
      $"Feature presets: {string.Join(", ", DebugProgressionPresets.FeatureNames)}");
  }

  private static void UnlockMeta(Upgrades upgrades, GameSave save, params string[] ids)
  {
    foreach (var id in ids)
      if (upgrades.UpgradeButtonsMeta.ContainsKey(id) && save.Meta.GetValueOrDefault(id) <= 0) save.Meta[id] = 1;
  }

  private static ulong Amount(double value) => value <= 0 ? 0 : value >= ulong.MaxValue ? ulong.MaxValue : (ulong)value;

  private static int MaxLevel(UpgradeButton button) => Math.Min(button.Data.NumLevels, button.Data.LevelInfo.Count);

  private static int Level(JsonElement? value, UpgradeButton button, int current)
  {
    if (value is not JsonElement element) return Math.Min(current + 1, MaxLevel(button));
    if (element.ValueKind == JsonValueKind.String && element.GetString() == "max") return MaxLevel(button);
    return Math.Clamp(element.GetInt32(), 0, MaxLevel(button));
  }

  private static void SetLevels(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels,
    Dictionary<string, JsonElement> wanted)
  {
    foreach (var (pattern, value) in wanted ?? new())
    {
      // "GS*" matches every node whose id starts with GS; "*" the whole tree.
      var ids = pattern.EndsWith('*')
        ? buttons.Keys.Where(id => id.StartsWith(pattern[..^1], StringComparison.Ordinal)).ToList()
        : [pattern];
      if (ids.Count == 0 || !buttons.ContainsKey(ids[0])) throw new ArgumentException($"Unknown upgrade {pattern} (see --capture-list)");
      foreach (var id in ids)
      {
        var button = buttons[id];
        int level = Level(value, button, 0);
        if (level <= 0) { levels.Remove(id); continue; }
        levels[id] = level;
        // Owning a node implies the nodes that block it, as in a real build.
        for (string parent = button.Data.BlockedBy; !string.IsNullOrEmpty(parent) && buttons.ContainsKey(parent);
             parent = buttons[parent].Data.BlockedBy)
          if (levels.GetValueOrDefault(parent) <= 0) levels[parent] = 1;
      }
    }
  }

  // Mid-shot level change through the debug path (no cost, no purchase animation).
  public static void SetLevel(string id, JsonElement? value)
  {
    var upgrades = UpgradeManager.CurrentUpgrades;
    var manager = UpgradeManager.Instance;
    var trees = new[]
    {
      (upgrades.UpgradeButtons, upgrades.UpgradeJoints), (upgrades.UpgradeButtonsAbilities, upgrades.UpgradeJointsAbilities),
      (upgrades.UpgradeButtonsMeta, upgrades.UpgradeJointsMeta)
    };
    var (buttons, joints) = trees.FirstOrDefault(tree => tree.Item1.ContainsKey(id));
    if (buttons == null) throw new ArgumentException($"Unknown upgrade {id} (see --capture-list)");
    var button = buttons[id];
    manager.SetDebugLevels(buttons, joints, new Dictionary<string, int> { [id] = Level(value, button, button.CurrentLevel) });
    ReapplyStats();
  }

  public static void ApplyStats(Dictionary<string, JsonElement> stats)
  {
    var manager = UpgradeManager.Instance;
    var upgrades = UpgradeManager.CurrentUpgrades;
    var sources = new (Dictionary<string, JsonUpgrade> definitions, Action<string, float> f, Action<string, int> i, Action<string, bool> b)[]
    {
      (upgrades.UpgradeDefinitions, manager.UG.Set, manager.UG.Set, manager.UG.Set),
      (upgrades.UpgradeDefinitionsAbilities, manager.UGA.Set, manager.UGA.Set, manager.UGA.Set),
      (upgrades.UpgradeDefinitionsMeta, manager.UGM.Set, manager.UGM.Set, manager.UGM.Set)
    };
    foreach (var (key, value) in stats ?? new())
    {
      bool found = false;
      foreach (var source in sources)
        foreach (var definition in source.definitions.Values.Where(d =>
          d.ShortName.Equals(key, StringComparison.OrdinalIgnoreCase) || d.PropertyName.Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
          found = true;
          if (definition.Type == "float") source.f(definition.ShortName, value.GetSingle());
          else if (definition.Type == "int") source.i(definition.ShortName, (int)Math.Round(value.GetDouble()));
          else source.b(definition.ShortName, value.GetBoolean());
        }
      if (!found) throw new ArgumentException($"Unknown stat {key} (see --capture-list stats)");
      appliedStats[key] = value;
    }
  }

  // Level changes recompute the stats they touch; keep the scene's overrides on top.
  public static void ReapplyStats() => ApplyStats(new Dictionary<string, JsonElement>(appliedStats));
}
#endif
