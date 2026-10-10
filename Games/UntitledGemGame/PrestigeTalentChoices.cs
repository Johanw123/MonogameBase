using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame.Screens;

namespace UntitledGemGame
{
  // Prestige talents: either/or choices (PrestigeTalentLayout.ExclusiveGroups) and unlearning
  // one talent at a time instead of refunding them all.
  public partial class UpgradeManager
  {
    // Right-click on the talent tree: between runs, or mid-run with the tree opened for
    // debugging (F3), where talents can be bought too. The Talents tab's view is read-only.
    private void UnlearnTalentFromTree(UpgradeButton button)
    {
      var screen = UntitledGemGameGameScreen.Instance;
      if (screen == null || screen.m_prestiging || UpdatingButtons
        || RenderGuiSystem.Instance?.TalentsReadOnly == true) return;
      if (!UnlearnPrestigeTalent(button)) return;
      ShowTooltip(button.Button.Visual, button.Button.Name, false);
    }

    // Gives back the point of one owned talent, as long as later tiers keep the points they need.
    public bool UnlearnPrestigeTalent(UpgradeButton button)
    {
      var buttons = CurrentUpgrades.UpgradeButtonsMeta;
      if (!PrestigeTalentLayout.CanUnlearn(buttons, button.Data.ShortName)) return false;

      ulong refund = button.Data.LevelInfo[button.CurrentLevel - 1].Cost;
      m_gameState.CurrentPurpleGemCount = PrestigeProgression.AddSaturating(m_gameState.CurrentPurpleGemCount, refund);
      button.CurrentLevel--;
      string stat = button.Data.UpgradeDefinition.ShortName;
      UGM.Reset(stat);
      foreach (var node in buttons.Values.Where(node => node.Data.UpgradeDefinition.ShortName == stat))
        for (int level = 0; level < node.CurrentLevel; level++)
          ApplyUpgradeEffect(node.Data, node.Data.LevelInfo[level]);
      // Free rewards are claimed again from the tiers still reached.
      foreach (var (id, node) in buttons)
        if (PrestigeTalentLayout.IsFreeReward(id)) node.CurrentLevel = 0;
      RefreshRestoredTree(buttons, CurrentUpgrades.UpgradeJointsMeta);
      ApplyExpandSpace();
      HideTooltip();
      UntitledGemGameGameScreen.Instance?.SaveProgress();
      return true;
    }

    // Tooltip line for a talent: what it can't be combined with. The node's label names the
    // talent that rules it out, and the panel's header says how to unlearn.
    private string TalentChoiceNotes(Dictionary<string, UpgradeButton> buttons, UpgradeButton button)
    {
      string id = button.Data.ShortName;
      if (!PrestigeTalentLayout.IsInTree(id)) return "";
      var rivals = PrestigeTalentLayout.ExclusiveWith(id).Where(buttons.ContainsKey)
        .Select(other => Loc.T(buttons[other].Data.UpgradeDefinition.Name)).ToList();
      return rivals.Count > 0
        ? Environment.NewLine + Environment.NewLine + Loc.F("Can't be combined with {0}.", string.Join(", ", rivals))
        : "";
    }
  }
}
