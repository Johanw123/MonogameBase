using System;

namespace UntitledGemGame;

// Commands are independent of automatically cast abilities. Running effects are session-only.
public sealed class ManualFleetAbilities
{
  public const int OverdriveSlot = 0;
  public const int CollectorSwarmSlot = 2;
  public const int MagnetizerSlot = 3;
  public const int AbilitySurgeSlot = 4;
  public const int ReserveBurstSlot = 1;
  public sealed record Definition(string Name, string Effect, float Duration, float Cooldown, ulong UnlockEarnings);
  public static readonly Definition[] Definitions =
  [
    new("Overdrive", "2x speed / no fuel use", 10f, 45f, 250),
    new("Reserve Burst", "Consume the reserve with bonus value", 0f, 30f, 5_000),
    new("Collector Swarm", "Launch 8 fleet-powered drones", 0f, 75f, 250_000),
    new("Homebase Magnetizer", "Pull gems towards home; strength fades beyond 600 units", 4f, 60f, 5_000_000),
    new("Ability Surge", "Automatic abilities recharge 4x faster for 15s", 15f, 90f, 100_000_000)
  ];
  public const int CollectorCount = 8;
  public float ReserveBurstMultiplier => 1f + (0.5f + (UpgradeManager.Instance?.UGM.ReserveBurstBonus ?? 0f))
    * Power * SignalBoost(SignalKind.CommandReserveBurstBonus);
  public ulong ReserveBurstPayout { get; private set; }

  private readonly float[] durations = new float[5];
  private readonly float[] castDurations = new float[5];
  private readonly float[] cooldowns = new float[5];
  private static float SignalBoost(SignalKind kind) => UpgradeManager.Instance?.Signals.Multiplier(kind) ?? 1f;
  public float CollectorValueMultiplier => SignalBoost(SignalKind.CommandCollectorValue);
  public float Power => Math.Clamp(UpgradeManager.Instance?.UGM.CommandAmplifier ?? 1f, 1f, 2f);
  public int UnlockedCount { get; private set; }
  public ulong RunEarnings { get; private set; }
  public void UpdateUnlocks(ulong runEarnings)
  {
    RunEarnings = runEarnings;
    UnlockedCount = 0;
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
  public float MagnetStrength { get; private set; }
  public float MagnetElapsed { get; private set; }
  public int MagnetCast { get; private set; }
  public int SessionVersion { get; private set; }

  public bool TryActivate(int slot, Action<int> effect, GemReserve reserve = null)
  {
    if ((uint)slot >= Definitions.Length || !IsReady(slot)) return false;
    if (slot == ReserveBurstSlot)
    {
      if (UpgradeManager.Instance?.UG.GemReserveUnlocked != true || reserve == null
        || !reserve.TryBurst(ReserveBurstMultiplier, out ulong payout)) return false;
      ReserveBurstPayout = payout;
    }
    castDurations[slot] = Definitions[slot].Duration * (slot == OverdriveSlot ? Power * SignalBoost(SignalKind.CommandOverdriveDuration) : 1f);
    durations[slot] = castDurations[slot];
    cooldowns[slot] = Definitions[slot].Cooldown;
    if (slot == AbilitySurgeSlot) surgeMultiplier = AbilitySurgeMultiplier;
    if (slot == MagnetizerSlot)
    {
      ++MagnetCast;
      MagnetElapsed = 0f;
      MagnetStrength = Power * SignalBoost(SignalKind.CommandMagnetStrength);
    }
    effect(slot);
    return true;
  }

  public void Update(float seconds)
  {
    seconds = Math.Max(0f, seconds);
    // Integrate only the portion of this frame inside the Surge window.
    AutomaticCooldownAdvanceMilliseconds = 1000d * (seconds
      + Math.Min(durations[AbilitySurgeSlot], seconds) * (surgeMultiplier - 1f));
    MagnetElapsed += Math.Min(durations[MagnetizerSlot], seconds);
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
    MagnetElapsed = MagnetStrength = 0f;
    ReserveBurstPayout = 0;
    surgeMultiplier = 1f;
    AutomaticCooldownAdvanceMilliseconds = 0;
    MagnetCast = 0;
    ++SessionVersion;
  }
}
