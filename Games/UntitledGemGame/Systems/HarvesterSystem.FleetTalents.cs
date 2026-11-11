using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
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
    if (ship.DropPods.Count > 0) ship.UpdateDropPods(UntitledGemGameGameScreen.HomeBasePos, dt);
  }

  // Before the ship turns home: its first full hold of the trip is delivered from
  // where it is, with everything an ordinary fleet delivery sets off.
  private void LaunchDropPod(Harvester ship)
  {
    if (!ship.TryLaunchDropPod()) return;
    uint cargo = ship.CarryingGemCount;
    ulong value = BaseStats.GetHarvesterDeliveryValue(ship, ship.CarryingGemBaseValue);
    UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
      UntitledGemGameGameScreen.DeliveredUncounted, value);
    RecordDelivery(ship, value);
    AudioManager.Instance?.PlaySound(SoundEvent.HarvesterDeliverCargo);
    var from = ship.BoundingCircle.Center;
    UntitledGemGameGameScreen.Instance?.ChargeWeaponsFromCargo(cargo);
    UntitledGemGameGameScreen.Instance?.OnFleetDelivery(ship, from, cargo);
    ship.FinishDropPod(UntitledGemGameGameScreen.HomeBasePos);
  }
}
