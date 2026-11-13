using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class MoreModuleChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.001f;
  // Delivery bonuses multiply as doubles and round up, so allow one unit of rounding.
  private static bool About(ulong a, ulong b) => (a > b ? a - b : b - a) <= 1;

  private static void Equip(Scene scene, ShipModule first, ShipModule second = ShipModule.None)
  {
    scene.Manager.Modules.TryEquip(0, 0, ShipModule.None);
    scene.Manager.Modules.TryEquip(0, 1, ShipModule.None);
    scene.Manager.Modules.TryEquip(0, 0, first);
    scene.Manager.Modules.TryEquip(0, 1, second);
    scene.Ship.CarryingGemCount = 0;
    scene.Ship.CarryingGemBaseValue = 0;
    scene.Ship.BeginModuleTrip();
  }

  public static void Run()
  {
    CheckLoadoutBits();
    CheckStats();
    CheckValueAndFuel();
    CheckPulls();
    CheckFullCargoEffects();
    CheckRelay();
    Console.WriteLine("Module expansion passed: 59 independent loadout bits, tradeoffs, conditional stats, Tuning Kit, cargo and delivery bonuses, module gem bonuses, pull limits, lightning, relay and phoenix.");
  }

  private static void CheckLoadoutBits()
  {
    using var scene = new Scene(ShipModule.None);
    var modules = Enum.GetValues<ShipModule>().Where(m => m != ShipModule.None).ToArray();
    Check(modules.Length == 59 && modules.All(m => (int)m < 64), "All 59 modules fit in a 64-bit trip snapshot");
    foreach (var module in modules)
    {
      Equip(scene, module);
      foreach (var candidate in modules)
        Check(scene.Ship.HasModule(candidate) == (candidate == module), $"No bit alias: {module} / {candidate}");
      Check(!scene.Ship.HasModule(ShipModule.None), "Empty slot never activates an effect");
      foreach (var type in new[] { Harvester.HarvesterType.Drone, Harvester.HarvesterType.HomeBase })
      {
        var other = new Harvester { Type = type };
        other.BeginModuleTrip();
        Check(modules.All(m => !other.HasModule(m)), "Non-fleet collectors never inherit modules");
      }
    }
    Equip(scene, ShipModule.FinalSweep, ShipModule.PrismFilter); // IDs differ by 32.
    scene.Manager.Modules.TryEquip(0, 0, ShipModule.None);
    Check(scene.Ship.HasModule(ShipModule.FinalSweep) && scene.Ship.HasModule(ShipModule.PrismFilter),
      "Both halves of the snapshot survive a mid-trip edit");
    scene.Ship.BeginModuleTrip();
    Check(!scene.Ship.HasModule(ShipModule.FinalSweep) && scene.Ship.HasModule(ShipModule.PrismFilter),
      "Removing a low bit does not erase a high bit");
    var available = scene.Manager.Modules.GetAvailableModules().ToArray();
    Check(available.Select(m => ModuleCatalog.Rarities[(int)m]).SequenceEqual(
      available.Select(m => ModuleCatalog.Rarities[(int)m]).OrderBy(r => r)), "Inventory is grouped by rarity");
    Check(ModuleCatalog.Icons.Skip(1).Distinct().Count() == ModuleCatalog.Icons.Length - 1, "Each module has its own icon");
  }

  private static void CheckStats()
  {
    using var scene = new Scene(ShipModule.None);
    var ship = scene.Ship;
    float speed = BaseStats.GetHarvesterSpeed(ship), range = BaseStats.GetHarvesterCollectionRange(ship);
    float fuel = ship.ModuleMaxFuel, refuel = BaseStats.GetHarvesterRefuelSpeedMultiplier(ship);
    float efficiency = BaseStats.GetHarvesterFuelEfficiency(ship);
    int capacity = BaseStats.GetHarvesterCapacity(ship);
    Equip(scene, ShipModule.AuxiliaryTank);
    Check(Near(ship.ModuleMaxFuel, fuel * 2f) && Near(BaseStats.GetHarvesterRefuelSpeedMultiplier(ship), refuel * 1.6f),
      "Auxiliary Tank doubles the tank and speeds up refueling");
    Equip(scene, ShipModule.AuxiliaryTank, ShipModule.TuningKit);
    Check(Near(ship.ModuleMaxFuel, fuel * 3f) && Near(BaseStats.GetHarvesterRefuelSpeedMultiplier(ship), refuel * 2.2f),
      "Tuning Kit doubles a Common module's bonuses");
    Equip(scene, ShipModule.IonBooster, ShipModule.TuningKit);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.5f), "Tuned Ion Booster");
    Equip(scene, ShipModule.CargoPod, ShipModule.TuningKit);
    Check(BaseStats.GetHarvesterCapacity(ship) == capacity * 2, "Tuned Cargo Pod");
    Equip(scene, ShipModule.IonBooster, ShipModule.GemPolisher);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.25f), "Commons keep their own bonus without the kit");
    Equip(scene, ShipModule.HomewardJets, ShipModule.LaunchCapacitor);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.6f), "Launch boost applies while empty");
    ship.CarryingGemCount = (uint)capacity;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 2.56f), "Launch and return-trip boosts stack");
    ship.AdvanceDroneTimers(4f);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.6f), "Launch boost expires at four seconds");
    Equip(scene, ShipModule.DeepHold, ShipModule.LightFrame);
    Check(BaseStats.GetHarvesterCapacity(ship) == (int)Math.Ceiling(capacity * 1.5)
      && Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.12f), "Capacity and speed tradeoffs combine");
    Equip(scene, ShipModule.VacuumNozzle, ShipModule.WidebandArray);
    Check(Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.5f)
      && Near(BaseStats.GetHarvesterSpeed(ship), speed), "Radius modules add together and cost no speed");
    Equip(scene, ShipModule.FuelInjector);
    ship.Fuel = ship.ModuleMaxFuel * 0.5f;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.35f), "Fuel Injector runs at half a tank or more");
    ship.Fuel -= 1;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed), "Fuel Injector cuts out below half a tank");
    ship.Fuel = ship.ModuleMaxFuel;
    Equip(scene, ShipModule.ChronoDrive, ShipModule.MomentumDrive);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 3f), "Chrono starts active");
    ship.AdvanceDroneTimers(5f);
    for (int i = 0; i < 25; i++) ship.PickedUpGem(new Gem { BaseValue = 1 });
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 2f), "Momentum caps at twenty pickups after chrono expires");
    ship.BeginModuleTrip();
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 3f), "New trip clears momentum and restarts chrono");
    Equip(scene, ShipModule.StellarEngine, ShipModule.KineticRefinery);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * (2f * 1.15f)) && Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.25f)
      && Near(BaseStats.GetHarvesterFuelEfficiency(ship), efficiency * 0.5f), "Stellar Engine doubles speed and fuel use");
    ship.PickedUpGem(new Gem { BaseValue = 100 });
    Check(ship.CarryingGemBaseValue == 230, "Kinetic Refinery pays out the ship's module speed bonus");
    Equip(scene, ShipModule.ChronoDrive, ShipModule.KineticRefinery);
    ship.PickedUpGem(new Gem { BaseValue = 100 });
    Check(ship.CarryingGemBaseValue == 250, "Kinetic Refinery's bonus is capped at +150%");
  }

  private static void CheckValueAndFuel()
  {
    using var scene = new Scene(ShipModule.GemPolisher, ShipModule.MidasTouch);
    var ship = scene.Ship;
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 20 && ship.CarryingGemCount == 1, "Polisher and Midas add base value without extra cargo");
    Equip(scene, ShipModule.PrismFilter);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1150), "Prism counts itself");
    Equip(scene, ShipModule.PrismFilter, ShipModule.GemPolisher);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1300), "Prism grows with every module fitted");
    Equip(scene, ShipModule.SoloRig);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1400), "Solo Rig pays out alone");
    Equip(scene, ShipModule.SoloRig, ShipModule.GemPolisher);
    ship.CarryingGemCount = 1;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 1000, "Solo Rig does nothing beside another module");
    Equip(scene, ShipModule.CargoScanner);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1120), "Cargo Scanner adds delivery value");
    Equip(scene, ShipModule.CargoScanner, ShipModule.TuningKit);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1240), "Tuned Cargo Scanner");
    Equip(scene, ShipModule.FleetUplink);
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1070), "Fleet Uplink counts its own ship type");
    scene.Manager.Modules.TryEquip(1, 0, ShipModule.GemPolisher);
    scene.Manager.Modules.TryEquip(2, 0, ShipModule.IonBooster);
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1210), "Fleet Uplink counts every ship type with a module");
    scene.Manager.Modules.TryEquip(1, 0, ShipModule.None);
    scene.Manager.Modules.TryEquip(2, 0, ShipModule.None);
    Equip(scene, ShipModule.LongHaulLedger);
    ship.CarryingGemCount = 1;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 1000, "The ledger starts empty");
    ship.AdvanceDroneTimers(5f);
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1400), "The ledger grows with trip time");
    ship.AdvanceDroneTimers(10f);
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1600), "The ledger is capped");
    Equip(scene, ShipModule.SupernovaCore);
    for (int i = 0; i < 10; i++) ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue >= 236 && ship.CarryingGemBaseValue <= 242, "Supernova pressure builds with the hold");
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue >= 271 && ship.CarryingGemBaseValue <= 277, "Gems past a full hold get the full pressure bonus");
    Equip(scene, ShipModule.QuantumForge);
    for (int i = 0; i < 4; i++) ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 90 && ship.CarryingGemCount == 4, "Forge bonus occurs every fourth cargo pickup");
    Equip(scene, ShipModule.OverflowVault);
    Check(ship.ModuleReturnCapacity == 13, "Overflow Vault holds a quarter more");
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship) - 1;
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 10 && !ship.ReturningToHomebase, "Last normal cargo space does not earn overflow value");
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 30, "Only pickups beyond capacity earn the overflow bonus");
    ship.CarryingGemCount = 13;
    Check(ship.ReturningToHomebase, "The vault turns home when its extra space is full");
    Equip(scene, ShipModule.CourierSeal, ShipModule.InfinityHold);
    Check(BaseStats.GetHarvesterCapacity(ship) == 20, "Infinity doubles cargo capacity");
    ship.CarryingGemCount = 19;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1904), "Fast partial deliveries earn Courier and per-gem Infinity bonuses");
    ship.CarryingGemCount = 1000;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 2800)
      && ship.ApplyAdditionalDeliveryModules(ulong.MaxValue) == ulong.MaxValue, "Infinity bonus caps at 75% and payout saturates");
    ship.AdvanceDroneTimers(ModuleCatalog.CourierDeadlineSeconds);
    ship.CarryingGemCount = 20;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1920), "Courier includes the deadline boundary");
    ship.AdvanceDroneTimers(0.01f);
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1200), "Slow full deliveries receive Infinity but no Courier bonus");
    ship.CarryingGemCount = 1;
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1010), "Infinity scales with actual cargo on partial deliveries");
    ship.BeginModuleTrip();
    Check(About(ship.ApplyAdditionalDeliveryModules(1000), 1616), "A new trip resets the Courier deadline");
    ship.CarryingGemCount = 0;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 1000, "Empty deliveries earn no delivery bonuses");
    Equip(scene, ShipModule.EchoVault);
    for (int i = 1; i <= 6; i++)
    {
      ship.CarryingGemCount = 0;
      Check(ship.ApplyAdditionalDeliveryModules(0) == 0, "Empty deliveries do not advance Echo Vault");
      ship.CarryingGemCount = 1;
      ship.CarryingGemBaseValue = 100;
      ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
      scene.Invoke("DeliverCargo", ship);
      Check(UntitledGemGameGameScreen.DeliveredUncounted - before == (i % 3 == 0 ? 300UL : 100UL),
        "Echo Vault sequence survives ordinary trip resets");
    }
    ship.CarryingGemCount = 1;
    ship.ApplyAdditionalDeliveryModules(100);
    Equip(scene, ShipModule.None);
    Equip(scene, ShipModule.EchoVault);
    ship.CarryingGemCount = 1;
    Check(ship.ApplyAdditionalDeliveryModules(100) == 100 && ship.ApplyAdditionalDeliveryModules(100) == 100,
      "Unequipping resets Echo Vault's delivery sequence");
    Equip(scene, ShipModule.TowWinch);
    Check(PrestigeTalentEffects.TractorTailCapacity(ship) == 3 && ship.ModuleGemBonus(GemCollectionMethod.TractorTails) == 3,
      "Tow Winch tows a quarter of the hold at four times the value without the talent");
    scene.Manager.UGM.TractorTails = true;
    Check(PrestigeTalentEffects.TractorTailCapacity(ship) == 9, "Tow Winch triples a Tractor Tails tail");
    Equip(scene, ShipModule.GemPolisher);
    Check(PrestigeTalentEffects.TractorTailCapacity(ship) == 3 && ship.ModuleGemBonus(GemCollectionMethod.TractorTails) == 0,
      "The talent alone tows at ordinary value");
    scene.Manager.UGM.TractorTails = false;
    Equip(scene, ShipModule.SalvageCell, ShipModule.AuxiliaryTank);
    ship.Fuel = 0;
    var gem = scene.AddGem(scene.Transform.Position);
    scene.Prepare();
    scene.Fleet.CollectGem(gem, ship);
    Check(Near(ship.Fuel, 12f), "Actual pickups restore salvage fuel");
    ship.Fuel = ship.ModuleMaxFuel + 100;
    ship.RegisterModulePickup(false);
    Check(Near(ship.Fuel, ship.ModuleMaxFuel + 100), "Fuel bonuses never remove an existing fuel overfill");
    Equip(scene, ShipModule.PhoenixReactor);
    ship.Fuel = 0;
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship) - 1;
    ship.CarryingGemBaseValue = 100;
    ulong beforePhoenix = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("IgnitePhoenix", ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted == beforePhoenix && ship.CarryingGemCount > 0, "Phoenix waits for a full hold");
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
    scene.Invoke("IgnitePhoenix", ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - beforePhoenix == 250 && ship.CarryingGemCount == 0
      && ship.CarryingGemBaseValue == 0 && Near(ship.Fuel, ship.ModuleMaxFuel) && !ship.ReturningToHomebase,
      "Phoenix beams a full hold home at 2.5x value, refuels and keeps collecting");
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
    ship.CarryingGemBaseValue = 100;
    scene.Invoke("IgnitePhoenix", ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - beforePhoenix == 250 && ship.ReturningToHomebase, "Phoenix ignites once per trip");
    ship.BeginModuleTrip();
    scene.Invoke("IgnitePhoenix", ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - beforePhoenix == 500, "Phoenix rearms on the next trip");
    foreach (var module in new[] { ShipModule.GemPolisher, ShipModule.MidasTouch, ShipModule.SupernovaCore, ShipModule.KineticRefinery, ShipModule.OverflowVault, ShipModule.QuantumForge })
    {
      Equip(scene, module);
      ship.CarryingGemBaseValue = ulong.MaxValue - 1;
      for (int i = 0; i < 4; i++) ship.PickedUpGem(new Gem { BaseValue = uint.MaxValue });
      Check(ship.CarryingGemBaseValue == ulong.MaxValue, $"{module} cannot overflow cargo value");
    }
  }

  private static void CheckPulls()
  {
    foreach (var test in new[] { (ShipModule.PulseHarvester, 6, 4, 50f, 10), (ShipModule.TwinTractor, 1, 1, 20f, 15), (ShipModule.RiftSiphon, 1, 10, 100f, 30),
      (ShipModule.EchoChamber, 6, 2, 50f, 30) })
    {
      using var scene = new Scene(test.Item1);
      var direct = Enumerable.Range(0, test.Item2).Select(_ => scene.AddGem(new Vector2(500, 500))).ToArray();
      var extras = Enumerable.Range(0, 16).Select(_ => scene.AddGem(new Vector2(500 + test.Item4, 500))).ToArray();
      var reserved = scene.AddGem(new Vector2(510, 500));
      var far = scene.AddGem(new Vector2(900, 500));
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
      foreach (var gem in direct) scene.Fleet.CollectGem(gem, scene.Ship);
      Check(extras.Count(g => g.PickedUp) == test.Item3 && !reserved.PickedUp && !far.PickedUp,
        $"{test.Item1} respects radius, reservation and pull limits");
      Check(scene.Ship.DirectModulePickups == (ulong)test.Item2, "Bonus pulls do not charge direct-pickup effects");
      var method = Enum.Parse<GemCollectionMethod>(test.Item1.ToString());
      Check(scene.Ship.Credits.Count == 1 && scene.Ship.Credits[method] == (ulong)(test.Item3 * test.Item5),
        $"{test.Item1} pulls are credited to the module for the Collection panel; direct pickups are not");
      if (test.Item1 == ShipModule.RiftSiphon)
      {
        var next = scene.AddGem(new Vector2(500, 500));
        scene.Prepare();
        scene.Fleet.CollectGem(next, scene.Ship);
        Check(extras.Count(g => g.PickedUp) == 10, "Rift opens only on the first direct pickup of a trip");
      }
    }
    using (var vacuum = new Scene(ShipModule.TwinTractor, ShipModule.VacuumNozzle))
    {
      var direct = vacuum.AddGem(new Vector2(500, 500));
      var extras = Enumerable.Range(0, 6).Select(_ => vacuum.AddGem(new Vector2(520, 500))).ToArray();
      vacuum.Prepare();
      vacuum.Fleet.CollectGem(direct, vacuum.Ship);
      Check(extras.Count(g => g.PickedUp) == 2, "Vacuum Nozzle adds one gem to a pull");
    }
    using (var cascade = new Scene(ShipModule.CascadeCapacitor))
    {
      var extras = Enumerable.Range(0, 60).Select(i => cascade.AddGem(new Vector2(520 + i % 10 * 4, 480 + i / 10 * 4))).ToArray();
      cascade.Prepare();
      cascade.Invoke("RunCascade", cascade.Ship, new Vector2(500, 500));
      int pulled = extras.Count(g => g.PickedUp);
      Check(pulled >= ModuleCatalog.CascadeGems && pulled <= ModuleCatalog.CascadeMaxGems && cascade.Ship.DirectModulePickups == 0
        && cascade.Ship.Credits[GemCollectionMethod.CascadeCapacitor] == (ulong)pulled * 20,
        "A cascade pulls at least one burst, stays bounded and doubles its gems");
    }
    using var storm = new Scene(ShipModule.StormCoil);
    var triggers = Enumerable.Range(0, 8).Select(_ => storm.AddGem(new Vector2(500, 500))).ToArray();
    var chain = Enumerable.Range(1, 7).Select(i => storm.AddGem(new Vector2(500 + i * 45, 500))).ToArray();
    storm.Prepare();
    foreach (var gem in triggers) storm.Fleet.CollectGem(gem, storm.Ship);
    Check(chain.Take(6).All(g => g.PickedUp) && !chain[6].PickedUp && storm.Ship.StormArcCount == 7,
      "Storm chains across six short hops beyond its starting radius and then stops");
    Check(storm.Ship.Credits[GemCollectionMethod.StormCoil] == 120, "Lightning-struck gems are worth double");
    Check(storm.Ship.DirectModulePickups == 8 && storm.Ship.StormArcRemaining > 0, "Lightning does not recursively charge and has visible arcs");
  }

  private static void CheckFullCargoEffects()
  {
    using var scene = new Scene(ShipModule.EventHorizon);
    var gems = Enumerable.Range(0, 60).Select(i => scene.AddGem(new Vector2(525 + i * 0.2f, 500))).ToArray();
    var far = scene.AddGem(new Vector2(901, 500));
    scene.Prepare();
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(!gems.Any(g => g.PickedUp), "Full-cargo triggers wait for cargo");
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.Count(g => g.PickedUp) == 24 && !far.PickedUp
      && scene.Ship.Credits[GemCollectionMethod.EventHorizon] == 600,
      "Event Horizon pulls a bounded field of gems at +150% value");
    Check(scene.Ship.DirectModulePickups == 0, "Full-load bonus pulls do not charge direct-pickup modules");
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.Count(g => g.PickedUp) == 24, "Event Horizon fires once per trip");
    scene.Ship.BeginModuleTrip();
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.Count(g => g.PickedUp) == 48, "Next full trip rearms it");
  }

  private static void CheckRelay()
  {
    using var scene = new Scene(ShipModule.AstralRelay, ShipModule.MidasTouch);
    var gems = Enumerable.Range(0, 10).Select(_ => scene.AddGem(new Vector2(500, 500))).ToArray();
    var bonus = scene.AddGem(new Vector2(500, 500));
    scene.Prepare();
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    foreach (var gem in gems) scene.Fleet.CollectGem(gem, scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 270
      && scene.Ship.CarryingGemBaseValue == 180 && scene.Ship.CarryingGemCount == 10,
      "Relay copies the cargo at half a hold and at a full one, without consuming cargo");
    scene.Invoke("CollectGem", bonus, scene.Ship, false);
    Check(scene.Ship.DirectModulePickups == 10 && UntitledGemGameGameScreen.DeliveredUncounted - before == 270,
      "The relay transmits twice per trip");
    Check(scene.Ship.RelayFlashRemaining > 0, "Relay transmission has visual feedback");
  }
}
