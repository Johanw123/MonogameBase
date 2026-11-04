using System;
using System.Threading;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Systems;

// Mothership Protocol (roles in Entities/Harvester.Carrier.cs, tuning in PrestigeTalentEffects).
// A carrier flies its type's usual route and holds station wherever it arrives; shuttles hop
// between it and nearby gems. Once its hold fills they return aboard or fall in around it,
// and the carrier delivers the lot through the ordinary DeliverCargo.
public partial class HarvesterCollectionSystem
{
  private void UpdateShuttle(GameTime gameTime, Harvester shuttle, Transform2 transform, PlayAreaBounds bounds)
  {
    Vector2 carrier = shuttle.DockPosition;
    if (shuttle.ReturningToHomebase)
    {
      if (!TryDock(shuttle, transform)) UpdateMovement(carrier, gameTime, transform, shuttle);
      return;
    }
    if (shuttle.ShuttleRecalled)
    {
      UpdateMovement(EscortSlot(shuttle, carrier), gameTime, transform, shuttle);
      return;
    }

    float arrival = GetTargetArrivalRadius(shuttle);
    // Gems at the very edge of the leash count for its search, so leave them some slack.
    float reach = shuttle.ShuttleLeash * 1.25f;
    if (shuttle.TargetScreenPosition is not Vector2 target || !IsCurrentGemTargetAvailable(shuttle)
      || Vector2.DistanceSquared(transform.Position, target) < arrival * arrival
      || Vector2.DistanceSquared(carrier, target) > reach * reach)
      shuttle.TargetScreenPosition = target = PickShuttleTarget(shuttle, carrier, bounds);
    UpdateMovement(target, gameTime, transform, shuttle);
  }

  // The golden angle spreads any number of shuttles around the hull without tracking slots.
  private static Vector2 EscortSlot(Harvester shuttle, Vector2 carrier)
  {
    float angle = shuttle.Id * 2.3999632f % MathHelper.TwoPi;
    float radius = PrestigeTalentEffects.ShuttleEscortRadius + shuttle.Id % 3 * 16f;
    return carrier + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
  }

  private Vector2 PickShuttleTarget(Harvester shuttle, Vector2 carrier, PlayAreaBounds bounds)
  {
    ReleaseTreasureScannerTarget(shuttle);
    float leash = shuttle.ShuttleLeash;
    var position = shuttle.BoundingCircle.Center;
    float near = PrestigeTalentEffects.ShuttleSearchRadius;
    // Close by first, in small rings: a dense field would otherwise cost a long scan per hop.
    // Centred on the carrier everything is in reach, so the first free gem ends the search.
    int gemIndex = FindShuttleGem(shuttle, position, near, carrier, leash);
    if (gemIndex < 0) gemIndex = FindShuttleGem(shuttle, position, near * 3f, carrier, leash);
    if (gemIndex < 0) gemIndex = FindShuttleGem(shuttle, carrier, leash, carrier, leash);
    if (gemIndex >= 0 && TryReserveTreasureScannerTarget(shuttle, gemIndex))
    {
      ref var gem = ref flatSpatialHash.Gems[gemIndex];
      shuttle.TargetGemGridIndex = gemIndex;
      shuttle.TargetGemEntityId = gem.EntityId;
      return new Vector2(gem.X, gem.Y);
    }
    // Nothing left to gather: bring in what it has, then circle the carrier until it finds more.
    shuttle.ShuttleUnloading = shuttle.CarryingGemCount > 0;
    float angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
    return bounds.Clamp(carrier + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * leash * 0.5f);
  }

  // A free gem around a point and within the leash; Prospector Lens and Treasure Scanner
  // shuttles look over them all for the most valuable.
  private int FindShuttleGem(Harvester shuttle, Vector2 center, float radius, Vector2 carrier, float leash)
  {
    bool byValue = shuttle.HasModule(ShipModule.ProspectorLens)
      || (shuttle.Type == Harvester.HarvesterType.AdvancedHarvester && UpgradeManager.Instance.UG.TreasureScanner);
    float leashSquared = leash * leash;
    var origin = shuttle.BoundingCircle.Center;
    int best = -1;
    uint bestValue = 0;
    float bestDistance = float.MaxValue;
    foreach (int index in flatSpatialHash.QueryCollection(center.X, center.Y, radius))
    {
      ref var gem = ref flatSpatialHash.Gems[index];
      if (!flatSpatialHash.IsAvailable(index) || Volatile.Read(ref _treasureScannerTargetOwners[index]) != 0) continue;
      var position = new Vector2(gem.X, gem.Y);
      if (Vector2.DistanceSquared(position, carrier) > leashSquared) continue;
      if (!byValue) return index;
      float distance = Vector2.DistanceSquared(origin, position);
      if (gem.BaseValue > bestValue || (gem.BaseValue == bestValue && distance < bestDistance))
      {
        best = index;
        bestValue = gem.BaseValue;
        bestDistance = distance;
      }
    }
    return best;
  }

  private void TransferToCarrier(Harvester shuttle)
  {
    ReleaseTreasureScannerTarget(shuttle);
    shuttle.Carrier.AddToHold(shuttle.CarryingGemCount, shuttle.CarryingGemBaseValue, shuttle.CarryingClickedBaseValue);
    shuttle.CarryingGemCount = 0;
    shuttle.CarryingGemBaseValue = 0;
    shuttle.CarryingClickedBaseValue = 0;
    shuttle.ReachedHome = false;
    shuttle.ShuttleUnloading = false;
    shuttle.TargetScreenPosition = null;
    shuttle.CollectionEndpoint = null;
  }

  // Three shuttles stand in for one ship, so a shuttle's trip lasts the carrier's whole
  // cycle: its small loads would otherwise fire trip effects three times as often per gem.
  private void StartShuttleTrips(Harvester carrier)
  {
    foreach (int id in _harvesters)
    {
      var ship = _harvesterMapper.Get(id);
      if (ship?.Carrier == carrier) ship.BeginModuleTrip();
    }
  }

  // True while the carrier should stay put: there is still about a gem in reach for every
  // shuttle, and it has not lingered too long. Staying put also saves its fuel.
  private bool HoldingStation(Harvester carrier, Vector2 position, float dt)
  {
    // The gem it flew to is the shuttles' to take now.
    ReleaseTreasureScannerTarget(carrier);
    carrier.StationSeconds += dt;
    if (carrier.StationSeconds < PrestigeTalentEffects.CarrierMaxStationSeconds)
    {
      int gems = 0;
      foreach (int index in flatSpatialHash.QueryCollection(position.X, position.Y, carrier.ShuttleLeash))
        if (flatSpatialHash.IsAvailable(index) && ++gems >= carrier.ShuttleCount) return true;
    }
    carrier.StationSeconds = 0f;
    return false;
  }
}
