using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Rebuilt on the main thread, then read by fleet workers. One scan serves the
// entire perimeter fleet rather than scanning every gem for every ship.
public sealed class PerimeterGemTargets
{
  private readonly List<(int Index, int EntityId)> candidates = new();
  private PlayAreaBounds bounds;
  private float targetBand;
  public int Count => candidates.Count;

  public static float EdgeDistance(Vector2 position, PlayAreaBounds bounds)
    => MathF.Max(0, MathF.Min(MathF.Min(position.X - bounds.Minimum.X, bounds.Maximum.X - position.X),
      MathF.Min(position.Y - bounds.Minimum.Y, bounds.Maximum.Y - position.Y)));

  public static float EdgeBand(PlayAreaBounds bounds)
    => MathF.Min(bounds.Maximum.X - bounds.Minimum.X, bounds.Maximum.Y - bounds.Minimum.Y) * 0.15f;

  public void Refresh(GemSpatialIndex gems, PlayAreaBounds area)
  {
    bounds = area;
    candidates.Clear();
    float nearestEdge = float.MaxValue;
    foreach (int index in gems.AvailableIndices)
    {
      ref var gem = ref gems.Gems[index];
      var position = new Vector2(gem.X, gem.Y);
      if (!gem.IsActive || gem.ClaimState != 0 || bounds.Clamp(position) != position) continue;
      nearestEdge = MathF.Min(nearestEdge, EdgeDistance(position, bounds));
    }
    // If the edge band is empty, use the outermost remaining gems.
    targetBand = MathF.Max(EdgeBand(bounds), nearestEdge);
    foreach (int index in gems.AvailableIndices)
    {
      ref var gem = ref gems.Gems[index];
      var position = new Vector2(gem.X, gem.Y);
      if (gem.IsActive && gem.ClaimState == 0 && bounds.Clamp(position) == position
        && EdgeDistance(position, bounds) <= targetBand)
        candidates.Add((index, gem.EntityId));
    }
  }

  public int Choose(GemSpatialIndex gems, Random random)
  {
    if (candidates.Count == 0) return -1;
    int start = random.Next(candidates.Count);
    for (int offset = 0; offset < Math.Min(32, candidates.Count); offset++)
    {
      var candidate = candidates[(start + offset) % candidates.Count];
      ref var gem = ref gems.Gems[candidate.Index];
      var position = new Vector2(gem.X, gem.Y);
      if (gem.IsActive && gem.ClaimState == 0 && gem.EntityId == candidate.EntityId
        && bounds.Clamp(position) == position && EdgeDistance(position, bounds) <= targetBand)
        return candidate.Index;
    }
    return -1;
  }
}
