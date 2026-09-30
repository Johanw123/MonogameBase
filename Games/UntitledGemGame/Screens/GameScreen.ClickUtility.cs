using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private Vector2 gemPointerScreen;
  private Rectangle gemPointerViewport;

  internal bool GemClickInputEnabled => ManualAbilityInputEnabled
    && Gum.GumService.Default.Cursor.Y < HudLayout.ContentBottom;

  internal float GemClickRadius => UntitledGemGame.ClickUtility.TargetRadius(
    TextureCache.HudRedGem.Value.Width, TextureCache.HudRedGem.Value.Height, SignalStats.ClickRadius);

  // ViewportAdapter.Viewport reads GraphicsDevice.Viewport. Capture the window
  // viewport during update, before drawing switches it to the virtual target.
  internal void CaptureGemPointer(Vector2 pointer, Rectangle viewport)
  {
    gemPointerScreen = pointer;
    gemPointerViewport = viewport;
  }

  private void DrawGemClickRadius()
  {
    if (!GemClickInputEnabled || gemPointerViewport.Width <= 0 || gemPointerViewport.Height <= 0) return;
    var targetSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
    var center = UntitledGemGame.ClickUtility.PointerToTarget(gemPointerScreen, gemPointerViewport, targetSize);
    var projection = m_camera.GetBoundingFrustum().Matrix;
    var origin = Vector3.Transform(Vector3.Zero, projection);
    var right = Vector3.Transform(new Vector3(GemClickRadius, 0, 0), projection);
    var down = Vector3.Transform(new Vector3(0, GemClickRadius, 0), projection);
    var radius = new Vector2(Math.Abs(right.X - origin.X) * targetSize.X / 2,
      Math.Abs(down.Y - origin.Y) * targetSize.Y / 2);
    float pixel = targetSize.X / gemPointerViewport.Width;
    m_shapeBatch.Begin(Matrix.Identity,
      Matrix.CreateOrthographicOffCenter(0, targetSize.X, targetSize.Y, 0, 0, 1), blendState: BlendState.AlphaBlend);
    ClickCursorVisual.Draw(m_shapeBatch, center, radius, pixel,
      UpgradeManager.Instance.UG.HoldClickEnabled && ClickUtility.IsHolding,
      ClickUtility.HoldVisualFill, ClickUtility.HoldActivationGlow);
    m_shapeBatch.End();
    ClickUtility.AcknowledgeHoldVisual();
  }
}
