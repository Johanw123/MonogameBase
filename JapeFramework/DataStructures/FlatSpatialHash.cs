using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

public struct GemData
{
  public float X;
  public float Y;
  public int ClaimState; // 0 = Available, 1 = Claimed, 2 = Clicked (Claimed)
  public bool IsActive;
  public int EntityId;
  public uint BaseValue; // NEW: Needed for your merge math
}

public class FlatSpatialHash
{
  // Public data for direct access (highest performance)
  public readonly GemData[] Gems;

  // Grid management
  public readonly int[] _bucketHeads;
  public readonly int[] _nextIndices;
  public readonly int[] _bucketCounts;
  private int[] _inboundHarvesters;

  // Memory recycling
  private readonly int[] _freeIndices;
  private int _freeCount = 0;
  private int _nextAvailableIndex = 0;

  private readonly float _cellSize;
  public readonly int _tableSize;
  public readonly int MaxCapacity;

  private readonly float _invCellSize; // NEW: Pre-calculated inverse cell size
  private readonly int _tableMask;    // NEW: Mask for power-of-two table size


  public int NumActiveGems = 0;

  public FlatSpatialHash(int maxCapacity, float cellSize, int tableSize = 10000)
  {
    // MaxCapacity = maxCapacity;
    // _cellSize = cellSize;
    // _tableSize = tableSize;
    //
    // Gems = new GemData[maxCapacity];
    // _nextIndices = new int[maxCapacity];
    // _bucketHeads = new int[tableSize];
    // _freeIndices = new int[maxCapacity];
    // _bucketCounts = new int[tableSize];

    // Force tableSize to be a power of 2 just in case
    _tableSize = ReverseToPowerOfTwo(tableSize);
    _tableMask = _tableSize - 1; // e.g., 8192 - 1 = 8191 (0x1FFF)
    _invCellSize = 1.0f / cellSize; // Precalculate inverse

    MaxCapacity = maxCapacity;
    _cellSize = cellSize;

    Gems = new GemData[maxCapacity];
    _nextIndices = new int[maxCapacity];
    _bucketHeads = new int[_tableSize];
    _bucketCounts = new int[_tableSize];
    _freeIndices = new int[maxCapacity];
    _inboundHarvesters = new int[_tableSize];
  }

  private static int ReverseToPowerOfTwo(int value)
  {
    int power = 2;
    while (power < value) power <<= 1;
    return power;
  }

  /// <summary>
  /// Rebuilds the spatial grid from scratch. 
  /// O(N) complexity where N is active gems.
  /// </summary>
  // public void RebuildGrid()
  // {
  //   Array.Fill(_bucketHeads, -1);
  //   Array.Fill(_bucketCounts, 0); // NEW
  //
  //   for (int i = 0; i < _nextAvailableIndex; i++)
  //   {
  //     if (!Gems[i].IsActive) continue;
  //
  //     int hash = GetBucketIndex(Gems[i].X, Gems[i].Y);
  //
  //     // Link this gem into the head of the bucket list
  //     _nextIndices[i] = _bucketHeads[hash];
  //     _bucketHeads[hash] = i;
  //
  //     _bucketCounts[hash]++; // NEW: Track density
  //   }
  // }

  public void RebuildGrid()
  {
    int[] bucketHeads = _bucketHeads;
    int[] bucketCounts = _bucketCounts;
    int[] nextIndices = _nextIndices;
    GemData[] gems = Gems;

    Array.Fill(bucketHeads, -1);
    Array.Fill(bucketCounts, 0);

    int count = _nextAvailableIndex;

    for (int i = 0; i < count; i++)
    {
      ref GemData gem = ref gems[i];
      if (!gem.IsActive) continue;

      // Uses the exact same GetBucketIndex method!
      int hash = GetBucketIndex(gem.X, gem.Y);

      nextIndices[i] = bucketHeads[hash];
      bucketHeads[hash] = i;
      bucketCounts[hash]++;
    }

    // foreach(var i in _inboundHarvesters)
    // {
    //   Console.WriteLine(i);
    // }
  }

  /// <summary>
  /// Adds a gem. Returns the array index to track.
  /// </summary>
  public int AddGem(int id, float x, float y, uint gemValue)
  {
    int index = (_freeCount > 0) ? _freeIndices[--_freeCount] : _nextAvailableIndex++;

    if (index >= MaxCapacity) return -1; // Grid full

    Gems[index] = new GemData
    {
      X = x,
      Y = y,
      IsActive = true,
      ClaimState = 0,
      EntityId = id,
      BaseValue = gemValue
    };

    ++NumActiveGems;

    return index;
  }

  public void RecycleIndex(int index)
  {
    --NumActiveGems;
    Gems[index].IsActive = false;
    _freeIndices[_freeCount++] = index;
  }


  public void ReserveBucket(int bucketIndex)
  {
    if (bucketIndex >= 0 && bucketIndex < _tableSize)
      _inboundHarvesters[bucketIndex]++;
  }

  public void ReleaseBucket(int bucketIndex)
  {
    if (bucketIndex >= 0 && bucketIndex < _tableSize && _inboundHarvesters[bucketIndex] > 0)
      _inboundHarvesters[bucketIndex]--;
  }

  /// <summary>
  /// Returns the array index of a random active gem. 
  /// Returns -1 if no gems are available.
  /// </summary>
  public int GetRandomActiveGemIndex(Random random)
  {
    // 1. Guard against null references and invalid boundaries
    if (random == null || Gems == null) return -1;
    if (NumActiveGems <= 0 || _nextAvailableIndex <= 0) return -1;

    // 2. Ensure upper boundary never exceeds actual array length
    int boundary = Math.Min(_nextAvailableIndex, Gems.Length);
    if (boundary <= 0) return -1;

    int startIndex = random.Next(0, boundary);
    int currentIndex = startIndex;

    do
    {
      ref GemData gem = ref Gems[currentIndex];

      if (gem.IsActive && gem.ClaimState == 0)
      {
        return currentIndex;
      }

      currentIndex++;

      // Wrap around within safe boundary
      if (currentIndex >= boundary)
      {
        currentIndex = 0;
      }

    } while (currentIndex != startIndex);

    return -1;
  }

  /// <summary>
  /// Grabs up to 'maxGems' active, unclaimed gem indices.
  /// Populates resultsBuffer and returns the actual amount found.
  /// </summary>
  public void GetActiveGems(int maxGems, int[] resultsBuffer, out int resultCount)
  {
    resultCount = 0;

    // Early out if there are no gems or you asked for 0
    if (NumActiveGems == 0 || maxGems <= 0) return;

    // Iterate through the flat array up to the high-water mark
    for (int i = 0; i < _nextAvailableIndex; i++)
    {
      ref GemData gem = ref Gems[i];

      // Make sure it exists and isn't already being sucked up/merged
      if (gem.IsActive && gem.ClaimState == 0)
      {
        resultsBuffer[resultCount++] = i;

        // Stop scanning the moment we hit the requested amount
        if (resultCount >= maxGems)
        {
          break;
        }
      }
    }
  }

  /// <summary>
  /// Finds indices of nearby gems. 
  /// Populates resultsBuffer and returns the count of items found.
  /// </summary>
  public void QueryNearbyIndices(float x, float y, int[] resultsBuffer, out int resultCount)
  {
    resultCount = 0;

    // Fast multiplication instead of division
    int cellX = (int)MathF.Floor(x * _invCellSize);
    int cellY = (int)MathF.Floor(y * _invCellSize);

    for (int offsetX = -1; offsetX <= 1; offsetX++)
    {
      for (int offsetY = -1; offsetY <= 1; offsetY++)
      {
        // Calls the updated GetHash with _tableMask
        int hash = GetHash(cellX + offsetX, cellY + offsetY);

        int currentGemIndex = _bucketHeads[hash];
        while (currentGemIndex != -1)
        {
          resultsBuffer[resultCount++] = currentGemIndex;
          currentGemIndex = _nextIndices[currentGemIndex];
        }
      }
    }
  }

  /// <summary>
  /// Returns the bucket indices that contain at least 'minGems'
  /// </summary>
  public void GetDenseBuckets(int minGems, int[] resultsBuffer, out int resultCount)
  {
    resultCount = 0;
    for (int i = 0; i < _tableSize; i++)
    {
      if (_bucketCounts[i] >= minGems)
      {
        resultsBuffer[resultCount++] = i;
      }
    }
  }

  // Note the new "out int selectedBucket" parameter
  public bool TryGetBestScoringClusterPosition(Vector2 harvesterPos, out Vector2 targetGemPosition, out int selectedBucket, int minGems = 3, float minSearchRadius = 40.0f)
  {
    targetGemPosition = Vector2.Zero;
    selectedBucket = -1;
    if (NumActiveGems == 0) return false;

    float bestScore = -1f;
    int bestBucketIndex = -1;
    float minSqrRadius = minSearchRadius * minSearchRadius;

    // 1. Find the highest scoring bucket
    for (int b = 0; b < _tableSize; b++)
    {
      if (_bucketCounts[b] < minGems) continue;

      if (TryCalculateBucketCentroid(b, out Vector2 clusterCenter, out int validCount))
      {
        float dx = clusterCenter.X - harvesterPos.X;
        float dy = clusterCenter.Y - harvesterPos.Y;
        float sqrDistance = dx * dx + dy * dy;

        if (sqrDistance >= minSqrRadius)
        {
          float distance = MathF.Max(1f, MathF.Sqrt(sqrDistance));

          // THE RTS RESERVATION PENALTY
          // We add 1 so we don't divide by zero. 
          // 1 harvester heading there cuts the score in half. 2 cuts it to a third, etc.
          int inboundCount = _inboundHarvesters[b];
          float score = (validCount * validCount) / (distance * (1 + inboundCount));

          if (score > bestScore)
          {
            bestScore = score;
            bestBucketIndex = b;
          }
        }
      }
    }

    // Fallback: If no clusters found outside radius, retry with 0 radius
    if (bestBucketIndex == -1 && minSearchRadius > 0)
    {
      return TryGetBestScoringClusterPosition(harvesterPos, out targetGemPosition, out selectedBucket, minGems, minSearchRadius: 0f);
    }

    if (bestBucketIndex == -1) return false;

    // 2. Pick a REAL GEM position inside the winning bucket
    selectedBucket = bestBucketIndex;
    return TryGetClosestGemInBucket(bestBucketIndex, harvesterPos, out targetGemPosition);
  }
  // /// <summary>
  // /// Finds the optimal cluster, then targets the REAL GEM inside that cluster 
  // /// closest to the harvester (preventing harvesters from targeting empty space).
  // /// </summary>
  // public bool TryGetBestScoringClusterPosition(Vector2 harvesterPos, out Vector2 targetGemPosition, int minGems = 3, float minSearchRadius = 40.0f)
  // {
  //   targetGemPosition = Vector2.Zero;
  //   if (NumActiveGems == 0) return false;
  //
  //   float bestScore = -1f;
  //   int bestBucketIndex = -1;
  //   float minSqrRadius = minSearchRadius * minSearchRadius;
  //
  //   // 1. Find the highest scoring bucket
  //   for (int b = 0; b < _tableSize; b++)
  //   {
  //     if (_bucketCounts[b] < minGems) continue;
  //
  //     if (TryCalculateBucketCentroid(b, out Vector2 clusterCenter, out int validCount))
  //     {
  //       float dx = clusterCenter.X - harvesterPos.X;
  //       float dy = clusterCenter.Y - harvesterPos.Y;
  //       float sqrDistance = dx * dx + dy * dy;
  //
  //       if (sqrDistance >= minSqrRadius)
  //       {
  //         float distance = MathF.Max(1f, MathF.Sqrt(sqrDistance));
  //         // float score = (validCount * validCount) / distance;
  //         float score = (validCount * validCount) / (distance * (1 + _inboundHarvesters[b]));
  //
  //         if (score > bestScore)
  //         {
  //           bestScore = score;
  //           bestBucketIndex = b;
  //         }
  //       }
  //     }
  //   }
  //
  //   // Fallback: If no clusters found outside radius, retry with 0 radius
  //   if (bestBucketIndex == -1 && minSearchRadius > 0)
  //   {
  //     return TryGetBestScoringClusterPosition(harvesterPos, out targetGemPosition, minGems, minSearchRadius: 0f);
  //   }
  //
  //   if (bestBucketIndex == -1) return false;
  //
  //   // 2. Pick a REAL GEM position inside the winning bucket
  //   return TryGetClosestGemInBucket(bestBucketIndex, harvesterPos, out targetGemPosition);
  // }

  /// <summary>
  /// Finds the position of a real, active gem inside the given bucket closest to referencePos.
  /// </summary>
  private bool TryGetClosestGemInBucket(int bucketIndex, Vector2 referencePos, out Vector2 gemPosition)
  {
    gemPosition = Vector2.Zero;
    float minSqrDist = float.MaxValue;
    bool found = false;

    int currentGemIndex = _bucketHeads[bucketIndex];
    while (currentGemIndex != -1)
    {
      ref GemData gem = ref Gems[currentGemIndex];
      if (gem.IsActive && gem.ClaimState == 0)
      {
        float dx = gem.X - referencePos.X;
        float dy = gem.Y - referencePos.Y;
        float sqrDist = dx * dx + dy * dy;

        if (sqrDist < minSqrDist)
        {
          minSqrDist = sqrDist;
          gemPosition = new Vector2(gem.X, gem.Y);
          found = true;
        }
      }
      currentGemIndex = _nextIndices[currentGemIndex];
    }

    return found;
  }


  /// <summary>
  /// Finds a dense cluster weighted by size. Larger clusters have higher chance to be selected,
  /// but harvesters naturally spread out across different clusters.
  /// </summary>
  public bool TryGetWeightedClusterPosition(Random random, out Vector2 centerPosition, int minGems = 3)
  {
    centerPosition = Vector2.Zero;
    if (NumActiveGems == 0) return false;

    // 1. Calculate total weight. We square the count so huge clusters are heavily favored,
    // but smaller clusters can still occasionally win.
    int totalWeight = 0;
    for (int b = 0; b < _tableSize; b++)
    {
      int count = _bucketCounts[b];
      if (count >= minGems)
      {
        totalWeight += count * count;
      }
    }

    if (totalWeight <= 0) return false;

    // 2. Pick a random target weight
    int roll = random.Next(0, totalWeight);
    int currentWeightSum = 0;

    // 3. Find which bucket won the roll
    for (int b = 0; b < _tableSize; b++)
    {
      int count = _bucketCounts[b];
      if (count < minGems) continue;

      currentWeightSum += count * count;
      if (currentWeightSum > roll)
      {
        // Compute the centroid of the winning bucket
        return TryCalculateBucketCentroid(b, out centerPosition);
      }
    }

    return false;
  }

  private bool TryCalculateBucketCentroid(int bucketIndex, out Vector2 centroid)
  {
    centroid = Vector2.Zero;
    int validCount = 0;
    float sumX = 0f;
    float sumY = 0f;

    int currentGemIndex = _bucketHeads[bucketIndex];
    while (currentGemIndex != -1)
    {
      ref GemData gem = ref Gems[currentGemIndex];
      if (gem.IsActive && gem.ClaimState == 0)
      {
        validCount += (int)gem.BaseValue;
        sumX += gem.X;
        sumY += gem.Y;
      }
      currentGemIndex = _nextIndices[currentGemIndex];
    }

    if (validCount == 0) return false;

    centroid = new Vector2(sumX / validCount, sumY / validCount);
    return true;
  }

  private bool TryCalculateBucketCentroid(int bucketIndex, out Vector2 centroid, out int validCount)
  {
    centroid = Vector2.Zero;
    validCount = 0;
    float sumX = 0f;
    float sumY = 0f;

    int currentGemIndex = _bucketHeads[bucketIndex];
    while (currentGemIndex != -1)
    {
      ref GemData gem = ref Gems[currentGemIndex];
      if (gem.IsActive && gem.ClaimState == 0)
      {
        validCount += (int)gem.BaseValue;
        sumX += gem.X;
        sumY += gem.Y;
      }
      currentGemIndex = _nextIndices[currentGemIndex];
    }

    if (validCount == 0) return false;

    centroid = new Vector2(sumX / validCount, sumY / validCount);
    return true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private int GetBucketIndex(float x, float y)
  {
    int cellX = (int)MathF.Floor(x * _invCellSize);
    int cellY = (int)MathF.Floor(y * _invCellSize);
    return GetHash(cellX, cellY);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private int GetHash(int cellX, int cellY)
  {
    const int p1 = 73856093;
    const int p2 = 19349663;

    // Fast bitwise AND replaces modulo everywhere!
    return ((cellX * p1) ^ (cellY * p2)) & _tableMask;
  }
}
