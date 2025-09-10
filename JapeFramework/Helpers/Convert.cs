using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace JapeFramework.Helpers
{
  public static class Convert
  {
    public static float DegreesToRadians(float degrees)
    {
      return degrees * .017453292519f;
    }
    public static float RadiansToDegrees(float radians)
    {
      return radians * 57.2957795130f;
    }
    public static Vector2 RadiansToDirection(float radians)
    {
      return new Vector2(-(float)(Math.Sin(radians)), (float)(Math.Cos(radians)));
    }
    public static float DirectionToRadians(Vector2 direction)
    {
      return (float)Math.Atan2(direction.X, direction.Y);
    }
  }
}
