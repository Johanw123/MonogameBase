using System.Reflection;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

internal static class ExpandedModuleChecks
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  internal sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime time) { }
  }

  internal sealed class Scene : IDisposable
  {
    public readonly UpgradeManager Manager = new();
    public readonly FleetProbe Fleet = new();
    public readonly World World;
    public readonly Harvester Ship;
    public readonly Transform2 Transform = new(new Vector2(500, 500));

    public Scene(ShipModule first, ShipModule second = ShipModule.None)
    {
      ModuleChecks.GrantAll(Manager.Modules);
      Manager.Modules.TryEquip(0, 0, first);
      Manager.Modules.TryEquip(0, 1, second);
      World = new WorldBuilder().AddSystem(Fleet).Build();
      var entity = World.CreateEntity();
      entity.Attach(Transform);
      Ship = new Harvester { Entity = entity, Id = entity.Id, Type = Harvester.HarvesterType.Harvester };
      entity.Attach(Ship);
      Ship.SetCollisionPosition(Transform.Position);
      World.Update(new GameTime());
      typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame", Private)!.SetValue(Fleet, 1);
    }

    public Gem AddGem(Vector2 position, uint value = 10)
    {
      var entity = World.CreateEntity();
      entity.Attach(new Transform2(position));
      var gem = new Gem();
      gem.Initialize(entity, 18, value);
      entity.Attach(gem);
      gem.GridIndex = Fleet.flatSpatialHash.AddGem(entity.Id, position.X, position.Y, value);
      return gem;
    }

    public void Prepare()
    {
      World.Update(new GameTime());
      Fleet.flatSpatialHash.PrepareQueries();
    }

    public object Invoke(string method, params object[] args)
      => typeof(HarvesterCollectionSystem).GetMethod(method, Private)!.Invoke(Fleet, args);
    public void Dispose() => World.Dispose();
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private static bool Near(float actual, float expected) => MathF.Abs(actual - expected) < 0.001f;

  public static void Run()
  {
    var home = UntitledGemGameGameScreen.HomeBasePos;
    try
    {
      CheckCatalogAndStats();
      CheckPickupEffects();
      CheckSingularity();
      CheckSupernovaAndWarp();
      Console.WriteLine("Expanded modules passed: rarity roster, stats, overdrive, echo, refinery, bounded singularity, supernova and phase anchor.");
    }
    finally { UntitledGemGameGameScreen.HomeBasePos = home; }
  }

  private static void CheckCatalogAndStats()
  {
    var manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    int count = Enum.GetValues<ShipModule>().Length;
    Check(count == 50 && ModuleCatalog.Names.Length == count && ModuleCatalog.Icons.Length == count
      && ModuleCatalog.Rarities.Length == count && ModuleCatalog.Descriptions.Length == count,
      "Forty-nine complete module definitions");
    Check(ModuleCatalog.Rarities.Skip(1).Distinct().Count() == 5, "All five rarity tiers have modules");
    var ship = new Harvester { Type = Harvester.HarvesterType.Harvester };
    ship.BeginModuleTrip();
    int capacity = BaseStats.GetHarvesterCapacity(ship);
    float speed = BaseStats.GetHarvesterSpeed(ship);
    float range = BaseStats.GetHarvesterCollectionRange(ship);
    float efficiency = BaseStats.GetHarvesterFuelEfficiency(ship);
    manager.Modules.TryEquip(0, 0, ShipModule.CargoPod);
    manager.Modules.TryEquip(0, 1, ShipModule.IonBooster);
    Check(BaseStats.GetHarvesterCapacity(ship) == capacity && Near(BaseStats.GetHarvesterSpeed(ship), speed),
      "Equipping stat modules preserves the active trip");
    ship.BeginModuleTrip();
    Check(BaseStats.GetHarvesterCapacity(ship) == (int)Math.Ceiling(capacity * 1.5)
      && Near(BaseStats.GetHarvesterSpeed(ship), speed * 1.25f), "Cargo and propulsion bonuses combine");
    manager.Modules.TryEquip(0, 0, ShipModule.WidebandArray);
    manager.Modules.TryEquip(0, 1, ShipModule.FuelRecycler);
    ship.BeginModuleTrip();
    Check(Near(BaseStats.GetHarvesterCollectionRange(ship), range * 1.5f)
      && Near(BaseStats.GetHarvesterFuelEfficiency(ship), efficiency * 2f), "Range and fuel modules apply advertised multipliers");
    manager.Modules.TryEquip(0, 1, ShipModule.FinalSweep);
    ship.BeginModuleTrip();
    ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
    Check(ship.TryBeginFinalSweep(Vector2.Zero) && Near(ship.FinalSweepRadius, range * 4.5f),
      "Wideband amplifies Final Sweep");
    manager.Modules.TryEquip(0, 0, ShipModule.CargoPod);
    manager.UG.HarvesterCapacity = int.MaxValue;
    ship.BeginModuleTrip();
    Check(BaseStats.GetHarvesterCapacity(ship) == int.MaxValue, "Expanded cargo saturates instead of overflowing");

    // Exercise every new enum value through the current save format and inventory ownership rules.
    string path = Path.Combine(Path.GetTempPath(), "expanded-modules-" + Guid.NewGuid() + ".json");
    try
    {
      foreach (var module in Enum.GetValues<ShipModule>().Skip(7))
      {
        var inventory = new ShipyardModules();
        ModuleChecks.GrantAll(inventory);
        Check(inventory.TryEquip(4, 1, module) && !inventory.TryEquip(1, 0, module), "New modules remain unique");
        var store = new GameSaveStore(path);
        Check(store.Save(new GameSave { Modules = inventory }), "Save new module");
        var restored = store.Load();
        Check(restored != null && restored.Modules.Has(Harvester.HarvesterType.PerimeterHarvester, module)
          && restored.Modules.GetAvailableModules().Count() == count - 2, "Reload new equipment without duplicating it");
      }
    }
    finally { File.Delete(path); }
  }

  private static void CheckPickupEffects()
  {
    using var scene = new Scene(ShipModule.OverdriveCoil, ShipModule.EchoChamber);
    float speed = BaseStats.GetHarvesterSpeed(scene.Ship);
    var gems = Enumerable.Range(0, 5).Select(i => scene.AddGem(new Vector2(500 + i, 500))).ToArray();
    scene.Prepare();
    scene.Fleet.CollectGem(gems[0], scene.Ship);
    Check(Near(BaseStats.GetHarvesterSpeed(scene.Ship), speed * 1.75f), "A pickup activates overdrive");
    scene.Ship.AdvanceDroneTimers(2f);
    scene.Fleet.CollectGem(gems[1], scene.Ship);
    Check(Near(scene.Ship.OverdriveTimeRemaining, 3f), "Further pickups refresh overdrive");
    foreach (var gem in gems.Skip(2)) scene.Fleet.CollectGem(gem, scene.Ship);
    Check(scene.Ship.CarryingGemCount == 5 && scene.Ship.CarryingGemBaseValue == 70,
      "Fifth pickup adds two echoes without taking cargo space");
    scene.Ship.AdvanceDroneTimers(3.1f);
    Check(Near(BaseStats.GetHarvesterSpeed(scene.Ship), speed), "Overdrive expires");
    scene.Ship.BeginModuleTrip();
    for (int i = 0; i < 4; i++) scene.Ship.PickedUpGem(new Gem { BaseValue = 1 });
    Check(scene.Ship.CarryingGemBaseValue == 74, "Echo counter resets each trip");
    scene.Ship.CarryingGemBaseValue = ulong.MaxValue - 1;
    scene.Ship.PickedUpGem(new Gem { BaseValue = uint.MaxValue });
    Check(scene.Ship.CarryingGemBaseValue == ulong.MaxValue, "Echo bonus safely saturates cargo value");

    scene.Manager.Modules.TryEquip(0, 0, ShipModule.CrystalRefinery);
    scene.Ship.BeginModuleTrip();
    Check(BaseStats.GetHarvesterDeliveryValue(scene.Ship, 100) == 150
      && BaseStats.GetHarvesterDeliveryValue(scene.Ship, ulong.MaxValue) == ulong.MaxValue,
      "Refinery increases delivery value without overflow");
    scene.Ship.CarryingGemBaseValue = 100;
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 150, "Refined cargo pays out through actual delivery");
    Check(scene.Ship.OverdriveTimeRemaining == 0f, "Delivery clears pickup buffs for the new trip");
  }

  private static void CheckSingularity()
  {
    using var scene = new Scene(ShipModule.SingularityEngine);
    var direct = Enumerable.Range(0, 8).Select(i => scene.AddGem(new Vector2(500, 500))).ToArray();
    var nearby = Enumerable.Range(0, 20).Select(i => scene.AddGem(new Vector2(600 + i, 500))).ToArray();
    var outside = scene.AddGem(new Vector2(800, 500));
    var reserved = scene.AddGem(new Vector2(500, 510));
    scene.Prepare();
    scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
    foreach (var gem in direct.Take(7)) scene.Fleet.CollectGem(gem, scene.Ship);
    Check(!nearby.Any(gem => gem.PickedUp), "Singularity waits for eight direct pickups");
    scene.Fleet.CollectGem(direct[7], scene.Ship);
    Check(nearby.Count(gem => gem.PickedUp) == 8 && !outside.PickedUp && !reserved.PickedUp,
      "Singularity is capped at eight nearby, unreserved gems");
    Check(scene.Ship.CarryingGemCount == 16 && scene.Ship.SingularityPickups == 0,
      "Bonus pulls exceed capacity without charging another singularity");
    Check(scene.Ship.ModulePulseRemaining > 0, "Singularity provides visual feedback");
  }

  private static void CheckSupernovaAndWarp()
  {
    using var scene = new Scene(ShipModule.SupernovaCore, ShipModule.PhaseAnchor);
    var nearby = Enumerable.Range(0, 40).Select(i => scene.AddGem(new Vector2(600 + i, 500))).ToArray();
    var outside = scene.AddGem(new Vector2(800, 500));
    scene.Prepare();
    scene.Invoke("ClaimSupernova", scene.Ship, scene.Transform.Position);
    Check(scene.Ship.ClaimedGems.Count == 0, "Supernova waits for full cargo");
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    scene.Ship.CarryingGemBaseValue = 100;
    scene.Invoke("ClaimSupernova", scene.Ship, scene.Transform.Position);
    scene.Invoke("ResolveClaimedGems", scene.Ship);
    Check(nearby.Count(gem => gem.PickedUp) == 32 && !outside.PickedUp,
      "Supernova collects at most 32 gems inside its radius");
    scene.Invoke("ClaimSupernova", scene.Ship, scene.Transform.Position);
    Check(scene.Ship.ClaimedGems.Count == 0, "Supernova fires once per trip");
    UntitledGemGameGameScreen.HomeBasePos = new Vector2(50, 50);
    Check((bool)scene.Invoke("TryActivateReturnGate", scene.Ship, scene.Transform)
      && scene.Transform.Position == UntitledGemGameGameScreen.HomeBasePos,
      "Phase Anchor guarantees home warp without the Return Gate upgrade");
    Check(scene.Ship.WarpDriveFlashTimeRemaining > 0f, "Phase Anchor displays a warp flash");
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 420,
      "Warp delivery includes supernova cargo");
    Check(!scene.Ship.TryBeginSupernova(), "A fresh empty trip cannot detonate");
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    Check(scene.Ship.TryBeginSupernova(), "Next full trip rearms supernova");

    scene.Manager.Modules.TryEquip(0, 0, ShipModule.ReturnBeacon);
    scene.Ship.BeginModuleTrip();
    scene.Transform.Position = new Vector2(700, 400);
    scene.Ship.SetCollisionPosition(scene.Transform.Position);
    scene.Ship.CollectionEndpoint = scene.Transform.Position;
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    scene.Ship.CarryingGemBaseValue = 10;
    Check((bool)scene.Invoke("TryActivateReturnGate", scene.Ship, scene.Transform), "Anchor activates again on next trip");
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(scene.Transform.Position == new Vector2(700, 400) && scene.Ship.CarryingGemCount == 0,
      "Phase Anchor and Return Beacon form a complete delivery round trip");
    scene.Manager.Modules.TryEquip(0, 1, ShipModule.None);
    scene.Ship.BeginModuleTrip();
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    Check(!(bool)scene.Invoke("TryActivateReturnGate", scene.Ship, scene.Transform), "Unequipped anchor no longer warps");
  }
}
