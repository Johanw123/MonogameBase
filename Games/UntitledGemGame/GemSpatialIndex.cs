using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

/// <summary>
/// Persistent index for loose gems. Structural changes run on the game thread;
/// fleet workers may query and atomically claim against that stable structure.
/// Claimed slots stay allocated for their animation, but leave the query lists.
/// </summary>
public sealed class GemSpatialIndex
{
  public readonly GemData[] Gems;
  public readonly int[] _nextIndices;
  private readonly int[] _previousIndices;
  private readonly int[] _gemBuckets;
  private readonly int[] _freeIndices;
  private readonly bool[] _allocated;
  private int _freeCount;
  private int _nextSlot;
  private readonly float _inverseCellSize;
  // Int64's default hash XORs the two halves: regular (x,y) grids then collide
  // heavily along diagonals. Mix each coordinate before combining instead.
  private sealed class CellComparer : IEqualityComparer<long>
  {
    public bool Equals(long a, long b) => a == b;
    public int GetHashCode(long key) => unchecked((int)(key >> 32) * 73856093 ^ (int)key * 19349663);
  }
  private readonly Dictionary<long, int> _cells = new(new CellComparer());
  public int[] _bucketHeads;
  public int[] _bucketCounts;
  private double[] _sumX, _sumY;
  private ulong[] _bucketValues;
  private int[] _inboundHarvesters;
  private int[] _occupiedBuckets, _occupiedPositions;
  private int _occupiedCount;
  private int _cellCount;
  private readonly int[] _availableIndices, _availablePositions;
  public int AvailableCount { get; private set; }
  public int NumActiveGems { get; private set; }
  public int MaxCapacity => Gems.Length;
  public int _tableSize => _cellCount;
  public ReadOnlySpan<int> AvailableIndices => _availableIndices.AsSpan(0, AvailableCount);

  private int[] _weightedBuckets;
  private long[] _weights;
  private int _weightedCount;
  private long _totalWeight;
  private bool _weightsDirty = true;

  public GemSpatialIndex(int maxCapacity, float cellSize, int tableSize = 16384)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(maxCapacity);
    if (!float.IsFinite(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
    _inverseCellSize = 1f / cellSize;
    Gems = new GemData[maxCapacity];
    _nextIndices = new int[maxCapacity];
    _previousIndices = new int[maxCapacity];
    _gemBuckets = new int[maxCapacity];
    _freeIndices = new int[maxCapacity];
    _allocated = new bool[maxCapacity];
    _availableIndices = new int[maxCapacity];
    _availablePositions = new int[maxCapacity];
    Array.Fill(_gemBuckets, -1);
    int size = Math.Max(4, tableSize);
    _bucketHeads = new int[size];
    Array.Fill(_bucketHeads, -1);
    _bucketCounts = new int[size];
    _sumX = new double[size];
    _sumY = new double[size];
    _bucketValues = new ulong[size];
    _inboundHarvesters = new int[size];
    _occupiedBuckets = new int[size];
    _occupiedPositions = new int[size];
    _weightedBuckets = new int[size];
    _weights = new long[size];
  }

  private static long CellKey(int x, int y) => ((long)x << 32) | (uint)y;
  private int Cell(float coordinate) => (int)MathF.Floor(coordinate * _inverseCellSize);

  private int GetOrAddBucket(float x, float y)
  {
    long key = CellKey(Cell(x), Cell(y));
    if (_cells.TryGetValue(key, out int bucket)) return bucket;
    bucket = _cellCount++;
    if (bucket == _bucketHeads.Length)
    {
      int size = checked(bucket * 2);
      Array.Resize(ref _bucketHeads, size);
      Array.Fill(_bucketHeads, -1, bucket, size - bucket);
      Array.Resize(ref _bucketCounts, size);
      Array.Resize(ref _sumX, size);
      Array.Resize(ref _sumY, size);
      Array.Resize(ref _bucketValues, size);
      Array.Resize(ref _inboundHarvesters, size);
      Array.Resize(ref _occupiedBuckets, size);
      Array.Resize(ref _occupiedPositions, size);
      Array.Resize(ref _weightedBuckets, size);
      Array.Resize(ref _weights, size);
    }
    _cells.Add(key, bucket);
    return bucket;
  }

  public int AddGem(int id, float x, float y, uint value)
  {
    if (_freeCount == 0 && _nextSlot == MaxCapacity) return -1;
    int index = _freeCount > 0 ? _freeIndices[--_freeCount] : _nextSlot++;
    Gems[index] = new GemData { EntityId = id, X = x, Y = y, BaseValue = value, IsActive = true };
    _allocated[index] = true;
    ++NumActiveGems;
    AddToQueries(index);
    return index;
  }

  private void AddToQueries(int index)
  {
    ref var gem = ref Gems[index];
    int bucket = GetOrAddBucket(gem.X, gem.Y);
    _gemBuckets[index] = bucket;
    _previousIndices[index] = -1;
    _nextIndices[index] = _bucketHeads[bucket];
    if (_bucketHeads[bucket] >= 0) _previousIndices[_bucketHeads[bucket]] = index;
    _bucketHeads[bucket] = index;
    if (_bucketCounts[bucket]++ == 0)
    {
      _occupiedPositions[bucket] = _occupiedCount;
      _occupiedBuckets[_occupiedCount++] = bucket;
    }
    _sumX[bucket] += gem.X;
    _sumY[bucket] += gem.Y;
    _bucketValues[bucket] += gem.BaseValue;
    _availablePositions[index] = AvailableCount;
    _availableIndices[AvailableCount++] = index;
    _weightsDirty = true;
  }

  public void RemoveFromQueries(int index)
  {
    if ((uint)index >= (uint)MaxCapacity) return;
    int bucket = _gemBuckets[index];
    if (bucket < 0) return;
    int previous = _previousIndices[index], next = _nextIndices[index];
    if (previous < 0) _bucketHeads[bucket] = next;
    else _nextIndices[previous] = next;
    if (next >= 0) _previousIndices[next] = previous;
    _gemBuckets[index] = -1;
    _sumX[bucket] -= Gems[index].X;
    _sumY[bucket] -= Gems[index].Y;
    _bucketValues[bucket] -= Gems[index].BaseValue;
    if (--_bucketCounts[bucket] == 0)
    {
      int movedBucket = _occupiedBuckets[--_occupiedCount];
      _occupiedBuckets[_occupiedPositions[bucket]] = movedBucket;
      _occupiedPositions[movedBucket] = _occupiedPositions[bucket];
      _sumX[bucket] = _sumY[bucket] = 0;
    }
    int movedIndex = _availableIndices[--AvailableCount];
    _availableIndices[_availablePositions[index]] = movedIndex;
    _availablePositions[movedIndex] = _availablePositions[index];
    _weightsDirty = true;
  }

  public void RecycleIndex(int index)
  {
    if ((uint)index >= (uint)MaxCapacity || !_allocated[index]) return;
    RemoveFromQueries(index);
    Gems[index].IsActive = false;
    _allocated[index] = false;
    _freeIndices[_freeCount++] = index;
    --NumActiveGems;
  }

  public void MoveGem(int index, float x, float y)
  {
    ref var gem = ref Gems[index];
    if (gem.X == x && gem.Y == y) return;
    int bucket = _gemBuckets[index];
    bool indexed = bucket >= 0;
    if (indexed && GetOrAddBucket(x, y) == bucket)
    {
      _sumX[bucket] += (double)x - gem.X;
      _sumY[bucket] += (double)y - gem.Y;
      gem.X = x;
      gem.Y = y;
      return;
    }
    if (indexed) RemoveFromQueries(index);
    gem.X = x;
    gem.Y = y;
    if (indexed) AddToQueries(index);
  }

  // Claim on a worker, unlink on the main thread after workers have joined.
  public bool TryClaim(int index) => Gems[index].IsActive
    && Interlocked.CompareExchange(ref Gems[index].ClaimState, 1, 0) == 0;

  public void ReleaseClaim(int index)
  {
    if (!_allocated[index] || !Gems[index].IsActive) return;
    Gems[index].ClaimState = 0;
    if (_gemBuckets[index] < 0) AddToQueries(index);
  }

  public void ReserveBucket(int bucket)
  {
    if ((uint)bucket < (uint)_cellCount) Interlocked.Increment(ref _inboundHarvesters[bucket]);
  }

  public void ReleaseBucket(int bucket)
  {
    if ((uint)bucket >= (uint)_cellCount) return;
    int count;
    do { count = Volatile.Read(ref _inboundHarvesters[bucket]); }
    while (count > 0 && Interlocked.CompareExchange(ref _inboundHarvesters[bucket], count - 1, count) != count);
  }

  public int GetRandomActiveGemIndex(Random random)
  {
    if (AvailableCount == 0) return -1;
    int start = random.Next(AvailableCount);
    for (int i = 0; i < AvailableCount; ++i)
    {
      int index = _availableIndices[(start + i) % AvailableCount];
      if (Gems[index].IsActive && Gems[index].ClaimState == 0) return index;
    }
    return -1;
  }

  public void GetActiveGems(int maxGems, int[] buffer, out int count)
  {
    count = 0;
    int limit = Math.Min(maxGems, buffer.Length);
    for (int i = 0; i < AvailableCount && count < limit; ++i)
    {
      int index = _availableIndices[i];
      if (Gems[index].IsActive && Gems[index].ClaimState == 0) buffer[count++] = index;
    }
  }

  // Allocation-free rectangle traversal, with no fixed candidate buffer or density limit.
  public QueryEnumerator Query(float x, float y, float halfWidth, float halfHeight)
    => new(this, x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);

  public struct QueryEnumerator
  {
    private readonly GemSpatialIndex _grid;
    private readonly float _minX, _minY, _maxX, _maxY;
    private readonly int _firstX, _lastX, _lastY;
    private int _x, _y, _next;
    public int Current { get; private set; }
    internal QueryEnumerator(GemSpatialIndex grid, float minX, float minY, float maxX, float maxY)
    {
      _grid = grid;
      _minX = minX; _minY = minY; _maxX = maxX; _maxY = maxY;
      _firstX = grid.Cell(minX); _lastX = grid.Cell(maxX); _lastY = grid.Cell(maxY);
      _x = _firstX; _y = grid.Cell(minY); _next = -1;
      Current = -1;
    }
    public QueryEnumerator GetEnumerator() => this;
    public bool MoveNext()
    {
      while (true)
      {
        while (_next >= 0)
        {
          int index = _next;
          _next = _grid._nextIndices[index];
          ref var gem = ref _grid.Gems[index];
          if (gem.IsActive && gem.ClaimState == 0
            && gem.X >= _minX && gem.X <= _maxX && gem.Y >= _minY && gem.Y <= _maxY)
          { Current = index; return true; }
        }
        if (_y > _lastY) return false;
        if (_grid._cells.TryGetValue(CellKey(_x, _y), out int bucket)) _next = _grid._bucketHeads[bucket];
        if (++_x > _lastX) { _x = _firstX; ++_y; }
      }
    }
  }

  public void QueryNearbyIndices(float x, float y, float radius, int[] buffer, out int count)
  {
    count = 0;
    foreach (int index in Query(x, y, radius, radius))
    {
      if (count == buffer.Length) break;
      buffer[count++] = index;
    }
  }

  public void GetDenseBuckets(int minimum, int[] buffer, out int count)
  {
    count = 0;
    for (int i = 0; i < _occupiedCount && count < buffer.Length; ++i)
    {
      int bucket = _occupiedBuckets[i];
      if (_bucketCounts[bucket] >= minimum) buffer[count++] = bucket;
    }
  }

  // Called on the main thread before fleet queries. No gem scan or rebuild.
  public void PrepareQueries()
  {
    if (!_weightsDirty) return;
    _weightedCount = 0;
    _totalWeight = 0;
    for (int i = 0; i < _occupiedCount; ++i)
    {
      int bucket = _occupiedBuckets[i], count = _bucketCounts[bucket];
      if (count < 3) continue;
      _totalWeight += (long)count * count;
      _weightedBuckets[_weightedCount] = bucket;
      _weights[_weightedCount++] = _totalWeight;
    }
    _weightsDirty = false;
  }

  private Vector2 Centroid(int bucket) => new((float)(_sumX[bucket] / _bucketCounts[bucket]),
    (float)(_sumY[bucket] / _bucketCounts[bucket]));

  public bool TryGetWeightedClusterPosition(Random random, out Vector2 target, int minGems = 3)
  {
    target = Vector2.Zero;
    if (_totalWeight == 0) return false;
    long roll = random.NextInt64(_totalWeight);
    int low = 0, high = _weightedCount - 1;
    while (low < high)
    {
      int mid = (low + high) / 2;
      if (_weights[mid] <= roll) low = mid + 1;
      else high = mid;
    }
    int bucket = _weightedBuckets[low];
    if (_bucketCounts[bucket] < minGems) return false;
    target = Centroid(bucket);
    return true;
  }

  public bool TryGetBestScoringClusterPosition(Vector2 position, out Vector2 target,
    out int selectedBucket, int minGems = 3, float minSearchRadius = 40f)
  {
    target = Vector2.Zero;
    selectedBucket = -1;
    double bestScore = -1;
    float minSquared = minSearchRadius * minSearchRadius;
    for (int i = 0; i < _occupiedCount; ++i)
    {
      int bucket = _occupiedBuckets[i], count = _bucketCounts[bucket];
      if (count < minGems) continue;
      float distanceSquared = Vector2.DistanceSquared(position, Centroid(bucket));
      if (distanceSquared < minSquared) continue;
      // Compare squared scores to avoid a square root/division for every cell.
      // Values are promoted before multiplying, including maximum-value piles.
      double value = _bucketValues[bucket];
      double valueSquared = value * value;
      double reservation = 1.0 + Volatile.Read(ref _inboundHarvesters[bucket]);
      double denominator = Math.Max(1, distanceSquared) * reservation * reservation;
      double numerator = valueSquared * valueSquared;
      if (numerator > bestScore * denominator && HasAvailableGem(bucket))
      {
        bestScore = numerator / denominator;
        selectedBucket = bucket;
      }
    }
    if (selectedBucket < 0)
      return minSearchRadius > 0 && TryGetBestScoringClusterPosition(position, out target,
        out selectedBucket, minGems, 0);
    float closest = float.MaxValue;
    for (int index = _bucketHeads[selectedBucket]; index >= 0; index = _nextIndices[index])
    {
      ref var gem = ref Gems[index];
      if (!gem.IsActive || gem.ClaimState != 0) continue;
      var candidate = new Vector2(gem.X, gem.Y);
      float distance = Vector2.DistanceSquared(position, candidate);
      if (distance < closest) { closest = distance; target = candidate; }
    }
    return closest < float.MaxValue;
  }

  private bool HasAvailableGem(int bucket)
  {
    for (int index = _bucketHeads[bucket]; index >= 0; index = _nextIndices[index])
      if (Gems[index].IsActive && Gems[index].ClaimState == 0) return true;
    return false;
  }
}
