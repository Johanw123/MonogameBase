using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImGuiNET;

namespace UntitledGemGame;

// Use the generated accessors rather than reflection so this also works in AOT builds.
public sealed class DebugStatEditor
{
  private string filter = "";

  public void Draw(UpgradeManager manager)
  {
    if (!ImGui.CollapsingHeader("Live stat sliders")) return;
    ImGui.TextWrapped("Changes apply immediately for this session. Values are raw upgrade stats; many speed, range and cooldown values are multipliers. Ctrl-click a slider to type a value.");
    ImGui.InputText("Find stat", ref filter, 128);
    var upgrades = UpgradeManager.CurrentUpgrades;
    Source[] sources =
    [
      new("Fleet and world", upgrades.UpgradeDefinitions,
        id => { manager.UG.GetFloat(id, out var value); return value; },
        id => { manager.UG.GetInt(id, out var value); return value; },
        id => { manager.UG.GetBool(id, out var value); return value; },
        manager.UG.Set, manager.UG.Set, manager.UG.Set),
      new("Abilities", upgrades.UpgradeDefinitionsAbilities,
        id => { manager.UGA.GetFloat(id, out var value); return value; },
        id => { manager.UGA.GetInt(id, out var value); return value; },
        id => { manager.UGA.GetBool(id, out var value); return value; },
        manager.UGA.Set, manager.UGA.Set, manager.UGA.Set),
      new("Permanent bonuses", upgrades.UpgradeDefinitionsMeta,
        id => { manager.UGM.GetFloat(id, out var value); return value; },
        id => { manager.UGM.GetInt(id, out var value); return value; },
        id => { manager.UGM.GetBool(id, out var value); return value; },
        manager.UGM.Set, manager.UGM.Set, manager.UGM.Set)
    ];
    // Favorites reuse the same controls and backing values as the full stat list.
    ImGui.SetNextItemOpen(true, ImGuiCond.Once);
    if (ImGui.TreeNode("Favorites"))
    {
      ImGui.PushID("Favorites");
      foreach (var stat in sources[0].Definitions.Values.Where(d => FavoriteProperties.Contains(d.PropertyName)))
        DrawStat(sources[0], stat);
      ImGui.PopID();
      ImGui.TreePop();
    }
    foreach (var group in sources.SelectMany(source => source.Definitions.Values.Select(stat => (source, stat)))
      .Where(item => item.stat.ShortName is not ("P" or "AP" or "ResetAbilities" or "HB"))
      .Where(item => string.IsNullOrEmpty(filter) || Label(item.stat).Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.stat.PropertyName.Contains(filter, StringComparison.OrdinalIgnoreCase))
      .GroupBy(item => Group(item.stat)).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
      if (!ImGui.TreeNode(group.Key)) continue;
      foreach (var (source, stat) in group.OrderBy(item => Label(item.stat), StringComparer.Ordinal).ThenBy(item => item.source.Name))
        DrawStat(source, stat);
      ImGui.TreePop();
    }
  }

  private static readonly HashSet<string> FavoriteProperties = ["MaxGemCount"];

  private sealed record Source(string Name, Dictionary<string, JsonUpgrade> Definitions,
    Func<string, float> GetFloat, Func<string, int> GetInt, Func<string, bool> GetBool,
    Action<string, float> SetFloat, Action<string, int> SetInt, Action<string, bool> SetBool);

  private static void DrawStat(Source source, JsonUpgrade stat)
  {
    ImGui.PushID(source.Name);
    ImGui.PushID(stat.ShortName);
    string label = Label(stat) + (source.Name == "Permanent bonuses" ? " (permanent)" : "");
    if (stat.Type == "float")
    {
      float value = source.GetFloat(stat.ShortName);
      float baseline = float.Parse(stat.BaseValue.TrimEnd('f', 'F'), CultureInfo.InvariantCulture);
      bool chance = stat.PropertyName.Contains("Chance", StringComparison.Ordinal);
      float minimum = stat.PropertyName.Contains("Cooldown", StringComparison.Ordinal)
        || stat.PropertyName.Contains("Interval", StringComparison.Ordinal)
        || stat.PropertyName.Contains("Efficiency", StringComparison.Ordinal)
        || stat.PropertyName == "CameraZoomScale" ? 0.01f : 0;
      float maximum = chance ? 1 : Math.Max(10, Math.Abs(baseline) * 20);
      if (ImGui.SliderFloat(label, ref value, minimum, maximum, "%.3f") && float.IsFinite(value))
        source.SetFloat(stat.ShortName, Math.Clamp(value, minimum, chance ? 1 : float.MaxValue));
    }
    else if (stat.Type == "int")
    {
      int value = source.GetInt(stat.ShortName);
      int maximum = stat.PropertyName switch
      {
        "MaxGemCount" => 500_000,
        "ModuleSlots" => ModuleCatalog.MaxSlotsPerType,
        "AbilitySlot" => 5,
        _ when stat.PropertyName.Contains("HarvesterCount", StringComparison.Ordinal) => 100,
        _ => 5_000
      };
      int minimum = stat.PropertyName == "ModuleSlots" ? ModuleCatalog.BaseSlotsPerType : 0;
      if (ImGui.SliderInt(label, ref value, minimum, maximum))
        source.SetInt(stat.ShortName, Math.Clamp(value, minimum, maximum));
    }
    else if (stat.Type == "bool")
    {
      bool value = source.GetBool(stat.ShortName);
      if (ImGui.Checkbox(label, ref value)) source.SetBool(stat.ShortName, value);
    }
    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{stat.PropertyName}\n{stat.Tooltip}");
    ImGui.PopID();
    ImGui.PopID();
  }

  private static string Label(JsonUpgrade stat) => stat.Name
    .Replace("Advanced Harvester", "Seeker").Replace("Expert Harvester", "Prospector")
    .Replace("Ultimate Harvester", "Trove hunter").Replace("Perimeter Harvester", "Rimrunner")
    .Replace("Harvester", "Drifter");

  private static string Group(JsonUpgrade stat) => stat.PropertyName switch
  {
    var p when p.StartsWith("Click", StringComparison.Ordinal) || p.StartsWith("HoldClick", StringComparison.Ordinal)
      || p.StartsWith("CursorGravity", StringComparison.Ordinal) => "Clicking and cursor gravity",
    var p when p.StartsWith("AdvancedHarvester", StringComparison.Ordinal) || p == "AdvancedFuelEfficiency"
      || p is "TreasureScanner" or "QuantumCargoHold" => "Fleet: Seekers",
    var p when p.StartsWith("ExpertHarvester", StringComparison.Ordinal) || p == "ExpertFuelEfficiency"
      || p == "ChainCollection" => "Fleet: Prospectors",
    var p when p.StartsWith("UltimateHarvester", StringComparison.Ordinal) || p == "UltimateFuelEfficiency"
      || p is "WarpDrive" or "ReturnGate" => "Fleet: Trove hunters",
    var p when p.StartsWith("Perimeter", StringComparison.Ordinal) => "Fleet: Rimrunners",
    var p when p.StartsWith("Harvester", StringComparison.Ordinal) || p is "FuelEfficiency" or "LaunchThrusters" => "Fleet: Drifters",
    var p when p.StartsWith("AllHarvester", StringComparison.Ordinal)
      || p is "JackpotHaul" or "ResonanceCascade" or "QuantumEntanglement" => "Fleet: shared bonuses",
    var p when p.Contains("Refuel", StringComparison.Ordinal) || p.StartsWith("HomebaseCollection", StringComparison.Ordinal)
      || p == "HomeBaseCollector" => "Homebase and refueling",
    var p when p.Contains("Drone", StringComparison.Ordinal) => "Abilities: drones",
    var p when p.StartsWith("Magnetizer", StringComparison.Ordinal) || p.StartsWith("HomebaseMagnetizer", StringComparison.Ordinal) => "Abilities: tractor field",
    var p when p.StartsWith("Chain", StringComparison.Ordinal) || p.StartsWith("Constellation", StringComparison.Ordinal) => "Abilities: graviton cascade",
    var p when p.StartsWith("GemSpawner", StringComparison.Ordinal) => "Abilities: genesis pulse",
    "Speedboost" => "Abilities: ion surge",
    var p when p.Contains("Ability", StringComparison.Ordinal) || p.StartsWith("Multicast", StringComparison.Ordinal)
      || p == "CommandAmplifier" => "Abilities: shared bonuses",
    var p when p.StartsWith("Cluster", StringComparison.Ordinal) || p is "CosmicClusters" or "Supercluster" or "MonochromeVein" => "Gem events: clusters",
    var p when p.StartsWith("Lucky", StringComparison.Ordinal) || p == "Motherlode" => "Gem events: lucky gems",
    var p when p.StartsWith("GemShower", StringComparison.Ordinal) => "Gem events: showers",
    var p when p.StartsWith("GemComet", StringComparison.Ordinal) => "Gem events: comets",
    var p when p.StartsWith("PassiveIncome", StringComparison.Ordinal) || p.StartsWith("GemValue", StringComparison.Ordinal)
      || p.StartsWith("GemMerger", StringComparison.Ordinal) => "Economy and gem value",
    var p when p.StartsWith("GemSpawn", StringComparison.Ordinal) || p.EndsWith("GemUnlocked", StringComparison.Ordinal)
      || p == "MaxGemCount" => "Gem spawning and capacity",
    "ShipyardUnlocked" or "ModuleSlots" or "SignalsUnlocked" => "Shipyard and signals",
    "CameraZoomScale" => "Camera and space",
    _ => "Other stats and features"
  };
}
