using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class ExpandedSignalChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    static void Near(double actual, double expected, string message)
      => Check(Math.Abs(actual - expected) < 0.001, $"{message}: {actual} != {expected}");
    var manager = new UpgradeManager();
    var signals = manager.Signals;
    void Stack(SignalKind kind, long count) => signals.Counts[(int)kind * SignalProgression.RarityCount] = count;

    Check(Enum.GetValues<SignalKind>().Length == SignalProgression.SignalCount, "Catalog matches stat IDs");
    Check(SignalCatalog.Definitions.Select(d => d.Name).Distinct().Count() == SignalCatalog.Definitions.Length,
      "Every signal has its own name");
    foreach (var definition in SignalCatalog.Definitions)
      Check(File.Exists(Path.Combine("Content", definition.Icon)), $"Signal icon exists: {definition.Name}");
    manager.UGA.ChainResidualCharge = 100;
    manager.UGA.DroneSweepEfficiency = 100;
    var stats = new (SignalKind Kind, Func<double> Read, bool Integer)[]
    {
      (SignalKind.SpawnFrequency, () => SignalStats.FireRate(MainShipWeapon.Cannon), false),
      (SignalKind.SpawnFrequency, () => SignalStats.FireRate(MainShipWeapon.Railgun), false),
      (SignalKind.SpawnCount, () => SignalStats.FirePower(MainShipWeapon.Cannon), true),
      (SignalKind.SpawnCount, () => SignalStats.FirePower(MainShipWeapon.Rockets), true),
      (SignalKind.ClickShotDamage, () => SignalStats.ClickShotDamage(100), true),
      (SignalKind.ClickRadius, () => SignalStats.ClickRadius, false),
      (SignalKind.ClickValue, () => SignalStats.ClickValue, false),
      (SignalKind.ClickChainRange, () => SignalStats.ClickChainRange, false),
      (SignalKind.HoldClickFrequency, () => SignalStats.HoldClickFrequency, false),
      (SignalKind.ClickComboWindow, () => SignalStats.ClickComboWindow, false),
      (SignalKind.TractorBeltRadius, () => SignalStats.TractorBeltRadius, false),
      (SignalKind.TractorBeltSpeed, () => SignalStats.TractorBeltSpeed, false),
      (SignalKind.TractorBeltDuration, () => SignalStats.TractorBeltDuration, false),
      (SignalKind.TractorBeltCapacity, () => SignalStats.TractorBeltCapacity, true),
      (SignalKind.DroneCount, () => SignalStats.DroneCount, true),
      (SignalKind.DroneLifetime, () => SignalStats.DroneLifetime, false),
      (SignalKind.DroneSpeed, () => SignalStats.DroneSpeed, false),
      (SignalKind.DroneRange, () => SignalStats.DroneRange, false),
      (SignalKind.CannonPower, () => SignalStats.FirePower(MainShipWeapon.Cannon), true),
      (SignalKind.LaserPower, () => SignalStats.FirePower(MainShipWeapon.Laser), true),
      (SignalKind.HarpoonPower, () => SignalStats.FirePower(MainShipWeapon.Harpoon), true),
      (SignalKind.RocketPower, () => SignalStats.FirePower(MainShipWeapon.Rockets), true),
      (SignalKind.RailgunPower, () => SignalStats.FirePower(MainShipWeapon.Railgun), true),
      (SignalKind.CriticalChance, () => SignalStats.CriticalChance, false),
      (SignalKind.CriticalChance, () => SignalStats.CritChance(0.05f), false),
      (SignalKind.CriticalDamage, () => SignalStats.CritMultiplier, false),
      (SignalKind.ShockDuration, () => SignalStats.ShockSeconds, false),
      (SignalKind.WeakPointDamage, () => SignalStats.WeakPointMultiplier, false),
      (SignalKind.MoltenDuration, () => SignalStats.MoltenDurationMultiplier, false),
      (SignalKind.DrillRate, () => SignalStats.CoreDrillRate, false),
      (SignalKind.SpawnerCount, () => SignalStats.SpawnerCount, true),
      (SignalKind.ChainValue, () => SignalStats.ChainValue, true),
      (SignalKind.SweepValue, () => SignalStats.SweepValue, true),
      (SignalKind.ConstellationCapacity, () => ConstellationNet.CaptureLimit, true)
    };
    foreach (var stat in stats)
    {
      double original = stat.Read();
      Stack(stat.Kind, 4); // +20%, leaves base tree values untouched.
      double expected = original * (1 + 4 * SignalProgression.BonusForRarity((int)stat.Kind, 0) / 100);
      Near(stat.Read(), stat.Integer ? Math.Ceiling(expected) : expected, stat.Kind.ToString());
      Stack(stat.Kind, 0);
      Near(stat.Read(), original, $"{stat.Kind} default is unchanged");
    }
    Near(SignalStats.GemLimit, manager.UG.MaxGemCount, "The gem limit is a fixed performance cap");
    Check(!Enum.GetNames<SignalKind>().Any(name => name is "GemLimit" or "ClusterSize" or "LuckyValue" or "ShowerCount"
      or "ShowerFrequency" or "CometCount" or "CometFrequency" or "MagnetDuration" or "MagnetCooldown"
      or "PassiveIncome" or "OverloadPressure" or "FuelEfficiency" or "CommandMagnetStrength"),
      "Signals for retired systems are gone");
    // Weapon signals stack with Shaped Charges, which boosts every weapon.
    Stack(SignalKind.SpawnCount, 4);
    Stack(SignalKind.LaserPower, 4);
    Check(SignalStats.FirePower(MainShipWeapon.Laser) == 3 && SignalStats.FirePower(MainShipWeapon.Cannon) == 2,
      "Per-weapon fire power stacks on top of Shaped Charges and only affects its weapon");
    Stack(SignalKind.SpawnCount, 0);
    Stack(SignalKind.LaserPower, 0);
    Stack(SignalKind.CriticalChance, 1000);
    Near(SignalStats.CriticalChance, SignalStats.MaxCriticalChance, "Critical Payload is capped for Critical Shells");
    Near(SignalStats.CritChance(0.5f), 1, "Every other crit chance tops out at always");
    Near(SignalStats.CritChance(0f), 0, "Critical Payload never creates crits from nothing");
    Stack(SignalKind.CriticalChance, 0);
    CheckClickPowers();

    var ship = new Harvester { Type = Harvester.HarvesterType.Harvester };
    float range = BaseStats.GetHarvesterCollectionRange(ship);
    Stack(SignalKind.CollectionRange, 4);
    Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.1, "Fleet collection range");
    ship.CarryingGemCount = int.MaxValue;
    float speed = BaseStats.GetHarvesterSpeed(ship);
    Stack(SignalKind.ReturnSpeed, 4);
    Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.2, "Return speed");
    ship.CarryingGemCount = 0;
    Near(BaseStats.GetHarvesterSpeed(ship), speed, "Return bonus only applies when returning");
    ship.Type = Harvester.HarvesterType.HomeBase;
    range = BaseStats.GetHarvesterCollectionRange(ship);
    Stack(SignalKind.HomeRange, 4);
    Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.1, "Homebase range");
    ship.Type = Harvester.HarvesterType.Drone;
    range = BaseStats.GetHarvesterCollectionRange(ship);
    speed = BaseStats.GetHarvesterSpeed(ship);
    Stack(SignalKind.DroneRange, 4); Stack(SignalKind.DroneSpeed, 4);
    Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.1, "Drone range");
    Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.2, "Drone speed");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
    manager.UGA.IncreaseDroneFuel = 1;
    Stack(SignalKind.DroneLifetime, 20);
    drone.AdvanceDroneTimers(1.5f);
    Check(!drone.ReturningToHomebase, "Lifetime signal keeps drone active past its old expiry");
    drone.AdvanceDroneTimers(0.6f);
    Check(drone.ReturningToHomebase, "Drone expires at boosted lifetime");

    var cooldowns = new (SignalKind Kind, IHomeBaseAbility Ability)[]
    {
      (SignalKind.DrillCooldown, new CoreDrillAbility()),
      (SignalKind.ChainCooldown, new ChainLightningAbility()),
      (SignalKind.DroneCooldown, new DroneAbility()),
      (SignalKind.SpawnerCooldown, new GemSpawnerAbility())
    };
    foreach (var item in cooldowns)
    {
      int original = item.Ability.MaxCooldownTime;
      Stack(item.Kind, 1); Stack(SignalKind.AbilityCooldown, 1);
      Check(item.Ability.MaxCooldownTime == (int)(original * 0.95 * 0.95), "Specific cooldown stacks with global");
      Stack(item.Kind, 0); Stack(SignalKind.AbilityCooldown, 0);
    }

    manager = new UpgradeManager();
    foreach (var kind in new[] { SignalKind.ClickChainRange, SignalKind.HoldClickFrequency, SignalKind.ClickComboWindow,
      SignalKind.TractorBeltRadius, SignalKind.TractorBeltSpeed, SignalKind.TractorBeltDuration, SignalKind.TractorBeltCapacity })
      Check(!SignalCatalog.IsAvailable((int)kind), $"Locked click utility excluded: {kind}");
    Check(SignalCatalog.IsAvailable((int)SignalKind.ClickValue), "Manual click value is always useful");
    manager.UG.ClickChainCount = 1;
    manager.UG.HoldClickEnabled = true;
    manager.UG.ClickComboBonus = 0.05f;
    manager.UG.TractorBeltEnabled = true;
    foreach (var kind in new[] { SignalKind.ClickChainRange, SignalKind.HoldClickFrequency, SignalKind.ClickComboWindow,
      SignalKind.TractorBeltRadius, SignalKind.TractorBeltSpeed, SignalKind.TractorBeltDuration, SignalKind.TractorBeltCapacity })
      Check(SignalCatalog.IsAvailable((int)kind), $"Unlocked click utility included: {kind}");
    manager.UG.TractorBeltPermanent = true;
    Check(!SignalCatalog.IsAvailable((int)SignalKind.TractorBeltDuration)
      && SignalCatalog.IsAvailable((int)SignalKind.TractorBeltSpeed), "A permanent belt no longer offers run time signals");
    manager.UG.TractorBeltPermanent = false;
    Check(!SignalCatalog.IsAvailable((int)SignalKind.DroneCount), "Locked drone ability excluded");
    Check(SignalCatalog.IsAvailable((int)SignalKind.ClickShotDamage), "Clicking the planet always fires shots");
    foreach (var kind in new[] { SignalKind.AbilityCooldown, SignalKind.CommandOverdriveDuration,
      SignalKind.CommandAbilityRecharge, SignalKind.LaserPower, SignalKind.HarpoonPower, SignalKind.RocketPower,
      SignalKind.RailgunPower, SignalKind.CriticalChance, SignalKind.CriticalDamage, SignalKind.ShockDuration,
      SignalKind.WeakPointDamage, SignalKind.MoltenDuration, SignalKind.DrillRate, SignalKind.DrillCooldown })
      Check(!SignalCatalog.IsAvailable((int)kind), $"Locked {kind} excluded");
    Check(SignalCatalog.IsAvailable((int)SignalKind.CannonPower), "The cannon is always mounted");
    manager.UGM.Deadeye = true;
    Check(SignalCatalog.IsAvailable((int)SignalKind.CriticalChance) && SignalCatalog.IsAvailable((int)SignalKind.CriticalDamage)
      && !SignalCatalog.IsAvailable((int)SignalKind.ShockDuration), "Any crit source opens the crit signals");
    manager.UGM.Deadeye = false;
    manager.UGM.FaultSeeding = true;
    Check(SignalCatalog.IsAvailable((int)SignalKind.WeakPointDamage), "Fault Seeding opens the weak point signal");
    manager.UGM.FaultSeeding = false;
    manager.UGM.CommandCenterUnlocked = true;
    Check(SignalCatalog.IsAvailable((int)SignalKind.WeakPointDamage), "Fault Scan opens the weak point signal");
    Check(SignalCatalog.IsAvailable((int)SignalKind.CommandOverdriveDuration)
      && !SignalCatalog.IsAvailable((int)SignalKind.CommandAbilityRecharge), "Command signals need Command Center; System Surge also systems");
    manager.UGM.ShipSystemsUnlocked = true;
    Check(SignalCatalog.IsAvailable((int)SignalKind.AbilityCooldown)
      && SignalCatalog.IsAvailable((int)SignalKind.CommandAbilityRecharge), "Auxiliary Power opens the system signals");
    manager.UG.MiningLaser = manager.UG.ArcHarpoon = manager.UG.RocketPods = manager.UG.Railgun = true;
    manager.UG.CannonCritical = true;
    manager.UGM.ThermiteRounds = manager.UGM.GalvanicShock = true;
    manager.UGA.CoreDrill = 1;
    foreach (var kind in new[] { SignalKind.LaserPower, SignalKind.HarpoonPower, SignalKind.RocketPower,
      SignalKind.RailgunPower, SignalKind.CriticalChance, SignalKind.CriticalDamage, SignalKind.ShockDuration,
      SignalKind.MoltenDuration, SignalKind.DrillRate, SignalKind.DrillCooldown })
      Check(SignalCatalog.IsAvailable((int)kind), $"Unlocked {kind} included");
    manager.UGA.Drones = 1;
    Check(SignalCatalog.IsAvailable((int)SignalKind.DroneCount), "Unlocked drone ability included");
    var wallet = new GameState { CurrentRedGemCount = ulong.MaxValue };
    var random = new Random(777);
    var seen = new HashSet<int>();
    for (int i = 0; i < 100; i++)
    {
      var offered = new SignalProgression();
      Check(offered.TryScan(wallet, random, SignalCatalog.IsAvailable), "Eligible scan succeeds");
      Check(offered.PendingChoices.All(c => SignalCatalog.IsAvailable(c.Signal)), "No locked-system offers");
      offered = new SignalProgression();
      Check(offered.TryScan(wallet, random), "Unfiltered catalog scan succeeds");
      foreach (var choice in offered.PendingChoices) seen.Add(choice.Signal);
    }
    Check(seen.Count == SignalProgression.SignalCount, "All catalog entries can roll");
    var savePath = Path.Combine(Path.GetTempPath(), "expanded-signals-" + Guid.NewGuid() + ".json");
    try
    {
      var store = new GameSaveStore(savePath);
      var saved = new GameSave();
      for (int i = 0; i < SignalProgression.SignalCount; i++) saved.Signals.Counts[i * 5 + 4] = i + 1;
      Check(store.Save(saved), "Expanded catalog saves");
      var restored = store.Load();
      Check(restored != null && restored.Signals.Counts.SequenceEqual(saved.Signals.Counts), "Every new signal survives save/load");
    }
    finally
    {
      foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
    }
    Console.WriteLine($"Expanded signal checks passed: {SignalProgression.SignalCount} definitions, effective stats, eligibility, cooldowns and persistence.");
  }

  private static void CheckClickPowers()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    static void Near(double actual, double expected, string message)
      => Check(Math.Abs(actual - expected) < 0.001, $"{message}: {actual} != {expected}");
    var ug = new UpgradesGeneratorUpgrades
    {
      ClickValueMultiplier = 2, ClickChainCount = 1, ClickChainRange = 60,
      ClickComboBonus = 0.05f, HoldClickEnabled = true, TractorBeltEnabled = true
    };
    var signals = new SignalProgression();
    foreach (var kind in new[] { SignalKind.ClickValue, SignalKind.ClickChainRange, SignalKind.HoldClickFrequency,
      SignalKind.ClickComboWindow, SignalKind.TractorBeltRadius, SignalKind.TractorBeltSpeed, SignalKind.TractorBeltDuration,
      SignalKind.TractorBeltCapacity })
      signals.Counts[(int)kind * SignalProgression.RarityCount] = 4;
    var clicks = new ClickUtility();
    Check(clicks.ShouldClick(true, true, true, 0, ug, signals), "Signals preserve immediate manual clicks");
    Check(!clicks.ShouldClick(false, true, true, 0.65f, ug, signals)
      && clicks.ShouldClick(false, true, true, 0.02f, ug, signals), "Pulse signal advances the actual repeat timer");
    var grid = new GemSpatialIndex(4, 30);
    int seed = grid.AddGem(1, 0, 0, 1);
    int chain = grid.AddGem(2, 65, 0, 1);
    int collected = 0;
    Check(clicks.Activate(grid, new[] { seed }, Microsoft.Xna.Framework.Vector2.Zero, ug,
      (index, multiplier) =>
      {
        ++collected;
        Near(multiplier, 2.4, "Click value signal reaches direct and linked collection");
        grid.Gems[index].ClaimState = 2;
        return true;
      }, 1, signals), "Boosted manual click activates");
    Check(collected == 2 && grid.Gems[chain].ClaimState == 2, "Chain signal reaches gems beyond the unboosted hop range");
    Near(clicks.ComboRemaining, 2.4, "Combo signal extends the live combo timer");
    Near(ug.ClickValueMultiplier, 2, "Signals leave purchased click stats intact");
    Near(ug.ClickChainRange, 60, "Signals leave purchased chain stats intact");
    var beltStats = TractorBeltStats.From(ug, 19, signals);
    Near(beltStats.Radius, 57 * 1.1, "Intake Lens applies reduced range bonuses");
    Near(beltStats.Speed, TractorBelt.BaseSpeed * 1.2, "Mag-Rail Coils speed up the belt");
    Near(beltStats.Duration, 15 * 1.2, "Belt Capacitors lengthen each placement");
    Check(beltStats.Capacity == 2, "Cargo Racks round pod capacity up");
    var belt = new TractorBelt();
    Check(belt.HandleInput(true, true, true, Microsoft.Xna.Framework.Vector2.Zero, beltStats)
      && Math.Abs(belt.Lines[0].Remaining - 18) < 0.001, "A placement runs for its signaled time");
    Near(ug.TractorBeltRadiusMultiplier, 3, "Signals leave purchased belt stats intact");
    Check(ug.TractorBeltCapacity == 1, "Signals leave purchased pod capacity intact");
  }
}
