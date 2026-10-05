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
    (UpgradeManager.Instance ?? new UpgradeManager()).UGM.CommandCenterUnlocked = true;
    var commands = new ManualFleetAbilities();
    commands.UpdateUnlocks(ulong.MaxValue);
    return commands;
  }

  private static void ResetReady(ManualFleetAbilities commands)
  {
    commands.Reset();
    commands.UpdateUnlocks(ulong.MaxValue);
  }

  private static void CheckPlanetCracker()
  {
    var manager = new UpgradeManager();
    var commands = FullyUnlockedCommands();
    int casts = 0;
    int slot = ManualFleetAbilities.PlanetCrackerSlot;
    Check(commands.ActivePlanetCrackerMultiplier == 0f, "No beam runs before the command is cast");
    Check(commands.TryActivate(slot, _ => casts++) && casts == 1
      && commands.CastDuration(slot) == 2.5f && commands.RemainingCooldown(slot) == 30f
      && commands.ActivePlanetCrackerMultiplier == 1f,
      "Planet Cracker fires a 2.5 second beam on a 30 second cooldown");
    Check(!commands.TryActivate(slot, _ => casts++) && casts == 1, "A running beam cannot be recast");
    manager.UGM.CommandAmplifier = 2;
    manager.UGM.PlanetCrackerBonus = 0.5f;
    manager.Signals.Counts[(int)SignalKind.CommandPlanetCrackerPower * SignalProgression.RarityCount] = 4;
    Check(commands.ActivePlanetCrackerMultiplier == 1f, "A running beam keeps the strength it was cast with");
    commands.Update(2.5f);
    Check(commands.ActivePlanetCrackerMultiplier == 0f && commands.RemainingCooldown(slot) == 27.5f,
      "The beam ends after its duration while the cooldown continues");
    commands.Update(30f);
    Check(commands.TryActivate(slot, _ => casts++)
      && MathF.Abs(commands.ActivePlanetCrackerMultiplier - 3.6f) < 0.001f,
      "Meta core, amplifier and signals multiply the beam's gems");
    commands.Reset();
    Check(commands.ActivePlanetCrackerMultiplier == 0f, "Command reset stops the beam");
  }

  private static void CheckAbilitySurge()
  {
    var manager = new UpgradeManager();
    var commands = FullyUnlockedCommands();
    Check(commands.TryActivate(ManualFleetAbilities.AbilitySurgeSlot, _ => { })
      && commands.AutomaticRechargeMultiplier == 4 && commands.CastDuration(4) == 15,
      "Surge must provide 4x recharge for 15 seconds");
    var ability = new MagnetAbility { CooldownTime = 100000, DurationTime = 1000 };
    commands.Update(14);
    ability.AdvanceCooldown(commands.AutomaticCooldownAdvanceMilliseconds);
    Check(ability.CooldownTime == 44000 && ability.DurationTime == 1000
      && commands.RemainingCooldown(4) == 76,
      "Surge must accelerate automatic cooldowns while command cooldowns and effect durations stay normal");
    commands.Update(2);
    ability.AdvanceCooldown(commands.AutomaticCooldownAdvanceMilliseconds);
    Check(ability.CooldownTime == 39000 && commands.AutomaticRechargeMultiplier == 1
      && commands.AutomaticCooldownAdvanceMilliseconds == 5000,
      "Frames spanning Surge expiry must boost only the remaining active second");
    commands.Update(1);
    ability.AdvanceCooldown(commands.AutomaticCooldownAdvanceMilliseconds);
    Check(ability.CooldownTime == 38000, "Recharge must return to normal after Surge");
    ResetReady(commands);
    manager.UGM.CommandAmplifier = 2;
    manager.Signals.Counts[(int)SignalKind.CommandAbilityRecharge * SignalProgression.RarityCount] = 4;
    commands.TryActivate(4, _ => { });
    Array.Clear(manager.Signals.Counts);
    manager.UGM.CommandAmplifier = 1;
    Check(MathF.Abs(commands.AutomaticRechargeMultiplier - 8.2f) < 0.001f,
      "An active Surge must retain the amplifier and signals from its activation");
    ability.CooldownTime = 100;
    ability.AdvanceCooldown(1000000);
    Check(ability.CooldownTime == 0, "Boosted cooldowns must stop at ready without integer underflow");
    ability.CooldownTime = 100;
    for (int i = 0; i < 10; i++) ability.AdvanceCooldown(0.5);
    Check(ability.CooldownTime == 95, "Small frames must retain fractional cooldown progress");
    commands.Reset();
    Check(commands.AutomaticRechargeMultiplier == 1 && commands.AutomaticCooldownAdvanceMilliseconds == 0,
      "Reset must remove Surge's recharge acceleration");
  }

  private static void CheckUnlockProgression()
  {
    var commands = new ManualFleetAbilities();
    int casts = 0;
    Check(commands.UnlockedCount == 0 && Enumerable.Range(0, 5).All(i => !commands.IsReady(i)),
      "New runs start with every command locked");
    Check(!commands.TryActivate(ManualFleetAbilities.PlanetCrackerSlot, _ => ++casts)
      && casts == 0 && commands.RemainingCooldown(ManualFleetAbilities.PlanetCrackerSlot) == 0f,
      "Locked commands reject input without triggering effects or cooldowns");
    Check(ManualFleetAbilities.Definitions.Select(d => d.Name).SequenceEqual(new[]
      { "Overdrive", "Planet Cracker", "Collector Swarm", "Homebase Magnetizer", "Ability Surge" }),
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
    wallet.EarnRedGems(ManualFleetAbilities.Definitions[ManualFleetAbilities.PlanetCrackerSlot].UnlockEarnings);
    Check(wallet.TryBuyAbilityPoint(), "Spend some earned gems");
    commands.UpdateUnlocks(wallet.RedGemsEarnedThisRun);
    Check(commands.UnlockedCount == 2, "Spending balance never relocks earned commands");
    commands.TryActivate(ManualFleetAbilities.PlanetCrackerSlot, _ => ++casts);
    Check(casts == 1 && commands.MagnetCast == 0
      && commands.RemainingCooldown(ManualFleetAbilities.PlanetCrackerSlot) == 30f,
      "Hotkey two activates Planet Cracker rather than gravity");
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
      Check(resumed.UnlockedCount == 2 && resumed.IsReady(ManualFleetAbilities.PlanetCrackerSlot),
        "Reload restores earned commands with session cooldowns cleared");
    }
    finally
    {
      foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    wallet.CompletePrestige(1);
    commands.Reset();
    commands.UpdateUnlocks(wallet.RedGemsEarnedThisRun);
    Check(commands.UnlockedCount == 0 && commands.RemainingCooldown(ManualFleetAbilities.PlanetCrackerSlot) == 0f,
      "Prestige resets command unlocks and timers");
    commands.UpdateUnlocks(ManualFleetAbilities.Definitions[0].UnlockEarnings);
    Check(commands.UnlockedCount == 1 && commands.IsReady(0), "A new run earns Overdrive again");
  }

  public static void Run()
  {
    var manager = new UpgradeManager();
    var lockedCommands = new ManualFleetAbilities();
    lockedCommands.UpdateUnlocks(ulong.MaxValue);
    Check(!lockedCommands.CommandsEnabled && lockedCommands.UnlockedCount == 0
      && !lockedCommands.TryActivate(0, _ => { }),
      "Commands do not exist before purchasing Command Center");
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
    Check(abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.AbilitySurgeSlot, effects.Add), "Magnetizer and cash out overlap");
    Check(abilities.TryActivate(ManualFleetAbilities.PlanetCrackerSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.CollectorSwarmSlot, effects.Add), "Cracker and swarm activate");
    Check(effects.SequenceEqual(new[] { ManualFleetAbilities.OverdriveSlot, ManualFleetAbilities.MagnetizerSlot, ManualFleetAbilities.AbilitySurgeSlot, ManualFleetAbilities.PlanetCrackerSlot, ManualFleetAbilities.CollectorSwarmSlot }), "Each activation runs its effect once");
    Check(!abilities.TryActivate(ManualFleetAbilities.PlanetCrackerSlot, effects.Add), "A running command cannot retrigger");
    abilities.Update(1000f);
    Check(Enumerable.Range(0, 5).All(abilities.IsReady) && effects.Count == 5, "Commands never auto-cast");
    manager.UGM.CommandAmplifier = 2f;
    abilities.TryActivate(0, effects.Add);
    Check(abilities.RemainingDuration(0) == 20f && abilities.RemainingCooldown(0) == 45f,
      "Amplifier extends Overdrive without shortening cooldown");
    Check(abilities.AbilitySurgeMultiplier == 7f, "Amplifier scales the recharge bonus");
    Check(abilities.PlanetCrackerMultiplier == 2f, "Amplifier scales the Planet Cracker beam");
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
    CheckPlanetCracker();
    CheckAbilitySurge();
    Console.WriteLine("Manual commands passed: cooldowns, amplifier, bounded gravity, pooling, planet cracker, ability surge, collector scaling and persistence.");
  }

  private static void CheckCommandSignals()
  {
    var manager = new UpgradeManager();
    manager.UGM.CommandAmplifier = 2f;
    var commands = FullyUnlockedCommands();
    foreach (var kind in new[] { SignalKind.CommandOverdriveDuration, SignalKind.CommandMagnetStrength,
      SignalKind.CommandAbilityRecharge, SignalKind.CommandPlanetCrackerPower, SignalKind.CommandCollectorValue })
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
    Check(MathF.Abs(commands.AbilitySurgeMultiplier - 8.2f) < 0.001f,
      "Surge signals multiply the recharge bonus");
    Check(MathF.Abs(commands.PlanetCrackerMultiplier - 2.4f) < 0.001f
      && MathF.Abs(commands.CollectorValueMultiplier - 1.2f) < 0.001f,
      "Planet Cracker and collector signals improve their commands");
    Array.Clear(manager.Signals.Counts);
    Check(MathF.Abs(commands.MagnetStrength - 2.4f) < 0.001f && commands.CastDuration(0) == 24f,
      "Running commands retain the bonuses present at activation");
    Check(commands.PlanetCrackerMultiplier == 2f && commands.CollectorValueMultiplier == 1f,
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
    // Starts outside the inner field, then crosses into exponential attraction.
    float distance = MathF.Sqrt(1000f * 1000f + 500f * 500f);
    float exposureToInnerField = (distance * distance - 600f * 600f) / (2f * 600f * 600f);
    float expected = 1000f / distance * 600f * MathF.Exp(-(4.4f - MathF.Log(0.82f) - exposureToInnerField));
    Check(MathF.Abs(grid.Gems[0].X - expected) < 0.02f && MathF.Abs(grid.Gems[^1].X - expected) < 0.02f,
      "Batched gems receive equal total pull, including the final partial frame");
    var early = ManualGravityField.Pull(new Vector2(100, 0), Vector2.Zero, 0.4f, 1f);
    var late = ManualGravityField.Pull(new Vector2(10000, 0), Vector2.Zero, 0.4f, 1f);
    Check(MathF.Abs(early.X - 40f) < 0.001f && 10000f - late.X < 100f - early.X,
      "Nearby gems retain their pull while distant gems move more gently");
    float edgeRetention = MathF.Exp(-0.001f);
    float atEdge = ManualGravityField.Pull(new Vector2(600, 0), Vector2.Zero, edgeRetention, 1f).X;
    float outsideEdge = ManualGravityField.Pull(new Vector2(600.01f, 0), Vector2.Zero, edgeRetention, 1f).X;
    Check(MathF.Abs((600f - atEdge) - (600.01f - outsideEdge)) < 0.001f,
      "Falloff must join the inner field smoothly");
    var once = ManualGravityField.Pull(new Vector2(2000, 0), Vector2.Zero, MathF.Exp(-6f), 1f);
    var many = new Vector2(2000, 0);
    for (int i = 0; i < 600; i++) many = ManualGravityField.Pull(many, Vector2.Zero, MathF.Exp(-0.01f), 1f);
    Check(Vector2.Distance(once, many) < 0.05f,
      "One delayed batch must match small updates even when crossing the falloff boundary");
    Check(ManualGravityField.Pull(new Vector2(9, 0), Vector2.Zero, 0.1f, 10f) == new Vector2(9, 0),
      "Gems already in homebase collection range must stay in place");
    var translated = ManualGravityField.Pull(new Vector2(2500, 400), new Vector2(500, 400), MathF.Exp(-6f), 1f);
    Check(Vector2.Distance(translated, once + new Vector2(500, 400)) < 0.001f,
      "Falloff must follow homebase position rather than the world origin");
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
    float pulsePosition = 1000f / distance * MathF.Sqrt(distance * distance + 2f * 600f * 600f * MathF.Log(0.82f));
    Check(MathF.Abs(grid.Gems[0].X - pulsePosition) < 0.01f, "Reused IDs and slots get the new cast pulse with distance falloff");
    for (int i = 0; i < grid.MaxCapacity - 1; i++) grid.RecycleIndex(i);
    grid.MoveGem(grid.MaxCapacity - 1, 1000f, 500f);
    ResetReady(abilities);
    abilities.TryActivate(ManualFleetAbilities.MagnetizerSlot, _ => { });
    field.Update(grid, abilities, Vector2.Zero, 10f, Move);
    Check(field.LastVisited == 1 && MathF.Abs(grid.Gems[^1].X - pulsePosition) < 0.01f,
      "A depleted late-game index pulls its few survivors immediately");
    Check(HarvesterCollectionSystem.ScaleCommandValue(ulong.MaxValue, 2f) == ulong.MaxValue,
      "Command income saturates without wrapping");
  }

  private static void CheckCargoAndDrones()
  {
    using var scene = new ExpandedModuleChecks.Scene(ShipModule.None);
    scene.Manager.UG.HarvesterSpeed *= 100;
    scene.Manager.UG.FleetCollectionRange *= 100;
    scene.Fleet.GetCollectorSwarmStats(out float speed, out float range);
    Check(speed >= BaseStats.GetHarvesterSpeed(scene.Ship) && range >= BaseStats.GetHarvesterCollectionRange(scene.Ship),
      "Collectors inherit late-game fleet strength");
    Check(scene.Fleet.GetFleetCargoCapacity() == (ulong)BaseStats.GetHarvesterCapacity(scene.Ship),
      "Cargo capacity uses the actual active fleet");
    var drone = new Harvester { Type = Harvester.HarvesterType.Drone, IsCommandDrone = true,
      CommandDroneSpeed = speed, CommandDroneRange = range, CommandDroneLifetime = 8f, CommandDroneValueMultiplier = 10f };
    Check(BaseStats.GetHarvesterSpeed(drone) == speed && BaseStats.GetHarvesterCollectionRange(drone) == range,
      "Collector stats use the cast snapshot");
    Check(BaseStats.GetHarvesterDeliveryValue(drone, 100) == 1000, "A fixed collector count can represent a larger fleet's output");
    drone.AdvanceDroneTimers(8f);
    Check(drone.ReturningToHomebase, "Collectors return home");
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
      manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["CAM1"] = 5, ["PCB1"] = 5 } });
      Check(manager.UGM.CommandCenterUnlocked, "Command Center restores from prestige talents");
      Check(MathF.Abs(manager.UGM.CommandAmplifier - 2f) < 0.001f, "Amplifier ranks restore from current saves");
      Check(upgrades.UpgradeButtonsMeta["CAM1"].Data.NumLevels == 5, "Amplifier caps at five ranks");
      Check(MathF.Abs(manager.UGM.PlanetCrackerBonus - 0.5f) < 0.001f,
        "Permanent Planet Cracker ranks must restore");
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
