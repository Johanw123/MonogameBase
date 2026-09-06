using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

public struct MagnetSource
{
  public Vector2 Position;
  public float Power;
}

public static class MagnetizerCache
{
  public static readonly List<MagnetSource> ActiveMagnets = new(32);

  /// <summary>
  /// Call ONCE at the start of frame update!
  /// </summary>
  public static void Refresh()
  {
    ActiveMagnets.Clear();

    // 1. HomeBase
    if (HomeBase.BonusMagnetPower > 0)
    {
      ActiveMagnets.Add(new MagnetSource
      {
        Position = UntitledGemGameGameScreen.HomeBasePos,
        Power = HomeBase.BonusMagnetPower
      });
    }

    // 2. Harvesters
    if (HomeBase.BonusHarvesterMagnetPower > 0)
    {
      foreach (var harvesterId in HarvesterCollectionSystem.Instance._harvesters)
      {
        var entity = HarvesterCollectionSystem.Instance.GetEntityP(harvesterId);
        if (entity != null && UpgradeManager.Instance.UGA.HasMagnetizer(entity.Get<Harvester>().Type))
        {
          ActiveMagnets.Add(new MagnetSource
          {
            Position = entity.Get<Transform2>().Position,
            Power = HomeBase.BonusHarvesterMagnetPower
          });
        }
      }
    }

    // 4. Beacons
    if (UpgradeManager.Instance.UGA.MagnetizerBeacons)
    {
      foreach (var b in EntityFactory.Instance.Beacons.Values)
      {
        if (b != null)
        {
          ActiveMagnets.Add(new MagnetSource
          {
            Position = b.Get<Transform2>().Position,
            Power = HomeBase.BonusMagnetPower
          });
        }
      }
    }
  }
}
