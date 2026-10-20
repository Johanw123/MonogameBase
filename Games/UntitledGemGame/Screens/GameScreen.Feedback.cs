using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

// In-game quick feedback button in the bottom HUD bar (next to the Damage button) and its modal overlay.
// Opening the quick feedback input box pauses gameplay simulation and blocks game hotkeys
// while the player types their note.
public partial class UntitledGemGameGameScreen
{
  private bool FeedbackHudVisible => GameStarted && !GameMain.IsPaused
    && !m_prestiging && !m_postPrestige && !_warpEscaping
    && !_renderGuiSystem.IsOverlayVisible && !_renderGuiSystem.BulkUpgradeButtonsShown
    && !_renderGuiSystem.DrawingPopout;

  private bool FeedbackHudUnderCursor => PlaytestFeedback.IsOpen
    || (FeedbackHudVisible && HudLayout.FeedbackButton.Contains(GameInput.UiCursor));

  // Returns true while the quick feedback modal is open so Gameplay update pauses cleanly.
  private bool UpdateFeedbackModal(GameTime gameTime)
  {
    if (!PlaytestFeedback.IsOpen) return false;
    PlaytestFeedback.Update(gameTime);
    return true;
  }

  private void UpdateFeedbackButton()
  {
    if (!FeedbackHudVisible || !GameplayInputEnabled) return;
    if (!HudLayout.FeedbackButton.Contains(GameInput.UiCursor)
      || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    PlaytestFeedback.OpenInGameQuick();
    AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
  }

  private void DrawFeedbackHud()
  {
    if (_renderGuiSystem?.DrawingPopout == true) return;
    if (FeedbackHudVisible)
      DrawFeedbackButton();
    if (PlaytestFeedback.IsOpen)
      PlaytestFeedback.Draw(m_spriteBatch);
  }

  private void DrawFeedbackButton()
  {
    var button = HudLayout.FeedbackButton;
    bool open = PlaytestFeedback.IsInGameQuickOpen;
    bool hovered = GameplayInputEnabled && button.Contains(GameInput.UiCursor);
    _renderGuiSystem.DrawHudButton(m_spriteBatch, button, Loc.T("FEEDBACK"),
      GuiSkin.PanelAccent, open, hovered, 0f);
  }
}
