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
    var rangeDrone = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
    var rangeHarvester = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Harvester };
    float baseDroneRange = BaseStats.GetHarvesterCollectionRange(rangeDrone);
    float fleetRange = BaseStats.GetHarvesterCollectionRange(rangeHarvester);
    manager.UGA.DroneCollectionRange = 1.45f;
    Check(Math.Abs(BaseStats.GetHarvesterCollectionRange(rangeDrone) - baseDroneRange * 1.45f) < 0.001f
      && BaseStats.GetHarvesterCollectionRange(rangeHarvester) == fleetRange,
      "Drone range upgrades must increase only drone collection radius");
    manager.UGA.Reset("DroneCollectionRange");
    Check(BaseStats.GetHarvesterCollectionRange(rangeDrone) == baseDroneRange,
      "Resetting drone range must restore its base radius");
    var tooltipHome = (UntitledGemGame.Entities.HomeBase)
      System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGame.Entities.HomeBase));
    var droneAbility = new UntitledGemGame.Entities.DroneAbility();
    manager.UGA.IncreaseDroneFuel = 2.34567f;
    manager.UGA.DroneRecharge = true;
    string droneDescription = tooltipHome.GetAbilityDescription(droneAbility);
  Check(droneDescription.Contains("4.69s") && droneDescription.Contains("Max lifespan:"),
    "Drone tooltip must show the recharge lifespan ceiling");
  manager.UGA.IncreaseDroneFuel = 1f;
  var rechargingDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  for (int step = 0; step < 8; step++)
  {
    rechargingDrone.AdvanceDroneTimers(0.25f);
    for (int gem = 0; gem < 20; gem++)
      rechargingDrone.PickedUpGem(new UntitledGemGame.Entities.Gem());
    Check(rechargingDrone.ReturningToHomebase == (step == 7) && !rechargingDrone.MarkedForDestroy,
      "Continuous recharge must keep a drone alive only until twice its lifetime");
  }
  var idleDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  idleDrone.AdvanceDroneTimers(1f);
  Check(idleDrone.ReturningToHomebase && !idleDrone.MarkedForDestroy,
    "An expired drone must return home alive, even with no cargo");
  Check(!idleDrone.ForceInstantCollection, "Drones must carry gems until delivery");
  Check(!idleDrone.TryConsumeDroneFission(), "Returning drones must not split before delivery");
  var collectingDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  var regularHarvester = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Harvester, CarryingGemCount = 10000 };
  float regularReturnSpeed = BaseStats.GetHarvesterSpeed(regularHarvester);
  manager.UGA.DroneSpeed = 1.5f;
  float normalDroneSpeed = BaseStats.GetHarvesterSpeed(collectingDrone);
  Check(BaseStats.GetHarvesterSpeed(idleDrone) == normalDroneSpeed,
    "Returning drones need the Afterburners upgrade to gain speed");
  manager.UGA.DroneAfterburners = true;
  Check(BaseStats.GetHarvesterSpeed(collectingDrone) == normalDroneSpeed
    && BaseStats.GetHarvesterSpeed(idleDrone) == normalDroneSpeed * 2f
    && BaseStats.GetHarvesterSpeed(regularHarvester) == regularReturnSpeed,
    "Afterburners must double upgraded drone return speed without affecting collection or fleet ships");
  Check(tooltipHome.GetAbilityDescription(droneAbility).Contains("Afterburners: 2x return speed"),
    "The ability tooltip must describe active Afterburners");
  manager.UGA.Reset("DroneAfterburners");
  Check(!manager.UGA.DroneAfterburners && BaseStats.GetHarvesterSpeed(idleDrone) == normalDroneSpeed
    && !tooltipHome.GetAbilityDescription(droneAbility).Contains("Afterburners:"),
    "Resetting Afterburners must remove its speed bonus and tooltip");
  manager.UGA.DroneSpeed = 1f;
  var cargoDrone = new UntitledGemGame.Entities.Harvester
    { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
  cargoDrone.PickedUpGem(new UntitledGemGame.Entities.Gem { BaseValue = 7 });
  cargoDrone.CarryingGemCount = 10000;
  Check(!cargoDrone.ReturningToHomebase && cargoDrone.CarryingGemBaseValue == 7,
    "Drones must retain cargo and collect until their timer expires, regardless of fleet capacity");
  cargoDrone.AdvanceDroneTimers(1f);
  cargoDrone.AdvanceDroneTimers(10f);
  Check(cargoDrone.ReturningToHomebase && !cargoDrone.MarkedForDestroy,
    "The return trip must not be limited by the collection timer");
  var fleet = new UntitledGemGame.Systems.HarvesterCollectionSystem(null, null);
  var deliver = typeof(UntitledGemGame.Systems.HarvesterCollectionSystem).GetMethod("DeliverCargo",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
  ulong previousDelivered = UntitledGemGame.Screens.UntitledGemGameGameScreen.DeliveredUncounted;
  try
  {
    cargoDrone.ReachedHome = true;
    deliver.Invoke(fleet, new object[] { cargoDrone });
    Check(cargoDrone.MarkedForDestroy && cargoDrone.CarryingGemCount == 0
      && cargoDrone.CarryingGemBaseValue == 0 && !cargoDrone.DepartingHomeBase
      && UntitledGemGame.Screens.UntitledGemGameGameScreen.DeliveredUncounted == previousDelivered + 7,
      "Docked drones must deposit their cargo and retire instead of departing again");
  }
  finally
  {
    UntitledGemGame.Screens.UntitledGemGameGameScreen.DeliveredUncounted = previousDelivered;
  }
  idleDrone.MarkedForDestroy = true;
  rechargingDrone.MarkedForDestroy = true;

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
      Check(offspring.ReturningToHomebase && !offspring.MarkedForDestroy && !offspring.TryConsumeDroneFission(),
        "Both offspring must expire without creating another generation");
    }
    Check(tooltipHome.GetAbilityDescription(droneAbility).Contains("2 drones (no further splits)"),
      "Fission tooltip must explain the offspring restriction");
    manager.UGA.DroneSweepEfficiency = 25;
    manager.UGA.DroneFinalSweep = true;
    var sweepDrone = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Drone };
    var valuableGem = new UntitledGemGame.Entities.Gem { BaseValue = 100 };
    sweepDrone.PickedUpGem(valuableGem);
    Check(sweepDrone.CarryingGemBaseValue == 100, "Normal pickups must not gain Sweep Efficiency");
    sweepDrone.AdvanceDroneTimers(100f);
    Check(sweepDrone.TryBeginFinalSweep(Microsoft.Xna.Framework.Vector2.Zero), "Sweep fixture must begin final pickup");
    sweepDrone.PickedUpGem(valuableGem);
    Check(sweepDrone.CarryingGemBaseValue == 225 && valuableGem.BaseValue == 100,
      "Sweep Efficiency must boost only swept cargo without mutating gems");
    sweepDrone.FinishFinalSweep();
    sweepDrone.PickedUpGem(valuableGem);
    Check(sweepDrone.CarryingGemBaseValue == 325, "Sweep bonus must stop after final pickup resolves");
    Console.WriteLine("Drone checks passed: recharge ceiling, stationary expiry, single-generation fission and tooltips.");
  }
}
