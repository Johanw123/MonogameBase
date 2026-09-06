using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame
{
  public partial class UpgradeManager
  {
    private void ApplyUpgradeEffect(UpgradeData upgradeData, UpgradeDataLevel currentLevelInfo)
    {
      if (upgradeData.UpgradeDefinition.Type == "float")
      {
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountFloat);
        UGA.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountFloat);
        UGM.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountFloat);
      }
      else if (upgradeData.UpgradeDefinition.Type == "int")
      {
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountInt);
        UGA.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountInt);
        UGM.Increment(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradeAmountInt);
      }
      else if (upgradeData.UpgradeDefinition.Type == "bool")
      {
        UG.Set(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradesToBool);
        UGA.Set(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradesToBool);
        UGM.Set(upgradeData.UpgradeDefinition.ShortName, currentLevelInfo.m_upgradesToBool);
      }

    }

    public void CaptureProgress(GameSave save)
    {
      save.Upgrades = CaptureLevels(CurrentUpgrades.UpgradeButtons);
      save.Abilities = CaptureLevels(CurrentUpgrades.UpgradeButtonsAbilities);
      save.Meta = CaptureLevels(CurrentUpgrades.UpgradeButtonsMeta);
    }

    private static Dictionary<string, int> CaptureLevels(Dictionary<string, UpgradeButton> buttons)
      => buttons.Where(pair => pair.Value.CurrentLevel > 0)
        .ToDictionary(pair => pair.Key, pair => pair.Value.CurrentLevel);

    // Called after all three trees have initialized their base values.
    // Apply only upgrade effects: loading must not spend currency, refund points, or trigger prestige.
    public void RestoreProgress(GameSave save)
    {
      RestoreTree(CurrentUpgrades.UpgradeButtons, CurrentUpgrades.UpgradeJoints, save.Upgrades);
      RestoreTree(CurrentUpgrades.UpgradeButtonsAbilities, CurrentUpgrades.UpgradeJointsAbilities, save.Abilities);
      RestoreTree(CurrentUpgrades.UpgradeButtonsMeta, CurrentUpgrades.UpgradeJointsMeta, save.Meta);
      foreach (var button in CurrentUpgrades.UpgradeButtons.Values
        .Concat(CurrentUpgrades.UpgradeButtonsAbilities.Values).Concat(CurrentUpgrades.UpgradeButtonsMeta.Values))
      {
        ulong balance = button.Data.UpgradeDefinition.Currency switch
        {
          "red" => save.RedGems,
          "blue" => save.BlueGems,
          "purple" => save.PurpleGems,
          _ => 0
        };
        button.CanAfford = !button.IsMaxLevel && !IsExpandSpaceLocked(button)
          && balance >= button.GetNextLevelCost();
      }
      if (CurrentUpgrades.UpgradeButtons.TryGetValue("HB", out var root) && root.CurrentLevel > 0)
        UG.HarvesterCount += 1;
    }

    private void RestoreTree(Dictionary<string, UpgradeButton> buttons,
      Dictionary<string, UpgradeJoint> joints, Dictionary<string, int> levels)
    {
      foreach (var (id, button) in buttons)
      {
        // Removed upgrades are ignored; shortened level lists are clamped to the current definition.
        levels.TryGetValue(id, out int level);
        button.CurrentLevel = Math.Clamp(level, 0, Math.Min(button.Data.NumLevels, button.Data.LevelInfo.Count));
        for (int i = 0; i < button.CurrentLevel; i++)
          ApplyUpgradeEffect(button.Data, button.Data.LevelInfo[i]);
      }

      bool Purchased(string id) => !string.IsNullOrEmpty(id)
        && buttons.TryGetValue(id, out var prerequisite) && prerequisite.CurrentLevel > 0;

      foreach (var button in buttons.Values)
      {
        var data = button.Data;
        var state = UpgradeButton.UnlockState.Invisible;
        bool root = string.IsNullOrEmpty(data.HiddenBy) && string.IsNullOrEmpty(data.LockedBy)
          && string.IsNullOrEmpty(data.BlockedBy);
        if (root || Purchased(data.BlockedBy))
          state = UpgradeButton.UnlockState.Unlocked;
        else if (Purchased(data.LockedBy))
          state = UpgradeButton.UnlockState.Revealed;
        else if (Purchased(data.HiddenBy))
          state = UpgradeButton.UnlockState.Hidden;

        // The prestige upgrade retains its level across runs but stays hidden until HB is bought again.
        if (button.CurrentLevel > 0 && (data.UpgradeDefinition.ShortName != "CZS" || Purchased("HB")))
          state = button.IsMaxLevel ? UpgradeButton.UnlockState.MaxedOut : UpgradeButton.UnlockState.Purchased;
        SetButtonState(button, state);
        button.ClickedTime = button.CurrentLevel > 0 ? 1.0f : 0.0f;
      }

      foreach (var joint in joints.Values)
      {
        joint.State = joint.EndButton.State switch
        {
          // Normal purchase animations finish at Purchased, including a button's final level.
          UpgradeButton.UnlockState.MaxedOut or UpgradeButton.UnlockState.Purchased => UpgradeJoint.JointState.Purchased,
          UpgradeButton.UnlockState.Unlocked or UpgradeButton.UnlockState.Revealed => UpgradeJoint.JointState.Unlocked,
          _ => UpgradeJoint.JointState.Hidden
        };
        // The renderer uses these fractions even when no animation is running.
        joint.UnlockingTime = joint.State == UpgradeJoint.JointState.Hidden ? 0.0f : 1.0f;
        joint.PurchasingTime = joint.State == UpgradeJoint.JointState.Purchased ? 1.0f : 0.0f;
      }
    }
  }
}
