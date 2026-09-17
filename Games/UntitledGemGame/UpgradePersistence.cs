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
        if (currentLevelInfo.m_upgradesToBool)
        {
          switch (upgradeData.UpgradeDefinition.ShortName)
          {
            case "HU": UG.HarvesterCount++; break;
            case "AHU": UG.AdvancedHarvesterCount++; break;
            case "PHU": UG.PerimeterHarvesterCount++; break;
            case "EHU": UG.ExpertHarvesterCount++; break;
            case "UHU": UG.UltimateHarvesterCount++; break;
          }
        }
      }

    }

    public void CaptureProgress(GameSave save)
    {
      save.Upgrades = CaptureLevels(CurrentUpgrades.UpgradeButtons);
      save.HasHarvesterUnlocks = true;
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
      var levels = MigrateGemQualityLevels(save.Upgrades);
      if (!save.HasHarvesterUnlocks)
      {
        if (levels.GetValueOrDefault("HB") > 0)
          levels["HU1"] = 1;
        foreach (string prefix in new[] { "AH", "EH", "UH" })
        {
          int ranks = Math.Clamp(levels.GetValueOrDefault(prefix + "C1"), 0, 5);
          if (ranks <= 0) continue;
          levels[prefix + "U1"] = 1;
          levels[prefix + "C1"] = ranks - 1;
        }
      }
      RestoreTree(CurrentUpgrades.UpgradeButtons, CurrentUpgrades.UpgradeJoints, levels);
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
    }

    private static Dictionary<string, int> MigrateGemQualityLevels(Dictionary<string, int> savedLevels)
    {
      var levels = new Dictionary<string, int>(savedLevels);
      // The old GSQ1 held five ranks. It now holds one, with the remaining
      // paid ranks distributed along the color branch. Capturing these split
      // levels makes the migration idempotent without changing the save schema.
      int legacyRanks = levels.TryGetValue("GSQ1", out int oldRanks) ? Math.Clamp(oldRanks, 0, 5) : 0;
      if (legacyRanks > 1)
        levels["GSQ1"] = 1;

      // Old saves could unlock colors without their quality prerequisites.
      // Supply the missing intervening ranks so those colors can actually spawn.
      string[] colors = { "ULG1", "UBL1", "UTE1", "ULI1", "UPU1", "UGO1", "UDB1" };
      int requiredRanks = legacyRanks;
      for (int i = 0; i < colors.Length; i++)
        if (levels.TryGetValue(colors[i], out int colorLevel) && colorLevel > 0)
          requiredRanks = Math.Max(requiredRanks, i);
      for (int i = 1; i <= requiredRanks; i++)
        levels[$"GSQ{i}"] = 1;
      return levels;
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
