using System.Text.Json;
using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class FleetDeliveryValueChecks
{
  public static void Run()
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var types = new[]
      {
        (Harvester.HarvesterType.Harvester, "HDV", "HU1"),
        (Harvester.HarvesterType.AdvancedHarvester, "AHDV", "AHU1"),
        (Harvester.HarvesterType.PerimeterHarvester, "PHDV", "PHU1"),
        (Harvester.HarvesterType.ExpertHarvester, "EHDV", "EHU1"),
        (Harvester.HarvesterType.UltimateHarvester, "UHDV", "UHU1"),
      };
      using var definitions = JsonDocument.Parse(File.ReadAllText("Content/Data/upgrades.json"));
      using var layout = JsonDocument.Parse(File.ReadAllText("Content/Data/upgrades_buttons.json"));
      foreach (var (type, id, root) in types)
      {
        var ship = new Harvester { Type = type };
        Check(BaseStats.GetHarvesterDeliveryValue(ship, 100) == 100, "Base delivery value must stay unchanged");
        manager.UG.Increment(id, 0.25f);
        foreach (var (otherType, _, _) in types)
          Check(BaseStats.GetHarvesterDeliveryValue(new Harvester { Type = otherType }, 100)
            == (otherType == type ? 125UL : 100UL), "Delivery upgrades must affect only their ship type");
        Check(BaseStats.GetHarvesterDeliveryValue(new Harvester { Type = Harvester.HarvesterType.Drone }, 100) == 100,
          "Fleet delivery upgrades must not boost drones");
        manager.UGM.AllHarvesterValueMultiplier = 2f;
        Check(BaseStats.GetHarvesterDeliveryValue(ship, 100) == 250, "Type bonuses must multiply prestige value");
        Check(BaseStats.GetHarvesterDeliveryValue(ship, ulong.MaxValue) == ulong.MaxValue,
          "Delivery bonuses must saturate instead of overflowing");
        manager.UGM.AllHarvesterValueMultiplier = 1f;
        manager.UG.Reset(id);
        Check(BaseStats.GetHarvesterDeliveryValue(ship, 100) == 100, "Reset must restore base delivery value");
        var definition = definitions.RootElement.GetProperty("upgrades").EnumerateArray()
          .Single(d => d.GetProperty("shortname").GetString() == id);
        Check(UpgradeValueFormatter.Format(new JsonUpgrade { ShortName = id, BaseValue = definition.GetProperty("base").GetString()! }, 1.25, true) == "+25%",
          "Value tooltips must display the bonus above base value");
        var buttons = layout.RootElement.GetProperty("buttons").EnumerateArray()
          .ToDictionary(b => b.GetProperty("shortname").GetString()!);
        var node = buttons.Values.Single(b => b.GetProperty("upgrade").GetString() == id);
        // The node sits in its own class's branch: its prerequisites lead back to the unlock.
        var ancestors = new List<string>();
        for (string? parent = node.GetProperty("blockedby").GetString(); !string.IsNullOrEmpty(parent)
          && buttons.TryGetValue(parent, out var ancestor); parent = ancestor.GetProperty("blockedby").GetString())
          ancestors.Add(parent);
        Check(ancestors.Contains(root)
          && node.GetProperty("hiddenby").GetString() == node.GetProperty("blockedby").GetString()
          && node.GetProperty("cost").GetArrayLength() == 5
          && node.GetProperty("value").GetArrayLength() == 5,
          "Each delivery upgrade must have five ranks within its ship's branch");
      }
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Fleet delivery value checks passed: type isolation, stacking, reset, saturation and upgrade nodes.");
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
}
