using System.Diagnostics;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

internal static class ManualAbilityChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private static ManualFleetAbilities FullyUnlockedCommands()
  {
    var commands = new ManualFleetAbilities();
    commands.UpdateUnlocks(ulong.MaxValue);
    return commands;
  }

  private static void ResetReady(ManualFleetAbilities commands)
  {
    commands.Reset();
    commands.UpdateUnlocks(ulong.MaxValue);
  }

  private static void CheckUnlockProgression()
  {
    var commands = new ManualFleetAbilities();
    int casts = 0;
    Check(commands.UnlockedCount == 0 && Enumerable.Range(0, 5).All(i => !commands.IsReady(i)),
      "New runs start with every command locked");
    Check(!commands.TryActivate(ManualFleetAbilities.CrystalShatterSlot, _ => ++casts)
      && casts == 0 && commands.RemainingCooldown(ManualFleetAbilities.CrystalShatterSlot) == 0f,
      "Locked commands reject input without triggering effects or cooldowns");
    Check(ManualFleetAbilities.Definitions.Select(d => d.Name).SequenceEqual(new[]
      { "Overdrive", "Crystal Shatter", "Collector Swarm", "Homebase Magnetizer", "Cash Out" }),
      "Command labels follow the new hotkey and unlock order");
    for (int slot = 0; slot < ManualFleetAbilities.Definitions.Length; slot++)
    {
      ulong threshold = ManualFleetAbilities.Definitions[slot].UnlockEarnings;
      commands.UpdateUnlocks(threshold - 1);
      Check(commands.UnlockedCount == slot && !commands.IsUnlocked(slot), "Commands stay locked below their milestone");
      commands.UpdateUnlocks(threshold);
      Check(commands.UnlockedCount == slot + 1 && commands.IsReady(slot)
        && Enumerable.Range(slot + 1, 4 - slot).All(i => !commands.IsUnlocked(i)),
        "Exactly the next command unlocks at each earnings milestone");
    }
    var wallet = new GameState();
    wallet.EarnRedGems(ManualFleetAbilities.Definitions[ManualFleetAbilities.CrystalShatterSlot].UnlockEarnings);
    Check(wallet.TryBuyAbilityPoint(), "Spend some earned gems");
    commands.UpdateUnlocks(wallet.RedGemsEarnedThisRun);
    Check(commands.UnlockedCount == 2, "Spending balance never relocks earned commands");
    commands.TryActivate(ManualFleetAbilities.CrystalShatterSlot, _ => ++casts);
    Check(casts == 1 && commands.MagnetCast == 0
      && commands.RemainingCooldown(ManualFleetAbilities.CrystalShatterSlot) == 30f,
      "Hotkey two activates Crystal Shatter rather than gravity");
    var path = Path.Combine(Path.GetTempPath(), "command-unlocks-" + Guid.NewGuid() + ".json");
    try
    {
      var store = new GameSaveStore(path);
      Check(store.Save(new GameSave { RedGems = wallet.CurrentRedGemCount,
        RedGemsEarnedThisRun = wallet.RedGemsEarnedThisRun }), "Save run earnings");
      var save = store.Load();
      Check(save != null, "Reload run earnings");
      var restored = new GameState();
      restored.Restore(save.RedGems, save.BlueGems, save.PurpleGems, save.RedGemsEarnedThisRun);
      var resumed = new ManualFleetAbilities();
      resumed.UpdateUnlocks(restored.RedGemsEarnedThisRun);
      Check(resumed.UnlockedCount == 2 && resumed.IsReady(ManualFleetAbilities.CrystalShatterSlot),
        "Reload restores earned commands with session cooldowns cleared");
    }
    finally
    {
      foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    wallet.CompletePrestige(1);
    commands.Reset();
    commands.UpdateUnlocks(wallet.RedGemsEarnedThisRun);
    Check(commands.UnlockedCount == 0 && commands.RemainingCooldown(ManualFleetAbilities.CrystalShatterSlot) == 0f,
      "Prestige resets command unlocks and timers");
    commands.UpdateUnlocks(ManualFleetAbilities.Definitions[0].UnlockEarnings);
    Check(commands.UnlockedCount == 1 && commands.IsReady(0), "A new run earns Overdrive again");
  }

  public static void Run()
  {
    var manager = new UpgradeManager();
    var abilities = FullyUnlockedCommands();
    var effects = new List<int>();
    Check(Enumerable.Range(0, 5).All(abilities.IsReady), "All commands start ready");
    Check(!abilities.TryActivate(-1, effects.Add) && !abilities.TryActivate(5, effects.Add), "Invalid slots ignored");
    Check(abilities.TryActivate(0, effects.Add), "Overdrive activates");
    Check(abilities.SpeedMultiplier == 2f && abilities.FreeFuel, "Overdrive boosts speed and suspends fuel use");
    Check(!abilities.TryActivate(0, effects.Add), "Active commands cannot retrigger");
    abilities.Update(10f);
    Check(abilities.SpeedMultiplier == 1f && !abilities.FreeFuel && abilities.RemainingCooldown(0) == 35f,
      "Duration expires while cooldown continues");
    Check(abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.CashOutSlot, effects.Add), "Magnetizer and cash out overlap");
    Check(abilities.TryActivate(ManualFleetAbilities.CrystalShatterSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.CollectorSwarmSlot, effects.Add), "Crystal and swarm activate");
    Check(effects.SequenceEqual(new[] { ManualFleetAbilities.OverdriveSlot, ManualFleetAbilities.MagnetizerSlot, ManualFleetAbilities.CashOutSlot, ManualFleetAbilities.CrystalShatterSlot, ManualFleetAbilities.CollectorSwarmSlot }), "Each activation runs its effect once");
    Check(!abilities.TryActivate(ManualFleetAbilities.CrystalShatterSlot, effects.Add), "Instant command obeys cooldown");
    abilities.Update(1000f);
    Check(Enumerable.Range(0, 5).All(abilities.IsReady) && effects.Count == 5, "Commands never auto-cast");
    manager.UGM.CommandAmplifier = 2f;
    abilities.TryActivate(0, effects.Add);
    Check(abilities.RemainingDuration(0) == 20f && abilities.RemainingCooldown(0) == 45f,
      "Amplifier extends Overdrive without shortening cooldown");
    Check(abilities.CashOutMultiplier == 2f, "Amplifier scales the cash out bonus");
    var quality = new GemSpawnData { Type = GemTypes.Red, BaseValue = 10 };
    uint earlyCrystal = GemQualityTable.RollCurrent(quality, abilities.CrystalShardValueMultiplier(0)).BaseValue;
    uint lateCrystal = GemQualityTable.RollCurrent(quality, abilities.CrystalShardValueMultiplier(10000)).BaseValue;
    Check(lateCrystal > earlyCrystal * 100, "Crystal reward grows with fleet capacity rather than entity count");
    manager.UGM.CommandAmplifier = 1f;
    Check(abilities.RemainingDuration(0) == 20f, "Duration snapshots cast power");
    abilities.Reset();
    Check(abilities.MagnetCast == 0 && abilities.MagnetElapsed == 0f && abilities.SpeedMultiplier == 1f,
      "Reset clears session effects");
    CheckUnlockProgression();
    CheckCommandSignals();
    CheckGravity();
    CheckCargoAndDrones();
    CheckMetaPersistence();
    Console.WriteLine("Manual commands passed: cooldowns, amplifier, bounded gravity, pooling, cash out, collector scaling and persistence.");
  }

  private static void CheckCommandSignals()
  {
    var manager = new UpgradeManager();
    manager.UGM.CommandAmplifier = 2f;
    var commands = FullyUnlockedCommands();
    foreach (var kind in new[] { SignalKind.CommandOverdriveDuration, SignalKind.CommandMagnetStrength,
      SignalKind.CommandCashOutBonus, SignalKind.CommandCrystalValue, SignalKind.CommandCollectorValue })
    {
      Check(SignalCatalog.IsAvailable((int)kind), "Manual command signals do not require automatic ability unlocks");
      manager.Signals.Counts[(int)kind * SignalProgression.RarityCount] = 4;
    }
    commands.TryActivate(0, _ => { });
    commands.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    Check(MathF.Abs(commands.CastDuration(0) - 24f) < 0.001f && commands.RemainingCooldown(0) == 45f,
      "Overdrive signals multiply amplifier duration without reducing cooldown");
    Check(MathF.Abs(commands.MagnetStrength - 2.4f) < 0.001f && commands.CastDuration(ManualFleetAbilities.MagnetizerSlot) == 4f,
      "Gravity signal strengthens the pull without increasing duration or work budget");
    Check(MathF.Abs(commands.CashOutMultiplier - 2.2f) < 0.001f,
      "Cash Out signals multiply the bonus, preserving the original cargo value");
    Check(MathF.Abs(commands.CrystalShardValueMultiplier(0) - 19.2f) < 0.001f
      && MathF.Abs(commands.CollectorValueMultiplier - 1.2f) < 0.001f,
      "Crystal and collector signals improve value while counts remain fixed");
    Array.Clear(manager.Signals.Counts);
    Check(MathF.Abs(commands.MagnetStrength - 2.4f) < 0.001f && commands.CastDuration(0) == 24f,
      "Running commands retain the bonuses present at activation");
    Check(commands.CashOutMultiplier == 2f && commands.CollectorValueMultiplier == 1f,
      "Signals for future instant commands read current progression");
    manager.Signals.Counts[(int)SignalKind.CommandMagnetStrength * SignalProgression.RarityCount] = 1000;
    ResetReady(commands);
    var grid = new GemSpatialIndex(1, 30);
    grid.AddGem(1, 1000f, 0f, 1);
    var gravity = new ManualGravityField(1);
    commands.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    gravity.Update(grid, commands, Vector2.Zero, 1f, (index, position) => grid.MoveGem(index, position.X, position.Y));
    Check(grid.Gems[0].X >= 0f && grid.Gems[0].X < 1000f && float.IsFinite(grid.Gems[0].X),
      "Large gravity signal stacks cannot overshoot or produce invalid positions");
  }

  private static void CheckGravity()
  {
    var manager = new UpgradeManager();
    var abilities = FullyUnlockedCommands();
    var grid = new GemSpatialIndex(ManualGravityField.FrameBudget + 100, 30);
    var field = new ManualGravityField(grid.MaxCapacity);
    for (int i = 0; i < grid.MaxCapacity; i++) grid.AddGem(i, 1000f, 500f, 1);
    grid.TryClaim(1);
    grid.RemoveFromQueries(1);
    void Move(int index, Vector2 position) => grid.MoveGem(index, position.X, position.Y);
    abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(field.LastVisited == ManualGravityField.FrameBudget && grid.Gems[^1].X == 1000f,
      "Activation respects the fixed work budget");
    for (int i = 0; i < 240; i++)
    {
      abilities.Update(1f / 60f);
      field.Update(grid, abilities, Vector2.Zero, 10f, Move);
      Check(field.LastVisited <= ManualGravityField.FrameBudget, "Every frame stays bounded");
    }
    abilities.Update(1f);
    for (int i = 0; i < 3; i++) field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(grid.Gems[1].X == 1000f, "Reserved gems are never pulled");
    float expected = 1000f * 0.82f * MathF.Exp(-4.4f);
    Check(MathF.Abs(grid.Gems[0].X - expected) < 0.02f && MathF.Abs(grid.Gems[^1].X - expected) < 0.02f,
      "Batched gems receive equal total pull, including the final partial frame");
    var early = ManualGravityField.Pull(new Vector2(100, 0), Vector2.Zero, 0.4f, 1f);
    var late = ManualGravityField.Pull(new Vector2(10000, 0), Vector2.Zero, 0.4f, 1f);
    Check(late.X == early.X * 100f, "Pull scales with field distance");
    Check(ManualGravityField.Pull(Vector2.Zero, Vector2.Zero, 0.4f, 10f) == Vector2.Zero,
      "Home center stays finite and stationary");
    ResetReady(abilities);
    abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    abilities.Update(10f);
    var skippedFrame = new ManualGravityField(grid.MaxCapacity);
    skippedFrame.Update(grid, abilities, Vector2.Zero, 1f, Move);
    Check(skippedFrame.LastVisited == ManualGravityField.FrameBudget, "Long frames still apply a completed cast with bounded work");
    // Reuse one slot after expiry to exercise birth during the final batch.
    grid.RecycleIndex(0);
    int newborn = grid.AddGem(0, 1000f, 500f, 1);
    skippedFrame.RegisterSpawn(grid, newborn, abilities);
    for (int i = 0; i < 3; i++) skippedFrame.Update(grid, abilities, Vector2.Zero, 1f, Move);
    Check(grid.Gems[newborn].X == 1000f, "Newborns cannot receive attraction from before they existed");
    ResetReady(abilities);
    grid.RecycleIndex(0);
    grid.AddGem(0, 1000f, 500f, 1);
    field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(grid.Gems[0].X == 1000f, "Reset cancels deferred work");
    abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(MathF.Abs(grid.Gems[0].X - 820f) < 0.01f, "Reused IDs and slots get the new cast pulse");
    for (int i = 0; i < grid.MaxCapacity - 1; i++) grid.RecycleIndex(i);
    grid.MoveGem(grid.MaxCapacity - 1, 1000f, 500f);
    ResetReady(abilities);
    abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(field.LastVisited == 1 && MathF.Abs(grid.Gems[^1].X - 820f) < 0.01f,
      "A depleted late-game index pulls its few survivors immediately");
    Check(HarvesterCollectionSystem.ScaleCommandValue(ulong.MaxValue, 2f) == ulong.MaxValue,
      "Command income saturates without wrapping");
  }

  private static void CheckCargoAndDrones()
  {
    using var scene = new ExpandedModuleChecks.Scene(ShipModule.None);
    scene.Ship.CarryingGemCount = 5;
    scene.Ship.CarryingGemBaseValue = 100;
    ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
    var position = scene.Transform.Position;
    scene.Fleet.CashOutFleet(1.5f);
    Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 150 && scene.Ship.CarryingGemCount == 0
      && scene.Ship.CarryingGemBaseValue == 0 && scene.Transform.Position == position, "Cash out unloads once without moving ships");
    before = UntitledGemGameGameScreen.DeliveredUncounted;
    scene.Fleet.CashOutFleet(1.5f);
    Check(UntitledGemGameGameScreen.DeliveredUncounted == before, "Empty cargo cannot be paid twice");
    scene.Manager.UG.HarvesterSpeed *= 100;
    scene.Manager.UG.HarvesterCollectionRange *= 100;
    scene.Fleet.GetCollectorSwarmStats(out float speed, out float range);
    Check(speed >= BaseStats.GetHarvesterSpeed(scene.Ship) && range >= BaseStats.GetHarvesterCollectionRange(scene.Ship),
      "Collectors inherit late-game fleet strength");
    Check(scene.Fleet.GetFleetCargoCapacity() == (ulong)BaseStats.GetHarvesterCapacity(scene.Ship),
      "Crystal capacity uses the actual active fleet");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone, IsCommandDrone = true,
      CommandDroneSpeed = speed, CommandDroneRange = range, CommandDroneLifetime = 8f, CommandDroneValueMultiplier = 10f };
    Check(BaseStats.GetHarvesterSpeed(drone) == speed && BaseStats.GetHarvesterCollectionRange(drone) == range,
      "Collector stats use the cast snapshot");
    Check(BaseStats.GetHarvesterDeliveryValue(drone, 100) == 1000, "A fixed collector count can represent a larger fleet's output");
    drone.AdvanceDroneTimers(8f);
    Check(drone.ReturningToHomebase, "Collectors return home");
    UntitledGemGameGameScreen.DeliveredUncounted = before;
  }

  private static void CheckMetaPersistence()
  {
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    var definitions = UpgradeManager.CurrentUpgrades;
    try
    {
      var upgrades = UpgradeManager.CurrentUpgrades = new Upgrades();
      upgrades.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta.json")),
        File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta_buttons.json")),
        upgrades.UpgradeButtonsMeta, upgrades.UpgradeDefinitionsMeta);
      var manager = new UpgradeManager();
      manager.RestoreProgress(new GameSave { Meta = new() { ["RH1"] = 1, ["CAM1"] = 5 } });
      Check(MathF.Abs(manager.UGM.CommandAmplifier - 2f) < 0.001f, "Amplifier ranks restore from current saves");
      Check(upgrades.UpgradeButtonsMeta["CAM1"].Data.NumLevels == 5, "Amplifier caps at five ranks");
    }
    finally { UpgradeManager.CurrentUpgrades = definitions; }
  }

  public static void Benchmark()
  {
    _ = new UpgradeManager();
    foreach (int population in new[] { 100_000, 500_000 })
    {
      var grid = new GemSpatialIndex(population, 30);
      var random = new Random(73);
      for (int i = 0; i < population; i++) grid.AddGem(i, random.NextSingle() * 16000f - 8000f,
        random.NextSingle() * 9000f - 4500f, 1);
      var field = new ManualGravityField(population);
      var abilities = FullyUnlockedCommands();
      Action<int, Vector2> move = (index, position) => grid.MoveGem(index, position.X, position.Y);
      abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
      var samples = new List<double>();
      long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
      for (int frame = 0; frame < 300; frame++)
      {
        abilities.Update(1f / 60f);
        long start = Stopwatch.GetTimestamp();
        field.Update(grid, abilities, Vector2.Zero, 20f, move);
        if (field.LastVisited > 0) samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        Check(field.LastVisited <= ManualGravityField.FrameBudget, "Benchmark exceeded gravity budget");
      }
      long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
      samples.Sort();
      Console.WriteLine($"Magnetizer {population:N0} gems: median {samples[samples.Count / 2]:0.00} ms, "
        + $"p95 {samples[(int)(samples.Count * 0.95)]:0.00} ms, max {samples[^1]:0.00} ms; "
        + $"{allocated:N0} bytes including spatial-cell growth (CPU movement/index only).");
    }
  }
}
