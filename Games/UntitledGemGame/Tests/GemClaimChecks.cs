using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class GemClaimChecks
{
  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime gameTime) { }
  }

  public static void Run()
  {
    CheckHomeBaseActivation();
    CheckHomeBaseLaunchInterception();
    CheckFinalSweep();
    var fleet = new FleetProbe();
    var resolve = typeof(HarvesterCollectionSystem).GetMethod("ResolveClaimedGems",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    void Resolve(Harvester harvester) => resolve.Invoke(fleet, new object[] { harvester });
    using var world = new WorldBuilder().AddSystem(fleet).Build();
    var entity = world.CreateEntity();
    entity.Attach(new Transform2(new Vector2(500, 200)));
    var gem = new Gem();
    gem.Initialize(entity, 18, 7);
    entity.Attach(gem);
    var grid = fleet.flatSpatialHash;
    gem.GridIndex = grid.AddGem(entity.Id, 500, 200, 7);
    world.Update(new GameTime());

    var ship = new Harvester { MarkedForDestroy = true, ReachedHome = true };
    grid.TryClaim(gem.GridIndex);
    grid.RemoveFromQueries(gem.GridIndex);
    ship.ClaimedGems.Add(entity.Id);
    Resolve(ship);
    if (ship.ClaimedGems.Count != 0 || gem.PickedUp || grid.AvailableCount != 1)
      throw new Exception("Retiring ships must release pending pickups, including during home arrival");
    var query = grid.Query(500, 200, 20, 20);
    if (!query.MoveNext() || query.Current != gem.GridIndex || !grid.TryClaim(gem.GridIndex))
      throw new Exception("Released gems must be clickable and claimable by another harvester");

    // A click can supersede a fleet reservation before it is resolved.
    gem.WasClicked = true;
    grid.Gems[gem.GridIndex].ClaimState = 2;
    grid.RemoveFromQueries(gem.GridIndex);
    ship.ClaimedGems.Add(entity.Id);
    Resolve(ship);
    if (grid.AvailableCount != 0 || grid.Gems[gem.GridIndex].ClaimState != 2)
      throw new Exception("Resolving an old fleet claim must preserve an ongoing click pickup");

    gem.WasClicked = false;
    grid.ReleaseClaim(gem.GridIndex);
    var collectorEntity = world.CreateEntity();
    collectorEntity.Attach(new Transform2(Vector2.Zero));
    var collector = new Harvester { Id = collectorEntity.Id, ReachedHome = true };
    collectorEntity.Attach(collector);
    world.Update(new GameTime());
    grid.TryClaim(gem.GridIndex);
    collector.ClaimedGems.Add(entity.Id);
    // Suppress pickup audio in this content-free fixture.
    typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
      .SetValue(fleet, 1);
    var previousManager = UpgradeManager.Instance;
    var previousCollected = UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected;
    _ = new UpgradeManager();
    try
    {
      Resolve(collector);
      if (!gem.PickedUp || collector.CarryingGemCount != 1 || collector.CarryingGemBaseValue != 7
        || collector.ClaimedGems.Count != 0 || grid.AvailableCount != 0)
        throw new Exception("Pending pickups must enter cargo before home delivery");
      Resolve(collector);
      if (collector.CarryingGemCount != 1)
        throw new Exception("Resolved pickups must not award cargo twice");
    }
    finally
    {
      UpgradeManager.Instance = previousManager;
      UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected = previousCollected;
    }
    Console.WriteLine("Gem claim checks passed: retiring ships, home arrival, recollection and superseding clicks.");
  }

  private static void CheckFinalSweep()
  {
    var previousManager = UpgradeManager.Instance;
    var previousCollected = UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected;
    try
    {
      var manager = new UpgradeManager();
      manager.UGA.DroneCapacity = 1;
      manager.UGA.DroneFinalSweep = true;
      manager.UGA.DroneRecharge = true;
      manager.UGA.DroneCollectionRange = 1.45f;
      var fleet = new FleetProbe();
      using var world = new WorldBuilder().AddSystem(fleet).Build();
      var entity = world.CreateEntity();
      var position = new Vector2(500, 500);
      entity.Attach(new Transform2(position));
      var drone = new Harvester { Entity = entity, Id = entity.Id, Type = Harvester.HarvesterType.Drone };
      entity.Attach(drone);
      var gems = new List<Gem>();
      foreach (float distance in new[]
      {
        BaseStats.DroneFinalSweepRadius * 0.8f,
        BaseStats.DroneFinalSweepRadius * 0.9f,
        BaseStats.DroneFinalSweepRadius * 1.2f
      })
      {
        var gemEntity = world.CreateEntity();
        var gemPosition = position + new Vector2(distance, 0);
        gemEntity.Attach(new Transform2(gemPosition));
        var gem = new Gem();
        gem.Initialize(gemEntity, 18, 7);
        gemEntity.Attach(gem);
        gem.GridIndex = fleet.flatSpatialHash.AddGem(gemEntity.Id, gemPosition.X, gemPosition.Y, 7);
        gems.Add(gem);
      }
      world.Update(new GameTime());
      fleet.flatSpatialHash.PrepareQueries();
      var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
      var claim = typeof(HarvesterCollectionSystem).GetMethod("ClaimFinalSweep", flags)!;
      var resolve = typeof(HarvesterCollectionSystem).GetMethod("ResolveClaimedGems", flags)!;
      typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame", flags)!.SetValue(fleet, 1);
      claim.Invoke(fleet, new object[] { drone, position });
      if (drone.ClaimedGems.Count != 0) throw new Exception("Final Sweep must wait for expiry");
      drone.AdvanceDroneTimers(manager.UGA.IncreaseDroneFuel);
      float expiredTimer = drone.TimeAlive;
      claim.Invoke(fleet, new object[] { drone, position });
      if (drone.ClaimedGems.Count != 2 || !drone.ResolvingFinalSweep)
        throw new Exception("Final Sweep must claim gems within its configured radius only");
      resolve.Invoke(fleet, new object[] { drone });
      if (drone.CarryingGemCount != 2)
        throw new Exception("Final Sweep must collect beyond cargo capacity");
      if (!gems[0].PickedUp || !gems[1].PickedUp || gems[2].PickedUp || drone.CarryingGemBaseValue != 14
        || drone.TimeAlive != expiredTimer || !drone.ReturningToHomebase || drone.ResolvingFinalSweep)
        throw new Exception("Final Sweep must load cargo without recharging or interrupting return");
      fleet.flatSpatialHash.TryClaim(gems[2].GridIndex);
      drone.ClaimedGems.Add(gems[2].Id);
      resolve.Invoke(fleet, new object[] { drone });
      if (gems[2].PickedUp || fleet.flatSpatialHash.Gems[gems[2].GridIndex].ClaimState != 0)
        throw new Exception("Normal pickups after Final Sweep must release excess reservations");
      drone.AdvanceDroneTimers(1f);
      claim.Invoke(fleet, new object[] { drone, position });
      if (drone.ResolvingFinalSweep || drone.ClaimedGems.Count != 0 || drone.FinalSweepTimeRemaining != 0)
        throw new Exception("Final Sweep must happen only once and its visual must expire");
      manager.UGA.DroneFinalSweep = false;
      var ordinary = new Harvester { Type = Harvester.HarvesterType.Drone };
      ordinary.AdvanceDroneTimers(manager.UGA.IncreaseDroneFuel);
      if (ordinary.TryBeginFinalSweep(position)) throw new Exception("Final Sweep requires its upgrade");
    }
    finally
    {
      UpgradeManager.Instance = previousManager;
      UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected = previousCollected;
    }
  }

  private static void CheckHomeBaseActivation()
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var fleet = new FleetProbe();
      using var world = new WorldBuilder().AddSystem(fleet).Build();
      var entity = world.CreateEntity();
      entity.Attach(new Transform2(new Vector2(400, 200)));
      var home = new Harvester
      {
        Entity = entity, Id = entity.Id, Type = Harvester.HarvesterType.HomeBase,
        CurrentState = Harvester.HarvesterState.None
      };
      entity.Attach(home);
      world.Update(new GameTime());
      var grid = fleet.flatSpatialHash;
      int nearby = grid.AddGem(300, 410, 200, 1);
      int distant = grid.AddGem(301, 600, 200, 1);
      grid.PrepareQueries();
      var update = typeof(HarvesterCollectionSystem).GetMethod("UpdateHarvesters",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
      void Collect() => update.Invoke(fleet, new object[] { 0, new GameTime() });
      Collect();
      if (grid.Gems[nearby].ClaimState != 0 || home.ClaimedGems.Count != 0)
        throw new Exception("Home Base must not collect before its unlock");
      var apply = typeof(UpgradeManager).GetMethod("ApplyUpgradeEffect",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
      apply.Invoke(manager, new object[]
      {
        new UpgradeData { UpgradeDefinition = new JsonUpgrade { ShortName = "HB", Type = "bool" } },
        new UpgradeDataLevel { m_upgradesToBool = true }
      });
      Collect();
      if (!manager.UG.HomeBaseCollector || manager.UG.HarvesterCount != 0
        || grid.Gems[nearby].ClaimState != 1 || grid.Gems[distant].ClaimState != 0
        || !home.ClaimedGems.SequenceEqual(new[] { 300 }) || !home.ForceInstantCollection)
        throw new Exception("Home Base purchase must activate local collection at its live position without granting a ship");
    }
    finally { UpgradeManager.Instance = previousManager; }
  }

  private static void CheckHomeBaseLaunchInterception()
  {
    var previousManager = UpgradeManager.Instance;
    var previousCollected = UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected;
    double previousClock = Gem.FlightClock;
    try
    {
      Gem.FlightClock = 10.0;
      var manager = new UpgradeManager();
      manager.UG.HomeBase = true;
      manager.UG.HomeBaseCollector = true;
      manager.UG.HomebaseCollectionRange = 3.0f;

      var fleet = new FleetProbe();
      using var world = new WorldBuilder().AddSystem(fleet).Build();
      var homePos = new Vector2(-240f, 0f);
      var homeEntity = world.CreateEntity();
      homeEntity.Attach(new Transform2(homePos));
      var home = new Harvester
      {
        Entity = homeEntity, Id = homeEntity.Id, Type = Harvester.HarvesterType.HomeBase,
        CurrentState = Harvester.HarvesterState.None
      };
      home.SetCollisionPosition(homePos, 64f);
      homeEntity.Attach(home);
      fleet.HomeBaseHarvester = home;
      world.Update(new GameTime());

      var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
      var update = typeof(HarvesterCollectionSystem).GetMethod("UpdateHarvesters", flags)!;
      var resolve = typeof(HarvesterCollectionSystem).GetMethod("ResolveClaimedGems", flags)!;
      typeof(HarvesterCollectionSystem).GetField("gemCountThisFrame", flags)!.SetValue(fleet, 1);

      var spawnPos = new Vector2(190f, 0f);
      var throughTarget = new Vector2(-600f, 0f);
      var launchVelocity = (throughTarget - spawnPos) * Gem.LaunchDamping;

      var gemEntity = world.CreateEntity();
      gemEntity.Attach(new Transform2(spawnPos));
      var gem = new Gem();
      gem.Initialize(gemEntity, 18f, 5u);
      gem.ApplyLaunch(spawnPos, launchVelocity, null, shaderGlide: true,
        out var cpuVelocity, out float availableAt);
      gemEntity.Attach(gem);
      var grid = fleet.flatSpatialHash;
      gem.GridIndex = grid.AddGem(gemEntity.Id, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y,
        gem.BaseValue, gem.CollectionRadius, availableAt);
      gem.ConfigureSpawnerTraits(false, false, cpuVelocity);
      world.Update(new GameTime());

      float reach = BaseStats.GetHarvesterCollectionRange(home) + gem.CollectionRadius;
      if (cpuVelocity != Vector2.Zero
        || Vector2.Distance(gem.BoundingCircle.Center, homePos) > reach
        || availableAt <= (float)Gem.FlightClock)
        throw new Exception("GPU-launched gem crossing Home Base range must land inside the range with an arrival delay");

      // While still flying between the planet and the Home Base boundary, it must not be claimed early.
      update.Invoke(fleet, new object[] { 0, new GameTime() });
      if (home.ClaimedGems.Count != 0 || grid.Gems[gem.GridIndex].ClaimState != 0)
        throw new Exception("Home Base must wait for the launched gem's flight to enter its collection radius");

      // Once FlightClock reaches availableAt, Home Base claims and collects the gem inside its radius.
      Gem.FlightClock = availableAt + 0.001;
      update.Invoke(fleet, new object[] { 0, new GameTime() });
      if (!home.ClaimedGems.SequenceEqual(new[] { gemEntity.Id }))
        throw new Exception("Home Base must claim the intercepted gem once its glide enters the collection radius");
      resolve.Invoke(fleet, new object[] { home });
      if (!gem.PickedUp || Vector2.Distance(gem.BoundingCircle.Center, homePos) > reach + 1f)
        throw new Exception("Collecting the intercepted gem must hand off its flight inside the Home Base collection radius");

      // Gems launched away from the Home Base must not be intercepted.
      var awayVelocity = (new Vector2(500f, 0f) - spawnPos) * Gem.LaunchDamping;
      if (Gem.TryInterceptHomeBase(spawnPos, spawnPos + awayVelocity / Gem.LaunchDamping, gem.CollectionRadius, out _, out _))
        throw new Exception("Gems launched away from Home Base must keep their original landing point");
    }
    finally
    {
      Gem.FlightClock = previousClock;
      UpgradeManager.Instance = previousManager;
      UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected = previousCollected;
    }
  }
}
