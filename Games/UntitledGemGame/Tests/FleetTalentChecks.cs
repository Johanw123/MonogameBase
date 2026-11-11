using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class FleetTalentChecks
{
  // A focused entry point also checks the real catalog, tier gates and save format.
  public static void CheckTree()
  {
    var previous = UpgradeManager.CurrentUpgrades;
    try
    {
      var upgrades = UpgradeManager.CurrentUpgrades = new Upgrades();
      foreach (var (suffix, buttons, definitions) in new[]
      {
        ("", upgrades.UpgradeButtons, upgrades.UpgradeDefinitions),
        ("_abilities", upgrades.UpgradeButtonsAbilities, upgrades.UpgradeDefinitionsAbilities),
        ("_meta", upgrades.UpgradeButtonsMeta, upgrades.UpgradeDefinitionsMeta)
      })
        upgrades.LoadJson(File.ReadAllText($"Content/Data/upgrades{suffix}.json"),
          File.ReadAllText($"Content/Data/upgrades{suffix}_buttons.json"), buttons, definitions);
      foreach (var (buttons, joints) in new[]
      {
        (upgrades.UpgradeButtons, upgrades.UpgradeJoints),
        (upgrades.UpgradeButtonsAbilities, upgrades.UpgradeJointsAbilities),
        (upgrades.UpgradeButtonsMeta, upgrades.UpgradeJointsMeta)
      })
        foreach (var (id, button) in buttons)
          if (!string.IsNullOrEmpty(button.Data.BlockedBy) && buttons.TryGetValue(button.Data.BlockedBy, out var parent))
            joints.Add(id, new UpgradeJoint { StartButton = parent, EndButton = button });
      PrestigeTalentChecks.Run();
      DebugProgressionChecks.Run(upgrades);
      LocalizationChecks.Run();
    }
    finally { UpgradeManager.CurrentUpgrades = previous; }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    var previous = UpgradeManager.Instance;
    ulong delivered = UntitledGemGameGameScreen.DeliveredUncounted;
    var collected = UntitledGemGameGameScreen.Collected;
    try
    {
      CheckTowing();
      CheckTailReservations();
      CheckShuttleTransfer();
      CheckDropPods();
      Console.WriteLine("Fleet talents passed: tow limits, swept pickups, reservations, bonuses, single delivery, shuttle transfer, prestige reset and one Drop Pod per trip.");
    }
    finally
    {
      UpgradeManager.Instance = previous;
      UntitledGemGameGameScreen.DeliveredUncounted = delivered;
      UntitledGemGameGameScreen.Collected = collected;
    }
  }

  private static void FillHold(Scene scene)
  {
    scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
    scene.Ship.CarryingGemBaseValue = 100;
  }

  private static void Tow(Scene scene, Harvester ship = null)
  {
    ship ??= scene.Ship;
    scene.Invoke("ClaimTractorTail", ship, new Vector2(500, 500), new Vector2(600, 500));
    scene.Invoke("ResolveTractorTail", ship, 0.1f);
  }

  private static void CheckTowing()
  {
    using var scene = new Scene(ShipModule.None);
    FillHold(scene);
    var gems = Enumerable.Range(0, 8).Select(i => scene.AddGem(new Vector2(540 + i, 500))).ToArray();
    var far = scene.AddGem(new Vector2(550, 900));
    scene.Prepare();
    Tow(scene);
    Check(scene.Ship.TowedGemCount == 0 && gems.All(g => !g.PickedUp), "Towing requires the talent");
    scene.Manager.UGM.TractorTails = true;
    uint hold = scene.Ship.CarryingGemCount;
    Tow(scene);
    Check(scene.Ship.TowedGemCount == 3 && scene.Ship.CarryingGemCount == hold
      && scene.Ship.CarryingGemBaseValue == 130 && gems.Count(g => g.PickedUp) == 3 && !far.PickedUp,
      "A full Drifter tows three nearby gems along its return path without filling more hold space");
    var picked = gems.First(g => g.PickedUp);
    Check(picked.ShouldDestroy && !scene.Fleet.flatSpatialHash.IsAvailable(picked.GridIndex)
      && scene.Ship.TailGems.Count == 3, "Tethering retires the loose gem and keeps a visual snapshot");
    Tow(scene);
    scene.Fleet.CollectGem(picked, scene.Ship);
    Check(scene.Ship.TowedGemCount == 3 && scene.Ship.CarryingGemBaseValue == 130,
      "A full tail and a repeated pickup cannot duplicate value");

    scene.Manager.UGM.AllHarvesterValueMultiplier = 2f;
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 260
      && scene.Ship.TowedGemCount == 0 && scene.Ship.TailGems.Count == 0 && scene.Ship.CarryingGemCount == 0,
      "Delivery pays hold and tow value with the same fleet bonus and clears the entire load");
    var meter = scene.Manager.Collection;
    Check(Math.Abs(meter.ThisRun(GemCollectionMethod.TractorTails) - 60) < 1e-6
      && Math.Abs(meter.ThisRun(GemCollectionSource.Drifter) - 200) < 1e-6 && meter.TotalThisRun == 260
      && scene.Ship.Credits.Count == 0,
      "The Collection panel credits the towed share of a delivery to Tractor Tails, the rest to the Drifter");
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 260
      && Math.Abs(meter.ThisRun(GemCollectionMethod.TractorTails) - 60) < 1e-6, "A delivered tail never pays twice");

    scene.Manager.UG.HarvesterCapacity = 41;
    Check(PrestigeTalentEffects.TractorTailCapacity(scene.Ship) == 11, "Tow allowance scales with cargo and rounds up");
    scene.Manager.UG.HarvesterCapacity = int.MaxValue;
    Check(PrestigeTalentEffects.TractorTailCapacity(scene.Ship) == 536870912, "Large tow capacities do not overflow");
    Check(PrestigeTalentEffects.TractorTailCapacity(new Harvester { Type = Harvester.HarvesterType.Drone }) == 0
      && PrestigeTalentEffects.TractorTailCapacity(new Harvester { Type = Harvester.HarvesterType.HomeBase }) == 0,
      "Drones and home base do not acquire tow capacity");
  }

  private static void CheckTailReservations()
  {
    using var scene = new Scene(ShipModule.GemPolisher);
    scene.Manager.UGM.TractorTails = true;
    scene.Manager.UG.HarvesterCapacity = 20;
    var reserved = scene.AddGem(new Vector2(550, 500));
    var gems = Enumerable.Range(0, 8).Select(i => scene.AddGem(new Vector2(550 + i, 500))).ToArray();
    scene.Prepare();
    scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
    Tow(scene);
    Check(gems.All(g => !g.PickedUp), "Outbound ships cannot fill their tails");
    FillHold(scene);
    scene.Ship.CurrentState = Harvester.HarvesterState.Refueling;
    Tow(scene);
    Check(gems.All(g => !g.PickedUp), "Refueling ships cannot gather a tail");
    scene.Ship.CurrentState = Harvester.HarvesterState.Collecting;
    Tow(scene);
    Check(!reserved.PickedUp && scene.Ship.TowedGemCount == 5 && scene.Ship.CarryingGemBaseValue == 165,
      "Tails respect other reservations and run the ordinary Gem Polisher pickup bonus");
    scene.Ship.ClearCargoForPrestige();
    Check(scene.Ship.TowedGemCount == 0 && scene.Ship.TailGems.Count == 0
      && scene.Ship.CarryingGemBaseValue == 0,
      "Extraction clears the tail");

    FillHold(scene);
    scene.Invoke("ClaimTractorTail", scene.Ship, new Vector2(500, 500), new Vector2(600, 500));
    scene.Ship.MarkedForDestroy = true;
    scene.Invoke("ResolveTractorTail", scene.Ship, 0.1f);
    Check(gems.Where(g => !g.PickedUp).All(g => scene.Fleet.flatSpatialHash.IsAvailable(g.GridIndex)),
      "Retiring ships release unfulfilled tail reservations");
  }

  private static void CheckShuttleTransfer()
  {
    using var scene = new Scene(ShipModule.None);
    scene.Manager.UGM.TractorTails = true;
    var carrier = AddShip(scene, new Vector2(650, 500));
    carrier.IsCarrier = true;
    carrier.ShuttleCount = 3;
    scene.Ship.JoinCarrier(carrier);
    FillHold(scene);
    uint hold = scene.Ship.CarryingGemCount;
    for (int i = 0; i < 3; i++) scene.AddGem(new Vector2(550 + i, 500));
    scene.Prepare();
    Tow(scene);
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(carrier.CarryingGemCount == hold + 3 && carrier.CarryingGemBaseValue == 130
      && scene.Ship.TowedGemCount == 0 && UntitledGemGameGameScreen.DeliveredUncounted == before,
      "Shuttles transfer tow counts and value to the carrier without paying out early");
    Check(carrier.Credits[GemCollectionMethod.TractorTails] == 30 && scene.Ship.Credits.Count == 0,
      "A shuttle's towed value reaches the carrier still credited to Tractor Tails");
    var tail = scene.Ship.DeliveredTails.Single();
    var last = tail.Gems[^1].Position;
    var flight = new Vector2(300, 40);
    carrier.SetCollisionPosition(carrier.BoundingCircle.Center + flight);
    scene.Invoke("ResolveTractorTail", scene.Ship, 0f);
    Check(tail.Receiver == carrier && tail.Anchor == carrier.BoundingCircle.Center
      && Vector2.Distance(tail.Gems[^1].Position, last + flight) < 0.01f,
      "A shuttle's handed-over tail reels in toward its carrier as the carrier flies on");
    scene.Invoke("DeliverCargo", carrier);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 130
      && Math.Abs(scene.Manager.Collection.ThisRun(GemCollectionMethod.TractorTails) - 30) < 1e-6,
      "The carrier delivers shuttle tails once through the usual payout");
  }

  private static void CheckDropPods()
  {
    using var scene = new Scene(ShipModule.None);
    scene.Prepare();
    FillHold(scene);
    uint hold = scene.Ship.CarryingGemCount;
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.CarryingGemCount == hold && UntitledGemGameGameScreen.DeliveredUncounted == before,
      "Drop Pods require the talent");
    scene.Manager.UGM.DropPods = true;
    ulong pays = BaseStats.GetHarvesterDeliveryValue(scene.Ship, 100);
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.CarryingGemCount == 0 && scene.Ship.CarryingGemBaseValue == 0
      && !scene.Ship.ReturningToHomebase && scene.Ship.DropPods.Count == 1
      && UntitledGemGameGameScreen.DeliveredUncounted - before == pays,
      "A full hold is paid out as a pod and the ship keeps collecting");
    FillHold(scene);
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.CarryingGemCount == hold && scene.Ship.ReturningToHomebase
      && UntitledGemGameGameScreen.DeliveredUncounted - before == pays,
      "The second full hold of a trip flies home");
    scene.Ship.CarryingGemCount = hold - 1;
    scene.Ship.BeginModuleTrip();
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.CarryingGemCount == hold - 1, "A hold that is not full launches nothing");
    FillHold(scene);
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.CarryingGemCount == 0 && UntitledGemGameGameScreen.DeliveredUncounted - before == pays * 2,
      "A new trip launches a new pod");
    for (int i = 0; i < 40 && scene.Ship.DropPods.Count > 0; i++)
      scene.Invoke("ResolveTractorTail", scene.Ship, 0.1f);
    Check(scene.Ship.DropPods.Count == 0, "Pods fly home and disappear");

    var drone = AddShip(scene, new Vector2(520, 500), Harvester.HarvesterType.Drone);
    drone.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(drone);
    var carrier = AddShip(scene, new Vector2(650, 500));
    carrier.IsCarrier = true;
    carrier.ShuttleCount = 3;
    var shuttle = AddShip(scene, new Vector2(560, 500));
    shuttle.JoinCarrier(carrier);
    shuttle.CarryingGemCount = (uint)shuttle.ModuleReturnCapacity;
    ulong paid = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("LaunchDropPod", drone);
    scene.Invoke("LaunchDropPod", shuttle);
    Check(drone.CarryingGemCount > 0 && shuttle.CarryingGemCount > 0
      && UntitledGemGameGameScreen.DeliveredUncounted == paid, "Drones and shuttles do not launch pods");

    scene.Ship.BeginModuleTrip();
    FillHold(scene);
    scene.Invoke("LaunchDropPod", scene.Ship);
    scene.Ship.ClearCargoForPrestige();
    FillHold(scene);
    paid = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Invoke("LaunchDropPod", scene.Ship);
    Check(scene.Ship.DropPods.Count == 1 && UntitledGemGameGameScreen.DeliveredUncounted > paid,
      "Extraction clears pods in flight and re-arms the launch");
  }

  private static Harvester AddShip(Scene scene, Vector2 position, Harvester.HarvesterType type = Harvester.HarvesterType.Harvester)
  {
    var entity = scene.World.CreateEntity();
    entity.Attach(new Transform2(position));
    var ship = new Harvester { Entity = entity, Id = entity.Id, Type = type, PositionMoved = true };
    ship.SetCollisionPosition(position);
    entity.Attach(ship);
    scene.World.Update(new GameTime());
    return ship;
  }
}
