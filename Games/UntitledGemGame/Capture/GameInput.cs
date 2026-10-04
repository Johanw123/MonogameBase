using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;

namespace UntitledGemGame;

// Pointer input for world gameplay (clicking, holding, cursor gravity, manual abilities).
// Normally the real mouse; a capture session substitutes a scripted pointer.
public static class GameInput
{
#if KNI_WEB
  public static MouseStateExtended Mouse => MouseExtended.GetState();
  public static bool WindowActive => GameMain.Instance.IsActive;
  public static Vector2 UiCursor => new(Gum.GumService.Default.Cursor.X, Gum.GumService.Default.Cursor.Y);
#else
  public static MouseStateExtended Mouse => Capture.CaptureSession.Active
    ? Capture.CaptureSession.Pointer.State : MouseExtended.GetState();

  // The offscreen capture window never has focus, but scripted input must still count.
  public static bool WindowActive => Capture.CaptureSession.Active || GameMain.Instance.IsActive;

  // Pointer position over the custom-drawn HUD panels (shipyard, signals), in HUD canvas units.
  public static Vector2 UiCursor => Capture.CaptureSession.Active
    ? Capture.CaptureSession.Pointer.State.Position.ToVector2()
    : new Vector2(Gum.GumService.Default.Cursor.X, Gum.GumService.Default.Cursor.Y);
#endif
}
