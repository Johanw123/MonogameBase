using Microsoft.Xna.Framework;

// The same orthographic matrix used to draw is used for culling, including
// browser target scaling and detached-window pan/zoom.
internal static class RenderVisibility
{
  public static bool Intersects(float left, float top, float right, float bottom, in Matrix matrix)
  {
    var a = Vector2.Transform(new Vector2(left, top), matrix);
    var b = Vector2.Transform(new Vector2(right, top), matrix);
    var c = Vector2.Transform(new Vector2(left, bottom), matrix);
    var d = Vector2.Transform(new Vector2(right, bottom), matrix);
    return !(a.X < -1 && b.X < -1 && c.X < -1 && d.X < -1
      || a.X > 1 && b.X > 1 && c.X > 1 && d.X > 1
      || a.Y < -1 && b.Y < -1 && c.Y < -1 && d.Y < -1
      || a.Y > 1 && b.Y > 1 && c.Y > 1 && d.Y > 1);
  }
}
