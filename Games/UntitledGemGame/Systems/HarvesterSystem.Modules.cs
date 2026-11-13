using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  private void ApplyAdditionalPickupModules(Harvester harvester, Vector2 origin, bool direct)
  {
    harvester.RegisterModulePickup(direct);
    while (harvester.TryBeginRelayCopy())
    {
      ulong value = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
      UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
        UntitledGemGameGameScreen.DeliveredUncounted, value);
      RecordCollection(harvester, GemCollectionMethod.AstralRelay, value);
      harvester.RelayOrigin = origin;
      harvester.RelayFlashRemaining = ModuleCatalog.PulseDuration;
      harvester.ShowModulePulse(origin, 80f, Color.Gold);
    }
    // Bonus pulls never trigger more pulls.
    if (!direct) return;
    ulong count = harvester.DirectModulePickups;
    if (harvester.HasModule(ShipModule.CascadeCapacitor) && moduleRandom.NextSingle() < ModuleCatalog.CascadeChance)
      RunCascade(harvester, origin);
    if (harvester.HasModule(ShipModule.EchoChamber) && count % ModuleCatalog.EchoInterval == 0)
      CollectModuleArea(harvester, origin, 70f, ModuleCatalog.EchoGems, Color.MediumPurple, GemCollectionMethod.EchoChamber);
    if (harvester.HasModule(ShipModule.PulseHarvester) && count % 6 == 0)
      CollectModuleArea(harvester, origin, 60f, 4, Color.DodgerBlue, GemCollectionMethod.PulseHarvester);
    if (harvester.HasModule(ShipModule.GunPod) && count % ModuleCatalog.GunPodInterval == 0)
      UntitledGemGameGameScreen.Instance?.FireShipShell(origin);
    if (harvester.HasModule(ShipModule.TwinTractor))
      CollectModuleArea(harvester, origin, 30f, 1, Color.Cyan, GemCollectionMethod.TwinTractor);
    if (harvester.HasModule(ShipModule.StormCoil) && count % 8 == 0)
      CollectStormChain(harvester, origin, GemCollectionMethod.StormCoil);
    if (harvester.HasModule(ShipModule.RiftSiphon) && count == 1)
      CollectModuleArea(harvester, origin, 160f, System.Math.Max(10, BaseStats.GetHarvesterCapacity(harvester) / 3),
        Color.MediumPurple, GemCollectionMethod.RiftSiphon);
  }

  private readonly System.Collections.Generic.Queue<Vector2> cascadeFrontier = new();

  // Each cascade pulls a few gems, and every gem it pulls may set off another.
  private void RunCascade(Harvester harvester, Vector2 origin)
  {
    cascadeFrontier.Clear();
    cascadeFrontier.Enqueue(origin);
    harvester.ShowModulePulse(origin, 80f, Color.Violet);
    int total = 0;
    while (cascadeFrontier.Count > 0 && total < ModuleCatalog.CascadeMaxGems)
    {
      var from = cascadeFrontier.Dequeue();
      for (int pull = 0; pull < ModuleLimit(harvester, ModuleCatalog.CascadeGems) && total < ModuleCatalog.CascadeMaxGems; pull++)
      {
        if (!TryCollectNearbyModuleGem(harvester, from, 80f, GemCollectionMethod.CascadeCapacitor, out var target)) break;
        ++total;
        if (moduleRandom.NextSingle() < ModuleCatalog.CascadeChance) cascadeFrontier.Enqueue(target);
      }
    }
  }

  // Vacuum Nozzle adds a gem to every pull, pulse and lightning strike.
  private static int ModuleLimit(Harvester harvester, int limit)
    => harvester.HasModule(ShipModule.VacuumNozzle) ? limit + 1 : limit;

  private bool TryCollectModuleGem(Harvester harvester, int index, GemCollectionMethod method, out Vector2 position)
  {
    position = default;
    ref var candidate = ref flatSpatialHash.Gems[index];
    if (!candidate.IsActive || candidate.ClaimState != 0) return false;
    var gem = GetEntity(candidate.EntityId)?.Get<Gem>();
    if (gem == null || !gem.IsLive || gem.PickedUp || gem.WasClicked
      || !flatSpatialHash.TryClaim(index)) return false;
    position = gem.BoundingCircle.Center;
    // Bonus pulls cannot recursively trigger more pulls or charge direct-pickup modules.
    CollectGemCore(gem, harvester, allowChainCollection: false, method);
    if (!gem.PickedUp && !gem.WasClicked && !gem.ShouldDestroy)
    {
      flatSpatialHash.ReleaseClaim(index);
      return false;
    }
    return true;
  }

  // The grid hands back gems cell by cell from one corner, so taking the first few always
  // pulls from the same side. Module effects pick around their origin instead: each probe
  // looks near a random point inside the radius.
  private int FindModuleGem(Vector2 origin, float radius)
  {
    float probeRadius = System.Math.Max(12f, radius * 0.3f);
    for (int probe = 0; probe < 3; probe++)
    {
      float angle = moduleRandom.NextSingle() * MathHelper.TwoPi;
      float distance = radius * System.MathF.Sqrt(moduleRandom.NextSingle());
      var point = origin + new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle)) * distance;
      foreach (int index in flatSpatialHash.QueryCollection(point.X, point.Y, probeRadius))
      {
        ref var candidate = ref flatSpatialHash.Gems[index];
        if (!candidate.IsActive || candidate.ClaimState != 0) continue;
        float dx = candidate.X - origin.X, dy = candidate.Y - origin.Y;
        if (dx * dx + dy * dy <= radius * radius) return index;
      }
    }
    return -1;
  }

  // Up to limit gems spread around the origin. Probes that miss or find a gem that cannot be
  // taken stop early, and a plain pass over the area then takes whatever is still in reach.
  private int CollectModuleGems(Harvester harvester, Vector2 origin, float radius, int limit,
    GemCollectionMethod method)
  {
    limit = ModuleLimit(harvester, limit);
    int collected = 0;
    for (int misses = 0; collected < limit && misses < 4;)
    {
      int index = FindModuleGem(origin, radius);
      if (index >= 0 && TryCollectModuleGem(harvester, index, method, out _)) ++collected;
      else ++misses;
    }
    if (collected >= limit) return collected;
    foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
    {
      if (TryCollectModuleGem(harvester, index, method, out _) && ++collected >= limit) break;
    }
    return collected;
  }

  private void CollectModuleArea(Harvester harvester, Vector2 origin, float radius, int limit, Color color,
    GemCollectionMethod method)
  {
    harvester.ShowModulePulse(origin, radius, color);
    CollectModuleGems(harvester, origin, radius, limit, method);
  }

  // One gem near the origin, in a random direction.
  private bool TryCollectNearbyModuleGem(Harvester harvester, Vector2 origin, float radius,
    GemCollectionMethod method, out Vector2 position)
  {
    for (int attempt = 0; attempt < 2; attempt++)
    {
      int index = FindModuleGem(origin, radius);
      if (index >= 0 && TryCollectModuleGem(harvester, index, method, out position)) return true;
    }
    foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
      if (TryCollectModuleGem(harvester, index, method, out position)) return true;
    position = default;
    return false;
  }

  private void CollectStormChain(Harvester harvester, Vector2 origin, GemCollectionMethod method,
    int limit = 6, float radius = 50f)
  {
    limit = ModuleLimit(harvester, limit);
    if (harvester.StormArcPoints == null || harvester.StormArcPoints.Length < limit + 1)
      harvester.StormArcPoints = new Vector2[limit + 1];
    harvester.StormArcPoints[0] = origin;
    harvester.StormArcCount = 1;
    for (int hop = 0; hop < limit; hop++)
    {
      if (!TryCollectNearbyModuleGem(harvester, origin, radius, method, out var target)) break;
      harvester.StormArcPoints[harvester.StormArcCount++] = target;
      origin = target;
    }
    harvester.StormArcRemaining = ModuleCatalog.PulseDuration;
    harvester.StormArcSeed = moduleRandom.NextSingle();
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
    CollectStormChain(drone, drone.BoundingCircle.Center, GemCollectionMethod.StormDrones,
      limit, BaseStats.DroneLightningJumpRadius);
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
        CollectModuleArea(harvester, harvester.BuoyPosition, 110f, 24, Color.MediumPurple,
          GemCollectionMethod.GravityBuoy);
    }
    if (harvester.ArcTravelCharge >= 90f)
    {
      harvester.ArcTravelCharge %= 90f;
      CollectStormChain(harvester, harvester.BoundingCircle.Center, GemCollectionMethod.ArcEmitter, 5, 70f);
    }
    if (harvester.BuoyTravelCharge >= 150f && harvester.BuoyRemaining == 0f)
    {
      harvester.BuoyTravelCharge %= 150f;
      harvester.BuoyPosition = harvester.BoundingCircle.Center;
      harvester.BuoyRemaining = 0.5f;
    }
  }

  private void ApplyFullCargoModules(Harvester harvester, Vector2 origin)
  {
    if (harvester.TryBeginEventHorizon())
      CollectModuleArea(harvester, origin, 220f, System.Math.Max(24, BaseStats.GetHarvesterCapacity(harvester) / 2),
        Color.MediumPurple, GemCollectionMethod.EventHorizon);
  }

  // Before the ship turns home: the full hold is delivered from where the ship is.
  private void IgnitePhoenix(Harvester harvester)
  {
    if (!harvester.TryIgnitePhoenix()) return;
    double scaled = System.Math.Ceiling(
      BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue) * ModuleCatalog.PhoenixMultiplier);
    ulong value = scaled >= ulong.MaxValue ? ulong.MaxValue : (ulong)scaled;
    UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
      UntitledGemGameGameScreen.DeliveredUncounted, value);
    RecordDelivery(harvester, value);
    harvester.FinishPhoenix();
  }

  // A gem a module sends straight home instead of loading it into the hold.
  private bool TrySendModuleGemHome(Harvester ship, int index, GemCollectionMethod method, Vector2 toward)
  {
    ref var candidate = ref flatSpatialHash.Gems[index];
    if (!candidate.IsActive || candidate.ClaimState != 0) return false;
    var gem = GetEntity(candidate.EntityId)?.Get<Gem>();
    if (gem == null || !gem.IsLive || gem.PickedUp || gem.WasClicked || !flatSpatialHash.TryClaim(index)) return false;
    // Bloom seeds keep their ordinary split behavior; they do not pay twice.
    if (!gem.TryBloom())
    {
      gem.MergeGem(toward);
      double scaled = System.Math.Ceiling(
        BaseStats.GetHarvesterDeliveryValue(ship, gem.BaseValue) * (1 + ship.ModuleGemBonus(method)));
      ulong value = scaled >= ulong.MaxValue ? ulong.MaxValue : (ulong)scaled;
      UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
        UntitledGemGameGameScreen.DeliveredUncounted, value);
      RecordCollection(ship, method, value);
    }
    ++UntitledGemGameGameScreen.Collected;
    UpgradeManager.Instance.Modules.RecordHarvest();
    return true;
  }
}
