using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class DroneCapstoneChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    CheckFinalSweep();
    ulong delivered = UntitledGemGameGameScreen.DeliveredUncounted;
    try
    {
      using var scene = new Scene(ShipModule.None);
      var drone = scene.Ship;
      drone.Type = Harvester.HarvesterType.Drone;
      scene.Manager.UGA.DroneRelay = true;
      scene.Manager.UGA.DroneOvercharge = true;
      scene.Manager.UGA.DroneRecharge = true;
      scene.Manager.UGA.DroneFinalSweep = true;
      scene.Manager.UGA.DroneCapacity = 100;
      float speed = BaseStats.GetHarvesterSpeed(drone);
      float range = BaseStats.GetHarvesterCollectionRange(drone);
      for (int i = 0; i < 30; i++) drone.PickedUpGem(new Gem { BaseValue = 10 });
      Check(BaseStats.GetHarvesterSpeed(drone) == speed * 2
        && BaseStats.GetHarvesterCollectionRange(drone) == range * 2,
        "Overcharge must grow from real pickups and stop at +100%.");
      scene.Manager.UGA.Reset("DroneOvercharge");
      Check(BaseStats.GetHarvesterSpeed(drone) == speed && BaseStats.GetHarvesterCollectionRange(drone) == range,
        "Refunding Overcharge must immediately remove active bonuses.");
      scene.Manager.UGA.DroneOvercharge = true;
      drone.AdvanceDroneTimers(100);
      Check(drone.TryBeginFinalSweep(Vector2.Zero), "First sortie must allow Final Sweep.");
      drone.FinishFinalSweep();
      drone.ReachedHome = true;
      scene.Invoke("DeliverCargo", drone);
      Check(UntitledGemGameGameScreen.DeliveredUncounted == delivered + 300 && drone.CarryingGemCount == 0
        && drone.CarryingGemBaseValue == 0 && !drone.MarkedForDestroy && !drone.ReturningToHomebase
        && !drone.ReachedHome && drone.TimeAlive == 0 && drone.DroneAgeSeconds == 0
        && drone.TargetScreenPosition == null && drone.DroneOverchargeMultiplier == 1,
        "Relay must pay the first delivery once and start a fresh, empty sortie.");
      drone.PickedUpGem(new Gem { BaseValue = 7 });
      Check(Math.Abs(drone.DroneOverchargeMultiplier - 1.04f) < 0.001f, "Overcharge must rebuild on the second sortie.");
      drone.AdvanceDroneTimers(100);
      Check(drone.TryBeginFinalSweep(Vector2.Zero), "Relay must refresh the once-per-sortie Final Sweep.");
      drone.FinishFinalSweep();
      drone.ReachedHome = true;
      scene.Invoke("DeliverCargo", drone);
      Check(drone.MarkedForDestroy && UntitledGemGameGameScreen.DeliveredUncounted == delivered + 307,
        "Relay drones must retire after delivering their second cargo.");
      Check(!drone.TryRelayDrone(), "Relay must never launch a third sortie.");

      var command = new Harvester { Type = Harvester.HarvesterType.Drone, IsCommandDrone = true, CarryingGemCount = 25 };
      Check(!command.TryRelayDrone() && command.DroneOverchargeMultiplier == 1,
        "Ability capstones must leave command drones unchanged.");
      var full = new Harvester { Type = Harvester.HarvesterType.Drone, CarryingGemCount = 100, CarryingGemBaseValue = 5 };
      Check(full.ReturningToHomebase, "Full cargo must trigger an early return.");
      scene.Invoke("DeliverCargo", full);
      Check(!full.ReturningToHomebase && !full.MarkedForDestroy, "Relay must also work after a full-cargo delivery.");
      scene.Manager.UGA.Reset("DroneRelay");
      var refunded = new Harvester { Type = Harvester.HarvesterType.Drone, CarryingGemBaseValue = 3 };
      scene.Invoke("DeliverCargo", refunded);
      Check(refunded.MarkedForDestroy, "Refunded Relay must stop active drones relaunching.");
      Check(new Harvester { Type = Harvester.HarvesterType.Harvester, CarryingGemCount = 25 }.DroneOverchargeMultiplier == 1,
        "Fleet ships must not gain Overcharge.");
    }
    finally { UntitledGemGameGameScreen.DeliveredUncounted = delivered; }
    Console.WriteLine("Drone capstones passed: bounded relaunch, delivery, fresh timers, sweep reset, Overcharge, refunds and command isolation.");
  }

  private static void CheckFinalSweep()
  {
    ulong delivered = UntitledGemGameGameScreen.DeliveredUncounted;
    try
    {
      using var scene = new Scene(ShipModule.None);
      var drone = scene.Ship;
      drone.Type = Harvester.HarvesterType.Drone;
      drone.BeginModuleTrip();
      scene.Manager.UGA.DroneFinalSweep = true;
      scene.Manager.UGA.DroneCollectionRange = 1.5f;
      scene.Manager.UGA.DroneSweepEfficiency = 25;
      scene.Manager.UGA.DroneRecharge = true;
      drone.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(drone);
      drone.CarryingGemBaseValue = 250;
      float range = BaseStats.DroneFinalSweepRadius;
      var gems = Enumerable.Range(0, 3)
        .Select(i => scene.AddGem(scene.Transform.Position + new Vector2(range * (0.75f + i * 0.1f), 0), 100)).ToArray();
      var outside = scene.AddGem(scene.Transform.Position + new Vector2(range * 1.1f, 0));
      var reserved = scene.AddGem(scene.Transform.Position + new Vector2(range * 0.75f, 1));
      var clicked = scene.AddGem(scene.Transform.Position + new Vector2(range * 0.75f, -1));
      clicked.WasClicked = true;
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
      drone.AdvanceDroneTimers(100);
      float timer = drone.TimeAlive;
      scene.Invoke("ClaimFinalSweep", drone, scene.Transform.Position);
      scene.Invoke("ResolveClaimedGems", drone);
      Check(drone.FinalSweepRadius == 300 && gems.All(g => g.PickedUp)
        && drone.CarryingGemCount == BaseStats.GetHarvesterCapacity(drone) + 3,
        "Drone Final Sweep must use its own large radius even with full cargo.");
      Check(!outside.PickedUp && !reserved.PickedUp && !clicked.PickedUp,
        "Sweep must respect range, existing reservations and manual pickups.");
      Check(drone.CarryingGemBaseValue == 625 && drone.TimeAlive == timer && drone.ReturningToHomebase
        && UntitledGemGameGameScreen.DeliveredUncounted == delivered,
        "Overflow sweep pickups must retain efficiency bonuses, await delivery and never recharge the timer.");
      Check(!drone.TryBeginFinalSweep(scene.Transform.Position), "Overflow sweep still fires once per sortie.");
      var normal = scene.AddGem(scene.Transform.Position);
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(normal.GridIndex);
      drone.ClaimedGems.Add(normal.Id);
      scene.Invoke("ResolveClaimedGems", drone);
      Check(!normal.PickedUp && scene.Fleet.flatSpatialHash.Gems[normal.GridIndex].ClaimState == 0,
        "Returning drones must reject ordinary pickups after the overflow sweep ends.");
      drone.ReachedHome = true;
      scene.Invoke("DeliverCargo", drone);
      Check(drone.MarkedForDestroy && drone.CarryingGemCount == 0
        && UntitledGemGameGameScreen.DeliveredUncounted == delivered + 625,
        "Overflow cargo must deliver its full value once through the usual drone lifecycle.");
    }
    finally { UntitledGemGameGameScreen.DeliveredUncounted = delivered; }
  }
}
