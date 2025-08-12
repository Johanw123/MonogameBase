using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FrogFight.Collisions
{
  public enum BodyType
  {
    Static, Dynamic
  }

  public class Body
  {
    public BodyType BodyType = BodyType.Static;
    public Vector2 Position;
    public Vector2 Velocity;
    public AABB BoundingBox => new AABB(Position - Size / 2f, Position + Size / 2f);
    public Vector2 Size;
  }
}
