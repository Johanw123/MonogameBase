using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame.Screens;

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
            case "HB": UG.HomeBaseCollector = true; break;
            case "SYU": Modules.StartSalvage(Random.Shared); break;
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
      save.HarvesterUnlockAchievements = new(harvesterUnlockAchievements);
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
      harvesterUnlockAchievements = new(save.HarvesterUnlockAchievements);
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
          CoreShards.Currency => save.CoreShards,
          _ => 0
        };
        button.CanAfford = !button.IsMaxLevel && !IsExpandSpaceLocked(button)
          && balance >= button.GetNextLevelCost();
      }
      ApplyExpandSpace();
    }

    // Rebuild only affected stats, preserving live slider changes in other systems.
    public void SetDebugLevels(Dictionary<string, UpgradeButton> buttons,
      Dictionary<string, UpgradeJoint> joints, Dictionary<string, int> levels)
    {
      var affected = new HashSet<string>();
      foreach (var (id, level) in levels)
      {
        if (!buttons.TryGetValue(id, out var button)) continue;
        affected.Add(button.Data.UpgradeDefinition.ShortName);
        button.CurrentLevel = Math.Clamp(level, 0, Math.Min(button.Data.NumLevels, button.Data.LevelInfo.Count));
      }
      var all = CurrentUpgrades.UpgradeButtons.Values.Concat(CurrentUpgrades.UpgradeButtonsAbilities.Values)
        .Concat(CurrentUpgrades.UpgradeButtonsMeta.Values);
      foreach (string stat in affected)
      {
        UG.Reset(stat); UGA.Reset(stat); UGM.Reset(stat);
        foreach (var button in all.Where(b => b.Data.UpgradeDefinition.ShortName == stat))
          foreach (var info in button.Data.LevelInfo.Take(button.CurrentLevel))
          {
            if (button.Data.UpgradeDefinition.Type == "float")
            { UG.Increment(stat, info.m_upgradeAmountFloat); UGA.Increment(stat, info.m_upgradeAmountFloat); UGM.Increment(stat, info.m_upgradeAmountFloat); }
            else if (button.Data.UpgradeDefinition.Type == "int")
            { UG.Increment(stat, info.m_upgradeAmountInt); UGA.Increment(stat, info.m_upgradeAmountInt); UGM.Increment(stat, info.m_upgradeAmountInt); }
            else
            { UG.Set(stat, info.m_upgradesToBool); UGA.Set(stat, info.m_upgradesToBool); UGM.Set(stat, info.m_upgradesToBool); }
          }
      }
      foreach (var (unlock, count) in new[] { ("HU", "HC"), ("AHU", "AHC"), ("EHU", "EHC"), ("UHU", "UHC"), ("PHU", "PHC") })
      {
        if (!affected.Contains(unlock) && !affected.Contains(count)) continue;
        UG.Reset(count);
        foreach (var node in CurrentUpgrades.UpgradeButtons.Values.Where(b => b.Data.UpgradeDefinition.ShortName == count))
          foreach (var info in node.Data.LevelInfo.Take(node.CurrentLevel)) UG.Increment(count, info.m_upgradeAmountInt);
        if (UG.GetBool(unlock, out bool enabled) && enabled)
        {
          UG.Increment(count, 1);
          if (GetHarvesterUnlockAchievementId(unlock) is { } achievement) harvesterUnlockAchievements.Add(achievement);
        }
      }
      if (affected.Contains("SYU") && UGM.ShipyardUnlocked) Modules.StartSalvage(Random.Shared);
      RefreshRestoredTree(buttons, joints);
      ApplyExpandSpace();
      HideTooltip();
    }

    // Free and lasts until prestige, like the Core Shard node it stands in for.
    public void GrantDebugAutoRefuel()
    {
      var button = CurrentUpgrades.UpgradeButtons["AR1"];
      if (button.CurrentLevel > 0) return;
      ApplyUpgradeEffect(button.Data, button.Data.LevelInfo[0]);
      button.CurrentLevel = 1;
      RefreshRestoredTree(CurrentUpgrades.UpgradeButtons, CurrentUpgrades.UpgradeJoints);
    }

    public ulong RespecPrestigeTalents()
    {
      var buttons = CurrentUpgrades.UpgradeButtonsMeta;
      ulong refund = PrestigeTalentLayout.SpentPoints(buttons);
      if (refund == 0) return 0;

      m_gameState.CurrentPurpleGemCount = PrestigeProgression.AddSaturating(
        m_gameState.CurrentPurpleGemCount, refund);
      foreach (var button in buttons.Values)
      {
        button.CurrentLevel = 0;
        UGM.Reset(button.Data.UpgradeDefinition.ShortName);
      }
      RefreshRestoredTree(buttons, CurrentUpgrades.UpgradeJointsMeta);
      ApplyExpandSpace();
      HideTooltip();
      UntitledGemGameGameScreen.Instance?.SaveProgress();
      return refund;
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

      RefreshRestoredTree(buttons, joints);
    }

    private void RefreshRestoredTree(Dictionary<string, UpgradeButton> buttons,
      Dictionary<string, UpgradeJoint> joints)
    {
      bool prestigeTalents = ReferenceEquals(buttons, CurrentUpgrades.UpgradeButtonsMeta);
      bool Purchased(string id) => !string.IsNullOrEmpty(id)
        && buttons.TryGetValue(id, out var prerequisite) && prerequisite.CurrentLevel > 0;

      foreach (var button in buttons.Values)
      {
        var data = button.Data;
        var state = UpgradeButton.UnlockState.Invisible;
        if (prestigeTalents)
          state = !PrestigeTalentLayout.IsInTree(data.ShortName)
            ? UpgradeButton.UnlockState.Invisible
            : PrestigeTalentLayout.IsUnlocked(buttons, data.ShortName)
              ? UpgradeButton.UnlockState.Unlocked : UpgradeButton.UnlockState.Revealed;
        bool root = string.IsNullOrEmpty(data.HiddenBy) && string.IsNullOrEmpty(data.LockedBy)
          && string.IsNullOrEmpty(data.BlockedBy);
        if (!prestigeTalents && (root || Purchased(data.BlockedBy)))
          state = UpgradeButton.UnlockState.Unlocked;
        else if (!prestigeTalents && Purchased(data.LockedBy))
          state = UpgradeButton.UnlockState.Revealed;
        else if (!prestigeTalents && Purchased(data.HiddenBy))
          state = UpgradeButton.UnlockState.Hidden;

        if (button.CurrentLevel > 0 && (!prestigeTalents || PrestigeTalentLayout.IsInTree(data.ShortName)))
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
