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
      CheckLadder();
      CheckExpandSpace(upgrades);
      CheckFirstRunShards(upgrades);
      CheckReset(upgrades);
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Core extraction checks passed: HUD-only extraction, counting, save/load, the income ladder and echo, free Expand Space per tier, zoom and reset.");
  }

  private static void CheckTree(Upgrades upgrades)
  {
    Check(!upgrades.UpgradeButtons.ContainsKey("P1") && !upgrades.UpgradeButtons.ContainsKey("CZS1")
      && !upgrades.UpgradeDefinitions.ContainsKey("P"),
      "Extraction must not be an upgrade-tree node, and Expand Space must not be bought");
    Check(upgrades.UpgradeDefinitions.TryGetValue(CoreExtraction.ExpandSpaceStat, out var space)
      && space.PropertyName == "CameraZoomScale", "Expand Space must remain the camera zoom stat");
    Check(!CoreExtraction.CanExtract(0, 0) && CoreExtraction.CanExtract(1, 0),
      "The first extraction must pay at least one prestige point");
    Check(CoreExtraction.CanExtract(0, 1), "After the first extraction, a run without a point can still end and leave an echo");
  }

  private static void CheckState()
  {
    var state = new GameState();
    state.RestorePrestige(2, 2, 0, 0);
    state.CompletePrestige();
    state.RestorePrestige(3, 1, 0, 0);
    state.CompletePrestige();
    Check(state.CoreExtractions == 2 && state.CurrentPurpleGemCount == 3 && state.PendingPrestigePoints == 0
      && state.PrestigePointsEarned == 3, "Every extraction must be counted and pay the run's points");
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

  private static void CheckLadder()
  {
    Check(PrestigeProgression.Threshold(0) == PrestigeProgression.FirstThreshold
      && PrestigeProgression.Threshold(1) > PrestigeProgression.Threshold(0)
      && PrestigeProgression.Progress(1e300, 10_000) == 0 && PrestigeProgression.Progress(-5, 0) == 0,
      "Each point must need more income than the last, and runaway ladders must read as no progress");
    for (ulong n = 0; n < 60; n++)
    {
      double step = PrestigeProgression.Threshold(n + 1) / PrestigeProgression.Threshold(n);
      double expected = Math.Min(PrestigeProgression.MaxThresholdStep,
        PrestigeProgression.ThresholdGrowth * Math.Pow(PrestigeProgression.GrowthSteepening, n));
      Check(Math.Abs(step / expected - 1) < 1e-9, $"Point {n + 1} must steepen until the cap, then hold it");
    }
    Check(PrestigeProgression.MaxThresholdStep == CoreExtraction.GemLore.Max(),
      "The ladder's steepest step must match the best talent's Gem Lore, or late progress stalls");

    var state = new GameState();
    ulong first = (ulong)PrestigeProgression.FirstThreshold;
    state.EarnRedGems(first - 1);
    Check(state.UpdatePrestigeProgress() == 0 && state.PendingPrestigePoints == 0, "Income short of the bar earns nothing");
    state.EarnRedGems(1);
    Check(state.UpdatePrestigeProgress() == 1 && state.PendingPrestigePoints == 1 && state.PrestigePointsEarned == 1,
      "A minute of income at the threshold earns a point");
    Check(state.UpdatePrestigeProgress() == 0, "The same income must not pay the next point");
    double best = first / PrestigeProgression.Threshold(1);
    Check(Math.Abs(state.BestPrestigeProgress - best) < 1e-9, "Progress toward the next point is measured against its own bar");
    for (int second = 0; second <= RollingMinute.WindowSeconds; second++) state.Income.Update(1f);
    Check(state.Income.PerMinute == 0 && state.UpdatePrestigeProgress() == 0 && Math.Abs(state.BestPrestigeProgress - best) < 1e-9,
      "Income leaves the window after a minute, but the run's best progress stays");

    state.CompletePrestige();
    Check(state.CurrentPurpleGemCount == 1 && state.PendingPrestigePoints == 0 && state.PrestigePointsEarned == 1
      && Math.Abs(state.PrestigeEcho - best * best) < 1e-9 && state.BestPrestigeProgress == 0 && state.Income.PerMinute == 0,
      "Extraction pays the points and banks the square of the run's best progress as the echo");
    // The echo goes into the next point and is spent by it.
    state.EarnRedGems((ulong)Math.Ceiling((1 - state.PrestigeEcho) * PrestigeProgression.Threshold(1)));
    Check(state.UpdatePrestigeProgress() == 1 && state.PrestigeEcho == 0 && state.PrestigePointsEarned == 2,
      "The echo and this run's income together fill the bar");

    // A short run late in the game leaves almost nothing.
    var late = new GameState();
    late.RestorePrestige(20, 0, 0, 0);
    late.EarnRedGems(first);
    late.UpdatePrestigeProgress();
    Check(late.EchoAfterExtraction < 1e-4, "A short run must not farm the echo");

    var jump = new GameState();
    jump.EarnRedGems((ulong)Math.Ceiling(PrestigeProgression.Threshold(3)));
    Check(jump.UpdatePrestigeProgress() == 4 && jump.PendingPrestigePoints == 4, "A big jump in income earns every point it passes");

    Check(PrestigeProgression.BankEcho(0, 0.5) == 0.25 && PrestigeProgression.BankEcho(0.9, 0.9) == 1
      && PrestigeProgression.BankEcho(double.NaN, double.PositiveInfinity) == 0,
      "The echo banks the square of the best progress and stays a share of one bar");
    var restored = new GameState();
    restored.RestorePrestige(1, 3, 2, -1);
    Check(restored.PrestigePointsEarned == 3 && restored.PrestigeEcho == 1 && restored.BestPrestigeProgress == 0,
      "Restoring must keep the run's points on the ladder and the echo within one bar");
  }

  private static void CheckExpandSpace(Upgrades upgrades)
  {
    string[] tierOne = PrestigeTalentLayout.Tiers[0].Talents;
    string[] tierTwo = PrestigeTalentLayout.Tiers[1].Talents;
    string[] tierThree = PrestigeTalentLayout.Tiers[2].Talents;
    string[] tierFour = PrestigeTalentLayout.Tiers[3].Talents;
    (string[] Talents, int Level)[] steps =
    [
      ([], 1),
      (tierOne[..3], 2),
      ([.. tierOne, .. tierTwo[..1]], 3),
      ([.. tierOne, .. tierTwo, .. tierThree[..1]], 4),
      ([.. tierOne, .. tierTwo, .. tierThree, .. tierFour[..1]], 5),
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

  // Weapon Core Shard upgrades are only good once their weapon has its other upgrades, which a
  // first run can't afford; they wait for Expand Space 1 (the first extraction).
  private static void CheckFirstRunShards(Upgrades upgrades)
  {
    string[] laterShards = ["LZQ1", "ALC1", "TC1", "THP1", "RSW1", "IW1", "BDS1", "RCB1"];
    var shards = upgrades.UpgradeButtons.Values.Where(b => b.Data.UpgradeDefinition.Currency == CoreShards.Currency).ToList();
    var firstRun = Extracted(0, new GameSave());
    Check(shards.All(b => firstRun.IsExpandSpaceLocked(b) == laterShards.Contains(b.Data.ShortName))
      && shards.Count > laterShards.Length,
      "The first run offers the cannon, click and fleet shard upgrades, and locks the other weapons'");
    var secondRun = Extracted(1, new GameSave());
    Check(shards.All(b => !secondRun.IsExpandSpaceLocked(b)), "Every shard upgrade opens after the first extraction");
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
