using System;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// With the warp drive repaired, the failed extraction becomes the way out:
//  1. Collapse (0-2.6 s): the planet destabilises and implodes as in a normal extraction
//     (GameScreen.TimeLoop.cs), and a black hole is born.
//  2. Charge (2.6-3.0 s): the core's energy floods the warp drive; light pours into the ship.
//  3. Climb (3.0-5.0 s): the ship pulls up and away, faster and faster. The camera follows
//     it, so the hole, the field and the fleet fall behind below.
//  4. Jump (5.0 s): the drive fires. The camera can't keep up; the ship streaks off the top
//     of the screen, leaving a fading warp trail.
//  5. The thanks-for-playing dialog fades in. Continue Playing puts the ship and camera
//     back and resumes the run, which the escape left untouched.
public partial class UntitledGemGameGameScreen
{
  private const float WarpChargeSeconds = 2.6f;
  private const float WarpClimbSeconds = 3.0f;
  private const float WarpJumpSeconds = 5.0f;
  private const float WarpDialogSeconds = 6.3f;
  // Share of the view's height climbed before the jump, and how long the camera takes to
  // stop once the ship outruns it.
  private const float WarpClimbHeights = 0.6f;
  private const float WarpCameraStopSeconds = 0.9f;
  private static readonly Color WarpColor = new(110, 200, 255);

  private bool _warpEscaping;
  private bool _endingDialogVisible;
  private float _endingDialogAge;
  private Vector2 _escapeOrigin;
  private Vector2 _escapeCameraOrigin;
  private float _escapeViewHeight;
  private float _escapeCameraClimb;
  private float _escapeParticleClock;
  private int _endingSelection = 1;

  // How far the background has scrolled with the camera during the escape.
  private float EscapeBackgroundScroll => _warpEscaping ? _escapeCameraClimb * 0.5f : 0f;

  private static Rectangle EndingPanel => new((HudLayout.Width - 1440) / 2, (HudLayout.Bottom - 740) / 2, 1440, 740);
  private static Rectangle EndingButton(int index)
  {
    var panel = EndingPanel;
    return new Rectangle(panel.X + 80 + index * 656, panel.Bottom - 172, 624, 100);
  }

  private Vector2 EndingCursor
  {
    get
    {
#if !KNI_WEB
      if (Capture.CaptureSession.Active) return GameInput.UiCursor;
#endif
      var viewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      return Vector2.Transform(GameInput.Mouse.Position.ToVector2() - new Vector2(viewport.X, viewport.Y),
        Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix()));
    }
  }

  private void BeginWarpEscape()
  {
    // Keep the entire run: Continue Playing resumes it, and quitting during the
    // sequence saves it without the normal extraction's prestige transaction.
    _warpEscaping = true;
    _endingDialogVisible = false;
    _endingDialogAge = 0f;
    _endingSelection = 1;
    _escapeOrigin = m_homeBaseEntity.Get<Transform2>().Position;
    _escapeCameraOrigin = m_camera.Position;
    _escapeViewHeight = m_camera.BoundingRectangle.Height;
    _escapeCameraClimb = _escapeParticleClock = 0f;
    m_prestiging = true;
    m_prestigeTime = 0f;
    ClickUtility.CancelHold();
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
    m_upgradeManager.HideTooltip();
    StartPrestigeCollapse();
    AudioManager.Instance.PlaySound(SoundEvent.CoreExtractionStart);
    SaveProgress();
  }

  // How far the ship has climbed: a steady acceleration, then the jump's far steeper one.
  private float WarpShipClimb(float t)
  {
    float h = _escapeViewHeight;
    float climbSeconds = WarpJumpSeconds - WarpClimbSeconds;
    float u = Math.Max(0f, t - WarpClimbSeconds);
    float climb = u <= climbSeconds
      ? h * WarpClimbHeights * (u / climbSeconds) * (u / climbSeconds)
      : h * WarpClimbHeights * (1f + 2f * (u - climbSeconds) / climbSeconds);
    float jump = Math.Max(0f, t - WarpJumpSeconds);
    return climb + h * 9f * jump * jump;
  }

  // The camera keeps up during the climb, then coasts to a stop as the ship jumps.
  private float WarpCameraClimb(float t)
  {
    if (t <= WarpJumpSeconds) return WarpShipClimb(t);
    float h = _escapeViewHeight;
    float speed = h * WarpClimbHeights * 2f / (WarpJumpSeconds - WarpClimbSeconds);
    float coast = Math.Min(t - WarpJumpSeconds, WarpCameraStopSeconds);
    return h * WarpClimbHeights + speed * (coast - coast * coast / (2f * WarpCameraStopSeconds));
  }

  private void UpdateWarpEscape(float dt)
  {
    if (_endingDialogVisible)
    {
      _endingDialogAge += dt;
      UpdateEndingDialog();
      return;
    }

    float previous = m_prestigeTime;
    m_prestigeTime += dt;
    float t = m_prestigeTime;
    bool Crossed(float moment) => previous < moment && t >= moment;
    UpdatePrestigeCollapse(dt);
    SpawnerEffects.Update(dt);
    // The normal update returns before the particles while escaping.
    UpdateGpuParticles(dt);

    var ship = m_homeBaseEntity.Get<Transform2>();
    ship.Position = _escapeOrigin - new Vector2(0f, WarpShipClimb(t));
    _escapeCameraClimb = WarpCameraClimb(t);
    m_camera.Position = _escapeCameraOrigin - new Vector2(0f, _escapeCameraClimb);

    if (Crossed(WarpJumpSeconds))
    {
      AudioManager.Instance.PlaySound(SoundEvent.TimeLoopRestart);
      EmitWarpJumpParticles(ship.Position);
    }
    if (t >= WarpChargeSeconds && t < WarpJumpSeconds + 0.3f)
      EmitWarpDriveParticles(dt, ship.Position, t);

    if (t < WarpDialogSeconds) return;
    _endingDialogVisible = true;
    _endingDialogAge = 0f;
    m_gameState.EscapedThisRun = true;
    SaveProgress();
  }

  // Energy pours into the ship while the drive charges; once it climbs, exhaust streams out.
  private void EmitWarpDriveParticles(float dt, Vector2 ship, float t)
  {
    _escapeParticleClock += dt;
    const float interval = 1f / 30f;
    if (_escapeParticleClock < interval) return;
    _escapeParticleClock %= interval;
    float charge = Smooth(WarpChargeSeconds, WarpJumpSeconds, t);
    if (t < WarpJumpSeconds)
      EmitInfall(ship, 24 + (int)(40 * charge), Color.Lerp(WarpColor, Color.White, 0.3f),
        150f, 70f, 10f, 0.3f, new(0.4f, 1f), 0.2f);
    if (t >= WarpClimbSeconds)
    {
      float power = Smooth(WarpClimbSeconds, WarpJumpSeconds, t);
      EmitCollapseParticles(new GpuParticleBurst(ship + new Vector2(0f, 26f), 30 + (int)(70 * power), WarpColor)
      {
        Direction = MathHelper.PiOver2, Spread = 0.35f, Radius = 6f,
        Speed = new Vector2(120f, 260f) * (1f + 2.5f * power), Lifetime = new(0.15f, 0.45f),
        Size = new(0.5f, 1.4f), Streak = true
      });
    }
  }

  // The drive fires: a ring of light where the ship was, and a burst trailing behind it.
  private void EmitWarpJumpParticles(Vector2 ship)
  {
    EmitCollapseParticles(new GpuParticleBurst(ship, 900, Color.Lerp(WarpColor, Color.White, 0.4f))
    {
      Motion = GpuParticleMotion.Outward, Radius = 30f, InnerRadius = 20f, Spread = 0.1f,
      Speed = new(250f, 520f), Lifetime = new(0.35f, 0.8f), Size = new(0.5f, 1.3f), Streak = true
    });
    EmitCollapseParticles(new GpuParticleBurst(ship + new Vector2(0f, 20f), 700, WarpColor)
    {
      Direction = MathHelper.PiOver2, Spread = 0.25f, Radius = 10f,
      Speed = new(300f, 1100f), Lifetime = new(0.3f, 0.9f), Size = new(0.6f, 1.6f), Streak = true
    });
    EmitCollapseParticles(new GpuParticleBurst(ship, 40, Color.White)
    {
      Speed = new(5f, 20f), Lifetime = new(0.08f, 0.2f), Size = new(10f, 22f), Radius = 4f
    });
  }

  private void UpdateEndingDialog()
  {
    if (!GameInput.WindowActive || _endingDialogAge < 0.3f) return;
    var keyboard = KeyboardExtended.GetState();
    if (keyboard.WasKeyPressed(Keys.Left) || keyboard.WasKeyPressed(Keys.Right)
      || keyboard.WasKeyPressed(Keys.Tab))
      _endingSelection = 1 - _endingSelection;

    if (keyboard.WasKeyPressed(Keys.Escape))
    {
      ContinueAfterEnding();
      return;
    }
    if (keyboard.WasKeyPressed(Keys.Enter) || keyboard.WasKeyPressed(Keys.Space))
    {
      ChooseEndingAction(_endingSelection);
      return;
    }
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    for (int i = 0; i < 2; i++)
      if (EndingButton(i).Contains(EndingCursor))
      {
        ChooseEndingAction(i);
        return;
      }
  }

  private void ChooseEndingAction(int index)
  {
    AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
    if (index == 1)
    {
      ContinueAfterEnding();
      return;
    }
    // ShowMainMenu replaces this screen; UnloadContent saves the completed run.
    SaveProgress();
    GumService.Default.Root.Children.Clear();
    GumService.Default.ModalRoot.Children.Clear();
    GameMain.IsPaused = false;
    ((GameMain)Game).ShowMainMenu();
    Delivered = Collected = DeliveredUncounted = 0;
  }

  private void ContinueAfterEnding()
  {
    m_homeBaseEntity.Get<Transform2>().Position = _escapeOrigin;
    m_camera.Position = _escapeCameraOrigin;
    _escapeCameraClimb = 0f;
    _warpEscaping = _endingDialogVisible = false;
    m_prestiging = false;
    m_prestigeTime = 0f;
    planetHitPulse = planetShake = 0f;
    ClearGpuParticles();
    SaveProgress();
  }

  // The drive's glow builds on the ship as it charges and climbs; the jump leaves a
  // trail from where it fired to the ship, fading as the ship vanishes.
  private void DrawWarpEscapeEffects()
  {
    if (!_warpEscaping || _endingDialogVisible) return;
    float t = m_prestigeTime;
    var ship = m_homeBaseEntity.Get<Transform2>().Position;
    float charge = Smooth(WarpChargeSeconds, WarpJumpSeconds, t);
    float glowFade = 1f - Smooth(WarpJumpSeconds, WarpJumpSeconds + 0.25f, t);
    float pulse = 0.85f + 0.15f * MathF.Sin(t * (10f + 30f * charge));
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    if (charge * glowFade > 0f)
    {
      m_shapeBatch.FillCircle(ship, 24f + 70f * charge, WarpColor * (0.35f * charge * glowFade * pulse), 30f);
      m_shapeBatch.FillCircle(ship, 8f + 20f * charge, Color.White * (0.7f * charge * glowFade * pulse), 8f);
      m_shapeBatch.BorderCircle(ship, 40f + 50f * charge * pulse, WarpColor * (0.5f * charge * glowFade), 2.5f, 3f);
    }
    if (t >= WarpJumpSeconds)
    {
      float trail = 1f - Smooth(WarpJumpSeconds + 0.1f, WarpJumpSeconds + 1.1f, t);
      var from = _escapeOrigin - new Vector2(0f, WarpShipClimb(WarpJumpSeconds));
      m_shapeBatch.FillLine(from, ship, 14f * trail, WarpColor * (0.6f * trail), 14f);
      m_shapeBatch.FillLine(from, ship, 4f * trail, Color.White * trail, 4f);
      float flash = 1f - Smooth(WarpJumpSeconds, WarpJumpSeconds + 0.5f, t);
      m_shapeBatch.FillCircle(from, 30f + 160f * (1f - flash), WarpColor * (0.5f * flash), 60f);
    }
    m_shapeBatch.End();
  }

  private void DrawWarpEndingHud()
  {
    if (RenderGuiSystem.Instance.DrawingPopout) return;
    if (!_endingDialogVisible)
    {
      float t = m_prestigeTime;
      var (caption, color, start, end) =
        t < CollapseBuildupSeconds ? (Loc.T("EXTRACTING CORE"), Color.LightCyan, 0f, CollapseBuildupSeconds)
        : t < WarpChargeSeconds ? (Loc.T("CORE BREACH"), FissureColor, CollapseBuildupSeconds, WarpChargeSeconds)
        : t < WarpJumpSeconds ? (Loc.T("WARP DRIVE CHARGING"), Color.LightCyan, WarpChargeSeconds, WarpJumpSeconds)
        : (Loc.T("WARP JUMP — ESCAPE SUCCESSFUL"), Color.LightCyan, WarpJumpSeconds, WarpDialogSeconds);
      float alpha = Math.Min(Smooth(start, start + 0.15f, t), 1f - Smooth(end - 0.15f, end, t));
      DrawCenteredNotification(caption, HudLayout.Width / 2f, 300f, 54f, color * alpha, Color.Black * alpha);
      return;
    }

    float fade = Smooth(0f, 0.4f, _endingDialogAge);
    var panel = EndingPanel;
    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, HudLayout.Width, HudLayout.Bottom),
      new Color(2, 8, 20) * (0.8f * fade));
    GuiSkin.Panel(m_spriteBatch, panel);
    for (int i = 0; i < 2; i++)
      GuiSkin.Button(m_spriteBatch, EndingButton(i),
        EndingButton(i).Contains(EndingCursor) || _endingSelection == i, confirm: i == 1);
    m_spriteBatch.End();

    float x = panel.X + 80;
    float width = panel.Width - 160;
    DrawFittedHudText(Loc.T("YOU ESCAPED"), new Vector2(x, panel.Y + 64), width, 30f, Color.LightCyan);
    DrawFittedHudText(Loc.T("Thanks for playing!"), new Vector2(x, panel.Y + 124), width, 72f, Color.White);
    DrawFittedHudText(Loc.T("The core powered your repaired warp drive."), new Vector2(x, panel.Y + 252), width, 36f, GuiSkin.ButtonTextColor);
    DrawFittedHudText(Loc.T("You escaped the black hole and broke free of the time loop."), new Vector2(x, panel.Y + 306), width, 36f, GuiSkin.ButtonTextColor);
    DrawFittedHudText(Loc.T("This is the end of the Beyond the Belt playtest for now."), new Vector2(x, panel.Y + 392), width, 34f, GuiSkin.ButtonTextColor);
    DrawFittedHudText(Loc.T("Continue playing to return to your current run. Your progress is saved."), new Vector2(x, panel.Y + 446), width, 30f, GuiSkin.MutedTextColor);
    for (int i = 0; i < 2; i++)
    {
      var button = EndingButton(i);
      DrawFittedHudText(i == 0 ? Loc.T("Main Menu") : Loc.T("Continue Playing"),
        new Vector2(button.X + 28, button.Y + 28), button.Width - 56, 36f, Color.White);
    }
  }
}
