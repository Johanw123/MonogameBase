using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private bool panelShortcutGestureCaptured;

  private void UpdatePanelShortcut()
  {
    var mouse = GameInput.Mouse;
    if (!mouse.IsButtonDown(MouseButton.Left) && !mouse.IsButtonDown(MouseButton.Right))
      panelShortcutGestureCaptured = false;
    if (!GameplayInputAvailable || FracturePaused || _renderGuiSystem.ChoosingTalents
      || (_renderGuiSystem.IsOverlayVisible && !_renderGuiSystem.TryGetActivePanelBounds(out _, out _))) return;
    var keyboard = KeyboardExtended.GetState();
    bool modifiers = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl)
      || keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
      || keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
    if (modifiers || !keyboard.WasKeyPressed(Keys.Tab)) return;
    panelShortcutGestureCaptured = true;
    ClickUtility.CancelHold();
    UpgradeManager.Instance.HideTooltip();
    _renderGuiSystem.ToggleNavigationPanel();
  }
}
