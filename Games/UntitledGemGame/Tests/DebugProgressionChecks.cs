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
      save.Modules.Validate();
      save.Signals.Validate();
      foreach (var (buttons, levels) in new[] {
        (upgrades.UpgradeButtons, save.Upgrades),
        (upgrades.UpgradeButtonsAbilities, save.Abilities),
        (upgrades.UpgradeButtonsMeta, save.Meta) })
        foreach (var (id, button) in buttons)
        {
          if (id is "P1" or "ResetAbilities1")
          {
            if (levels.ContainsKey(id)) throw new Exception("Preset bought a repeatable action");
            continue;
          }
          if (stage == 4 && levels.GetValueOrDefault(id) != button.Data.NumLevels)
            throw new Exception($"Endgame did not max {id}");
          if (levels.ContainsKey(id) && !string.IsNullOrEmpty(button.Data.BlockedBy)
            && !levels.ContainsKey(button.Data.BlockedBy))
            throw new Exception($"Preset skipped prerequisite for {id}");
        }
      string path = Path.Combine(Path.GetTempPath(), $"preset-{Guid.NewGuid():N}.json");
      try
      {
        var store = new GameSaveStore(path);
        if (!store.Save(save) || store.Load() is not {} loaded
          || loaded.Modules.Owned.Count != save.Modules.Owned.Count
          || loaded.AbilityPointsPurchased != save.AbilityPointsPurchased)
          throw new Exception("Preset save round trip failed");
      }
      finally { File.Delete(path); }
      if (stage == 4 && !save.Modules.CollectionComplete) throw new Exception("Incomplete endgame modules");
    }
    var beginning = DebugProgressionPresets.Create(0, upgrades);
    if (beginning.Meta.Count != 0 || beginning.Abilities.Count != 0 || beginning.Modules.Owned.Count != 0
      || beginning.Signals.Counts.Any(c => c != 0)) throw new Exception("Beginning retains progression");
    Console.WriteLine("Passed progression preset checks.");
  }
}
