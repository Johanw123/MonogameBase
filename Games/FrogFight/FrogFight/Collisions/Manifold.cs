using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FrogFight.Collisions
{
  public struct Manifold
  {
    public float Penetration;
    public Vector2 Normal;
    public Vector2 Overlap;
  }
}
