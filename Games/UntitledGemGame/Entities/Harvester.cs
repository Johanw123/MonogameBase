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

    public Vector2? TargetScreenPosition { get; set; } = null;

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public IShapeF Bounds { get; set; }

    public int CurrentCapacity = 100;

    public Bag<int> CarryingGems { get; } = new(5000);
  }
}
