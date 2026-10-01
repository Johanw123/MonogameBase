using System;
using Apos.Shapes;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public static class ClickCursorVisual
{
  // The translucent disk grows from the pointer to the targeting boundary.
  // Its area follows cooldown progress; the boundary stays visible throughout.
  public static void Draw(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel,
    bool holding, float progress, float activation = 0)
  {
    if (holding && progress > 0)
    {
      float fillRadius = Math.Min(radius.X, radius.Y) * MathF.Sqrt(Math.Clamp(progress, 0, 1));
      batch.FillCircle(center, fillRadius, new Color(105, 210, 185, 72), pixel);
    }
    float glow = holding ? Math.Clamp(activation, 0, 1) : 0;
    var boundary = Color.Lerp(new Color(145, 210, 255, 160), new Color(190, 255, 235, 240), glow);
    Arc(batch, center, radius, 1, (1.5f + 0.6f * glow) * pixel, boundary, pixel);
  }

  private static void Arc(ShapeBatch batch, Vector2 center, Vector2 radius, float progress,
    float thickness, Color color, float feather)
  {
    if (progress <= 0) return;
    const int segments = 64;
    int count = Math.Max(1, (int)MathF.Ceiling(segments * progress));
    var previous = center - new Vector2(0, radius.Y);
    for (int i = 1; i <= count; ++i)
    {
      float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * Math.Min(i / (float)segments, progress);
      var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
      batch.FillLine(previous, next, thickness, color, feather);
      previous = next;
    }
  }

  public static void DrawGravity(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel,
    float progress, float activation, bool ready, float denied = 0)
  {
    if (progress > 0)
      batch.FillCircle(center, Math.Min(radius.X, radius.Y) * MathF.Sqrt(Math.Clamp(progress, 0, 1)),
        Color.Lerp(new Color(175, 135, 240, 48), new Color(255, 75, 85, 80),
          Math.Clamp(denied, 0, 1)), pixel);
    var boundary = Color.Lerp(ready ? new Color(200, 165, 255, 210) : new Color(170, 140, 215, 150),
      new Color(245, 225, 255, 250), Math.Clamp(activation, 0, 1));
    boundary = Color.Lerp(boundary, new Color(255, 75, 85, 240), Math.Clamp(denied, 0, 1));
    Arc(batch, center, radius, 1, (1.5f + Math.Max(activation, denied) * 0.6f) * pixel, boundary, pixel);
  }

  public static void DrawGravityWell(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel,
    float lifeProgress)
  {
    float fade = Math.Clamp((1 - lifeProgress) * 5, 0, 1);
    batch.FillCircle(center, 3 * pixel, new Color(220, 180, 255, (int)(180 * fade)), pixel);
    for (int i = 0; i < 2; ++i)
    {
      float phase = (lifeProgress * 3 + i * 0.5f) % 1;
      Arc(batch, center, radius * (1 - phase), 1, pixel,
        new Color(170, 135, 245, (int)(80 * fade * MathF.Sin(phase * MathHelper.Pi))), pixel);
    }
  }

  public static void DrawGravityReady(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel, float glow)
  {
    var color = new Color(225, 190, 255, 230) * Math.Clamp(glow, 0, 1);
    var pulseRadius = Vector2.Max(radius, new Vector2(18 * pixel)) * (1.1f + 0.5f * (1 - glow));
    Arc(batch, center, pulseRadius, 1, 2.5f * pixel, color, pixel);
    for (int i = 0; i < 4; i++)
    {
      float angle = MathHelper.PiOver4 + i * MathHelper.PiOver2;
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      batch.FillLine(center + direction * 5 * pixel, center + direction * 13 * pixel,
        2 * pixel, color, pixel);
    }
  }

  public static void DrawQuantumTouch(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel)
  {
    Arc(batch, center, radius, 1, 1.5f * pixel, new Color(220, 145, 255, 175), pixel);
    batch.FillCircle(center, 3f * pixel, new Color(220, 145, 255, 160), pixel);
  }

  public static void DrawEventHorizon(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel)
  {
    Arc(batch, center, radius, 1, 2f * pixel, new Color(245, 180, 255, 200), pixel);
    batch.FillCircle(center, Math.Min(radius.X, radius.Y), new Color(210, 130, 255, 25), pixel);
  }

  public static void DrawGravityCollapse(ShapeBatch batch, Vector2 center, Vector2 radius, float pixel, float glow)
  {
    Arc(batch, center, radius * (1f + (1f - glow) * 0.3f), 1, 3f * pixel,
      Color.Gold * glow, pixel);
  }
}
