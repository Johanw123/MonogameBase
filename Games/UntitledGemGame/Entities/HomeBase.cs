using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended.Collisions;
using UntitledGemGame.Screens;
using MonoGame.Extended.ECS;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities
{
  public abstract class IHomeBaseAbility
  {
    public int CooldownTime = 5000;
    public int MaxCooldownTime = 5000;
    public int DurationTime = 0;
    public virtual int DurationTimeMax => 1000;

    public bool IsActive => DurationTime > 0;
    public abstract void Activate();
    public abstract void Deactivate();
  }

  public class SpeedboostAbility : IHomeBaseAbility
  {
    public override void Activate()
    {
      HomeBase.BonusMoveSpeed = 2.0f;
      Console.WriteLine("Speedboost activated!");
    }

    public override void Deactivate()
    {
      HomeBase.BonusMoveSpeed = 1.0f;
      Console.WriteLine("Speedboost deactivated!");
    }
  }

  public class MagnetAbility : IHomeBaseAbility
  {
    public override void Activate()
    {
      HomeBase.BonusMagnetPower = 5.0f;
      Console.WriteLine("Magnet activated!");
    }

    public override void Deactivate()
    {
      HomeBase.BonusMagnetPower = 0.0f;
      Console.WriteLine("Magnet deactivated!");
    }
  }

  public class HarvesterMagnetAbility : IHomeBaseAbility
  {
    public override void Activate()
    {
      HomeBase.BonusHarvesterMagnetPower = 5.0f;
    }

    public override void Deactivate()
    {
      HomeBase.BonusHarvesterMagnetPower = 0.0f;
    }
  }

  public class DroneAbility : IHomeBaseAbility
  {
    private List<Entity> drones = new List<Entity>();

    public override int DurationTimeMax => 5000;

    public override void Activate()
    {
      Console.WriteLine("Drone activated!");
      var random = new Random();
      for (int i = 0; i < 10; i++)
      {
        var drone = EntityFactory.Instance.CreateDrone(UntitledGemGameGameScreen.HomeBasePos + new Vector2(random.NextSingle(-50, 50), random.NextSingle(-50, 50)));
        drones.Add(drone);
      }
    }

    public override void Deactivate()
    {
      foreach (var drone in drones)
      {
        // EntityFactory.Instance.RemoveHarvester(drone.Id);
        drone.Destroy();
      }
      drones.Clear();
      Console.WriteLine("Drone deactivated!");
    }
  }

  public class HomeBase : ICollisionActor
  {
    public static float BonusMoveSpeed = 1.0f;
    public static float BonusMagnetPower = 0.0f;
    public static float BonusHarvesterMagnetPower = 0.0f;

    public List<IHomeBaseAbility> Abilities = new List<IHomeBaseAbility>();
    public List<IHomeBaseAbility> ActiveAbilities = new List<IHomeBaseAbility>();

    public HomeBase()
    {
      Abilities.Add(new SpeedboostAbility());
      Abilities.Add(new MagnetAbility());
      Abilities.Add(new DroneAbility());
      Abilities.Add(new HarvesterMagnetAbility());

      foreach (var ability in Abilities)
      {
        ActiveAbilities.Add(ability);
      }
    }

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public void Update(GameTime gameTime)
    {
      foreach (var ability in ActiveAbilities)
      {
        if (ability.IsActive)
        {
          ability.DurationTime -= gameTime.ElapsedGameTime.Milliseconds;
          if (ability.DurationTime <= 0)
          {
            ability.Deactivate();
            ability.CooldownTime = ability.MaxCooldownTime;
          }
        }
        else
        {
          ability.CooldownTime -= gameTime.ElapsedGameTime.Milliseconds;
          if (ability.CooldownTime <= 0)
          {
            ability.Activate();
            ability.DurationTime = ability.DurationTimeMax;
          }
        }
      }
    }

    public IShapeF Bounds { get; set; }
  }
}
