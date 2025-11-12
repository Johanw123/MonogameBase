using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collections;

namespace JapeFramework.Helpers
{
  public static class RandomHelper
  {
    // private static FastRandom m_rand = new FastRandom();
    private static FastRandom m_rand = FastRandom.Shared;
    public static int Int(int min = 0, int max = int.MaxValue)
    {
      return m_rand.Next(min, max);
    }

    public static int Int(float min = 0, float max = int.MaxValue)
    {
      return m_rand.Next((int)min, (int)max);
    }

    public static float Float(float min = 0, float max = float.MaxValue)
    {
      return m_rand.NextSingle(min, max);
    }

    public static Vector2 Vector2(Vector2 min, Vector2 max)
    {
      return new Vector2(Int(min.X, max.X), Int(min.Y, max.Y));
    }

    public static Vector2 Vector2(int minX, int minY, int maxX, int maxY)
    {
      return new Vector2(Int(minX, maxX), Float(minY, maxY));
    }

    //public static T GetRandom<T>(this IEnumerable<T> data)
    //{
    //  var enumerable = data as T[];
    //  int randomNumber = m_rand.Next(0, enumerable.Count());
    //  return enumerable.ElementAt(randomNumber);
    //}

    public static T GetRandom<T>(this Bag<T> data)
    {
      var randomNumber = m_rand.Next(0, data.Count);
      return data[randomNumber];
    }

    public static T GetRandom<T>(this HashSet<T> data)
    {
      var randomNumber = m_rand.Next(0, data.Count);
      return data.ElementAtOrDefault(randomNumber) ?? data.FirstOrDefault();
    }
  }
}
