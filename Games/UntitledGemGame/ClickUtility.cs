using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// One activation per mouse gesture. Only nearby loose gems are queried; effects
// keep positions rather than entity references, so recycling cannot move a link.
// The effects are drawn by ModuleEffects.fx: a pulse where the click lands, and a spark
// that hops along the Gem Link from gem to gem, each one flaring as it is reached.
public sealed class ClickUtility
{
  public const int MaxCombo = 10;
  public const float ShockwaveRadius = 90f;
  private const int MaxEffects = 128;
  private const int MaxCandidatesPerSearch = 4096;
  private const float PulseSeconds = 0.4f, LinkSeconds = 0.5f;
  // Clicks come several times a second, so their rings stay thinner and dimmer than a module's.
  public const float PulseWidth = 0.6f, PulseIntensity = 0.65f;
  // The part of a link's life its spark takes to cross it (LinkPS in ModuleEffects.fx).
  private const float LinkCrossing = 0.22f;
  private float flashSeed;
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
    // Age is below zero while a link waits for the spark to reach it.
    public float Age, Life, Radius, Seed;
    public bool Burst;
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
      if (flash.Age >= flash.Life) flashes.RemoveAt(i);
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
    float clickRadius = 0,
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
    if (seed < 0) return false;
    Vector2 origin = new(grid.Gems[seed].X, grid.Gems[seed].Y);
    int nextCombo = Math.Min(MaxCombo, Combo + 1);
    bool critical = criticalRoll < Math.Clamp(upgrades.ClickCriticalChance, 0f, 1f);
    double multiplier = Math.Max(1, SignalStats.Scale(SignalKind.ClickValue,
      upgrades.ClickValueMultiplier * (meta?.ClickValueMultiplier ?? 1), signals))
      * CoreShards.ClickValueMultiplier(upgrades) * PrestigeTalentEffects.HandValueMultiplier
      * (1 + (nextCombo - 1) * Math.Max(0, upgrades.ClickComboBonus)) * (critical ? 3 : 1);
    bool success = false;
    foreach (int index in direct) success |= collect(index, multiplier);
    if (!success) return false;

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
        AddFlash(origin, origin, Color.Orange, radius, burst: true);
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
    for (int i = 0; i < Math.Clamp(upgrades.ClickChainCount, 0, 96); ++i)
    {
      int target = FindNearest(grid, previous, Math.Max(0, SignalStats.Scale(SignalKind.ClickChainRange, upgrades.ClickChainRange, signals)));
      if (target < 0) break;
      Vector2 position = new(grid.Gems[target].X, grid.Gems[target].Y);
      if (!collect(target, multiplier)) break;
      AddFlash(previous, position, color, delay: HopDelay(i));
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

  // When the spark reaches each hop: quick at first, and a long chain still finishes in a
  // third of a second.
  public static float HopDelay(int hop) => 0.32f * (1f - MathF.Exp(-hop * 0.07f));

  private void AddFlash(Vector2 start, Vector2 end, Color color, float radius = 0, float delay = 0, bool burst = false)
  {
    if (flashes.Count == MaxEffects) flashes.RemoveAt(0);
    flashSeed = (flashSeed + 0.137f) % 1f;
    flashes.Add(new Flash { Start = start, End = end, Color = color, Radius = radius, Age = -delay,
      Life = radius > 0 ? PulseSeconds : LinkSeconds, Seed = flashSeed, Burst = burst });
  }

  // Queues the effects; the caller draws the batch in world space. Zoom is the camera's:
  // pulled far out, the links are drawn wider so they stay about a pixel thick.
  public void Draw(ModuleEffectBatch effects, float zoom)
  {
    float pixel = RenderQuality.WorldScale / Math.Max(0.01f, zoom);
    float halfWidth = Math.Max(0.9f, pixel);
    float flare = Math.Max(1f, pixel * 0.8f);
    foreach (var flash in flashes)
    {
      if (flash.Age < 0f) continue;
      float progress = flash.Age / flash.Life;
      if (flash.Radius > 0)
      {
        effects.Ring(flash.Start, flash.Radius, progress, flash.Color,
          flash.Burst ? ModuleEffectBatch.RingStyle.Burst : ModuleEffectBatch.RingStyle.Shock, flash.Seed,
          PulseIntensity, PulseWidth);
        continue;
      }
      effects.Link(flash.Start, flash.End, progress, flash.Color, halfWidth, flash.Seed);
      // The gem flares as the spark lands on it.
      float landed = (progress - LinkCrossing) / (1f - LinkCrossing);
      if (landed <= 0f) continue;
      float fade = 1f - landed;
      effects.Glint(flash.End, MathHelper.Lerp(9f, 15f, fade) * flare, flash.Color, flash.Seed, fade * fade,
        heading: flash.End - flash.Start);
    }
  }
}
