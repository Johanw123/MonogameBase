using System;
using System.Linq;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame
{
  // Ship system talents: learning rules, free refunds during a run, and the reset when
  // the core is extracted.
  public partial class UpgradeManager
  {
    private bool IsAbilityNode(UpgradeButton button) => button != null
      && CurrentUpgrades.UpgradeButtonsAbilities.Values.Contains(button);

    private bool CanLearnSystemTalent(UpgradeButton button) => ShipSystems.Online
      && ShipSystems.CanLearn(CurrentUpgrades.UpgradeButtonsAbilities, button.Data.ShortName);

    private ulong RefundedPoints(UpgradeButton button)
    {
      if (button != null)
        return IsAbilityNode(button) && button.CurrentLevel > 0
          ? button.Data.LevelInfo[button.CurrentLevel - 1].Cost : 0;
      ulong points = 0;
      foreach (var node in CurrentUpgrades.UpgradeButtonsAbilities.Values)
        for (int i = 0; i < node.CurrentLevel; i++)
          points = PrestigeProgression.AddSaturating(points, node.Data.LevelInfo[i].Cost);
      return points;
    }

    public ulong SpentSystemPoints => RefundedPoints(null);

    public bool CanRefundAllSystems => !UntitledGemGameGameScreen.Instance.m_prestiging
      && !UntitledGemGameGameScreen.Instance.m_postPrestige && !UpdatingButtons
      && SpentSystemPoints > 0 && SpentSystemPoints <= ulong.MaxValue - m_gameState.CurrentBlueGemCount;

    private bool HasPurchasedDependents(UpgradeButton button)
      => !ShipSystems.CanRefund(CurrentUpgrades.UpgradeButtonsAbilities, button.Data.ShortName);

    public void RefundAllSystems() => RespecAbilities(null);

    private void RespecAbilities(UpgradeButton button)
    {
      var screen = UntitledGemGameGameScreen.Instance;
      if (screen.m_prestiging || screen.m_postPrestige
        || UpdatingButtons || (button != null && (!IsAbilityNode(button) || HasPurchasedDependents(button))))
        return;
      if (!m_gameState.TryRefundAbilityPoints(RefundedPoints(button))) return;

      var levels = CaptureLevels(CurrentUpgrades.UpgradeButtonsAbilities);
      var equipped = HomeBase.Instance.GetEquippedAbilities();
      if (button == null) levels.Clear();
      else levels[button.Data.ShortName]--;
      RebuildSystems(levels, equipped);
      HideTooltip();
      screen.SaveProgress();
    }

    // Extracting the core ends the run: every system goes offline and its cells are gone.
    public void ResetSystems() => RebuildSystems(new(), new());

    private void RebuildSystems(System.Collections.Generic.Dictionary<string, int> levels,
      System.Collections.Generic.List<string> equipped)
    {
      var homeBase = HomeBase.Instance;
      homeBase?.ResetAbilities();
      foreach (var definition in CurrentUpgrades.UpgradeDefinitionsAbilities.Values)
        UGA.Reset(definition.ShortName);
      RestoreTree(CurrentUpgrades.UpgradeButtonsAbilities, CurrentUpgrades.UpgradeJointsAbilities, levels);
      if (homeBase == null) return;
      foreach (var node in CurrentUpgrades.UpgradeButtonsAbilities.Values)
        if (node.CurrentLevel > 0) homeBase.ActivateAbility(node.Data.ShortName);
      homeBase.RestoreEquippedAbilities(equipped);
    }

    private void UpdateRespecTooltip(UpgradeButton button)
    {
      if (!IsAbilityNode(button)) return;
      ulong points = RefundedPoints(button);
      if (points > 0)
      {
        m_tooltipDescription.Text = Loc.T(button.Data.UpgradeDefinition.Tooltip) + (HasPurchasedDependents(button)
          ? Loc.T("\nRefund the talents that depend on it first.")
          : Loc.F("\nRight-click: refund one rank ({0}) for free.", Loc.P((long)points, "{0} power cell", "{0} power cells")));
      }
    }
  }
}
