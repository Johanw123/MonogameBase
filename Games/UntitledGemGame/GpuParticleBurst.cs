using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public enum GpuParticleMotion { Directional, Outward, Inward, Tangential }

/// <summary>A one-shot emitter. Distances, sizes and speeds are in world units; times are seconds.</summary>
public readonly record struct GpuParticleBurst(Vector2 Position, int Count, Color Color)
{
  public Vector2 Velocity { get; init; }
  public Vector2 Speed { get; init; } = new(30f, 110f);
  public Vector2 Lifetime { get; init; } = new(0.4f, 1.2f);
  public Vector2 Size { get; init; } = new(0.7f, 2f);
  public float Radius { get; init; } = 1f;
  public float InnerRadius { get; init; }
  public float Direction { get; init; }
  public float Spread { get; init; } = MathHelper.TwoPi;
  public GpuParticleMotion Motion { get; init; }
  public bool Streak { get; init; }

  internal void Validate()
  {
    if (Count < 0) throw new ArgumentOutOfRangeException(nameof(Count));
    if (!Finite(Position) || !Finite(Velocity)) throw new ArgumentException("Particle position and velocity must be finite.");
    Range(Speed, nameof(Speed), false);
    Range(Lifetime, nameof(Lifetime), true);
    Range(Size, nameof(Size), true);
    if (!float.IsFinite(Radius) || Radius < 0f) throw new ArgumentOutOfRangeException(nameof(Radius));
    if (!float.IsFinite(InnerRadius) || InnerRadius < 0 || InnerRadius > Radius)
      throw new ArgumentOutOfRangeException(nameof(InnerRadius));
    if ((uint)Motion > (uint)GpuParticleMotion.Tangential) throw new ArgumentOutOfRangeException(nameof(Motion));
    if (!float.IsFinite(Direction)) throw new ArgumentOutOfRangeException(nameof(Direction));
    if (!float.IsFinite(Spread) || Spread < 0f || Spread > MathHelper.TwoPi)
      throw new ArgumentOutOfRangeException(nameof(Spread));
  }

  private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
  private static void Range(Vector2 value, string name, bool positive)
  {
    if (!Finite(value) || value.X < 0f || (positive && value.X == 0f) || value.Y < value.X)
      throw new ArgumentOutOfRangeException(name, "Expected a finite minimum and maximum, in ascending order.");
  }
}
