using System.Reflection;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

internal static class ModuleChecks
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime time) { }
  }
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
  private static object Invoke(HarvesterCollectionSystem fleet, string method, params object[] args)
    => typeof(HarvesterCollectionSystem).GetMethod(method, Private)!.Invoke(fleet, args);

  public static void Run()
  {
    var previousManager = UpgradeManager.Instance;
    ulong previousDelivered = UntitledGemGameGameScreen.DeliveredUncounted;
    try
    {
      CheckInventory();
      CheckSlotMoves();
      CheckCollection();
      CheckDelivery();
      ExpandedModuleChecks.Run();
      MoreModuleChecks.Run();
      ModuleBaysChecks.Run();
      ModuleSalvageChecks.Run();
      Console.WriteLine("Module checks passed: shared inventory, saves, trip snapshots, sweep, tractor, prospecting, wake, jackpot and beacon.");
    }
    finally
    {
      UpgradeManager.Instance = previousManager;
      UntitledGemGameGameScreen.DeliveredUncounted = previousDelivered;
    }
  }

  internal static void GrantAll(ShipyardModules inventory)
  {
    inventory.StartSalvage(new Random(1));
    inventory.Owned.UnionWith(ModuleCatalog.InventoryOrder);
  }

  private static void CheckInventory()
  {
    var manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    var inventory = manager.Modules;
    Check(inventory.GetAvailableModules().Count() == Enum.GetValues<ShipModule>().Length - 1, "Effect-test fixture contains every module");
    Check(inventory.TryEquip(0, 0, ShipModule.FinalSweep), "Equip first module");
    Check(!inventory.TryEquip(1, 0, ShipModule.FinalSweep), "A copy cannot be allocated twice");
    Check(inventory.TryEquip(0, 1, ShipModule.TractorLink)
      && !inventory.TryEquip(0, 2, ShipModule.JackpotCore), "Start with two unlocked slots per type");
    var ship = new Harvester { Type = Harvester.HarvesterType.Harvester };
    var sibling = new Harvester { Type = ship.Type };
    ship.BeginModuleTrip();
    sibling.BeginModuleTrip();
    Check(ship.HasModule(ShipModule.FinalSweep) && sibling.HasModule(ShipModule.FinalSweep), "A type shares its loadout");
    inventory.TryEquip(0, 0, ShipModule.None);
    Check(inventory.IsAvailable(ShipModule.FinalSweep) && ship.HasModule(ShipModule.FinalSweep),
      "Unequip returns inventory immediately but preserves the active trip");
    ship.BeginModuleTrip();
    Check(!ship.HasModule(ShipModule.FinalSweep), "Next trip applies changes");
    Check(inventory.TryEquip(1, 0, ShipModule.FinalSweep), "Move freed copy to another harvester type");
    Check(!inventory.TryEquip(5, 0, ShipModule.JackpotCore)
      && inventory.GetLoadout(Harvester.HarvesterType.Drone) == 0, "Drones have no shipyard slots or modules");
    inventory.Validate();
    var path = Path.Combine(Path.GetTempPath(), "modules-" + Guid.NewGuid() + ".json");
    try
    {
      var store = new GameSaveStore(path);
      Check(store.Save(new GameSave { Modules = inventory }), "Save module inventory");
      var restored = store.Load();
      Check(restored != null && restored.Modules.Slots.SequenceEqual(inventory.Slots)
        && restored.Modules.GetAvailableModules().SequenceEqual(inventory.GetAvailableModules()), "Round-trip exact inventory and assignments");
      Check(!restored.Modules.IsAvailable(ShipModule.FinalSweep), "Reload must not grant another starter copy");
    }
    finally { File.Delete(path); }
    inventory.Slots[0] = ShipModule.FinalSweep;
    bool rejected = false;
    try { inventory.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Reject saves allocating more copies than owned");
    var state = new GameState();
    ModuleChecks.GrantAll(state.Modules);
    state.Modules.TryEquip(1, 1, ShipModule.WakeCollector);
    state.CompletePrestige(1);
    Check(state.Modules.Has(Harvester.HarvesterType.AdvancedHarvester, ShipModule.WakeCollector), "Prestige preserves modules");
  }

  private static void CheckSlotMoves()
  {
    var inventory = new ShipyardModules();
    ModuleChecks.GrantAll(inventory);
    inventory.TryEquip(0, 0, ShipModule.FinalSweep);
    inventory.TryEquip(0, 1, ShipModule.TractorLink);
    Check(inventory.TryMoveSlot(0, 1)
      && inventory.Slots[0] == ShipModule.TractorLink && inventory.Slots[1] == ShipModule.FinalSweep,
      "Dropping equipment on an occupied slot swaps modules without releasing either copy");
    int destination = 2 * ModuleCatalog.MaxSlotsPerType;
    Check(inventory.TryMoveSlot(1, destination) && inventory.Slots[1] == ShipModule.None
      && inventory.Slots[destination] == ShipModule.FinalSweep, "Dragging between ship types moves the module to its destination");
    var before = inventory.Slots.ToArray();
    Check(!inventory.TryMoveSlot(destination, destination) && !inventory.TryMoveSlot(destination, -1)
      && !inventory.TryMoveSlot(destination, inventory.Slots.Length) && !inventory.TryMoveSlot(1, 0)
      && inventory.Slots.SequenceEqual(before), "Invalid drops and empty sources preserve all equipment");
    Check(inventory.TryEquip(2, 0, ShipModule.JackpotCore)
      && inventory.IsAvailable(ShipModule.FinalSweep) && !inventory.IsAvailable(ShipModule.JackpotCore),
      "Replacing equipment from inventory returns the displaced module to inventory");
    Check(inventory.TryEquip(2, 0, ShipModule.None) && inventory.IsAvailable(ShipModule.JackpotCore),
      "Returning equipment makes the module available again");
    inventory.Validate();
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    Check(ModuleCatalog.Icons.Length == ModuleCatalog.Names.Length
      && ModuleCatalog.Icons.Skip(1).All(icon => File.Exists(Path.Combine(root, "Content", icon))),
      "Every module has an existing icon asset");
  }

  private static void CheckCollection()
  {
    var manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    manager.Modules.TryEquip(0, 0, ShipModule.FinalSweep);
    manager.Modules.TryEquip(0, 1, ShipModule.TractorLink);
    var fleet = new FleetProbe();
    using var world = new WorldBuilder().AddSystem(fleet).Build();
    var entity = world.CreateEntity();
    entity.Attach(new Transform2(new Vector2(500, 500)));
    var ship = new Harvester { Entity = entity, Id = entity.Id, Type = Harvester.HarvesterType.Harvester };
    entity.Attach(ship);
    ship.SetCollisionPosition(new Vector2(500, 500));
    ship.BeginModuleTrip();
    Gem AddGem(Vector2 position, uint value)
    {
      var e = world.CreateEntity();
      e.Attach(new Transform2(position));
      var gem = new Gem();
      gem.Initialize(e, 18, value);
      e.Attach(gem);
      gem.GridIndex = fleet.flatSpatialHash.AddGem(e.Id, position.X, position.Y, value);
      return gem;
    }
    float range = BaseStats.GetHarvesterCollectionRange(ship);
    var sweepGem = AddGem(new Vector2(500 + range * 2.5f, 500), 7);
    var farGem = AddGem(new Vector2(850, 500), 100);
    var wakeGem = AddGem(new Vector2(1100, 510), 3);
    var outsideWake = AddGem(new Vector2(1100, 550), 4);
    world.Update(new GameTime());
    fleet.flatSpatialHash.PrepareQueries();
    typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame", Private)!.SetValue(fleet, 1);
    Invoke(fleet, "ClaimFinalSweep", ship, new Vector2(500, 500));
    Check(ship.ClaimedGems.Count == 0, "Sweep waits for full cargo");
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
    Invoke(fleet, "ClaimFinalSweep", ship, new Vector2(500, 500));
    Check(ship.ClaimedGems.Count == 1 && ship.ResolvingFinalSweep, "Full regular ship claims nearby sweep gems");
    Invoke(fleet, "ResolveClaimedGems", ship);
    Check(sweepGem.PickedUp && ship.CarryingGemCount > BaseStats.GetHarvesterCapacity(ship), "Sweep can exceed capacity");
    Check(!ship.TryBeginFinalSweep(new Vector2(500, 500)), "Sweep fires once per trip");
    var target = (Vector2?)Invoke(fleet, "FindProspectorTarget", ship);
    Check(target == new Vector2(850, 500), "Prospector chooses the highest-value local loose gem");
    Invoke(fleet, "CollectTractorGem", new Vector2(800, 500), ship);
    Check(farGem.PickedUp && !wakeGem.PickedUp && ship.TractorFlashRemaining > 0,
      "Tractor collects one nearby gem with visual feedback");
    // Isolate the wake geometry: Tractor Link can legitimately pull a gem outside it.
    manager.Modules.TryEquip(0, 1, ShipModule.WakeCollector);
    ship.BeginModuleTrip();
    Invoke(fleet, "ClaimWake", ship, new Vector2(1000, 500), new Vector2(1200, 500));
    Invoke(fleet, "ResolveClaimedGems", ship);
    Check(wakeGem.PickedUp && !outsideWake.PickedUp, "Wake covers the return segment while excluding gems outside the narrow trail");
    manager.Modules.TryEquip(0, 0, ShipModule.None);
    manager.UGA.DroneFinalSweep = true;
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
    drone.BeginModuleTrip();
    drone.AdvanceDroneTimers(100f);
    Check(drone.TryBeginFinalSweep(Vector2.Zero) && !drone.TryBeginFinalSweep(Vector2.Zero), "Drone sweep fires exactly once at expiry");
  }

  private static void CheckDelivery()
  {
    var manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    manager.Modules.TryEquip(0, 0, ShipModule.ReturnBeacon);
    manager.Modules.TryEquip(0, 1, ShipModule.JackpotCore);
    var fleet = new FleetProbe();
    using var world = new WorldBuilder().AddSystem(fleet).Build();
    var entity = world.CreateEntity();
    var transform = new Transform2(new Vector2(50, 50));
    entity.Attach(transform);
    var ship = new Harvester { Type = Harvester.HarvesterType.Harvester, Entity = entity, Id = entity.Id };
    entity.Attach(ship);
    world.Update(new GameTime());
    ship.BeginModuleTrip();
    ship.CollectionEndpoint = new Vector2(700, 400);
    ship.CarryingGemBaseValue = 100;
    ship.CarryingGemCount = 10;
    var seeded = new Random(1234);
    typeof(HarvesterCollectionSystem).GetField("moduleRandom", Private)!.SetValue(fleet, new Random(1234));
    ulong expected = seeded.NextSingle() < ModuleCatalog.JackpotChance ? 500UL : 100UL;
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    Invoke(fleet, "DeliverCargo", ship);
    Check(transform.Position == new Vector2(700, 400) && !ship.DepartingHomeBase
      && ship.CarryingGemCount == 0 && ship.CollectionEndpoint == null, "Delivery warps to endpoint and starts a fresh trip");
    for (int i = 0; i < 500; i++)
    {
      ship.CarryingGemBaseValue = 100;
      expected += seeded.NextSingle() < ModuleCatalog.JackpotChance ? 500UL : 100UL;
      Invoke(fleet, "DeliverCargo", ship);
    }
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == expected && expected > 50100,
      "Jackpot rolls exactly once per nonempty delivery with the advertised chance and multiplier");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone };
    drone.BeginModuleTrip();
    Check(Enum.GetValues<ShipModule>().All(module => !drone.HasModule(module)),
      "Drones cannot inherit any shipyard module effects");
  }
}
