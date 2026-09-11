using UntitledGemGame;

internal static class DroneChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var manager = new UpgradeManager();
    var tooltipHome = (UntitledGemGame.Entities.HomeBase)
      System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGame.Entities.HomeBase));
    var droneAbility = new UntitledGemGame.Entities.DroneAbility();
    manager.UGA.IncreaseDroneFuel = 2.34567f;
    manager.UGA.DroneRecharge = true;
    string droneDescription = tooltipHome.GetAbilityDescription(droneAbility);
  Check(droneDescription.Contains("4.69s") && droneDescription.Contains("Max lifespan:"),
    "Drone tooltip must show the recharge lifespan ceiling");
  manager.UGA.HarvesterDrones = 1;
  Check(tooltipHome.GetAbilityDescription(droneAbility).Contains("Limit: 1 per 1s per harvester"),
    "Drone tooltip must explain the per-harvester launch cooldown");
  var launcher = new UntitledGemGame.Entities.Harvester();
  var otherLauncher = new UntitledGemGame.Entities.Harvester();
  launcher.MovedDistance = otherLauncher.MovedDistance = 10000;
  Check(launcher.TryLaunchDistanceDrone() && otherLauncher.TryLaunchDistanceDrone(),
    "Each harvester must have its own launch cooldown");
  launcher.MovedDistance = 10000;
  launcher.AdvanceDroneTimers(0.5f);
  Check(!launcher.TryLaunchDistanceDrone(), "High speed must not bypass the launch cooldown");
  launcher.AdvanceDroneTimers(0.5f);
  Check(launcher.TryLaunchDistanceDrone() && !launcher.TryLaunchDistanceDrone(),
    "Launch may resume after one second without queuing a burst");
  manager.UGA.IncreaseDroneFuel = 1f;
  var rechargingDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  for (int step = 0; step < 8; step++)
  {
    rechargingDrone.AdvanceDroneTimers(0.25f);
    for (int gem = 0; gem < 20; gem++)
      rechargingDrone.PickedUpGem(new UntitledGemGame.Entities.Gem());
    Check(rechargingDrone.MarkedForDestroy == (step == 7),
      "Continuous recharge must keep a drone alive only until twice its lifetime");
  }
  var idleDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  idleDrone.AdvanceDroneTimers(1f);
  Check(idleDrone.MarkedForDestroy, "A drone without recharge must expire even without movement");

    manager.UGA.DroneFission = false;
    Check(!idleDrone.TryConsumeDroneFission(), "Fission must require the upgrade");
    manager.UGA.DroneFission = true;
    Check(idleDrone.TryConsumeDroneFission() && !idleDrone.TryConsumeDroneFission(),
      "An expired original drone must split exactly once");
    Check(rechargingDrone.TryConsumeDroneFission(),
      "Reaching the recharge lifespan ceiling must also allow fission");
    var livingDrone = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
    Check(!livingDrone.TryConsumeDroneFission(), "Living drones must not split");
    livingDrone.MarkedForDestroy = true;
    Check(!livingDrone.TryConsumeDroneFission(), "Cleanup must not trigger fission");
    for (int i = 0; i < 2; i++)
    {
      var offspring = new UntitledGemGame.Entities.Harvester
        { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone, IsDroneOffspring = true };
      Check(offspring.TimeAlive == 0 && offspring.DroneAgeSeconds == 0,
        "Offspring must start with a fresh lifetime");
      offspring.AdvanceDroneTimers(1f);
      Check(offspring.MarkedForDestroy && !offspring.TryConsumeDroneFission(),
        "Both offspring must expire without creating another generation");
    }
    Check(tooltipHome.GetAbilityDescription(droneAbility).Contains("2 drones (no further splits)"),
      "Fission tooltip must explain the offspring restriction");
    Console.WriteLine("Drone checks passed: independent launch cooldowns, recharge ceiling, stationary expiry, single-generation fission and tooltips.");
  }
}
