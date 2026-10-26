using System;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private bool panelNavigationGestureCaptured;
  private int panelNavigationHovered = -1;
  private int lastNavigationTab = HudLayout.UpgradesTab;

  private RenderTarget2D m_terminalRenderTarget;
  private float terminalOpenProgress;
  private bool terminalTargetHasFrame;
  private const float TerminalOpenSeconds = 0.27f;
  private const float TerminalCloseSeconds = 0.21f;
  private static readonly Color TerminalAmber = new(255, 175, 55);
  private static readonly Color TerminalGreen = new(70, 235, 145);

  private static Rectangle TerminalMonitorBounds
  {
    get
    {
      var panel = PanelNavigationLayout.Panel;
      var strip = PanelNavigationLayout.TabStrip;
      return new Rectangle(panel.X - 28, strip.Y - 22, panel.Width + 56, (panel.Bottom - strip.Y) + 64);
    }
  }

  internal bool IsTerminalClosingAnimationActive
    => !drawUpgradesGui && !IsDetached && !ChoosingTalents && !GameMain.IsPaused
      && terminalOpenProgress > 0.001f && terminalTargetHasFrame
      && m_terminalRenderTarget is { IsDisposed: false };

  internal float EffectiveTerminalOverlayProgress
  {
    get
    {
      if (IsOverlayVisible)
        return EaseTerminalProgress(terminalOpenProgress, opening: true);
      if (IsTerminalClosingAnimationActive)
        return EaseTerminalProgress(terminalOpenProgress, opening: false);
      return 0f;
    }
  }

  private void DisposeTerminalRenderTarget()
  {
    m_terminalRenderTarget?.Dispose();
    m_terminalRenderTarget = null;
    terminalTargetHasFrame = false;
    terminalOpenProgress = 0f;
  }

  private RenderTarget2D EnsureTerminalRenderTarget(GraphicsDevice device)
  {
    int targetW = BaseGame._renderTargetHud?.Width ?? HudLayout.Width;
    int targetH = BaseGame._renderTargetHud?.Height ?? HudLayout.Bottom;
    if (m_terminalRenderTarget == null || m_terminalRenderTarget.IsDisposed
      || m_terminalRenderTarget.Width != targetW || m_terminalRenderTarget.Height != targetH)
    {
      m_terminalRenderTarget?.Dispose();
      m_terminalRenderTarget = new RenderTarget2D(
        device,
        targetW,
        targetH,
        mipMap: false,
        SurfaceFormat.Color,
        DepthFormat.Depth24Stencil8,
        preferredMultiSampleCount: 0,
        RenderTargetUsage.PreserveContents);
      terminalTargetHasFrame = false;
    }
    return m_terminalRenderTarget;
  }

  private void NotifyTerminalTransition(UpgradeTypes previous, UpgradeTypes next)
  {
    if (previous == next) return;
    var screen = UntitledGemGameGameScreen.Instance;
    bool betweenRuns = screen?.m_postPrestige == true || screen?.m_prestiging == true;
    bool instant = UntitledGemGame.Capture.CaptureSession.Active || betweenRuns || IsDetached;
    if (instant)
    {
      terminalOpenProgress = next != UpgradeTypes.None ? 1f : 0f;
      return;
    }

    if (previous == UpgradeTypes.None && next != UpgradeTypes.None)
    {
      if (terminalOpenProgress <= 0.01f)
        terminalOpenProgress = 0.02f;
      UntitledGemGameGameScreen.Instance?.TriggerShipTerminalPulse(1f);
    }
    else if (previous != UpgradeTypes.None && next == UpgradeTypes.None)
    {
      UntitledGemGameGameScreen.Instance?.TriggerShipTerminalPulse(0.85f);
    }
  }

  private void UpdateTerminalAnimation(float dt)
  {
    if (UntitledGemGame.Capture.CaptureSession.Active || ChoosingTalents || IsDetached)
    {
      terminalOpenProgress = drawUpgradesGui ? 1f : 0f;
      return;
    }

    float target = drawUpgradesGui ? 1f : 0f;
    if (MathF.Abs(terminalOpenProgress - target) <= 0.0005f)
    {
      terminalOpenProgress = target;
      return;
    }

    float duration = drawUpgradesGui ? TerminalOpenSeconds : TerminalCloseSeconds;
    float step = Math.Max(0f, dt) / duration;
    terminalOpenProgress = target > terminalOpenProgress
      ? Math.Min(target, terminalOpenProgress + step)
      : Math.Max(target, terminalOpenProgress - step);
  }

  private static float EaseTerminalProgress(float t, bool opening)
  {
    t = Math.Clamp(t, 0f, 1f);
    if (opening)
    {
      float inv = 1f - t;
      return 1f - inv * inv * inv;
    }
    return t * t * (3f - 2f * t);
  }

  private void BeginTerminalScreenCapture(GraphicsDevice device)
  {
    var target = EnsureTerminalRenderTarget(device);
    device.SetRenderTarget(target);
    device.Clear(Color.Transparent);
  }

  private void EndTerminalScreenCapture(SpriteBatch batch)
  {
    terminalTargetHasFrame = true;
    var device = batch.GraphicsDevice;
    device.SetRenderTarget(BaseGame._renderTargetHud);
    device.Clear(Color.Transparent);
    CompositeTerminalScreen(batch, opening: true);
  }

  private void CompositeTerminalScreen(SpriteBatch batch, bool opening)
  {
    if (m_terminalRenderTarget == null || m_terminalRenderTarget.IsDisposed) return;

    float raw = Math.Clamp(terminalOpenProgress, 0f, 1f);
    float t = EaseTerminalProgress(raw, opening);
    if (t <= 0.001f) return;

    var screen = UntitledGemGameGameScreen.Instance;
    Rectangle shipRect = default;
    Vector2 shipCenter = default;
    bool hasShipAnchor = screen != null && screen.TryGetShipTerminalHudBounds(out shipRect, out shipCenter);
    if (!hasShipAnchor)
    {
      shipCenter = new Vector2(HudLayout.Width * 0.24f, HudLayout.Bottom * 0.52f);
      shipRect = new Rectangle((int)shipCenter.X - 36, (int)shipCenter.Y - 26, 72, 52);
    }

    var monitor = TerminalMonitorBounds;
    var monitorCenter = monitor.Center.ToVector2();

    // Expand horizontally slightly faster than vertically for a CRT/holo terminal power-on feel.
    float horizT = Math.Clamp(t * 1.12f, 0f, 1f);
    float vertT = Math.Clamp((t - 0.05f) / 0.95f, 0f, 1f);
    vertT = vertT * vertT * (3f - 2f * vertT);

    float scaleX = MathHelper.Lerp(0.036f, 1f, horizT);
    float scaleY = MathHelper.Lerp(0.028f, 1f, vertT);
    var currentCenter = Vector2.Lerp(shipCenter, monitorCenter, t);

    var currentMonitor = new Rectangle(
      (int)MathF.Round(currentCenter.X - monitor.Width * 0.5f * scaleX),
      (int)MathF.Round(currentCenter.Y - monitor.Height * 0.5f * scaleY),
      Math.Max(16, (int)MathF.Round(monitor.Width * scaleX)),
      Math.Max(10, (int)MathF.Round(monitor.Height * scaleY)));

    var destRect = new Rectangle(
      (int)MathF.Round(currentCenter.X - monitorCenter.X * scaleX),
      (int)MathF.Round(currentCenter.Y - monitorCenter.Y * scaleY),
      Math.Max(1, (int)MathF.Round(HudLayout.Width * scaleX)),
      Math.Max(1, (int)MathF.Round(HudLayout.Bottom * scaleY)));

    var accent = GuiSkin.PanelAccent;
    float transitionGlow = t < 0.995f ? MathF.Sin(t * MathHelper.Pi) : 0f;
    float tetherAlpha = t < 0.995f ? Math.Clamp(0.25f + 0.65f * transitionGlow, 0f, 1f) : 0.14f;

    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);

    if (hasShipAnchor && tetherAlpha > 0.01f)
    {
      // Holographic projection beams linking the physical terminal on the ship hull to the terminal screen.
      DrawProjectionLine(batch, pixel, new Vector2(shipRect.Left, shipRect.Top),
        new Vector2(currentMonitor.Left, currentMonitor.Top), 2.5f, accent * (tetherAlpha * 0.55f));
      DrawProjectionLine(batch, pixel, new Vector2(shipRect.Right, shipRect.Top),
        new Vector2(currentMonitor.Right, currentMonitor.Top), 2.5f, accent * (tetherAlpha * 0.45f));
      DrawProjectionLine(batch, pixel, new Vector2(shipRect.Left, shipRect.Bottom),
        new Vector2(currentMonitor.Left, currentMonitor.Bottom), 2.5f, accent * (tetherAlpha * 0.55f));
      DrawProjectionLine(batch, pixel, new Vector2(shipRect.Right, shipRect.Bottom),
        new Vector2(currentMonitor.Right, currentMonitor.Bottom), 2.5f, accent * (tetherAlpha * 0.45f));

      // Anchor reticle around the physical ship terminal on the HUD layer while linked.
      DrawRectOutline(batch, pixel, shipRect, 2, accent * (tetherAlpha * 0.75f));
    }

    float contentAlpha = Math.Clamp((t - 0.04f) / 0.96f, 0f, 1f);
    batch.Draw(m_terminalRenderTarget, destRect, Color.White * contentAlpha);

    if (t < 0.995f)
    {
      // Crisp glowing holographic frame + CRT horizontal power-line flare during open/close zoom.
      DrawRectOutline(batch, pixel, currentMonitor, 3, Color.Lerp(accent, Color.White, 0.35f) * (0.4f + 0.55f * transitionGlow));
      if (t < 0.45f)
      {
        float flare = 1f - t / 0.45f;
        int flareH = Math.Max(3, (int)MathF.Round(10f * flare));
        var flareRect = new Rectangle(
          currentMonitor.X - 18,
          currentMonitor.Center.Y - flareH / 2,
          currentMonitor.Width + 36,
          flareH);
        batch.Draw(pixel, flareRect, Color.Lerp(accent, Color.White, 0.6f) * (0.65f * flare));
      }
    }
    batch.End();
  }

  private static void DrawProjectionLine(SpriteBatch batch, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color)
  {
    var delta = end - start;
    float length = delta.Length();
    if (length <= 0.5f) return;
    float rotation = MathF.Atan2(delta.Y, delta.X);
    batch.Draw(pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
  }

  private static void DrawRectOutline(SpriteBatch batch, Texture2D pixel, Rectangle rect, int thickness, Color color)
  {
    batch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
    batch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
    batch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
    batch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
  }

  private void DrawNavigationPanelBackground(SpriteBatch batch, Color accent)
  {
    var panel = PanelNavigationLayout.Panel;
    if (!IsDetached && !ChoosingTalents)
      DrawShipTerminalScreenFrame(batch, panel, accent);
    GuiSkin.Panel(batch, panel, header: false, accent);
    var watermark = PanelNavigationLayout.Watermark;
    GuiSkin.DrawScannerReticle(batch, watermark.Center, watermark.Width, accent, 0.2f);
    var icon = PanelNavigationIcon(batch.GraphicsDevice, CurrentNavigationTab);
    batch.Draw(icon, watermark.Center.ToVector2(), null, accent * 0.045f, 0,
      new Vector2(icon.Width, icon.Height) / 2, 240f / Math.Max(icon.Width, icon.Height), SpriteEffects.None, 0);
    if (!IsDetached && !ChoosingTalents)
      DrawShipTerminalScreenInterior(batch, panel, accent);
  }

  // Outer armored Synty terminal monitor casing (matching SM_Prop_ControlPanel_04 + SM_Prop_Screen_06).
  private void DrawShipTerminalScreenFrame(SpriteBatch batch, Rectangle panel, Color accent)
  {
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    var bezel = TerminalMonitorBounds;
    var strip = PanelNavigationLayout.TabStrip;

    Color outerHousing = new(9, 14, 22, 248);
    Color innerCasing = new(16, 25, 38, 252);
    Color rimHighlight = Color.Lerp( new Color(36, 54, 78), accent, 0.28f);

    // Outer armored console silhouette with stepped corner chamfers.
    batch.Draw(pixel, new Rectangle(bezel.X - 6, bezel.Y - 6, bezel.Width + 12, bezel.Height + 12), new Color(3, 6, 11, 220));
    batch.Draw(pixel, bezel, outerHousing);
    batch.Draw(pixel, new Rectangle(bezel.X + 4, bezel.Y + 4, bezel.Width - 8, bezel.Height - 8), innerCasing);
    DrawRectOutline(batch, pixel, bezel, 3, rimHighlight * 0.85f);
    DrawRectOutline(batch, pixel, new Rectangle(bezel.X + 8, bezel.Y + 8, bezel.Width - 16, bezel.Height - 16), 1, accent * 0.22f);

    // Top bezel rail above the navigation tab strip with Synty status LEDs (amber + cyan) and telemetry vents.
    var topRail = new Rectangle(panel.X, bezel.Y + 6, panel.Width, strip.Y - bezel.Y - 8);
    batch.Draw(pixel, topRail, new Color(11, 18, 28, 250));
    batch.Draw(pixel, new Rectangle(topRail.X + 12, topRail.Center.Y - 4, 18, 8), TerminalAmber);
    batch.Draw(pixel, new Rectangle(topRail.X + 38, topRail.Center.Y - 4, 18, 8), accent);
    batch.Draw(pixel, new Rectangle(topRail.X + 64, topRail.Center.Y - 4, 10, 8), TerminalGreen * 0.85f);
    for (int i = 0; i < 12; i++)
      batch.Draw(pixel, new Rectangle(topRail.Right - 220 + i * 14, topRail.Center.Y - 3, 8, 6), accent * (i < 9 ? 0.35f : 0.15f));

    // Side armor gussets flanking the screen.
    int sideY = panel.Y + 48;
    int sideH = panel.Height - 96;
    batch.Draw(pixel, new Rectangle(bezel.X + 10, sideY, 10, sideH), new Color(10, 16, 25, 255));
    batch.Draw(pixel, new Rectangle(bezel.Right - 20, sideY, 10, sideH), new Color(10, 16, 25, 255));
    batch.Draw(pixel, new Rectangle(bezel.X + 13, sideY + 32, 4, sideH - 64), accent * 0.24f);
    batch.Draw(pixel, new Rectangle(bezel.Right - 17, sideY + 32, 4, sideH - 64), accent * 0.24f);

    // Bottom hardware console deck (matching SM_Prop_ControlPanel_04's illuminated button strip).
    var deck = new Rectangle(panel.X, panel.Bottom + 4, panel.Width, bezel.Bottom - panel.Bottom - 12);
    batch.Draw(pixel, deck, new Color(10, 16, 26, 252));
    DrawRectOutline(batch, pixel, deck, 1, accent * 0.25f);

    // Left hardware button bank (amber/cyan/green console switches).
    Color[] leftButtons = [TerminalAmber, accent, TerminalAmber * 0.85f, TerminalGreen, accent * 0.75f];
    for (int i = 0; i < leftButtons.Length; i++)
    {
      var btn = new Rectangle(deck.X + 24 + i * 52, deck.Center.Y - 7, 38, 14);
      batch.Draw(pixel, new Rectangle(btn.X - 2, btn.Y - 2, btn.Width + 4, btn.Height + 4), new Color(5, 9, 15, 255));
      batch.Draw(pixel, btn, leftButtons[i] * 0.85f);
    }

    // Center optical telemetry bar.
    int centerBarW = 460;
    var centerBar = new Rectangle(deck.Center.X - centerBarW / 2, deck.Center.Y - 4, centerBarW, 8);
    batch.Draw(pixel, centerBar, accent * 0.18f);
    batch.Draw(pixel, new Rectangle(centerBar.X + 40, centerBar.Y + 2, centerBarW - 80, 4), accent * 0.65f);

    // Right status indicator pips.
    for (int i = 0; i < 6; i++)
    {
      var pip = new Rectangle(deck.Right - 240 + i * 34, deck.Center.Y - 5, 22, 10);
      batch.Draw(pixel, pip, (i % 2 == 0 ? accent : TerminalAmber) * 0.6f);
    }

    // Corner mounting bolts.
    ReadOnlySpan<Point> bolts =
    [
      new(bezel.Left + 14, bezel.Top + 12),
      new(bezel.Right - 22, bezel.Top + 12),
      new(bezel.Left + 14, bezel.Bottom - 20),
      new(bezel.Right - 22, bezel.Bottom - 20)
    ];
    foreach (var b in bolts)
    {
      batch.Draw(pixel, new Rectangle(b.X, b.Y, 8, 8), new Color(6, 10, 16, 255));
      batch.Draw(pixel, new Rectangle(b.X + 2, b.Y + 2, 4, 4), rimHighlight * 0.7f);
    }
  }

  // Subtle CRT scanlines and phosphor edge sheen inside the terminal screen.
  private void DrawShipTerminalScreenInterior(SpriteBatch batch, Rectangle panel, Color accent)
  {
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    Color scanline = accent * 0.014f;
    for (int y = panel.Top + 6; y < panel.Bottom - 6; y += 6)
      batch.Draw(pixel, new Rectangle(panel.X + 6, y, panel.Width - 12, 2), scanline);

    batch.Draw(pixel, new Rectangle(panel.X + 6, panel.Top + 4, panel.Width - 12, 10), accent * 0.045f);
    batch.Draw(pixel, new Rectangle(panel.X + 6, panel.Bottom - 14, panel.Width - 12, 10), accent * 0.035f);
  }

  private bool PanelNavigationShown
  {
    get
    {
      if (!drawUpgradesGui || ChoosingTalents || GameMain.IsPaused
        || (IsDetached && !DrawingPopout && !IsPopoutFocused)) return false;
      return m_upgradeWindowType switch
      {
        UpgradeTypes.Upgrades or UpgradeTypes.Abilities => true,
        UpgradeTypes.Meta => TalentsReadOnly,
        UpgradeTypes.Shipyard => revealingModule == ShipModule.None,
        UpgradeTypes.Signals => !signalChoicesVisible && !IsSelectingSignal,
        _ => false
      };
    }
  }

  private int CurrentNavigationTab => m_upgradeWindowType switch
  {
    UpgradeTypes.Upgrades => HudLayout.UpgradesTab,
    UpgradeTypes.Meta => HudLayout.TalentsTab,
    UpgradeTypes.Abilities => HudLayout.SystemsTab,
    UpgradeTypes.Shipyard => HudLayout.ShipyardTab,
    _ => HudLayout.SignalsTab
  };

  private static string PanelNavigationLabel(int tab) => tab switch
  {
    HudLayout.UpgradesTab => Loc.T("Upgrades"),
    HudLayout.TalentsTab => Loc.T("Talents"),
    HudLayout.SystemsTab => ShipSystems.NavigationLabel,
    HudLayout.ShipyardTab => Loc.T("Shipyard"),
    _ => Loc.T("Signals")
  };

  private Texture2D PanelNavigationIcon(GraphicsDevice graphics, int tab) => tab switch
  {
    HudLayout.UpgradesTab => AssetManager.Load<Texture2D>("Textures/scifi_icons/icon_arrow/14_arrow.png"),
    HudLayout.TalentsTab => GemSurfaceTexture.PrestigeOrbIcon(),
    HudLayout.SystemsTab => PowerCells.IconTexture(),
    HudLayout.ShipyardTab => GuiSkin.GetTexture(graphics, "synty_map_ship"),
    _ => AssetManager.Load<Texture2D>("Textures/scifi_icons/icon_accuracy/14_accuracy.png")
  };

  private bool UpdatePanelNavigation()
  {
    var mouse = GameInput.Mouse;
    if (panelNavigationGestureCaptured)
    {
      if (mouse.IsButtonDown(MouseButton.Left)) return true;
      panelNavigationGestureCaptured = false;
    }
    if (!PanelNavigationShown)
    {
      panelNavigationHovered = -1;
      return false;
    }
    var cursor = SalvageCursor;
    int hovered = -1;
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
      if (PanelNavigationLayout.Tab(tab).Contains(cursor)) { hovered = tab; break; }
    if (hovered != panelNavigationHovered && hovered >= 0 && CanOpenNavigation(hovered))
      AudioManager.Instance.PlaySound(SoundEvent.MenuHover);
    panelNavigationHovered = hovered;
    if (hovered < 0 || !mouse.WasButtonPressed(MouseButton.Left)) return false;

    // The whole gesture belongs to the tab strip, including disabled placeholders.
    panelNavigationGestureCaptured = true;
    PanelDismissInputCaptured = true;
    ClearGumCursorInput();
    if (hovered != CurrentNavigationTab && CanOpenNavigation(hovered))
      OpenNavigation(hovered);
    return true;
  }

  private void DrawPanelNavigation(SpriteBatch batch)
  {
    if (!PanelNavigationShown) return;
    var frame = PanelNavigationLayout.Panel;
    var accent = GuiSkin.Accent;
    var cursor = SalvageCursor;
    var strip = PanelNavigationLayout.TabStrip;
    var badgeRect = new Rectangle(frame.Right - 490, strip.Y + 18, 462, strip.Height - 28);

    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      var bounds = PanelNavigationLayout.Tab(tab);
      bool enabled = CanOpenNavigation(tab);
      bool selected = tab == CurrentNavigationTab;
      bool hovered = enabled && bounds.Contains(cursor);
      GuiSkin.NavigationTab(batch, bounds, accent, selected, hovered, enabled);
    }
    if (!IsDetached)
    {
      var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
      batch.Draw(pixel, badgeRect, new Color(8, 14, 23, 215));
      DrawRectOutline(batch, pixel, badgeRect, 1, accent * 0.32f);
      float time = BaseGame.Time != null ? (float)BaseGame.Time.TotalGameTime.TotalSeconds : 0f;
      float blink = 0.65f + 0.35f * MathF.Sin(time * 4.5f);
      batch.Draw(pixel, new Rectangle(badgeRect.X + 16, badgeRect.Center.Y - 6, 12, 12), TerminalGreen * blink);
      batch.Draw(pixel, new Rectangle(badgeRect.X + 34, badgeRect.Center.Y - 6, 8, 12), TerminalAmber * 0.85f);

      var close = GuiSkin.PanelCloseButtonBounds(frame, size: 56, marginX: 36, marginY: 24);
      GuiSkin.CloseButton(batch, close, accent, close.Contains(cursor));
    }
    batch.End();

    DrawPanelResources(batch, badgeRect.Left - 28, badgeRect.Center.Y);

    if (!IsDetached)
    {
      string badgeText = Loc.T("SHIP TERMINAL  •  TAB TO CLOSE");
      const float badgeFontSize = 21f;
      var badgeMeasure = Measure2(badgeText, Vector2.Zero, badgeFontSize);
      FontManager.RenderFieldFont(SystemFont, badgeText,
        new Vector2(badgeRect.X + 56, badgeRect.Center.Y - badgeMeasure.Y * 0.5f + 1f),
        GuiSkin.MutedTextColor, Color.Black, badgeFontSize);
    }

    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      if (!CanOpenNavigation(tab)) continue; // Neither names nor feature icons reveal a locked panel.
      var bounds = PanelNavigationLayout.Tab(tab);
      bool selected = tab == CurrentNavigationTab;
      bool hovered = bounds.Contains(cursor);
      int topInset = selected ? 4 : hovered ? 8 : 12;
      var tabRect = new Rectangle(bounds.X, bounds.Y + topInset, bounds.Width, bounds.Height - topInset);
      string label = PanelNavigationLabel(tab);
      float fontSize = 28f;
      var measured = Measure2(label, Vector2.Zero, fontSize);
      if (measured.X > tabRect.Width - 36)
      {
        fontSize *= (tabRect.Width - 36) / measured.X;
        measured = Measure2(label, Vector2.Zero, fontSize);
      }
      Color textColor = selected ? GuiSkin.SelectionAccent
        : hovered ? GuiSkin.HoverAccent
        : GuiSkin.MutedTextColor;
      FontManager.RenderFieldFont(SystemFont, label,
        new Vector2(tabRect.Center.X - measured.X / 2f, tabRect.Center.Y - measured.Y / 2f + 2f),
        textColor, Color.Black, fontSize);
    }
  }

  private void DrawPanelResources(SpriteBatch batch, float right, float centerY)
  {
    var game = UntitledGemGameGameScreen.Instance;
    var state = game?.State;
    string gemsText = state != null ? NumberFormatter.AbbreviateBigNumber(state.CurrentRedGemCount) : "0";
    string shardsText = state != null ? NumberFormatter.AbbreviateBigNumber(state.CurrentCoreShardCount) : "0";
    const float fontSize = 30f;
    var gemsMeasure = Measure2(gemsText, Vector2.Zero, fontSize);
    var shardsMeasure = Measure2(shardsText, Vector2.Zero, fontSize);
    float gemsTextX = right - gemsMeasure.X;
    float gemsIconX = gemsTextX - 28;
    float shardsTextX = gemsIconX - 48 - shardsMeasure.X;
    float shardsIconX = shardsTextX - 28;

    if (game != null)
    {
      batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
      game.DrawHudRedGem(batch, new Vector2(gemsIconX, centerY), 40f);
      game.DrawHudCoreShard(batch, new Vector2(shardsIconX, centerY), 40f);
      batch.End();
    }

    const string font = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);
    FontManager.RenderFieldFont(font, gemsText,
      new Vector2(gemsTextX, centerY - gemsMeasure.Y * 0.5f), GuiSkin.StatValueColor, Color.Black, fontSize);
    FontManager.RenderFieldFont(font, shardsText,
      new Vector2(shardsTextX, centerY - shardsMeasure.Y * 0.5f), CoreShards.Color, Color.Black, fontSize);
  }

  public void ToggleNavigationPanel()
  {
    if (drawUpgradesGui)
    {
      lastNavigationTab = CurrentNavigationTab;
      SetUpgradeType(UpgradeTypes.None);
    }
    else
      OpenNavigation(CanOpenNavigation(lastNavigationTab) ? lastNavigationTab : HudLayout.UpgradesTab);
  }

  public bool CanOpenNavigation(int tab) => !ChoosingTalents && tab switch
  {
    HudLayout.UpgradesTab => true,
    HudLayout.TalentsTab => TalentsTabShown,
    HudLayout.SystemsTab => ShipSystems.Online,
    HudLayout.ShipyardTab => UpgradeManager.Instance.UGM.ShipyardUnlocked,
    HudLayout.SignalsTab => UpgradeManager.Instance.UGM.SignalsUnlocked,
    _ => false
  };

  // Both navigation locations open the same windows, including the read-only Talents view.
  public bool OpenNavigation(int tab)
  {
    if (!CanOpenNavigation(tab)) return false;
    lastNavigationTab = tab;
    switch (tab)
    {
      case HudLayout.UpgradesTab:
        SetUpgradeType(UpgradeTypes.Upgrades);
        m_animateButtonClickUpgrades = 0.001f;
        break;
      case HudLayout.TalentsTab:
        SetUpgradeType(UpgradeTypes.Meta);
        m_talentsReadOnly = true;
        m_animateButtonClickTalents = 0.001f;
        break;
      case HudLayout.SystemsTab:
        SetUpgradeType(UpgradeTypes.Abilities);
        m_animateButtonClickAbilities = 0.001f;
        break;
      case HudLayout.ShipyardTab:
        SetUpgradeType(UpgradeTypes.Shipyard);
        m_animateButtonClickShipyard = 0.001f;
        break;
      case HudLayout.SignalsTab:
        SetUpgradeType(UpgradeTypes.Signals);
        m_animateButtonClickSignals = 0.001f;
        break;
    }
    return true;
  }
}
