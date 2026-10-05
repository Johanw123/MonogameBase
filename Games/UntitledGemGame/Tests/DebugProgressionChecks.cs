using UntitledGemGame;

static class DebugProgressionChecks
{
  public static void Run(Upgrades upgrades)
  {
    // Constructing the replacement must leave the live session available to UnloadContent.
    var activeManager = UpgradeManager.Instance;
    var activeScreen = UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance;
    var game = (Microsoft.Xna.Framework.Game)System.Runtime.CompilerServices.RuntimeHelpers
      .GetUninitializedObject(typeof(Microsoft.Xna.Framework.Game));
    _ = new UntitledGemGame.Screens.UntitledGemGameGameScreen(game);
    if (!ReferenceEquals(UpgradeManager.Instance, activeManager)
      || !ReferenceEquals(UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance, activeScreen))
      throw new Exception("Replacement screen construction changed the outgoing session before teardown");

    for (int stage = 0; stage < DebugProgressionPresets.Names.Length; stage++)
    {
      var save = DebugProgressionPresets.Create(stage, upgrades);
      string[] unlocks = ["HU1", "AHU1", "EHU1", "UHU1", "PHU1"];
      string[] achievements = ["unlock_drifter", "unlock_seeker", "unlock_prospector", "unlock_trove_hunter", "unlock_rimrunner"];
      for (int type = 0; type < unlocks.Length; type++)
        if (save.HarvesterUnlockAchievements.Contains(achievements[type]) != (save.Upgrades.GetValueOrDefault(unlocks[type]) > 0))
          throw new Exception($"{DebugProgressionPresets.Names[stage]} has incorrect discovery for {unlocks[type]}");
      save.Modules.Validate();
      save.Signals.Validate();
      // Kamikaze Drones swaps Drone Swarm for the Kamikaze Wing: only one of them is learnable.
      bool kamikaze = save.Meta.GetValueOrDefault(PrestigeTalentEffects.KamikazeDronesTalent) > 0;
      foreach (var (buttons, levels) in new[] {
        (upgrades.UpgradeButtons, save.Upgrades),
        (upgrades.UpgradeButtonsAbilities, save.Abilities),
        (upgrades.UpgradeButtonsMeta, save.Meta) })
        foreach (var (id, button) in buttons)
        {
          // Talents come only from the prestige tree, whose tiers replace the legacy links.
          bool talent = ReferenceEquals(buttons, upgrades.UpgradeButtonsMeta);
          if (talent && !PrestigeTalentLayout.IsInTree(id))
          {
            if (levels.ContainsKey(id)) throw new Exception($"Preset bought legacy talent {id}");
            continue;
          }
          bool swappedOut = ReferenceEquals(buttons, upgrades.UpgradeButtonsAbilities)
            && !ShipSystems.IsTabAvailable(ShipSystems.TabOf(id), kamikaze);
          if (swappedOut && levels.GetValueOrDefault(id) > 0)
            throw new Exception($"Preset learned {id} from a system its talents swapped out");
          if (stage == DebugProgressionPresets.Names.Length - 1 && !swappedOut && levels.GetValueOrDefault(id) != button.Data.NumLevels)
            throw new Exception($"Endgame did not max {id}");
          if (!talent && levels.ContainsKey(id) && !string.IsNullOrEmpty(button.Data.BlockedBy)
            && !levels.ContainsKey(button.Data.BlockedBy))
            throw new Exception($"Preset skipped prerequisite for {id}");
          if (ReferenceEquals(buttons, upgrades.UpgradeButtonsAbilities) && levels.GetValueOrDefault(id) > 0
            && !ShipSystems.CanLearn(buttons, id, other => levels.GetValueOrDefault(other), kamikaze))
            throw new Exception($"Preset learned {id} without its tier requirement");
        }
      if (save.Abilities.Count > 0 && save.Meta.GetValueOrDefault(ShipSystems.UnlockTalent) <= 0)
        throw new Exception($"{DebugProgressionPresets.Names[stage]} uses ship systems without Auxiliary Power");
      if (stage > 0 && stage < DebugProgressionPresets.Names.Length - 1)
      {
        ulong spent = 0;
        foreach (var (id, level) in save.Upgrades)
          if (id is not ("HB" or "HU1"))
            foreach (var info in upgrades.UpgradeButtons[id].Data.LevelInfo.Take(level)) spent += info.Cost;
        for (ulong point = 0; point < save.AbilityPointsPurchased; point++)
          spent += AbilityPointProgression.GetPrice(point) ?? throw new Exception("Unaffordable ability point count");
        var signals = new SignalProgression();
        for (ulong scan = 0; scan < save.Signals.ScansPurchased; scan++)
        {
          spent += signals.ScanCost ?? throw new Exception("Unaffordable scan count");
          signals.ScansPurchased++;
        }
        if (spent + save.RedGems != save.RedGemsEarnedThisRun)
          throw new Exception("Preset spending does not match its earned gems");
        ulong abilityCost = save.BlueGems;
        foreach (var (id, level) in save.Abilities)
          foreach (var info in upgrades.UpgradeButtonsAbilities[id].Data.LevelInfo.Take(level)) abilityCost += info.Cost;
        if (abilityCost != save.AbilityPointsPurchased)
          throw new Exception("Preset ability points do not match its tree and wallet");
        if ((ulong)save.Signals.Counts.Sum() != save.Signals.ScansPurchased)
          throw new Exception("Preset signal counts do not match paid scans");
        if (DebugProgressionPresets.ProgressionStage(stage) >= 3 && (save.Modules.Owned.Count == 0 || save.Signals.ScansPurchased == 0))
          throw new Exception("Developed presets must include shipyard and signals");
      }
      if (stage is >= 8 and <= 10)
      {
        var baseline = DebugProgressionPresets.Create(DebugProgressionPresets.ProgressionStage(stage), upgrades);
        bool Clicking(JsonUpgrade definition) => definition.PropertyName.StartsWith("Click")
          || definition.PropertyName.StartsWith("HoldClick") || definition.PropertyName.StartsWith("CursorGravity");
        ulong ClickSpending(GameSave snapshot) => snapshot.Upgrades.Where(pair => Clicking(upgrades.UpgradeButtons[pair.Key].Data.UpgradeDefinition))
          .Aggregate(0UL, (sum, pair) => sum + upgrades.UpgradeButtons[pair.Key].Data.LevelInfo.Take(pair.Value)
            .Aggregate(0UL, (cost, info) => cost + info.Cost));
        if (ClickSpending(save) < ClickSpending(baseline) || save.AbilityPointsPurchased > baseline.AbilityPointsPurchased)
          throw new Exception("Clicking build did not prioritize manual collection over ability spending");
        if (stage == 8 && ClickSpending(save) <= ClickSpending(baseline))
          throw new Exception("Early clicking build must invest more in manual collection than the general build");
        if (stage >= 9)
        {
          long clicks = Enumerable.Range((int)SignalKind.ClickValue, SignalProgression.SignalCount - (int)SignalKind.ClickValue)
            .Sum(save.Signals.StackCount);
          if (clicks <= save.Signals.Counts.Sum() / 2 || save.Upgrades.GetValueOrDefault("CGE1") == 0
            || save.Upgrades.GetValueOrDefault("HCE1") == 0)
            throw new Exception("Developed clicking build is missing manual systems or focused signals");
        }
      }
      string path = Path.Combine(Path.GetTempPath(), $"preset-{Guid.NewGuid():N}.json");
      try
      {
        var store = new GameSaveStore(path);
        if (!store.Save(save) || store.Load() is not {} loaded
          || loaded.Modules.Owned.Count != save.Modules.Owned.Count
          || !loaded.HarvesterUnlockAchievements.SetEquals(save.HarvesterUnlockAchievements)
          || loaded.AbilityPointsPurchased != save.AbilityPointsPurchased
          || !loaded.Upgrades.OrderBy(pair => pair.Key).SequenceEqual(save.Upgrades.OrderBy(pair => pair.Key))
          || !loaded.Signals.Counts.SequenceEqual(save.Signals.Counts))
          throw new Exception("Preset save round trip failed");
      }
      finally { File.Delete(path); }
      if (stage == DebugProgressionPresets.Names.Length - 1 && !save.Modules.CollectionComplete) throw new Exception("Incomplete endgame modules");
    }
    for (int feature = 0; feature < DebugProgressionPresets.FeatureNames.Length; feature++)
    {
      var save = DebugProgressionPresets.CreateFeature(feature, upgrades);
      save.Modules.Validate();
      save.Signals.Validate();
      if (feature == 1)
      {
        foreach (string id in DebugProgressionPresets.FullWeaponNodes)
          if (save.Upgrades.GetValueOrDefault(id) != upgrades.UpgradeButtons[id].Data.NumLevels)
            throw new Exception("Fully upgraded ability scenario must retain its gem-production ranks: " + id);
        if (new[] { "HU1", "AHU1", "EHU1", "UHU1", "PHU1" }.Any(save.Upgrades.ContainsKey))
          throw new Exception("Fully upgraded ability scenario must not unlock harvesters");
      }
      if (feature is 0 or 1 && (save.Meta.GetValueOrDefault(ShipSystems.UnlockTalent) <= 0
        || ShipSystems.Tabs.Where((_, index) => ShipSystems.IsTabAvailable(index,
          save.Meta.GetValueOrDefault(PrestigeTalentEffects.KamikazeDronesTalent) > 0))
          .Any(tab => save.Abilities.GetValueOrDefault(tab.Root) <= 0)))
        throw new Exception("Ship system scenarios must bring every system online");
      if (feature == 7 && save.Modules.PendingReveals.Count != 3)
        throw new Exception("Discovery scenario must queue three reveals");
      if (feature == 8 && save.Signals.PendingChoices.Count != 3)
        throw new Exception("Signal scenario must offer three choices");
    }
    var nearlyComplete = new ShipyardModules();
    nearlyComplete.DiscoverAllModules();
    nearlyComplete.Owned.Remove(ShipModule.SupernovaCore);
    nearlyComplete.DiscoveryRarity = ModuleRarity.Legendary;
    nearlyComplete.DiscoveryThresholdSeconds = ShipyardModules.MinimumDiscoverySeconds(ModuleRarity.Legendary);
    DebugProgressionPresets.QueueDiscoveries(nearlyComplete);
    nearlyComplete.Validate();
    if (!nearlyComplete.PendingReveals.Contains(ShipModule.SupernovaCore))
      throw new Exception("Completing the collection must preserve the final discovery animation");
    var beginning = DebugProgressionPresets.Create(0, upgrades);
    if (beginning.Meta.Count != 0 || beginning.Abilities.Count != 0 || beginning.Modules.Owned.Count != 0
      || beginning.Signals.Counts.Any(c => c != 0)) throw new Exception("Beginning retains progression");
    CheckLiveChanges(upgrades);
    Console.WriteLine("Passed progression preset checks.");
  }
  private static void CheckLiveChanges(Upgrades upgrades)
  {
    var previousManager = UpgradeManager.Instance;
    var previousUpgrades = UpgradeManager.CurrentUpgrades;
    var levels = upgrades.UpgradeButtons.Values.Concat(upgrades.UpgradeButtonsMeta.Values)
      .Concat(upgrades.UpgradeButtonsAbilities.Values).ToDictionary(button => button, button => button.CurrentLevel);
    try
    {
      var manager = new UpgradeManager();
      UpgradeManager.CurrentUpgrades = upgrades;
      foreach (var button in levels.Keys) button.CurrentLevel = 0;
      manager.UG.AdvancedHarvesterSpeed = 7.5f;
      manager.SetDebugLevels(upgrades.UpgradeButtonsAbilities, upgrades.UpgradeJointsAbilities,
        new() { ["Drones1"] = 1 });
      int drones = manager.UGA.Drones;
      manager.SetDebugLevels(upgrades.UpgradeButtonsMeta, upgrades.UpgradeJointsMeta,
        new() { ["SYU1"] = 1 });
      manager.Modules.Validate();
      if (!manager.UGM.ShipyardUnlocked || manager.UGA.Drones != drones
        || manager.UG.AdvancedHarvesterSpeed != 7.5f || upgrades.UpgradeButtonsAbilities["Drones1"].CurrentLevel != 1)
        throw new Exception("Live shipyard grants changed unrelated abilities or slider stats");
      manager.SetDebugLevels(upgrades.UpgradeButtons, upgrades.UpgradeJoints,
        new() { ["AHU1"] = 1, ["AHC1"] = 2 });
      int ships = manager.UG.AdvancedHarvesterCount;
      manager.SetDebugLevels(upgrades.UpgradeButtons, upgrades.UpgradeJoints,
        new() { ["AHU1"] = 1, ["AHC1"] = 2 });
      if (ships <= 1 || ships != manager.UG.AdvancedHarvesterCount)
        throw new Exception("Repeated live fleet grants duplicated unlock ships");
      var remove = upgrades.UpgradeButtons.Where(p => p.Key == "AHU1"
        || p.Value.Data.UpgradeDefinition.ShortName == "AHC").ToDictionary(p => p.Key, _ => 0);
      manager.SetDebugLevels(upgrades.UpgradeButtons, upgrades.UpgradeJoints, remove);
      if (manager.UG.AdvancedHarvesterCount != 0 || manager.UG.AdvancedHarvesterUnlocked)
        throw new Exception("Live fleet removal left ships unlocked");
      manager.Modules.DiscoverAllModules();
      manager.Modules.Owned.Remove(ShipModule.SupernovaCore);
      DebugProgressionPresets.RepairDiscoveryTarget(manager.Modules);
      manager.Modules.Validate();
      var save = new GameSave();
      manager.CaptureProgress(save);
      if (save.Meta.GetValueOrDefault("SYU1") != 1 || save.Abilities.GetValueOrDefault("Drones1") != 1
        || save.Upgrades.ContainsKey("AHU1"))
        throw new Exception("Live changes were not captured for saving");
    }
    finally
    {
      foreach (var (button, level) in levels) button.CurrentLevel = level;
      UpgradeManager.Instance = previousManager;
      UpgradeManager.CurrentUpgrades = previousUpgrades;
    }
  }

}
