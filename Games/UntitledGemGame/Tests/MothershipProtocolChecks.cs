using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;
using Scene = ExpandedModuleChecks.Scene;

// Mothership Protocol: shuttles feed a carrier's hold and only the carrier delivers.
internal static class MothershipProtocolChecks
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(0.1));

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    var previousManager = UpgradeManager.Instance;
    var home = UntitledGemGameGameScreen.HomeBasePos;
    ulong delivered = UntitledGemGameGameScreen.DeliveredUncounted;
    try
    {
      UntitledGemGameGameScreen.HomeBasePos = new Vector2(1500, 1500);
      CheckRules();
      CheckHold();
      CheckDelivery();
      CheckRecall();
      CheckDocking();
      CheckGemReach();
      CheckForaging();
      CheckStation();
      CheckCollectorSwarm();
      CheckModuleParity();
      Console.WriteLine("Mothership Protocol passed: fleet roles, shared hold, single delivery, recall, docking, fuel, foraging, station keeping and collector value.");
    }
    finally
    {
      UpgradeManager.Instance = previousManager;
      UntitledGemGameGameScreen.HomeBasePos = home;
      UntitledGemGameGameScreen.DeliveredUncounted = delivered;
    }
  }

  // Movement sizes ships by their sprite region; a blank one needs no graphics device.
  private static Sprite BlankHull()
  {
    var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
    typeof(Sprite).GetField("_textureRegion", Private)!
      .SetValue(sprite, RuntimeHelpers.GetUninitializedObject(typeof(Texture2DRegion)));
    return sprite;
  }

  // A scene whose ship is the carrier, with a play area for the movement code.
  private static Scene Carrier(int shuttles, ShipModule first = ShipModule.None, ShipModule second = ShipModule.None)
  {
    var scene = new Scene(first, second);
    scene.Manager.UGM.MothershipProtocol = true;
    scene.Ship.IsCarrier = true;
    scene.Ship.ShuttleCount = shuttles;
    scene.Ship.m_sprite = BlankHull();
    scene.Ship.BeginModuleTrip();
    typeof(HarvesterCollectionSystem).GetField("_playArea", Private)!
      .SetValue(scene.Fleet, new PlayAreaBounds(Vector2.Zero, new Vector2(3000, 3000)));
    return scene;
  }

  private static Harvester AddShuttle(Scene scene, Vector2 position, Harvester.HarvesterType type = Harvester.HarvesterType.Harvester)
  {
    var entity = scene.World.CreateEntity();
    entity.Attach(new Transform2(position));
    var ship = new Harvester { Entity = entity, Id = entity.Id, Type = type, m_sprite = BlankHull() };
    ship.SetCollisionPosition(position);
    ship.JoinCarrier(scene.Ship);
    entity.Attach(ship);
    scene.World.Update(new GameTime());
    return ship;
  }

  private static void Update(Scene scene, Harvester ship)
  {
    var ids = scene.Fleet._harvesters;
    for (int i = 0; i < ids.Count; i++)
      if (ids[i] == ship.Id) scene.Invoke("UpdateHarvesters", i, Frame);
  }

  private static Vector2 Position(Harvester ship) => ship.Entity.Get<Transform2>().Position;

  private static void CheckRules()
  {
    var manager = new UpgradeManager();
    Check(PrestigeTalentEffects.FleetCount(5) == 5 && PrestigeTalentEffects.CatapultQueueLimit == 40
      && PrestigeTalentEffects.CatapultFireInterval == 0.25f, "Without the talent ships and the catapult stay as they were");
    manager.UGM.MothershipProtocol = true;
    Check(PrestigeTalentEffects.FleetCount(5) == 15, "Each purchased ship becomes three shuttles");
    Check(PrestigeTalentEffects.CatapultQueueLimit == 400 && PrestigeTalentEffects.CatapultFireInterval == 0.1f,
      "The catapult keeps up with whole-hold deliveries");
    Check(PrestigeTalentLayout.ExclusiveWith("MSP1").SequenceEqual(["LOP1"])
      && PrestigeTalentLayout.ExclusiveWith("LOP1").SequenceEqual(["MSP1"]),
      "Lone Operator rules out combining a fleet it docks");
    var plain = new Harvester { Type = Harvester.HarvesterType.Harvester };
    Check(plain.HullScale == 1f && plain.RoleSpeedMultiplier == 1f && plain.RoleCapacityMultiplier == 1.0
      && !plain.IsShuttle && plain.DockPosition == UntitledGemGameGameScreen.HomeBasePos,
      "Ships outside the protocol keep their size, speed, hold and home");
  }

  private static void CheckHold()
  {
    using var scene = Carrier(6, ShipModule.CargoPod);
    var plain = new Harvester { Type = Harvester.HarvesterType.Harvester };
    plain.BeginModuleTrip();
    int capacity = BaseStats.GetHarvesterCapacity(plain);
    var shuttle = AddShuttle(scene, new Vector2(600, 500));
    shuttle.BeginModuleTrip();
    Check(capacity == 15 && BaseStats.GetHarvesterCapacity(scene.Ship) == capacity * 6
      && BaseStats.GetHarvesterCapacity(shuttle) == capacity / 3,
      "A carrier holds a ship's load per shuttle, a shuttle a third of one, and cargo modules grow both");
    float speed = BaseStats.GetHarvesterSpeed(plain);
    Check(MathF.Abs(BaseStats.GetHarvesterSpeed(scene.Ship) - speed * 0.5f) < 0.001f
      && MathF.Abs(BaseStats.GetHarvesterSpeed(shuttle) - speed) < 0.001f,
      "Carriers are heavy and shuttles fly like the ships they came from");
    Check(scene.Ship.HullScale > 1f && shuttle.HullScale < 1f, "Carriers are drawn large and shuttles small");
  }

  private static void CheckDelivery()
  {
    using var scene = Carrier(2);
    var carrier = scene.Ship;
    var first = AddShuttle(scene, new Vector2(520, 500));
    var second = AddShuttle(scene, new Vector2(480, 500));
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    foreach (var (shuttle, value) in new[] { (first, 40UL), (second, 60UL) })
    {
      shuttle.CarryingGemCount = 4;
      shuttle.CarryingGemBaseValue = value;
      shuttle.ReachedHome = true;
      scene.Invoke("DeliverCargo", shuttle);
      Check(shuttle.CarryingGemCount == 0 && shuttle.CarryingGemBaseValue == 0 && !shuttle.ReachedHome,
        "Unloading empties the shuttle for its next hop");
    }
    Check(UntitledGemGameGameScreen.DeliveredUncounted == before && carrier.CarryingGemCount == 8
      && carrier.CarryingGemBaseValue == 100, "Shuttles fill the hold and pay nothing themselves");
    scene.Invoke("DeliverCargo", carrier);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 100 && carrier.CarryingGemCount == 0,
      "The carrier delivers the whole hold at once");
    Check(carrier.IsCarrier && carrier.DepartingHomeBase, "A carrier leaves home like any other ship");
    carrier.CarryingGemCount = uint.MaxValue;
    carrier.AddToHold(5, 0, 0);
    Check(carrier.CarryingGemCount == uint.MaxValue, "A hold saturates rather than wrapping");
  }

  private static void CheckRecall()
  {
    using var scene = Carrier(2);
    var carrier = scene.Ship;
    var loaded = AddShuttle(scene, new Vector2(700, 500));
    var empty = AddShuttle(scene, new Vector2(700, 600));
    loaded.CarryingGemCount = 1;
    scene.AddGem(Position(empty));
    scene.Prepare();
    Check(!carrier.ReturningToHomebase && !loaded.ReturningToHomebase && !empty.ShuttleRecalled,
      "Shuttles work while the hold has room");
    Update(scene, empty);
    Check(empty.ClaimedGems.Count == 1, "A working shuttle scoops the gem under it");
    scene.Invoke("ResolveClaimedGems", empty);
    empty.CarryingGemCount = 0;

    carrier.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(carrier);
    Check(carrier.ReturningToHomebase && loaded.ReturningToHomebase && !empty.ReturningToHomebase && empty.ShuttleRecalled,
      "A full hold calls loaded shuttles aboard and empty ones to escort");
    scene.AddGem(Position(empty));
    scene.Prepare();
    Update(scene, empty);
    Check(empty.ClaimedGems.Count == 0, "Escorting shuttles leave gems alone");

    carrier.CarryingGemCount = 1;
    Check(!carrier.ReturningToHomebase, "A part-filled carrier stays out");
    carrier.AdvanceDroneTimers(PrestigeTalentEffects.CarrierMaxTripSeconds);
    Check(carrier.ReturningToHomebase, "A long trip sends the carrier home with what it has");
    carrier.CarryingGemCount = 0;
    Check(!carrier.ReturningToHomebase, "An empty carrier has no reason to come home");
  }

  private static void CheckDocking()
  {
    using var scene = Carrier(1, ShipModule.PhaseAnchor);
    var carrier = scene.Ship;
    var home = UntitledGemGameGameScreen.HomeBasePos;
    var shuttle = AddShuttle(scene, home);
    shuttle.BeginModuleTrip();
    shuttle.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(shuttle);
    shuttle.Fuel = 0f;
    Update(scene, shuttle);
    Check(!shuttle.ReachedHome && Position(shuttle) != home, "A shuttle flying past home docks only with its carrier");
    Check(shuttle.Fuel == 0f && shuttle.CurrentState == Harvester.HarvesterState.Collecting,
      "Shuttles fly without fuel");
    var transform = shuttle.Entity.Get<Transform2>();
    var before = transform.Position;
    Check(!(bool)scene.Invoke("TryActivateReturnGate", shuttle, transform) && transform.Position == before,
      "Return gates and Phase Anchor never pull a shuttle to the home base");
    transform.Position = Position(carrier) + new Vector2(10, 0);
    shuttle.SetCollisionPosition(transform.Position);
    Update(scene, shuttle);
    Check(shuttle.ReachedHome, "A shuttle docks on reaching its carrier");

    float fuel = carrier.Fuel;
    carrier.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(carrier);
    Update(scene, carrier);
    Check(carrier.Fuel < fuel, "The carrier burns fuel for the whole group");
    Check(!carrier.ReachedHome, "The carrier has to fly home itself");
    carrier.Entity.Get<Transform2>().Position = home;
    carrier.SetCollisionPosition(home);
    Update(scene, carrier);
    Check(carrier.ReachedHome, "A carrier docks at the home base");
  }

  private static void CheckGemReach()
  {
    using var scene = Carrier(1);
    scene.AddGem(Position(scene.Ship));
    scene.Prepare();
    Update(scene, scene.Ship);
    Check(scene.Ship.ClaimedGems.Count == 0, "Carriers leave gathering to their shuttles");
  }

  private static void CheckForaging()
  {
    using (var scene = Carrier(3))
    {
      var center = Position(scene.Ship);
      var near = scene.AddGem(center + new Vector2(100, 0));
      var farther = scene.AddGem(center + new Vector2(150, 0));
      scene.AddGem(center + new Vector2(scene.Ship.ShuttleLeash + 150, 0));
      scene.Prepare();
      var bounds = new PlayAreaBounds(Vector2.Zero, new Vector2(3000, 3000));
      var ships = Enumerable.Range(0, 3).Select(_ => AddShuttle(scene, center)).ToArray();
      var targets = ships.Select(ship => (Vector2)scene.Invoke("PickShuttleTarget", ship, center, bounds)).ToArray();
      Check(ships[0].TargetGemGridIndex == near.GridIndex && targets[0] == center + new Vector2(100, 0),
        "A shuttle takes a gem close by first");
      Check(ships[1].TargetGemGridIndex == farther.GridIndex, "Shuttles never chase the same gem");
      Check(ships[2].TargetGemGridIndex == -1 && Vector2.Distance(targets[2], center) <= scene.Ship.ShuttleLeash,
        "Gems beyond the leash are out of reach, and an idle shuttle stays near its carrier");
      scene.Invoke("TransferToCarrier", ships[0]);
      Check(ships[0].TargetGemGridIndex == -1, "Docking gives up the gem a shuttle was chasing");
      scene.Invoke("PickShuttleTarget", ships[2], center, bounds);
      Check(ships[2].TargetGemGridIndex == near.GridIndex, "A released gem is free for the next shuttle");
    }
    using (var scene = Carrier(1, ShipModule.ProspectorLens))
    {
      var center = Position(scene.Ship);
      scene.AddGem(center + new Vector2(20, 0), 10);
      var rich = scene.AddGem(center + new Vector2(35, 0), 500);
      scene.Prepare();
      var shuttle = AddShuttle(scene, center);
      shuttle.BeginModuleTrip();
      scene.Invoke("PickShuttleTarget", shuttle, center, new PlayAreaBounds(Vector2.Zero, new Vector2(3000, 3000)));
      Check(shuttle.TargetGemGridIndex == rich.GridIndex, "Prospector Lens shuttles take the richest gem nearby");
    }
    using (var scene = Carrier(1))
    {
      var center = Position(scene.Ship);
      var bounds = new PlayAreaBounds(Vector2.Zero, new Vector2(3000, 3000));
      var shuttle = AddShuttle(scene, center + new Vector2(50, 0));
      shuttle.CarryingGemCount = 1;
      scene.Prepare();
      scene.Invoke("PickShuttleTarget", shuttle, center, bounds);
      Check(shuttle.ReturningToHomebase, "A part-filled shuttle with nothing left to gather brings in what it has");
      scene.Invoke("DeliverCargo", shuttle);
      Check(!shuttle.ReturningToHomebase && scene.Ship.CarryingGemCount == 1, "Unloading ends with the shuttle free to work");
      scene.Invoke("PickShuttleTarget", shuttle, center, bounds);
      Check(!shuttle.ReturningToHomebase, "An empty shuttle with nothing to do just waits");

      float leash = scene.Ship.ShuttleLeash;
      Check(leash == PrestigeTalentEffects.MinShuttleLeash, "A slow group keeps the minimum leash");
      scene.Manager.UG.HarvesterSpeed = 100f;
      Check(scene.Ship.ShuttleLeash > leash && shuttle.ShuttleLeash == scene.Ship.ShuttleLeash,
        "Speed upgrades widen the swarm as the field grows");
    }
  }

  private static void CheckStation()
  {
    using var scene = Carrier(1);
    var carrier = scene.Ship;
    var center = Position(carrier);
    Check(!(bool)scene.Invoke("HoldingStation", carrier, center, 0.1f), "A carrier with nothing in reach moves on");
    var gem = scene.AddGem(center + new Vector2(120, 0));
    scene.Prepare();
    carrier.TargetScreenPosition = center;
    Update(scene, carrier);
    Check(Position(carrier) == center && carrier.TargetScreenPosition == center,
      "A carrier that arrives over gems holds station");
    Check((bool)scene.Invoke("HoldingStation", carrier, center, PrestigeTalentEffects.CarrierMaxStationSeconds - 1f),
      "It stays while shuttles have gems in reach");
    Check(!(bool)scene.Invoke("HoldingStation", carrier, center, 1f) && carrier.StationSeconds == 0f,
      "A stream of gems cannot hold it forever");
    carrier.ShuttleCount = 2;
    Check(!(bool)scene.Invoke("HoldingStation", carrier, center, 0.1f), "A trickle too thin for the swarm is not worth staying for");
    carrier.ShuttleCount = 1;

    // Its own target must not be pulled away by the shuttles collecting it.
    carrier.CollectionStrategy = HarvesterStrategy.RandomGemPosition;
    carrier.TargetGemGridIndex = gem.GridIndex;
    carrier.TargetGemEntityId = gem.Id;
    carrier.TargetScreenPosition = center + new Vector2(120, 0);
    scene.Fleet.flatSpatialHash.TryClaim(gem.GridIndex);
    Update(scene, carrier);
    Check(carrier.TargetScreenPosition == center + new Vector2(120, 0) && Position(carrier) != center,
      "A carrier flies on to where it was heading when the gem there is taken");
  }

  // Three shuttles stand in for one ship, so per gem and per distance modules stay as strong.
  private static void CheckModuleParity()
  {
    using (var scene = Carrier(1, ShipModule.FinalSweep))
    {
      var shuttle = AddShuttle(scene, new Vector2(600, 500));
      shuttle.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(shuttle);
      Check(shuttle.TryBeginFinalSweep(Vector2.Zero), "A shuttle's first full load sweeps");
      shuttle.FinishFinalSweep();
      shuttle.ReachedHome = true;
      scene.Invoke("DeliverCargo", shuttle);
      shuttle.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(shuttle);
      Check(!shuttle.TryBeginFinalSweep(Vector2.Zero), "Unloading is not a new trip, so small loads cannot sweep more often");
      scene.Invoke("DeliverCargo", shuttle);
      scene.Ship.CarryingGemCount = 1;
      scene.Ship.CollectionEndpoint = new Vector2(1500, 1500);
      scene.Invoke("DeliverCargo", scene.Ship);
      shuttle.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(shuttle);
      Check(shuttle.TryBeginFinalSweep(Vector2.Zero), "A carrier delivery starts every shuttle's next trip");
    }
    using (var scene = Carrier(1, ShipModule.ArcEmitter, ShipModule.WorldEater))
    {
      var shuttle = AddShuttle(scene, new Vector2(600, 500));
      shuttle.BeginModuleTrip();
      shuttle.ChargeTravelModules(90f);
      scene.Ship.ChargeTravelModules(90f);
      Check(shuttle.ArcTravelCharge == 30f && scene.Ship.ArcTravelCharge == 90f,
        "A shuttle's travel charges modules at a third of a ship's rate");
      Check(scene.Ship.HasModule(ShipModule.WorldEater) && !shuttle.HasModule(ShipModule.WorldEater)
        && shuttle.ModuleReturnCapacity == BaseStats.GetHarvesterCapacity(shuttle),
        "The group's one black hole is on the carrier");
      shuttle.LeaveCarrier();
      Check(shuttle.HasModule(ShipModule.WorldEater) && !shuttle.IsShuttle, "A ship back on its own regains its modules");
    }
  }

  private static void CheckCollectorSwarm()
  {
    using var scene = Carrier(48);
    for (int i = 0; i < 48; i++) AddShuttle(scene, new Vector2(600, 600));
    Check(scene.Fleet.CollectorSwarmValueMultiplier() == 2f,
      "Collector value counts purchased ships: three shuttles are one ship, a carrier none");
    float plainSpeed = BaseStats.GetHarvesterSpeed(new Harvester { Type = Harvester.HarvesterType.Harvester });
    scene.Fleet.GetCollectorSwarmStats(out float speed, out _);
    Check(MathF.Abs(speed - plainSpeed) < 0.001f, "Collectors match a ship's speed, not a carrier's");
  }
}
