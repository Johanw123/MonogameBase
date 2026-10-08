using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class CoreShardChecks
{
  public static void Run(Upgrades upgrades)
  {
    CheckFractureRules();
    CheckShellRules();
    CheckDamageTracker();
    CheckDamageMeter();
    CheckRunState();
    CheckSave();
    CheckTree(upgrades);
    CheckEffects(upgrades);
    Console.WriteLine("Core shard checks passed: fracture thresholds, planet shell, damage window, damage by source, eruption size, prestige reset, save/load, tree choices and powerful upgrade effects.");
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
    Check(CoreFracture.PlanetSize(0) == 1f && CoreFracture.PlanetSize(-4) == 1f
      && Enumerable.Range(0, CoreFracture.MaxGrowthFractures).All(n => CoreFracture.PlanetSize(n + 1) > CoreFracture.PlanetSize(n))
      && CoreFracture.PlanetSize(CoreFracture.MaxGrowthFractures + 20) == CoreFracture.PlanetSize(CoreFracture.MaxGrowthFractures)
      && CoreFracture.PlanetSize(CoreFracture.MaxGrowthFractures) < 1.7f,
      "The planet swells with every fracture, up to a limit that keeps it clear of the homebase");
    Check(NumberFormatter.AbbreviateBigNumber(1_000_000, true) == "1M"
      && NumberFormatter.AbbreviateBigNumber(1_000_000_000_000, true) == "1T"
      && NumberFormatter.AbbreviateBigNumber(999_999, true) == "999.99K",
      "Thresholds at exact powers of 1000 must use the larger suffix");
  }

  private static void CheckShellRules()
  {
    Check(PlanetShell.Health == CoreFracture.FirstThreshold / CoreFracture.ThresholdGrowth,
      "The shell must hold one step below the first fracture's damage");
    Check(!PlanetShell.Broken(PlanetShell.Health - 1) && PlanetShell.Broken(PlanetShell.Health),
      "The shell must break exactly when it has taken its health");
    Check(PlanetShell.Wear(0) == 0f && PlanetShell.Wear(PlanetShell.Health / 2) == 0.5f
      && PlanetShell.Wear(PlanetShell.Health * 3) == 1f && PlanetShell.Wear(double.NaN) == 0f && PlanetShell.Wear(-5) == 0f,
      "Shell wear must run from 0 untouched to 1 broken, ignoring invalid damage");

    var layout = PlanetShell.CreateLayout(1009);
    Check(layout.Plates.Length == PlanetShell.PlateCount && layout.CrackOrigins.Length == PlanetShell.CrackOriginCount
      && layout.Plates.All(p => Math.Abs(p.Length() - 1f) < 1e-4f),
      "The shell layout must match the shader's plate and crack counts, on the unit sphere");
    var owned = new HashSet<int>();
    float latest = float.MinValue;
    for (int i = 0; i < 4000; i++)
    {
      // Evenly over the sphere.
      float y = 1f - 2f * (i + 0.5f) / 4000f, ring = MathF.Sqrt(1f - y * y), angle = i * 2.39996f;
      var point = new Microsoft.Xna.Framework.Vector3(MathF.Cos(angle) * ring, y, MathF.Sin(angle) * ring);
      owned.Add(PlanetShell.PlateAt(layout, point));
      latest = Math.Max(latest, PlanetShell.CrackWear(layout, point));
    }
    Check(owned.Count == PlanetShell.PlateCount, "Every plate must own part of the shell, so each can fly off as a fragment");
    Check(latest < PlanetShell.FullWear, "Every seam must have cracked open by the time the shell bursts");
    var first = layout.CrackOrigins[0];
    Check(PlanetShell.CrackWear(layout, new Microsoft.Xna.Framework.Vector3(first.X, first.Y, first.Z)) < 0f,
      "The first crack must open with the first hits, so the player sees the shell give");
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

  private static void CheckDamageMeter()
  {
    var meter = new PlanetDamageMeter();
    var names = Enum.GetValues<PlanetDamageSource>().Select(PlanetDamageMeter.Name).ToList();
    Check(names.Count == PlanetDamageMeter.SourceCount && names.All(n => !string.IsNullOrWhiteSpace(n))
      && names.Distinct().Count() == names.Count, "Every damage source must have its own display name");
    meter.Record(PlanetDamageSource.AutoCannon, 100);
    meter.Record(PlanetDamageSource.MiningLaser, 40);
    meter.Record(PlanetDamageSource.AutoCannon, 20);
    Check(meter.PerMinute(PlanetDamageSource.AutoCannon) == 120 && meter.ThisRun(PlanetDamageSource.AutoCannon) == 120
      && meter.TotalPerMinute == 160 && meter.TotalThisRun == 160 && meter.ThisRun(PlanetDamageSource.Railgun) == 0,
      "Damage must count toward its own source, in the minute and the run");
    meter.Record(PlanetDamageSource.Railgun, double.NaN);
    meter.Record(PlanetDamageSource.Railgun, -5);
    meter.Record(PlanetDamageSource.Railgun, double.PositiveInfinity);
    meter.Record((PlanetDamageSource)999, 50);
    Check(meter.TotalThisRun == 160, "Invalid damage or an unknown source must be ignored");
    for (int second = 0; second < 61; second++) meter.Update(1f);
    Check(meter.TotalPerMinute == 0 && meter.TotalThisRun == 160,
      "Damage must leave the minute window but stay in the run total");

    var totals = meter.RunTotals();
    Check(totals.Count == 2 && totals["AutoCannon"] == 120 && totals["MiningLaser"] == 40,
      "Run totals must save by source name, leaving out sources that dealt nothing");
    var restored = new PlanetDamageMeter();
    restored.RestoreRunTotals(new Dictionary<string, double>(totals)
      { ["Unknown"] = 5, ["3"] = 7, ["Railgun"] = double.NaN, ["CoreDrill"] = -1 });
    Check(restored.ThisRun(PlanetDamageSource.AutoCannon) == 120 && restored.ThisRun(PlanetDamageSource.MiningLaser) == 40
      && restored.TotalThisRun == 160 && restored.TotalPerMinute == 0,
      "Restoring must bring back each source's run total and skip unknown or corrupt entries");
    restored.RestoreRunTotals(null);
    Check(restored.TotalThisRun == 0, "A save without damage totals must start the run at zero");

    var state = new GameState();
    state.Damage.Record(PlanetDamageSource.Railgun, 500);
    state.CompletePrestige(1);
    Check(state.Damage.TotalThisRun == 0 && state.Damage.TotalPerMinute == 0, "Extraction must clear the damage by source");
  }

  private static void CheckRunState()
  {
    var state = new GameState();
    Check(state.CoreFractures == 0 && state.CurrentCoreShardCount == 0, "A fresh run has no fractures or shards");
    Check(state.ShellDamage == 0 && !state.ShellBroken, "A fresh run's planet must have its shell");
    state.ShellDamage = PlanetShell.Health;
    state.CoreFractures = 3;
    state.CurrentCoreShardCount = 2;
    Check(state.GetBalance(CoreShards.Currency) == 2, "Shards must be a spendable balance");
    state.Spend(CoreShards.Currency, 1);
    Check(state.CurrentCoreShardCount == 1 && state.GetBalance(CoreShards.Currency) == 1,
      "Spending shards must debit only the shard balance");
    state.CompletePrestige(1);
    Check(state.CurrentCoreShardCount == 0 && state.CoreFractures == 0 && state.CurrentPurpleGemCount == 1,
      "Prestige must reset shards and fractures like the regular tree");
    Check(state.ShellDamage == 0 && !state.ShellBroken, "Prestige must give the next run's planet its shell back");
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
      state.RestoreCoreShards(loaded.CoreShards, loaded.CoreFractures, loaded.ShellDamage);
      Check(state.CurrentCoreShardCount == 4 && state.CoreFractures == 3,
        "Shards and fractures must round-trip, so reloading never pays a threshold twice");
      Check(state.ShellBroken, "A fractured planet must have lost its shell");
      state.RestoreCoreShards(1, -2, 0);
      Check(state.CoreFractures == 0, "A corrupt fracture count must not lower the next threshold");

      Check(store.Save(new GameSave { DamageThisRun = new() { ["ArcHarpoon"] = 1234 } }), "Damage save must write");
      loaded = new GameSaveStore(store.SavePath).Load() ?? throw new Exception("Damage save must load");
      state.Damage.RestoreRunTotals(loaded.DamageThisRun);
      Check(state.Damage.ThisRun(PlanetDamageSource.ArcHarpoon) == 1234, "Damage by source must round-trip through the save");

      Check(store.Save(new GameSave { ShellDamage = 120 }), "Shell save must write");
      loaded = new GameSaveStore(store.SavePath).Load() ?? throw new Exception("Shell save must load");
      state.RestoreCoreShards(loaded.CoreShards, loaded.CoreFractures, loaded.ShellDamage);
      Check(state.ShellDamage == 120 && !state.ShellBroken, "A cracked shell must reload as cracked as it was");
      state.RestoreCoreShards(0, 0, double.NaN);
      Check(state.ShellDamage == 0, "Corrupt shell damage must leave the shell whole");
      state.RestoreCoreShards(0, 0, PlanetShell.Health * 9);
      Check(state.ShellDamage == PlanetShell.Health && state.ShellBroken, "Shell damage must not exceed its health");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }

  private static void CheckTree(Upgrades upgrades)
  {
    // Lookups such as ShipSystems.IsInTree go by id alone, so ids must not repeat across trees.
    var ids = upgrades.UpgradeButtons.Keys.Concat(upgrades.UpgradeButtonsAbilities.Keys)
      .Concat(upgrades.UpgradeButtonsMeta.Keys).ToList();
    var definitions = upgrades.UpgradeDefinitions.Keys.Concat(upgrades.UpgradeDefinitionsAbilities.Keys)
      .Concat(upgrades.UpgradeDefinitionsMeta.Keys).ToList();
    Check(ids.Count == ids.Distinct().Count() && definitions.Count == definitions.Distinct().Count(),
      "Upgrade ids must be unique across the run, ship system and prestige trees");
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
      var ug = new UpgradesGeneratorUpgrades { RocketPods = true, Railgun = true, MiningLaser = true, ArcHarpoon = true };
      float cannonRate = MainShipWeapons.FireRate(ug, MainShipWeapon.Cannon);
      double rockets = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Rockets, 1, 3);
      double railgun = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Railgun, 1, 3);
      double harpoon = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Harpoon, 1, 3);
      Check(MainShipWeapons.HarpoonCount(ug) == 1, "The Arc Harpoon must fire one harpoon on its own");
      ug.LaserTwinBeam = true;
      Check(MainShipWeapons.LaserBeams(ug) == 2, "Twin Lasers must keep two beams");
      ug.GatlingCannon = ug.LaserQuadBeam = ug.RocketSwarm = ug.RailgunDoomsday = ug.HarpoonTwin = true;
      Check(MainShipWeapons.FireRate(ug, MainShipWeapon.Cannon) == cannonRate * MainShipWeapons.GatlingFireRateMultiplier,
        "Gatling Cannon must multiply the cannon's fire rate");
      Check(MainShipWeapons.LaserBeams(ug) == MainShipWeapons.QuadLaserBeams, "Quad Lasers must fire four beams");
      Check(MainShipWeapons.RocketsPerSalvo(ug) == Math.Max(1, ug.RocketCount) * MainShipWeapons.RocketSwarmMultiplier
        && Math.Abs(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Rockets, 1, 3) - rockets * 2) < 1e-9,
        "Rocket Swarm must double every salvo");
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Railgun, 1, 3) - railgun * 2) < 1e-9,
        "Doomsday Round must double the Railgun payload");
      Check(MainShipWeapons.HarpoonCount(ug) == MainShipWeapons.TwinHarpoons
        && Math.Abs(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Harpoon, 1, 3) - harpoon * 2) < 1e-9,
        "Twin Harpoons must fire two harpoons, doubling the Arc Harpoon's yield");
      // Lightning shards add half a pulse per conductor, once per pulse for the whole volley.
      var storm = new UpgradesGeneratorUpgrades { ArcHarpoon = true, MiningLaser = true, Railgun = true, LaserTwinBeam = true };
      double pulsesOnly = MainShipWeapons.GemsPerSecond(storm, MainShipWeapon.Harpoon, 1, 3);
      storm.LaserArcLance = true;
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(storm, MainShipWeapon.Harpoon, 1, 3) - pulsesOnly * 2) < 1e-9,
        "Arc Lance adds half a pulse at each beam: Twin Lasers double the harpoon");
      storm.LaserArcLance = false;
      storm.RailgunConductor = true;
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(storm, MainShipWeapon.Harpoon, 1, 3) - pulsesOnly * 2.5) < 1e-9,
        "Conductor Round adds half a pulse for each of three lodged slugs");
      storm.Railgun = false;
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(storm, MainShipWeapon.Harpoon, 1, 3) - pulsesOnly) < 1e-9,
        "Conductor Round needs the Railgun");
      storm.Railgun = storm.HarpoonTwin = storm.LaserArcLance = true;
      storm.RailgunConductor = false;
      double twinPulses = 2 * pulsesOnly;
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(storm, MainShipWeapon.Harpoon, 1, 3) - twinPulses * 1.5) < 1e-9,
        "Conductors arc once per pulse, not once per harpoon");
      Check(upgrades.UpgradeButtons["ALC1"].Data.UpgradeDefinition.Currency == CoreShards.Currency
        && upgrades.UpgradeButtons["ALC1"].Data.BlockedBy == "LZH1",
        "Arc Lance must be a Core Shard choice at the end of the laser's branch");
      var crits = new UpgradesGeneratorUpgrades { AutoCannon = true, CannonCritical = true };
      double critCannon = MainShipWeapons.GemsPerSecond(crits, MainShipWeapon.Cannon, 1, 3);
      crits.CannonCascade = true;
      Check(MainShipWeapons.GemsPerSecond(crits, MainShipWeapon.Cannon, 1, 3) > critCannon * 2
        && upgrades.UpgradeButtons["CCS1"].Data.UpgradeDefinition.Currency == CoreShards.Currency
        && upgrades.UpgradeButtons["CCS1"].Data.BlockedBy == "CCR1",
        "Critical Cascade is the cannon's second Core Shard choice, after Critical Shells, and chains more crits");
      Check(upgrades.UpgradeButtons["TGF1"].Data.UpgradeDefinition.Currency == CoreShards.Currency
        && upgrades.UpgradeButtons["TGF1"].Data.BlockedBy == "HCM1",
        "Trigger Finger is a Core Shard choice at the end of the hold-click branch");
      var bank = new UpgradesGeneratorUpgrades { Railgun = true };
      double plainRail = MainShipWeapons.GemsPerSecond(bank, MainShipWeapon.Railgun, 1, 3);
      bank.RailgunCapacitor = true;
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(bank, MainShipWeapon.Railgun, 1, 3)
          - plainRail * MainShipWeapons.CapacitorRoundBonus) < 1e-9
        && upgrades.UpgradeButtons["RCB1"].Data.UpgradeDefinition.Currency == CoreShards.Currency
        && upgrades.UpgradeButtons["RCB1"].Data.BlockedBy == "BTS1",
        "Capacitor Bank is the Railgun's second Core Shard choice, its rounds 50% stronger");
      Check(upgrades.UpgradeButtons["RCN1"].Data.UpgradeDefinition.Currency == "red"
        && upgrades.UpgradeButtons["RCN1"].Data.BlockedBy == "RGR1" && !upgrades.UpgradeButtons.ContainsKey("BSS1"),
        "Conductor Round is a regular railgun upgrade where Singularity Round used to be");

      var twin = upgrades.UpgradeButtons["THP1"].Data;
      Check(twin.UpgradeDefinition.Currency == CoreShards.Currency && twin.BlockedBy == "AHB1",
        "Twin Harpoons must be a Core Shard choice at the end of the harpoon's anchor branch");

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
