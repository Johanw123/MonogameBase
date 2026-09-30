using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private Vector2 gemPointerScreen;
  private Rectangle gemPointerViewport;
  private bool rightPointerHeld;

  internal bool GemClickInputEnabled => ManualAbilityInputEnabled
    && Gum.GumService.Default.Cursor.Y < HudLayout.ContentBottom;

  internal float GemClickRadius => UntitledGemGame.ClickUtility.TargetRadius(
    TextureCache.HudRedGem.Value.Width, TextureCache.HudRedGem.Value.Height, SignalStats.ClickRadius);

  // ViewportAdapter.Viewport reads GraphicsDevice.Viewport. Capture the window
  // viewport during update, before drawing switches it to the virtual target.
  internal void CaptureGemPointer(Vector2 pointer, Rectangle viewport, bool rightHeld)
  {
    gemPointerScreen = pointer;
    gemPointerViewport = viewport;
    rightPointerHeld = rightHeld;
  }

  private void DrawGemClickRadius()
  {
    if ((!GemClickInputEnabled && !CursorGravity.IsActive)
      || gemPointerViewport.Width <= 0 || gemPointerViewport.Height <= 0) return;
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
    var upgrades = UpgradeManager.Instance.UG;
    if (CursorGravity.IsActive)
    {
      var projected = Vector3.Transform(new Vector3(CursorGravity.Position, 0), projection);
      var wellCenter = new Vector2((projected.X + 1) * targetSize.X / 2,
        (1 - projected.Y) * targetSize.Y / 2);
      ClickCursorVisual.DrawGravityWell(m_shapeBatch, wellCenter,
        radius * (CursorGravity.Radius / GemClickRadius), pixel, CursorGravity.LifeProgress);
    }
    if (GemClickInputEnabled && rightPointerHeld && upgrades.CursorGravityEnabled)
      ClickCursorVisual.DrawGravity(m_shapeBatch, center,
        radius * (CursorGravityWell.PreviewRadius(upgrades, GemClickRadius, UpgradeManager.Instance.Signals) / GemClickRadius), pixel,
        CursorGravity.ActivationPending ? 1 : CursorGravity.CooldownRemaining > 0 ? CursorGravity.RechargeProgress : 0,
        CursorGravity.ActivationGlow, CursorGravity.CooldownRemaining <= 0, CursorGravity.DenialGlow);
    else if (GemClickInputEnabled)
      ClickCursorVisual.Draw(m_shapeBatch, center, radius, pixel,
        upgrades.HoldClickEnabled && ClickUtility.IsHolding,
        ClickUtility.HoldVisualFill, ClickUtility.HoldActivationGlow);
    m_shapeBatch.End();
    ClickUtility.AcknowledgeHoldVisual();
    CursorGravity.AcknowledgeVisual();
  }
}
