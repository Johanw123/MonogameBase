using System;
using Apos.Shapes;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public static class CometVisual
{
  // Fixed geometry budget; no particle objects, textures, or per-frame allocations.
  public static void Draw(ShapeBatch batch, Vector2 tail, Vector2 head, Color color,
    float width, float opacity, float age, float feather)
  {
    Vector2 travel = head - tail;
    if (travel.LengthSquared() < 0.01f || opacity <= 0f) return;
    Vector2 direction = Vector2.Normalize(travel);
    Vector2 side = new(-direction.Y, direction.X);
    Color ice = Color.Lerp(color, Color.White, 0.65f);
    // Preserve RGB when fading: the shape shader already premultiplies by alpha.
    Color Tint(Color tint, float alpha) => new(tint.R, tint.G, tint.B,
      (byte)(255f * Math.Clamp(alpha * opacity, 0f, 1f)));

    const int segments = 12;
    Vector2 previous = tail;
    for (int i = 1; i <= segments; i++)
    {
      float t = i / (float)segments;
      float body = MathF.Sin(t * MathHelper.Pi * 0.8f);
      float ripple = MathF.Sin(t * 11f - age * 13f) * width * 0.2f * (1f - t);
      Vector2 next = Vector2.Lerp(tail, head, t) + side * ripple;
      float thickness = width * (0.12f + body * 0.8f);
      batch.FillLine(previous, next, thickness, Tint(color, t * 0.28f),
        Math.Max(feather, width * 0.8f));
      batch.FillLine(previous, next, thickness * 0.45f, Tint(color, t * 0.65f),
        Math.Max(feather, width * 0.2f));
      batch.FillLine(previous, next, thickness * 0.13f, Tint(ice, t * 0.9f), feather);
      previous = next;
    }

    // Sparse streaming embers widen the silhouette without a particle simulation.
    for (int i = 0; i < 6; i++)
    {
      float t = (i / 6f + age * 0.55f) % 1f;
      float offset = MathF.Sin(i * 7.3f) * width * (0.5f + (1f - t));
      Vector2 spark = Vector2.Lerp(tail, head, t) + side * offset;
      batch.FillLine(spark - direction * width * 0.65f, spark,
        Math.Max(0.6f, width * 0.055f), Tint(ice, 0.55f * t), feather);
    }

    batch.FillCircle(head, width * 0.7f, Tint(color, 0.25f), width * 1.2f);
    batch.FillCircle(head, width * 0.38f, Tint(color, 0.8f), width * 0.35f);
    batch.FillLine(head - direction * width * 0.35f, head, width * 0.32f,
      Tint(ice, 1f), feather);
    batch.FillCircle(head, width * 0.13f, Tint(Color.White, 1f), feather);
  }
}
