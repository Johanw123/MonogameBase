using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class ConstellationChecks
{
  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime gameTime) { }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    var hull = ConstellationNet.BuildHull(new()
    {
      new(10, 10), new(0, 0), new(5, 5), new(0, 10), new(10, 0), new(0, 0)
    });
    Check(hull.Length == 4 && ConstellationNet.Contains(hull, new(5, 5))
      && ConstellationNet.Contains(hull, new(0, 5)) && !ConstellationNet.Contains(hull, new(-1, 5)),
      "Net hull must ignore ordering, duplicates and interior anchors, and include its boundary");
    Check(ConstellationNet.BuildHull(new() { new(0, 0), new(1, 1), new(2, 2) }).Length == 0
      && ConstellationNet.BuildHull(new() { new(1, 1), new(1, 1), new(1, 1) }).Length == 0,
      "Collinear and coincident anchors must not create a net");
    foreach (int capacity in new[] { 256, 384, 512, 640 }) BoundedCapture(capacity);
    OverlapAndWaves();
    ReactionAndCompletion();
    MulticastNets();
    DegenerateTargets();
    Console.WriteLine("Constellation checks passed: geometry, bounded capture, synchronized pull, charge, overlap, waves, cancellation and pooled reuse.");
  }

  private sealed class Fixture : IDisposable
  {
    public readonly UpgradeManager Manager = new();
    public readonly FleetProbe Fleet = new();
    public readonly World World;
    public readonly ChainLightningAbility Ability = new();
    public Fixture()
    {
      World = new WorldBuilder().AddSystem(Fleet).Build();
      Manager.UGA.ChainMagnetizerConstellation = true;
      Manager.UGA.ChainMagnetizerCount = 4;
    }
    public Entity Add(Vector2 position, Gem reused = null)
    {
      var entity = World.CreateEntity();
      entity.Attach(new Transform2(position));
      var gem = reused ?? new Gem();
      gem.Initialize(entity, 18, 100);
      gem.GridIndex = Fleet.flatSpatialHash.AddGem(entity.Id, position.X, position.Y, 100);
      entity.Attach(gem);
      World.Update(new GameTime());
      return entity;
    }
    public void Square(float offset)
    {
      Add(new(offset, offset)); Add(new(offset + 400, offset));
      Add(new(offset + 400, offset + 400)); Add(new(offset, offset + 400));
    }
    public void Tick(float dt) => Ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(dt)));
    public void Dispose() { Ability.Cancel(); World.Dispose(); }
  }

  private static void BoundedCapture(int capacity)
  {
    using var f = new Fixture();
    f.Manager.UGA.ConstellationCapacity = capacity;
    f.Manager.UGA.ChainResidualCharge = 20;
    var home = (HomeBase)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(HomeBase));
    Check(home.GetAbilityDescription(f.Ability).Contains($"up to {capacity} extra gems"),
      "Constellation tooltip must show upgraded capacity");
    f.Square(1000);
    var interior = new List<Entity>();
    for (int i = 0; i < capacity + 44; ++i) interior.Add(f.Add(new(1100 + i % 20, 1100 + i / 20)));
    var outside = f.Add(new(2000, 2000));
    var reserved = f.Add(new(1200, 1200));
    var grid = f.Fleet.flatSpatialHash;
    grid.TryClaim(reserved.Get<Gem>().GridIndex);
    grid.RemoveFromQueries(reserved.Get<Gem>().GridIndex);
    f.Ability.Activate();
    Check(ChainLightningAbility.Constellations.Count == 1, "One activation must make one net");
    var caught = interior.Where(e => grid.Gems[e.Get<Gem>().GridIndex].ClaimState == 1).ToArray();
    Check(caught.Length == capacity && ChainLightningAbility.TargetLines.Count == 4,
      "Net must cap extra captures and render no per-capture chain lines");
    Check(caught.All(e => e.Get<Gem>().BaseValue == 120)
      && grid.Gems[outside.Get<Gem>().GridIndex].ClaimState == 0 && reserved.Get<Gem>().BaseValue == 100,
      "Net captures must get full Residual Charge without stealing foreign claims or outside gems");
    var moving = caught[0].Get<Gem>();
    var before = moving.BoundingCircle.Center;
    f.Tick(0.3f);
    Check(moving.BoundingCircle.Center == before, "Net gems must hold still during stitching");
    f.Tick(0.35f);
    Check(moving.BoundingCircle.Center != before && ChainLightningAbility.Constellations[0].Collapse > 0,
      "Captured gems and outline must collapse together after windup");

    // Reuse a captured component and slot while the old net is still collapsing.
    grid.RecycleIndex(moving.GridIndex);
    caught[0].Destroy();
    moving.Reset();
    f.World.Update(new GameTime());
    var replacement = f.Add(new(3000, 3000), moving);
    grid.TryClaim(moving.GridIndex);
    grid.RemoveFromQueries(moving.GridIndex);
    f.Tick(0.1f);
    f.Ability.Cancel();
    Check(replacement.Get<Transform2>().Position == new Vector2(3000, 3000)
      && grid.Gems[moving.GridIndex].ClaimState == 1 && grid.Gems[reserved.Get<Gem>().GridIndex].ClaimState == 1,
      "Cancelling a net must not move or release a reused gem or another collector's claim");
    Check(ChainLightningAbility.Constellations.Count == 0 && ChainLightningAbility.TargetLines.Count == 0
      && grid.AvailableCount == capacity + 48, "Cancellation must clear net visuals and release every remaining capture");
    f.Manager.UGA.Reset("CMCapacity");
    Check(ConstellationNet.CaptureLimit == 256, "Refunding capacity must restore the base capture limit");
  }

  private static void OverlapAndWaves()
  {
    using var f = new Fixture();
    f.Square(1000);
    f.Add(new(1200, 1200));
    f.Manager.UGA.ChainMagnetizerAftershock = true;
    f.Manager.UGA.ChainMagnetizerSuperconductor = true;
    f.Manager.UGA.ChainMagnetizerAftershockChance = 10000;
    f.Ability.Activate();
    f.Tick(0.2f);
    f.Square(2000);
    f.Add(new(2200, 2200));
    f.Ability.Activate();
    Check(ChainLightningAbility.Constellations.Count == 2, "Overlapping casts must own independent nets");
    for (int i = 0; i < 40; ++i) f.Add(new(4000 + i, 4000));
    f.Tick(0.61f);
    Check(ChainLightningAbility.Constellations.Count == 2 && ChainLightningAbility.TargetLines.Count == 12,
      "Only original primary anchors may trigger aftershocks; captured gems must not schedule waves or nets");
    f.Tick(0.8f);
    Check(ChainLightningAbility.Constellations.Count == 1, "Older net must expire independently of the newer net");
    f.Ability.Cancel();
    f.Tick(2f);
    Check(ChainLightningAbility.Constellations.Count == 0 && ChainLightningAbility.TargetLines.Count == 0
      && f.Fleet.flatSpatialHash.AvailableCount == 50, "Cancellation must suppress pending waves and release both nets");
  }

  private static void MulticastNets()
  {
    foreach (var (roll, casts) in new[] { (70d, 2), (45d, 3), (25d, 4), (0d, 5) })
    {
      using var f = new Fixture();
      f.Manager.UGA.ChainMagnetizerChainReaction = true;
      f.Manager.UGA.ChainMagnetizerAftershock = true;
      f.Manager.UGA.ChainMagnetizerSuperconductor = true;
      f.Manager.UGA.ChainMagnetizerAftershockChance = 10000;
      for (int i = 0; i < 2000; ++i)
      {
        float angle = i * MathHelper.TwoPi / 2000;
        f.Add(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20000);
      }
      // Two 5x activations overlap: a renderer budget must not suppress nets 9/10.
      for (int batch = 0; batch < 2; ++batch)
        Check(f.Ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, roll) == casts,
          "Multicast must dispatch the rolled number of primary activations");
      Check(ChainLightningAbility.Constellations.Count == 2,
        "Each overlapping multicast must create only its first net immediately");
      f.Tick((float)((casts - 1) * IHomeBaseAbility.MulticastIntervalSeconds));
      Check(ChainLightningAbility.Constellations.Count == casts * 2 && f.Ability.DurationTime == 0
        && f.Ability.CooldownTime == f.Ability.MaxCooldownTime,
        "All multicast nets must exist while the ability recharges once");
      Check(ChainLightningAbility.Constellations.Count == casts * 2,
        "Reaction and Superconductor must not replace or discard concurrent nets");
      f.Ability.Cancel();
      f.Tick(2f);
      Check(ChainLightningAbility.Constellations.Count == 0 && ChainLightningAbility.TargetLines.Count == 0
        && f.Fleet.flatSpatialHash.AvailableCount == 2000,
        "Cancelling multicast must release every chain/net and suppress every scheduled wave");
    }
  }

  private static void DegenerateTargets()
  {
    using var f = new Fixture();
    for (int i = 0; i < 4; ++i) f.Add(new(1000 + i * 100, 1000));
    f.Ability.Activate();
    Check(ChainLightningAbility.Constellations.Count == 0 && ChainLightningAbility.TargetLines.Count == 4,
      "Collinear targets must fall back to ordinary chains");
    f.Tick(2f);
    Check(f.Fleet.flatSpatialHash.AvailableCount == 4, "Ordinary fallback chains must finish normally");
  }

  private static void ReactionAndCompletion()
  {
    using var f = new Fixture();
    f.Manager.UGA.ChainMagnetizerChainReaction = true;
    f.Square(1000);
    var branch = f.Add(new(990, 1000));
    var center = f.Add(new(1200, 1200));
    f.Ability.Activate();
    var net = ChainLightningAbility.Constellations.Single();
    Check(net.Hull.Length == 4 && net.Hull.All(point => point.X >= 1000)
      && ChainLightningAbility.TargetLines.ContainsKey(branch.Id)
      && !ChainLightningAbility.TargetLines.ContainsKey(center.Id),
      "Reaction branches must coexist outside the net without becoming primary anchors");
    f.Tick(1.4f);
    Check(ChainLightningAbility.TargetLines.Count == 0 && f.Fleet.flatSpatialHash.AvailableCount == 6
      && ChainLightningAbility.Constellations.Count == 1,
      "All branches and captures must release at home while the final flash fades");
    f.Tick(0.21f);
    Check(ChainLightningAbility.Constellations.Count == 0,
      "The impact flash must expire even when no active chain remains");
    f.Manager.UGA.ChainMagnetizerConstellation = false;
    f.Ability.Activate();
    Check(ChainLightningAbility.Constellations.Count == 0,
      "Unpurchased Constellation must leave ordinary chain behavior intact");
  }
}
