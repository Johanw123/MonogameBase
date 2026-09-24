using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class PerimeterChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var bounds = new PlayAreaBounds(new Vector2(-500, -300), new Vector2(500, 300));
    // Large padded sprites must still reach every corner, even when movement
    // finishes just short of the clamped target. Exercise the real collection query.
    foreach (float range in new[] { 12f, 27f, 150f })
    {
      float arrival = Math.Min(20f, range * 0.5f);
      var movementBounds = bounds.InsetForCollection(100f, range, arrival);
      foreach (var corner in new[] { bounds.Minimum, bounds.Maximum,
        new Vector2(bounds.Minimum.X, bounds.Maximum.Y), new Vector2(bounds.Maximum.X, bounds.Minimum.Y) })
      {
        var cornerGrid = new GemSpatialIndex(4, 30);
        int gemIndex = cornerGrid.AddGem(123, corner.X, corner.Y, 1);
        var target = movementBounds.Clamp(corner);
        var stoppedPosition = target + Vector2.Normalize(target - corner) * (arrival * 0.99f);
        bool found = false;
        foreach (int index in cornerGrid.QueryCollection(stoppedPosition.X, stoppedPosition.Y, range))
          found |= index == gemIndex;
        Check(found, "Harvesters must collect corner gems before treating their clamped target as reached");
      }
    }
    Check(bounds.InsetForCollection(40f, 150f, 20f).Minimum == bounds.Inset(40f).Minimum,
      "Ships with sufficient range must retain their full sprite margin");
    var random = new Random(73);
    var starts = new HashSet<Vector2>();
    for (int trip = 0; trip < 100; trip++)
    {
      var patrol = new PerimeterPatrol();
      Check(!patrol.IsStarted, "New ships must choose an edge entry point");
      patrol.Start(random);
      var entry = patrol.GetTarget(bounds);
      starts.Add(entry);
      Check(PerimeterPatrol.EdgeDistance(entry, bounds) == 0, "Entry points must lie on an edge");
      var corners = new HashSet<Vector2>();
      var previousTarget = entry;
      for (int step = 0; step < 9; step++)
      {
        patrol.Advance();
        var target = patrol.GetTarget(bounds);
        Check(target.X == previousTarget.X || target.Y == previousTarget.Y,
          "Patrol segments must follow an edge without cutting across the field");
        Check(target != previousTarget, "Patrol must continue moving at corners");
        Check(PerimeterPatrol.EdgeDistance(target, bounds) == 0, "Patrol must stay at the edge");
        corners.Add(target);
        previousTarget = target;
      }
      Check(corners.Count == 4, "Repeated circuits must visit all four corners");
      var shifted = new PlayAreaBounds(new Vector2(700, -100), new Vector2(900, 100));
      var shiftedTarget = patrol.GetTarget(shifted);
      Check(shifted.Clamp(shiftedTarget) == shiftedTarget
        && PerimeterPatrol.EdgeDistance(shiftedTarget, shifted) == 0,
        "Camera changes must keep patrol targets on the new boundary");
      patrol.Reset();
      Check(!patrol.IsStarted, "Delivery must reset patrol entry selection");
      patrol.Start(random);
      Check(patrol.IsStarted, "Patrol must restart after delivery");
    }
    Check(starts.Count > 90, "Trips must have varied edge entry points");

    var previous = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var ship = new Harvester { Type = Harvester.HarvesterType.PerimeterHarvester };
      manager.UG.PerimeterHarvesterSpeed = 2;
      manager.UG.PerimeterHarvesterCapacity = 37;
      manager.UG.PerimeterHarvesterMaxFuel = 3;
      manager.UG.PerimeterFuelEfficiency = 4;
      manager.UG.PerimeterHarvesterRefuelSpeed = 5;
      Check(BaseStats.GetHarvesterSpeed(ship) == 240 && BaseStats.GetHarvesterCapacity(ship) == 37,
        "Perimeter speed and cargo upgrades must use independent stats");
      Check(BaseStats.GetHarvesterMaxFuelMultiplier(ship) == 3 && BaseStats.GetHarvesterFuelEfficiency(ship) == 4
        && BaseStats.GetHarvesterRefuelSpeedMultiplier(ship) == 5, "Perimeter fuel upgrades must apply");
      Check(BaseStats.IsFleetHarvester(ship), "Permanent fleet effects must include perimeter ships");
      manager.UGA.MagnetizerAdvancedHarvesters = true;
      Check(!manager.UGA.HasMagnetizer(ship.Type), "Advanced talents must not enable perimeter talents");
      manager.UGA.MagnetizerPerimeterHarvesters = true;
      Check(manager.UGA.HasMagnetizer(ship.Type),
        "Perimeter magnetizer must target the new type");
    }
    finally { UpgradeManager.Instance = previous; }
    Console.WriteLine("Perimeter checks passed: edge circuits, randomized entry, delivery reset, camera bounds, independent stats and abilities.");
  }
}
