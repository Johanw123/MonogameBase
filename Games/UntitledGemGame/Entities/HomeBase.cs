using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended.Collisions;

namespace UntitledGemGame.Entities
{
  internal class HomeBase : ICollisionActor
  {
    public void OnCollision(CollisionEventArgs collisionInfo)
    {
      Console.WriteLine(collisionInfo.Other + " aaaaaa");


    }

    public IShapeF Bounds { get; set; }
  }
}
