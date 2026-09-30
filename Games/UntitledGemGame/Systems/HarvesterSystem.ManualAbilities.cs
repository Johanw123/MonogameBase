using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  public void CashOutFleet(float multiplier)
  {
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship == null || ship.MarkedForDestroy || ship.CarryingGemCount == 0
        || !(BaseStats.IsFleetHarvester(ship) || ship.Type == Harvester.HarvesterType.Drone)) continue;
      // Unload without docking, moving the ship, resetting its trip, or expiring a drone.
      ReleaseTreasureScannerTarget(ship);
      ulong value = ScaleCommandValue(CalculateDeliveryValue(ship), multiplier);
      UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
        UntitledGemGameGameScreen.DeliveredUncounted, value);
      ship.CarryingGemCount = 0;
      ship.CarryingGemBaseValue = 0;
      ship.ReachedHome = false;
      ship.CollectionEndpoint = null;
      ship.ShowModulePulse(ship.BoundingCircle.Center, BaseStats.GetHarvesterCollectionRange(ship), Color.Gold);
      ship.RelayOrigin = ship.BoundingCircle.Center;
      ship.RelayFlashRemaining = ModuleCatalog.PulseDuration;
      if (ship._currentTargetBucket != -1)
      {
        flatSpatialHash.ReleaseBucket(ship._currentTargetBucket);
        ship._currentTargetBucket = -1;
      }
      ship.TargetScreenPosition = null;
      ship.DepartingHomeBase = false;
    }
  }

  public static ulong ScaleCommandValue(ulong value, double multiplier)
  {
    double result = Math.Ceiling(value * multiplier);
    return result >= ulong.MaxValue ? ulong.MaxValue : (ulong)result;
  }

  public ulong GetFleetCargoCapacity()
  {
    ulong total = 0;
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship != null && !ship.MarkedForDestroy && BaseStats.IsFleetHarvester(ship))
        total = PrestigeProgression.AddSaturating(total, (ulong)BaseStats.GetHarvesterCapacity(ship));
    }
    return total;
  }

  public float CollectorSwarmValueMultiplier()
  {
    int ships = 0;
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship != null && !ship.MarkedForDestroy && BaseStats.IsFleetHarvester(ship)) ++ships;
    }
    return Math.Max(1f, ships / (float)ManualFleetAbilities.CollectorCount);
  }

  public void GetCollectorSwarmStats(out float speed, out float range)
  {
    var baseline = new Harvester { Type = Harvester.HarvesterType.Drone };
    speed = BaseStats.GetHarvesterSpeed(baseline);
    range = BaseStats.GetHarvesterCollectionRange(baseline);
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship == null || ship.MarkedForDestroy || !BaseStats.IsFleetHarvester(ship)) continue;
      speed = Math.Max(speed, BaseStats.GetHarvesterSpeed(ship));
      range = Math.Max(range, BaseStats.GetHarvesterCollectionRange(ship));
    }
    // Overdrive is applied dynamically to drones, rather than captured twice.
    speed /= UntitledGemGameGameScreen.Instance?.ManualAbilities.SpeedMultiplier ?? 1f;
  }
}
