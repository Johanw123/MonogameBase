using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using Scene = ExpandedModuleChecks.Scene;

// Late-game collection: buried gems and the dig upgrades, fleet-wide boosts, the collection
// Core Shard upgrades and the prices of the new nodes.
internal static class CollectionScalingChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    BuriedGems.Reset();
    try
    {
      CheckPool();
      CheckFleetDig();
      CheckBoosts();
      CheckTree();
      Console.WriteLine("Collection scaling passed: buried gem pool, fleet digging, fleet boosts, Express Line and the new nodes' prices and gates.");
    }
    finally { BuriedGems.Reset(); }
  }

  private static void CheckPool()
  {
    using var scene = new Scene(ShipModule.None);
    Check(BuriedGems.Dig(5) == 0, "Nothing to dig from an empty pool");
    BuriedGems.OnDamage(10);
    for (int i = 0; i < 4; i++) BuriedGems.OnGemLoose();
    Check(BuriedGems.Pool == 6, "Damage that reached the field as loose gems is not buried");
    Check(BuriedGems.Dig(0) == 0 && BuriedGems.Dig(4) == 4 && BuriedGems.Dig(9) == 2 && BuriedGems.Pool == 0,
      "Digging takes what was asked for, or what is left");
    BuriedGems.OnGemLoose();
    Check(BuriedGems.Pool == 0, "An eruption's free gems cannot drive the pool negative");
    BuriedGems.OnDamage(double.MaxValue);
    Check(BuriedGems.Pool == BuriedGems.PoolLimitPerFieldGem * scene.Manager.UG.MaxGemCount, "The pool is capped");
    BuriedGems.Reset();
  }

  private static void CheckFleetDig()
  {
    using var scene = new Scene(ShipModule.GemPolisher);
    scene.Manager.UG.FleetDig = 3;
    var gems = Enumerable.Range(0, 3).Select(_ => scene.AddGem(new Vector2(500, 500))).ToArray();
    scene.Prepare();
    scene.Fleet.CollectGem(gems[0], scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 13 && scene.Ship.CarryingGemCount == 1, "With nothing buried a pickup is an ordinary pickup");
    BuriedGems.OnDamage(5);
    scene.Fleet.CollectGem(gems[1], scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 13 + 13 * 4 && scene.Ship.CarryingGemCount == 2 && BuriedGems.Pool == 2,
      "Three buried gems come up with the pickup, with its bonuses, and take no cargo space");
    scene.Fleet.CollectGem(gems[2], scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 65 + 13 * 3 && BuriedGems.Pool == 0, "A pickup digs up what is left");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
    BuriedGems.OnDamage(5);
    Check((int)typeof(UntitledGemGame.Systems.HarvesterCollectionSystem).GetMethod("DigBuriedGems",
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [drone])! == 0 && BuriedGems.Pool == 5, "Only fleet ships dig with Deep Scoops");
    BuriedGems.Reset();
  }

  private static void CheckBoosts()
  {
    using var scene = new Scene(ShipModule.None);
    var ug = scene.Manager.UG;
    float speed = BaseStats.GetHarvesterSpeed(scene.Ship);
    int capacity = BaseStats.GetHarvesterCapacity(scene.Ship);
    ug.FleetSpeedBoost = 3f;
    ug.FleetCapacityBoost = 5f;
    Check(MathF.Abs(BaseStats.GetHarvesterSpeed(scene.Ship) - speed * 3f) < 0.01f
      && BaseStats.GetHarvesterCapacity(scene.Ship) == capacity * 5, "Fleet Afterburners and Bulk Holds multiply every fleet ship");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
    float droneSpeed = BaseStats.GetHarvesterSpeed(drone);
    ug.FleetSpeedBoost = 1f;
    Check(MathF.Abs(BaseStats.GetHarvesterSpeed(drone) - droneSpeed) < 0.01f, "Drones are not fleet ships");
    Check(PrestigeTalentEffects.FleetCount(7) == 7, "An ordinary fleet fields what was bought");
    ug.TractorBeltEnabled = true;
    var plain = TractorBeltStats.From(ug, 10f);
    ug.ExpressLine = true;
    var express = TractorBeltStats.From(ug, 10f);
    Check(express.Capacity == plain.Capacity * 3 && MathF.Abs(express.Speed - plain.Speed * 3) < 0.01f,
      "Express Line triples belt speed and pod size");
  }

  private static void CheckTree()
  {
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    var upgrades = new Upgrades();
    upgrades.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades.json")),
      File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_buttons.json")),
      upgrades.UpgradeButtons, upgrades.UpgradeDefinitions);
    foreach (string id in new[] { "FDG1", "CDG1", "BDG1", "FAB1", "FBH1", "CLC3", "HCF3", "TBS2", "TBC2" })
    {
      var data = upgrades.UpgradeButtons[id].Data;
      var levels = data.LevelInfo;
      Check(levels.Count >= 5 && data.UpgradeDefinition.Currency != CoreShards.Currency, $"{id} is a deep gem-priced node");
      for (int i = 1; i < levels.Count; i++)
        Check(levels[i].Cost >= levels[i - 1].Cost * 3 && levels[i].RequiredExpandSpaceLevel >= levels[i - 1].RequiredExpandSpaceLevel,
          $"{id}: each rank costs at least three times the last and never needs less Expand Space");
      Check(levels[^1].Cost >= 10_000_000_000_000 && levels[^1].RequiredExpandSpaceLevel >= 4, $"{id} ends in the late loops");
    }
    foreach (string id in new[] { "MGN1", "TDP1", "XPL1" })
    {
      var data = upgrades.UpgradeButtons[id].Data;
      Check(data.UpgradeDefinition.Currency == CoreShards.Currency && data.LevelInfo.Count == 1 && data.LevelInfo[0].Cost == 1,
        $"{id} costs one Core Shard");
    }
    var positions = upgrades.UpgradeButtons.Values.Where(b => b.Data.PosX > 0)
      .GroupBy(b => (b.Data.PosX, b.Data.PosY)).Where(g => g.Count() > 1 && g.Any(b => b.Data.ShortName is
        "FDG1" or "CDG1" or "BDG1" or "FAB1" or "FBH1" or "CLC3" or "HCF3" or "TBS2" or "TBC2" or "MGN1" or "TDP1" or "XPL1"));
    Check(!positions.Any(), "No new node sits on top of another");
  }
}
