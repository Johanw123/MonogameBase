using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class WarpDriveChecks
{
  public static void Run()
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var fleet = new HarvesterCollectionSystem(null, null);
      var activate = typeof(HarvesterCollectionSystem).GetMethod("TryActivateWarpDrive",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
      var origin = new Vector2(100, 100);
      var target = new Vector2(600, 100);
      var transform = new Transform2(origin);
      var ship = new Harvester { Type = Harvester.HarvesterType.UltimateHarvester, TargetScreenPosition = target };
      void Activate() => activate.Invoke(fleet, new object[] { ship, transform });
      Activate();
      if (transform.Position != origin || ship.WarpDriveFlashTimeRemaining != 0)
        throw new Exception("Locked Warp Drive must not teleport or flash");
      manager.UG.WarpDrive = true;
      ship.TargetScreenPosition = origin + Vector2.One;
      Activate();
      if (ship.WarpDriveFlashTimeRemaining != 0)
        throw new Exception("Short movements must not trigger a warp flash");
      ship.TargetScreenPosition = target;
      Activate();
      if (transform.Position != target || ship.WarpDriveDeparturePosition != origin
        || ship.WarpDriveArrivalPosition != target
        || ship.WarpDriveFlashTimeRemaining != BaseStats.WarpDriveFlashDurationSeconds
        || ship.WarpDriveCooldownRemaining != BaseStats.WarpDriveCooldownSeconds)
        throw new Exception("Warp Drive must capture both endpoints and start its effect and cooldown");
      ship.TargetScreenPosition = origin;
      ship.WarpDriveFlashTimeRemaining = 0.1f;
      Activate();
      if (transform.Position != target || ship.WarpDriveFlashTimeRemaining != 0.1f
        || ship.WarpDriveDeparturePosition != origin || ship.WarpDriveArrivalPosition != target)
        throw new Exception("Cooldown must prevent repeated teleport effects");
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Warp Drive checks passed: unlock, distance, effect endpoints and cooldown.");
  }
}
