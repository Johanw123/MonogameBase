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
}
