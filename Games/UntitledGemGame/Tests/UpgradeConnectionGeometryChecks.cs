using System.Reflection;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame;

internal static class UpgradeConnectionGeometryChecks
{
  public static void Run()
  {
    var trim = typeof(GameMain).Assembly.GetType("UntitledGemGame.UpgradeConnectionGeometry")!
      .GetMethod("TrimEndpoints", BindingFlags.Static | BindingFlags.Public)!;
    void Trim(Vector2[] path, RectangleF start, RectangleF end)
      => trim.Invoke(null, new object[] { path, start, end, 8f });
    void Expect(Vector2 actual, Vector2 expected, string reason)
    {
      if (Vector2.Distance(actual, expected) > 0.001f) throw new Exception(reason + $": {actual} != {expected}");
    }
    var first = new RectangleF(0, 0, 80, 80);
    var last = new RectangleF(200, 0, 80, 80);
    Vector2[] horizontal = [new(40, 40), new(240, 40)];
    Trim(horizontal, first, last);
    Expect(horizontal[0], new(88, 40), "Horizontal source clears the border");
    Expect(horizontal[1], new(192, 40), "Horizontal destination clears the border");

    Vector2[] diagonal = [new(40, 40), new(240, 240)];
    Trim(diagonal, first, new RectangleF(200, 200, 80, 80));
    Expect(diagonal[0], new(88, 88), "Diagonal source clears the corner");
    Expect(diagonal[1], new(192, 192), "Diagonal destination clears the corner");

    Vector2[] routed = [new(40, 30), new(40, 70), new(40, 160), new(240, 160), new(240, 40)];
    Trim(routed, first, last);
    Expect(routed[0], new(40, 88), "Internal waypoints are trimmed from the source");
    Expect(routed[1], routed[0], "Internal source segment collapses");
    Expect(routed[2], new(40, 160), "External routing is preserved");
    Expect(routed[4], new(240, 88), "Routed destination clears its border");

    Vector2[] rectangle = [new(60, 20), new(220, 160)];
    Trim(rectangle, new RectangleF(0, 0, 120, 40), new RectangleF(200, 100, 40, 120));
    Expect(rectangle[0], new(92, 48), "Non-square source uses its actual height");
    Expect(rectangle[1], new(192, 135.5f), "Non-square destination uses its actual width");

    Vector2[] outside = [new(90, 40), new(240, 40)];
    Trim(outside, first, last);
    Expect(outside[0], new(90, 40), "An already external offset is preserved");

    Vector2[] coincident = [new(40, 40), new(40, 40), new(40, 40)];
    Trim(coincident, first, first);
    foreach (var point in coincident) Expect(point, new(40, 40), "Degenerate paths stay finite and collapsed");
    var geometry = trim.DeclaringType!;
    var round = geometry.GetMethod("RoundCorners", BindingFlags.Static | BindingFlags.Public)!;
    Vector2[] elbow = [new(0, 0), new(100, 0), new(100, 100)];
    var curved = (Vector2[])round.Invoke(null, new object[] { elbow, 14f })!;
    Expect(curved[0], elbow[0], "Rounding preserves the start");
    Expect(curved[^1], elbow[^1], "Rounding preserves the end");
    Expect(curved[1], new(86, 0), "Bend begins before the corner");
    Expect(curved[^2], new(100, 14), "Bend ends beyond the corner");
    if (curved.Contains(elbow[1])) throw new Exception("Sharp elbow must be replaced by a curve");
    Vector2[] repeated = [new(0, 0), new(0, 0), new(4, 0), new(4, 3)];
    var shortCurve = (Vector2[])round.Invoke(null, new object[] { repeated, 14f })!;
    foreach (var point in shortCurve)
      if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) throw new Exception("Short/repeated routes must stay finite");

    var join = geometry.GetMethod("JoinOffset", BindingFlags.Static | BindingFlags.Public)!;
    const float halfWidth = 7.8f;
    for (int i = 1; i < curved.Length - 1; i++)
    {
      var offset = (Vector2)join.Invoke(null, new object[] { curved, i, halfWidth })!;
      var incoming = Vector2.Normalize(curved[i] - curved[i - 1]);
      var outgoing = Vector2.Normalize(curved[i + 1] - curved[i]);
      float widthBefore = Vector2.Dot(offset, new Vector2(-incoming.Y, incoming.X));
      float widthAfter = Vector2.Dot(offset, new Vector2(-outgoing.Y, outgoing.X));
      if (Math.Abs(widthBefore - halfWidth) > .001f || Math.Abs(widthAfter - halfWidth) > .001f)
        throw new Exception("Shared curve joins must keep a constant width on both segments");
    }

    var badgeTrim = geometry.GetMethod("TrimBadge", BindingFlags.Static | BindingFlags.Public)!;
    Vector2[] nearBadge = [new(84, 40), new(192, 40)];
    badgeTrim.Invoke(null, new object[] { nearBadge, new RectangleF(68, -16, 28, 28), false });
    Expect(nearBadge[0], new(84, 40), "A corner badge does not extend the entire right edge");
    Vector2[] throughBadge = [new(84, 4), new(192, 4)];
    badgeTrim.Invoke(null, new object[] { throughBadge, new RectangleF(68, -16, 28, 28), false });
    Expect(throughBadge[0], new(96, 4), "Connections through the badge clear its own frame");
    Console.WriteLine("Upgrade connection geometry checks passed, including rounded corners, shared strip joins and separate badge clearance.");
  }
}
