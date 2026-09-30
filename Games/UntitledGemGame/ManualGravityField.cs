using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Main-thread, bounded work over stable slots in the existing gem index. No queries or population wake-up.
public sealed class ManualGravityField
{
  public const int FrameBudget = 8192;
  private readonly int[] casts, versions;
  private readonly int[] smallBatch = new int[FrameBudget];
  private readonly float[] elapsed;
  private int cursor, cast, session = -1, finalRemaining;
  private bool wasActive;
  public int LastVisited { get; private set; }

  public ManualGravityField(int capacity)
  {
    casts = new int[capacity];
    versions = new int[capacity];
    elapsed = new float[capacity];
  }

  private void SynchronizeSession(ManualFleetAbilities abilities)
  {
    if (session != abilities.SessionVersion)
    {
      Array.Clear(casts);
      session = abilities.SessionVersion;
      cast = cursor = finalRemaining = 0;
      wasActive = false;
    }
  }

  public void RegisterSpawn(GemSpatialIndex grid, int index, ManualFleetAbilities abilities)
  {
    SynchronizeSession(abilities);
    if ((uint)index >= (uint)casts.Length || abilities.MagnetCast == 0) return;
    // A newly spawned/recycled gem only experiences attraction after its birth.
    casts[index] = abilities.MagnetCast;
    versions[index] = grid.SlotVersion(index);
    elapsed[index] = abilities.MagnetElapsed;
  }

  public static Vector2 Pull(Vector2 position, Vector2 home, float retention, float homeRadius)
    => Vector2.DistanceSquared(position, home) <= homeRadius * homeRadius
      ? position : home + (position - home) * retention;

  public void Update(GemSpatialIndex grid, ManualFleetAbilities abilities, Vector2 home,
    float homeRadius, Action<int, Vector2> move)
  {
    LastVisited = 0;
    SynchronizeSession(abilities);
    if (abilities.MagnetCast != cast)
    {
      cast = abilities.MagnetCast;
      cursor = 0;
      finalRemaining = !abilities.IsActive(ManualFleetAbilities.MagnetizerSlot) && abilities.MagnetElapsed > 0f ? grid.AllocatedSlotCount : 0;
      wasActive = false;
    }
    bool active = abilities.IsActive(ManualFleetAbilities.MagnetizerSlot);
    if (wasActive && !active) finalRemaining = grid.AllocatedSlotCount;
    wasActive = active;
    if (cast == 0 || (!active && finalRemaining == 0) || grid.AllocatedSlotCount == 0) return;
    int count = Math.Min(FrameBudget, grid.AllocatedSlotCount);
    if (!active) count = Math.Min(count, finalRemaining);
    bool smallPopulation = grid.AvailableCount <= FrameBudget;
    if (smallPopulation)
    {
      // A depleted field can have a large historical slot count. Visit its live
      // entries directly, copying first because movement may reorder query lists.
      count = grid.AvailableCount;
      grid.AvailableIndices.CopyTo(smallBatch);
      if (!active) finalRemaining = count;
    }
    // Exponentials are shared by gems last visited in the same batch.
    float cachedDt = float.NaN, cachedRetention = 1f;
    for (int i = 0; i < count; ++i)
    {
      if (cursor >= grid.AllocatedSlotCount) cursor = 0;
      int index = smallPopulation ? smallBatch[i] : cursor++;
      ref var gem = ref grid.Gems[index];
      ++LastVisited;
      if (!gem.IsActive || gem.ClaimState != 0) continue;
      bool first = casts[index] != cast || versions[index] != grid.SlotVersion(index);
      float previous = first ? 0f : elapsed[index];
      float dt = Math.Max(0f, abilities.MagnetElapsed - previous);
      if (dt != cachedDt)
      {
        cachedDt = dt;
        cachedRetention = MathF.Exp(-1.1f * abilities.MagnetStrength * dt);
      }
      float retention = cachedRetention * (first ? Math.Clamp(1f - 0.18f * abilities.MagnetStrength, 0.35f, 1f) : 1f);
      casts[index] = cast;
      versions[index] = grid.SlotVersion(index);
      elapsed[index] = abilities.MagnetElapsed;
      var position = new Vector2(gem.X, gem.Y);
      var next = Pull(position, home, retention, homeRadius);
      if (next != position) move(index, next);
    }
    if (!active) finalRemaining -= count;
  }
}
