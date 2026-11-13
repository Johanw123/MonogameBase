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
      ["MCV1"] = 2, ["MCR1"] = 1,
      ["MCSN1"] = 1
    };
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = levels });
    var meta = manager.UGM;
    Near(meta.ClickValueMultiplier, 3, "Restore all click value meta ranks");
    Near(meta.ClickRadiusMultiplier, 2, "Restore all click radius meta ranks");
    Check(meta.ClickComboSupernova, "Restore all mouse mechanic talents");
    foreach (var id in levels.Keys)
    {
      var button = tree.UpgradeButtonsMeta[id];
      Check(button.IsMaxLevel && button.State == (id is "MCV1" or "MCR1"
        ? UpgradeButton.UnlockState.Invisible : UpgradeButton.UnlockState.MaxedOut),
        "Active talents restore while retired prestige upgrades stay hidden");
      Check(File.Exists(Path.Combine("Content", button.Data.UpgradeDefinition.Icon)), "Click meta icons exist");
      if (id is "MCV1" or "MCR1")
        Check(UpgradeValueFormatter.Format(button.Data.UpgradeDefinition, 1.25, true) == "+25%", "Meta tooltips show bonuses above base");
    }
    var ug = manager.UG;
    var signals = manager.Signals;
    foreach (var kind in new[] { SignalKind.ClickValue, SignalKind.HoldClickFrequency,
      SignalKind.TractorBeltSpeed, SignalKind.TractorBeltDuration })
      signals.Counts[(int)kind * SignalProgression.RarityCount] = 4;
    signals.Counts[(int)SignalKind.ClickRadius * SignalProgression.RarityCount] = 4;
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
    float clickRadius = 19 * SignalStats.ClickRadius;
    Check(!new TractorBelt().HandleInput(true, true, true, Vector2.Zero, TractorBeltStats.From(ug, clickRadius, signals)),
      "Meta ranks do not bypass the tractor belt unlock");
    ug.TractorBeltEnabled = true;
    ug.TractorBeltSpeedMultiplier = 1.5f;
    ug.TractorBeltDuration = 20;
    var belt = TractorBeltStats.From(ug, clickRadius, signals);
    Near(belt.Radius, clickRadius * 3, "The belt intake inherits permanent click radius");
    Near(belt.Speed, TractorBelt.BaseSpeed * 1.5 * 1.2, "Belt speed stacks run ranks and signals");
    Near(belt.Duration, 20 * 1.2, "Belt run time stacks run ranks and signals");

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
      Check(manager.UGM.ClickComboSupernova, "Mechanic talents survive prestige and current-format saves");
      Check(!manager.UG.HoldClickEnabled && !manager.UG.TractorBeltEnabled, "New runs still require their power unlocks");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
    Near(manager.UGM.ClickValueMultiplier, 1, "Fresh click value is neutral");
    Near(manager.UGM.ClickRadiusMultiplier, 1, "Fresh click radius is neutral");
    Check(!manager.UGM.ClickComboSupernova, "Fresh saves have no mouse talents");
    MouseMetaTalentChecks.Run();
    Console.WriteLine("Click meta checks passed: ranks, effective powers, stacking, unlocks, tree state, persistence and reset.");
  }
}
