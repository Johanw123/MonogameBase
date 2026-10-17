using System;
using System.Collections.Generic;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

// Ending a run is an attempt to extract the planet's core (CoreExtraction): the player
// holds the HUD panel, a drill beam reaches the core, the core breaches and collapses
// into a black hole, and the ship is pulled into a new time loop. A repaired warp
// drive lets it escape instead, completing the playtest (GameScreen.WarpEnding).
public partial class UntitledGemGameGameScreen
{
  private const float LoopCaptionSeconds = 3f;
  private static readonly Color ExtractionGlow = new(255, 200, 90);
  private float _extractHold;
  private bool _extractHolding;
  private float _loopCaptionTime;
  private readonly List<(string Text, float Size, Color Color)> _extractTooltipLines = new();

  private bool ExtractInputEnabled => GameStarted && !m_prestiging && !m_postPrestige
    && (GameInput.WindowActive || _renderGuiSystem.HasInputFocus)
    && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;

  private bool WarpEscapeReady => CoreExtraction.CanEscape(m_upgradeManager.UG.WarpDriveRepaired, m_gameState.EscapedThisRun);

  private bool CanExtractCore => WarpEscapeReady || CoreExtraction.CanExtract(m_gameState.PendingPrestigePoints, m_gameState.CoreExtractions);

  private bool ExtractPanelHovered => ExtractInputEnabled && HudLayout.PrestigePanel.Contains(GameInput.UiCursor);

  // Also used by capture scenes, which may extract regardless of the reward.
  public void ExtractCore()
  {
    if (m_prestiging || m_postPrestige) return;
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

  // A deliberate hold, so a stray click on the HUD can never end the run.
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
    bool ready = CanExtractCore;
    bool paying = m_gameState.PendingPrestigePoints > 0;
    bool hovered = ExtractPanelHovered;
    float hold = Math.Clamp(_extractHold / CoreExtraction.HoldSeconds, 0f, 1f);
    int padding = HudLayout.ProgressPanelPadding;
    int contentWidth = panel.Width - padding * 2;
    var bar = new Rectangle(panel.X + padding, panel.Y + HudLayout.ProgressBarTop, contentWidth, 10);

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Button(m_spriteBatch, panel, ready && hovered, confirm: paying);
    if (prestigePanelFlash > 0f)
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panel, PrestigePointColor * (0.45f * prestigePanelFlash));
    if (hold > 0f)
    {
      // The charge floods the panel and flickers harder as the core gives way.
      float flicker = 0.75f + 0.25f * MathF.Sin((float)BaseGame.Time.TotalGameTime.TotalSeconds * (12f + 30f * hold));
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(panel.X, panel.Y, (int)(panel.Width * hold), panel.Height), ExtractionGlow * (0.5f * flicker));
      OrbitSkin.Progress(m_spriteBatch, bar, hold);
    }
    else
    {
      OrbitSkin.ProgressTrack(m_spriteBatch, bar);
      OrbitSkin.ProgressFill(m_spriteBatch, bar, 0f, echo, Color.White * 0.4f);
      OrbitSkin.ProgressFill(m_spriteBatch, bar, echo, charge, Color.White);
    }
    m_spriteBatch.End();

    string rewardText = WarpEscapeReady ? Loc.T("ESCAPE") : $"+{NumberFormatter.AbbreviateBigNumber(ShownPrestigePoints)}";
    float rewardWidth = Measure2(rewardText, Vector2.Zero, 30f).X;
    Color rewardColor = paying ? OrbitSkin.ConfirmAccent : OrbitSkin.PrestigeAccent;
    if (hold > 0f || (ready && hovered))
    {
      string prompt = hold > 0f
        ? Loc.F("Extracting core... {0}%", (int)(hold * 100))
        : WarpEscapeReady ? Loc.T("Hold to escape") : Loc.T("Hold to extract");
      DrawFittedHudText(prompt, new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressTitleTop),
        contentWidth - rewardWidth - 16, 28f, Color.White);
    }
    else
    {
      DrawFittedHudText(CoreExtraction.Name, new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressTitleTop),
        contentWidth - rewardWidth - 16, 28f, OrbitSkin.ButtonTextColor);
    }
    DrawFittedHudText(rewardText, new Vector2(panel.Right - padding - rewardWidth, panel.Y + HudLayout.ProgressTitleTop),
      rewardWidth + 2, 30f, ready && hovered || prestigePanelFlash > 0f ? Color.White : rewardColor);
  }

  private void DrawExtractTooltip()
  {
    if (GameMain.IsPaused || !ExtractPanelHovered)
      return;

    ulong reward = m_gameState.PendingPrestigePoints;
    var lines = _extractTooltipLines;
    lines.Clear();
    lines.Add((Loc.T("EXTRACT THE PLANET'S CORE"), 34f, OrbitSkin.EpicRarity));
    if (WarpEscapeReady)
    {
      lines.Add((Loc.T("Warp drive repaired. The core can power your escape."), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("Break free of the time loop and complete the playtest."), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("You can continue playing with your current progress."), 27f, OrbitSkin.MutedTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add((Loc.T("Hold the button to escape."), 29f, Color.White));
    }
    else
    {
      // Hand-wrapped: each drawn line is its own key.
      lines.Add((Loc.T("Drill into the core and harvest its power. The core"), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("collapses into a black hole and drags you into a new"), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("time loop, back to the crash landing."), 27f, OrbitSkin.ButtonTextColor));
      lines.Add(("", 12f, Color.Transparent));
      if (reward > 0)
      {
        long points = (long)Math.Min(reward, (ulong)long.MaxValue);
        lines.Add((Loc.P(points, "Gain {0:N0} prestige point for talents.", "Gain {0:N0} prestige points for talents."),
          29f, OrbitSkin.EpicRarity));
      }
      lines.Add((Loc.T("Gem income held up for a minute charges the bar."), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("Each full bar earns a prestige point, and every"), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("point needs more income than the last."), 27f, OrbitSkin.ButtonTextColor));
      lines.Add((Loc.T("The loop remembers how close you came: the next"), 27f, OrbitSkin.MutedTextColor));
      lines.Add((Loc.T("loop's bar starts partly charged."), 27f, OrbitSkin.MutedTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add((Loc.T("Gems, the upgrade tree and Core Shards are lost."), 27f, OrbitSkin.LockedTextColor));
      lines.Add((Loc.F("This is loop {0:N0}.", PrestigeProgression.AddSaturating(m_gameState.CoreExtractions, 1)), 24f, OrbitSkin.MutedTextColor));
      lines.Add(("", 12f, Color.Transparent));
      lines.Add(CanExtractCore
        ? (Loc.T("Hold the button to extract."), 29f, Color.White)
        : (Loc.T("Earn a prestige point to extract."), 27f, OrbitSkin.LockedTextColor));
    }

    const int width = 920, padding = 32;
    float height = padding * 2;
    foreach (var line in lines) height += line.Size * 1.3f;
    var anchor = HudLayout.PrestigePanel;
    var box = new Rectangle(anchor.X, anchor.Y - (int)height - 24, width, (int)height);

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Panel(m_spriteBatch, box);
    m_spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(box.X + 4, box.Y + 24, HudLayout.ButtonBorderThickness, Math.Max(0, box.Height - 48)), OrbitSkin.EpicRarity * 0.8f);
    m_spriteBatch.End();

    float y = box.Y + padding;
    foreach (var (text, size, color) in lines)
    {
      if (text.Length > 0)
        DrawFittedHudText(text, new Vector2(box.X + padding, y), width - padding * 2, size, color);
      y += size * 1.3f;
    }
  }

  // Names each stage of the collapse, then the loop the ship lands in.
  private void DrawExtractionCaptions()
  {
    if (GameMain.IsPaused || RenderGuiSystem.Instance.DrawingPopout)
      return;

    float centerX = HudLayout.Width / 2f;
    if (m_prestiging && m_prestigeTime < PrestigeSwallowSeconds)
    {
      float t = m_prestigeTime;
      var (text, color, start, end) =
        t < CollapseBuildupSeconds ? (Loc.F("EXTRACTING CORE  {0}%", (int)(100 * t / CollapseBuildupSeconds)), ExtractionGlow, 0f, CollapseBuildupSeconds)
        : t < CollapseImplodeSeconds ? (Loc.T("CORE BREACH"), FissureColor, CollapseBuildupSeconds, CollapseImplodeSeconds)
        : (Loc.T("SINGULARITY"), VoidGlowColor, CollapseImplodeSeconds, PrestigeSwallowSeconds);
      float alpha = Math.Min(Smooth(start, start + 0.15f, t), 1f - Smooth(end - 0.15f, end, t));
      DrawCenteredNotification(text, centerX, 300f, 54f, color * alpha, Color.Black * alpha);
    }

    if (_loopCaptionTime > 0f)
    {
      float age = LoopCaptionSeconds - _loopCaptionTime;
      float alpha = Math.Min(Smooth(0f, 0.3f, age), Smooth(0f, 0.6f, _loopCaptionTime));
      DrawCenteredNotification(Loc.F("LOOP {0:N0}", PrestigeProgression.AddSaturating(m_gameState.CoreExtractions, 1)),
        centerX, 300f, 64f, OrbitSkin.EpicRarity * alpha, Color.Black * alpha);
      DrawCenteredNotification(Loc.T("Time loops back to the crash landing."), centerX, 370f, 30f,
        OrbitSkin.ButtonTextColor * alpha, Color.Black * alpha);
    }
  }

  private void StartLoopCaption() => _loopCaptionTime = LoopCaptionSeconds;

  private void UpdateLoopCaption(float dt) => _loopCaptionTime = Math.Max(0f, _loopCaptionTime - dt);
}
