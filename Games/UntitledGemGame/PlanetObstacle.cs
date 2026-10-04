using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Screens;

namespace UntitledGemGame;

// Ships fly around the mined planet instead of through it. Movement aims at a
// steering waypoint while the straight path is blocked, and positions/targets
// inside the blocked circle are pushed back to its edge.
public static class PlanetObstacle
{
  // Steering circles a little wider than the blocked circle so ships arc around
  // the rim; a 0.55 rad orbit step keeps every chord outside the blocked circle.
  private const float SteeringMargin = 1.12f;
  private const float OrbitStep = 0.55f;

  public static bool Active => UntitledGemGameGameScreen.PlanetMiningEnabled;
  public static Vector2 Center => UntitledGemGameGameScreen.PlanetPos;

  // Ships may overlap the rim by part of their hull, but never come further out
  // than the nearest landed gems (PlanetDebrisGap), so those stay collectable.
  public static float Clearance(float shipRadius)
    => UntitledGemGameGameScreen.PlanetRadius
      + Math.Min(shipRadius * 0.65f, UntitledGemGameGameScreen.PlanetDebrisGap);

  public static Vector2 PushOut(Vector2 position, Vector2 center, float radius)
  {
    var offset = position - center;
    float distanceSquared = offset.LengthSquared();
    if (distanceSquared >= radius * radius) return position;
    var direction = distanceSquared > 1e-4f ? offset / MathF.Sqrt(distanceSquared) : -Vector2.UnitX;
    return center + direction * radius;
  }

  // Where to head now so that the path to target goes around the circle.
  public static Vector2 Steer(Vector2 position, Vector2 target, Vector2 center, float radius)
  {
    if (!SegmentHitsCircle(position, target, center, radius)) return target;
    float steerRadius = radius * SteeringMargin;
    var fromCenter = position - center;
    var toTarget = target - center;
    float distance = fromCenter.Length();
    // Go round the short way: turn from the ship's side of the planet toward the target's.
    float side = fromCenter.X * toTarget.Y - fromCenter.Y * toTarget.X >= 0f ? 1f : -1f;
    float angle = MathF.Atan2(fromCenter.Y, fromCenter.X);
    angle += distance > steerRadius * 1.02f
      ? side * MathF.Acos(steerRadius / distance) // tangent point seen from the ship
      : side * OrbitStep;                          // already close: follow the orbit
    return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * steerRadius;
  }

  public static bool SegmentHitsCircle(Vector2 start, Vector2 end, Vector2 center, float radius)
  {
    var segment = end - start;
    float lengthSquared = segment.LengthSquared();
    float t = lengthSquared > 1e-4f
      ? Math.Clamp(Vector2.Dot(center - start, segment) / lengthSquared, 0f, 1f)
      : 0f;
    return Vector2.DistanceSquared(start + segment * t, center) < radius * radius;
  }
}
