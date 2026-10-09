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
      { "Overdrive", "Planet Cracker", "Collector Swarm", "Fault Scan", "System Surge" }),
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
    Check(casts == 1 && !commands.IsActive(ManualFleetAbilities.FaultScanSlot)
      && commands.RemainingCooldown(ManualFleetAbilities.PlanetCrackerSlot) == 30f,
      "Hotkey two activates Planet Cracker rather than Fault Scan");
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
    wallet.CompletePrestige();
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
    Check(abilities.TryActivate(ManualFleetAbilities.FaultScanSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.AbilitySurgeSlot, effects.Add), "Fault Scan and System Surge overlap");
    Check(abilities.TryActivate(ManualFleetAbilities.PlanetCrackerSlot, effects.Add) && abilities.TryActivate(ManualFleetAbilities.CollectorSwarmSlot, effects.Add), "Cracker and swarm activate");
    Check(effects.SequenceEqual(new[] { ManualFleetAbilities.OverdriveSlot, ManualFleetAbilities.FaultScanSlot, ManualFleetAbilities.AbilitySurgeSlot, ManualFleetAbilities.PlanetCrackerSlot, ManualFleetAbilities.CollectorSwarmSlot }), "Each activation runs its effect once");
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
    Check(!abilities.IsActive(ManualFleetAbilities.FaultScanSlot) && abilities.SpeedMultiplier == 1f,
      "Reset clears session effects");
    CheckUnlockProgression();
    CheckCommandSignals();
    CheckCargoAndDrones();
    CheckMetaPersistence();
    CheckPlanetCracker();
    CheckAbilitySurge();
    Console.WriteLine("Manual commands passed: cooldowns, amplifier, fault scan, planet cracker, ability surge, collector scaling and persistence.");
  }

  private static void CheckCommandSignals()
  {
    var manager = new UpgradeManager();
    manager.UGM.CommandAmplifier = 2f;
    // Command signals need the Command Center (System Surge also Ship Systems), not any bought system.
    manager.UGM.CommandCenterUnlocked = manager.UGM.ShipSystemsUnlocked = true;
    var commands = FullyUnlockedCommands();
    foreach (var kind in new[] { SignalKind.CommandOverdriveDuration, SignalKind.WeakPointDamage,
      SignalKind.CommandAbilityRecharge, SignalKind.CommandPlanetCrackerPower, SignalKind.CommandCollectorValue })
    {
      Check(SignalCatalog.IsAvailable((int)kind), "Manual command signals do not require automatic ability unlocks");
      manager.Signals.Counts[(int)kind * SignalProgression.RarityCount] = 4;
    }
    commands.TryActivate(0, _ => { });
    commands.TryActivate(ManualFleetAbilities.FaultScanSlot, _ => { });
    Check(MathF.Abs(commands.CastDuration(0) - 24f) < 0.001f && commands.RemainingCooldown(0) == 45f,
      "Overdrive signals multiply amplifier duration without reducing cooldown");
    Check(MathF.Abs(commands.FaultScanMultiplier - 2.4f) < 0.001f && commands.CastDuration(ManualFleetAbilities.FaultScanSlot) == 12f,
      "Fault Scan signals strengthen its bursts without lengthening the scan");
    Check(MathF.Abs(commands.AbilitySurgeMultiplier - 8.2f) < 0.001f,
      "Surge signals multiply the recharge bonus");
    Check(MathF.Abs(commands.PlanetCrackerMultiplier - 2.4f) < 0.001f
      && MathF.Abs(commands.CollectorValueMultiplier - 1.2f) < 0.001f,
      "Planet Cracker and collector signals improve their commands");
    Array.Clear(manager.Signals.Counts);
    Check(commands.CastDuration(0) == 24f, "Running commands retain the bonuses present at activation");
    // The game snapshots Fault Scan's bursts as it places the weak points.
    Check(commands.PlanetCrackerMultiplier == 2f && commands.CollectorValueMultiplier == 1f
      && commands.FaultScanMultiplier == 2f, "Signals for future casts read current progression");
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
    Check(HarvesterCollectionSystem.ScaleCommandValue(ulong.MaxValue, 2f) == ulong.MaxValue,
      "Command income saturates without wrapping");
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
}
