using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  public static Rectangle UpgradesBulkButton(int index) => new(
    UpgradesExtractButton.Right + 36 + index * 300, UpgradesHeaderCenterY - 38, 284, 76);
#if !KNI_WEB
  private Rectangle PopoutButton => new(
    (BulkUpgradeButtonsShown ? UpgradesBulkButton(1).Right : UpgradesExtractButton.Right) + 16,
    UpgradesHeaderCenterY - 38, 220, 76);
#endif

  private bool CanBuyBulkUpgrade => UpgradeManager.CurrentUpgrades.UpgradeButtons.Values.Any(button =>
    !Weaponry.IsInTree(button.Data.ShortName)
    && button.Data.UpgradeDefinition.Currency != CoreShards.Currency && button.CanAfford
    && button.CurrentLevel < button.Data.NumLevels && button.Button.IsEnabled);

  private bool UpdateUpgradesHeaderActions()
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades || !drawUpgradesGui
      || IsDetached && !IsPopoutFocused || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return false;
#if !KNI_WEB
    if (PopoutButton.Contains(PanelPointer))
    {
      PanelDismissInputCaptured = true;
      ClearGumCursorInput();
      TogglePopout();
      return true;
    }
#endif
    if (!BulkUpgradeButtonsShown) return false;
    for (int index = 0; index < 2; index++)
    {
      if (!UpgradesBulkButton(index).Contains(PanelPointer)) continue;
      PanelDismissInputCaptured = true;
      ClearGumCursorInput();
      m_animateButtonClickCheapestUpgrade = 0.001f;
      int count = index == 0 ? 1 : 100;
      for (int purchase = 0; purchase < count && UpgradeCheapest(); purchase++) { }
      return true;
    }
    return false;
  }

  private void DrawUpgradesHeaderActions(SpriteBatch batch)
  {
    bool enabled = CanBuyBulkUpgrade;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    if (BulkUpgradeButtonsShown)
      for (int index = 0; index < 2; index++)
      {
        var bounds = UpgradesBulkButton(index);
        GuiSkin.HudButton(batch, bounds, GuiSkin.PanelAccent, false,
          enabled && bounds.Contains(PanelPointer), opacity: enabled ? 1f : 0.45f);
      }
#if !KNI_WEB
    GuiSkin.HudButton(batch, PopoutButton, GuiSkin.Accent, false, PopoutButton.Contains(PanelPointer));
#endif
    batch.End();
    if (BulkUpgradeButtonsShown)
      for (int index = 0; index < 2; index++)
        DrawHeaderActionLabel(UpgradesBulkButton(index), index == 0 ? Loc.T("Upgrade Cheapest") : Loc.T("Spend All"),
          enabled ? GuiSkin.ButtonTextColor : GuiSkin.MutedTextColor);
#if !KNI_WEB
    DrawHeaderActionLabel(PopoutButton, popoutError == null ? IsDetached ? Loc.T("Dock") : Loc.T("Pop out")
      : Loc.T(popoutError), GuiSkin.ButtonTextColor);
#endif
  }

  private void DrawHeaderActionLabel(Rectangle bounds, string label, Color color)
  {
    float size = 28;
    var measure = Measure2(label, Vector2.Zero, size);
    size *= System.Math.Min(1f, (bounds.Width - 24) / System.Math.Max(1f, measure.X));
    measure = Measure2(label, Vector2.Zero, size);
    DrawFittedSystemText(label, new Vector2(bounds.Center.X - measure.X / 2, bounds.Center.Y - measure.Y / 2),
      bounds.Width - 24, size, color);
  }
}
