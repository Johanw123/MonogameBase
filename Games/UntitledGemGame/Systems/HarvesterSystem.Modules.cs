using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  private void ApplyAdditionalPickupModules(Harvester harvester, Vector2 origin, bool direct)
  {
    harvester.RegisterModulePickup(direct);
    if (!direct) return;
    // Consume charge from earlier pulls before this pickup creates any more.
    // Cascade pulls can recharge the capacitor, but never trigger another cascade here.
    if (harvester.TryConsumeCascade())
      CollectModuleArea(harvester, origin, 90f, 6, Color.Violet);
    ulong count = harvester.DirectModulePickups;
    if (harvester.HasModule(ShipModule.PulseHarvester) && count % 6 == 0)
      CollectModuleArea(harvester, origin, 60f, 3, Color.DodgerBlue);
    if (harvester.HasModule(ShipModule.GunPod) && count % ModuleCatalog.GunPodInterval == 0)
      UntitledGemGameGameScreen.Instance?.FireShipShell(origin);
    if (harvester.HasModule(ShipModule.TwinTractor))
      CollectModuleArea(harvester, origin, 30f, 1, Color.Cyan);
    if (harvester.HasModule(ShipModule.StormCoil) && count % 8 == 0)
      CollectStormChain(harvester, origin);
    if (harvester.HasModule(ShipModule.RiftSiphon) && count == 1)
      CollectModuleArea(harvester, origin, 110f, 10, Color.MediumPurple);
    if (harvester.HasModule(ShipModule.AstralRelay) && count % 5 == 0)
    {
      ulong value = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
      UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
        UntitledGemGameGameScreen.DeliveredUncounted, value);
      harvester.RelayOrigin = origin;
      harvester.RelayFlashRemaining = ModuleCatalog.PulseDuration;
      harvester.ShowModulePulse(origin, 80f, Color.Gold);
    }
  }

  private bool TryCollectModuleGem(Harvester harvester, int index, out Vector2 position)
  {
    position = default;
    ref var candidate = ref flatSpatialHash.Gems[index];
    if (!candidate.IsActive || candidate.ClaimState != 0) return false;
    var gem = GetEntity(candidate.EntityId)?.Get<Gem>();
    if (gem == null || !gem.IsLive || gem.PickedUp || gem.WasClicked
      || !flatSpatialHash.TryClaim(index)) return false;
    position = gem.BoundingCircle.Center;
    // Bonus pulls cannot recursively trigger more pulls or charge direct-pickup modules.
    CollectGem(gem, harvester, allowChainCollection: false);
    if (!gem.PickedUp && !gem.WasClicked && !gem.ShouldDestroy)
    {
      flatSpatialHash.ReleaseClaim(index);
      return false;
    }
    return true;
  }

  private void CollectModuleArea(Harvester harvester, Vector2 origin, float radius, int limit, Color color)
  {
    harvester.ShowModulePulse(origin, radius, color);
    int collected = 0;
    foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
    {
      if (collected >= limit) break;
      if (TryCollectModuleGem(harvester, index, out _)) ++collected;
    }
  }

  private void CollectStormChain(Harvester harvester, Vector2 origin, int limit = 6, float radius = 50f)
  {
    if (harvester.StormArcPoints == null || harvester.StormArcPoints.Length < limit + 1)
      harvester.StormArcPoints = new Vector2[limit + 1];
    harvester.StormArcPoints[0] = origin;
    harvester.StormArcCount = 1;
    for (int hop = 0; hop < limit; hop++)
    {
      bool found = false;
      foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
      {
        if (!TryCollectModuleGem(harvester, index, out var target)) continue;
        harvester.StormArcPoints[harvester.StormArcCount++] = target;
        origin = target;
        found = true;
        break;
      }
      if (!found) break;
    }
    harvester.StormArcRemaining = ModuleCatalog.PulseDuration;
  }

  private void ApplyDroneLightning(Harvester drone, float dt)
  {
    if (drone.Type != Harvester.HarvesterType.Drone) return;
    drone.DroneLightningCooldownRemaining = System.Math.Max(0f, drone.DroneLightningCooldownRemaining - dt);
    if (!UpgradeManager.Instance.UGA.DroneLightning || drone.MarkedForDestroy
      || drone.ReturningToHomebase || drone.ReachedHome
      || drone.DroneLightningCooldownRemaining > 0f) return;

    // Bonus pickups use normal claims and cargo, without triggering further chains.
    int remainingCargo = BaseStats.GetHarvesterCapacity(drone) - (int)drone.CarryingGemCount;
    int limit = System.Math.Min(BaseStats.DroneLightningGemLimit, remainingCargo);
    CollectStormChain(drone, drone.BoundingCircle.Center, limit, BaseStats.DroneLightningJumpRadius);
    drone.DroneLightningCooldownRemaining = BaseStats.DroneLightningIntervalSeconds;
  }

  // Collection runs on the main thread after the parallel movement/claim pass.
  private void ApplyTravelModules(Harvester harvester, float dt)
  {
    if (harvester.MarkedForDestroy) return;
    if (harvester.BuoyRemaining > 0f)
    {
      harvester.BuoyRemaining = System.Math.Max(0f, harvester.BuoyRemaining - dt);
      if (harvester.BuoyRemaining == 0f)
        CollectModuleArea(harvester, harvester.BuoyPosition, 100f, 8, Color.MediumPurple);
    }
    if (harvester.ArcTravelCharge >= 90f)
    {
      harvester.ArcTravelCharge %= 90f;
      CollectStormChain(harvester, harvester.BoundingCircle.Center, 4, 70f);
    }
    if (harvester.BuoyTravelCharge >= 180f && harvester.BuoyRemaining == 0f)
    {
      harvester.BuoyTravelCharge %= 180f;
      harvester.BuoyPosition = harvester.BoundingCircle.Center;
      harvester.BuoyRemaining = 1f;
    }
    if (harvester.RecallTravelCharge >= 120f && harvester.CollectionEndpoint.HasValue)
    {
      harvester.RecallTravelCharge %= 120f;
      var endpoint = harvester.CollectionEndpoint.Value;
      CollectModuleArea(harvester, endpoint, 100f, 3, Color.Cyan);
      harvester.TractorOrigin = endpoint;
      harvester.TractorTarget = harvester.BoundingCircle.Center;
      harvester.TractorFlashRemaining = 0.25f;
    }
  }

  private void ApplyFullCargoModules(Harvester harvester, Vector2 origin)
  {
    if (harvester.TryBeginReactorBloom())
      CollectModuleArea(harvester, origin, 50f, 6, Color.LimeGreen);
    if (harvester.TryBeginEventHorizon())
      CollectModuleArea(harvester, origin, 200f, 48, Color.MediumPurple);
  }
}
