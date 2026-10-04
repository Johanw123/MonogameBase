#if !KNI_WEB
using System;
using System.Linq;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Capture;

// What a scene can refer to, read from the running game so it never goes stale.
internal static class Catalog
{
  public static CaptureCatalog Build()
  {
    var upgrades = UpgradeManager.CurrentUpgrades;
    var catalog = new CaptureCatalog();
    for (int i = 0; i < DebugProgressionPresets.Names.Length; i++)
      catalog.Presets.Add(new CatalogEntry { Id = i.ToString(), Name = DebugProgressionPresets.Names[i],
        Description = DebugProgressionPresets.Descriptions[i] });
    foreach (var name in DebugProgressionPresets.FeatureNames)
      catalog.FeaturePresets.Add(new CatalogEntry { Id = Actions.Slug(name), Name = name });
    foreach (var (tree, buttons) in new[] { ("upgrades", upgrades.UpgradeButtons),
      ("abilities", upgrades.UpgradeButtonsAbilities), ("meta", upgrades.UpgradeButtonsMeta) })
      foreach (var (id, button) in buttons.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        catalog.Upgrades.Add(new CatalogUpgrade { Id = id, Tree = tree, Name = button.Data.UpgradeDefinition.Name,
          Stat = button.Data.UpgradeDefinition.ShortName, Levels = Math.Min(button.Data.NumLevels, button.Data.LevelInfo.Count),
          BlockedBy = string.IsNullOrEmpty(button.Data.BlockedBy) ? null : button.Data.BlockedBy });
    foreach (var (source, definitions) in new[] { ("fleet and world", upgrades.UpgradeDefinitions),
      ("abilities", upgrades.UpgradeDefinitionsAbilities), ("permanent", upgrades.UpgradeDefinitionsMeta) })
      foreach (var definition in definitions.Values.OrderBy(d => d.PropertyName, StringComparer.Ordinal))
        catalog.Stats.Add(new CatalogStat { Id = definition.ShortName, Property = definition.PropertyName,
          Name = definition.Name, Type = definition.Type, Base = definition.BaseValue, Source = source });
    string[] abilityNames = ["Genesis pulse (gem spawner)", "Ion surge (speed)", "Tractor field (magnet)", "Drones",
      "Graviton cascade (chain)"];
    for (int i = 0; i < Actions.AbilityIds.Length; i++)
      if (upgrades.UpgradeButtonsAbilities.ContainsKey(Actions.AbilityIds[i])) // equippable only with a tree node
        catalog.Abilities.Add(new CatalogEntry { Id = Actions.AbilityIds[i], Name = abilityNames[i] });
    foreach (var definition in ManualFleetAbilities.Definitions)
      catalog.ManualAbilities.Add(new CatalogEntry { Id = Actions.Slug(definition.Name), Name = definition.Name,
        Description = $"{definition.Effect}; unlocks at {definition.UnlockEarnings} gems earned this run" });
    catalog.Modules.AddRange(ModuleCatalog.Names.Skip(1));
    catalog.Signals.AddRange(SignalCatalog.Definitions.Select(d => d.Name));
    catalog.SignalRarities.AddRange(Staging.Rarities);
    catalog.Panels.AddRange(Enum.GetNames<RenderGuiSystem.UpgradeTypes>().Select(n => n.ToLowerInvariant()));
    catalog.Sounds.AddRange(AudioManager.LoadedSoundNames);
    return catalog;
  }
}
#endif
