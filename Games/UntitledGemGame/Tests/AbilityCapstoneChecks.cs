using System.Reflection;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using Scene = ExpandedModuleChecks.Scene;

internal static class AbilityCapstoneChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    CheckAvalanche();
    CheckHorizon();
    CheckPersistence();
    Console.WriteLine("Ability finishers passed: bounded Avalanche, expanded Event Horizon, pull timing and current-format save/load.");
  }

  private static void CheckAvalanche()
  {
    using var scene = new Scene(ShipModule.None);
    var ability = new ChainLightningAbility();
    var start = scene.AddGem(new Vector2(1000, 1000));
    for (int i = 0; i < 60; i++) scene.AddGem(new Vector2(1000 + i % 10 * 2, 1000 + i / 10 * 2));
    scene.Prepare();
    scene.Manager.UGA.ChainMagnetizerChainReaction = true;
    var add = typeof(ChainLightningAbility).GetMethod("AddChain", BindingFlags.NonPublic | BindingFlags.Instance)!;
    try
    {
      add.Invoke(ability, new object[] { start.GridIndex, Vector2.Zero, true, Color.Cyan });
      int baseline = ChainLightningAbility.TargetLines.Count;
      Check(baseline <= 7, "Base Chain Reaction must remain bounded to seven gems per root.");
      ability.Cancel();
      scene.Manager.UGA.ChainAvalanche = true;
      scene.Manager.UGA.ChainPullSpeed = 2;
      add.Invoke(ability, new object[] { start.GridIndex, Vector2.Zero, true, Color.Cyan });
      Check(ChainLightningAbility.TargetLines.Count > baseline && ChainLightningAbility.TargetLines.Count <= 40,
        "Avalanche must collect more than the midpoint mechanic and stay bounded to forty gems per root.");
      ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.8)));
      Check(ChainLightningAbility.TargetLines.Count == 0, "Cascade Velocity must accelerate all reaction pulls.");
      scene.Manager.UGA.Reset("CMAvalanche");
      scene.Manager.UGA.Reset("CMVelocity");
      scene.Manager.UGA.ChainReactionReach = 120;
      var distant = scene.AddGem(new Vector2(1100, 1000));
      scene.Prepare();
      add.Invoke(ability, new object[] { distant.GridIndex, Vector2.Zero, true, Color.Cyan });
      Check(ChainLightningAbility.TargetLines.Count > 1, "Reaction Reach must allow jumps beyond the base eighty units.");
    }
    finally { ability.Cancel(); }
  }

  private static void CheckHorizon()
  {
    foreach (int mode in new[] { 0, 1, 2 })
    {
      using var scene = new Scene(ShipModule.None);
      var ability = new ChainLightningAbility();
      scene.Manager.UGA.ChainMagnetizerConstellation = true;
      scene.Manager.UGA.ChainMagnetizerCount = 4;
      scene.Manager.UGA.ConstellationCapacity = 2;
      scene.Manager.UGA.ChainEventHorizon = mode == 1;
      scene.Manager.UGA.ConstellationReach = mode == 2 ? 1.3f : 1f;
      scene.Manager.UGA.ChainPullSpeed = 2;
      foreach (var p in new[] { new Vector2(1000, 1000), new Vector2(1400, 1000), new Vector2(1400, 1400), new Vector2(1000, 1400) })
        scene.AddGem(p);
      var extra = Enumerable.Range(0, 5).Select(i => scene.AddGem(new Vector2(950, 1100 + i * 10))).ToArray();
      scene.Prepare();
      try
      {
        ability.Activate();
        int caught = extra.Count(g => scene.Fleet.flatSpatialHash.Gems[g.GridIndex].ClaimState == 1);
        Check(caught == (mode == 0 ? 0 : mode == 1 ? 4 : 2),
          "Net reach must expand capture eligibility, while only Event Horizon doubles capacity.");
        var net = ChainLightningAbility.Constellations.Single();
        Check(Math.Abs(net.Hull.Min(p => p.X) - (mode == 0 ? 1000 : mode == 1 ? 900 : 940)) < 0.001f
          && net.PullDuration == ConstellationNet.CollapseDuration / 2,
          "Expanded geometry and net animation must reflect reach and pull-speed upgrades.");
      }
      finally { ability.Cancel(); }
    }
  }

  private static void CheckPersistence()
  {
    var previous = UpgradeManager.CurrentUpgrades;
    string directory = Path.Combine(Path.GetTempPath(), "ability-finishers-" + Guid.NewGuid());
    try
    {
      var tree = new Upgrades();
      UpgradeManager.CurrentUpgrades = tree;
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades_abilities.json"), File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"),
        tree.UpgradeButtonsAbilities, tree.UpgradeDefinitionsAbilities);
      foreach (var node in tree.UpgradeButtonsAbilities.Values) node.CurrentLevel = node.Data.NumLevels;
      var manager = new UpgradeManager();
      var save = new GameSave();
      manager.CaptureProgress(save);
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(save), "Full ability trees must save in the current format.");
      manager.RestoreProgress(store.Load() ?? throw new Exception("Ability save missing"));
      Check(manager.UGA.ChainAvalanche && manager.UGA.ChainEventHorizon
        && manager.UGA.GemSpawnerCosmicGenesis && manager.UGA.GemSpawnerWorldseed && manager.UGA.GemSpawnerGoldenAge,
        "Current-format save/load must restore every new finisher.");
      Check(manager.UGA.ChainReactionReach == 155 && Math.Abs(manager.UGA.ChainPullSpeed - 1.5f) < 0.001f
        && Math.Abs(manager.UGA.ConstellationReach - 1.3f) < 0.001f && manager.UGA.GemSpawnerBloomSeeds == 6
        && manager.UGA.GemSpawnerMidasCapacity == 256 && manager.UGA.GemSpawnerMidasReach == 1000,
        "Save/load must restore all support talent ranks.");
      manager = new UpgradeManager();
      manager.RestoreProgress(new GameSave());
      Check(!manager.UGA.ChainAvalanche && !manager.UGA.ChainEventHorizon && !manager.UGA.GemSpawnerCosmicGenesis
        && !manager.UGA.GemSpawnerWorldseed && !manager.UGA.GemSpawnerGoldenAge
        && manager.UGA.ChainReactionReach == 80 && manager.UGA.ChainPullSpeed == 1 && manager.UGA.ConstellationReach == 1,
        "Restoring an empty tree must remove finishers and restore base support values.");
    }
    finally
    {
      UpgradeManager.CurrentUpgrades = previous;
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }
}
