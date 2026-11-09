using Microsoft.Xna.Framework;
using UntitledGemGame;

// The belt over a bare spatial index: boarding claims a slot, riding moves it, and
// deliveries record the value multiplier the game would pay.
internal sealed class GridBeltCargo(GemSpatialIndex grid) : ITractorBeltCargo
{
  public readonly GemSpatialIndex Grid = grid;
  public readonly Dictionary<int, double> Delivered = new();
  public readonly List<int> DeliveryOrder = new();
  public readonly Dictionary<int, float> Scales = new();
  public readonly HashSet<int> Gone = new();
  public int Dropped;

  public bool TryBoard(int index, out BeltRider rider, out Vector2 position)
  {
    rider = default;
    position = default;
    if (!Grid.IsAvailable(index)) return false;
    ref var gem = ref Grid.Gems[index];
    gem.ClaimState = 2;
    Grid.RemoveFromQueries(index);
    rider = new BeltRider(index, gem.EntityId, 0);
    position = new Vector2(gem.X, gem.Y);
    return true;
  }

  public bool Ride(in BeltRider rider, Vector2 position, float scale)
  {
    if (Gone.Contains(rider.GridIndex)) return false;
    Grid.MoveGem(rider.GridIndex, position.X, position.Y);
    Scales[rider.GridIndex] = scale;
    return true;
  }

  public void Deliver(in BeltRider rider, double valueMultiplier)
  {
    Delivered[rider.GridIndex] = valueMultiplier;
    DeliveryOrder.Add(rider.GridIndex);
  }

  public void Drop(in BeltRider rider) => Dropped++;
}

internal static class TractorBeltChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private static void Near(double actual, double expected, string message)
    => Check(Math.Abs(actual - expected) < 0.001, $"{message}: {actual} != {expected}");

  private static void Run(TractorBelt belt, float seconds, GemSpatialIndex grid, GridBeltCargo cargo,
    UpgradesGeneratorUpgrades upgrades, Vector2? planet = null, float planetRadius = 0)
  {
    var stats = TractorBeltStats.From(upgrades, 19);
    for (float t = 0; t < seconds - 1e-4f; t += 0.05f)
      belt.Update(Math.Min(0.05f, seconds - t), grid, cargo, stats, Vector2.Zero, planet, planetRadius);
  }

  public static void Run()
  {
    Placement();
    Highlights();
    Carrying();
    Lifetime();
    Intakes();
    Retracting();
    Routing();
    Freight();
    Sweeping();
    Teardown();
    // The belt's look: compiled the way the game does at runtime on Linux.
    if (GpuParticleChecks.Compile("Content/Shaders/TractorBelt.fx").Length == 0)
      throw new Exception("TractorBelt.fx compiles to an empty effect");
    Console.WriteLine("Tractor belt checks passed: placement, dragging, catch ring, loading, delivery, run time, intakes, routing, freight, sweeping and teardown.");
  }

  private static void Placement()
  {
    var upgrades = new UpgradesGeneratorUpgrades();
    var belt = new TractorBelt();
    var locked = TractorBeltStats.From(upgrades, 19);
    Check(!belt.HandleInput(true, true, true, new Vector2(300, 0), locked) && belt.Lines.Count == 0,
      "The tractor belt requires its unlock");
    upgrades.TractorBeltEnabled = true;
    var stats = TractorBeltStats.From(upgrades, 19);
    Check(stats.Radius == 57 && stats.Speed == TractorBelt.BaseSpeed && stats.Capacity == 1 && stats.Duration == 15
      && stats.MaxIntakes == 1 && stats.ValueMultiplier == 1 && !stats.Permanent,
      "Base belt: 3x click radius intake, one gem per pod, 15 second placements, one intake");
    Check(!belt.HandleInput(true, true, false, new Vector2(300, 0), stats) && belt.Lines.Count == 0,
      "Menus and the HUD block placement");
    Check(belt.HandleInput(true, true, true, new Vector2(300, 0), stats) && belt.Lines.Count == 1
      && belt.Lines[0].Intake == new Vector2(300, 0) && belt.Lines[0].Remaining == 15 && belt.Grabbed == belt.Lines[0],
      "A right press places an intake with a full run time");
    Check(!belt.HandleInput(false, true, true, new Vector2(320, 10), stats) && belt.Lines[0].Intake == new Vector2(320, 10),
      "Holding right drags the intake");
    belt.HandleInput(false, false, true, new Vector2(500, 0), stats);
    Check(belt.Grabbed == null && belt.Lines[0].Intake == new Vector2(320, 10), "Releasing right anchors the intake");
    belt.Update(5, null, null, stats, Vector2.Zero);
    Check(belt.HandleInput(true, true, true, new Vector2(330, 10), stats) && belt.Lines.Count == 1
      && belt.Lines[0].Remaining == 15 && belt.Lines[0].Intake == new Vector2(330, 10),
      "Pressing on the intake grabs it again and restarts its run time");
    belt.HandleInput(false, true, false, new Vector2(0, 300), stats);
    Check(belt.Grabbed == null && belt.Lines[0].Intake == new Vector2(330, 10), "Opening a menu mid-drag drops the intake");
    upgrades.TractorBeltRadiusMultiplier = 5.5f;
    upgrades.TractorBeltSpeedMultiplier = 2;
    upgrades.TractorBeltCapacity = 6;
    upgrades.TractorBeltDuration = 40;
    upgrades.TractorBeltValue = 0.5f;
    stats = TractorBeltStats.From(upgrades, 38);
    Check(stats.Radius == 209 && stats.Speed == TractorBelt.BaseSpeed * 2 && stats.Capacity == 6 && stats.Duration == 40
      && stats.ValueMultiplier == 1.5, "Belt ranks stack with click radius");
  }

  private static void Highlights()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.Update(0.01f, null, null, stats, Vector2.Zero);
    var line = belt.Lines[0];
    Check(line.Highlight == 1, "Placing an intake shows its catch ring at once");
    belt.UpdateHighlights(1, new Vector2(0, 500), stats.Radius, 10);
    Check(line.Highlight == 1, "The ring stays while the intake is held");
    belt.HandleInput(false, false, true, new Vector2(0, 500), stats);
    belt.UpdateHighlights(0.1f, new Vector2(0, 500), stats.Radius, 10);
    Check(line.Highlight > 0 && line.Highlight < 1, "After placing, the ring fades out");
    belt.UpdateHighlights(1, null, stats.Radius, 10);
    Check(line.Highlight == 0, "Away from the belt the ring is hidden");
    belt.UpdateHighlights(1, new Vector2(150, 8), stats.Radius, 10);
    Check(line.Highlight == 1, "Pointing at the belt shows the ring");
    belt.UpdateHighlights(1, new Vector2(150, 30), stats.Radius, 10);
    Check(line.Highlight == 0, "Beyond reach of the belt it hides again");
    belt.UpdateHighlights(1, new Vector2(330, 40), stats.Radius, 10);
    Check(line.Highlight == 1, "Pointing inside the catch area shows the ring");
  }

  private static void Carrying()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var grid = new GemSpatialIndex(16, 30);
    int near = grid.AddGem(1, 312, 0, 1);
    int far = grid.AddGem(2, 340, 0, 1);
    int outside = grid.AddGem(3, 370, 0, 1);
    int reserved = grid.AddGem(4, 302, 0, 1);
    grid.Gems[reserved].ClaimState = 1;
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, new Vector2(300, 0), stats);
    belt.Update(0.001f, grid, cargo, stats, Vector2.Zero);
    var line = belt.Lines[0];
    Check(line.Route.Count == 2 && Math.Abs(line.RouteLength - 300) < 0.01, "With no planet in the way the belt runs straight home");
    Check(line.Pods.Count == 1 && line.Pods[0].Count == 1 && grid.Gems[near].ClaimState == 2
      && grid.Gems[far].ClaimState == 0, "The first pod leaves at once with the nearest loose gem");
    Check(grid.Gems[reserved].ClaimState == 1 && grid.Gems[outside].ClaimState == 0,
      "The intake leaves harvester reservations and gems outside its radius alone");
    Run(belt, TractorBelt.PodSpacing / stats.Speed, grid, cargo, upgrades);
    Check(line.Pods.Count == 2 && grid.Gems[far].ClaimState == 2, "The next pod leaves one pod spacing later");
    Run(belt, 1f, grid, cargo, upgrades);
    Check(grid.Gems[near].X < 300 && grid.Gems[near].X > TractorBelt.EndReach && cargo.Delivered.Count == 0,
      "Gems ride the belt toward the home base");
    Run(belt, 2f, grid, cargo, upgrades);
    Check(cargo.Delivered.ContainsKey(near) && cargo.Delivered[near] == 1 && cargo.DeliveryOrder[0] == near,
      "Pods deliver their gems at the home base, in order");
    Check(cargo.Scales[near] < 1, "Gems shrink into the home base");
    Run(belt, 0.5f, grid, cargo, upgrades);
    Check(cargo.Delivered.ContainsKey(far) && !cargo.Delivered.ContainsKey(outside) && !cargo.Delivered.ContainsKey(reserved),
      "Only the gems the intake caught are delivered");
  }

  private static void Lifetime()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var grid = new GemSpatialIndex(64, 30);
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, new Vector2(300, 0), stats);
    Run(belt, 13f, grid, cargo, upgrades);
    var line = belt.Lines[0];
    Check(line.Warning && !line.Expired && !belt.TakeExpiredNotification(), "The last seconds warn before the belt shuts off");
    int late = grid.AddGem(1, 300, 0, 1);
    Run(belt, 2.01f, grid, cargo, upgrades);
    Check(line.Expired && belt.TakeExpiredNotification() && !belt.TakeExpiredNotification(),
      "A placement times out once, with one notification");
    Check(grid.Gems[late].ClaimState == 2 && line.Pods.Count > 0, "Pods on the belt are still running home");
    int afterward = grid.AddGem(2, 300, 0, 1);
    Run(belt, 3f, grid, cargo, upgrades);
    Check(cargo.Delivered.ContainsKey(late) && grid.Gems[afterward].ClaimState == 0,
      "A shut-off belt finishes its deliveries but loads nothing new");
    Run(belt, 1f, grid, cargo, upgrades);
    Check(belt.Lines.Count == 0, "A drained belt reels in and is gone");

    upgrades.TractorBeltPermanent = true;
    stats = TractorBeltStats.From(upgrades, 19);
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    Run(belt, 120f, null, cargo, upgrades);
    Check(stats.Permanent && belt.Lines.Count == 1 && !belt.Lines[0].Expired && belt.Lines[0].LifeFraction == 1,
      "Fixed Installation keeps the belt running");
    upgrades.TractorBeltPermanent = false;
    Run(belt, 14.9f, null, cargo, upgrades);
    Check(!belt.Lines[0].Expired, "Losing permanence restarts a full run time");
    Run(belt, 0.2f, null, cargo, upgrades);
    Check(belt.Lines[0].Expired, "...which then runs out");
  }

  private static void Intakes()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    belt.HandleInput(true, true, true, new Vector2(0, 300), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Check(belt.Lines.Count == 2 && belt.Lines[0].Expired && !belt.Lines[1].Expired && belt.ActiveCount == 1,
      "A new placement shuts off the old intake, which still runs its pods home");
    upgrades.TractorBeltBranch = true;
    stats = TractorBeltStats.From(upgrades, 19);
    belt.Reset();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    belt.HandleInput(true, true, true, new Vector2(0, 300), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Check(stats.MaxIntakes == 2 && belt.ActiveCount == 2, "Branch Line runs two intakes");
    belt.HandleInput(true, true, true, new Vector2(300, 5), stats);
    Check(belt.ActiveCount == 2 && belt.Grabbed == belt.Lines[0], "Pressing on an intake grabs it instead of placing");
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    belt.HandleInput(true, true, true, new Vector2(-300, 0), stats);
    Check(belt.ActiveCount == 2 && belt.Lines[0].Expired && !belt.Lines[1].Expired,
      "A third placement shuts off the oldest intake");
  }

  private static void Retracting()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var grid = new GemSpatialIndex(16, 30);
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Run(belt, 0.5f, grid, cargo, upgrades);
    var empty = belt.Lines[0];
    belt.HandleInput(true, true, true, new Vector2(0, 300), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Run(belt, 0.2f, grid, cargo, upgrades);
    Check(empty.Expired && empty.Retracted > 0 && empty.Pods.TrueForAll(pod => empty.RouteLength - pod.DistanceToEnd > empty.Retracted),
      "A replaced belt reels in toward the home base, taking in the empty pods it passes");
    Run(belt, 1f, grid, cargo, upgrades);
    Check(belt.Lines.Count == 1 && !belt.Lines[0].Expired, "An empty belt is fully reeled in within a second");

    // Spamming placements keeps one pod rhythm instead of sending a pod per click.
    belt.Reset();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    Run(belt, 0.05f, grid, cargo, upgrades);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    belt.HandleInput(true, true, true, new Vector2(0, 300), stats);
    Run(belt, 0.05f, grid, cargo, upgrades);
    Check(belt.Lines[0].Pods.Count <= 1 && belt.Lines[^1].Pods.Count == 0, "Re-placing does not send an extra pod");

    belt.Reset();
    int gem = grid.AddGem(1, 300, 0, 1);
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Run(belt, 0.05f, grid, cargo, upgrades);
    var loaded = belt.Lines[0];
    belt.HandleInput(true, true, true, new Vector2(0, 300), stats);
    belt.HandleInput(false, false, true, Vector2.Zero, stats);
    Run(belt, 0.1f, grid, cargo, upgrades);
    var carrier = loaded.Pods.Find(pod => pod.Count > 0);
    Check(carrier != null && loaded.Retracted <= loaded.RouteLength - carrier.DistanceToEnd && belt.Lines.Contains(loaded),
      "A pod with gems holds the reeling end behind it");
    Run(belt, 1.2f, grid, cargo, upgrades);
    Check(cargo.Delivered.ContainsKey(gem) && !belt.Lines.Contains(loaded),
      "A shut-off belt reels its cargo home faster than the belt runs, then is gone");
  }

  private static void Routing()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var stats = TractorBeltStats.From(upgrades, 19);
    var planet = new Vector2(150, 0);
    const float radius = 50;
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    belt.Update(0.01f, null, null, stats, Vector2.Zero, planet, radius);
    var line = belt.Lines[0];
    Check(line.Route.Count > 2 && line.RouteLength > 300, "The belt bends around the planet");
    for (int i = 1; i < line.Route.Count; i++)
      Check(!PlanetObstacle.SegmentHitsCircle(line.Route[i - 1], line.Route[i], planet, radius),
        "No stretch of the belt crosses the planet");
    belt.HandleInput(false, true, true, new Vector2(160, 0), stats);
    belt.Update(0.01f, null, null, stats, Vector2.Zero, planet, radius);
    Check(Vector2.Distance(line.Intake, planet) >= radius, "An intake dragged onto the planet sits on its rim");
  }

  private static void Freight()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true, TractorBeltCapacity = 2, TractorBeltValue = 0.3f };
    var grid = new GemSpatialIndex(16, 30);
    int a = grid.AddGem(1, 300, 0, 1);
    int b = grid.AddGem(2, 305, 0, 1);
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    var stats = TractorBeltStats.From(upgrades, 19);
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    Run(belt, 4f, grid, cargo, upgrades);
    Near(cargo.Delivered[a], 1.3, "Polished Rails raise belt delivery value");
    Near(cargo.Delivered[b], 1.3, "Each pod carries up to its capacity");
    upgrades.TractorBeltFullFreight = true;
    cargo.Delivered.Clear();
    int c = grid.AddGem(3, 300, 0, 1);
    int d = grid.AddGem(4, 302, 0, 1);
    Run(belt, 4f, grid, cargo, upgrades);
    Near(cargo.Delivered[c], 2.6, "Full Freight doubles a full pod");
    Near(cargo.Delivered[d], 2.6, "Every gem in a full pod is doubled");
    belt.Reset();
    belt.HandleInput(true, true, true, new Vector2(300, 0), stats);
    cargo.Delivered.Clear();
    int lone = grid.AddGem(5, 300, 0, 1);
    Run(belt, 4f, grid, cargo, upgrades);
    Near(cargo.Delivered[lone], 1.3, "A pod with room left is not full freight");
  }

  private static void Sweeping()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true };
    var grid = new GemSpatialIndex(16, 30);
    int onBelt = grid.AddGem(1, 150, 3, 1);
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), TractorBeltStats.From(upgrades, 19));
    Run(belt, 2.5f, grid, cargo, upgrades);
    Check(grid.Gems[onBelt].ClaimState == 0, "Without Sweeping Rail the belt passes gems by");
    upgrades.TractorBeltSweep = true;
    Run(belt, 4f, grid, cargo, upgrades);
    Check(cargo.Delivered.ContainsKey(onBelt), "Sweeping Rail fills empty pods on the way home");
  }

  private static void Teardown()
  {
    var upgrades = new UpgradesGeneratorUpgrades { TractorBeltEnabled = true, TractorBeltCapacity = 3 };
    var grid = new GemSpatialIndex(16, 30);
    int swallowed = grid.AddGem(1, 300, 0, 1);
    int kept = grid.AddGem(2, 301, 0, 1);
    int third = grid.AddGem(3, 302, 0, 1);
    var cargo = new GridBeltCargo(grid);
    var belt = new TractorBelt();
    belt.HandleInput(true, true, true, new Vector2(300, 0), TractorBeltStats.From(upgrades, 19));
    Run(belt, 0.1f, grid, cargo, upgrades);
    cargo.Gone.Add(swallowed);
    Run(belt, 0.1f, grid, cargo, upgrades);
    Check(belt.Lines[0].Pods[0].Count == 2, "A gem something else took leaves the belt");
    belt.Reset(cargo);
    Check(belt.Lines.Count == 0 && cargo.Dropped == 2 && cargo.Delivered.Count == 0
      && grid.Gems[kept].ClaimState == 2 && grid.Gems[third].ClaimState == 2,
      "Reset drops the gems aboard and clears the belt");
    upgrades.TractorBeltEnabled = false;
    belt.HandleInput(true, true, true, new Vector2(300, 0), TractorBeltStats.From(upgrades, 19));
    Check(belt.Lines.Count == 0, "A reset run cannot place without the unlock");

    var dense = new GemSpatialIndex(TractorBelt.ScanBudget * 2, 30);
    for (int i = 0; i < dense.MaxCapacity; ++i) dense.AddGem(i, 300, 0, 1);
    upgrades.TractorBeltEnabled = true;
    var crowd = new GridBeltCargo(dense);
    belt.HandleInput(true, true, true, new Vector2(300, 0), TractorBeltStats.From(upgrades, 19));
    Run(belt, 0.01f, dense, crowd, upgrades);
    Check(belt.Lines[0].Pods[0].Count == 3, "Dense piles load a pod's capacity and no more");
  }
}
