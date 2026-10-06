using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class GemQualityChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    var oldManager = UpgradeManager.Instance;
    var oldTree = UpgradeManager.CurrentUpgrades;
    try
    {
      var tree = new Upgrades();
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades.json"),
        File.ReadAllText("Content/Data/upgrades_buttons.json"), tree.UpgradeButtons, tree.UpgradeDefinitions);
      UpgradeManager.CurrentUpgrades = tree;
      var buttons = tree.UpgradeButtons;
      foreach (var row in GemQualityTable.Levels)
      {
        Check(Math.Abs(row.Sum(e => e.ChancePercent) - 100) < .001,
          "Every drop table must total 100 percent");
        Check(row.All(e => e.ChancePercent > 0) && row.Select(e => e.Type).Distinct().Count() == row.Length,
          "Drop table entries must have positive probabilities and unique colors");
      }

      // Colors unlock from a hit's fire power, in value order.
      var thresholds = GemQualityTable.ColorFirePower;
      uint Value(GemTypes type) => GemQualityTable.Levels.SelectMany(row => row).First(e => e.Type == type).ValueMultiplier;
      for (int i = 1; i < thresholds.Length; i++)
        Check(thresholds[i].FirePower > thresholds[i - 1].FirePower
          && Value(thresholds[i].Type) > Value(thresholds[i - 1].Type),
          "Color fire power thresholds must rise with gem value");
      Check(GemQualityTable.ExpectedValueMultiplier(1) == 1, "The weakest hits knock loose only red gems");
      foreach (var (color, required) in thresholds)
      {
        Check(GemQualityTable.Levels[GemQualityTable.RowFor(required)].Any(e => e.Type == color),
          $"{color} must be able to drop as soon as fire power reaches {required}");
        Check(GemQualityTable.ExpectedValueMultiplier(required) > GemQualityTable.ExpectedValueMultiplier(required - 1),
          $"Reaching {color}'s fire power must improve drops");
      }
      for (int power = 2; power <= 80; power++)
        Check(GemQualityTable.ExpectedValueMultiplier(power) >= GemQualityTable.ExpectedValueMultiplier(power - 1),
          "More fire power must never make drops worse");
      Check(GemQualityTable.RowFor(1000) == GemQualityTable.Levels.Length - 1, "Fire power stops at the last row");

      // Some weapon's Fire Power in the tree (before signals) reaches every color.
      var ug = new UpgradesGeneratorUpgrades();
      int maxPower = MainShipWeapons.All.Max(weapon =>
      {
        string node = weapon switch
        {
          MainShipWeapon.Cannon => "CFP",
          MainShipWeapon.Laser => "LZP",
          MainShipWeapon.Harpoon => "AHP",
          MainShipWeapon.Rockets => "RPP",
          _ => "RGP",
        };
        return MainShipWeapons.FirePower(ug, weapon) + buttons.Values
          .Where(b => b.Data.UpgradeDefinition.ShortName == node)
          .Sum(b => b.Data.LevelInfo.Sum(level => level.m_upgradeAmountInt));
      });
      Check(maxPower >= thresholds[^1].FirePower, "Some weapon's fire power must unlock every color");
    }
    finally
    {
      UpgradeManager.Instance = oldManager;
      UpgradeManager.CurrentUpgrades = oldTree;
    }
    Console.WriteLine("Gem quality checks passed: fire power colors, drop tables and reachable colors.");
  }
}
