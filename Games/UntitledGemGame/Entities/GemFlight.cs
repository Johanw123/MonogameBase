using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

// A new gem's launch glide and grow-in are drawn by the gem shader (GemShader.fx) from the
// values below, so even a burst of tens of thousands costs nothing per frame. For every
// other system the gem already sits on its landing point at full size. Anything that moves
// it while it still looks in flight calls TakeOverGlide (the CPU takes the position, the
// shader keeps growing it); anything that claims it calls TakeOverFlight (both on the CPU,
// via UpdateSpawnMotion and SetAnimation). Either way it continues from where it appears.
//
// Collection is drawn the same way (BeginGpuCollect): the gem bursts away from its collector,
// homes in on it and shrinks, following the collector's live position (GemCollectors). The
// CPU only delivers clicked gems on arrival and retires the gem once it has vanished
// (UpdateSystem2). Without a free collector slot the CPU animation (DoAnim) runs instead.
// Chain pulls (BeginGpuPull) and core fracture swallows (GemSwallow.cs) work alike.
public partial class Gem
{
  // Seconds of simulation, advanced by UpdateSystem2; the shader reads the same clock.
  public static double FlightClock;
  // How fast a new gem grows to full size: the CPU animation's per-frame lerp (5 * dt).
  internal const float GrowRate = 5f;
  // After this long both curves are within a fraction of a percent of the end.
  private const float FlightSettleSeconds = 1.5f;

  // FlightClock at spawn, or -1 once the CPU owns the gem's motion.
  internal float FlightStart = -1f;
  // Where the glide starts (the landing point itself for an ordinary spawn).
  internal Vector2 FlightFrom;
  // The starting scale as a share of the full scale.
  internal float FlightGrowFrom = 1f;
  // The CPU owns the position (something moved the gem); the shader still grows it in.
  internal bool FlightGlideTaken;

  internal static bool DrawsFlights => RenderGemSystem.Instance?.DrawsGpuCollection == true;
  internal bool InFlight => FlightStart >= 0f && FlightClock - FlightStart < FlightSettleSeconds;

  // Spawned at the current position, growing in from the CPU animation's old start size.
  private void BeginFlight()
  {
    FlightGlideTaken = false;
    FlightStart = (float)FlightClock;
    FlightFrom = m_transform.Position;
    FlightGrowFrom = Math.Clamp(0.1f / Math.Max(0.001f, OrigScale.X), 0f, 1f);
  }

  // A launch: the gem lands on its point at once and the shader draws the glide there.
  internal void LaunchTo(Vector2 landing)
  {
    m_transform.Position = landing;
    m_targetPosition = landing;
    SetCollisionPosition(landing);
  }

  // A chain pull drawn by the shader (Graviton Cascade, ChainLightningAbility): a quintic
  // ease-out from the gem's position to the chain's target. The CPU keeps the gem at the start
  // until the chain completes, or until TakeOverPull places it where it appears.
  internal float PullStart = -1f;
  internal Vector2 PullFrom, PullTo;
  internal float PullDuration;
  internal bool PullingOnGpu => PullStart >= 0f;
  // Marks a GPU pull in the quad's Timing.y: PullTimingBase + duration.
  internal const float PullTimingBase = 2f;

  internal static Vector2 PullPosition(Vector2 from, Vector2 to, float progress)
  {
    float inverse = 1f - Math.Clamp(progress, 0f, 1f);
    return Vector2.Lerp(from, to, 1f - inverse * inverse * inverse * inverse * inverse);
  }

  // A gem still settling is settled where it appears, at full size: the chain's yank hides
  // the last of its grow-in. Call before reading its position for the pull's start.
  internal void SettleForPull()
  {
    if (!InFlight)
    {
      FlightStart = -1f;
      return;
    }
    if (!FlightGlideTaken) PlaceAtGlide((float)(FlightClock - FlightStart));
    FlightStart = -1f;
    FlightGlideTaken = false;
    LaunchVelocity = Vector2.Zero;
    m_animating = false;
    m_transform.Scale = OrigScale;
    RenderGemSystem.Instance?.UpdateGem(Id);
  }

  internal bool BeginGpuPull(Vector2 to, float delay, float duration)
  {
    if (RenderGemSystem.Instance?.DrawsGpuCollection != true || InFlight || !IsLive) return false;
    FlightStart = -1f;
    PullStart = (float)(FlightClock + delay);
    PullFrom = m_transform.Position;
    PullTo = to;
    PullDuration = Math.Max(duration, 0.001f);
    RenderGemSystem.Instance.UpdateGem(Id);
    return true;
  }

  // The chain completed: the gem now sits on the target.
  internal void EndGpuPull()
  {
    if (PullStart < 0f) return;
    PullStart = -1f;
    MoveByChain(PullTo);
  }

  internal void TakeOverPull()
  {
    if (PullStart < 0f) return;
    var position = PullPosition(PullFrom, PullTo, (float)(FlightClock - PullStart) / PullDuration);
    PullStart = -1f;
    MoveByChain(position);
  }

  // Something moves the gem (magnets, chains, gravity): the CPU takes the glide over from
  // where the gem appears, while the shader keeps growing it in around its CPU position.
  internal void TakeOverGlide()
  {
    TakeOverPull();
    if (FlightStart < 0f || FlightGlideTaken) return;
    float age = (float)(FlightClock - FlightStart);
    if (age >= FlightSettleSeconds)
    {
      FlightStart = -1f;
      return;
    }
    FlightGlideTaken = true;
    PlaceAtGlide(age);
    RenderGemSystem.Instance?.UpdateGem(Id);
    Wake();
  }

  // Everything back on the CPU (collection, clicks, a swallow): position and size.
  internal void TakeOverFlight()
  {
    TakeOverPull();
    if (FlightStart < 0f) return;
    float age = (float)(FlightClock - FlightStart);
    FlightStart = -1f;
    if (age >= FlightSettleSeconds) return;
    float grow = 1f - (1f - FlightGrowFrom) * MathF.Exp(-GrowRate * age);
    var position = FlightGlideTaken ? m_transform.Position : PlaceAtGlide(age);
    FlightGlideTaken = false;
    m_targetPosition = position;
    if (grow < 0.999f)
    {
      m_transform.Scale = OrigScale * grow;
      m_targetScale = OrigScale;
      m_animationSpeedScale = GrowRate;
      m_animating = true;
    }
    RenderGemSystem.Instance?.UpdateGem(Id);
    Wake();
  }

  // Moves the gem to where its glide has got to; the rest of the glide stays on the CPU.
  private Vector2 PlaceAtGlide(float age)
  {
    float glide = MathF.Exp(-LaunchDamping * age);
    var landing = m_transform.Position;
    var position = landing - (landing - FlightFrom) * glide;
    if (position != landing)
    {
      m_transform.Position = position;
      SetCollisionPosition(position);
      if ((uint)GridIndex < (uint)HarvesterCollectionSystem.Instance.flatSpatialHash.MaxCapacity)
        HarvesterCollectionSystem.Instance.flatSpatialHash.MoveGem(GridIndex, position.X, position.Y);
      // The rest of the glide still ends on the landing point.
      LaunchVelocity = (landing - FlightFrom) * glide * LaunchDamping;
      PositionMoved = true;
    }
    m_targetPosition = position;
    return position;
  }

  // The collection curve, matching DoAnim: a burst away at BurstSpeed, then acceleration
  // toward the collector up to MaxCollectSpeed. Shrinks like SetAnimation's scale (GrowRate).
  private const float BurstSpeed = 300f, HomingAcceleration = 2500f, MaxCollectSpeed = 800f;
  private const float TopSpeedTime = (MaxCollectSpeed + BurstSpeed) / HomingAcceleration;
  private const float TopSpeedDistance = -BurstSpeed * TopSpeedTime + HomingAcceleration * TopSpeedTime * TopSpeedTime / 2f;
  // By then the gem has shrunk out of sight (the CPU animation's own end).
  private const float CollectVanishSeconds = 0.92f;

  // FlightClock when a GPU collection began, or -1.
  internal float CollectStart = -1f;
  internal Vector2 CollectFrom;
  internal int CollectSlot;
  internal float CollectDistance;
  internal bool CollectingOnGpu => CollectStart >= 0f;

  // How long the curve takes to cover a distance toward the collector.
  internal static float CollectTime(float distance)
  {
    if (distance <= 0f) return 0f;
    if (distance <= TopSpeedDistance)
      return (BurstSpeed + MathF.Sqrt(BurstSpeed * BurstSpeed + 2f * HomingAcceleration * distance)) / HomingAcceleration;
    return TopSpeedTime + (distance - TopSpeedDistance) / MaxCollectSpeed;
  }

  // Starts a collection drawn by the shader toward a collector. Returns false when no
  // collector slot is free (or no GPU renderer runs), leaving the CPU animation to the caller.
  // deliverReach: for clicked gems, how far from the collector they are delivered.
  internal bool BeginGpuCollect(Transform2 collector, float deliverReach, out double deliverAt, out double retireAt)
  {
    deliverAt = retireAt = 0;
    if (RenderGemSystem.Instance?.DrawsGpuCollection != true || m_transform == null) return false;
    int slot = GemCollectors.SlotFor(collector);
    if (slot < 0) return false;
    float distance = Vector2.Distance(m_transform.Position, collector.Position);
    float arrive = CollectTime(distance);
    CollectStart = (float)FlightClock;
    CollectFrom = m_transform.Position;
    CollectSlot = slot;
    CollectDistance = Math.Max(distance, 0.001f);
    deliverAt = FlightClock + CollectTime(distance - deliverReach);
    // A gem reaching its collector disappears into it; a far one has shrunk away first.
    retireAt = FlightClock + Math.Max(CollectTime(distance - deliverReach), Math.Min(arrive, CollectVanishSeconds));
    GemCollectors.Use(slot, retireAt);
    // Nothing is left for the CPU to animate: the gem can sleep until it is retired.
    m_animating = false;
    m_targetHarvester = null;
    LaunchVelocity = Vector2.Zero;
    RenderGemSystem.Instance?.UpdateGem(Id);
    return true;
  }
}

// Collectors that gems are flying into on the GPU: the home base and fleet ships. Their
// positions go to the gem shader each frame; a slot is reused once no gem targets it.
public static class GemCollectors
{
  public const int Slots = 128;
  private static readonly Transform2[] collectors = new Transform2[Slots];
  private static readonly double[] busyUntil = new double[Slots];
  private static readonly Dictionary<Transform2, int> slots = new(ReferenceEqualityComparer.Instance);
  private static readonly Vector4[] positions = new Vector4[Slots];

  public static int SlotFor(Transform2 collector)
  {
    if (slots.TryGetValue(collector, out int slot)) return slot;
    for (int i = 0; i < Slots; i++)
    {
      if (collectors[i] != null && busyUntil[i] > Gem.FlightClock) continue;
      if (collectors[i] != null) slots.Remove(collectors[i]);
      collectors[i] = collector;
      slots[collector] = i;
      positions[i] = new Vector4(collector.Position, 0f, 0f);
      return i;
    }
    return -1;
  }

  public static void Use(int slot, double until) => busyUntil[slot] = Math.Max(busyUntil[slot], until);

  // The collectors' current positions, for the shader.
  public static Vector4[] Positions()
  {
    for (int i = 0; i < Slots; i++)
      if (collectors[i] != null) positions[i] = new Vector4(collectors[i].Position, 0f, 0f);
    return positions;
  }

  public static void Clear()
  {
    Array.Clear(collectors);
    Array.Clear(busyUntil);
    slots.Clear();
  }
}