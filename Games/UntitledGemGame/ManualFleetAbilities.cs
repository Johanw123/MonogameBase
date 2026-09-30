using System;

namespace UntitledGemGame;

// These commands are independent of the upgradeable, automatically cast abilities.
// Like other running ability effects, their timers belong to the current session.
public sealed class ManualFleetAbilities
{
  public sealed record Definition(string Name, string Effect, float Duration, float Cooldown);
  public static readonly Definition[] Definitions =
  [
    new("Overdrive", "2x speed / no fuel use", 10f, 45f),
    new("Wide Sweep", "2x fleet pickup radius", 12f, 60f),
    new("Double Yield", "2x fleet delivery value", 15f, 90f),
    new("Gem Burst", "Spawn 80 gems near home", 0f, 30f),
    new("Emergency Refuel", "Refill and restart fleet", 0f, 75f)
  ];
  private readonly float[] durations = new float[5];
  private readonly float[] cooldowns = new float[5];
  public bool IsActive(int slot) => durations[slot] > 0f;
  public bool IsReady(int slot) => cooldowns[slot] <= 0f && !IsActive(slot);
  public float RemainingDuration(int slot) => durations[slot];
  public float RemainingCooldown(int slot) => cooldowns[slot];
  public float SpeedMultiplier => IsActive(0) ? 2f : 1f;
  public bool FreeFuel => IsActive(0);
  public float RangeMultiplier => IsActive(1) ? 2f : 1f;
  public float DeliveryMultiplier => IsActive(2) ? 2f : 1f;

  public bool TryActivate(int slot, Action<int> instantEffect)
  {
    if ((uint)slot >= Definitions.Length || !IsReady(slot)) return false;
    durations[slot] = Definitions[slot].Duration;
    cooldowns[slot] = Definitions[slot].Cooldown;
    if (Definitions[slot].Duration == 0f) instantEffect(slot);
    return true;
  }

  public void Update(float seconds)
  {
    for (int i = 0; i < Definitions.Length; i++)
    {
      durations[i] = Math.Max(0f, durations[i] - seconds);
      cooldowns[i] = Math.Max(0f, cooldowns[i] - seconds);
    }
  }

  public void Reset()
  {
    Array.Clear(durations);
    Array.Clear(cooldowns);
  }
}
