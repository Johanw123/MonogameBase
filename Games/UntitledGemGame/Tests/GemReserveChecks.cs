using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class GemReserveChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime gameTime) { }
  }

  public static void Run()
  {
    CapacityAndCondensation();
    RoutingAndReset();
    SavingAndProgression();
    Console.WriteLine("Gem reserve checks passed: capacity, condensation, traits, spawn routing, save/load and progression.");
  }

  private static GemSpawnData Spawn(uint value = 10, bool lucky = false, bool seed = false, bool gilded = false)
    => new() { Position = new Vector2(123, 456), Type = GemTypes.Red,
      BaseValue = value, IsLucky = lucky, IsBloomSeed = seed, IsGilded = gilded,
      LaunchVelocity = new Vector2(12, -34) };

  private static void CapacityAndCondensation()
  {
    var reserve = new GemReserve();
    Check(!reserve.TryStore(Spawn(), 0, true), "Locked reserves must not accept gems");
    Check(reserve.TryStore(Spawn(), 1, false) && !reserve.TryStore(Spawn(), 1, false),
      "Capacity must bound stored slots");
    for (uint i = 1; i <= 3; i++)
      Check(reserve.TryStore(Spawn(i), 1, true), "Condensation must pack an existing slot even when full");
    Check(reserve.Count == 1 && reserve.StoredGemCount == 4 && !reserve.TryStore(Spawn(), 1, true),
      "Condensation must pack at most four gems per slot");
    Check(reserve.TryRelease(out var released) && released.BaseValue == 16
      && released.Position == new Vector2(123, 456) && released.LaunchVelocity == new Vector2(12, -34)
      && reserve.Count == 0 && reserve.StoredGemCount == 0 && reserve.StoredValue == 0,
      "Release must conserve value, location and motion and remove all represented gems");
    reserve.TryStore(Spawn(10, lucky: true, gilded: true), 8, true);
    reserve.TryStore(Spawn(20, lucky: true, gilded: true), 8, true);
    reserve.TryStore(Spawn(), 8, true);
    reserve.TryStore(Spawn(seed: true), 8, true);
    reserve.TryStore(Spawn(seed: true), 8, true);
    Check(reserve.Count == 4 && reserve.StoredGemCount == 5,
      "Condensation must preserve luck and gilding and never combine Bloom seeds");
    Check(reserve.TryRelease(out released) && released.BaseValue == 30 && released.IsLucky && released.IsGilded,
      "Compatible special traits must survive condensation");
    reserve.Clear();
    reserve.TryStore(Spawn(uint.MaxValue), 1, true);
    Check(!reserve.TryStore(Spawn(1), 1, true) && reserve.Capture()[0].BaseValue == uint.MaxValue,
      "Condensation must not overflow gem value");
    reserve.Clear();
    reserve.TryStore(Spawn(), 2, true);
    reserve.TryRelease(out _);
    reserve.TryStore(Spawn(7), 2, true);
    Check(reserve.TryRelease(out released) && released.BaseValue == 7,
      "A released slot must not remain a condensation target");
  }

  private static void RoutingAndReset()
  {
    var previousManager = UpgradeManager.Instance;
    var previousFactory = EntityFactory.Instance;
    var previousFleet = HarvesterCollectionSystem.Instance;
    try
    {
      var manager = new UpgradeManager();
      manager.UG.MaxGemCount = 2;
      var fleet = new FleetProbe();
      var pending = new Queue<GemSpawnData>();
      var factory = (EntityFactory)RuntimeHelpers.GetUninitializedObject(typeof(EntityFactory));
      typeof(EntityFactory).GetField("_gemSpawnQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(factory, pending);
      EntityFactory.Instance = factory;
      Check(factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 10)
        && factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 20),
        "Special rewards should spawn normally when the field has room");
      Check(!factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 30) && pending.Count == 2,
        "Staged spawns must count against the field cap");
      manager.UG.GemReserveUnlocked = true;
      manager.UG.GemReserveCapacity = 2;
      Check(factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 30)
        && factory.GemReserve.Count == 1, "Overflow must enter the unlocked reserve");
      pending.Clear();
      factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 40);
      Check(pending.Count == 0 && factory.GemReserve.Count == 2,
        "New special gems must wait behind existing stored rewards");
      Check(!factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 50), "Full reserves must remain bounded");
      manager.UGA.GemSpawnerCrystalCondensation = true;
      Check(factory.QueueSpecialGemSpawn(Vector2.Zero, GemTypes.Red, 50)
        && factory.GemReserve.Count == 2 && factory.GemReserve.StoredGemCount == 3,
        "Buying Condensation must immediately enable packing existing reserve slots");
      fleet.flatSpatialHash.AddGem(1, 0, 0, 1);
      fleet.flatSpatialHash.AddGem(2, 0, 0, 1);
      factory.Update();
      Check(factory.GemReserve.Count == 2, "A full field must not dequeue stored gems");
      factory.ClearPendingGemSpawns();
      Check(factory.GemReserve.Count == 0 && factory.PendingGemSpawnCount == 0,
        "Prestige and new-run reset must clear both pending and reserved gems");
    }
    finally
    {
      UpgradeManager.Instance = previousManager;
      EntityFactory.Instance = previousFactory;
      HarvesterCollectionSystem.Instance = previousFleet;
    }
  }

  private static void SavingAndProgression()
  {
    var previousManager = UpgradeManager.Instance;
    var previousTree = UpgradeManager.CurrentUpgrades;
    string directory = Path.Combine(Path.GetTempPath(), "gem-reserve-check-" + Guid.NewGuid());
    try
    {
      var reserve = new GemReserve();
      reserve.TryStore(Spawn(10, lucky: true), 10, true);
      reserve.TryStore(Spawn(20, lucky: true), 10, true);
      reserve.TryStore(Spawn(40, seed: true, gilded: true), 10, true);
      var tree = new Upgrades();
      UpgradeManager.CurrentUpgrades = tree;
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades.json"),
        File.ReadAllText("Content/Data/upgrades_buttons.json"), tree.UpgradeButtons, tree.UpgradeDefinitions);
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades_abilities.json"),
        File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"), tree.UpgradeButtonsAbilities, tree.UpgradeDefinitionsAbilities);
      var manager = new UpgradeManager();
      var save = new GameSave { GemReserve = reserve.Capture(),
        Upgrades = new() { ["HB"] = 1, ["GSC1"] = 1, ["GSR1"] = 1, ["MGC1"] = 1,
          ["GRU1"] = 1, ["GRC1"] = 5, ["GRC2"] = 5 },
        Abilities = new() { ["GSCondense1"] = 1 } };
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(save), "Reserve save must succeed");
      var loaded = store.Load()!;
      Check(loaded != null, "Reserve save must load");
      reserve.Restore(loaded.GemReserve);
      Check(reserve.Count == 2 && reserve.StoredGemCount == 3 && reserve.StoredValue == 70,
        "Save/load must preserve condensation counts and stored value");
      Check(reserve.TryRelease(out var gem) && gem.BaseValue == 30 && gem.IsLucky
        && gem.Position == new Vector2(123, 456) && gem.LaunchVelocity == new Vector2(12, -34),
        "Save/load must preserve actual rolled value, traits and coordinates");
      Check(reserve.TryRelease(out gem) && gem.IsBloomSeed && gem.IsGilded && gem.BaseValue == 40,
        "Save/load must preserve Bloom seeds separately");
      manager.RestoreProgress(loaded);
      Check(manager.UG.GemReserveUnlocked && manager.UG.GemReserveCapacity == 6500
        && manager.UGA.GemSpawnerCrystalCondensation,
        "Upgrade restoration must rebuild reserve capacity and Condensation");
      manager.UGM.GemReserveCapacityMultiplier = 2;
      Check(SignalStats.ReserveCapacity == 13000, "Permanent capacity must multiply all run reserve upgrades");
      Check(tree.UpgradeButtons["GRU1"].Data.BlockedBy == "MGC1"
        && tree.UpgradeButtonsAbilities["GSCondense1"].Data.BlockedBy == "GSBloom1",
        "Reserve must unlock early and Condensation must follow the Bloom midpoint");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
      UpgradeManager.Instance = previousManager;
      UpgradeManager.CurrentUpgrades = previousTree;
    }
  }
}
