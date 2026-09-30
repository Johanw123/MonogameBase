using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  private readonly Queue<(Vector2 Position, int Depth)> thunderFrontier = new(40);

  private void UnleashThunder(Harvester ship)
  {
    ship.ThunderArcs.Clear();
    thunderFrontier.Clear();
    thunderFrontier.Enqueue((ship.BoundingCircle.Center, 0));
    while (thunderFrontier.Count > 0 && ship.ThunderArcs.Count < 40)
    {
      var (origin, depth) = thunderFrontier.Dequeue();
      int forks = 0;
      foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, 110f))
      {
        if (!TryCollectModuleGem(ship, index, out var target)) continue;
        ship.ThunderArcs.Add((origin, target));
        if (depth < 3) thunderFrontier.Enqueue((target, depth + 1));
        if (++forks == 3 || ship.ThunderArcs.Count == 40) break;
      }
    }
    ship.ThunderFlashRemaining = ModuleCatalog.PulseDuration;
  }

  private void ApplyMythicModules(Harvester ship, float dt)
  {
    if (ship.MarkedForDestroy) return;
    ship.ThunderFlashRemaining = Math.Max(0f, ship.ThunderFlashRemaining - dt);
    if (ship.ThunderTravelCharge >= 60f)
    {
      ship.ThunderTravelCharge %= 60f;
      UnleashThunder(ship);
    }
    if (ship.HasModule(ShipModule.WorldEater))
    {
      ship.EaterCooldown = Math.Max(0f, ship.EaterCooldown - dt);
      if (ship.EaterCooldown == 0f && !ship.ReachedHome)
      {
        int swallowed = 0;
        foreach (int index in flatSpatialHash.QueryCollection(ship.BoundingCircle.Center.X,
          ship.BoundingCircle.Center.Y, ship.WorldEaterRadius))
        {
          if (TryCollectModuleGem(ship, index, out _))
          {
            ship.WorldEaterPickups = Math.Min(80, ship.WorldEaterPickups + 1);
            if (++swallowed == 16) break;
          }
        }
        ship.EaterCooldown = 0.25f;
      }
    }
    UpdateHeistReplay(ship, dt);
  }

  private void DetonateWorldEater(Harvester ship)
  {
    if (ship.MarkedForDestroy || !ship.HasModule(ShipModule.WorldEater) || ship.CarryingGemCount == 0) return;
    CollectModuleArea(ship, ship.BoundingCircle.Center, 300f, 96, Color.MediumPurple);
  }

  private void UpdateHeistReplay(Harvester ship, float dt)
  {
    if (ship.GhostRoute == null || dt <= 0f) return;
    float travel = 300f * dt;
    int collected = 0;
    // Walk each recorded segment independently, preserving discontinuities at warps.
    while (travel > 0f && ship.GhostSegment < ship.GhostRoute.Length)
    {
      var segment = ship.GhostRoute[ship.GhostSegment];
      float length = Vector2.Distance(segment.Start, segment.End);
      float step = Math.Min(travel, Math.Max(0f, length - ship.GhostSegmentDistance));
      var start = length > 0f ? Vector2.Lerp(segment.Start, segment.End, ship.GhostSegmentDistance / length) : segment.Start;
      ship.GhostSegmentDistance += step;
      var end = length > 0f ? Vector2.Lerp(segment.Start, segment.End, ship.GhostSegmentDistance / length) : segment.End;
      ship.GhostPreviousPosition = start;
      ship.GhostPosition = end;
      CollectGhostTrail(ship, start, end, ref collected);
      travel -= step;
      if (ship.GhostSegmentDistance >= length)
      {
        ++ship.GhostSegment;
        ship.GhostSegmentDistance = 0f;
      }
    }
    if (ship.GhostSegment >= ship.GhostRoute.Length) ship.GhostRoute = null;
  }

  private void CollectGhostTrail(Harvester ship, Vector2 start, Vector2 end, ref int collected)
  {
    var center = (start + end) * 0.5f;
    var direction = end - start;
    float lengthSquared = direction.LengthSquared();
    foreach (int index in flatSpatialHash.QueryCollection(center.X, center.Y, direction.Length() * 0.5f + 70f))
    {
      if (collected >= 32) break;
      ref var candidate = ref flatSpatialHash.Gems[index];
      if (!candidate.IsActive || candidate.ClaimState != 0) continue;
      var position = new Vector2(candidate.X, candidate.Y);
      float t = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(position - start, direction) / lengthSquared, 0f, 1f) : 0f;
      if (Vector2.DistanceSquared(position, start + direction * t) > 70f * 70f) continue;
      var gem = GetEntity(candidate.EntityId)?.Get<Gem>();
      if (gem == null || !gem.IsLive || !flatSpatialHash.TryClaim(index)) continue;
      // Bloom seeds keep their ordinary split behavior; they do not pay twice.
      if (!gem.TryBloom())
      {
        gem.MergeGem(position);
        UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
          UntitledGemGameGameScreen.DeliveredUncounted, BaseStats.GetHarvesterDeliveryValue(ship, gem.BaseValue));
      }
      ++collected;
      ++UntitledGemGameGameScreen.Collected;
      UpgradeManager.Instance.Modules.RecordHarvest();
    }
  }
}
