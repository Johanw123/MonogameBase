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
  private const float ReadyFlashDuration = 0.65f;
  private float readyRemaining;
  private bool readyNotificationPending, readyVisualPending;
  private bool eventHorizon, mobile, collapse, collapsePending;
  private double collectionMultiplier;
  private float collapseGlow;
  public float CollapseGlow => Math.Clamp(collapseGlow / 0.5f, 0, 1);
  public bool HasEventHorizon => IsActive && eventHorizon;
  public float CoreRadius => Math.Max(8, Radius * 0.25f);
  public bool TakeCollapseNotification()
  {
    bool pending = collapsePending;
    collapsePending = false;
    return pending;
  }
  public void FollowPointer(Vector2 position, bool aiming)
  {
    if (aiming && IsActive && mobile) Position = position;
  }
  public Vector2 Position { get; private set; }
  public float Radius { get; private set; }
  public float CooldownRemaining { get; private set; }
  public bool IsActive => remaining > 0;
  public float LifeProgress => duration > 0 ? 1 - remaining / duration : 0;
  public float RechargeProgress => cooldownLength > 0 ? Math.Clamp(1 - CooldownRemaining / cooldownLength, 0, 1) : 1;
  public float ActivationGlow => activationPending ? 1 : Math.Clamp(activation / 0.16f, 0, 1);
  public bool ActivationPending => activationPending;
  public float DenialGlow => Math.Clamp(denialRemaining / 0.2f, 0, 1);
  public float ReadyGlow => readyVisualPending ? 1 : Math.Clamp(readyRemaining / ReadyFlashDuration, 0, 1);

  public bool TakeReadyNotification()
  {
    bool pending = readyNotificationPending;
    readyNotificationPending = false;
    return pending;
  }
  public static float PreviewRadius(UpgradesGeneratorUpgrades upgrades, float clickRadius, SignalProgression signals = null)
    => clickRadius * SignalStats.Scale(SignalKind.CursorGravityRadius, upgrades.CursorGravityRadiusMultiplier, signals);
  public static float Cooldown(UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null,
    UpgradesGeneratorUpgrades_meta meta = null)
    => Math.Max(1f, (float)(BaseCooldown / Math.Max(1, upgrades.CursorGravityFrequencyMultiplier)
      / Math.Max(1, meta?.AllAbilityCooldown ?? 1)
      * (signals?.ReductionMultiplier(SignalKind.CursorGravityCooldown) ?? 1)
      * (signals?.CooldownMultiplier ?? 1)));

  public bool HandleInput(bool rightHeld, bool leftPressed, bool inputEnabled,
    Vector2 position, float clickRadius, UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null,
    UpgradesGeneratorUpgrades_meta meta = null)
  {
    FollowPointer(position, inputEnabled && rightHeld);
    return inputEnabled && rightHeld && leftPressed && TryActivate(position, clickRadius, upgrades, signals, meta);
  }

  public bool TryActivate(Vector2 position, float clickRadius, UpgradesGeneratorUpgrades upgrades, SignalProgression signals = null,
    UpgradesGeneratorUpgrades_meta meta = null)
  {
    if (!upgrades.CursorGravityEnabled) return false;
    if (CooldownRemaining > 0)
    {
      denialRemaining = 0.45f;
      return false;
    }
    denialRemaining = 0;
    readyRemaining = 0;
    readyNotificationPending = readyVisualPending = false;
    Position = position;
    Radius = PreviewRadius(upgrades, clickRadius, signals);
    remaining = duration = SignalStats.Scale(SignalKind.CursorGravityDuration, upgrades.CursorGravityDuration, signals);
    strength = BaseStrength * SignalStats.Scale(SignalKind.CursorGravityStrength,
      upgrades.CursorGravityStrengthMultiplier, signals);
    mobile = meta?.CursorGravityMobile == true;
    eventHorizon = mobile || meta?.CursorGravityEventHorizon == true;
    collapse = meta?.CursorGravityCollapse == true;
    collectionMultiplier = Math.Max(1, SignalStats.Scale(SignalKind.ClickValue,
      upgrades.ClickValueMultiplier * (meta?.ClickValueMultiplier ?? 1), signals))
      * CoreShards.ClickValueMultiplier(upgrades);
    collapseGlow = 0;
    collapsePending = false;
    CooldownRemaining = cooldownLength = Cooldown(upgrades, signals, meta);
    activation = 0.16f;
    activationPending = true;
    return true;
  }

  public void AcknowledgeVisual() => activationPending = false;

  public void AcknowledgeReadyVisual()
  {
    if (readyVisualPending) readyRemaining = ReadyFlashDuration;
    readyVisualPending = false;
  }

  public void Update(float dt, GemSpatialIndex grid, Action<int, Vector2> move,
    Func<int, Vector2, float, bool> overlaps = null, Func<int, double, bool> collect = null)
  {
    dt = Math.Max(0, dt);
    collapseGlow = Math.Max(0, collapseGlow - dt);
    readyRemaining = Math.Max(0, readyRemaining - dt);
    bool wasCoolingDown = CooldownRemaining > 0;
    CooldownRemaining = Math.Max(0, CooldownRemaining - dt);
    if (wasCoolingDown && CooldownRemaining == 0)
    {
      readyNotificationPending = readyVisualPending = true;
      readyRemaining = ReadyFlashDuration;
    }
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
    int harvested = 0;
    for (int i = 0; i < count; ++i)
    {
      int index = targets[i];
      ref var gem = ref grid.Gems[index];
      if (!gem.IsActive || gem.ClaimState != 0) continue;
      var position = new Vector2(gem.X, gem.Y);
      var moved = Position + (position - Position) * retention;
      move(index, moved);
      if (eventHorizon && harvested < 16 && collect != null
        && Vector2.DistanceSquared(moved, Position) <= CoreRadius * CoreRadius
        && collect(index, collectionMultiplier * 2)) harvested++;
    }
    if (remaining == 0 && collapse)
    {
      collapseGlow = 0.5f;
      collapsePending = true;
      // Finish the query before collection removes gems from the index.
      count = visited = 0;
      foreach (int index in grid.QueryClickCandidates(Position.X, Position.Y, Radius))
      {
        ref var gem = ref grid.Gems[index];
        if (gem.IsActive && gem.ClaimState == 0 && (overlaps?.Invoke(index, Position, Radius)
          ?? Vector2.DistanceSquared(new Vector2(gem.X, gem.Y), Position) <= Radius * Radius))
          targets[count++] = index;
        if (++visited >= FrameBudget) break;
      }
      for (int i = 0; i < count && harvested < 128; i++)
        if (collect?.Invoke(targets[i], collectionMultiplier * 3) == true) harvested++;
    }
  }

  public void Reset()
  {
    remaining = duration = strength = cooldownLength = activation = denialRemaining = CooldownRemaining = Radius = 0;
    activationPending = false;
    readyRemaining = 0;
    readyNotificationPending = readyVisualPending = false;
    Position = Vector2.Zero;
    eventHorizon = mobile = collapse = collapsePending = false;
    collapseGlow = 0;
    collectionMultiplier = 1;
  }
}
