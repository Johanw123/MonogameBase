using System;
using Microsoft.Xna.Framework;

public class SpatialHashGrid
{
    private readonly float _cellSize;
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    private readonly int _maxActors;

    // Grid Buckets: Head actor index for each cell (-1 if empty)
    private readonly int[] _heads;

    // Linked List nodes (sized to _maxActors)
    private readonly int[] _nexts;
    private readonly Vector2[] _positions;
    private readonly float[] _radii;

    private int _actorCount;

    public SpatialHashGrid(float cellSize, int gridWidth, int gridHeight, int maxActors)
    {
        _cellSize = cellSize;
        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
        _maxActors = maxActors;

        _heads = new int[gridWidth * gridHeight];
        _nexts = new int[maxActors];
        _positions = new Vector2[maxActors];
        _radii = new float[maxActors];

        ClearGrid();
    }

    /// <summary>
    /// Resets the spatial grid links without allocating new memory.
    /// Call this at the start of every frame/physics step.
    /// </summary>
    public void ClearGrid()
    {
        Array.Fill(_heads, -1);
        _actorCount = 0;
    }

    /// <summary>
    /// Inserts an actor's bounding sphere into the grid.
    /// Returns the internal handle index assigned to this actor for the frame.
    /// </summary>
    public int Insert(Vector2 position, float radius, int id)
    {
        if (_actorCount >= _maxActors)
            throw new InvalidOperationException("Spatial Grid capacity exceeded!");

        _positions[id] = position;
        _radii[id] = radius;

        // Calculate bounding box in cell coordinates
        int minX = Math.Clamp((int)((position.X - radius) / _cellSize), 0, _gridWidth - 1);
        int maxX = Math.Clamp((int)((position.X + radius) / _cellSize), 0, _gridWidth - 1);
        int minY = Math.Clamp((int)((position.Y - radius) / _cellSize), 0, _gridHeight - 1);
        int maxY = Math.Clamp((int)((position.Y + radius) / _cellSize), 0, _gridHeight - 1);

        // Prepend actor to the linked list of every overlapping cell
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                _nexts[id] = _heads[cellIndex]; // Point next to old head
                _heads[cellIndex] = id;         // Make this actor the new head
            }
        }

        return id;
    }

    /// <summary>
    /// Queries potential collisions for an actor without generating allocations or delegates.
    /// Passes matching candidate IDs into the supplied buffer.
    /// </summary>
    public int GetCandidates(Vector2 position, float radius, Span<int> outputCandidates)
    {
        int minX = Math.Clamp((int)((position.X - radius) / _cellSize), 0, _gridWidth - 1);
        int maxX = Math.Clamp((int)((position.X + radius) / _cellSize), 0, _gridWidth - 1);
        int minY = Math.Clamp((int)((position.Y - radius) / _cellSize), 0, _gridHeight - 1);
        int maxY = Math.Clamp((int)((position.Y + radius) / _cellSize), 0, _gridHeight - 1);

        int count = 0;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                int curr = _heads[cellIndex];

                // Traverse linked list inside cell
                while (curr != -1 && count < outputCandidates.Length)
                {
                    // Avoid duplicate checks if using Span buffer filtering
                    outputCandidates[count++] = curr;
                    curr = _nexts[curr];
                }
            }
        }

        return count;
    }
}
