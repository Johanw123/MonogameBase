using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;

// The Talents tab: during a run the prestige talent tree opens read-only, to check what
// this run's talents are. Talents are still chosen between runs, where extraction opens
// the tree with Begin run and Refund all (ChoosingTalents).
public partial class RenderGuiSystem
{
  private bool m_talentsReadOnly;
  private float m_animateButtonClickTalents;

  public bool TalentsReadOnly => m_upgradeWindowType == UpgradeTypes.Meta && m_talentsReadOnly;
  private bool ChoosingTalents => m_upgradeWindowType == UpgradeTypes.Meta && !m_talentsReadOnly;

  // Shown once there are talents to look at.
  private static bool TalentsTabShown
    => PrestigeTalentLayout.SpentPoints(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta) > 0;

  private void UpdateTalentsNavigation()
  {
    if (!TalentsTabShown || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)
      || !HudLayout.NavigationButton(HudLayout.TalentsTab).Contains(GameInput.UiCursor)) return;
    if (TalentsReadOnly) SetUpgradeType(UpgradeTypes.None);
    else
    {
      SetUpgradeType(UpgradeTypes.Meta);
      m_talentsReadOnly = true;
    }
    m_animateButtonClickTalents = 0.001f;
  }

  private void DrawTalentsNavigation(SpriteBatch batch)
  {
    if (!TalentsTabShown) return;
    var bounds = HudLayout.NavigationButton(HudLayout.TalentsTab);
    bool selected = TalentsReadOnly;
    DrawHudButton(batch, bounds, selected ? Loc.T("Hide") : Loc.T("Talents"), OrbitSkin.EpicRarity, selected,
      bounds.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), m_animateButtonClickTalents, tab: true);
  }
}
