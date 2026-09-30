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
}
