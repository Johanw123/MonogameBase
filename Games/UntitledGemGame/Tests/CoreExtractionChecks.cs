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
    string[] tierOne = ["CC1", "OH1", "FLR1", "DCM1", "TPM1"];
    string[] tierTwo = ["GM1", "MHF1", "CAT1"];
    string[] tierThree = ["MA1", "RCM1", "JHM1", "MGD1", "CA1"];
    (string[] Talents, int Level)[] steps =
    [
      ([], 1),
      (tierOne[..3], 2),
      (tierOne, 3),
      ([.. tierOne, .. tierTwo, .. tierThree[..2]], 4),
      ([.. tierOne, .. tierTwo, .. tierThree, "QEM1", "MGS1", "MCSN1"], 5),
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
      Abilities = new() { ["AS1"] = 1 } });
    Check(manager.UG.HarvesterCount > 0 && manager.UG.AutoRefuel, "The fixture must restore a run");
    manager.ResetUpgrades();
    Check(upgrades.UpgradeButtons.Values.All(b => b.CurrentLevel == 0)
      && upgrades.UpgradeButtons["HB"].State == UpgradeButton.UnlockState.Unlocked
      && upgrades.UpgradeJoints.Values.All(j => j.State == UpgradeJoint.JointState.Hidden),
      "Extraction must reset the whole regular tree back to the home base");
    Check(manager.UG.HarvesterCount == 0 && !manager.UG.AutoRefuel && !manager.UG.HarvesterUnlocked,
      "Extraction must clear the run's upgrade effects");
    Check(upgrades.UpgradeButtonsAbilities["AS1"].CurrentLevel == 1, "Extraction must keep abilities");
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
