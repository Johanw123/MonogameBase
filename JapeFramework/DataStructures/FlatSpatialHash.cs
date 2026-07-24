using System;
using System.Runtime.CompilerServices;

public struct GemData
{
    public float X;
    public float Y;
    public int ClaimState; // 0 = Available, 1 = Claimed
    public bool IsActive;
    public int EntityId;
}

public class FlatSpatialHash
{
    // Public data for direct access (highest performance)
    public readonly GemData[] Gems;
    
    // Grid management
    private readonly int[] _bucketHeads;
    private readonly int[] _nextIndices;
    
    // Memory recycling
    private readonly int[] _freeIndices;
    private int _freeCount = 0;
    private int _nextAvailableIndex = 0;
    
    private readonly float _cellSize;
    private readonly int _tableSize;
    public readonly int MaxCapacity;


    public int NumActiveGems = 0;

    public FlatSpatialHash(int maxCapacity, float cellSize, int tableSize = 10000)
    {
        MaxCapacity = maxCapacity;
        _cellSize = cellSize;
        _tableSize = tableSize;

        Gems = new GemData[maxCapacity];
        _nextIndices = new int[maxCapacity];
        _bucketHeads = new int[tableSize];
        _freeIndices = new int[maxCapacity];
    }

    /// <summary>
    /// Rebuilds the spatial grid from scratch. 
    /// O(N) complexity where N is active gems.
    /// </summary>
    public void RebuildGrid()
    {
        Array.Fill(_bucketHeads, -1);

        for (int i = 0; i < _nextAvailableIndex; i++)
        {
            if (!Gems[i].IsActive) continue;

            int hash = GetBucketIndex(Gems[i].X, Gems[i].Y);
            
            // Link this gem into the head of the bucket list
            _nextIndices[i] = _bucketHeads[hash];
            _bucketHeads[hash] = i;
        }
    }

    /// <summary>
    /// Adds a gem. Returns the array index to track.
    /// </summary>
    public int AddGem(int id, float x, float y)
    {
        int index = (_freeCount > 0) ? _freeIndices[--_freeCount] : _nextAvailableIndex++;

        if (index >= MaxCapacity) return -1; // Grid full

        Gems[index] = new GemData 
        { 
            X = x, 
            Y = y, 
            IsActive = true, 
            ClaimState = 0,
            EntityId = id
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

    /// <summary>
    /// Finds indices of nearby gems. 
    /// Populates resultsBuffer and returns the count of items found.
    /// </summary>
    public void QueryNearbyIndices(float x, float y, int[] resultsBuffer, out int resultCount)
    {
        resultCount = 0;
        int cellX = (int)MathF.Floor(x / _cellSize);
        int cellY = (int)MathF.Floor(y / _cellSize);

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int hash = GetHash((cellX + offsetX), (cellY + offsetY));
                
                int currentGemIndex = _bucketHeads[hash];
                while (currentGemIndex != -1)
                {
                    resultsBuffer[resultCount++] = currentGemIndex;
                    currentGemIndex = _nextIndices[currentGemIndex];
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(float x, float y)
    {
        return GetHash((int)MathF.Floor(x / _cellSize), (int)MathF.Floor(y / _cellSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetHash(int cellX, int cellY)
    {
        const int p1 = 73856093;
        const int p2 = 19349663;
        int hash = (cellX * p1) ^ (cellY * p2);
        return (hash % _tableSize + _tableSize) % _tableSize;
    }
}
