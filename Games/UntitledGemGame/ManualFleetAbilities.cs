using System;

namespace UntitledGemGame;

// Commands are independent of automatically cast abilities. Running effects are session-only.
public sealed class ManualFleetAbilities
{
  public const int OverdriveSlot = 0;
  public const int CollectorSwarmSlot = 2;
  public const int FaultScanSlot = 3;
  public const int AbilitySurgeSlot = 4;
  public const int PlanetCrackerSlot = 1;
  // Name and Effect are English (Name also identifies the command in capture scripts);
  // translate them where shown.
  public sealed record Definition(string Name, string Effect, float Duration, float Cooldown, ulong UnlockEarnings);
  public static readonly Definition[] Definitions =
  [
    new(Loc.N("Overdrive"), Loc.N("2x speed / no fuel use"), 10f, 45f, 250),
    new(Loc.N("Planet Cracker"), Loc.N("Overload beam rips gems off the planet"), 2.5f, 30f, 5_000),
    new(Loc.N("Collector Swarm"), Loc.N("Launch 8 fleet-powered drones"), 0f, 75f, 250_000),
    new(Loc.N("Fault Scan"), Loc.N("Marks 8 weak points to burst"), 12f, 60f, 5_000_000),
    new(Loc.N("System Surge"), Loc.N("Ship systems recharge 4x faster for 15s"), 15f, 90f, 100_000_000)
  ];
  public const int CollectorCount = 8;
  // Fault Scan sweeps this many weak points across the planet's near side (the Effect text
  // names the count) over FaultScanSweepSeconds; they last until the scan ends. A hit near
  // one bursts it for FaultScanSeconds of the planet's recent damage, or FaultScanPower
  // times the hit's fire power if that is more, scaled by FaultScanMultiplier.
  public const int FaultScanPoints = 8;
  public const float FaultScanSweepSeconds = 0.8f;
  public const float FaultScanSeconds = 1.5f;
  public const int FaultScanPower = 20;
  public float FaultScanMultiplier => Power * SignalBoost(SignalKind.WeakPointDamage);
  // The Planet Cracker beam knocks loose this many gems per second for each point of
  // cannon fire power, and cracks deeper than the cannon: its gems roll their colors
  // as if fire power were PlanetCrackerDepth higher.
  public const float PlanetCrackerGemsPerSecond = 12f;
  public const int PlanetCrackerDepth = 4;
  public float PlanetCrackerMultiplier => (1f + (UpgradeManager.Instance?.UGM.PlanetCrackerBonus ?? 0f))
    * Power * SignalBoost(SignalKind.CommandPlanetCrackerPower);
  private float crackerMultiplier = 1f;
  // The strength of the running beam, fixed when it was cast.
  public float ActivePlanetCrackerMultiplier => IsActive(PlanetCrackerSlot) ? crackerMultiplier : 0f;

  private readonly float[] durations = new float[5];
  private readonly float[] castDurations = new float[5];
  private readonly float[] cooldowns = new float[5];
  private static float SignalBoost(SignalKind kind) => UpgradeManager.Instance?.Signals.Multiplier(kind) ?? 1f;
  public float CollectorValueMultiplier => SignalBoost(SignalKind.CommandCollectorValue);
  public float Power => Math.Clamp(UpgradeManager.Instance?.UGM.CommandAmplifier ?? 1f, 1f, 2f);
  public bool CommandsEnabled => UpgradeManager.Instance?.UGM.CommandCenterUnlocked == true;
  public int UnlockedCount { get; private set; }
  public ulong RunEarnings { get; private set; }
  public void UpdateUnlocks(ulong runEarnings)
  {
    RunEarnings = runEarnings;
    UnlockedCount = 0;
    if (!CommandsEnabled) return;
    if (UpgradeManager.Instance?.UGM.CommandNexus == true)
    {
      UnlockedCount = Definitions.Length;
      return;
    }
    while (UnlockedCount < Definitions.Length && runEarnings >= Definitions[UnlockedCount].UnlockEarnings)
      ++UnlockedCount;
  }
  public bool IsUnlocked(int slot) => (uint)slot < (uint)UnlockedCount;
  public bool IsActive(int slot) => durations[slot] > 0f;
  public bool IsReady(int slot) => IsUnlocked(slot) && cooldowns[slot] <= 0f && !IsActive(slot);
  public float RemainingDuration(int slot) => durations[slot];
  public float CastDuration(int slot) => castDurations[slot];
  public float RemainingCooldown(int slot) => cooldowns[slot];
  public float SpeedMultiplier => IsActive(OverdriveSlot) ? 2f : 1f;
  public bool FreeFuel => IsActive(OverdriveSlot);
  public float AbilitySurgeMultiplier => 1f + 3f * Power * SignalBoost(SignalKind.CommandAbilityRecharge);
  private float surgeMultiplier = 1f;
  public double AutomaticCooldownAdvanceMilliseconds { get; private set; }
  public float AutomaticRechargeMultiplier => IsActive(AbilitySurgeSlot) ? surgeMultiplier : 1f;
  public int SessionVersion { get; private set; }

  public bool TryActivate(int slot, Action<int> effect)
  {
    if ((uint)slot >= Definitions.Length || !IsReady(slot)) return false;
    if (slot == PlanetCrackerSlot) crackerMultiplier = PlanetCrackerMultiplier;
    castDurations[slot] = Definitions[slot].Duration * (slot == OverdriveSlot ? Power * SignalBoost(SignalKind.CommandOverdriveDuration) : 1f);
    durations[slot] = castDurations[slot];
    bool nexus = UpgradeManager.Instance?.UGM.CommandNexus == true;
    cooldowns[slot] = Definitions[slot].Cooldown
      * (nexus ? PrestigeTalentEffects.CommandNexusCooldownMultiplier : 1f);
    if (nexus || UpgradeManager.Instance?.UGM.CommandChain == true)
      for (int i = 0; i < cooldowns.Length; i++)
        if (i != slot) cooldowns[i] *= 1f - PrestigeTalentEffects.CommandChainCooldownShare;
    if (slot == AbilitySurgeSlot) surgeMultiplier = AbilitySurgeMultiplier;
    effect(slot);
    return true;
  }

  public void Update(float seconds)
  {
    seconds = Math.Max(0f, seconds);
    // Integrate only the portion of this frame inside the Surge window.
    AutomaticCooldownAdvanceMilliseconds = 1000d * (seconds
      + Math.Min(durations[AbilitySurgeSlot], seconds) * (surgeMultiplier - 1f));
    for (int i = 0; i < Definitions.Length; i++)
    {
      durations[i] = Math.Max(0f, durations[i] - seconds);
      cooldowns[i] = Math.Max(0f, cooldowns[i] - seconds);
    }
  }

  public void Reset()
  {
    Array.Clear(durations);
    Array.Clear(castDurations);
    Array.Clear(cooldowns);
    RunEarnings = 0;
    UnlockedCount = 0;
    crackerMultiplier = 1f;
    surgeMultiplier = 1f;
    AutomaticCooldownAdvanceMilliseconds = 0;
    ++SessionVersion;
  }
}
