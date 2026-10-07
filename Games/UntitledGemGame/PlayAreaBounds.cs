using JapeFramework;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace UntitledGemGame;

public readonly struct PlayAreaBounds
{
  public Vector2 Minimum { get; }
  public Vector2 Maximum { get; }

  public PlayAreaBounds(Vector2 minimum, Vector2 maximum)
  {
    Minimum = Vector2.Min(minimum, maximum);
    Maximum = Vector2.Max(minimum, maximum);
  }

  // ScreenToWorld inverts the camera's view matrix, and a burst of spawns asks for the same
  // bounds thousands of times in one frame. Everything the result depends on is in the key;
  // the entry is swapped whole, so fleet worker threads can read it safely.
  private sealed record CachedBounds(OrthographicCamera Camera, Vector2 Position, Vector2 Origin, float Zoom,
    float Rotation, Rectangle Viewport, int HudHeight, int HudBottom, PlayAreaBounds Bounds);
  private static CachedBounds cached;

  public static PlayAreaBounds ForCamera(OrthographicCamera camera)
  {
    var viewport = BaseGame.BoxingViewportAdapter.Viewport.Bounds;
#if KNI_WEB
    int hudHeight = HudLayout.Height;
#else
    // HUD-less captures use the whole frame as play area.
    int hudHeight = Capture.CaptureSession.ReserveHudSpace ? HudLayout.Height : 0;
#endif
    int hudBottom = HudLayout.Bottom;
    var entry = cached;
    if (entry != null && entry.Camera == camera && entry.Position == camera.Position && entry.Origin == camera.Origin
      && entry.Zoom == camera.Zoom && entry.Rotation == camera.Rotation && entry.Viewport == viewport
      && entry.HudHeight == hudHeight && entry.HudBottom == hudBottom)
      return entry.Bounds;
    var screenBounds = GetScreenBounds(viewport, hudHeight, hudBottom);
    var bounds = new PlayAreaBounds(camera.ScreenToWorld(screenBounds.Minimum),
      camera.ScreenToWorld(screenBounds.Maximum));
    cached = new CachedBounds(camera, camera.Position, camera.Origin, camera.Zoom, camera.Rotation, viewport,
      hudHeight, hudBottom, bounds);
    return bounds;
  }

  // Convert HUD units into window pixels before using ScreenToWorld, including letterboxing.
  public static PlayAreaBounds GetScreenBounds(Rectangle viewport, float hudHeight, float hudCanvasHeight)
  {
    float bottomInset = viewport.Height * hudHeight / hudCanvasHeight;
    return new PlayAreaBounds(new Vector2(viewport.Left, viewport.Top),
      new Vector2(viewport.Right, viewport.Bottom - bottomInset));
  }

  public PlayAreaBounds Inset(float radius)
    => Inset(new Vector2(radius));

  public PlayAreaBounds InsetForCollection(float spriteMargin, float collectionRange, float arrivalRadius)
  {
    // At a corner both axes contribute to the distance. Leave room for the
    // ship to stop short of its target and still collect a gem at the corner.
    float reachableInset = System.MathF.Max(0f, collectionRange - arrivalRadius) / System.MathF.Sqrt(2f);
    return Inset(System.MathF.Min(spriteMargin, reachableInset));
  }

  public PlayAreaBounds Inset(Vector2 halfSize)
  {
    var inset = Vector2.Min(Vector2.Max(Vector2.Zero, halfSize), (Maximum - Minimum) / 2);
    return new PlayAreaBounds(Minimum + inset, Maximum - inset);
  }

  public Vector2 Clamp(Vector2 position) => Vector2.Clamp(position, Minimum, Maximum);
}
