using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class ModuleBaysChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    var definitions = UpgradeManager.CurrentUpgrades;
    var home = UntitledGemGameGameScreen.HomeBasePos;
    try
    {
      CheckProgression();
      CheckRangeStack();
      CheckCascadeBuild();
      CheckOverflowBuild();
      CheckKineticBuild();
      Console.WriteLine("Module Bays passed: talent, locked bays, four-module loadouts, saves, trip snapshots and three combo builds.");
    }
    finally
    {
      UpgradeManager.CurrentUpgrades = definitions;
      UntitledGemGameGameScreen.HomeBasePos = home;
    }
  }

  private static void CheckProgression()
  {
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    var definitions = UpgradeManager.CurrentUpgrades = new Upgrades();
    definitions.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta.json")),
      File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta_buttons.json")),
      definitions.UpgradeButtonsMeta, definitions.UpgradeDefinitionsMeta);
    var button = definitions.UpgradeButtonsMeta["MS1"];
    Check(button.Data.NumLevels == 1 && button.Data.LevelInfo[0].Cost == 1
      && PrestigeTalentLayout.IsInTree("MS1")
      && PrestigeTalentLayout.TierIndex("MS1") > PrestigeTalentLayout.TierIndex("SYU1"),
      "Module Bays is a one-point talent in a tier after the Shipyard unlock");
    var manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    manager.RestoreProgress(new GameSave());
    Check(ModuleCatalog.UnlockedSlots == 2 && button.State != UpgradeButton.UnlockState.Invisible,
      "Start with two bays and show the talent that adds more");
    var slots = manager.Modules;
    Check(!slots.TryEquip(0, 2, ShipModule.FinalSweep) && !slots.TryEquip(0, 3, ShipModule.FinalSweep), "Extra bays start locked");
    slots.TryEquip(0, 0, ShipModule.FinalSweep);
    Check(!slots.TryMoveSlot(0, 2) && !slots.TryMoveSlot(0, 3), "Dragging cannot bypass locked bays");
    for (int rank = 0; rank <= 1; rank++)
    {
      manager = new UpgradeManager();
      ModuleChecks.GrantAll(manager.Modules);
      manager.RestoreProgress(new GameSave { Meta = new() { ["SYU1"] = 1, ["MS1"] = rank } });
      Check(ModuleCatalog.UnlockedSlots == 2 + rank * 2 && manager.UGM.ShipyardUnlocked, "The talent adds both extra bays");
      Check(button.IsMaxLevel == (rank == 1), "Module Bays is learned once");
      for (int slot = 0; slot < 4; slot++)
        Check(manager.Modules.TryEquip(0, slot, (ShipModule)(slot + 1)) == (slot < 2 + rank * 2), "Only unlocked bays accept equipment");
      Check(!manager.Modules.TryEquip(0, 4, ShipModule.ProspectorLens), "No fifth bay");
    }

    // All five ship types have an independent four-slot row.
    manager = new UpgradeManager();
    ModuleChecks.GrantAll(manager.Modules);
    manager.RestoreProgress(new GameSave { Meta = new() { ["SYU1"] = 1, ["MS1"] = 1 } });
    for (int type = 0; type < ModuleCatalog.Types.Length; type++)
      for (int slot = 0; slot < 4; slot++)
      {
        var module = (ShipModule)(type * 4 + slot + 1);
        Check(manager.Modules.TryEquip(type, slot, module) && manager.Modules.Has(ModuleCatalog.Types[type], module), "Four modules apply to the correct ship type");
      }
    Check(manager.Modules.TryMoveSlot(3, 7) && manager.Modules.Slots[3] == (ShipModule)8
      && manager.Modules.Slots[7] == (ShipModule)4, "Fourth bays swap across ship types");
    string path = Path.Combine(Path.GetTempPath(), "module-bays-" + Guid.NewGuid() + ".json");
    try
    {
      var save = new GameSave { Modules = manager.Modules };
      manager.CaptureProgress(save);
      var store = new GameSaveStore(path);
      Check(store.Save(save), "Save upgraded bays and all twenty assigned modules");
      var loaded = store.Load();
      Check(loaded != null && loaded.Meta["MS1"] == 1 && loaded.Modules.Slots.SequenceEqual(save.Modules.Slots), "Round-trip current slot format and meta ranks");
      manager = new UpgradeManager();
      ModuleChecks.GrantAll(manager.Modules);
      manager.RestoreProgress(loaded);
      Check(ModuleCatalog.UnlockedSlots == 4 && loaded.Modules.Has(ModuleCatalog.Types[4], (ShipModule)20), "Restore rank before using fourth-bay effects");
      var state = new GameState { Modules = loaded.Modules };
      state.CompletePrestige();
      // Modules last one run, but the permanent bays remain for the next run's finds.
      Check(state.Modules.Slots.All(module => module == ShipModule.None) && ModuleCatalog.UnlockedSlots == 4,
        "Prestige clears the run's equipment but keeps the bays");
    }
    finally { File.Delete(path); }

    using var scene = new Scene(ShipModule.FinalSweep, ShipModule.TractorLink);
    scene.Manager.UGM.ModuleSlots = 4;
    scene.Manager.Modules.TryEquip(0, 2, ShipModule.JackpotCore);
    scene.Manager.Modules.TryEquip(0, 3, ShipModule.AstralRelay);
    Check(!scene.Ship.HasModule(ShipModule.JackpotCore) && !scene.Ship.HasModule(ShipModule.AstralRelay), "Newly unlocked bays preserve the active trip");
    scene.Ship.BeginModuleTrip();
    Check(new[] { ShipModule.FinalSweep, ShipModule.TractorLink, ShipModule.JackpotCore, ShipModule.AstralRelay }
      .All(scene.Ship.HasModule), "Next trip activates all four modules");
  }

  private static void AddBays(Scene scene, ShipModule third, ShipModule fourth)
  {
    scene.Manager.UGM.ModuleSlots = 4;
    Check(scene.Manager.Modules.TryEquip(0, 2, third) && scene.Manager.Modules.TryEquip(0, 3, fourth), "Equip combo in expanded bays");
    scene.Ship.BeginModuleTrip();
  }

  private static void CheckCascadeBuild()
  {
    using var scene = new Scene(ShipModule.RiftSiphon, ShipModule.EchoChamber);
    AddBays(scene, ShipModule.DeepHold, ShipModule.QuantumForge);
    var first = scene.AddGem(new Vector2(500, 500));
    var extras = Enumerable.Range(0, 40).Select(_ => scene.AddGem(new Vector2(550, 500))).ToArray();
    scene.Prepare();
    scene.Fleet.CollectGem(first, scene.Ship);
    Check(extras.Count(g => g.PickedUp) == 10 && scene.Ship.CarryingGemCount == 11 && !scene.Ship.ReturningToHomebase,
      "Rift pulls its gems into the hold; Deep Hold leaves room for them");
    // Each rift gem: 10 base, +20 from the rift, +20 from Echo Chamber. The forge fires twice.
    Check(scene.Ship.CarryingGemBaseValue == 10 + 10 * 50 + 2 * 50,
      "Echo Chamber adds to the rift's own bonus, and the pulls feed Quantum Forge");
  }

  private static void CheckRangeStack()
  {
    using var scene = new Scene(ShipModule.None);
    float range = BaseStats.GetHarvesterCollectionRange(scene.Ship);
    scene.Manager.Modules.TryEquip(0, 0, ShipModule.WidebandArray);
    scene.Manager.Modules.TryEquip(0, 1, ShipModule.CargoScanner);
    AddBays(scene, ShipModule.StellarEngine, ShipModule.VacuumNozzle);
    Check(MathF.Abs(BaseStats.GetHarvesterCollectionRange(scene.Ship) - range * 1.75f) < 0.001f,
      "Module range bonuses add together");
    scene.Manager.UG.FleetCollectionRange *= 2f;
    Check(MathF.Abs(BaseStats.GetHarvesterCollectionRange(scene.Ship) - range * 3.5f) < 0.001f,
      "Range upgrades multiply the summed module bonus");
  }

  private static void CheckOverflowBuild()
  {
    using var scene = new Scene(ShipModule.SupernovaCore, ShipModule.OverflowDrive);
    AddBays(scene, ShipModule.OverflowVault, ShipModule.PhaseAnchor);
    float speed = BaseStats.GetHarvesterSpeed(scene.Ship), range = BaseStats.GetHarvesterCollectionRange(scene.Ship);
    for (int i = 0; i < 40; i++) scene.AddGem(new Vector2(600, 500));
    scene.Prepare();
    scene.Ship.CarryingGemCount = (uint)scene.Ship.ModuleReturnCapacity;
    scene.Ship.CarryingGemBaseValue = 100;
    scene.Invoke("ClaimSupernova", scene.Ship, scene.Transform.Position);
    scene.Invoke("ResolveClaimedGems", scene.Ship);
    // Each blast gem: 10 base, +10 past full cargo, +25 at full pressure.
    Check(scene.Ship.CarryingGemCount == 45 && scene.Ship.CarryingGemBaseValue == 100 + 32 * 45,
      "Supernova's blast earns its own pressure bonus and Overflow Vault's");
    UntitledGemGameGameScreen.HomeBasePos = new Vector2(50, 50);
    Check((bool)scene.Invoke("TryActivateReturnGate", scene.Ship, scene.Transform), "Phase Anchor returns the overloaded ship instantly");
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(scene.Ship.OverflowDriveStacks == 20 && MathF.Abs(BaseStats.GetHarvesterSpeed(scene.Ship) - speed * 3) < 0.001f
      && MathF.Abs(BaseStats.GetHarvesterCollectionRange(scene.Ship) - range * 1.125f) < 0.001f,
      "Overflow delivery powers the next trip at the capped speed and radius bonuses");
    scene.Ship.CarryingGemCount = 10;
    scene.Ship.CarryingGemBaseValue = 100;
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(scene.Ship.OverflowDriveStacks == 0, "A normal delivery does not sustain overflow boosts");
    scene.Ship.CarryingGemCount = 100;
    scene.Ship.CarryingGemBaseValue = 100;
    scene.Manager.Modules.TryEquip(0, 1, ShipModule.None);
    scene.Invoke("DeliverCargo", scene.Ship);
    Check(scene.Ship.OverflowDriveStacks == 0, "Unequipping prevents the next trip from inheriting the boost");
  }

  private static void CheckKineticBuild()
  {
    using var scene = new Scene(ShipModule.ChronoDrive, ShipModule.OverdriveCoil);
    AddBays(scene, ShipModule.MomentumDrive, ShipModule.KineticRefinery);
    scene.Manager.UG.HarvesterCapacity = 100;
    var gems = Enumerable.Range(0, 22).Select(_ => scene.AddGem(new Vector2(500, 500))).ToArray();
    scene.Prepare();
    foreach (var gem in gems.Take(20)) scene.Fleet.CollectGem(gem, scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 500 && scene.Ship.CarryingGemCount == 20,
      "Kinetic Refinery pays out the capped speed bonus without spending cargo space");
    Check(MathF.Abs(BaseStats.GetHarvesterSpeed(scene.Ship) - BaseStats.HarvesterSpeed * (1.75f * 2f * 3f * 1.15f)) < 0.01f,
      "Chrono, Overdrive and Momentum form a powerful multiplicative speed build");
    scene.Ship.AdvanceDroneTimers(5.1f);
    scene.Fleet.CollectGem(gems[20], scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 523, "With the boosts expired the refinery pays out momentum and its own speed only");
    scene.Fleet.CollectGem(gems[21], scene.Ship);
    Check(scene.Ship.CarryingGemBaseValue == 548, "Refreshing Overdrive takes the refinery back to its cap");
  }
}
