using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
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
