using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended.Collisions;

namespace UntitledGemGame.Entities
{
  public abstract class IHomeBaseAbility
  {
    public int CooldownTime = 5000;
    public int MaxCooldownTime = 5000;
    public int DurationTime = 0;
    public int DurationTimeMax = 1000;

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
    override public void Deactivate()
    {
      HomeBase.BonusMoveSpeed = 1.0f;
      Console.WriteLine("Speedboost deactivated!");
    }
  }

  public class MagnetAbility : IHomeBaseAbility
  {
    public override void Activate()
    {
      HomeBase.BonusMagnetPower = 10.0f;
      Console.WriteLine("Magnet activated!");
    }
    override public void Deactivate()
    {
      HomeBase.BonusMagnetPower = 0.0f;
      Console.WriteLine("Magnet deactivated!");
    }
  }

  public class HomeBase : ICollisionActor
  {
    public static float BonusMoveSpeed = 1.0f;
    public static float BonusMagnetPower = 0.0f;

    public List<IHomeBaseAbility> Abilities = new List<IHomeBaseAbility>();
    public List<IHomeBaseAbility> ActiveAbilities = new List<IHomeBaseAbility>();

    public HomeBase()
    {
      Abilities.Add(new SpeedboostAbility());
      Abilities.Add(new MagnetAbility());
      ActiveAbilities.Add(Abilities[0]);
      ActiveAbilities.Add(Abilities[1]);
    }

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
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
