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

public partial class UntitledGemGameGameScreen
{
  private const float WarpLaunchSeconds = 3.2f;
  private bool _warpEscaping;
  private bool _endingDialogVisible;
  private Vector2 _escapeOrigin;
  private int _endingSelection = 1;

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

  private Vector2 EscapeDirection => Vector2.Normalize(_escapeOrigin - PlanetPos);

  private void BeginWarpEscape()
  {
    // Keep the entire run: Continue Playing resumes it, and quitting during the
    // sequence saves it without the normal extraction's prestige transaction.
    _warpEscaping = true;
    _endingDialogVisible = false;
    _endingSelection = 1;
    _escapeOrigin = m_homeBaseEntity.Get<Transform2>().Position;
    m_prestiging = true;
    m_prestigeTime = 0f;
    ClickUtility.CancelHold();
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
    m_upgradeManager.HideTooltip();
    StartPrestigeCollapse();
    AudioManager.Instance.PlaySound(SoundEvent.CoreExtractionStart);
    SaveProgress();
  }

  private void UpdateWarpEscape(float dt)
  {
    if (_endingDialogVisible)
    {
      UpdateEndingDialog();
      return;
    }

    float previous = m_prestigeTime;
    m_prestigeTime += dt;
    UpdatePrestigeCollapse(dt);
    SpawnerEffects.Update(dt);
    if (previous < WarpLaunchSeconds && m_prestigeTime >= WarpLaunchSeconds)
      AudioManager.Instance.PlaySound(SoundEvent.TimeLoopRestart);

    float flight = Smooth(WarpLaunchSeconds, 4.2f, m_prestigeTime);
    var view = m_camera.BoundingRectangle;
    m_homeBaseEntity.Get<Transform2>().Position = _escapeOrigin
      + EscapeDirection * ((view.Width + view.Height) * flight * flight);

    if (m_prestigeTime < PrestigeCollapseSeconds) return;
    _endingDialogVisible = true;
    m_gameState.EscapedThisRun = true;
    SaveProgress();
  }

  private void UpdateEndingDialog()
  {
    if (!GameInput.WindowActive) return;
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
    _warpEscaping = _endingDialogVisible = false;
    m_prestiging = false;
    m_prestigeTime = 0f;
    planetHitPulse = planetShake = 0f;
    SaveProgress();
  }

  private void DrawWarpEscapeEffects()
  {
    if (!_warpEscaping || _endingDialogVisible) return;
    float t = m_prestigeTime;
    float charge = Smooth(CollapseBuildupSeconds, WarpLaunchSeconds, t);
    float fade = 1f - Smooth(WarpLaunchSeconds, 4.35f, t);
    if (charge * fade <= 0f) return;
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    m_shapeBatch.FillCircle(_escapeOrigin, 20f + 80f * charge,
      Color.DeepSkyBlue * (0.55f * charge * fade), 12f);
    m_shapeBatch.FillCircle(_escapeOrigin, 8f + 26f * charge,
      Color.White * (charge * fade), 6f);
    if (t >= WarpLaunchSeconds)
    {
      var ship = m_homeBaseEntity.Get<Transform2>().Position;
      m_shapeBatch.FillLine(_escapeOrigin, ship, 10f, Color.DeepSkyBlue * fade, 10f);
      m_shapeBatch.FillLine(_escapeOrigin, ship, 3f, Color.White * fade, 4f);
    }
    m_shapeBatch.End();
  }

  private void DrawWarpEndingHud()
  {
    if (RenderGuiSystem.Instance.DrawingPopout) return;
    if (!_endingDialogVisible)
    {
      float t = m_prestigeTime;
      string caption = t < CollapseBuildupSeconds ? Loc.T("EXTRACTING CORE")
        : t < WarpLaunchSeconds ? Loc.T("WARP DRIVE CHARGING") : Loc.T("WARP JUMP — ESCAPE SUCCESSFUL");
      DrawCenteredNotification(caption, HudLayout.Width / 2f, 300f, 54f, Color.LightCyan, Color.Black);
      float flash = Smooth(WarpLaunchSeconds, WarpLaunchSeconds + 0.12f, t)
        * (1f - Smooth(WarpLaunchSeconds + 0.12f, 4.1f, t));
      m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, HudLayout.Width, HudLayout.Bottom),
        Color.LightCyan * (flash * 0.65f));
      m_spriteBatch.End();
      return;
    }

    var panel = EndingPanel;
    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, HudLayout.Width, HudLayout.Bottom),
      new Color(2, 8, 20) * 0.94f);
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
