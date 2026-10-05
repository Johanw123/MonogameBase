using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class CoreShardChecks
{
  public static void Run(Upgrades upgrades)
  {
    CheckObjectives();
    CheckRunState();
    CheckSave();
    CheckTree(upgrades);
    CheckEffects(upgrades);
    Console.WriteLine("Core shard checks passed: objectives, payouts, prestige reset, save/load, tree choices and powerful upgrade effects.");
  }

  private static void CheckObjectives()
  {
    var ids = new HashSet<string>();
    foreach (var objective in CoreShards.Objectives)
    {
      Check(ids.Add(objective.Id), $"Objective id {objective.Id} must be unique");
      Check(!string.IsNullOrWhiteSpace(objective.Title) && objective.Target > 0 && objective.Reward > 0,
        $"Objective {objective.Id} needs a title, a target and a reward");
    }
    foreach (var group in CoreShards.Objectives.GroupBy(o => o.Metric))
    {
      var targets = group.Select(o => o.Target).ToList();
      Check(targets.SequenceEqual(targets.Order()), $"{group.Key} objectives must be listed in increasing order");
    }
    Check(NumberFormatter.AbbreviateBigNumber(1_000_000, true) == "1M"
      && NumberFormatter.AbbreviateBigNumber(1_000_000_000_000, true) == "1T"
      && NumberFormatter.AbbreviateBigNumber(999_999, true) == "999.99K",
      "Objective targets at exact powers of 1000 must use the larger suffix");
    Check(CoreShards.Progress(CoreShards.Objectives[0], default) == 0
      && CoreShards.Progress(CoreShards.Objectives[0], new RunObjectiveStats(double.MaxValue, 0, 0, 0, 0)) == 1,
      "Objective progress must stay within 0..1");
  }

  private static void CheckRunState()
  {
    var state = new GameState();
    Check(state.CompleteObjectives(default).Count == 0 && state.CurrentCoreShardCount == 0,
      "A fresh run must not pay any shards");
    var stats = new RunObjectiveStats(1_000_000, 0, 10, 0, 0);
    var completed = state.CompleteObjectives(stats);
    Check(completed.Select(o => o.Id).SequenceEqual(["earn_10k", "fleet_10"])
      && state.CurrentCoreShardCount == 2, "Reached objectives must pay their rewards");
    Check(state.CompleteObjectives(stats).Count == 0 && state.CurrentCoreShardCount == 2,
      "An objective must pay only once per run");
    Check(state.GetBalance(CoreShards.Currency) == 2, "Shards must be a spendable balance");
    state.Spend(CoreShards.Currency, 1);
    Check(state.CurrentCoreShardCount == 1 && state.GetBalance(CoreShards.Currency) == 1,
      "Spending shards must debit only the shard balance");
    state.CompletePrestige(1);
    Check(state.CurrentCoreShardCount == 0 && state.CompletedObjectives.Count == 0
      && state.CurrentPurpleGemCount == 1, "Prestige must reset shards and objectives like the regular tree");
    Check(state.CompleteObjectives(stats).Count == 2 && state.CurrentCoreShardCount == 2,
      "Every new run must be able to complete its objectives again");
    var full = new RunObjectiveStats(double.MaxValue, double.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
    state.CompleteObjectives(full);
    Check(state.CurrentCoreShardCount == CoreShards.TotalReward
      && state.CompletedObjectives.Count == CoreShards.Objectives.Length, "A perfect run must pay exactly the total reward");
  }

  private static void CheckSave()
  {
    string directory = Path.Combine(Path.GetTempPath(), "core-shard-save-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(new GameSave { CoreShards = 4, CompletedObjectives = new() { "earn_10k", "fleet_10" } }),
        "Shard save must write");
      var loaded = new GameSaveStore(store.SavePath).Load() ?? throw new Exception("Shard save must load");
      var state = new GameState();
      state.RestoreObjectives(loaded.CoreShards, loaded.CompletedObjectives);
      Check(state.CurrentCoreShardCount == 4 && state.CompletedObjectives.SetEquals(["earn_10k", "fleet_10"]),
        "Shards and completed objectives must round-trip");
      Check(state.CompleteObjectives(new RunObjectiveStats(10_000, 0, 10, 0, 0)).Count == 0,
        "Loading must not pay restored objectives again");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }

  private static void CheckTree(Upgrades upgrades)
  {
    var powerful = upgrades.UpgradeButtons.Values
      .Where(b => b.Data.UpgradeDefinition.Currency == CoreShards.Currency).ToList();
    Check(powerful.Count >= 6, "The regular tree must offer several powerful upgrades to choose between");
    ulong total = 0;
    foreach (var button in powerful)
    {
      var data = button.Data;
      Check(upgrades.UpgradeButtons.TryGetValue(data.BlockedBy, out var parent)
        && parent.Data.UpgradeDefinition.Currency == "red" && data.HiddenBy == data.BlockedBy && data.LockedBy == data.BlockedBy,
        $"{data.ShortName} must sit at the end of a red-gem branch");
      Check(data.NumLevels == 1 && data.LevelInfo.Count == 1 && data.LevelInfo[0].Cost == 1,
        $"{data.ShortName} must be a single choice costing one shard");
      Check(File.Exists(Path.Combine("Content", data.UpgradeDefinition.Icon)), $"{data.ShortName} icon must exist");
      total += data.LevelInfo[0].Cost;
    }
    Check(total > CoreShards.TotalReward, "Even a perfect run must not afford every powerful upgrade");
    Check(CoreShards.Objectives[0].Target <= 10_000 && CoreShards.Objectives[0].Reward >= 1,
      "An early run must earn a shard for its first powerful upgrade");
    Check(upgrades.UpgradeButtons["AR1"].Data.UpgradeDefinition.Currency == CoreShards.Currency
      && upgrades.UpgradeButtons["RH1"].Data.UpgradeDefinition.Currency == "red"
      && !upgrades.UpgradeButtonsMeta.ContainsKey("AR1") && !upgrades.UpgradeButtonsMeta.ContainsKey("RH1"),
      "Auto refuel must be a Core Shard choice and partial refuel a gem upgrade, not prestige talents");
    foreach (var other in upgrades.UpgradeButtons.Values)
      Check(!powerful.Any(p => p.Data.ShortName == other.Data.BlockedBy),
        "Regular upgrades must never require a powerful upgrade");
    Check(File.Exists(Path.Combine("Content", CoreShards.IconPath)), "The shard icon must exist");
    for (int stage = 0; stage < DebugProgressionPresets.Names.Length - 1; stage++)
      Check(!powerful.Any(p => DebugProgressionPresets.Create(stage, upgrades).Upgrades.ContainsKey(p.Data.ShortName)),
        "Presets must leave shards to the objectives instead of buying powerful upgrades with gems");
  }

  private static void CheckEffects(Upgrades upgrades)
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      var ug = new UpgradesGeneratorUpgrades { RocketPods = true, BigSpaceGun = true, MiningLaser = true };
      float cannonRate = MainShipWeapons.FireRate(ug, MainShipWeapon.Cannon);
      double rockets = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Rockets, 1, 3);
      double bigGun = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.BigSpaceGun, 1, 3);
      ug.LaserTwinBeam = true;
      Check(MainShipWeapons.LaserBeams(ug) == 2, "Twin Lasers must keep two beams");
      ug.GatlingCannon = ug.LaserQuadBeam = ug.RocketSwarm = ug.BigSpaceGunDoomsday = true;
      Check(MainShipWeapons.FireRate(ug, MainShipWeapon.Cannon) == cannonRate * MainShipWeapons.GatlingFireRateMultiplier,
        "Gatling Cannon must multiply the cannon's fire rate");
      Check(MainShipWeapons.LaserBeams(ug) == MainShipWeapons.QuadLaserBeams, "Quad Lasers must fire four beams");
      Check(MainShipWeapons.RocketsPerSalvo(ug) == Math.Max(1, ug.RocketCount) * MainShipWeapons.RocketSwarmMultiplier
        && Math.Abs(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Rockets, 1, 3) - rockets * 2) < 1e-9,
        "Rocket Swarm must double every salvo");
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.BigSpaceGun, 1, 3) - bigGun * 2) < 1e-9,
        "Doomsday Shell must double the Big Space Gun payload");

      var manager = new UpgradeManager();
      var drifter = new Harvester { Type = Harvester.HarvesterType.Harvester };
      var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
      manager.RestoreProgress(new GameSave
      {
        Upgrades = new() { ["HB"] = 1, ["CVM1"] = 1, ["MDT1"] = 1, ["HU1"] = 1, ["HDV1"] = 1, ["GLH1"] = 1,
          ["AR1"] = 1, ["RH1"] = 1 }
      });
      Check(manager.UG.MidasTouch && manager.UG.GoldenHolds && manager.UG.AutoRefuel && manager.UG.RefuelHomebase,
        "Purchased powerful and refuel upgrades must restore their effects");
      Check(Math.Abs(SignalStats.ClickValue - manager.UG.ClickValueMultiplier * manager.UGM.ClickValueMultiplier
        * CoreShards.MidasTouchValueMultiplier) < 1e-4f, "Midas Touch must multiply manual collection value");
      ulong drifterValue = (ulong)Math.Ceiling(100 * manager.UG.HarvesterDeliveryValue * CoreShards.GoldenHoldsValueMultiplier);
      Check(BaseStats.GetHarvesterDeliveryValue(drifter, 100) == drifterValue,
        "Golden Holds must double fleet deliveries on top of other bonuses");
      Check(BaseStats.GetHarvesterDeliveryValue(drone, 100) == 100, "Golden Holds must not boost drones");

      manager = new UpgradeManager();
      manager.RestoreProgress(new GameSave
      {
        Upgrades = new() { ["HB"] = 1, ["HU1"] = 1, ["HC1"] = 2, ["AC1"] = 1, ["AHU1"] = 1 }
      });
      manager.UG.MiningLaser = manager.UG.ArcHarpoon = true;
      var stats = CoreShards.Measure(manager.UG, 42, 7);
      Check(stats == new RunObjectiveStats(42, 7, 4, 2, 3),
        "Objective stats must count every fleet ship, harvester class and automatic weapon");
    }
    finally { UpgradeManager.Instance = previousManager; }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
}
