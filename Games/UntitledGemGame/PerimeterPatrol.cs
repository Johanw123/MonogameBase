using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Targets stay on consecutive sides of the inset play area, even after a resize.
public sealed class PerimeterPatrol
{
  private int edge;
  private float fraction;
  private int direction;
  public bool IsStarted { get; private set; }

  public void Reset() => IsStarted = false;

  public void Start(Random random)
  {
    edge = random.Next(4);
    fraction = random.NextSingle();
    direction = random.Next(2) == 0 ? -1 : 1;
    IsStarted = true;
  }

  public Vector2 GetTarget(PlayAreaBounds bounds)
  {
    var min = bounds.Minimum;
    var max = bounds.Maximum;
    return edge switch
    {
      0 => Vector2.Lerp(min, new Vector2(max.X, min.Y), fraction),
      1 => Vector2.Lerp(new Vector2(max.X, min.Y), max, fraction),
      2 => Vector2.Lerp(max, new Vector2(min.X, max.Y), fraction),
      _ => Vector2.Lerp(new Vector2(min.X, max.Y), min, fraction)
    };
  }

  public void Advance()
  {
    float end = direction > 0 ? 1f : 0f;
    if (fraction == end)
      edge = (edge + direction + 4) % 4;
    fraction = end;
  }

  public static float EdgeDistance(Vector2 position, PlayAreaBounds bounds)
    => MathF.Max(0, MathF.Min(MathF.Min(position.X - bounds.Minimum.X, bounds.Maximum.X - position.X),
      MathF.Min(position.Y - bounds.Minimum.Y, bounds.Maximum.Y - position.Y)));

  public static float EdgeBand(PlayAreaBounds bounds)
    => MathF.Min(bounds.Maximum.X - bounds.Minimum.X, bounds.Maximum.Y - bounds.Minimum.Y) * 0.15f;
}
