using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class PerimeterChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var grid = new GemSpatialIndex(32, 30);
    var bounds = new PlayAreaBounds(new Vector2(-500, -300), new Vector2(500, 300));
    var cache = new PerimeterGemTargets();
    var random = new Random(73);
    int center = grid.AddGem(1, 0, 0, 10000);
    int[] edges = { grid.AddGem(2, -490, 0, 1), grid.AddGem(3, 490, 0, 1),
      grid.AddGem(4, 0, -290, 1), grid.AddGem(5, 0, 290, 1) };
    grid.AddGem(6, 800, 0, 1);
    cache.Refresh(grid, bounds);
    var chosen = new HashSet<int>();
    for (int i = 0; i < 200; i++) chosen.Add(cache.Choose(grid, random));
    Check(chosen.SetEquals(edges), "All four edges must be targeted, excluding center and offscreen gems");
    foreach (int edge in edges) grid.TryClaim(edge);
    Check(cache.Choose(grid, random) == -1, "Claimed edge gems must not remain valid targets");
    cache.Refresh(grid, bounds);
    Check(cache.Choose(grid, random) == center, "An empty perimeter must fall back to the outermost remaining gem");
    grid.RecycleIndex(center);
    int reused = grid.AddGem(7, 0, 0, 1);
    Check(reused == center && cache.Choose(grid, random) == -1, "Recycled slots must not inherit cached gem identities");
    var shifted = new PlayAreaBounds(new Vector2(700, -100), new Vector2(900, 100));
    cache.Refresh(grid, shifted);
    Check(grid.Gems[cache.Choose(grid, random)].EntityId == 6, "Camera changes must rebuild edge targets in world coordinates");
    cache.Refresh(new GemSpatialIndex(0, 30), bounds);
    Check(cache.Count == 0 && cache.Choose(grid, random) == -1, "Empty fields must not produce a target");

    var previous = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var ship = new Harvester { Type = Harvester.HarvesterType.PerimeterHarvester };
      manager.UG.PerimeterHarvesterSpeed = 2;
      manager.UG.PerimeterHarvesterCapacity = 37;
      manager.UG.PerimeterHarvesterMaxFuel = 3;
      manager.UG.PerimeterFuelEfficiency = 4;
      manager.UG.PerimeterHarvesterRefuelSpeed = 5;
      Check(BaseStats.GetHarvesterSpeed(ship) == 240 && BaseStats.GetHarvesterCapacity(ship) == 37,
        "Perimeter speed and cargo upgrades must use independent stats");
      Check(BaseStats.GetHarvesterMaxFuelMultiplier(ship) == 3 && BaseStats.GetHarvesterFuelEfficiency(ship) == 4
        && BaseStats.GetHarvesterRefuelSpeedMultiplier(ship) == 5, "Perimeter fuel upgrades must apply");
      Check(BaseStats.IsFleetHarvester(ship), "Permanent fleet effects must include perimeter ships");
      manager.UGA.MagnetizerAdvancedHarvesters = true;
      Check(!manager.UGA.HasMagnetizer(ship.Type), "Advanced talents must not enable perimeter talents");
      manager.UGA.MagnetizerPerimeterHarvesters = true;
      manager.UGA.ChainMagnetizerPerimeterHarvesters = true;
      manager.UGA.GemSpawnerPerimeterHarvesters = true;
      manager.UGA.PerimeterHarvesterDrones = true;
      Check(manager.UGA.HasMagnetizer(ship.Type) && manager.UGA.HasChainMagnetizer(ship.Type)
        && manager.UGA.HasGemSpawner(ship.Type) && manager.UGA.CanDeployDrones(ship.Type),
        "Each perimeter ability extension must target the new type");
    }
    finally { UpgradeManager.Instance = previous; }
    Console.WriteLine("Perimeter checks passed: four edges, fallback, claims, recycled targets, camera bounds, independent stats and abilities.");
  }
}
