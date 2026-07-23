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
    private static readonly Dictionary<int, float> PrdConstants = new Dictionary<int, float>
    {
        { 10, 0.01475f },
        { 15, 0.03222f },
        { 20, 0.05570f },
        { 25, 0.08504f },
        { 30, 0.12158f },
        { 40, 0.20154f },
        { 50, 0.30210f }
    };
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

    public static bool PercentChance(int percentage)
    {
      if(percentage <= 0) return false;

      // Clamp the percentage between 0 and 100 just to be safe from weird inputs
      percentage = Math.Clamp(percentage, 0, 100);

      // m_rand.Next(0, 100) returns a value from 0 to 99.
      // If percentage is 30, values 0-29 will return true (exactly 30 values).
      return m_rand.Next(0, 100) < percentage;
    }

    public static bool PseudoPercentChance(int targetPercentage, ref int failCount)
    {
      // Fallback to true random if we haven't mapped the PRD constant for this percentage
      if (!PrdConstants.TryGetValue(targetPercentage, out float c))
      {
        return m_rand.Next(0, 100) < targetPercentage;
      }

      // Calculate the current chance based on how many times we've failed
      // Current Chance = C * (Number of attempts)
      // number of attempts = failCount + 1
      float currentChance = c * (failCount + 1);

      // Roll a random float between 0.0 and 1.0
      if (m_rand.NextSingle() < currentChance)
      {
        // Success! Reset the fail counter.
        failCount = 0;
        return true;
      }
      else
      {
        // Failure. Increment the counter to boost the chance next time.
        failCount++;
        return false;
      }
    }

    public static Vector2 Vector2(Vector2 min, Vector2 max)
    {
      return new Vector2(Int(min.X, max.X), Int(min.Y, max.Y));
    }

    public static Vector2 Vector2(int minX, int minY, int maxX, int maxY)
    {
      return new Vector2(Int(minX, maxX), Float(minY, maxY));
    }
    public static Vector2 Vector2(float min, float max)
    {
      return new Vector2(Float(min, max), Float(min, max));
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
