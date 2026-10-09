using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame;

public sealed class DebugFeatureTools
{
  private int shipType;
  private int moduleIndex;
  private int rarity;
  private int signal;
  private static readonly string[] ShipNames = ["Drifter", "Seeker", "Prospector", "Trove hunter", "Rimrunner"];
  private static readonly string[] Unlocks = ["HU1", "AHU1", "EHU1", "UHU1", "PHU1"];
  private static readonly string[] Counts = ["HC1", "AHC1", "EHC1", "UHC1", "PHC1"];

  public void Draw(UpgradeManager manager, GameState state, Action<Action> queue)
  {
    if (!ImGui.CollapsingHeader("Live feature tools")) return;
    ImGui.TextWrapped("Mix systems into your current build. Actions are free, apply on the next update and save progress. Debug grants bypass progression requirements.");
    var upgrades = UpgradeManager.CurrentUpgrades;
    void Button(string label, Action action) { if (ImGui.Button(label)) queue(action); }
    void Meta(params (string id, int level)[] levels) => manager.SetDebugLevels(upgrades.UpgradeButtonsMeta,
      upgrades.UpgradeJointsMeta, levels.ToDictionary(p => p.id, p => p.level));

    if (ImGui.TreeNode("Shipyard and modules"))
    {
      Button("Unlock shipyard", () => { Meta(("SYU1", 1)); state.Modules.StartSalvage(Random.Shared); });
      Button("Remove shipyard and signals", () =>
      {
        Meta(("SYU1", 0), ("SGU1", 0));
        if (RenderGuiSystem.Instance.m_upgradeWindowType is RenderGuiSystem.UpgradeTypes.Shipyard or RenderGuiSystem.UpgradeTypes.Signals)
          RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
      });
      ImGui.TextDisabled("Removing unlocks keeps collections; clear them separately below.");
      Button("Grant all modules", () => { Meta(("SYU1", 1)); state.Modules.DiscoverAllModules(); });
      Button("Reset module collection and equipment", () =>
      {
        state.Modules = new ShipyardModules();
        if (manager.UGM.ShipyardUnlocked) state.Modules.StartSalvage(Random.Shared);
      });
      Button("Unequip all modules", () => Array.Clear(state.Modules.Slots));
      Button("Queue discovery animations", () => { Meta(("SYU1", 1)); DebugProgressionPresets.QueueDiscoveries(state.Modules); });
      ImGui.Combo("Module", ref moduleIndex, ModuleCatalog.Names.Skip(1).ToArray(), ModuleCatalog.Names.Length - 1);
      var selectedModule = (ShipModule)(moduleIndex + 1);
      Button("Grant selected module", () =>
      {
        Meta(("SYU1", 1));
        state.Modules.StartSalvage(Random.Shared);
        state.Modules.Owned.Add(selectedModule);
        state.Modules.PendingReveals.Remove(selectedModule);
        DebugProgressionPresets.RepairDiscoveryTarget(state.Modules);
      });
      Button("Remove selected module", () =>
      {
        // Salvage always owns its two starter modules.
        if (selectedModule is ShipModule.CargoPod or ShipModule.IonBooster) return;
        state.Modules.Owned.Remove(selectedModule);
        state.Modules.PendingReveals.Remove(selectedModule);
        for (int i = 0; i < state.Modules.Slots.Length; i++)
          if (state.Modules.Slots[i] == selectedModule) state.Modules.Slots[i] = ShipModule.None;
        DebugProgressionPresets.RepairDiscoveryTarget(state.Modules);
      });
      ImGui.TextDisabled("Cargo Pod and Ion Booster are salvage starter modules; use clear collection to reset them.");
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Fleet"))
    {
      ImGui.Combo("Harvester class", ref shipType, ShipNames, ShipNames.Length);
      int type = shipType;
      Button("Add three harvesters", () =>
      {
        var levels = new Dictionary<string, int> { [Unlocks[type]] = 1 };
        var node = upgrades.UpgradeButtons[Counts[type]];
        levels[Counts[type]] = Math.Min(node.Data.NumLevels, node.CurrentLevel + 3);
        manager.SetDebugLevels(upgrades.UpgradeButtons, upgrades.UpgradeJoints, levels);
      });
      Button("Remove selected class", () =>
      {
        string countStat = upgrades.UpgradeButtons[Counts[type]].Data.UpgradeDefinition.ShortName;
        var levels = upgrades.UpgradeButtons.Where(p => p.Key == Unlocks[type]
          || p.Value.Data.UpgradeDefinition.ShortName == countStat).ToDictionary(p => p.Key, _ => 0);
        manager.SetDebugLevels(upgrades.UpgradeButtons, upgrades.UpgradeJoints, levels);
      });
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Ship systems"))
    {
      void Apply(bool max, bool clear)
      {
        if (!clear) Meta((ShipSystems.UnlockTalent, 1));
        var oldEquipped = HomeBase.Instance.GetEquippedAbilities();
        HomeBase.Instance.ResetAbilities();
        // Only the systems that can be learned: Drone Swarm or, with Kamikaze Drones, the Kamikaze Wing.
        var levels = upgrades.UpgradeButtonsAbilities.ToDictionary(p => p.Key,
          p => clear || !ShipSystems.IsTabAvailable(ShipSystems.TabOf(p.Key)) ? 0
            : max ? p.Value.Data.NumLevels : ShipSystems.Tabs.Any(tab => tab.Root == p.Key) ? 1 : 0);
        manager.SetDebugLevels(upgrades.UpgradeButtonsAbilities, upgrades.UpgradeJointsAbilities, levels);
        ulong points = state.CurrentBlueGemCount;
        foreach (var node in upgrades.UpgradeButtonsAbilities.Values)
          foreach (var info in node.Data.LevelInfo.Take(node.CurrentLevel))
            points = PrestigeProgression.AddSaturating(points, info.Cost);
        state.Restore(state.CurrentRedGemCount, state.CurrentBlueGemCount, state.CurrentPurpleGemCount,
          state.RedGemsEarnedThisRun, Math.Max(state.AbilityPointsPurchased, points));
        foreach (var node in upgrades.UpgradeButtonsAbilities.Values)
          if (node.CurrentLevel > 0) HomeBase.Instance.ActivateAbility(node.Data.ShortName);
        HomeBase.Instance.RestoreEquippedAbilities(clear ? new() : oldEquipped.Any(id => !string.IsNullOrEmpty(id))
          ? oldEquipped : new() { "GS1", ShipSystems.DroneSystemRoot(PrestigeTalentEffects.KamikazeDrones), "CM1" });
      }
      Button("Bring all systems online", () => Apply(false, false));
      Button("Max all system talents", () => Apply(true, false));
      Button("Remove all system talents", () => Apply(false, true));
      Button("Remove Auxiliary Power", () =>
      {
        Apply(false, true);
        Meta((ShipSystems.UnlockTalent, 0));
        if (RenderGuiSystem.Instance.m_upgradeWindowType == RenderGuiSystem.UpgradeTypes.Abilities)
          RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
      });
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Signals"))
    {
      Button("Unlock signals", () => { Meta(("SYU1", 1), ("SGU1", 1)); state.Modules.StartSalvage(Random.Shared); });
      Button("Clear signals and pending choices", () => state.Signals = new SignalProgression());
      ImGui.Combo("Signal", ref signal, SignalCatalog.Definitions.Select(s => s.Name).ToArray(), SignalProgression.SignalCount);
      ImGui.Combo("Rarity", ref rarity, new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary" }, SignalProgression.RarityCount);
      int index = signal * SignalProgression.RarityCount + rarity;
      Button("Add selected signal stack", () => { Meta(("SYU1", 1), ("SGU1", 1)); if (state.Signals.Counts[index] < long.MaxValue && state.Signals.StackCount(index / SignalProgression.RarityCount) < long.MaxValue) state.Signals.Counts[index]++; });
      Button("Remove selected signal stack", () => { if (state.Signals.Counts[index] > 0) state.Signals.Counts[index]--; });
      ImGui.TextDisabled("Free signal grants leave the paid scan price unchanged.");
      ImGui.TreePop();
    }
  }
}
