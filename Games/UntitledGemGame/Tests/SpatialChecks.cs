extern alias SpatialBaseline;
using OriginalFlatSpatialHash = SpatialBaseline::FlatSpatialHash;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class SpatialChecks
{
  private static void Require(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    CheckCollectionRadii();
    var random = new Random(42);
    var grid = new GemSpatialIndex(1024, 30, 4);
    var live = new HashSet<int>();
    int nextId = 0;
    for (int step = 0; step < 8000; ++step)
    {
      int operation = random.Next(5);
      if (live.Count == 0 || (operation == 0 && live.Count < 1024))
      {
        int index = grid.AddGem(nextId++, random.Next(-1200, 1200), random.Next(-800, 800), (uint)random.Next(1, 1000000));
        Require(index >= 0 && live.Add(index), "Insertion reused a live slot");
      }
      else
      {
        int index = live.ElementAt(random.Next(live.Count));
        if (operation == 1)
          grid.MoveGem(index, random.Next(-1200, 1200), random.Next(-800, 800));
        else if (operation == 2)
        {
          grid.TryClaim(index);
          grid.RemoveFromQueries(index);
        }
        else if (operation == 3)
          grid.ReleaseClaim(index);
        else
        {
          grid.RecycleIndex(index);
          grid.RecycleIndex(index); // Duplicate cleanup must not recycle a slot twice.
          live.Remove(index);
        }
      }

      float x = random.Next(-1200, 1200), y = random.Next(-800, 800), radius = random.Next(1, 250);
      var actual = new HashSet<int>();
      foreach (int index in grid.Query(x, y, radius, radius))
        Require(actual.Add(index), "Query returned a duplicate across cells");
      var expected = live.Where(i => grid.Gems[i].ClaimState == 0
        && Math.Abs(grid.Gems[i].X - x) <= radius && Math.Abs(grid.Gems[i].Y - y) <= radius).ToHashSet();
      Require(expected.SetEquals(actual), $"Spatial query disagrees with brute force at mutation {step}");
      Require(grid.NumActiveGems == live.Count, "Animation slot accounting drifted");
      Require(grid.AvailableCount == live.Count(i => grid.Gems[i].ClaimState == 0), "Queryable population drifted");
      Require(grid.AvailableIndices.ToArray().ToHashSet().SetEquals(live.Where(i => grid.Gems[i].ClaimState == 0)),
        "Dense available list drifted after move, claim, release or recycle");
    }

    var cluster = new GemSpatialIndex(10, 30);
    for (int i = 0; i < 3; ++i) cluster.AddGem(i, 305 + i, -305 - i, uint.MaxValue);
    cluster.PrepareQueries();
    Require(cluster.TryGetWeightedClusterPosition(random, out var center)
      && center == new Vector2(306, -306), "Cluster centroid must use gem count, even at maximum value");
    Require(cluster.TryGetBestScoringClusterPosition(Vector2.Zero, out var target, out int bucket)
      && target == new Vector2(305, -305), "Scored cluster must select a real nearby gem without value overflow");
    cluster.ReserveBucket(bucket);
    cluster.ReleaseBucket(bucket);
    cluster.ReleaseBucket(bucket);
    cluster.MoveGem(0, 307, -307);
    cluster.PrepareQueries();
    Require(cluster.TryGetWeightedClusterPosition(random, out center)
      && Math.Abs(center.X - 306.66666f) < 0.001f, "Same-cell movement must update cached centroids");
    for (int i = 0; i < 3; ++i) { cluster.TryClaim(i); cluster.RemoveFromQueries(i); }
    cluster.PrepareQueries();
    Require(cluster.GetRandomActiveGemIndex(random) == -1
      && !cluster.TryGetWeightedClusterPosition(random, out _), "Animation-only gems must not be targeted");
    cluster.ReleaseClaim(1);
    Require(cluster.GetRandomActiveGemIndex(random) == 1, "Released chain gem must re-enter targeting");

    // A pile larger than the original 100,000-entry query buffer must be safe.
    const int denseCount = 120_000;
    var dense = new GemSpatialIndex(denseCount, 30);
    for (int i = 0; i < denseCount; ++i) dense.AddGem(i, -30, 30, uint.MaxValue);
    int count = 0;
    foreach (int index in dense.Query(-30, 30, 100, 100)) ++count;
    Require(count == denseCount, "Dense queries must not truncate or overflow");
    dense.QueryNearbyIndices(-30, 30, 100, new int[2], out count);
    Require(count == 2, "Bounded query must respect destination capacity");
    dense.GetActiveGems(int.MaxValue, new int[2], out count);
    Require(count == 2, "Ability buffer must respect destination capacity");
    dense.PrepareQueries();
    Require(dense.TryGetWeightedClusterPosition(random, out center) && center == new Vector2(-30, 30),
      "Cluster weights must not overflow with more than 46,340 gems in one cell");

    var winners = new int[denseCount];
    Parallel.For(0, 16, _ =>
    {
      foreach (int index in dense.Query(-30, 30, 100, 100))
        if (dense.TryClaim(index)) Interlocked.Increment(ref winners[index]);
    });
    Require(winners.All(w => w == 1), "Parallel collectors must claim each gem exactly once");
    // Main-thread unlinking keeps worker traversal stable and later queries empty.
    for (int i = 0; i < denseCount; ++i) dense.RemoveFromQueries(i);
    Require(dense.AvailableCount == 0 && dense.NumActiveGems == denseCount,
      "Claims must leave animation slots alive while removing all query work");
    for (int i = 0; i < denseCount; ++i) dense.RecycleIndex(i);
    Require(dense.NumActiveGems == 0, "Dense animation cleanup leaked slots");
    for (int i = 0; i < denseCount; ++i) Require(dense.AddGem(i, i % 500, 0, 1) >= 0, "Dense slots cannot be reused");
    Require(dense.AddGem(-1, 0, 0, 1) == -1, "Overflow insertion must fail cleanly");
    Console.WriteLine("Spatial checks passed: 8,000 randomized mutations, clusters, 120,000-gem piles, parallel claims and recycling.");
  }

  private static void CheckCollectionRadii()
  {
    var grid = new GemSpatialIndex(8, 10);
    int touching = grid.AddGem(1, 18, 0, 1, 6);
    int outside = grid.AddGem(2, 18.1f, 0, 1, 6);
    int large = grid.AddGem(3, -24, 0, 1000, 12);
    int diagonal = grid.AddGem(4, 18, 18, 1, 6);
    int growing = grid.AddGem(5, 20, 0, 1000, 1);

    HashSet<int> Collectable()
    {
      var result = new HashSet<int>();
      foreach (int index in grid.QueryCollection(0, 0, 12)) result.Add(index);
      return result;
    }

    Require(Collectable().SetEquals(new[] { touching, large }),
      "Collection must include touching edges across cells and reject gaps and diagonal non-overlaps");
    grid.SetCollectionRadius(growing, 8);
    Require(Collectable().Contains(growing), "Growing gems must use their updated visual radius");
    grid.SetCollectionRadius(growing, 1);
    Require(!Collectable().Contains(growing), "Shrinking gems must stop overlapping");
    grid.TryClaim(touching);
    Require(!Collectable().Contains(touching), "Collection must exclude already claimed gems");
    grid.RecycleIndex(large);
    int reused = grid.AddGem(6, -24, 0, 1);
    Require(reused == large && !Collectable().Contains(reused),
      "Recycled gem slots must not retain the previous gem's radius");
  }

  private static (double Milliseconds, long Bytes) Measure(Action action)
  {
    for (int i = 0; i < 3; ++i) action();
    var times = new double[7];
    long allocated = 0;
    for (int i = 0; i < times.Length; ++i)
    {
      long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
      long start = Stopwatch.GetTimestamp();
      action();
      times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
      allocated += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
    }
    Array.Sort(times);
    return (times[times.Length / 2], allocated / times.Length);
  }

  private static long _sink;
  public static void Benchmark()
  {
    Console.WriteLine("Spatial microbenchmarks; median of 7 warmed runs, CPU only, not whole-game FPS.");
    Console.WriteLine($"Runtime: {Environment.Version}; logical CPUs: {Environment.ProcessorCount}");
    foreach (int count in new[] { 40_000, 250_000, 500_000 })
    {
      var random = new Random(17);
      var old = new OriginalFlatSpatialHash(count, 30);
      var current = new GemSpatialIndex(count, 30);
      for (int i = 0; i < count; ++i)
      {
        float x = random.NextSingle() * 3840, y = random.NextSingle() * 2000;
        old.AddGem(i, x, y, 1);
        current.AddGem(i, x, y, 1);
      }
      old.RebuildGrid();
      current.PrepareQueries();
      var positions = Enumerable.Range(0, 1000).Select(_ => new Vector2(random.NextSingle() * 3840, random.NextSingle() * 2000)).ToArray();
      var buffer = new int[count];
      var oldFrame = Measure(() =>
      {
        old.RebuildGrid();
        long found = 0;
        foreach (var p in positions)
        {
          old.QueryNearbyIndices(p.X, p.Y, buffer, out int candidates);
          for (int j = 0; j < candidates; ++j)
          {
            ref var gem = ref old.Gems[buffer[j]];
            float dx = gem.X - p.X, dy = gem.Y - p.Y;
            if (gem.IsActive && gem.ClaimState == 0 && dx * dx + dy * dy < 900) ++found;
          }
        }
        _sink = found;
      });
      var newFrame = Measure(() =>
      {
        current.PrepareQueries();
        long found = 0;
        foreach (var p in positions)
          foreach (int index in current.Query(p.X, p.Y, 30, 30))
          {
            ref var gem = ref current.Gems[index];
            float dx = gem.X - p.X, dy = gem.Y - p.Y;
            if (dx * dx + dy * dy < 900) ++found;
          }
        _sink = found;
      });
      var oldClusters = Measure(() =>
      {
        foreach (var p in positions.AsSpan(0, 100))
          if (old.TryGetBestScoringClusterPosition(p, out _, out _)) ++_sink;
      });
      var newClusters = Measure(() =>
      {
        foreach (var p in positions.AsSpan(0, 100))
          if (current.TryGetBestScoringClusterPosition(p, out _, out _)) ++_sink;
      });
      var oldWeighted = Measure(() =>
      {
        for (int i = 0; i < 1000; ++i) if (old.TryGetWeightedClusterPosition(random, out _)) ++_sink;
      });
      var newWeighted = Measure(() =>
      {
        for (int i = 0; i < 1000; ++i) if (current.TryGetWeightedClusterPosition(random, out _)) ++_sink;
      });
      Console.WriteLine($"{count:N0} gems / 1,000 queries: old {oldFrame.Milliseconds:F3} ms, new {newFrame.Milliseconds:F3} ms; allocation {newFrame.Bytes} B");
      Console.WriteLine($"  100 scored targets: old {oldClusters.Milliseconds:F3} ms, new {newClusters.Milliseconds:F3} ms");
      Console.WriteLine($"  1,000 weighted targets: old {oldWeighted.Milliseconds:F3} ms, new {newWeighted.Milliseconds:F3} ms");
    }
  }
}
