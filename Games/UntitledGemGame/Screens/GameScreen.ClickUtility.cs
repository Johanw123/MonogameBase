using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private Vector2 gemPointerScreen;
  private Vector2 gemPointerWorld;
  private Rectangle gemPointerViewport;
  private bool rightPointerHeld;
  internal bool WorldClickTriggered { get; private set; }

  private void UpdateWorldClickInput(float seconds)
  {
    var mouse = GameInput.Mouse;
    WorldClickTriggered = ClickUtility.ShouldClick(
      mouse.WasButtonPressed(MonoGame.Extended.Input.MouseButton.Left),
      mouse.IsButtonDown(MonoGame.Extended.Input.MouseButton.Left),
      GemClickInputEnabled && !mouse.IsButtonDown(MonoGame.Extended.Input.MouseButton.Right),
      seconds, UpgradeManager.Instance.UG, UpgradeManager.Instance.Signals, UpgradeManager.Instance.UGM);
  }

  internal bool GemClickInputEnabled => GameplayInputEnabled
    && GumService.Default.Cursor.Y < HudLayout.ContentBottom && !DamagePanelUnderCursor && !FeedbackHudUnderCursor
    && !ShipTerminalUnderCursor && !panelShortcutGestureCaptured;

  internal float GemClickRadius => UntitledGemGame.ClickUtility.TargetRadius(
    Entities.Gem.Footprint.X, Entities.Gem.Footprint.Y, SignalStats.ClickRadius);
  internal Vector2 GemPointerWorld => gemPointerWorld;

  // ViewportAdapter.Viewport reads GraphicsDevice.Viewport. Capture the window
  // viewport during update, before drawing switches it to the virtual target.
  internal void CaptureGemPointer(Vector2 pointer, Rectangle viewport, bool rightHeld)
  {
    gemPointerScreen = pointer;
    gemPointerWorld = m_camera.ScreenToWorld(pointer);
    gemPointerViewport = viewport;
    rightPointerHeld = rightHeld;
  }

  private void DrawGemClickRadius()
  {
    if (!GemClickInputEnabled || gemPointerViewport.Width <= 0 || gemPointerViewport.Height <= 0) return;
    var upgrades = UpgradeManager.Instance.UG;
    // While right is held the belt's intake ring sits under the pointer (DrawTractorBelt).
    if (rightPointerHeld && upgrades.TractorBeltEnabled)
    {
      ClickUtility.AcknowledgeHoldVisual();
      return;
    }
    var targetSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
    var center = UntitledGemGame.ClickUtility.PointerToTarget(gemPointerScreen, gemPointerViewport, targetSize);
    var projection = m_camera.ViewProjection();
    var origin = Vector3.Transform(Vector3.Zero, projection);
    var right = Vector3.Transform(new Vector3(GemClickRadius, 0, 0), projection);
    var down = Vector3.Transform(new Vector3(0, GemClickRadius, 0), projection);
    var radius = new Vector2(Math.Abs(right.X - origin.X) * targetSize.X / 2,
      Math.Abs(down.Y - origin.Y) * targetSize.Y / 2);
    float pixel = targetSize.X / gemPointerViewport.Width;
    m_shapeBatch.Begin(Matrix.Identity,
      Matrix.CreateOrthographicOffCenter(0, targetSize.X, targetSize.Y, 0, 0, 1), blendState: BlendState.AlphaBlend);
    ClickCursorVisual.Draw(m_shapeBatch, center, radius, pixel,
      upgrades.HoldClickEnabled && ClickUtility.IsHolding,
      ClickUtility.HoldVisualFill, ClickUtility.HoldActivationGlow);
    m_shapeBatch.End();
    ClickUtility.AcknowledgeHoldVisual();
  }
}
