using System;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Screens;

// The demo's ending (DemoLimits). Once the points that would open the first locked talent
// tier are spent, the next run is the last with something new in it
// (PrestigeTalentLayout.DemoComplete). The extraction that ends it closes on this dialog:
// after the collapse, before the talent tree opens. It shows once. Continue Playing opens
// the tree as usual and the save plays on.
public partial class UntitledGemGameGameScreen
{
  private bool _demoEndVisible;
  private float _demoEndAge;
  private int _demoEndSelection = 1;

  internal bool DemoEndVisible => _demoEndVisible;

  private static bool DemoEndWishlist => !OperatingSystem.IsBrowser();

  private bool DemoEndDue => !m_gameState.DemoEndShown && PrestigeTalentLayout.DemoComplete(
    UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta, m_gameState.PrestigePointsEarned);

  private void BeginDemoEnd()
  {
    _demoEndVisible = true;
    _demoEndAge = 0f;
    _demoEndSelection = 1;
    m_upgradeManager.HideTooltip();
  }

  // True while the dialog owns the input.
  private bool UpdateDemoEnd(float dt)
  {
    if (!_demoEndVisible) return false;
    _demoEndAge += dt;
    if (!GameInput.WindowActive || GameMain.IsPaused || _demoEndAge < 0.3f) return true;
    var keyboard = KeyboardExtended.GetState();
    if (DemoEndWishlist && (keyboard.WasKeyPressed(Keys.Left) || keyboard.WasKeyPressed(Keys.Right)
      || keyboard.WasKeyPressed(Keys.Tab)))
      _demoEndSelection = 1 - _demoEndSelection;
    if (keyboard.WasKeyPressed(Keys.Enter) || keyboard.WasKeyPressed(Keys.Space))
      ChooseDemoEndAction(_demoEndSelection);
    else if (GameInput.Mouse.WasButtonPressed(MouseButton.Left))
      for (int i = DemoEndWishlist ? 0 : 1; i < 2; i++)
        if (EndingButton(i).Contains(EndingCursor))
        {
          ChooseDemoEndAction(i);
          break;
        }
    return true;
  }

  private void ChooseDemoEndAction(int index)
  {
    AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
    if (index == 0)
    {
      MainMenu.OpenStorePage();
      return;
    }
    ContinueAfterDemoEnd();
  }

  // Also used by capture scenes.
  internal void ContinueAfterDemoEnd()
  {
    if (!_demoEndVisible) return;
    _demoEndVisible = false;
    m_gameState.DemoEndShown = true;
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Meta);
    SaveProgress();
  }

  private void DrawDemoEndHud()
  {
    if (!_demoEndVisible || RenderGuiSystem.Instance.DrawingPopout) return;
    float fade = Smooth(0f, 0.4f, _demoEndAge);
    var panel = EndingPanel;
    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, HudLayout.Width, HudLayout.Bottom),
      new Color(2, 8, 20) * (0.8f * fade));
    GuiSkin.Panel(m_spriteBatch, panel);
    for (int i = DemoEndWishlist ? 0 : 1; i < 2; i++)
      GuiSkin.Button(m_spriteBatch, EndingButton(i),
        EndingButton(i).Contains(EndingCursor) || _demoEndSelection == i, confirm: i == 0);
    m_spriteBatch.End();

    float x = panel.X + 80;
    float width = panel.Width - 160;
    DrawFittedHudText(Loc.T("END OF THE DEMO"), new Vector2(x, panel.Y + 64), width, 30f, PrestigePointColor);
    DrawFittedHudText(Loc.T("Thanks for playing!"), new Vector2(x, panel.Y + 124), width, 72f, Color.White);
    DrawFittedHudText(Loc.F("The full game picks up right here, with tier {0} and the Shipyard.", DemoLimits.TalentTiers + 1),
      new Vector2(x, panel.Y + 252), width, 36f, GuiSkin.ButtonTextColor);
    DrawFittedHudText(Loc.T("Past that: Deep Space Signals, the Railgun, bigger fleets and a way out of the time loop."),
      new Vector2(x, panel.Y + 306), width, 36f, GuiSkin.ButtonTextColor);
    if (DemoEndWishlist)
      DrawFittedHudText(Loc.T("Wishlisting Beyond the Belt on Steam helps me more than anything."),
        new Vector2(x, panel.Y + 392), width, 34f, GuiSkin.ButtonTextColor);
    DrawFittedHudText(Loc.T("Keep playing as long as you like. Your progress is saved."),
      new Vector2(x, panel.Y + 446), width, 30f, GuiSkin.MutedTextColor);
    for (int i = DemoEndWishlist ? 0 : 1; i < 2; i++)
    {
      var button = EndingButton(i);
      DrawFittedHudText(i == 0 ? Loc.T("Wishlist on Steam") : Loc.T("Continue Playing"),
        new Vector2(button.X + 28, button.Y + 28), button.Width - 56, 36f, Color.White);
    }
  }
}
