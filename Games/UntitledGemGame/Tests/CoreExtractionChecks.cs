using UntitledGemGame;

internal static class CoreExtractionChecks
{
  public static void Run(Upgrades upgrades)
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      CheckTree(upgrades);
      CheckState();
      CheckExpandSpace(upgrades);
      CheckReset(upgrades);
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Core extraction checks passed: HUD-only extraction, counting, save/load, free Expand Space per tier, zoom and reset.");
  }

  private static void CheckTree(Upgrades upgrades)
  {
    Check(!upgrades.UpgradeButtons.ContainsKey("P1") && !upgrades.UpgradeButtons.ContainsKey("CZS1")
      && !upgrades.UpgradeDefinitions.ContainsKey("P"),
      "Extraction must not be an upgrade-tree node, and Expand Space must not be bought");
    Check(upgrades.UpgradeDefinitions.TryGetValue(CoreExtraction.ExpandSpaceStat, out var space)
      && space.PropertyName == "CameraZoomScale", "Expand Space must remain the camera zoom stat");
    Check(!CoreExtraction.CanExtract(0) && CoreExtraction.CanExtract(1),
      "Extracting must pay at least one prestige point");
  }

  private static void CheckState()
  {
    var state = new GameState();
    state.CompletePrestige(2);
    state.CompletePrestige(1);
    Check(state.CoreExtractions == 2 && state.CurrentPurpleGemCount == 3, "Every extraction must be counted");
    string directory = Path.Combine(Path.GetTempPath(), "core-extraction-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(new GameSave { CoreExtractions = 7 }), "Extraction count must save");
      Check(new GameSaveStore(store.SavePath).Load()?.CoreExtractions == 7, "Extraction count must load");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }

  private static void CheckExpandSpace(Upgrades upgrades)
  {
    string[] tierOne = PrestigeTalentLayout.Tiers[0].Talents;
    string[] tierTwo = PrestigeTalentLayout.Tiers[1].Talents;
    string[] tierThree = PrestigeTalentLayout.Tiers[2].Talents;
    (string[] Talents, int Level)[] steps =
    [
      ([], 1),
      (tierOne[..3], 2),
      (tierOne[..5], 3),
      ([.. tierOne, .. tierTwo[..4]], 4),
      ([.. tierOne, .. tierTwo, .. tierThree], 5),
    ];
    foreach (var (talents, level) in steps)
    {
      var manager = Extracted(1, new GameSave { Meta = talents.ToDictionary(id => id, _ => 1) });
      Check(manager.ExpandSpaceLevel == level
        && CoreExtraction.ExpandSpaceLevel(upgrades.UpgradeButtonsMeta, 1) == level,
        $"{talents.Length} talent points must reach Expand Space {level}");
      Check(Math.Abs(manager.UG.CameraZoomScale - (3.5f - CoreExtraction.ExpandSpaceZoomStep * level)) < 0.001f,
        "Restoring a save must apply the reached tiers' zoom");
      Check(CoreExtraction.ExpandSpaceLevel(upgrades.UpgradeButtonsMeta, 0) == 0,
        "No tier is reached before the first extraction");
    }

    var refund = Extracted(1, new GameSave { Meta = tierOne.ToDictionary(id => id, _ => 1) });
    refund.RespecPrestigeTalents();
    Check(refund.ExpandSpaceLevel == 1 && Math.Abs(refund.UG.CameraZoomScale - 3f) < 0.001f,
      "Refunding talents must give back the free Expand Space of the tiers left behind");

    var ug = new UpgradesGeneratorUpgrades();
    CoreExtraction.ApplyExpandSpace(ug, 5);
    CoreExtraction.ApplyExpandSpace(ug, 2);
    Check(Math.Abs(ug.CameraZoomScale - 2.5f) < 0.001f, "Reapplying Expand Space must not stack levels");
  }

  // Extraction can start from any view, so the reset must not depend on the open tree.
  private static void CheckReset(Upgrades upgrades)
  {
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Upgrades = new() { ["HB"] = 1, ["HU1"] = 1, ["HC1"] = 2, ["AR1"] = 1 },
      Abilities = new() { ["Drones1"] = 1, ["DroneSpeed1"] = 2 } });
    Check(manager.UG.HarvesterCount > 0 && manager.UG.AutoRefuel && manager.UGA.Drones == 1,
      "The fixture must restore a run");
    manager.ResetUpgrades();
    Check(upgrades.UpgradeButtons.Values.All(b => b.CurrentLevel == 0)
      && upgrades.UpgradeButtons["HB"].State == UpgradeButton.UnlockState.Unlocked
      && upgrades.UpgradeJoints.Values.All(j => j.State == UpgradeJoint.JointState.Hidden),
      "Extraction must reset the whole regular tree back to the home base");
    Check(manager.UG.HarvesterCount == 0 && !manager.UG.AutoRefuel && !manager.UG.HarvesterUnlocked,
      "Extraction must clear the run's upgrade effects");
    Check(upgrades.UpgradeButtonsAbilities["Drones1"].CurrentLevel == 1, "Resetting the regular tree must leave the systems alone");
    manager.ResetSystems();
    Check(upgrades.UpgradeButtonsAbilities.Values.All(b => b.CurrentLevel == 0) && manager.UGA.Drones == 0
      && Math.Abs(manager.UGA.DroneSpeed - 1f) < 0.001f
      && upgrades.UpgradeButtonsAbilities["Drones1"].State == UpgradeButton.UnlockState.Unlocked
      && upgrades.UpgradeButtonsAbilities["DroneSpeed1"].State == UpgradeButton.UnlockState.Revealed,
      "Extraction must take every system offline and clear its talents");
  }

  private static UpgradeManager Extracted(ulong extractions, GameSave save)
  {
    var manager = new UpgradeManager();
    typeof(UpgradeManager).GetField("m_gameState", System.Reflection.BindingFlags.Instance
      | System.Reflection.BindingFlags.NonPublic)!.SetValue(manager, new GameState { CoreExtractions = extractions });
    manager.RestoreProgress(save);
    return manager;
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
}
