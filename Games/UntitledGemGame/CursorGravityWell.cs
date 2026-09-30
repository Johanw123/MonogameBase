using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// A local attraction field. Snapshot query results before movement changes the
// spatial index; never claim gems or disturb pickups reserved by harvesters.
public sealed class CursorGravityWell
{
  public const int FrameBudget = 8192;
  public const float BaseCooldown = 20f;
  public const float BaseStrength = 0.45f;
  private readonly int[] targets = new int[FrameBudget];
  private float remaining, duration, strength, cooldownLength, activation, denialRemaining;
  private bool activationPending;
  public Vector2 Position { get; private set; }
  public float Radius { get; private set; }
  public float CooldownRemaining { get; private set; }
  public bool IsActive => remaining > 0;
  public float LifeProgress => duration > 0 ? 1 - remaining / duration : 0;
  public float RechargeProgress => cooldownLength > 0 ? Math.Clamp(1 - CooldownRemaining / cooldownLength, 0, 1) : 1;
  public float ActivationGlow => activationPending ? 1 : Math.Clamp(activation / 0.16f, 0, 1);
  public bool ActivationPending => activationPending;
  public float DenialGlow => Math.Clamp(denialRemaining / 0.2f, 0, 1);
  public static float PreviewRadius(UpgradesGeneratorUpgrades upgrades, float clickRadius, SignalProgression signals = null)
    => clickRadius * SignalStats.Scale(SignalKind.CursorGravityRadius, upgrades.CursorGravityRadiusMultiplier, signals);
  public static float Cooldown(UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null)
    => Math.Max(1f, (float)(BaseCooldown / Math.Max(1, upgrades.CursorGravityFrequencyMultiplier)
      * (signals?.ReductionMultiplier(SignalKind.CursorGravityCooldown) ?? 1)
      * (signals?.CooldownMultiplier ?? 1)));

  public bool HandleInput(bool rightHeld, bool leftPressed, bool inputEnabled,
    Vector2 position, float clickRadius, UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null)
    => inputEnabled && rightHeld && leftPressed && TryActivate(position, clickRadius, upgrades, signals);

  public bool TryActivate(Vector2 position, float clickRadius, UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null)
  {
    if (!upgrades.CursorGravityEnabled) return false;
    if (CooldownRemaining > 0)
    {
      denialRemaining = 0.45f;
      return false;
    }
    denialRemaining = 0;
    Position = position;
    Radius = PreviewRadius(upgrades, clickRadius, signals);
    remaining = duration = SignalStats.Scale(SignalKind.CursorGravityDuration, upgrades.CursorGravityDuration, signals);
    strength = BaseStrength * SignalStats.Scale(SignalKind.CursorGravityStrength, upgrades.CursorGravityStrengthMultiplier, signals);
    CooldownRemaining = cooldownLength = Cooldown(upgrades, signals);
    activation = 0.16f;
    activationPending = true;
    return true;
  }

  public void AcknowledgeVisual() => activationPending = false;

  public void Update(float dt, GemSpatialIndex grid, Action<int, Vector2> move,
    Func<int, Vector2, float, bool> overlaps = null)
  {
    dt = Math.Max(0, dt);
    CooldownRemaining = Math.Max(0, CooldownRemaining - dt);
    activation = Math.Max(0, activation - dt);
    denialRemaining = Math.Max(0, denialRemaining - dt);
    float activeDt = Math.Min(dt, remaining);
    remaining = Math.Max(0, remaining - dt);
    if (activeDt <= 0) return;
    int count = 0, visited = 0;
    foreach (int index in grid.QueryClickCandidates(Position.X, Position.Y, Radius))
    {
      ref var gem = ref grid.Gems[index];
      bool inside = overlaps != null ? overlaps(index, Position, Radius)
        : Vector2.DistanceSquared(new Vector2(gem.X, gem.Y), Position) <= Radius * Radius;
      if (inside) targets[count++] = index;
      if (++visited >= FrameBudget) break;
    }
    float retention = MathF.Exp(-strength * activeDt);
    for (int i = 0; i < count; ++i)
    {
      int index = targets[i];
      ref var gem = ref grid.Gems[index];
      if (!gem.IsActive || gem.ClaimState != 0) continue;
      var position = new Vector2(gem.X, gem.Y);
      move(index, Position + (position - Position) * retention);
    }
  }

  public void Reset()
  {
    remaining = duration = strength = cooldownLength = activation = denialRemaining = CooldownRemaining = Radius = 0;
    activationPending = false;
    Position = Vector2.Zero;
  }
}
