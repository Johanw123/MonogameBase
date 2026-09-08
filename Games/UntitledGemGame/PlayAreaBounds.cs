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

  public static PlayAreaBounds ForCamera(OrthographicCamera camera)
  {
    var viewport = BaseGame.BoxingViewportAdapter.Viewport.Bounds;
    var screenBounds = GetScreenBounds(viewport, HudLayout.Height, HudLayout.Bottom);
    return new PlayAreaBounds(camera.ScreenToWorld(screenBounds.Minimum),
      camera.ScreenToWorld(screenBounds.Maximum));
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

  public PlayAreaBounds Inset(Vector2 halfSize)
  {
    var inset = Vector2.Min(Vector2.Max(Vector2.Zero, halfSize), (Maximum - Minimum) / 2);
    return new PlayAreaBounds(Minimum + inset, Maximum - inset);
  }

  public Vector2 Clamp(Vector2 position) => Vector2.Clamp(position, Minimum, Maximum);
}
