using System;
using System.Linq;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame
{
  public partial class UpgradeManager
  {
    private bool IsAbilityNode(UpgradeButton button) => button != null
      && CurrentUpgrades.UpgradeButtonsAbilities.Values.Contains(button)
      && button.Data.ShortName != "ResetAbilities1";

    private ulong RefundedPoints(UpgradeButton button)
    {
      if (button != null)
        return IsAbilityNode(button) && button.CurrentLevel > 0
          ? button.Data.LevelInfo[button.CurrentLevel - 1].Cost : 0;
      ulong points = 0;
      foreach (var node in CurrentUpgrades.UpgradeButtonsAbilities.Values.Where(IsAbilityNode))
        for (int i = 0; i < node.CurrentLevel; i++)
          points = PrestigeProgression.AddSaturating(points, node.Data.LevelInfo[i].Cost);
      return points;
    }

    private bool HasPurchasedDependents(UpgradeButton button) => button.CurrentLevel == 1
      && CurrentUpgrades.UpgradeButtonsAbilities.Values.Any(other => other.CurrentLevel > 0
        && (other.Data.BlockedBy == button.Data.ShortName
          || other.Data.LockedBy == button.Data.ShortName || other.Data.HiddenBy == button.Data.ShortName));

    private void RespecAbilities(UpgradeButton button)
    {
      var screen = UntitledGemGameGameScreen.Instance;
      if (screen.IsPrestigeConfirmationOpen || screen.m_prestiging || screen.m_postPrestige
        || UpdatingButtons || (button != null && (!IsAbilityNode(button) || HasPurchasedDependents(button))))
        return;
      if (!m_gameState.TryRefundAbilityPoints(RefundedPoints(button))) return;

      var levels = CaptureLevels(CurrentUpgrades.UpgradeButtonsAbilities);
      var equipped = HomeBase.Instance.GetEquippedAbilities();
      if (button == null) levels.Clear();
      else levels[button.Data.ShortName]--;
      HomeBase.Instance.ResetAbilities();
      foreach (var definition in CurrentUpgrades.UpgradeDefinitionsAbilities.Values)
        UGA.Reset(definition.ShortName);
      RestoreTree(CurrentUpgrades.UpgradeButtonsAbilities, CurrentUpgrades.UpgradeJointsAbilities, levels);
      foreach (var node in CurrentUpgrades.UpgradeButtonsAbilities.Values)
        if (node.CurrentLevel > 0) HomeBase.Instance.ActivateAbility(node.Data.ShortName);
      HomeBase.Instance.RestoreEquippedAbilities(equipped);
      HideTooltip();
      screen.SaveProgress();
    }

    private void UpdateRespecTooltip(UpgradeButton button)
    {
      bool all = button.Data.ShortName == "ResetAbilities1";
      if (!all && !IsAbilityNode(button)) return;
      ulong points = RefundedPoints(all ? null : button);
      string price = m_gameState.GetRespecCost(points) is ulong cost
        ? NumberFormatter.AbbreviateBigNumber(cost) : "Unavailable";
      string description = button.Data.UpgradeDefinition.Tooltip;
      if (all)
      {
        m_tooltipDescription.Text = $"Refund all {points} ability points.\nCosts 10% of this prestige's peak gems/min per point (minimum 10 red gems per point).";
        m_tooltipCost.Text = points > 0 ? price : "No points to refund";
        m_tooltipCost.FillColor = points > 0 && m_gameState.GetRespecCost(points) is ulong total
          && m_gameState.CurrentRedGemCount >= total ? greenColor : redColor;
        m_tooltipCostIconRed.Visible = true;
        m_tooltipCostIconBlue.Visible = m_tooltipCostIconPurple.Visible = false;
      }
      else if (points > 0)
      {
        m_tooltipDescription.Text = description + (HasPurchasedDependents(button)
          ? "\nRefund dependent ranks first."
          : $"\nRight-click: refund one rank ({points} point(s)) for {price} red gems.");
      }
    }
  }
}
