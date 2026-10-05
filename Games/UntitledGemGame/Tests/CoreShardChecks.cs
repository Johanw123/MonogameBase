using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class CoreShardChecks
{
  public static void Run(Upgrades upgrades)
  {
    CheckFractureRules();
    CheckDamageTracker();
    CheckRunState();
    CheckSave();
    CheckTree(upgrades);
    CheckEffects(upgrades);
    Console.WriteLine("Core shard checks passed: fracture thresholds, damage window, eruption size, prestige reset, save/load, tree choices and powerful upgrade effects.");
  }

  private static void CheckFractureRules()
  {
    Check(CoreFracture.Threshold(0) == CoreFracture.FirstThreshold
      && Enumerable.Range(0, 12).All(n => CoreFracture.Threshold(n + 1) == CoreFracture.Threshold(n) * CoreFracture.ThresholdGrowth),
      "Each fracture must multiply the damage the next one needs");
    Check(CoreFracture.Threshold(-3) == CoreFracture.FirstThreshold, "A negative count must not lower the first threshold");
    Check(CoreFracture.EruptionGems(100, 0) == 100 * CoreFracture.EruptionMultiplier
      && CoreFracture.EruptionGems(0, 6_000) == (long)(6_000 / 60.0 * CoreFracture.EruptionMinimumSeconds),
      "The eruption must return twice the swallowed gems, or a few seconds of the damage that caused it");
    Check(NumberFormatter.AbbreviateBigNumber(1_000_000, true) == "1M"
      && NumberFormatter.AbbreviateBigNumber(1_000_000_000_000, true) == "1T"
      && NumberFormatter.AbbreviateBigNumber(999_999, true) == "999.99K",
      "Thresholds at exact powers of 1000 must use the larger suffix");
  }

  private static void CheckDamageTracker()
  {
    var damage = new PlanetDamageTracker();
    damage.Record(100);
    damage.Update(0.5f);
    damage.Record(50);
    Check(damage.PerMinute == 150, "Damage must count as soon as it is dealt");
    for (int second = 0; second < 60; second++) damage.Update(1f);
    Check(damage.PerMinute == 150, "Damage must stay in the window for a minute");
    damage.Update(1f);
    Check(damage.PerMinute == 0, "Damage older than a minute must leave the window");
    damage.Record(double.NaN);
    damage.Record(-5);
    damage.Record(double.PositiveInfinity);
    damage.Update(float.NaN);
    damage.Update(-1f);
    Check(damage.PerMinute == 0, "Invalid damage or time must be ignored");
    damage.Record(1_000);
    damage.Update(10_000f);
    Check(damage.PerMinute == 0, "A very long frame must empty the window");
    for (int second = 0; second < 120; second++)
    {
      damage.Record(10);
      damage.Update(1f);
    }
    Check(damage.PerMinute == 600, "Steady damage must settle at a minute's worth");
    damage.Reset();
    Check(damage.PerMinute == 0, "Reset must clear the window");
  }

  private static void CheckRunState()
  {
    var state = new GameState();
    Check(state.CoreFractures == 0 && state.CurrentCoreShardCount == 0, "A fresh run has no fractures or shards");
    state.CoreFractures = 3;
    state.CurrentCoreShardCount = 2;
    Check(state.GetBalance(CoreShards.Currency) == 2, "Shards must be a spendable balance");
    state.Spend(CoreShards.Currency, 1);
    Check(state.CurrentCoreShardCount == 1 && state.GetBalance(CoreShards.Currency) == 1,
      "Spending shards must debit only the shard balance");
    state.CompletePrestige(1);
    Check(state.CurrentCoreShardCount == 0 && state.CoreFractures == 0 && state.CurrentPurpleGemCount == 1,
      "Prestige must reset shards and fractures like the regular tree");
  }

  private static void CheckSave()
  {
    string directory = Path.Combine(Path.GetTempPath(), "core-shard-save-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(new GameSave { CoreShards = 4, CoreFractures = 3 }), "Shard save must write");
      var loaded = new GameSaveStore(store.SavePath).Load() ?? throw new Exception("Shard save must load");
      var state = new GameState();
      state.RestoreCoreShards(loaded.CoreShards, loaded.CoreFractures);
      Check(state.CurrentCoreShardCount == 4 && state.CoreFractures == 3,
        "Shards and fractures must round-trip, so reloading never pays a threshold twice");
      state.RestoreCoreShards(1, -2);
      Check(state.CoreFractures == 0, "A corrupt fracture count must not lower the next threshold");
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
    // Measured live (Simulation/BALANCE.md): mid game deals 90K-155K damage per minute,
    // a strong late run passes 1M, the uber endgame about 7.6M.
    static int Fractures(double damagePerMinute)
    {
      int fractures = 0;
      while (damagePerMinute >= CoreFracture.Threshold(fractures)) fractures++;
      return fractures;
    }
    Check(Fractures(150_000) == 4 && Fractures(1_100_000) == 6 && (ulong)Fractures(7_600_000) < total,
      "Mid game must earn four shards, a strong run six, and even the endgame must not afford every powerful upgrade");
    Check(CoreFracture.FirstThreshold <= 2_000, "An early run must reach its first fracture for its first powerful upgrade");
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
        "Presets must leave shards to core fractures instead of buying powerful upgrades with gems");
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

    }
    finally { UpgradeManager.Instance = previousManager; }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
}
