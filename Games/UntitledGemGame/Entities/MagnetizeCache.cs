using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

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
      foreach (var h in EntityFactory.Instance.Harvesters.Values)
      {
        if (h != null)
        {
          ActiveMagnets.Add(new MagnetSource
          {
            Position = h.Get<Transform2>().Position,
            Power = HomeBase.BonusHarvesterMagnetPower
          });
        }
      }

      // 3. Drones
      if (UpgradeManager.UG.MagnetizerDrones)
      {
        foreach (var d in EntityFactory.Instance.Drones.Values)
        {
          if (d != null)
          {
            ActiveMagnets.Add(new MagnetSource
            {
              Position = d.Get<Transform2>().Position,
              Power = HomeBase.BonusHarvesterMagnetPower
            });
          }
        }
      }
    }

    // 4. Beacons
    if (UpgradeManager.UG.MagnetizerBeacons)
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
