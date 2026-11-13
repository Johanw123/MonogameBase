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
      for (int forks = 0; forks < 3 && ship.ThunderArcs.Count < 40; forks++)
      {
        if (!TryCollectNearbyModuleGem(ship, origin, 110f, GemCollectionMethod.ThunderGod, out var target)) break;
        ship.ThunderArcs.Add((origin, target));
        if (depth < 3) thunderFrontier.Enqueue((target, depth + 1));
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
        ship.WorldEaterPickups = Math.Min(80, ship.WorldEaterPickups + SwallowGems(ship));
        ship.EaterCooldown = 0.1f;
      }
    }
    UpdateHeistReplay(ship, dt);
  }

  // The black hole feeds from all around: random probes first, then whatever is in reach.
  private int SwallowGems(Harvester ship)
  {
    var center = ship.BoundingCircle.Center;
    float radius = ship.WorldEaterRadius;
    // It always nibbles, so the hole is alive before the ship's first pickup.
    int limit = ModuleLimit(ship, Math.Max(1, ship.WorldEaterHunger)), swallowed = 0;
    ship.WorldEaterHunger = 0;
    for (int misses = 0; swallowed < limit && misses < 4;)
    {
      int index = FindModuleGem(center, radius);
      if (index >= 0 && TrySendModuleGemHome(ship, index, GemCollectionMethod.WorldEater, center)) ++swallowed;
      else ++misses;
    }
    if (swallowed >= limit) return swallowed;
    foreach (int index in flatSpatialHash.QueryCollection(center.X, center.Y, radius))
      if (TrySendModuleGemHome(ship, index, GemCollectionMethod.WorldEater, center) && ++swallowed >= limit) break;
    return swallowed;
  }

  private static void PayGhost(Harvester ship)
  {
    if (ship.GhostPayout == 0) return;
    UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
      UntitledGemGameGameScreen.DeliveredUncounted, ship.GhostPayout);
    RecordCollection(ship, GemCollectionMethod.TimeHeist, ship.GhostPayout);
    ship.GhostPayout = 0;
  }

  private void UpdateHeistReplay(Harvester ship, float dt)
  {
    if (ship.GhostRoute == null || dt <= 0f) return;
    // The ghost keeps pace with its ship, so a fast ship's ghost finishes before the next delivery.
    float travel = Math.Max(300f, BaseStats.GetHarvesterSpeed(ship)) * dt;
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
    if (ship.GhostSegment >= ship.GhostRoute.Length)
    {
      ship.GhostRoute = null;
      PayGhost(ship);
    }
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
      if (TrySendModuleGemHome(ship, index, GemCollectionMethod.TimeHeist, position)) ++collected;
    }
  }
}
