using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class DroneLightningChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    ulong delivered = UntitledGemGameGameScreen.DeliveredUncounted;
    try
    {
      using (var scene = new Scene(ShipModule.None))
      {
        scene.Ship.Type = Harvester.HarvesterType.Drone;
        var gems = Enumerable.Range(1, 8).Select(i => scene.AddGem(new Vector2(500 + i * 60, 500))).ToArray();
        var reserved = scene.AddGem(new Vector2(510, 500));
        var clicked = scene.AddGem(new Vector2(520, 500));
        clicked.WasClicked = true;
        var outside = scene.AddGem(new Vector2(500, 700));
        scene.Prepare();
        scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
        Check(gems.All(g => !g.PickedUp), "Drones need the capstone before firing.");
        scene.Manager.UGA.DroneLightning = true;
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
        Check(gems.Count(g => g.PickedUp) == 6 && scene.Ship.CarryingGemCount == 6,
          $"Lightning must chain beyond the drone's pickup radius and stop at six gems (picked {gems.Count(g => g.PickedUp)}, cargo {scene.Ship.CarryingGemCount}, arcs {scene.Ship.StormArcCount}).");
        Check(!reserved.PickedUp && !clicked.PickedUp && !outside.PickedUp,
          "Lightning must leave claimed, clicked and out-of-range gems alone.");
        Check(scene.Fleet.flatSpatialHash.Gems[clicked.GridIndex].ClaimState == 0,
          "Rejected manual pickups must release the lightning claim.");
        Check(scene.Ship.StormArcCount == 7 && scene.Ship.StormArcRemaining > 0f,
          "Every collected gem needs a visible lightning segment.");
        for (int i = 1; i < scene.Ship.StormArcCount; i++)
          Check(Vector2.Distance(scene.Ship.StormArcPoints[i - 1], scene.Ship.StormArcPoints[i]) <= BaseStats.DroneLightningJumpRadius,
            "Each hop must stay inside the advertised range.");
        Check(scene.Ship.CarryingGemBaseValue == 60 && UntitledGemGameGameScreen.DeliveredUncounted == delivered,
          "Zapped gems must enter cargo without paying income before delivery.");

        var next = scene.AddGem(new Vector2(530, 510));
        scene.Prepare();
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0.25f);
        Check(!next.PickedUp, "Lightning must respect its cooldown.");
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0.25f);
        Check(next.PickedUp && scene.Ship.CarryingGemCount == 7,
          "Lightning must resume once its cooldown expires.");
        var afterRefund = scene.AddGem(new Vector2(530, 520));
        scene.Prepare();
        scene.Manager.UGA.Reset("DroneLightning");
        scene.Invoke("ApplyDroneLightning", scene.Ship, 1f);
        Check(!afterRefund.PickedUp, "Refunding the capstone must stop active drones firing.");
        scene.Ship.ReachedHome = true;
        scene.Invoke("DeliverCargo", scene.Ship);
        Check(UntitledGemGameGameScreen.DeliveredUncounted == delivered + 70 && scene.Ship.MarkedForDestroy,
          "Lightning cargo must be delivered once through the normal drone lifecycle.");
      }

      using (var scene = new Scene(ShipModule.None))
      {
        scene.Ship.Type = Harvester.HarvesterType.Drone;
        scene.Manager.UGA.DroneLightning = true;
        scene.Manager.UGA.DroneLightningCount = 12;
        scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship) - 1;
        var gems = Enumerable.Range(0, 8).Select(i => scene.AddGem(new Vector2(530 + i, 500))).ToArray();
        scene.Prepare();
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
        Check(gems.Count(g => g.PickedUp) == 1 && scene.Ship.ReturningToHomebase,
          "Lightning must use only remaining cargo space.");
        scene.Invoke("ApplyDroneLightning", scene.Ship, 1f);
        Check(gems.Count(g => g.PickedUp) == 1, "Returning drones must stop firing.");
      }

      using (var scene = new Scene(ShipModule.None))
      {
        scene.Manager.UGA.DroneLightning = true;
        var gem = scene.AddGem(new Vector2(530, 500));
        scene.Prepare();
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
        Check(!gem.PickedUp, "Fleet harvesters must not gain the drone capstone.");
        scene.Ship.Type = Harvester.HarvesterType.Drone;
        scene.Ship.AdvanceDroneTimers(100f);
        scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
        Check(!gem.PickedUp, "Expired drones must stop firing even with empty cargo.");
        scene.Ship.DroneLightningCooldownRemaining = 1f;
        scene.Ship.ClearCargoForPrestige();
        Check(scene.Ship.DroneLightningCooldownRemaining == 0f, "Prestige must clear lightning runtime state.");
      }
      CheckUpgradedLightning();
      CheckPersistence();
    }
    finally { UntitledGemGameGameScreen.DeliveredUncounted = delivered; }
    Console.WriteLine("Drone lightning passed: range and chain upgrades, firing speed, bounded chains, reservations, cargo, delivery, expiry, refund and persistence.");
  }

  private static void CheckUpgradedLightning()
  {
    using var scene = new Scene(ShipModule.None);
    scene.Ship.Type = Harvester.HarvesterType.Drone;
    scene.Manager.UGA.DroneLightning = true;
    var gems = Enumerable.Range(1, 13).Select(i => scene.AddGem(new Vector2(500 + i * 100, 500))).ToArray();
    scene.Prepare();
    scene.Invoke("ApplyDroneLightning", scene.Ship, 0f);
    Check(gems.All(g => !g.PickedUp), "Base lightning must not reach gems 100 units away.");

    scene.Manager.UGA.DroneLightningRange = 130;
    scene.Manager.UGA.DroneLightningCount = 12;
    scene.Manager.UGA.DroneLightningInterval = 0.35f;
    scene.Invoke("ApplyDroneLightning", scene.Ship, 0.5f);
    Check(gems.Count(g => g.PickedUp) == 12 && !gems[12].PickedUp && scene.Ship.CarryingGemCount == 12,
      "Upgraded lightning must reach farther gems and stop at twelve pickups.");
    Check(scene.Ship.StormArcCount == 13 && scene.Ship.StormArcPoints.Length >= 13,
      "Longer chains must have room to render every lightning segment.");
    var next = scene.AddGem(new Vector2(520, 520));
    scene.Prepare();
    scene.Invoke("ApplyDroneLightning", scene.Ship, 0.34f);
    Check(!next.PickedUp, "Rapid Discharge must wait for its upgraded interval.");
    scene.Invoke("ApplyDroneLightning", scene.Ship, 0.011f);
    Check(next.PickedUp, "Rapid Discharge must fire after 0.35 seconds instead of 0.5.");

    var home = (HomeBase)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(HomeBase));
    string description = home.GetAbilityDescription(new DroneAbility());
    Check(description.Contains("12 gems every 0.35s") && description.Contains("130 range"),
      "The drone tooltip must show upgraded lightning stats.");
    foreach (string property in new[] { "DroneLightningRange", "DroneLightningCount", "DroneLightningInterval" })
      scene.Manager.UGA.Reset(property);
    Check(BaseStats.DroneLightningJumpRadius == 70 && BaseStats.DroneLightningGemLimit == 6
      && BaseStats.DroneLightningIntervalSeconds == 0.5f,
      "Refunding lightning upgrades must restore the original stats.");
  }

  private static void CheckPersistence()
  {
    var previousTree = UpgradeManager.CurrentUpgrades;
    string directory = Path.Combine(Path.GetTempPath(), "drone-lightning-" + Guid.NewGuid());
    try
    {
      var tree = new Upgrades();
      UpgradeManager.CurrentUpgrades = tree;
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades_abilities.json"),
        File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"), tree.UpgradeButtonsAbilities, tree.UpgradeDefinitionsAbilities);
      var capstone = tree.UpgradeButtonsAbilities["DroneLightning1"];
      Check(capstone.Data.BlockedBy == "DSE3" && capstone.Data.LevelInfo.Single().Cost == 5,
        "Storm Drones must be a five-point capstone after the Final Sweep route.");
      capstone.CurrentLevel = 1;
      foreach (string id in new[] { "DroneLightningRange1", "DroneLightningCount1", "DroneLightningInterval1" })
      {
        var talent = tree.UpgradeButtonsAbilities[id];
        Check(talent.Data.BlockedBy == "DroneLightning1" && talent.Data.LockedBy == "DroneLightning1"
          && talent.Data.LevelInfo.Select(level => level.Cost).SequenceEqual(new ulong[] { 1, 2, 3 }),
          "Lightning talents must require the capstone and cost one, two, then three points.");
        talent.CurrentLevel = 3;
      }
      var manager = new UpgradeManager();
      var save = new GameSave();
      manager.CaptureProgress(save);
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(save), "The current save format must save the capstone.");
      manager.RestoreProgress(store.Load() ?? throw new Exception("Capstone save did not load."));
      Check(manager.UGA.DroneLightning && capstone.IsMaxLevel, "Save/load must restore the purchased capstone.");
      Check(BaseStats.DroneLightningJumpRadius == 130 && BaseStats.DroneLightningGemLimit == 12
        && MathF.Abs(BaseStats.DroneLightningIntervalSeconds - 0.35f) < 0.001f,
        "Save/load must restore all three ranks of every lightning talent.");
      manager = new UpgradeManager();
      manager.RestoreProgress(new GameSave());
      Check(!manager.UGA.DroneLightning, "Restoring an unpurchased tree must remove the capstone.");
      Check(BaseStats.DroneLightningJumpRadius == 70 && BaseStats.DroneLightningGemLimit == 6
        && BaseStats.DroneLightningIntervalSeconds == 0.5f,
        "An unpurchased tree must restore base lightning stats.");
    }
    finally
    {
      UpgradeManager.CurrentUpgrades = previousTree;
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }
}
