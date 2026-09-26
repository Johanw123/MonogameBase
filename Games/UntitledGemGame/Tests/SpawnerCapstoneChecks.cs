using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class SpawnerCapstoneChecks
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

  private sealed class TestSpawnerAbility : GemSpawnerAbility
  {
    public override void Activate() => ActivateAt(Vector2.Zero, 100f);
  }

  private sealed class Fixture : IDisposable
  {
    private readonly EntityFactory previousFactory = EntityFactory.Instance;
    private readonly UpgradeManager previousManager = UpgradeManager.Instance;
    private readonly List<Gem> gems = new();
    public readonly UpgradeManager Manager = new();
    public readonly FleetProbe Fleet = new();
    public readonly Queue<GemSpawnData> Queue = new();
    public readonly GemSpawnerAbility Ability = new TestSpawnerAbility();
    public readonly World World;
    public Fixture()
    {
      World = new WorldBuilder().AddSystem(Fleet).Build();
      var factory = (EntityFactory)RuntimeHelpers.GetUninitializedObject(typeof(EntityFactory));
      typeof(EntityFactory).GetField("_gemSpawnQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(factory, Queue);
      EntityFactory.Instance = factory;
      Manager.UGA.GemSpawnerNrGems = 8;
      Manager.UGA.GemSpawnerNumberOfRings = 3;
      Manager.UGA.GemSpawnerRingReduction = 50;
      Manager.UG.LuckyGems = false;
    }
    public Entity Add(Vector2 position, uint value = 100, Gem reused = null)
    {
      var entity = World.CreateEntity();
      entity.Attach(new Transform2(position));
      var gem = reused ?? new Gem();
      if (reused == null) gems.Add(gem);
      gem.Initialize(entity, 18, value);
      gem.GridIndex = Fleet.flatSpatialHash.AddGem(entity.Id, position.X, position.Y, value);
      entity.Attach(gem);
      World.Update(new GameTime());
      return entity;
    }
    public void Tick(float dt) => Ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(dt)));
    public void Dispose()
    {
      Ability.Cancel();
      foreach (var gem in gems) gem.Reset();
      World.Dispose();
      SpawnerEffects.Clear();
      EntityFactory.Instance = previousFactory;
      UpgradeManager.Instance = previousManager;
    }
  }

  public static void Run()
  {
    ScaledRanges();
    SpiralAndCancellation();
    BloomAndCollection();
    MidasAndReuse();
    CombinedAndMerger();
    MulticastCapstones();
    StaggeredSpiralTiming();
    Console.WriteLine("Spawner capstone checks passed: scheduled rings, seed payouts, gilding limits, pooled reuse, merger behavior and cancellation.");
  }

  private static void ScaledRanges()
  {
    using var f = new Fixture();
    var center = new Vector2(200, -100);
    foreach (bool spiral in new[] { false, true })
    {
      f.Manager.UGA.GemSpawnerGenesisSpiral = spiral;
      f.Manager.UGA.GemSpawnerCrystalBloom = true;
      foreach (float radius in new[] { 100f, 600f, 1800f })
      {
        f.Queue.Clear();
        f.Ability.ActivateAt(center, radius);
        f.Tick(2f);
        Check(f.Queue.Count == (spiral ? 22 : 14)
          && f.Queue.All(g => Vector2.Distance(g.Position, center) >= radius + MathF.Max(74f, radius * 0.24f)),
          "Normal rings, seeds and Spiral finales must keep a growing margin outside collection reach");
      }
    }
    f.Ability.Cancel();
    f.Manager.UGA.GemSpawnerGenesisSpiral = false;
    f.Manager.UGA.GemSpawnerMidasPulse = true;
    var inside = f.Add(center + new Vector2(1100, 0)).Get<Gem>();
    var outside = f.Add(center + new Vector2(1250, 0)).Get<Gem>();
    f.Ability.ActivateAt(center, 600f);
    Check(SpawnerEffects.Pulses.Single().EndRadius == 1200f,
      "Midas visual must expand to twice a large homebase's collection radius");
    f.Tick(0.4f);
    Check(!inside.IsGilded, "Expanded Midas must wait until its visible wave reaches the gem");
    f.Tick(0.4f);
    Check(inside.IsGilded && !outside.IsGilded,
      "Midas targeting and wave arrival must use the same expanded range");
  }

  private static void SpiralAndCancellation()
  {
    using var f = new Fixture();
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    Check(f.Queue.Count == 14 && f.Queue.All(g => !g.IsBloomSeed && g.LaunchVelocity == Vector2.Zero),
      "Ordinary Spawner must still emit all normal rings immediately");
    uint baseValue = BaseStats.GetCurrentGemValue();
    f.Queue.Clear();
    f.Manager.UGA.GemSpawnerGenesisSpiral = true;
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    Check(f.Queue.Count == 0 && f.Ability.DurationTimeMax == 0, "Spiral must charge without delaying cooldown start");
    f.Tick(0.31f);
    Check(f.Queue.Count == 8, "First Spiral pulse must use the full gem count");
    f.Tick(0.19f);
    Check(f.Queue.Count == 12, "Second Spiral pulse must apply ring loss");
    var firstTwo = f.Queue.ToArray();
    Check(firstTwo[0].LaunchVelocity != Vector2.Zero
      && Vector2.Distance(Vector2.Normalize(firstTwo[0].Position), Vector2.Normalize(firstTwo[8].Position)) > 0.1f,
      "Successive rings must rotate and impart outward movement");
    f.Tick(0.14f);
    Check(f.Queue.Count == 14, "Third ring must retain normal yield");
    f.Tick(0.12f);
    Check(f.Queue.Count == 22 && f.Queue.Skip(14).All(g => g.BaseValue == baseValue * 2),
      "Finale must add a full first-ring count at double value");
    f.Queue.Clear();
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    f.Ability.Cancel();
    f.Tick(10f);
    Check(f.Queue.Count == 0 && SpawnerEffects.Pulses.Count == 0, "Cancellation must remove delayed rings and visuals");
    f.Manager.UGA.GemSpawnerRingReduction = 0;
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    f.Ability.ActivateAt(new(1000, 0), 100f);
    f.Tick(2f);
    Check(f.Queue.Count == 64, "Overlapping Spirals must preserve independent casts and zero-loss rings");
  }

  private static void BloomAndCollection()
  {
    using var f = new Fixture();
    f.Manager.UGA.GemSpawnerCrystalBloom = true;
    f.Manager.UGA.GemSpawnerRichVeins = 100;
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    uint baseValue = BaseStats.GetCurrentGemValue();
    Check(f.Queue.Count == 14 && f.Queue.Count(g => g.IsBloomSeed) == 3
      && f.Queue.Where(g => g.IsBloomSeed).All(g => g.BaseValue == baseValue * 8 && g.IsLucky),
      "Bloom must replace exactly three gems; Rich Veins must double their four-gem contents");
    f.Queue.Clear();
    var entity = f.Add(new(100, 100), 43);
    var seed = entity.Get<Gem>();
    seed.ConfigureSpawnerTraits(true, true);
    var shipEntity = f.World.CreateEntity();
    shipEntity.Attach(new Transform2(new Vector2(100, 100)));
    var ship = new Harvester { Id = shipEntity.Id, Entity = shipEntity, Type = Harvester.HarvesterType.Drone };
    shipEntity.Attach(ship);
    f.World.Update(new GameTime());
    var collected = UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected;
    try
    {
      f.Fleet.CollectGem(seed, ship);
      Check(seed.PickedUp && ship.CarryingGemBaseValue == 0 && ship.CarryingGemCount == 0
        && f.Queue.Count == 4 && f.Queue.Sum(g => (long)g.BaseValue) == 43,
        "Collecting a seed must pay only through its contents, preserving fractional splits exactly");
      Check(f.Queue.All(g => !g.IsBloomSeed && g.IsGilded && g.LaunchVelocity != Vector2.Zero),
        "Children must inherit gilding and burst outward, without becoming recursive seeds");
      f.Fleet.CollectGem(seed, ship);
      Check(!seed.TryBloom() && f.Queue.Count == 4, "Repeated collection must not reopen seeds");
    }
    finally { UntitledGemGame.Screens.UntitledGemGameGameScreen.Collected = collected; }
    seed.Reset();
    Check(!seed.IsBloomSeed && !seed.IsGilded && seed.LaunchVelocity == Vector2.Zero
      && !SpawnerEffects.Seeds.Contains(seed), "Pool reset must clear seed, gold and movement state");
  }

  private static void MidasAndReuse()
  {
    using var f = new Fixture();
    f.Manager.UGA.GemSpawnerMidasPulse = true;
    var gems = new List<Gem>();
    for (int i = 0; i < 150; ++i) gems.Add(f.Add(new(100 + i, 50)).Get<Gem>());
    var far = f.Add(new(700, 0)).Get<Gem>();
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    var later = f.Add(new(10, 10)).Get<Gem>();
    f.Tick(0.8f);
    Check(gems.Count(g => g.IsGilded) == 128 && gems.Where(g => g.IsGilded).All(g => g.BaseValue == 200)
      && !far.IsGilded && !later.IsGilded, "Midas must target at most 128 pre-existing gems inside its range");
    Check(gems.Where(g => g.IsGilded).All(g => !g.TryGild())
      && gems.All(g => f.Fleet.flatSpatialHash.Gems[g.GridIndex].BaseValue == g.BaseValue),
      "Gilding must be once per gem and keep indexed values synchronized");
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    f.Ability.Cancel();
    f.Tick(1f);
    Check(!later.IsGilded, "Cancelling Midas must stop its pending wave");

    // A wave targeting a gem cannot gild a new lifetime occupying its old slot.
    f.Ability.ActivateAt(new(700, 0), 100f);
    int oldId = far.Id;
    f.Fleet.flatSpatialHash.RecycleIndex(far.GridIndex);
    f.Fleet.GetEntityP(oldId).Destroy();
    far.Reset();
    f.World.Update(new GameTime());
    f.Add(new(700, 0), reused: far);
    f.Tick(1f);
    Check(!far.IsGilded && far.BaseValue == 100, "Old Midas targets must not affect reused components");
  }

  private static void StaggeredSpiralTiming()
  {
    using var f = new Fixture();
    f.Manager.UGA.GemSpawnerGenesisSpiral = true;
    f.Manager.UGA.GemSpawnerCrystalBloom = true;
    f.Ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, 0);
    f.Tick(0.29f);
    Check(f.Queue.Count == 0, "Spirals must charge before releasing rings");
    f.Tick(0.02f);
    Check(f.Queue.Count == 8, "Only the first Spiral may release its first ring at 0.3 seconds");
    f.Tick(0.25f);
    Check(f.Queue.Count == 20, "The second Spiral must begin a quarter second after the first");
    f.Tick(2f);
    Check(f.Queue.Count == 110 && f.Queue.Count(g => g.IsBloomSeed) == 15,
      "Staggering must preserve all five finales and seed budgets");
  }

  private static void MulticastCapstones()
  {
    foreach (var (roll, casts) in new[] { (70d, 2), (45d, 3), (25d, 4), (0d, 5) })
    {
      using var f = new Fixture();
      f.Manager.UGA.GemSpawnerCrystalBloom = true;
      f.Manager.UGA.GemSpawnerGenesisSpiral = true;
      f.Manager.UGA.GemSpawnerMidasPulse = true;
      f.Manager.UGA.GemSpawnerRichVeins = 100;
      var targets = new List<Gem>();
      for (int i = 0; i < 700; ++i) targets.Add(f.Add(new(100 + i % 30, 50 + i / 30)).Get<Gem>());
      Check(f.Ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, roll) == casts,
        "Spawner multicast must use the same one-roll dispatcher as the HUD");
      f.Tick(2f);
      Check(f.Queue.Count == 22 * casts && f.Queue.Count(g => g.IsBloomSeed) == 3 * casts,
        "Each multicast must retain its own Spiral finale and full Bloom seed budget");
      Check(targets.Count(g => g.IsGilded) == GemSpawnerAbility.MidasLimit * casts
        && targets.All(g => g.BaseValue == (g.IsGilded ? 200u : 100u)),
        "Multicast Midas must reserve distinct targets without repeatedly doubling the same gems");
      Check(f.Ability.CooldownTime == f.Ability.MaxCooldownTime && f.Ability.DurationTime == 0,
        "Spawner multicast must start one cooldown for the entire batch");
      f.Queue.Clear();
      f.Ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, roll);
      f.Ability.Cancel();
      f.Tick(2f);
      Check(f.Queue.Count == 0 && targets.Count(g => g.IsGilded) == GemSpawnerAbility.MidasLimit * casts,
        "Cancel must clear all multicast rings and Midas reservations before another wave lands");
      // After cancellation, reserved-but-ungilded gems must be eligible again.
      f.Ability.Activate();
      f.Tick(2f);
      Check(targets.Count(g => g.IsGilded) == Math.Min(700, GemSpawnerAbility.MidasLimit * (casts + 1)),
        "Cancelled reservations must not permanently exclude remaining gems");
    }
  }

  private static void CombinedAndMerger()
  {
    using var f = new Fixture();
    f.Manager.UGA.GemSpawnerCrystalBloom = true;
    f.Manager.UGA.GemSpawnerGenesisSpiral = true;
    f.Manager.UGA.GemSpawnerMidasPulse = true;
    f.Manager.UGA.GemSpawnerRichVeins = 100;
    f.Ability.ActivateAt(Vector2.Zero, 100f);
    f.Tick(2f);
    Check(f.Queue.Count == 22 && f.Queue.Count(g => g.IsBloomSeed) == 3
      && f.Queue.Skip(14).All(g => g.BaseValue == BaseStats.GetCurrentGemValue() * 4),
      "All capstones must stack with Rich Veins while retaining one seed budget per activation");
    f.Queue.Clear();
    for (int i = 0; i < 5; ++i)
      f.Add(new(100 + i, 100)).Get<Gem>().ConfigureSpawnerTraits(true, false);
    f.Fleet.ProcessGemMergers(f.Fleet.flatSpatialHash, 20f);
    Check(f.Queue.Count == 0, "Gem mergers must leave unopened seeds intact");
    for (int i = 0; i < 5; ++i)
      f.Add(new(500 + i, 500)).Get<Gem>().ConfigureSpawnerTraits(false, i == 0);
    f.Fleet.ProcessGemMergers(f.Fleet.flatSpatialHash, 20f);
    Check(f.Queue.Count == 1 && f.Queue.Peek().IsGilded,
      "Merged gilded value must retain its once-only gilding marker");
  }
}
