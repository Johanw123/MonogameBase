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
    Console.WriteLine("Thirty-module expansion passed: 49 independent loadout bits, tradeoffs, conditional stats, fuel recovery, cargo bonuses, delivery sequences, pull limits, lightning and relay.");
  }

  private static void CheckLoadoutBits()
  {
    using var scene = new Scene(ShipModule.None);
    var modules = Enum.GetValues<ShipModule>().Where(m => m != ShipModule.None).ToArray();
    Check(modules.Length == 49 && modules.All(m => (int)m < 64), "All 49 modules fit in a 64-bit trip snapshot");
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
    Check(ModuleCatalog.Icons.Skip(1).Distinct().Count() == 49, "Each module has its own icon");
  }

  private static void CheckStats()
  {
    using var scene = new Scene(ShipModule.None);
    var ship = scene.Ship;
    float speed = BaseStats.GetHarvesterSpeed(ship), range = BaseStats.GetHarvesterCollectionRange(ship);
    float fuel = ship.ModuleMaxFuel, refuel = BaseStats.GetHarvesterRefuelSpeedMultiplier(ship);
    float efficiency = BaseStats.GetHarvesterFuelEfficiency(ship);
    int capacity = BaseStats.GetHarvesterCapacity(ship);
    Equip(scene, ShipModule.AuxiliaryTank, ShipModule.QuickCoupler);
    Check(Near(ship.ModuleMaxFuel, fuel * 1.75f) && Near(BaseStats.GetHarvesterRefuelSpeedMultiplier(ship), refuel * 1.6f),
      "Tank and coupler bonuses");
    Equip(scene, ShipModule.HomewardJets, ShipModule.LaunchCapacitor);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.6f), "Launch boost applies while empty");
    ship.CarryingGemCount = (uint)capacity;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 2.56f), "Launch and full-cargo boosts stack");
    ship.AdvanceDroneTimers(4f);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.6f), "Launch boost expires at four seconds");
    Equip(scene, ShipModule.DeepHold, ShipModule.LightFrame);
    Check(BaseStats.GetHarvesterCapacity(ship) == (int)Math.Ceiling(capacity * 1.5)
      && Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.12f), "Capacity and speed tradeoffs combine");
    Equip(scene, ShipModule.VacuumNozzle, ShipModule.CargoScanner);
    Check(Near(BaseStats.GetHarvesterCollectionRange(ship), range * 2f), "Scanner remains inactive below half cargo");
    ship.CarryingGemCount = (uint)((capacity + 1) / 2);
    Check(Near(BaseStats.GetHarvesterCollectionRange(ship), range * 3.5f)
      && Near(BaseStats.GetHarvesterSpeed(ship), speed * 0.85f), "Scanner threshold and nozzle tradeoff");
    Equip(scene, ShipModule.ReserveBurn);
    ship.Fuel = fuel * 0.25f;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed), "Reserve Burn waits until below its threshold");
    ship.Fuel -= 1;
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.8f), "Low fuel activates Reserve Burn");
    Equip(scene, ShipModule.ChronoDrive, ShipModule.MomentumDrive);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 3f), "Chrono starts active");
    ship.AdvanceDroneTimers(5f);
    for (int i = 0; i < 25; i++) ship.PickedUpGem(new Gem { BaseValue = 1 });
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 2f), "Momentum caps at twenty pickups after chrono expires");
    ship.BeginModuleTrip();
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 3f), "New trip clears momentum and restarts chrono");
    Equip(scene, ShipModule.StellarEngine, ShipModule.FuelRecycler);
    Check(Near(BaseStats.GetHarvesterSpeed(ship), speed * 2f) && Near(BaseStats.GetHarvesterCollectionRange(ship), range * 2f)
      && Near(BaseStats.GetHarvesterFuelEfficiency(ship), efficiency), "Recycler offsets Stellar Engine's fuel penalty");
  }

  private static void CheckValueAndFuel()
  {
    using var scene = new Scene(ShipModule.GemPolisher, ShipModule.MidasTouch);
    var ship = scene.Ship;
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 23 && ship.CarryingGemCount == 1, "Polisher rounds up and Midas adds base value without extra cargo");
    Equip(scene, ShipModule.PrismFilter);
    foreach (uint value in new uint[] { 5, 5, 10 }) ship.PickedUpGem(new Gem { BaseValue = value });
    Check(ship.CarryingGemBaseValue == 50, "Prism rewards the first gem and strictly higher records only");
    Equip(scene, ShipModule.QuantumForge);
    for (int i = 0; i < 4; i++) ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 90 && ship.CarryingGemCount == 4, "Forge bonus occurs every fourth cargo pickup");
    Equip(scene, ShipModule.OverflowVault);
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship) - 1;
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 10, "Last normal cargo space does not earn overflow value");
    ship.PickedUpGem(new Gem { BaseValue = 10 });
    Check(ship.CarryingGemBaseValue == 30, "Only pickups beyond capacity earn the overflow bonus");
    Equip(scene, ShipModule.CourierSeal, ShipModule.InfinityHold);
    Check(BaseStats.GetHarvesterCapacity(ship) == 30, "Infinity triples cargo capacity");
    ship.CarryingGemCount = 29;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 1000, "Full-load bonuses do not apply to partial loads");
    ship.CarryingGemCount = 30;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 2275, "Courier and Infinity multiply on a full load");
    ship.CarryingGemCount = 1000;
    Check(ship.ApplyAdditionalDeliveryModules(1000) == 5250
      && ship.ApplyAdditionalDeliveryModules(ulong.MaxValue) == ulong.MaxValue, "Infinity bonus caps at 200% and payout saturates");
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
    Equip(scene, ShipModule.DockBattery);
    ship.Fuel = 0;
    ship.CarryingGemCount = 1;
    scene.Invoke("DeliverCargo", ship);
    Check(Near(ship.Fuel, ship.ModuleMaxFuel * 0.35f), "Dock Battery restores fuel on delivery");
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
    Check(ship.TryRestorePhoenixFuel(1) && Near(ship.Fuel, ship.ModuleMaxFuel), "Phoenix restores a depleted tank");
    ship.Fuel = 0;
    Check(!ship.TryRestorePhoenixFuel(1), "Phoenix activates only once per trip");
    ship.BeginModuleTrip();
    Check(ship.TryRestorePhoenixFuel(1), "Phoenix rearms on the next trip");
    foreach (var module in new[] { ShipModule.GemPolisher, ShipModule.MidasTouch, ShipModule.PrismFilter, ShipModule.OverflowVault, ShipModule.QuantumForge })
    {
      Equip(scene, module);
      ship.CarryingGemBaseValue = ulong.MaxValue - 1;
      for (int i = 0; i < 4; i++) ship.PickedUpGem(new Gem { BaseValue = uint.MaxValue });
      Check(ship.CarryingGemBaseValue == ulong.MaxValue, $"{module} cannot overflow cargo value");
    }
  }

  private static void CheckPulls()
  {
    foreach (var test in new[] { (ShipModule.PulseHarvester, 6, 3, 100f), (ShipModule.TwinTractor, 1, 1, 40f), (ShipModule.RiftSiphon, 1, 10, 200f) })
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
      if (test.Item1 == ShipModule.RiftSiphon)
      {
        var next = scene.AddGem(new Vector2(500, 500));
        scene.Prepare();
        scene.Fleet.CollectGem(next, scene.Ship);
        Check(extras.Count(g => g.PickedUp) == 10, "Rift opens only on the first direct pickup of a trip");
      }
    }
    using var storm = new Scene(ShipModule.StormCoil);
    var triggers = Enumerable.Range(0, 8).Select(_ => storm.AddGem(new Vector2(500, 500))).ToArray();
    var chain = Enumerable.Range(1, 7).Select(i => storm.AddGem(new Vector2(500 + i * 90, 500))).ToArray();
    storm.Prepare();
    foreach (var gem in triggers) storm.Fleet.CollectGem(gem, storm.Ship);
    Check(chain.Take(6).All(g => g.PickedUp) && !chain[6].PickedUp && storm.Ship.StormArcCount == 7,
      "Storm chains across six short hops beyond its starting radius and then stops");
    Check(storm.Ship.DirectModulePickups == 8 && storm.Ship.StormArcRemaining > 0, "Lightning does not recursively charge and has visible arcs");
  }

  private static void CheckFullCargoEffects()
  {
    using var scene = new Scene(ShipModule.ReactorBloom, ShipModule.EventHorizon);
    var gems = Enumerable.Range(0, 60).Select(i => scene.AddGem(new Vector2(550 + i * 0.2f, 500))).ToArray();
    var far = scene.AddGem(new Vector2(901, 500));
    scene.Prepare();
    scene.Ship.Fuel = 1;
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(!gems.Any(g => g.PickedUp) && Near(scene.Ship.Fuel, 1), "Full-cargo triggers wait for cargo");
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.Count(g => g.PickedUp) == 54 && !far.PickedUp && Near(scene.Ship.Fuel, scene.Ship.ModuleMaxFuel),
      "Reactor Bloom and Event Horizon combine bounded pulls and a full fuel restore");
    Check(scene.Ship.DirectModulePickups == 0, "Full-load bonus pulls do not charge direct-pickup modules");
    scene.Ship.Fuel = 1;
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.Count(g => g.PickedUp) == 54 && Near(scene.Ship.Fuel, 1), "Both full-load effects fire once per trip");
    scene.Ship.BeginModuleTrip();
    scene.Invoke("ApplyFullCargoModules", scene.Ship, scene.Transform.Position);
    Check(gems.All(g => g.PickedUp), "Next full trip rearms both effects");
  }

  private static void CheckRelay()
  {
    using var scene = new Scene(ShipModule.AstralRelay, ShipModule.MidasTouch);
    var gems = Enumerable.Range(0, 10).Select(_ => scene.AddGem(new Vector2(500, 500))).ToArray();
    var bonus = scene.AddGem(new Vector2(500, 500));
    scene.Prepare();
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    foreach (var gem in gems) scene.Fleet.CollectGem(gem, scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 300
      && scene.Ship.CarryingGemBaseValue == 200 && scene.Ship.CarryingGemCount == 10,
      "Relay transmits growing cargo snapshots without consuming cargo");
    scene.Invoke("CollectGem", bonus, scene.Ship, false);
    Check(scene.Ship.DirectModulePickups == 10 && UntitledGemGameGameScreen.DeliveredUncounted - before == 300,
      "A bonus pickup cannot charge or trigger a relay transmission");
    Check(scene.Ship.RelayFlashRemaining > 0, "Relay transmission has visual feedback");
  }
}
