using System;
using AsyncContent;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  public bool SalvageInputCaptured { get; private set; }
  private ShipModule revealingModule;
  private float moduleRevealAge;
  private bool moduleRevealSoundPlayed;

  // Focused modal popup centered over the live Shipyard panel.
  private static Rectangle ModuleRevealPanel => new(HudLayout.Width / 2 - 560, 480, 1120, 920);

  private static Rectangle RevealSingleButton => new(ModuleRevealPanel.Center.X - 210, ModuleRevealPanel.Bottom - 118, 420, 74);
  private static Rectangle InspectModuleButton => DiscoveryRevealButton;
  private static Rectangle RevealContinue => ModuleInventory.PendingReveals.Count > 1
    ? new(ModuleRevealPanel.Center.X - 410, ModuleRevealPanel.Bottom - 118, 390, 74)
    : RevealSingleButton;
  private static Rectangle RevealShipyard => ModuleInventory.PendingReveals.Count > 1
    ? new(ModuleRevealPanel.Center.X + 20, ModuleRevealPanel.Bottom - 118, 390, 74)
    : RevealSingleButton;
  private static Rectangle RevealSkip => RevealSingleButton;

  private Vector2 SalvageCursor
  {
    get
    {
#if !KNI_WEB
      if (UntitledGemGame.Capture.CaptureSession.Active) return GameInput.UiCursor;
      if (IsDetached)
        return Vector2.Transform(GameInput.Mouse.Position.ToVector2(), popout.InputTransform());
#endif
      var viewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      return Vector2.Transform(GameInput.Mouse.Position.ToVector2() - new Vector2(viewport.X, viewport.Y),
        Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix()));
    }
  }

  private float ModuleRevealDuration => ModuleCatalog.Rarities[(int)revealingModule] switch
  {
    ModuleRarity.Mythic => 4.2f,
    ModuleRarity.Legendary => 3.4f,
    ModuleRarity.Epic => 2.4f,
    ModuleRarity.Rare => 1.65f,
    _ => 1.1f
  };

  private void BeginModuleReveal()
  {
    revealingModule = ModuleInventory.PendingReveals.Count > 0 ? ModuleInventory.PendingReveals[0] : ShipModule.None;
    moduleRevealAge = 0;
    moduleRevealSoundPlayed = false;
    AudioManager.Instance?.PlaySound(SoundEvent.ModuleRevealStart);
  }

  private Color GetEscalatingRevealColor(ModuleRarity targetRarity, float progress)
  {
    if (progress >= 0.72f) return ModuleRarityColor(targetRarity);
    float t = Math.Clamp(progress / 0.72f, 0f, 1f);
    if (targetRarity == ModuleRarity.Common)
      return Color.Lerp(GuiSkin.PanelAccent, ModuleRarityColor(ModuleRarity.Common), t);
    float tierFloat = t * (int)targetRarity;
    int low = Math.Clamp((int)MathF.Floor(tierFloat), 0, (int)targetRarity);
    int high = Math.Clamp(low + 1, 0, (int)targetRarity);
    return Color.Lerp(ModuleRarityColor((ModuleRarity)low), ModuleRarityColor((ModuleRarity)high), tierFloat - low);
  }

  private bool UpdateModuleSalvageInput(float dt)
  {
    if (GameMain.IsPaused || UntitledGemGameGameScreen.Instance.m_prestiging
      || UntitledGemGameGameScreen.Instance.m_postPrestige
      || (IsDetached && !IsPopoutFocused)) return false;
    if (revealingModule != ShipModule.None && !ModuleInventory.PendingReveals.Contains(revealingModule))
      revealingModule = ShipModule.None;
    if (m_upgradeWindowType != UpgradeTypes.Shipyard) return false;

    var mouse = GameInput.Mouse;
    bool clicked = mouse.WasButtonPressed(MouseButton.Left);
    var keys = KeyboardExtended.GetState();

    // When no popup is open, only the inline "Reveal Discovery" button above Available Modules starts a reveal.
    if (revealingModule == ShipModule.None)
    {
      if (ModuleInventory.PendingReveals.Count > 0 && clicked && DiscoveryRevealButton.Contains(SalvageCursor))
      {
        CancelModuleDrag();
        BeginModuleReveal();
        SalvageInputCaptured = true;
        return true;
      }
      return false;
    }

    // Keep bottom HUD navigation usable while the popup is open. Leaving never consumes a module.
    if (clicked && !ShipyardPanel.Contains(SalvageCursor))
      return false;

    SalvageInputCaptured = true;
    moduleRevealAge += dt;
    bool ready = moduleRevealAge >= ModuleRevealDuration;

    if (!ready && ((clicked && ModuleRevealPanel.Contains(SalvageCursor))
      || keys.WasKeyPressed(Keys.Space) || keys.WasKeyPressed(Keys.Enter)))
    {
      moduleRevealAge = ModuleRevealDuration;
    }

    if (moduleRevealAge >= ModuleRevealDuration && !moduleRevealSoundPlayed)
    {
      moduleRevealSoundPlayed = true;
      var rarity = ModuleCatalog.Rarities[(int)revealingModule];
      AudioManager.Instance?.PlaySound(rarity >= ModuleRarity.Legendary ? SoundEvent.ModuleRevealLegendary
        : rarity == ModuleRarity.Epic ? SoundEvent.ModuleRevealEpic : SoundEvent.ModuleRevealCommon);
    }

    // A skip press reveals the module; a subsequent press claims it.
    if (ready)
    {
      bool multiplePending = ModuleInventory.PendingReveals.Count > 1;
      if (multiplePending && clicked && RevealContinue.Contains(SalvageCursor))
      {
        if (UntitledGemGameGameScreen.Instance.TryAcknowledgeModule(revealingModule))
          BeginModuleReveal();
      }
      else if ((clicked && (RevealShipyard.Contains(SalvageCursor) || (!multiplePending && RevealContinue.Contains(SalvageCursor)) || !ModuleRevealPanel.Contains(SalvageCursor)))
        || keys.WasKeyPressed(Keys.Escape)
        || (!multiplePending && (keys.WasKeyPressed(Keys.Space) || keys.WasKeyPressed(Keys.Enter))))
      {
        if (UntitledGemGameGameScreen.Instance.TryAcknowledgeModule(revealingModule))
          revealingModule = ShipModule.None;
      }
    }
    return true;
  }

  // Draws the inline Module Discovery progress strip and direct Reveal button above Available Modules.
  private void DrawInlineModuleDiscovery(SpriteBatch batch, Vector2 cursorPosition)
  {
    int pending = ModuleInventory.PendingReveals.Count;
    bool complete = ModuleInventory.CollectionComplete;
    float progress = ModuleInventory.DiscoveryThresholdSeconds > 0
      ? (float)Math.Clamp(ModuleInventory.DiscoveryProgressSeconds / ModuleInventory.DiscoveryThresholdSeconds, 0, 1) : 0;
    var rarity = ModuleInventory.DiscoveryRarity;
    Color discoveryAccent = rarity.HasValue ? ModuleRarityColor(rarity.Value) : GuiSkin.PanelAccent;
    Color socketAccent = pending > 0 ? GuiSkin.ConfirmAccent : complete ? GuiSkin.ConfirmAccent * 0.75f : discoveryAccent;

    var socket = new Rectangle(AvailableLeft, 222, 92, 92);
    int textX = AvailableLeft + 116;
    int barWidth = pending > 0
      ? DiscoveryRevealButton.Left - 36 - textX
      : AvailableLeft + AvailableWidth - textX;
    var track = new Rectangle(textX, 264, barWidth, 14);
    float time = (float)BaseGame.Time.TotalGameTime.TotalSeconds;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);

    if (pending > 0 || !complete)
    {
      float spinSpeed = pending > 0 ? 1.15f : 0.45f;
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", new Vector2(socket.Center.X, socket.Center.Y),
        socket.Width + 18, time * spinSpeed, socketAccent * (pending > 0 ? 0.82f : 0.48f));
    }
    GuiSkin.DrawHexSocket(batch, socket, socketAccent, active: pending > 0 || complete);

    if (complete && pending == 0)
    {
      GuiSkin.DrawHexPip(batch, new Rectangle(socket.Center.X - 14, socket.Center.Y - 14, 28, 28), GuiSkin.ConfirmAccent, true);
    }

    batch.Draw(pixel, track, GuiSkin.PanelColor);
    if (complete && pending == 0)
    {
      GuiSkin.ProgressFill(batch, track, 0f, 1f, GuiSkin.ConfirmAccent * 0.65f);
    }
    else if (pending > 0 && (complete || progress <= 0f))
    {
      GuiSkin.ProgressFill(batch, track, 0f, 1f, GuiSkin.ConfirmAccent);
    }
    else if (progress > 0f)
    {
      GuiSkin.ProgressFill(batch, track, 0f, progress, discoveryAccent);
    }

    // Subtle separator line between the inline Discovery section and the Available Modules header below
    batch.Draw(pixel, new Rectangle(AvailableLeft, 334, AvailableWidth, 1), GuiSkin.BorderColor * 0.28f);
    batch.End();

    if (!(complete && pending == 0))
    {
      DrawShipyardLabelCentered(pending > 0 ? "!" : "?", new Vector2(socket.Center.X, socket.Center.Y), 40,
        pending > 0 ? GuiSkin.ConfirmAccent : discoveryAccent);
    }

    if (pending > 0)
    {
      string readyTitle = pending == 1
        ? Loc.T("SEALED MODULE READY TO REVEAL")
        : Loc.Upper(Loc.P(pending, "{0} sealed module ready to reveal", "{0} sealed modules ready to reveal"));
      ShipyardLabel(readyTitle, new Vector2(textX, 222), 26, GuiSkin.ConfirmAccent);

      if (!complete && progress > 0f)
      {
        string nextTrace = rarity.HasValue
          ? Loc.F("{0} TRACE • {1}%", ModuleRarityLabel(rarity.Value), (int)MathF.Round(progress * 100))
          : $"{(int)MathF.Round(progress * 100)}%";
        float nextW = Measure2(nextTrace, Vector2.Zero, 21).X;
        ShipyardLabel(nextTrace, new Vector2(track.Right - nextW, 226), 21, discoveryAccent);
      }

      ShipyardLabel(Loc.T("A sealed module awaits inspection. Reveal it directly to add it to your available modules."),
        new Vector2(textX, 288), 21, GuiSkin.ButtonTextColor);

      string btnLabel = pending > 1
        ? Loc.F("Reveal Discovery ({0})", pending)
        : Loc.T("Reveal Discovery");
      bool btnHover = revealingModule == ShipModule.None && DiscoveryRevealButton.Contains(cursorPosition);
      DrawHudButton(batch, DiscoveryRevealButton, btnLabel, GuiSkin.ConfirmAccent,
        true, btnHover, 0f, badge: true, confirm: true);
    }
    else if (complete)
    {
      ShipyardLabel(Loc.T("MODULE DISCOVERY COMPLETE"), new Vector2(textX, 222), 26, GuiSkin.ConfirmAccent);
      string pct = "100%";
      float pctW = Measure2(pct, Vector2.Zero, 23).X;
      ShipyardLabel(pct, new Vector2(track.Right - pctW, 224), 23, GuiSkin.ConfirmAccent);
      ShipyardLabel(Loc.T("Every module has been discovered"), new Vector2(textX, 288), 21, GuiSkin.MutedTextColor);
    }
    else
    {
      string traceTitle = rarity.HasValue
        ? Loc.F("{0} MODULE TRACE", ModuleRarityLabel(rarity.Value))
        : Loc.T("SEARCHING FOR A TRACE");
      ShipyardLabel(traceTitle, new Vector2(textX, 222), 26, discoveryAccent);

      string stage = progress < 0.2f ? Loc.T("Searching the debris")
        : progress < 0.5f ? Loc.T("A faint trace emerges")
        : progress < 0.8f ? Loc.T("Isolating an unknown signature")
        : Loc.T("Closing in on the source");
      float titleW = Measure2(traceTitle, Vector2.Zero, 26).X;
      ShipyardLabel($"•   {stage}", new Vector2(textX + titleW + 20, 225), 22, GuiSkin.MutedTextColor);

      string pct = $"{(int)MathF.Round(progress * 100)}%";
      float pctW = Measure2(pct, Vector2.Zero, 26).X;
      ShipyardLabel(pct, new Vector2(track.Right - pctW, 222), 26, discoveryAccent);

      ShipyardLabel(Loc.T("Harvest gems or scan for signals to strengthen the trace."),
        new Vector2(textX, 288), 21, GuiSkin.MutedTextColor);
    }
  }

  private void RevealLabel(string text, float y, float size, Color color)
    => ShipyardLabel(text, new Vector2(ModuleRevealPanel.Center.X - Measure2(text, Vector2.Zero, size).X / 2, y), size, color);

  private void DrawModuleReveal(SpriteBatch batch)
  {
    if (revealingModule == ShipModule.None) return;
    drawingRevealPopup = true;
    try
    {
      DrawModuleRevealCore(batch);
    }
    finally
    {
      drawingRevealPopup = false;
    }
  }

  private void DrawModuleRevealCore(SpriteBatch batch)
  {
    var panel = ModuleRevealPanel;
    float progress = Math.Clamp(moduleRevealAge / ModuleRevealDuration, 0f, 1f);
    bool ready = progress >= 1f;
    var rarity = ModuleCatalog.Rarities[(int)revealingModule];
    Color accent = ready ? ModuleColor(revealingModule) : GetEscalatingRevealColor(rarity, progress);
    var center = new Vector2(panel.Center.X, panel.Y + 292);
    var device = batch.GraphicsDevice;
    var pixel = GuiSkin.Pixel(device);
    var glow = GuiSkin.GetTexture(device, "synty_fx_glow_01");

    // 1. Darken the Shipyard behind the modal popup while keeping the Shipyard visible around it.
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(pixel, ShipyardPanel, Color.Black * 0.74f);
    batch.Draw(glow, new Rectangle(panel.X - 120, panel.Y - 100, panel.Width + 240, panel.Height + 200),
      accent * (ready ? 0.28f : 0.16f + progress * 0.12f));
    batch.Draw(pixel, panel, new Color(4, 10, 18));
    GuiSkin.TechCard(batch, panel, GuiPalette.Base(new Color(5, 13, 24, 248)), accent * 0.95f, cornerGraphic: true, hexWatermark: true);
    DrawCleanHorizDivider(batch, panel.X + 72, panel.Y + 92, panel.Width - 144, accent * 0.72f, leadingPip: true);
    batch.End();

    RevealLabel(ready ? Loc.T("MODULE DISCOVERED") : Loc.T("UNKNOWN MODULE RECOVERED"), panel.Y + 32, 32, accent);

    // 2. Holographic rings, scanner reticle, quantum sparks, and breach shockwaves.
    DrawModuleRevealEffects(batch, center, rarity, progress);

    if (!ready)
    {
      float release = Math.Clamp((progress - 0.68f) / 0.32f, 0f, 1f);
      float releaseEase = release * release * (3f - 2f * release);
      int spread = (int)(releaseEase * (170 + (int)rarity * 24));
      float shake = MathF.Sin(moduleRevealAge * 68f) * (0.25f + progress * 0.75f) * (1f - release * 0.5f) * (2f + (int)rarity);
      var podCenter = new Vector2(center.X + shake, center.Y);

      // Growing core light and emerging hex socket inside the parting containment shutters
      int coreHexSize = (int)(120 + releaseEase * 96);
      float shutterAlpha = 1f - releaseEase * 0.55f;
      batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
      if (release > 0f)
      {
        int flareD = (int)(260 + releaseEase * 240);
        batch.Draw(glow, new Rectangle((int)podCenter.X - flareD / 2, (int)podCenter.Y - flareD / 2, flareD, flareD),
          Color.Lerp(accent, Color.White, 0.45f) * (releaseEase * 0.65f));
      }
      GuiSkin.DrawHexSocket(batch, new Rectangle((int)podCenter.X - coreHexSize / 2, (int)podCenter.Y - coreHexSize / 2, coreHexSize, coreHexSize),
        accent * (0.45f + releaseEase * 0.55f), active: true);
      batch.End();

      var left = new Rectangle((int)podCenter.X - 122 - spread, (int)podCenter.Y - 124, 118, 248);
      var right = new Rectangle((int)podCenter.X + 4 + spread, (int)podCenter.Y - 124, 118, 248);
      DrawModulePanel(batch, left, accent * shutterAlpha);
      DrawModulePanel(batch, right, accent * shutterAlpha);

      // Sweeping horizontal laser scan beam across the containment pod
      float scanSweep = MathF.Sin(moduleRevealAge * (6.5f + progress * 5f));
      int scanY = (int)(podCenter.Y + scanSweep * 98f);
      int scanHalfW = 148 + spread;
      batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
      batch.Draw(glow, new Rectangle((int)podCenter.X - scanHalfW, scanY - 18, scanHalfW * 2, 36), accent * (0.42f * (1f - release * 0.5f)));
      batch.Draw(pixel, new Rectangle((int)podCenter.X - scanHalfW + 18, scanY - 1, (scanHalfW - 18) * 2, 2),
        Color.Lerp(accent, Color.White, 0.65f) * (0.85f * (1f - release * 0.4f)));
      batch.End();

      RevealLabel("?", podCenter.Y - 46, 76, Color.Lerp(accent, Color.White, releaseEase * 0.5f));

      string stageText = progress < 0.38f ? Loc.T("Opening sealed module...")
        : progress < 0.72f ? Loc.T("Decoding module signature...")
        : Loc.T("Unsealing containment core...");
      RevealLabel(stageText, panel.Y + 492, 28, accent);

      var decodeTrack = new Rectangle(panel.Center.X - 280, panel.Y + 544, 560, 12);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.ProgressTrack(batch, decodeTrack);
      GuiSkin.ProgressFill(batch, decodeTrack, 0f, progress, accent);
      batch.End();

      RevealLabel($"{(int)MathF.Round(progress * 100)}%", panel.Y + 568, 22, GuiSkin.MutedTextColor);
      DrawHudButton(batch, RevealSkip, Loc.T("Skip reveal"), accent, false, RevealSkip.Contains(SalvageCursor), 0);
      return;
    }

    // 3. Revealed Module Showcase with elastic bounce + glowing hexagonal medallion
    float settle = Math.Clamp((moduleRevealAge - ModuleRevealDuration) / 0.48f, 0f, 1f);
    float bounce = 1f + 0.22f * MathF.Sin(settle * MathF.PI) * (1f - settle);
    int hexSize = (int)(224 * bounce);
    int iconSize = (int)(134 * bounce);
    float spin = (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.55f;

    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(glow, new Rectangle((int)center.X - 210, (int)center.Y - 210, 420, 420), accent * 0.34f);
    GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", center, hexSize + 36, spin, accent * 0.72f);
    GuiSkin.DrawHexSocket(batch, new Rectangle((int)center.X - hexSize / 2, (int)center.Y - hexSize / 2, hexSize, hexSize),
      Color.Lerp(accent, Color.White, (1f - settle) * 0.35f), active: true);
    batch.End();

    QueueModuleIcon(revealingModule, new Rectangle((int)center.X - iconSize / 2, (int)center.Y - iconSize / 2, iconSize, iconSize));
    FlushIcons(batch, SamplerState.PointClamp);

    string rarityText = ModuleRarityLabel(revealingModule);
    float rarityW = Measure2(rarityText, Vector2.Zero, 26).X;
    RevealLabel(rarityText, panel.Y + 462, 26, accent);
    RevealLabel(Loc.Upper(Loc.T(ModuleCatalog.Names[(int)revealingModule])), panel.Y + 504, 42, GuiSkin.StatHeadingColor);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.DrawHexPip(batch, new Rectangle(panel.Center.X - (int)(rarityW / 2) - 28, panel.Y + 468, 16, 16), accent, true);
    GuiSkin.DrawHexPip(batch, new Rectangle(panel.Center.X + (int)(rarityW / 2) + 12, panel.Y + 468, 16, 16), accent, true);
    batch.Draw(pixel, new Rectangle(panel.Center.X - 320, panel.Y + 568, 640, 2), accent * 0.55f);
    GuiSkin.DrawHexPip(batch, new Rectangle(panel.Center.X - 7, panel.Y + 562, 14, 14), accent, true);
    batch.End();

    float y = panel.Y + 592;
    string line = "";
    foreach (var (word, join) in Loc.WrapPieces(Loc.T(ModuleCatalog.Descriptions[(int)revealingModule])))
    {
      string next = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 28).X > panel.Width - 160)
      {
        RevealLabel(line, y, 28, GuiSkin.ButtonTextColor);
        y += 38;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0) RevealLabel(line, y, 28, GuiSkin.ButtonTextColor);

    if (ModuleInventory.PendingReveals.Count > 1)
    {
      DrawHudButton(batch, RevealContinue, Loc.F("Reveal Next ({0})", ModuleInventory.PendingReveals.Count - 1),
        accent, true, RevealContinue.Contains(SalvageCursor), 0, confirm: true);
      DrawHudButton(batch, RevealShipyard, Loc.T("Done"),
        accent, false, RevealShipyard.Contains(SalvageCursor), 0);
    }
    else
    {
      DrawHudButton(batch, RevealSingleButton, Loc.T("Claim Module"),
        accent, true, RevealSingleButton.Contains(SalvageCursor), 0, confirm: true);
    }
  }
}
