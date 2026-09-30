using System;

namespace UntitledGemGame;

// Commands are independent of automatically cast abilities. Running effects are session-only.
public sealed class ManualFleetAbilities
{
  public sealed record Definition(string Name, string Effect, float Duration, float Cooldown);
  public static readonly Definition[] Definitions =
  [
    new("Overdrive", "2x speed / no fuel use", 10f, 45f),
    new("Homebase Magnetizer", "Pull the gem field towards home", 4f, 60f),
    new("Cash Out", "Beam cargo home with a bonus", 0f, 90f),
    new("Crystal Shatter", "Click the crystal to burst gems", 0f, 30f),
    new("Collector Swarm", "Launch 8 fleet-powered drones", 0f, 75f)
  ];
  public const int CrystalShardCount = 24;
  public const int CollectorCount = 8;
  public float CrystalShardValueMultiplier(ulong fleetCapacity)
    => (float)(Math.Max(192.0, fleetCapacity * 2.0) / CrystalShardCount * Power);

  private readonly float[] durations = new float[5];
  private readonly float[] castDurations = new float[5];
  private readonly float[] cooldowns = new float[5];
  public float Power => Math.Clamp(UpgradeManager.Instance?.UGM.CommandAmplifier ?? 1f, 1f, 2f);
  public bool IsActive(int slot) => durations[slot] > 0f;
  public bool IsReady(int slot) => cooldowns[slot] <= 0f && !IsActive(slot);
  public float RemainingDuration(int slot) => durations[slot];
  public float CastDuration(int slot) => castDurations[slot];
  public float RemainingCooldown(int slot) => cooldowns[slot];
  public float SpeedMultiplier => IsActive(0) ? 2f : 1f;
  public bool FreeFuel => IsActive(0);
  public float CashOutMultiplier => 1f + 0.5f * Power;
  public float MagnetStrength { get; private set; }
  public float MagnetElapsed { get; private set; }
  public int MagnetCast { get; private set; }
  public int SessionVersion { get; private set; }

  public bool TryActivate(int slot, Action<int> effect)
  {
    if ((uint)slot >= Definitions.Length || !IsReady(slot)) return false;
    castDurations[slot] = Definitions[slot].Duration * (slot == 0 ? Power : 1f);
    durations[slot] = castDurations[slot];
    cooldowns[slot] = Definitions[slot].Cooldown;
    if (slot == 1)
    {
      ++MagnetCast;
      MagnetElapsed = 0f;
      MagnetStrength = Power;
    }
    effect(slot);
    return true;
  }

  public void Update(float seconds)
  {
    seconds = Math.Max(0f, seconds);
    MagnetElapsed += Math.Min(durations[1], seconds);
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
    MagnetElapsed = MagnetStrength = 0f;
    MagnetCast = 0;
    ++SessionVersion;
  }
}
