using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.Collisions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities
{
  public class Harvester : ICollisionActor
  {
    public string Name { get; set; }
    public int ID { get; set; }

    public Vector2? TargetScreenPosition { get; set; } = null;

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public bool ReachedHome = false;

    public IShapeF Bounds { get; set; }

    public int CurrentCapacity = 2000;

    //public Bag<int> CarryingGems { get; } = new(5000);

    public int CarryingGemCount = 0;

    public void PickedUpGem(Gem gem)
    {
      //Check gem type etc and save for when delivering
      ++CarryingGemCount;
    }
  }
}
