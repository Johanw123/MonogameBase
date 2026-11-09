using System;
using Apos.Shapes;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// The Tractor Belt in world space, drawn additively over the field so it reads through dense
// gem piles. The belt itself (bed, rails, chevrons flowing toward the home base) and the
// intake's marks are signed-distance quads shaded by TractorBelt.fx; the pods are hexagons
// from the shape batch, drawn over them.
public static class TractorBeltVisual
{
  private static readonly Color Rail = new(70, 205, 255);
  private static readonly Color Bright = new(190, 245, 255);
  private static readonly Color Warning = new(255, 120, 70);
  private const float PodRadius = 7.5f;
  private const float MouthRadius = 6f;
  // Arc quads are this many radians wide; the shader keeps the curve exact inside them.
  private const float ArcQuadStep = 0.12f;

  // A shut-off belt dims a little while it reels in (TractorBelt.Retract).
  private static float Alpha(TractorBelt.Line line) => line.Expired ? 0.7f : 1f;

  private static Color RailColor(TractorBelt.Line line, float clock)
    => line.Warning && MathF.Sin(clock * 18f) > 0f ? Color.Lerp(Rail, Warning, 0.6f) : Rail;

  public static void AddBelt(TractorBeltRenderer renderer, TractorBelt.Line line, float intakeRadius,
    in TractorBeltWidths widths, float clock)
  {
    if (line.Pieces.Count == 0) return;
    float alpha = Alpha(line);
    var rail = RailColor(line, clock);
    // Style.w: the belt starts this far along, past what has been reeled in.
    var style = new Vector4(alpha, line.RouteLength, line.PodPhase, line.Retracted);
    float margin = widths.Margin;
    foreach (var piece in line.Pieces)
    {
      if (piece.Start + piece.Length <= line.Retracted) continue;
      if (!piece.IsArc)
      {
        var direction = piece.Length > 1e-4f ? (piece.B - piece.A) / piece.Length : Vector2.UnitX;
        var side = new Vector2(-direction.Y, direction.X) * margin;
        renderer.Quad(piece.A + side, piece.B + side, piece.A - side, piece.B - side,
          new Vector4(piece.A, piece.B.X, piece.B.Y), new Vector4(0, piece.Start, 0, 0), style, rail);
        continue;
      }
      // Wedges of an annulus; neighbours share their radial edges, as do the straight pieces
      // at the arc's ends, so no pixel is shaded twice.
      int steps = Math.Max(1, (int)MathF.Ceiling(Math.Abs(piece.Sweep) / ArcQuadStep));
      float step = piece.Sweep / steps;
      float inner = Math.Max(0f, piece.Radius - margin);
      // Chords cut inside the circle; push the outer edge out so they still clear the margin.
      float outer = (piece.Radius + margin) / MathF.Cos(Math.Abs(step) * 0.5f);
      var shape = new Vector4(piece.Center, piece.Radius, piece.StartAngle);
      var shape2 = new Vector4(1, piece.Start, Math.Sign(piece.Sweep), 0);
      for (int i = 0; i < steps; i++)
      {
        var from = Direction(piece.StartAngle + step * i);
        var to = Direction(piece.StartAngle + step * (i + 1));
        renderer.Quad(piece.Center + from * outer, piece.Center + to * outer,
          piece.Center + from * inner, piece.Center + to * inner, shape, shape2, style, rail);
      }
    }

    float pixel = widths.Pixel;
    float mouth = Math.Max(MouthRadius, 6f * pixel);
    float lineHalf = Math.Max(0.45f, 0.6f * pixel);
    float dot = Math.Max(1.6f, 2f * pixel);
    if (line.Expired)
    {
      // The reeling end: just the dot, running home ahead of the belt it pulls in.
      if (line.Retracted < line.RouteLength - TractorBelt.EndReach)
        renderer.Square(line.PointAlong(line.Retracted), dot * 4f + 2f * pixel, new Vector4(line.PointAlong(line.Retracted), 0.01f, 0),
          new Vector4(2, dot, lineHalf, 0), new Vector4(alpha, 0, 0, 0), rail);
      return;
    }
    var intake = line.Intake;
    float glow = line.PlaceGlow / TractorBelt.PlaceGlowSeconds;
    var lifeColor = (line.Warning ? Warning : Bright).ToVector3();
    // A permanent belt shows the run time full.
    float life = line.LifeFraction;
    renderer.Square(intake, mouth * 2.6f + 2f * pixel, new Vector4(intake, mouth, life),
      new Vector4(2, dot, lineHalf, glow), new Vector4(alpha, lifeColor.X, lifeColor.Y, lifeColor.Z), rail);
    // The catch area only shows while the intake is being placed or the pointer is over the
    // belt (Line.Highlight).
    float ring = line.Highlight * alpha;
    if (ring > 0.01f)
      renderer.Square(intake, intakeRadius + 3f * pixel, new Vector4(intake, intakeRadius, line.Travel * 0.004f),
        new Vector4(3, 0, Math.Max(0.4f, 0.5f * pixel), 0), new Vector4(ring, 0, 0, 0), rail);
  }

  public static void DrawPods(ShapeBatch batch, TractorBelt.Line line, float pixel, float clock)
  {
    float alpha = Alpha(line);
    var rail = RailColor(line, clock);
    float Width(float world, float screen) => Math.Max(world, screen * pixel);
    foreach (var pod in line.Pods)
    {
      var center = line.PodPosition(pod);
      var direction = line.DirectionAlong(line.RouteLength - pod.DistanceToEnd);
      float load = pod.Capacity > 0 ? Math.Clamp(pod.Count / (float)pod.Capacity, 0f, 1f) : 0f;
      float rotation = MathF.Atan2(direction.Y, direction.X);
      var border = Color.Lerp(rail * 0.6f, Bright, load) * alpha;
      batch.DrawHexagon(center, Width(PodRadius, 4.5f), rail * ((0.06f + 0.22f * load) * alpha), border,
        Width(0.7f, 0.8f), rotation: rotation, aaSize: pixel);
    }
  }

  private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
}
