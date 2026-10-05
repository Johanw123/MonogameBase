using System;
using System.Collections.Generic;
using Apos.Shapes;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// One activation per mouse gesture. Only nearby loose gems are queried; effects
// keep positions rather than entity references, so recycling cannot move a link.
public sealed class ClickUtility
{
  public const int MaxCombo = 10;
  public const float ShockwaveRadius = 90f;
  private const int MaxEffects = 128;
  private const int MaxCandidatesPerSearch = 4096;
  private readonly List<Flash> flashes = new(MaxEffects);
  private readonly List<int> extraTargets = new(32);
  public int SupernovaProgress { get; private set; }
  private float comboRemaining, passiveCredit;
  private double bonusRemainder;
  private float holdRemaining, heldSeconds, holdCycleInterval, holdVisualFill, holdActivationRemaining;
  private bool wasHolding, holdCompletionPending;
  public int Combo { get; private set; }
  public bool LastCritical { get; private set; }
  public double LastMultiplier { get; private set; } = 1;
  public float ComboRemaining => comboRemaining;
  public bool IsHolding => wasHolding;
  public float HoldProgress => wasHolding && holdCycleInterval > 0
    ? Math.Clamp(1f - holdRemaining / holdCycleInterval, 0, 1) : 0;
  public float HoldVisualFill => holdCompletionPending ? 1f : holdVisualFill;
  public bool HoldClickActivated => holdCompletionPending;
  public float HoldActivationGlow => wasHolding
    ? Math.Max(holdCompletionPending ? 1f : 0f, Math.Clamp(holdActivationRemaining / 0.04f, 0, 1)) : 0f;
  public void AcknowledgeHoldVisual() => holdCompletionPending = false;
  private struct Flash
  {
    public Vector2 Start, End;
    public Color Color;
    public float Age, Radius;
  }

  public static Matrix RenderView(Matrix cameraView, Matrix viewportScale)
    => cameraView * Matrix.Invert(viewportScale);

  public static Vector2 PointerToTarget(Vector2 windowPointer, Rectangle displayViewport, Vector2 targetSize)
    => (windowPointer - displayViewport.Location.ToVector2())
      * targetSize / new Vector2(displayViewport.Width, displayViewport.Height);

  public static float TargetRadius(float gemWidth, float gemHeight, float scale)
    => Math.Max(gemWidth, gemHeight) * 0.5f * scale;

  public static bool ContainsTarget(Vector2 pointer, Vector2 gem, float radius,
    Vector2 gemHalfSize = default, float gemRotation = 0)
  {
    // Circle versus the visible sprite bounds. Testing centers alone misses
    // gems whose edges overlap the cursor ring.
    var offset = pointer - gem;
    if (gemRotation != 0)
      offset = Vector2.Transform(offset, Matrix.CreateRotationZ(-gemRotation));
    var outside = new Vector2(Math.Max(0, Math.Abs(offset.X) - Math.Abs(gemHalfSize.X)),
      Math.Max(0, Math.Abs(offset.Y) - Math.Abs(gemHalfSize.Y)));
    return outside.LengthSquared() <= radius * radius;
  }

  public static float PassiveInterval(UpgradesGeneratorUpgrades upgrades)
    => BaseStats.PassiveIncomeInterval / Math.Max(1f, upgrades.PassiveIncomeFrequencyMultiplier);

  public static float HoldInterval(UpgradesGeneratorUpgrades upgrades, float heldSeconds = 0,
    SignalProgression signals = null, UpgradesGeneratorUpgrades_meta meta = null)
    => Math.Max(0.08f, 0.8f / (Math.Max(1f, SignalStats.Scale(SignalKind.HoldClickFrequency,
      upgrades.HoldClickFrequencyMultiplier, signals))
      * (1 + Math.Clamp(heldSeconds, 0, 5) * Math.Max(0, upgrades.HoldClickMomentum))));

  private void ResetHold()
  {
    wasHolding = holdCompletionPending = false;
    heldSeconds = holdRemaining = holdCycleInterval = holdVisualFill = holdActivationRemaining = 0;
  }

  public void CancelHold() => ResetHold();

  public bool ShouldClick(bool pressed, bool held, bool enabled, float dt, UpgradesGeneratorUpgrades upgrades,
    SignalProgression signals = null, UpgradesGeneratorUpgrades_meta meta = null)
  {
    if (!enabled || !held)
    {
      ResetHold();
      return enabled && pressed;
    }
    if (pressed)
    {
      ResetHold();
      wasHolding = true;
      holdRemaining = holdCycleInterval = HoldInterval(upgrades, signals: signals, meta: meta);
      holdCompletionPending = true;
      holdActivationRemaining = 0.04f;
      return true;
    }
    if (!upgrades.HoldClickEnabled) { ResetHold(); return false; }
    // Holding over a menu and moving into gameplay starts a fresh repeat delay.
    if (!wasHolding)
    {
      wasHolding = true;
      holdRemaining = holdCycleInterval = HoldInterval(upgrades, signals: signals, meta: meta);
      return false;
    }
    heldSeconds += Math.Max(0, dt);
    float interval = HoldInterval(upgrades, heldSeconds, signals, meta);
    holdRemaining = Math.Min(holdRemaining, interval) - Math.Max(0, dt);
    // Follow the actual cooldown directly so every repeat reaches the boundary.
    holdVisualFill = Math.Clamp(1f - holdRemaining / holdCycleInterval, 0, 1);
    if (holdRemaining > 0) return false;
    // At most one gesture per frame. Do not replay clicks accumulated during a hitch.
    holdRemaining = holdCycleInterval = interval;
    // Latch the completed disk until it has been drawn. Multiple fixed-timestep
    // updates before one draw must not skip the visual activation endpoint.
    holdCompletionPending = true;
    holdActivationRemaining = 0.04f;
    return true;
  }

  public ulong BonusValue(uint value, double multiplier)
  {
    double bonus = value * Math.Max(0, multiplier - 1) + bonusRemainder;
    if (bonus >= ulong.MaxValue) { bonusRemainder = 0; return ulong.MaxValue; }
    ulong whole = (ulong)bonus;
    bonusRemainder = bonus - whole;
    return whole;
  }

  public void Reset()
  {
    flashes.Clear();
    Combo = 0;
    comboRemaining = passiveCredit = 0;
    LastCritical = false;
    LastMultiplier = 1;
    bonusRemainder = 0;
    ResetHold();
    SupernovaProgress = 0;
  }

  public void Update(float dt)
  {
    holdActivationRemaining = Math.Max(0, holdActivationRemaining - dt);
    comboRemaining = Math.Max(0, comboRemaining - dt);
    if (comboRemaining == 0) { Combo = 0; SupernovaProgress = 0; }
    for (int i = flashes.Count - 1; i >= 0; --i)
    {
      var flash = flashes[i];
      flash.Age += dt;
      if (flash.Age >= 0.4f) flashes.RemoveAt(i);
      else flashes[i] = flash;
    }
  }

  public float TakePassiveCredit()
  {
    float credit = passiveCredit;
    passiveCredit = 0;
    return credit;
  }

  public bool Activate(GemSpatialIndex grid, IReadOnlyList<int> direct, Vector2 mouse,
    UpgradesGeneratorUpgrades upgrades, Func<int, double, bool> collect, double criticalRoll,
    SignalProgression signals = null, UpgradesGeneratorUpgrades_meta meta = null,
    Vector2? mirrorCenter = null, float clickRadius = 0,
    Func<int, Vector2, float, bool> overlaps = null)
  {
    int seed = -1;
    float nearest = float.MaxValue;
    foreach (int index in direct)
    {
      ref var gem = ref grid.Gems[index];
      if (!gem.IsActive || gem.ClaimState != 0) continue;
      float distance = Vector2.DistanceSquared(mouse, new Vector2(gem.X, gem.Y));
      if (distance < nearest) { nearest = distance; seed = index; }
    }
    Vector2 mirror = mirrorCenter.GetValueOrDefault() * 2 - mouse;
    extraTargets.Clear();
    if (meta?.QuantumTouch == true && mirrorCenter.HasValue && clickRadius > 0)
      GatherTargets(grid, mirror, clickRadius, overlaps);
    if (seed < 0 && extraTargets.Count == 0) return false;
    Vector2 origin = seed >= 0 ? new(grid.Gems[seed].X, grid.Gems[seed].Y) : mouse;
    int nextCombo = Math.Min(MaxCombo, Combo + 1);
    bool critical = criticalRoll < Math.Clamp(upgrades.ClickCriticalChance, 0f, 1f);
    double multiplier = Math.Max(1, SignalStats.Scale(SignalKind.ClickValue,
      upgrades.ClickValueMultiplier * (meta?.ClickValueMultiplier ?? 1), signals))
      * CoreShards.ClickValueMultiplier(upgrades)
      * (1 + (nextCombo - 1) * Math.Max(0, upgrades.ClickComboBonus)) * (critical ? 3 : 1);
    bool success = false;
    foreach (int index in direct) success |= collect(index, multiplier);
    bool mirrorSuccess = false;
    foreach (int index in extraTargets) mirrorSuccess |= collect(index, multiplier);
    success |= mirrorSuccess;
    if (!success) return false;
    if (mirrorSuccess)
    {
      AddFlash(mouse, mirror, Color.Violet);
      AddFlash(mirror, mirror, Color.Violet, clickRadius);
    }

    Combo = nextCombo;
    comboRemaining = Math.Max(0.1f, SignalStats.Scale(SignalKind.ClickComboWindow, upgrades.ClickComboWindow, signals));
    LastCritical = critical;
    LastMultiplier = multiplier;
    if (upgrades.PassiveIncome > 0) passiveCredit += Math.Max(0, upgrades.ClickPassiveSeconds);
    Color color = critical ? Color.Gold : Color.Aquamarine;
    AddFlash(origin, origin, color, upgrades.ClickShockwaveCount > 0 ? ShockwaveRadius : 18f);

    if (meta?.ClickComboSupernova == true)
    {
      SupernovaProgress = (SupernovaProgress + 1) % 5;
      if (SupernovaProgress == 0)
      {
        float radius = Math.Max(180, clickRadius * 2);
        GatherTargets(grid, origin, radius, overlaps);
        foreach (int index in extraTargets) collect(index, multiplier * 3);
        AddFlash(origin, origin, Color.Orange, radius);
      }
    }

    // Finish each query before collecting: removal changes the index's linked lists.
    for (int i = 0; i < Math.Clamp(upgrades.ClickShockwaveCount, 0, 32); ++i)
    {
      int target = FindNearest(grid, origin, ShockwaveRadius);
      if (target < 0 || !collect(target, multiplier)) break;
      AddFlash(origin, new Vector2(grid.Gems[target].X, grid.Gems[target].Y), color);
    }
    Vector2 previous = origin;
    for (int i = 0; i < Math.Clamp(upgrades.ClickChainCount, 0, 64); ++i)
    {
      int target = FindNearest(grid, previous, Math.Max(0, SignalStats.Scale(SignalKind.ClickChainRange, upgrades.ClickChainRange, signals)));
      if (target < 0) break;
      Vector2 position = new(grid.Gems[target].X, grid.Gems[target].Y);
      if (!collect(target, multiplier)) break;
      AddFlash(previous, position, color);
      previous = position;
    }
    return true;
  }

  private void GatherTargets(GemSpatialIndex grid, Vector2 center, float radius,
    Func<int, Vector2, float, bool> overlaps)
  {
    extraTargets.Clear();
    int visited = 0;
    foreach (int index in grid.QueryClickCandidates(center.X, center.Y, radius))
    {
      ref var gem = ref grid.Gems[index];
      if (gem.IsActive && gem.ClaimState == 0 && (overlaps?.Invoke(index, center, radius)
        ?? Vector2.DistanceSquared(center, new Vector2(gem.X, gem.Y)) <= radius * radius))
        extraTargets.Add(index);
      if (extraTargets.Count == 32 || ++visited >= MaxCandidatesPerSearch) break;
    }
  }

  private static int FindNearest(GemSpatialIndex grid, Vector2 origin, float radius)
  {
    int best = -1;
    float distance = radius * radius;
    int visited = 0;
    foreach (int index in grid.Query(origin.X, origin.Y, radius, radius))
    {
      ref var gem = ref grid.Gems[index];
      float candidate = Vector2.DistanceSquared(origin, new Vector2(gem.X, gem.Y));
      if (candidate <= distance) { distance = candidate; best = index; }
      // Dense piles must not turn one gesture into dozens of full-field scans.
      if (++visited >= MaxCandidatesPerSearch) break;
    }
    return best;
  }

  private void AddFlash(Vector2 start, Vector2 end, Color color, float radius = 0)
  {
    if (flashes.Count == MaxEffects) flashes.RemoveAt(0);
    flashes.Add(new Flash { Start = start, End = end, Color = color, Radius = radius });
  }

  // Caller owns the additive, world-space shape batch.
  public void Draw(ShapeBatch batch, float zoom)
  {
    float feather = 1f / Math.Max(0.1f, zoom);
    foreach (var flash in flashes)
    {
      float progress = flash.Age / 0.4f;
      var color = new Color(flash.Color.R, flash.Color.G, flash.Color.B, (byte)(255 * (1 - progress)));
      if (flash.Radius > 0)
      {
        float radius = MathHelper.Lerp(5f, flash.Radius, progress);
        const int segments = 32;
        Vector2 previous = flash.Start + new Vector2(radius, 0);
        for (int i = 1; i <= segments; ++i)
        {
          float angle = i * MathHelper.TwoPi / segments;
          Vector2 next = flash.Start + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
          batch.FillLine(previous, next, 1.5f, color, feather);
          previous = next;
        }
      }
      else
      {
        Vector2 direction = flash.End - flash.Start;
        Vector2 side = direction.LengthSquared() > 0 ? Vector2.Normalize(new Vector2(-direction.Y, direction.X)) : Vector2.Zero;
        Vector2 previous = flash.Start;
        for (int i = 1; i <= 6; ++i)
        {
          Vector2 next = Vector2.Lerp(flash.Start, flash.End, i / 6f)
            + (i < 6 ? side * MathF.Sin(i * 8f + flash.Age * 20f) * 4f : Vector2.Zero);
          batch.FillLine(previous, next, 4f, color * 0.3f, feather * 2f);
          batch.FillLine(previous, next, 1.2f, color, feather);
          previous = next;
        }
        batch.FillCircle(flash.End, 4f, color, feather);
      }
    }
  }
}
