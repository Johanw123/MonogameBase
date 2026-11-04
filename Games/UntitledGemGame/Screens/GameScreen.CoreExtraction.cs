using System;
using System.Collections.Generic;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

// Ending a run is an attempt to extract the planet's core (CoreExtraction): the player
// holds the extraction control, a drill beam reaches the core, the core breaches and collapses
// into a black hole, and the ship is pulled into a new time loop. A repaired warp
// drive lets it escape instead, completing the playtest (GameScreen.WarpEnding).
public partial class UntitledGemGameGameScreen
{
  private static readonly Color ExtractionGlow = new(255, 200, 90);
  private ExtractionGuide extractionGuide = new();
  private float _extractHold;
  private bool _extractHolding;
  private readonly List<(string Text, float Size, Color Color)> _extractTooltipLines = new();

  // A core fracture holds commands still until its shard is out; extraction waits for it.
  private bool ExtractInputEnabled => GameStarted && !m_prestiging && !m_postPrestige && !FracturePaused
    && (GameInput.WindowActive || _renderGuiSystem.HasInputFocus)
    && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;

  private bool WarpEscapeReady => CoreExtraction.CanEscape(m_upgradeManager.UG.WarpDriveRepaired, m_gameState.EscapedThisRun);

  // Worth ending the run now. The Upgrades tab and the button say so; the call stays the player's.
  internal bool ExtractRecommended => !WarpEscapeReady
    && extractionGuide.Recommended(m_gameState.PendingPrestigePoints, m_gameState.CoreExtractions);

  private bool CanExtractCore => WarpEscapeReady || CoreExtraction.CanExtract(m_gameState.PendingPrestigePoints, m_gameState.CoreExtractions);

  private bool ExtractInUpgrades => _renderGuiSystem.drawUpgradesGui
    && _renderGuiSystem.m_upgradeWindowType == RenderGuiSystem.UpgradeTypes.Upgrades;
  private Rectangle ExtractControlBounds => ExtractInUpgrades
    ? RenderGuiSystem.UpgradesExtractButton : HudLayout.PrestigePanel;
  private bool ExtractControlVisible => (ExtractInUpgrades || HudLayout.BottomBarVisible)
    && (!_renderGuiSystem.IsDetached || _renderGuiSystem.DrawingPopout);
  private bool ExtractPanelHovered => ExtractInputEnabled && !panelShortcutGestureCaptured
    && !_renderGuiSystem.PanelDismissInputCaptured && (ExtractInUpgrades || HudLayout.BottomBarVisible)
    && (!_renderGuiSystem.IsDetached || _renderGuiSystem.IsPopoutFocused)
    && ExtractControlBounds.Contains(_renderGuiSystem.PanelPointer);

  internal void DrawUpgradesExtractControl()
  {
    if (!ExtractInUpgrades || !ExtractControlVisible) return;
    DrawExtractPanel(ExtractControlBounds);
  }

  internal void DrawUpgradesExtractTooltip()
  {
    if (ExtractInUpgrades)
      DrawExtractTooltip();
  }

  // Also used by capture scenes, which may extract regardless of the reward.
  public void ExtractCore()
  {
    if (m_prestiging || m_postPrestige || FracturePaused) return;
    _extractHolding = false;
    _extractHold = 0f;
    if (WarpEscapeReady)
    {
      BeginWarpEscape();
      return;
    }
    BeginPrestige();
    m_upgradeManager.ResetUpgrades();
    m_upgradeManager.ResetSystems();
    // Modules last one run; reset now so a save during the collapse is already clean.
    m_gameState.Modules.ResetRun(Random.Shared);
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
    RenderGuiSystem.Instance.ForgetView(RenderGuiSystem.UpgradeTypes.Upgrades);
    m_upgradeManager.HideTooltip();
    AudioManager.Instance.PlaySound(SoundEvent.CoreExtractionStart);
    SaveProgress();
  }

  // A deliberate hold in either navigation layout keeps a stray click from ending the run.
  private void UpdateExtractHold(float dt)
  {
    var mouse = GameInput.Mouse;
    bool held = ExtractPanelHovered && mouse.IsButtonDown(MouseButton.Left);
    if (held && !_extractHolding && mouse.WasButtonPressed(MouseButton.Left)
      && CanExtractCore)
      _extractHolding = true;
    if (!held || !_extractHolding)
    {
      _extractHolding = false;
      _extractHold = 0f;
      return;
    }
    _extractHold += dt;
    if (_extractHold >= CoreExtraction.HoldSeconds)
      ExtractCore();
  }

  // The bar is the next prestige point (PrestigeProgression): dim for what earlier loops
  // remember, bright for the last minute's gem income. It shows no numbers; the player
  // learns what charges it by watching it move.
  private void DrawExtractPanel(Rectangle panel)
  {
    if (GameMain.IsPaused || m_prestiging || m_postPrestige)
      return;

    float echo = (float)m_gameState.PrestigeEcho;
    float charge = (float)Math.Min(1, m_gameState.PrestigeEcho + m_gameState.PrestigeProgress);
    // After the first loop any run can end, but the panel only lights up with points to collect.
    bool ready = CanExtractCore && !FracturePaused;
    bool paying = m_gameState.PendingPrestigePoints > 0;
    bool hovered = ExtractPanelHovered;
    float hold = Math.Clamp(_extractHold / CoreExtraction.HoldSeconds, 0f, 1f);
    int padding = HudLayout.ProgressPanelPadding;
    int contentWidth = panel.Width - padding * 2;
    int contentOffsetY = ExtractInUpgrades ? 6 : 0;
    int titleY = panel.Y + HudLayout.ProgressTitleTop + contentOffsetY;
    var bar = new Rectangle(panel.X + padding, panel.Y + HudLayout.ProgressBarTop + contentOffsetY, contentWidth, 10);

    // A point waiting lights the button. When extracting is the smart move it calls out, and
    // repeats the Upgrades tab's ! so the player can see what that was about.
    bool waiting = ready && !WarpEscapeReady && ShownPrestigePoints > 0 && hold <= 0f;
    bool calling = waiting && ExtractRecommended;
    float breathe = 0.5f + 0.5f * MathF.Sin((float)BaseGame.Time.TotalGameTime.TotalSeconds * (calling ? 4.2f : 2.1f));

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(GraphicsDevice);
    if (calling)
    {
      var halo = GuiSkin.GetTexture(GraphicsDevice, "synty_fx_glow_01");
      m_spriteBatch.Draw(halo, new Rectangle(panel.X - 70, panel.Y - 60, panel.Width + 140, panel.Height + 120),
        PrestigePointColor * (0.3f + 0.35f * breathe));
    }
    m_spriteBatch.Draw(pixel, panel, hovered ? GuiSkin.ButtonHoverColor * 0.75f : GuiSkin.PanelColor * 0.3f);
    m_spriteBatch.Draw(pixel, bar, GuiSkin.BorderColor * 0.18f);
    if (prestigePanelFlash > 0f)
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panel, PrestigePointColor * (0.45f * prestigePanelFlash));
    if (waiting)
      GuiSkin.FlatPanel(m_spriteBatch, panel, PrestigePointColor * (calling ? 0.12f + 0.2f * breathe : 0.05f + 0.05f * breathe),
        PrestigePointColor * (calling ? 0.7f + 0.3f * breathe : 0.4f + 0.25f * breathe), calling ? 3 : 2);
    if (hold > 0f)
    {
      // The charge floods the panel and flickers harder as the core gives way.
      float flicker = 0.75f + 0.25f * MathF.Sin((float)BaseGame.Time.TotalGameTime.TotalSeconds * (12f + 30f * hold));
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(panel.X, panel.Y, (int)(panel.Width * hold), panel.Height), ExtractionGlow * (0.5f * flicker));
      m_spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * hold), bar.Height), ExtractionGlow);
    }
    else
    {
      int echoWidth = (int)(bar.Width * Math.Clamp(echo, 0f, 1f));
      int chargeWidth = (int)(bar.Width * Math.Clamp(charge, 0f, 1f));
      if (echoWidth > 0)
        m_spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, echoWidth, bar.Height), Color.White * 0.4f);
      if (chargeWidth > echoWidth)
        m_spriteBatch.Draw(pixel, new Rectangle(bar.X + echoWidth, bar.Y, chargeWidth - echoWidth, bar.Height), Color.White);
    }
    if (calling)
      GuiSkin.AlertMark(m_spriteBatch, new Point(panel.Right, panel.Top), 40);
    m_spriteBatch.End();

    string rewardText = WarpEscapeReady ? Loc.T("ESCAPE") : $"+{NumberFormatter.AbbreviateBigNumber(ShownPrestigePoints)}";
    float rewardWidth = Measure2(rewardText, Vector2.Zero, 30f).X;
    Color rewardColor = paying ? GuiSkin.ConfirmAccent : GuiSkin.PrestigeAccent;
    if (hold > 0f || (ready && hovered))
    {
      string prompt = hold > 0f
        ? Loc.F("Extracting core... {0}%", (int)(hold * 100))
        : WarpEscapeReady ? Loc.T("Hold to escape") : Loc.T("Hold to extract");
      DrawFittedHudText(prompt, new Vector2(panel.X + padding, titleY),
        contentWidth - rewardWidth - 16, 28f, Color.White);
    }
    else
    {
      DrawFittedHudText(CoreExtraction.Name, new Vector2(panel.X + padding, titleY),
        contentWidth - rewardWidth - 16, 28f, GuiSkin.ButtonTextColor);
    }
    DrawFittedHudText(rewardText, new Vector2(panel.Right - padding - rewardWidth, titleY),
      rewardWidth + 2, 30f, ready && hovered || prestigePanelFlash > 0f ? Color.White : rewardColor);
  }

  private void DrawExtractTooltip()
  {
    if (GameMain.IsPaused || !ExtractControlVisible || !ExtractPanelHovered)
      return;

    var lines = _extractTooltipLines;
    lines.Clear();
    lines.Add((Loc.T("EXTRACT THE PLANET'S CORE"), 34f, GuiSkin.EpicRarity));
    if (WarpEscapeReady)
    {
      lines.Add((Loc.T("Warp drive repaired. The core can power your escape."), 27f, GuiSkin.ButtonTextColor));
      lines.Add((Loc.T("Break free of the time loop and complete the playtest."), 27f, GuiSkin.ButtonTextColor));
      lines.Add((Loc.T("You can continue playing with your current progress."), 27f, GuiSkin.MutedTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add((Loc.T("Hold the button to escape."), 29f, Color.White));
    }
    else
    {
      lines.Add((Loc.T("Attempt to drill into the planet and extract the core."), 27f, GuiSkin.ButtonTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add((Loc.T("Gems, Core Shards and upgrade tree progress will be lost."), 27f, GuiSkin.LockedTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add(CanExtractCore
        ? (Loc.T("Hold the button to extract."), 29f, Color.White)
        : (Loc.T("Earn a prestige point to extract."), 27f, GuiSkin.LockedTextColor));
    }

    const int width = 920, padding = 32;
    float height = padding * 2;
    foreach (var line in lines) height += line.Size * 1.3f;
    var anchor = ExtractControlBounds;
    var box = ExtractInUpgrades
      ? new Rectangle(anchor.Center.X - width / 2, anchor.Bottom + 20, width, (int)height)
      : new Rectangle(anchor.X, anchor.Y - (int)height - 24, width, (int)height);

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Panel(m_spriteBatch, box);
    m_spriteBatch.End();

    float y = box.Y + padding;
    foreach (var (text, size, color) in lines)
    {
      if (text.Length > 0)
        DrawFittedHudText(text, new Vector2(box.X + padding, y), width - padding * 2, size, color);
      y += size * 1.3f;
    }
  }
}
