using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace UntitledGemGame;

// Keep line cores and their soft glow outside the node's border, including routed
// paths whose first/last waypoint still lies inside a node.
internal static class UpgradeConnectionGeometry
{
  public static void TrimEndpoints(Vector2[] points, RectangleF start, RectangleF end, float gap)
  {
    if (points.Length < 2) return;
    start = new RectangleF(start.X - gap, start.Y - gap, start.Width + gap * 2, start.Height + gap * 2);
    end = new RectangleF(end.X - gap, end.Y - gap, end.Width + gap * 2, end.Height + gap * 2);
    Trim(points, start, false);
    Trim(points, end, true);
  }

  // Quadratic bends retain straight runs and cap the radius on short segments.
  public static Vector2[] RoundCorners(Vector2[] points, float radius)
  {
    var clean = new System.Collections.Generic.List<Vector2>();
    foreach (var point in points)
      if (clean.Count == 0 || Vector2.DistanceSquared(clean[^1], point) > 0.0001f) clean.Add(point);
    if (clean.Count < 3) return clean.ToArray();
    var result = new System.Collections.Generic.List<Vector2> { clean[0] };
    for (int i = 1; i < clean.Count - 1; i++)
    {
      var before = clean[i - 1] - clean[i];
      var after = clean[i + 1] - clean[i];
      if (Vector2.Dot(Vector2.Normalize(before), Vector2.Normalize(after)) < -0.9999f) continue;
      float distance = Math.Min(radius, Math.Min(before.Length(), after.Length()) * 0.4f);
      var entry = clean[i] + Vector2.Normalize(before) * distance;
      var exit = clean[i] + Vector2.Normalize(after) * distance;
      result.Add(entry);
      for (int step = 1; step <= 8; step++)
      {
        float t = step / 8f;
        result.Add((1 - t) * (1 - t) * entry + 2 * (1 - t) * t * clean[i] + t * t * exit);
      }
    }
    result.Add(clean[^1]);
    return result.ToArray();
  }

  // Adjacent strip edges use the same miter, so their glow never overlaps.
  public static Vector2 JoinOffset(Vector2[] points, int index, float halfWidth)
  {
    if (points.Length < 2) return new Vector2(0, halfWidth);
    var before = Vector2.Normalize(index == 0 ? points[1] - points[0] : points[index] - points[index - 1]);
    var after = index == points.Length - 1 ? before : Vector2.Normalize(points[index + 1] - points[index]);
    var normalBefore = new Vector2(-before.Y, before.X);
    var normalAfter = new Vector2(-after.Y, after.X);
    var sum = normalBefore + normalAfter;
    if (sum.LengthSquared() < 0.0001f) return normalAfter * halfWidth;
    var miter = Vector2.Normalize(sum);
    return miter * Math.Min(halfWidth / Math.Max(0.5f, Vector2.Dot(miter, normalAfter)), halfWidth * 2f);
  }

  public static void TrimBadge(Vector2[] points, RectangleF badge, bool atEnd)
    => Trim(points, badge, atEnd);

  private static bool Contains(RectangleF bounds, Vector2 point)
    => point.X >= bounds.Left && point.X <= bounds.Right
      && point.Y >= bounds.Top && point.Y <= bounds.Bottom;

  private static void Trim(Vector2[] points, RectangleF bounds, bool reverse)
  {
    int first = reverse ? points.Length - 1 : 0;
    int step = reverse ? -1 : 1;
    if (!Contains(bounds, points[first])) return;
    int outside = first + step;
    while (outside >= 0 && outside < points.Length && Contains(bounds, points[outside])) outside += step;
    if (outside < 0 || outside >= points.Length)
    {
      Array.Fill(points, points[first]);
      return;
    }
    var origin = points[outside - step];
    var direction = points[outside] - origin;
    float t = 1f;
    if (direction.X > 0) t = Math.Min(t, (bounds.Right - origin.X) / direction.X);
    else if (direction.X < 0) t = Math.Min(t, (bounds.Left - origin.X) / direction.X);
    if (direction.Y > 0) t = Math.Min(t, (bounds.Bottom - origin.Y) / direction.Y);
    else if (direction.Y < 0) t = Math.Min(t, (bounds.Top - origin.Y) / direction.Y);
    var edge = origin + direction * Math.Clamp(t, 0f, 1f);
    for (int i = first; i != outside; i += step) points[i] = edge;
  }
}
