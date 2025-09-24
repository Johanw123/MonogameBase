using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace JapeFramework.Helpers
{
  public static class RandomHelper
  {
    private static FastRandom m_rand = new FastRandom();
    public static int Int(int min = 0, int max = int.MaxValue)
    {
      return m_rand.Next(min, max);
    }

    public static float Float(float min = 0, float max = float.MaxValue)
    {
      return m_rand.NextSingle(min, max);
    }

    public static Vector2 Vector2(Vector2 min, Vector2 max)
    {
      return new Vector2(Float(min.X, max.X), Float(min.Y, max.Y));
    }
  }
}
