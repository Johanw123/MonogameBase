using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  private readonly Dictionary<(int X, int Y), List<Harvester>> slipstreamCells = new();
  private readonly Stack<List<Harvester>> spareSlipstreamCells = new();

  private static bool CanSlipstream(Harvester ship)
    => ship != null && BaseStats.IsFleetHarvester(ship) && !ship.MarkedForDestroy
      && ship.CurrentState == Harvester.HarvesterState.Collecting && ship.PositionMoved
      && (ship.IsShuttle || ship.Fuel > 0f);

  private static (int X, int Y) SlipstreamCell(Vector2 position)
    => ((int)MathF.Floor(position.X / PrestigeTalentEffects.SlipstreamRadius),
      (int)MathF.Floor(position.Y / PrestigeTalentEffects.SlipstreamRadius));

  // Snapshot moving ships before the parallel movement pass. Nearby cells avoid an
  // all-pairs fleet scan, and one neighbour is enough even in a crowded departure lane.
  private void UpdateSlipstreams(float dt)
  {
    foreach (var cell in slipstreamCells.Values)
    {
      cell.Clear();
      spareSlipstreamCells.Push(cell);
    }
    slipstreamCells.Clear();
    bool enabled = UpgradeManager.Instance.UGM.Slipstream;
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship == null) continue;
      ship.SlipstreamTimeRemaining = enabled ? Math.Max(0f, ship.SlipstreamTimeRemaining - dt) : 0f;
      if (!enabled || !CanSlipstream(ship)) continue;
      var key = SlipstreamCell(ship.BoundingCircle.Center);
      if (!slipstreamCells.TryGetValue(key, out var cell))
        slipstreamCells.Add(key, cell = spareSlipstreamCells.TryPop(out var spare) ? spare : new());
      cell.Add(ship);
    }
    float radiusSquared = PrestigeTalentEffects.SlipstreamRadius * PrestigeTalentEffects.SlipstreamRadius;
    foreach (var (key, ships) in slipstreamCells)
      foreach (var ship in ships)
      {
        bool found = false;
        for (int x = key.X - 1; x <= key.X + 1 && !found; x++)
          for (int y = key.Y - 1; y <= key.Y + 1 && !found; y++)
          {
            if (!slipstreamCells.TryGetValue((x, y), out var neighbours)) continue;
            foreach (var other in neighbours)
            {
              if (other == ship || Vector2.DistanceSquared(ship.BoundingCircle.Center,
                other.BoundingCircle.Center) > radiusSquared) continue;
              ship.SlipstreamTimeRemaining = other.SlipstreamTimeRemaining = PrestigeTalentEffects.SlipstreamSeconds;
              found = true;
              break;
            }
          }
      }
  }

  // Reserve along the actual return movement, so fast ships don't skip gems. This
  // only runs on a return trip, including a shuttle's trip back to its carrier.
  private void ClaimTractorTail(Harvester ship, Vector2 start, Vector2 end)
  {
    long remaining = PrestigeTalentEffects.TractorTailCapacity(ship) - (long)ship.TowedGemCount;
    if (remaining <= 0 || !ship.ReturningToHomebase || ship.MarkedForDestroy
      || ship.CurrentState != Harvester.HarvesterState.Collecting) return;
    var segment = end - start;
    float lengthSquared = segment.LengthSquared();
    if (lengthSquared < 0.001f) return;
    float radius = BaseStats.GetHarvesterCollectionRange(ship);
    var center = (start + end) * 0.5f;
    foreach (int index in flatSpatialHash.QueryCollection(center.X, center.Y, segment.Length() * 0.5f + radius))
    {
      if (remaining == 0) break;
      ref var candidate = ref flatSpatialHash.Gems[index];
      var position = new Vector2(candidate.X, candidate.Y);
      float t = Math.Clamp(Vector2.Dot(position - start, segment) / lengthSquared, 0f, 1f);
      if (Vector2.DistanceSquared(position, start + segment * t) > radius * radius
        || !flatSpatialHash.TryClaim(index)) continue;
      ship.ClaimedTailGems.Add(candidate.EntityId);
      --remaining;
    }
  }

  private void ResolveTractorTail(Harvester ship, float dt)
  {
    foreach (int id in ship.ClaimedTailGems)
    {
      var gem = _gemMapper.Get(id);
      if (gem == null || gem.Id != id || !gem.IsLive) continue;
      if (!ship.MarkedForDestroy && ship.ReturningToHomebase
        && ship.TowedGemCount < PrestigeTalentEffects.TractorTailCapacity(ship))
        CollectGemCore(gem, ship, allowChainCollection: false, GemCollectionMethod.TractorTails);
      if (!gem.PickedUp && !gem.WasClicked && !gem.ShouldDestroy)
        flatSpatialHash.ReleaseClaim(gem.GridIndex);
    }
    ship.ClaimedTailGems.Clear();
    var transform = ship.Entity?.Get<Transform2>();
    if (transform != null) ship.UpdateTractorTail(transform.Position, transform.Rotation, dt);
  }
}
