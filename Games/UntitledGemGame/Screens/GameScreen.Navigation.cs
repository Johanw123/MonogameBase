using System;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

// Physical upgrade/navigation terminal mounted on the home base cruiser's left deck wing
// (prerendered from Synty SM_Prop_ControlPanel_04 + SM_Prop_Screen_06 in FleetAtlas.HullTerminal).
// Hovering the central hull highlights the console with an outline and tooltip;
// clicking the hull or pressing Tab expands its screen and shrinks it back on close.
public partial class UntitledGemGameGameScreen
{
  private static readonly Vector2 ShipTerminalMount = new(-14.5f, 1.5f);
  private const float ShipTerminalScaleMultiplier = 0.88f;
  private static readonly Color ShipTerminalCyan = new(45, 225, 255);
  private static readonly Color ShipTerminalAmber = new(255, 175, 55);

  private bool panelShortcutGestureCaptured;
  private float shipTerminalHoverAmount;
  private float shipTerminalPulse;
  private FleetAtlas shipTerminalHitAtlas;
  private Color[] shipTerminalHullPixels;

  internal bool ShipTerminalUnderCursor { get; private set; }

  internal Vector2 ShipTerminalWorldPosition => HullMount(ShipTerminalMount.X, ShipTerminalMount.Y);

  internal void TriggerShipTerminalPulse(float strength = 1f)
    => shipTerminalPulse = Math.Max(shipTerminalPulse, strength);

  private bool IsOnShipTerminal(Vector2 worldPosition)
  {
    if (!TurretsVisible || m_homeBaseEntity == null || planetConsumed) return false;
    float scale = Math.Max(0.01f, HullScale());
    var delta = (worldPosition - HullCenter()) / scale;
    float angle = HullRotation();
    if (angle != 0f)
      delta = Vector2.Transform(delta, Matrix.CreateRotationZ(-angle));
    // Most of the central hull opens the terminal. Leave the nose, engines and
    // outer wings available for gem collection, and keep the highlight on the console.
    var normalized = delta / new Vector2(21f, 37f);
    if (normalized.LengthSquared() > 1f) return false;

    // The hull has gaps between its wings and spine. An inset ellipse alone
    // would still catch empty space, so also require solid sprite coverage.
    var fleet = TextureCache.Fleet;
    var region = fleet.Region(FleetAtlas.PrerenderHomeBase).Bounds;
    if (shipTerminalHitAtlas != fleet)
    {
      shipTerminalHullPixels = new Color[region.Width * region.Height];
      fleet.Texture.GetData(0, region, shipTerminalHullPixels, 0, shipTerminalHullPixels.Length);
      shipTerminalHitAtlas = fleet;
    }
    var texel = fleet.Pivot(FleetAtlas.PrerenderHomeBase) + delta / PrerenderedShips.HomeBaseScale;
    int x = (int)MathF.Floor(texel.X), y = (int)MathF.Floor(texel.Y);
    const int inset = 2;
    if (x < inset || y < inset || x >= region.Width - inset || y >= region.Height - inset) return false;
    return shipTerminalHullPixels[y * region.Width + x].A >= 128
      && shipTerminalHullPixels[y * region.Width + x - inset].A >= 128
      && shipTerminalHullPixels[y * region.Width + x + inset].A >= 128
      && shipTerminalHullPixels[(y - inset) * region.Width + x].A >= 128
      && shipTerminalHullPixels[(y + inset) * region.Width + x].A >= 128;
  }

  internal bool TryGetShipTerminalHudBounds(out Rectangle hudRect, out Vector2 hudCenter)
  {
    if (m_homeBaseEntity == null || m_camera == null)
    {
      hudCenter = new Vector2(HudLayout.Width * 0.25f, HudLayout.Bottom * 0.5f);
      hudRect = new Rectangle((int)hudCenter.X - 36, (int)hudCenter.Y - 26, 72, 52);
      return false;
    }

    var worldCenter = ShipTerminalWorldPosition;
    var projection = m_camera.ViewProjection();
    var clipCenter = Vector3.Transform(new Vector3(worldCenter, 0f), projection);
    float scale = Math.Max(0.01f, HullScale());
    var clipRight = Vector3.Transform(new Vector3(worldCenter + new Vector2(5.6f * scale, 0f), 0f), projection);
    var clipDown = Vector3.Transform(new Vector3(worldCenter + new Vector2(0f, 4.0f * scale), 0f), projection);

    hudCenter = new Vector2(
      (clipCenter.X + 1f) * 0.5f * HudLayout.Width,
      (1f - clipCenter.Y) * 0.5f * HudLayout.Bottom);
    float halfW = Math.Max(28f, MathF.Abs(clipRight.X - clipCenter.X) * 0.5f * HudLayout.Width);
    float halfH = Math.Max(20f, MathF.Abs(clipDown.Y - clipCenter.Y) * 0.5f * HudLayout.Bottom);
    hudRect = new Rectangle(
      (int)MathF.Round(hudCenter.X - halfW),
      (int)MathF.Round(hudCenter.Y - halfH),
      (int)MathF.Round(halfW * 2f),
      (int)MathF.Round(halfH * 2f));
    return true;
  }

  private void UpdatePanelShortcut()
  {
    float dt = BaseGame.Time != null ? (float)BaseGame.Time.ElapsedGameTime.TotalSeconds : 1f / 60f;
    var mouse = GameInput.Mouse;
    if (!mouse.IsButtonDown(MouseButton.Left) && !mouse.IsButtonDown(MouseButton.Right))
      panelShortcutGestureCaptured = false;

    bool canInteract = GameplayInputAvailable && !FracturePaused && !_renderGuiSystem.ChoosingTalents
      && (!_renderGuiSystem.IsOverlayVisible || _renderGuiSystem.TryGetActivePanelBounds(out _, out _));

    bool canHoverTerminal = canInteract && !_renderGuiSystem.IsOverlayVisible
      && GumService.Default.Cursor.Y < HudLayout.ContentBottom
      && !DamagePanelUnderCursor && !FeedbackHudUnderCursor && !ExtractPanelHovered;

    bool hovered = false;
    if (canHoverTerminal && m_camera != null)
    {
      var pointerWorld = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      hovered = IsOnShipTerminal(pointerWorld);
    }

    if (hovered && !ShipTerminalUnderCursor)
      AudioManager.Instance.PlaySound(SoundEvent.MenuHover);
    ShipTerminalUnderCursor = hovered;

    float hoverTarget = hovered ? 1f : 0f;
    shipTerminalHoverAmount = MathHelper.Lerp(shipTerminalHoverAmount, hoverTarget, 1f - MathF.Exp(-dt * 16f));
    if (shipTerminalHoverAmount < 0.002f && hoverTarget == 0f)
      shipTerminalHoverAmount = 0f;
    shipTerminalPulse = Math.Max(0f, shipTerminalPulse - dt * 2.8f);

    if (!canInteract) return;

    if (hovered && mouse.WasButtonPressed(MouseButton.Left) && !mouse.IsButtonDown(MouseButton.Right))
    {
      panelShortcutGestureCaptured = true;
      ShipTerminalUnderCursor = false;
      ClickUtility.CancelHold();
      UpgradeManager.Instance.HideTooltip();
      TriggerShipTerminalPulse(1f);
      _renderGuiSystem.ToggleNavigationPanel();
      return;
    }

    var keyboard = KeyboardExtended.GetState();
    bool modifiers = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl)
      || keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
      || keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
    if (modifiers || !keyboard.WasKeyPressed(Keys.Tab)) return;
    panelShortcutGestureCaptured = true;
    ClickUtility.CancelHold();
    UpgradeManager.Instance.HideTooltip();
    TriggerShipTerminalPulse(1f);
    _renderGuiSystem.ToggleNavigationPanel();
  }

  private void DrawShipTerminalInBatch()
  {
    var fleet = TextureCache.Fleet;
    if (fleet == null) return;
    var region = fleet.Region(FleetAtlas.HullTerminal);
    float scale = TurretTexel * HullScale() * ShipTerminalScaleMultiplier;
    m_spriteBatch.Draw(fleet.Texture, ShipTerminalWorldPosition, region.Bounds, Color.White, HullRotation(),
      fleet.Pivot(FleetAtlas.HullTerminal), scale, SpriteEffects.None, 0f);
  }

  private void DrawShipTerminalOutlined()
  {
    var fleet = TextureCache.Fleet;
    if (fleet == null) return;
    float openGlow = _renderGuiSystem?.IsOverlayVisible == true ? 0.55f : 0f;
    float highlight = Math.Max(shipTerminalHoverAmount, Math.Max(shipTerminalPulse, openGlow));
    if (highlight <= 0.01f || EffectCache.HarvesterEffect == null || !EffectCache.HarvesterEffect.IsLoaded)
      return;

    var fx = EffectCache.HarvesterEffect.Value;
    float time = BaseGame.Time != null ? (float)BaseGame.Time.TotalGameTime.TotalSeconds : 0f;
    float pulse = 0.5f + 0.5f * MathF.Sin(time * 7f);
    var outlineColor = Color.Lerp(ShipTerminalCyan, Color.White, Math.Clamp(shipTerminalPulse * 0.65f + pulse * 0.2f, 0f, 1f))
      * Math.Clamp(highlight * 1.15f, 0f, 1f);

    fx.Parameters["view_projection"]?.SetValue(m_camera.ViewProjection());
    fx.Parameters["TexelSize"]?.SetValue(new Vector2(1f / fleet.Texture.Width, 1f / fleet.Texture.Height));
    fx.Parameters["_OutlineWidth"]?.SetValue((3.2f + 1.4f * shipTerminalHoverAmount + 1.8f * shipTerminalPulse) * RenderQuality.WorldScale);
    fx.Parameters["_OutlineColor"]?.SetValue(outlineColor.ToVector4());
    fx.Parameters["_TotalTime"]?.SetValue(time);

    // In HarvesterShader.fx, alpha < 0.35 brightens the sprite and draws a crisp 12-tap outline + halo;
    // alpha around 0.40 keeps normal hull brightness and breathes the outline.
    byte stateAlpha = shipTerminalHoverAmount > 0.25f || shipTerminalPulse > 0.2f ? (byte)76 : (byte)102;
    var region = fleet.Region(FleetAtlas.HullTerminal);
    float scale = TurretTexel * HullScale() * ShipTerminalScaleMultiplier;

    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      DepthStencilState.Default, RasterizerState.CullNone, effect: fx, transformMatrix: m_camera.GetViewMatrix());
    m_spriteBatch.Draw(fleet.Texture, ShipTerminalWorldPosition, region.Bounds,
      new Color((byte)255, (byte)255, (byte)255, stateAlpha), HullRotation(),
      fleet.Pivot(FleetAtlas.HullTerminal), scale, SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }

  private void DrawShipTerminalGlow(float feather)
  {
    if (!TurretsVisible || m_homeBaseEntity == null || planetConsumed || !GameStarted || m_prestiging || m_postPrestige)
      return;

    float time = BaseGame.Time != null ? (float)BaseGame.Time.TotalGameTime.TotalSeconds : 0f;
    float hullScale = HullScale();
    float angle = HullRotation();
    var center = ShipTerminalWorldPosition;
    var right = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    var down = new Vector2(-right.Y, right.X);

    float breathe = 0.5f + 0.5f * MathF.Sin(time * 4.2f);
    float hover = shipTerminalHoverAmount;
    float pulse = shipTerminalPulse;
    float activity = Math.Clamp(hover + pulse * 0.9f + (_renderGuiSystem?.IsOverlayVisible == true ? 0.45f : 0f), 0f, 1.3f);

    // Ambient + hover cyan screen glow over the angled telemetry display, plus a warm amber console LED glow.
    var screenCenter = center - down * (0.45f * hullScale);
    float glowRadius = (3.4f + 1.6f * activity + 0.4f * breathe) * hullScale;
    m_shapeBatch.FillCircle(screenCenter, glowRadius,
      ShipTerminalCyan * (0.16f + 0.06f * breathe + 0.24f * activity),
      Math.Max(feather, glowRadius * 0.95f));

    var ledPos = center - right * (2.1f * hullScale) + down * (1.5f * hullScale);
    m_shapeBatch.FillCircle(ledPos, 1.3f * hullScale,
      ShipTerminalAmber * (0.24f + 0.18f * breathe + 0.2f * activity),
      Math.Max(feather, 1.8f * hullScale));

    if (hover <= 0.01f && pulse <= 0.01f) return;

    // Animated targeting brackets around the physical terminal when hovered or activated.
    float bracketAlpha = Math.Clamp(hover * 0.85f + pulse * 0.75f, 0f, 1f);
    float spread = 1f + (1f - hover) * 0.18f + pulse * 0.22f;
    float halfW = 5.4f * hullScale * spread;
    float halfH = 3.9f * hullScale * spread;
    float arm = 2.1f * hullScale;
    float lineThickness = Math.Max(0.45f * hullScale, feather * 0.7f);
    var bracketColor = Color.Lerp(ShipTerminalCyan, Color.White, pulse * 0.5f) * bracketAlpha;

    for (int sx = -1; sx <= 1; sx += 2)
    {
      for (int sy = -1; sy <= 1; sy += 2)
      {
        var corner = center + right * (sx * halfW) + down * (sy * halfH);
        var armX = corner - right * (sx * arm);
        var armY = corner - down * (sy * arm);
        m_shapeBatch.FillLine(corner, armX, lineThickness, bracketColor, feather);
        m_shapeBatch.FillLine(corner, armY, lineThickness, bracketColor, feather);
      }
    }
  }

  private void DrawShipTerminalTooltip()
  {
    if (shipTerminalHoverAmount <= 0.02f || GameMain.IsPaused || !GameStarted
      || m_prestiging || m_postPrestige || _warpEscaping
      || _renderGuiSystem == null || _renderGuiSystem.IsOverlayVisible || _renderGuiSystem.ChoosingTalents
      || _renderGuiSystem.DrawingPopout)
      return;

    if (!TryGetShipTerminalHudBounds(out var termRect, out var termCenter))
      return;

    float alpha = Math.Clamp(shipTerminalHoverAmount, 0f, 1f);
    float slide = (1f - alpha) * 14f;
    string title = Loc.T("SHIP TERMINAL");
    string prompt = Loc.T("Click to open upgrade terminal  (Tab)");
    const float titleSize = 28f;
    const float promptSize = 24f;
    var titleMeasure = Measure2(title, Vector2.Zero, titleSize);
    var promptMeasure = Measure2(prompt, Vector2.Zero, promptSize);

    int padX = 28;
    int padY = 20;
    int width = (int)MathF.Ceiling(Math.Max(titleMeasure.X, promptMeasure.X)) + padX * 2 + 16;
    int height = (int)MathF.Ceiling(titleMeasure.Y + promptMeasure.Y + 12f) + padY * 2;

    int boxX = Math.Clamp((int)MathF.Round(termCenter.X - width * 0.5f), 32, HudLayout.Width - width - 32);
    int boxY = Math.Max(32, termRect.Top - height - 28 + (int)MathF.Round(slide));
    var box = new Rectangle(boxX, boxY, width, height);
    var accent = GuiSkin.PanelAccent;

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(GraphicsDevice);
    // Leader line connecting the physical ship terminal to the floating HUD tooltip.
    int leaderX = Math.Clamp((int)MathF.Round(termCenter.X), box.Left + 24, box.Right - 24);
    int leaderTop = box.Bottom;
    int leaderBottom = Math.Max(leaderTop + 4, termRect.Top - 4);
    m_spriteBatch.Draw(pixel, new Rectangle(leaderX - 1, leaderTop, 3, leaderBottom - leaderTop), accent * (0.65f * alpha));
    m_spriteBatch.Draw(pixel, new Rectangle(leaderX - 4, leaderBottom - 2, 9, 5), accent * (0.9f * alpha));

    GuiSkin.Panel(m_spriteBatch, box, header: false, accent * alpha);
    GuiSkin.DrawHexPip(m_spriteBatch, new Rectangle(box.Right - 34, box.Y + 18, 14, 14), accent * alpha, true);
    m_spriteBatch.End();

    float textX = box.X + padX + 6;
    float titleY = box.Y + padY;
    float promptY = titleY + titleMeasure.Y + 10f;
    FontManager.RenderFieldFont(HudFont, title, new Vector2(textX, titleY),
      accent * alpha, Color.Black * alpha, titleSize);
    FontManager.RenderFieldFont(HudFont, prompt, new Vector2(textX, promptY),
      GuiSkin.ButtonTextColor * alpha, Color.Black * alpha, promptSize);
  }
}
