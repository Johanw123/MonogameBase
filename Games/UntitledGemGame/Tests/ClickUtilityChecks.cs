using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;
using UntitledGemGame.Screens;
using UntitledGemGame;

internal static class ClickUtilityChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    // The world target uses display coordinates; shapes draw into a virtual target.
    // Verify an off-center pointer with downscaling and a letterboxed viewport.
    var virtualView = Matrix.CreateScale(3.5f) * Matrix.CreateTranslation(960, 540, 0);
    var viewportScale = Matrix.CreateScale(0.5f, 0.5f, 1);
    var cameraView = virtualView * viewportScale;
    var pointer = new Vector2(740, 450);
    var viewportOrigin = new Vector2(0, 50);
    var worldPointer = Vector2.Transform(pointer - viewportOrigin, Matrix.Invert(cameraView));
    var virtualPointer = Vector2.Transform(worldPointer, ClickUtility.RenderView(cameraView, viewportScale));
    var displayedPointer = Vector2.Transform(virtualPointer, viewportScale) + viewportOrigin;
    Check(Vector2.Distance(pointer, displayedPointer) < 0.001f,
      "Pointer ring remains centered after virtual rendering, window downscaling and letterboxing");
    Check(ClickUtility.TargetRadius(26, 38, 1) == 19 && ClickUtility.TargetRadius(26, 38, 2) == 38,
      "Pointer radius grows with click upgrades and uses the same gem size as targeting");
    Check(ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(19, 0), 19)
      && !ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(18, 18), 19),
      "Pointer ring matches circular targeting, including boundaries and square corners");
    var visibleHalfSize = new Vector2(9, 15); // Opaque 18x30 gem, excluding its transparent border.
    Check(ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(28, 0), 19, visibleHalfSize)
      && ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(0, 34), 19, visibleHalfSize),
      "Touching a gem side or tip targets it even when its center is outside the ring");
    Check(!ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(28.1f, 0), 19, visibleHalfSize)
      && !ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(0, 34.1f), 19, visibleHalfSize)
      && !ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(25, 31), 19, visibleHalfSize),
      "Non-overlapping sides, tips and diagonal corners remain untargeted");
    Check(ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(37, 0), 19, visibleHalfSize * 2)
      && ClickUtility.ContainsTarget(Vector2.Zero, new Vector2(34, 0), 19, visibleHalfSize, MathHelper.PiOver2),
      "Hover overlap follows gem scale and rotation");
    var overlapGrid = new GemSpatialIndex(4, 30);
    int edgeGem = overlapGrid.AddGem(1, 28, 0, 1, 15);
    int farGem = overlapGrid.AddGem(2, 80, 0, 1, 15);
    var candidates = new List<int>();
    foreach (int candidate in overlapGrid.QueryClickCandidates(0, 0, 19)) candidates.Add(candidate);
    Check(candidates.Contains(edgeGem) && !candidates.Contains(farGem),
      "Spatial candidates include overlapping gems whose centers are outside the cursor circle");
    var upgrades = new UpgradesGeneratorUpgrades
    {
      ClickValueMultiplier = 2, ClickChainCount = 3, ClickChainRange = 60,
      ClickComboBonus = 0.1f, ClickCriticalChance = 0.25f,
      PassiveIncome = 10, ClickPassiveSeconds = 0.2f,
      PassiveIncomeFrequencyMultiplier = 2
    };
    var utility = new ClickUtility();
    var grid = new GemSpatialIndex(30, 30);
    int first = grid.AddGem(1, 0, 0, 10);
    int second = grid.AddGem(2, 40, 0, 10);
    int third = grid.AddGem(3, 80, 0, 10);
    int fourth = grid.AddGem(4, 120, 0, 10);
    int far = grid.AddGem(5, 250, 0, 10);
    int reserved = grid.AddGem(6, 5, 0, 10);
    grid.TryClaim(reserved);
    var collected = new List<int>();
    ulong bonus = 0;
    bool Collect(int index, double multiplier)
    {
      ref var gem = ref grid.Gems[index];
      if (!gem.IsActive || gem.ClaimState != 0) return false;
      bonus += utility.BonusValue(gem.BaseValue, multiplier);
      collected.Add(index);
      gem.ClaimState = 2;
      grid.RemoveFromQueries(index);
      return true;
    }
    Check(utility.Activate(grid, new[] { first }, Vector2.Zero, upgrades, Collect, 0.9), "Direct click activates");
    Check(collected.SequenceEqual(new[] { first, second, third, fourth }), "Links hop from gem to gem beyond the original click radius");
    Check(grid.Gems[far].ClaimState == 0 && grid.Gems[reserved].ClaimState == 1, "Chains respect range and fleet reservations");
    Check(bonus == 40 && utility.Combo == 1, "All linked gems share the bonus, combo increments once");
    Check(Math.Abs(utility.TakePassiveCredit() - 0.2f) < 0.0001 && utility.TakePassiveCredit() == 0, "One synthesis charge per gesture, consumed once");
    Check(ClickUtility.PassiveInterval(upgrades) == 0.5f, "Overclock halves payout interval");
    utility.Update(1);
    Check(utility.Activate(grid, new[] { far }, new Vector2(250, 0), upgrades, Collect, 0.1), "Second click activates");
    Check(utility.Combo == 2 && utility.LastCritical && Math.Abs(utility.LastMultiplier - 6.6) < 0.001, "Critical click triples combo value");
    Check(!utility.Activate(grid, Array.Empty<int>(), Vector2.Zero, upgrades, Collect, 0), "Empty clicks do not activate");
    Check(utility.Combo == 2, "Empty clicks cannot build combo");
    utility.Update(upgrades.ClickComboWindow + 0.1f);
    Check(utility.Combo == 0, "Combo expires during gameplay");
    utility.Reset();
    Check(utility.TakePassiveCredit() == 0 && !utility.LastCritical, "Reset clears synthesis credit and transient state");
    ulong fractional = 0;
    for (int i = 0; i < 4; ++i) fractional += utility.BonusValue(1, 1.25);
    Check(fractional == 1, "Small gems preserve fractional bonus value without rounding every gem up");
    Check(utility.BonusValue(uint.MaxValue, double.MaxValue) == ulong.MaxValue, "Bonuses saturate without overflow");

    utility.Reset();
    upgrades.ClickShockwaveCount = 2;
    upgrades.ClickChainCount = 0;
    upgrades.ClickComboBonus = 0;
    upgrades.PassiveIncome = 0;
    grid = new GemSpatialIndex(30, 30);
    first = grid.AddGem(10, 0, 0, 1);
    int near = grid.AddGem(11, 10, 0, 1);
    int middle = grid.AddGem(12, 20, 0, 1);
    int diagonal = grid.AddGem(13, 80, 80, 1);
    grid.AddGem(14, 85, 0, 1);
    collected.Clear();
    utility.Activate(grid, new[] { first }, Vector2.Zero, upgrades, Collect, 0.9);
    Check(collected.SequenceEqual(new[] { first, near, middle }), "Shockwave picks nearest gems up to its limit");
    Check(grid.Gems[diagonal].ClaimState == 0, "Shockwave uses circular distance");
    Check(utility.TakePassiveCredit() == 0, "Tap Dynamo requires passive production");

    utility.Reset();
    upgrades.ClickShockwaveCount = 0;
    for (int i = 0; i < 15; ++i)
    {
      int target = grid.AddGem(20 + i, 0, 0, 1);
      utility.Activate(grid, new[] { target }, Vector2.Zero, upgrades, Collect, 0.9);
    }
    Check(utility.Combo == ClickUtility.MaxCombo, "Combo caps at ten successful gestures");
    Check(utility.LastMultiplier == 2, "Combo without its upgrade adds no value");
    CheckHoldTiming();
    CheckDelivery();
    Console.WriteLine("Click utility checks passed: chains, shockwaves, combos, critical value, synthesis and reset.");
  }

  private static void CheckHoldTiming()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var utility = new ClickUtility();
    Check(Vector2.Distance(ClickUtility.PointerToTarget(new Vector2(900, 450),
      new Rectangle(128, 72, 1024, 576), new Vector2(3840, 2160)), new Vector2(2895, 1417.5f)) < 0.001f,
      "Pointer uses the window viewport captured before rendering changes the graphics viewport");
    var upgrades = new UpgradesGeneratorUpgrades();
    Check(utility.ShouldClick(true, true, true, 0, upgrades), "Unupgraded manual click is immediate");
    Check(!utility.ShouldClick(false, true, true, 2, upgrades), "Hold collection requires its unlock");
    upgrades.HoldClickEnabled = true;
    Check(utility.ShouldClick(true, true, true, 0, upgrades), "Hold starts with an immediate click");
    Check(!utility.ShouldClick(false, true, true, 0.4f, upgrades), "Hold waits for its initial interval");
    Check(Math.Abs(utility.HoldProgress - 0.5f) < 0.001f, "Hold ring fills halfway through the repeat period");
    Check(!utility.ShouldClick(false, true, true, 0.39f, upgrades), "Hold continues waiting until its period ends");
    Check(utility.ShouldClick(false, true, true, 0.02f, upgrades), "Hold repeats after 0.8 seconds");
    Check(utility.HoldProgress == 0, "Repeat restarts the actual cooldown");
    upgrades.HoldClickFrequencyMultiplier = 2;
    Check(ClickUtility.HoldInterval(upgrades) == 0.4f, "Frequency upgrades halve repeat interval");
    utility.ShouldClick(true, true, true, 0, upgrades);
    Check(!utility.ShouldClick(false, true, true, 0.39f, upgrades)
      && utility.ShouldClick(false, true, true, 0.02f, upgrades), "Upgraded repeats follow the shorter interval");
    Check(!utility.ShouldClick(false, true, false, 10, upgrades), "HUD, overlays and lost focus block repetition");
    Check(!utility.ShouldClick(false, true, true, 10, upgrades), "Reentering gameplay starts a fresh delay");
    Check(utility.ShouldClick(false, true, true, 10, upgrades)
      && !utility.ShouldClick(false, true, true, 0, upgrades), "Hitches trigger one gesture without replaying a backlog");
    Check(!utility.ShouldClick(false, false, true, 1, upgrades), "Releasing stops repetition");
    upgrades.HoldClickMomentum = 0.5f;
    Check(ClickUtility.HoldInterval(upgrades, 5) < ClickUtility.HoldInterval(upgrades, 0)
      && ClickUtility.HoldInterval(upgrades, 50) == ClickUtility.HoldInterval(upgrades, 5), "Momentum speeds up and caps after five seconds");
    upgrades.HoldClickFrequencyMultiplier = 100;
    Check(ClickUtility.HoldInterval(upgrades, 5) == 0.08f, "Repeat speed has a bounded minimum interval");
    utility.ShouldClick(true, true, true, 0, upgrades);
    utility.AcknowledgeHoldVisual();
    Check(!utility.ShouldClick(false, true, true, 0.016f, upgrades), "Fast repeat waits for its cooldown");
    Check(Math.Abs(utility.HoldVisualFill - utility.HoldProgress) < 0.001f,
      "Expanding fill follows the real cooldown even at maximum repeat speed");
    Check(utility.ShouldClick(false, true, true, 0.08f, upgrades)
      && utility.HoldVisualFill == 1 && utility.HoldClickActivated,
      "Click fires exactly when the displayed disk reaches the outer circle");
    utility.ShouldClick(false, true, true, 0.016f, upgrades);
    Check(utility.HoldVisualFill == 1, "Multiple updates before drawing cannot skip the full-circle endpoint");
    utility.AcknowledgeHoldVisual();
    utility.Update(0.02f);
    Check(Math.Abs(utility.HoldActivationGlow - 0.5f) < 0.001f,
      "Outer ring activation glow fades briefly after the click");
    utility.Update(0.02f);
    Check(utility.HoldActivationGlow == 0, "Outer ring glow ends before the next fastest repeat");
    Check(!utility.HoldClickActivated && Math.Abs(utility.HoldVisualFill - 0.2f) < 0.001f,
      "After displaying activation the fill resumes the next cooldown");
    utility.Reset();
    Check(!utility.ShouldClick(false, true, true, 10, upgrades), "Prestige reset clears armed repeat state");
  }

  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime gameTime) { }
  }

  private static void CheckDelivery()
  {
    var oldManager = UpgradeManager.Instance;
    ulong oldDelivered = UntitledGemGameGameScreen.DeliveredUncounted;
    ulong oldCollected = UntitledGemGameGameScreen.Collected;
    var oldFleet = HarvesterCollectionSystem.Instance;
    try
    {
      _ = new UpgradeManager();
      var fleet = new FleetProbe();
      using var world = new WorldBuilder().AddSystem(fleet).Build();
      var gemEntity = world.CreateEntity();
      gemEntity.Attach(new Transform2(new Vector2(100, 100)));
      var gem = new Gem();
      gem.Initialize(gemEntity, 18, 7);
      gemEntity.Attach(gem);
      gem.GridIndex = fleet.flatSpatialHash.AddGem(gemEntity.Id, 100, 100, 7);
      gem.WasClicked = true;
      gem.ManualClickBonus = 14;
      var homeEntity = world.CreateEntity();
      homeEntity.Attach(new Transform2(Vector2.Zero));
      var home = new Harvester { Id = homeEntity.Id, Type = Harvester.HarvesterType.HomeBase };
      homeEntity.Attach(home);
      world.Update(new GameTime());
      typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(fleet, 1);
      UntitledGemGameGameScreen.DeliveredUncounted = 0;
      fleet.CollectGem(gem, home);
      if (UntitledGemGameGameScreen.DeliveredUncounted != 14 || home.CarryingGemBaseValue != 7 || gem.ManualClickBonus != 0)
        throw new Exception("Click bonus arrives alongside the original delivery value");
      fleet.CollectGem(gem, home);
      if (UntitledGemGameGameScreen.DeliveredUncounted != 14 || home.CarryingGemBaseValue != 7)
        throw new Exception("Click delivery cannot award bonus or base value twice");
      gem.Reset();
      if (gem.ManualClickBonus != 0) throw new Exception("Pooled gems cannot retain click bonuses");
    }
    finally
    {
      UpgradeManager.Instance = oldManager;
      HarvesterCollectionSystem.Instance = oldFleet;
      UntitledGemGameGameScreen.DeliveredUncounted = oldDelivered;
      UntitledGemGameGameScreen.Collected = oldCollected;
    }
  }

  public static void CheckPersistence(Upgrades tree)
  {
    var manager = new UpgradeManager();
    var levels = new Dictionary<string, int>
    {
      ["HB"] = 1, ["CVM1"] = 5, ["CLC1"] = 5, ["CLR1"] = 5, ["CLC2"] = 5,
      ["CR1"] = 5, ["CSC1"] = 5, ["CCB1"] = 5, ["CCW1"] = 5,
      ["CCC1"] = 5, ["PI1"] = 5, ["PIF1"] = 5, ["CPS1"] = 5,
      ["HCE1"] = 1, ["HCF1"] = 5, ["HCF2"] = 5, ["HCM1"] = 5
    };
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    manager.RestoreProgress(new GameSave { Upgrades = levels });
    Check(manager.UG.ClickChainCount == 22 && manager.UG.ClickChainRange == 135, "Restore both chain tiers and reach");
    Check(manager.UG.ClickShockwaveCount == 10 && manager.UG.ClickValueMultiplier == 3, "Restore manual collection upgrades");
    Check(Math.Abs(manager.UG.PassiveIncomeFrequencyMultiplier - 2) < 0.001
      && Math.Abs(manager.UG.ClickPassiveSeconds - 0.5) < 0.001, "Restore synthesis upgrades");
    Check(manager.UG.HoldClickEnabled && Math.Abs(manager.UG.HoldClickFrequencyMultiplier - 4) < 0.001
      && Math.Abs(manager.UG.HoldClickMomentum - 0.5) < 0.001, "Restore hold unlock, both speed tiers and momentum");
    var saved = new GameSave();
    manager.CaptureProgress(saved);
    string directory = Path.Combine(Path.GetTempPath(), "click-utility-save-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "save.json"));
      if (!store.Save(saved)) throw new Exception("Click upgrade save must write successfully");
      saved = store.Load() ?? throw new Exception("Click upgrade save must load successfully");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(saved);
    Check(manager.UG.ClickChainCount == 22, "Loading twice must not stack click upgrades");
    foreach (var (id, level) in levels)
    {
      Check(saved.Upgrades[id] == level && tree.UpgradeButtons[id].IsMaxLevel, "All click ranks survive capture and restore");
      Check(tree.UpgradeButtons[id].State == UpgradeButton.UnlockState.MaxedOut, "Purchased click nodes restore visual state");
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
    Check(manager.UG.ClickChainCount == 0 && manager.UG.ClickValueMultiplier == 1
      && manager.UG.PassiveIncomeFrequencyMultiplier == 1 && manager.UG.ClickPassiveSeconds == 0
      && !manager.UG.HoldClickEnabled && manager.UG.HoldClickFrequencyMultiplier == 1 && manager.UG.HoldClickMomentum == 0, "Empty run clears click and synthesis effects");
    Check(tree.UpgradeButtons["CVM1"].State == UpgradeButton.UnlockState.Invisible, "Click branch requires homebase each run");
    foreach (var id in levels.Keys.Where(id => id != "HB"))
    {
      var button = tree.UpgradeButtons[id];
      Check(File.Exists(Path.Combine("Content", button.Data.UpgradeDefinition.Icon)), "Click upgrade icons exist");
    }
    Console.WriteLine("Click utility persistence checks passed: all ranks, visuals and reset.");
  }
}
