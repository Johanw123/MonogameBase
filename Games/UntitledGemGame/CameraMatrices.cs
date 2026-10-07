using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace UntitledGemGame;

public static class CameraMatrices
{
  private sealed class Entry
  {
    public bool Valid;
    public Vector2 Position, Origin;
    public float Zoom, Rotation;
    public Matrix ViewProjection;
  }

  private static readonly ConditionalWeakTable<OrthographicCamera, Entry> entries = new();

  // The world-to-clip matrix the shaders take (GetBoundingFrustum().Matrix). Building a
  // frustum allocates it and its plane and corner arrays, and every effect asked each
  // frame; this recomputes only when the camera moves.
  public static Matrix ViewProjection(this OrthographicCamera camera)
  {
    var entry = entries.GetValue(camera, static _ => new Entry());
    if (!entry.Valid || entry.Position != camera.Position || entry.Origin != camera.Origin
      || entry.Zoom != camera.Zoom || entry.Rotation != camera.Rotation)
    {
      entry.ViewProjection = camera.GetBoundingFrustum().Matrix;
      entry.Position = camera.Position;
      entry.Origin = camera.Origin;
      entry.Zoom = camera.Zoom;
      entry.Rotation = camera.Rotation;
      entry.Valid = true;
    }
    return entry.ViewProjection;
  }
}
