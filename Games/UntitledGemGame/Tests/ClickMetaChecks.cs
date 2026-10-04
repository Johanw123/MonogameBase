using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class ClickMetaChecks
{
  public static void Run(Upgrades tree)
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    static void Near(double actual, double expected, string message)
      => Check(Math.Abs(actual - expected) < 0.001, $"{message}: {actual} != {expected}");
    var levels = new Dictionary<string, int>
    {
      ["RH1"] = 1, ["MCV1"] = 2, ["MCR1"] = 1, ["MHF1"] = 1,
      ["MGS1"] = 1, ["MGD1"] = 1, ["MGF1"] = 1, ["MCSN1"] = 1
    };
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = levels });
    var meta = manager.UGM;
    Near(meta.ClickValueMultiplier, 3, "Restore all click value meta ranks");
    Near(meta.ClickRadiusMultiplier, 2, "Restore all click radius meta ranks");
    Check(meta.QuantumTouch && meta.ClickComboSupernova && meta.CursorGravityEventHorizon
      && meta.CursorGravityMobile && meta.CursorGravityCollapse, "Restore all mouse mechanic talents");
    foreach (var id in levels.Keys)
    {
      var button = tree.UpgradeButtonsMeta[id];
      Check(button.IsMaxLevel && button.State == (id is "RH1" or "MCV1"
        ? UpgradeButton.UnlockState.Invisible : UpgradeButton.UnlockState.MaxedOut),
        "Meta purchases restore while retired first-tier placeholders stay hidden");
      Check(File.Exists(Path.Combine("Content", button.Data.UpgradeDefinition.Icon)), "Click meta icons exist");
      if (id is "MCV1" or "MCR1")
        Check(UpgradeValueFormatter.Format(button.Data.UpgradeDefinition, 1.25, true) == "+25%", "Meta tooltips show bonuses above base");
    }
    var ug = manager.UG;
    var signals = manager.Signals;
    foreach (var kind in new[] { SignalKind.ClickValue, SignalKind.HoldClickFrequency,
      SignalKind.CursorGravityStrength, SignalKind.CursorGravityDuration })
      signals.Counts[(int)kind * SignalProgression.RarityCount] = 4;
    signals.Counts[(int)SignalKind.ClickRadius * SignalProgression.RarityCount] = 4;
    signals.Counts[(int)SignalKind.CursorGravityCooldown * SignalProgression.RarityCount] = 1;
    signals.Counts[(int)SignalKind.AbilityCooldown * SignalProgression.RarityCount] = 1;
    ug.ClickValueMultiplier = 2;
    ug.ClickRadius = 2;
    ug.HoldClickFrequencyMultiplier = 2;
    Near(SignalStats.ClickRadius, 2 * 2 * 1.1, "Click radius stacks run, meta and signals");
    Near(SignalStats.ClickValue, 2 * 3 * 1.2, "Click value stacks run, meta and signals");
    Near(ClickUtility.HoldInterval(ug, signals: signals, meta: meta), 0.8 / (2 * 1.2), "Held repeat interval uses all bonuses");
    var grid = new GemSpatialIndex(4, 30);
    int gem = grid.AddGem(1, 0, 0, 1);
    var clicks = new ClickUtility();
    Check(clicks.Activate(grid, new[] { gem }, Vector2.Zero, ug, (_, multiplier) =>
    {
      Near(multiplier, 7.2, "Actual manual collection receives the permanent bonus");
      return true;
    }, 1, signals, meta), "Meta-boosted manual click succeeds");
    var well = new CursorGravityWell();
    Check(!well.HandleInput(true, true, true, Vector2.Zero, 19, ug, signals, meta), "Meta ranks do not bypass the gravity unlock");
    ug.CursorGravityEnabled = true;
    ug.CursorGravityStrengthMultiplier = 1.75f;
    ug.CursorGravityDuration = 4.5f;
    ug.CursorGravityFrequencyMultiplier = 2;
    meta.AllAbilityCooldown = 2;
    float clickRadius = 19 * SignalStats.ClickRadius;
    Check(well.HandleInput(true, true, true, Vector2.Zero, clickRadius, ug, signals, meta), "Meta-boosted aimed gravity cast succeeds");
    Near(well.Radius, clickRadius * 3, "Gravity radius inherits permanent click radius and matches the preview");
    Near(well.CooldownRemaining, 20 / (2 * 2) * 0.95 * 0.95, "Gravity recharge stacks run, specific meta, global meta and both signals");
    grid.MoveGem(gem, 50, 0);
    well.Update(1, grid, (index, position) => grid.MoveGem(index, position.X, position.Y));
    Near(grid.Gems[gem].X, 50 * Math.Exp(-0.45 * 1.75 * 1.2), "Permanent gravity strength affects actual gem movement");
    well.Update(4.3f, grid, (_, _) => { });
    Check(well.IsActive, "Run upgrades and signals still control well lifetime");
    well.Update(1.2f, grid, (_, _) => { });
    Check(!well.IsActive, "Boosted well expires after its effective duration");

    var saved = new GameSave();
    manager.CaptureProgress(saved);
    var directory = Path.Combine(Path.GetTempPath(), "click-meta-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "save.json"));
      Check(store.Save(saved), "Click meta progress saves");
      saved = store.Load() ?? throw new Exception("Click meta progress loads");
      foreach (var (id, level) in levels) Check(saved.Meta[id] == level, "All permanent click ranks survive saving");
      // Prestige resets run purchases while preserving the permanent tracks.
      saved.Upgrades.Clear();
      saved.Abilities.Clear();
      manager = new UpgradeManager();
      manager.RestoreProgress(saved);
      Near(manager.UGM.ClickValueMultiplier, 3, "Click meta survives run reset and reload without stacking twice");
      Check(manager.UGM.QuantumTouch && manager.UGM.ClickComboSupernova
        && manager.UGM.CursorGravityEventHorizon && manager.UGM.CursorGravityMobile
        && manager.UGM.CursorGravityCollapse, "Mechanic talents survive prestige and current-format saves");
      Check(!manager.UG.HoldClickEnabled && !manager.UG.CursorGravityEnabled, "New runs still require their power unlocks");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
    Near(manager.UGM.ClickValueMultiplier, 1, "Fresh click value is neutral");
    Near(manager.UGM.ClickRadiusMultiplier, 1, "Fresh click radius is neutral");
    Check(!manager.UGM.QuantumTouch && !manager.UGM.ClickComboSupernova
      && !manager.UGM.CursorGravityEventHorizon && !manager.UGM.CursorGravityMobile
      && !manager.UGM.CursorGravityCollapse, "Fresh saves have no mouse talents");
    MouseMetaTalentChecks.Run();
    Console.WriteLine("Click meta checks passed: ranks, effective powers, stacking, unlocks, tree state, persistence and reset.");
  }
}
