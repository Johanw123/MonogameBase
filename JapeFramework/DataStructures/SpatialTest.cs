// using MonoGame.Extended.Collisions;
// using Microsoft.Xna.Framework;
// using MonoGame.Extended.Collections;
//
// namespace JapeFramework.DataStructures
// {
//   //https://www.youtube.com/watch?v=vxZx_PXo-yo
//   public class SpatialTest
//   {
//     private Dictionary<int, Bag<ICollisionActor>> _collisionActors;
//     private Bag<ICollisionActor> _actorBag = new();
//
//     public SpatialTest(int xSize, int ySize)
//     {
//       _collisionActors = new Dictionary<int, Bag<ICollisionActor>>(5000);
//     }
//
//     public void SetNumActors()
//     {
//     }
//
//     public void Add(ICollisionActor actor)
//     {
//       var center = actor.Shape.BoundingBox.Center;
//       var gridPosition = GridPosition(center.X, center.Y);
//       var id = GetIndex(gridPosition.x, gridPosition.y);
//
//       if (!_collisionActors.ContainsKey(id))
//       {
//         Console.WriteLine("id: " + id);
//         _collisionActors.Add(id, new(5000));
//       }
//
//       _collisionActors[id].Add(actor);
//       _actorBag.Add(actor);
//     }
//
//     private int GetIndex(int x, int y)
//     {
//       unchecked
//       {
//         return x * 73856093 ^ y * 19349663;
//       }
//     }
//
//     public void RefreshBuckets()
//     {
//       foreach(var a in _collisionActors)
//         a.Value.Clear();
//
//       var actors = _actorBag.ToArray();
//       _actorBag.Clear();
//       foreach (var actor in actors)
//       {
//         Add(actor);
//       }
//     }
//
//     private float cellSize = 25;
//     private (int x, int y) GridPosition(float x, float y)
//     {
//       return ((int)MathF.Floor(x / cellSize), (int)MathF.Floor(y / cellSize));
//     }
//
//     private (int x, int y) GridPosition(Vector2 v)
//     {
//       return ((int)MathF.Floor(v.X / cellSize), (int)MathF.Floor(v.Y / cellSize));
//     }
//
//     public void Remove(ICollisionActor actor)
//     {
//       var center = actor.Shape.BoundingBox.Center;
//       var gridPosition = GridPosition(center.X, center.Y);
//       var id = GetIndex(gridPosition.x, gridPosition.y);
//
//       if (_collisionActors.ContainsKey(id))
//         _collisionActors[id].Remove(actor);
//
//       _actorBag.Remove(actor);
//     }
//
//     public IEnumerable<Bag<ICollisionActor>> GetBuckets()
//     {
//       return _collisionActors.Values;
//     }
//
//     public IEnumerable<ICollisionActor> Query2(Vector2 position, int querySize)
//     {
//       var finalList = new List<ICollisionActor>();
//
//       var queryRadius = new Vector2(querySize, querySize);
//       var minGridPos = GridPosition(position - queryRadius);
//       var maxGridPos = GridPosition(position + queryRadius);
//
//       for (int x = minGridPos.x; x < maxGridPos.x; x++)
//       {
//         for (int y = minGridPos.y; y < maxGridPos.y; y++)
//         {
//           var hash = GetIndex(x, y);
//
//           _collisionActors.TryGetValue(hash, out var l);
//           if (l != null)
//           {
//             finalList.AddRange(l);
//           }
//         }
//       }
//
//       return finalList;
//     }
//   }
// }

//Version 2
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Microsoft.Xna.Framework;
// using MonoGame.Extended.Collisions;
//
// namespace JapeFramework.DataStructures
// {
//   public class SpatialTest
//   {
//     private readonly Dictionary<int, List<ICollisionActor>> _collisionActors;
//     private readonly List<ICollisionActor> _actorList;
//     private readonly List<ICollisionActor> _queryBuffer;
//     private readonly float _cellSize;
//
//     public SpatialTest(float cellSize = 25f, int initialCapacity = 1024)
//     {
//       _cellSize = cellSize;
//       _collisionActors = new Dictionary<int, List<ICollisionActor>>(initialCapacity);
//       _actorList = new List<ICollisionActor>(initialCapacity);
//       _queryBuffer = new List<ICollisionActor>(128);
//     }
//
//     public void Add(ICollisionActor actor)
//     {
//       _actorList.Add(actor);
//       InsertActorToGrid(actor);
//     }
//
//     private void InsertActorToGrid(ICollisionActor actor)
//     {
//       var bounds = actor.Shape.BoundingBox;
//
//       // Map Min/Max to cell coordinates
//       int minX = (int)MathF.Floor(bounds.Min.X / _cellSize);
//       int maxX = (int)MathF.Floor(bounds.Max.X / _cellSize);
//       int minY = (int)MathF.Floor(bounds.Min.Y / _cellSize);
//       int maxY = (int)MathF.Floor(bounds.Max.Y / _cellSize);
//
//       for (int x = minX; x <= maxX; x++)
//       {
//         for (int y = minY; y <= maxY; y++)
//         {
//           int hash = GetIndex(x, y);
//
//           if (!_collisionActors.TryGetValue(hash, out var list))
//           {
//             list = new List<ICollisionActor>(16);
//             _collisionActors.Add(hash, list);
//           }
//
//           list.Add(actor);
//         }
//       }
//     }
//
//     private int GetIndex(int x, int y)
//     {
//       unchecked
//       {
//         return (x * 73856093) ^ (y * 19349663);
//       }
//     }
//
//     public void RefreshBuckets()
//     {
//       // Clear lists without reallocating underlying memory
//       foreach (var kvp in _collisionActors)
//       {
//         kvp.Value.Clear();
//       }
//
//       // Zero-allocation loop through the internal actor tracking list
//       for (int i = 0; i < _actorList.Count; i++)
//       {
//         InsertActorToGrid(_actorList[i]);
//       }
//     }
//
//     public void Remove(ICollisionActor actor)
//     {
//       _actorList.Remove(actor);
//
//       var bounds = actor.Shape.BoundingBox;
//       int minX = (int)MathF.Floor(bounds.Min.X / _cellSize);
//       int maxX = (int)MathF.Floor(bounds.Max.X / _cellSize);
//       int minY = (int)MathF.Floor(bounds.Min.Y / _cellSize);
//       int maxY = (int)MathF.Floor(bounds.Max.Y / _cellSize);
//
//       for (int x = minX; x <= maxX; x++)
//       {
//         for (int y = minY; y <= maxY; y++)
//         {
//           int hash = GetIndex(x, y);
//           if (_collisionActors.TryGetValue(hash, out var list))
//           {
//             list.Remove(actor);
//           }
//         }
//       }
//     }
//
//     /// <summary>
//     /// Zero-allocation query returning a reusable buffer.
//     /// </summary>
//     // public List<ICollisionActor> Query(Vector2 position, float querySize)
//     // {
//     //     _queryBuffer.Clear();
//     //
//     //     int minX = (int)MathF.Floor((position.X - querySize) / _cellSize);
//     //     int maxX = (int)MathF.Floor((position.X + querySize) / _cellSize);
//     //     int minY = (int)MathF.Floor((position.Y - querySize) / _cellSize);
//     //     int maxY = (int)MathF.Floor((position.Y + querySize) / _cellSize);
//     //
//     //     for (int x = minX; x <= maxX; x++)
//     //     {
//     //         for (int y = minY; y <= maxY; y++)
//     //         {
//     //             int hash = GetIndex(x, y);
//     //             if (_collisionActors.TryGetValue(hash, out var list))
//     //             {
//     //                 for (int i = 0; i < list.Count; i++)
//     //                 {
//     //                     _queryBuffer.Add(list[i]);
//     //                 }
//     //             }
//     //         }
//     //     }
//     //
//     //     return _queryBuffer;
//     // }
//     public List<ICollisionActor> Query(Vector2 position, float querySize)
//     {
//       // Allocates a new list every single call. Thread-safe, but creates garbage.
//       var uniqueBuffer = new List<ICollisionActor>(128);
//
//       int minX = (int)MathF.Floor((position.X - querySize) / _cellSize);
//       int maxX = (int)MathF.Floor((position.X + querySize) / _cellSize);
//       int minY = (int)MathF.Floor((position.Y - querySize) / _cellSize);
//       int maxY = (int)MathF.Floor((position.Y + querySize) / _cellSize);
//
//       for (int x = minX; x <= maxX; x++)
//       {
//         for (int y = minY; y <= maxY; y++)
//         {
//           int hash = GetIndex(x, y);
//           if (_collisionActors.TryGetValue(hash, out var list))
//           {
//             for (int i = 0; i < list.Count; i++)
//             {
//               uniqueBuffer.Add(list[i]);
//             }
//           }
//         }
//       }
//
//       return uniqueBuffer;
//     }
//   }
// }


using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Collisions;

namespace JapeFramework.DataStructures
{
    public class SpatialTest
    {
        // Tracks cell bounds so we know if an actor actually changed cells
        private readonly Dictionary<ICollisionActor, (int minX, int maxX, int minY, int maxY)> _actorBounds;
        private readonly Dictionary<int, List<ICollisionActor>> _collisionActors;
        private readonly float _cellSize;

        public SpatialTest(float cellSize = 25f, int initialCapacity = 1024)
        {
            _cellSize = cellSize;
            _collisionActors = new Dictionary<int, List<ICollisionActor>>(initialCapacity);
            _actorBounds = new Dictionary<ICollisionActor, (int, int, int, int)>(initialCapacity);
        }

        public void Add(ICollisionActor actor)
        {
            var bounds = GetCellBounds(actor);
            _actorBounds[actor] = bounds;
            InsertToGrid(actor, bounds);
        }

        public void Remove(ICollisionActor actor)
        {
            if (_actorBounds.TryGetValue(actor, out var oldBounds))
            {
                RemoveFromGrid(actor, oldBounds);
                _actorBounds.Remove(actor);
            }
        }

        /// <summary>
        /// Updates ONLY actors that have moved. Call this during movement updates.
        /// </summary>
        public void UpdateActor(ICollisionActor actor)
        {
            if (!_actorBounds.TryGetValue(actor, out var oldBounds))
            {
                Add(actor);
                return;
            }

            var newBounds = GetCellBounds(actor);

            // 🚀 FAST PATH: If the actor hasn't crossed into a new cell boundary, do NOTHING!
            if (oldBounds == newBounds)
                return;

            // Actor moved into new cell(s): remove from old cells and add to new
            RemoveFromGrid(actor, oldBounds);
            InsertToGrid(actor, newBounds);
            _actorBounds[actor] = newBounds;
        }

        /// <summary>
        /// Thread-safe spatial query. Populates the provided resultBuffer to avoid allocations.
        /// </summary>
        // public void Query(Vector2 position, float queryRadius, List<ICollisionActor> resultBuffer)
        // {
        //     resultBuffer.Clear();
        //
        //     int minX = (int)MathF.Floor((position.X - queryRadius) / _cellSize);
        //     int maxX = (int)MathF.Floor((position.X + queryRadius) / _cellSize);
        //     int minY = (int)MathF.Floor((position.Y - queryRadius) / _cellSize);
        //     int maxY = (int)MathF.Floor((position.Y + queryRadius) / _cellSize);
        //
        //     for (int x = minX; x <= maxX; x++)
        //     {
        //         for (int y = minY; y <= maxY; y++)
        //         {
        //             int hash = GetIndex(x, y);
        //             if (_collisionActors.TryGetValue(hash, out var list))
        //             {
        //                 for (int i = 0; i < list.Count; i++)
        //                 {
        //                     resultBuffer.Add(list[i]);
        //                 }
        //             }
        //         }
        //     }
        // }

    public List<ICollisionActor> Query(Vector2 position, float querySize)
    {
      // Allocates a new list every single call. Thread-safe, but creates garbage.
      var uniqueBuffer = new List<ICollisionActor>(128);

      int minX = (int)MathF.Floor((position.X - querySize) / _cellSize);
      int maxX = (int)MathF.Floor((position.X + querySize) / _cellSize);
      int minY = (int)MathF.Floor((position.Y - querySize) / _cellSize);
      int maxY = (int)MathF.Floor((position.Y + querySize) / _cellSize);

      for (int x = minX; x <= maxX; x++)
      {
        for (int y = minY; y <= maxY; y++)
        {
          int hash = GetIndex(x, y);
          if (_collisionActors.TryGetValue(hash, out var list))
          {
            for (int i = 0; i < list.Count; i++)
            {
              uniqueBuffer.Add(list[i]);
            }
          }
        }
      }

      return uniqueBuffer;
    }

        private (int minX, int maxX, int minY, int maxY) GetCellBounds(ICollisionActor actor)
        {
            var bounds = actor.Shape.BoundingBox;
            return (
                (int)MathF.Floor(bounds.Min.X / _cellSize),
                (int)MathF.Floor(bounds.Max.X / _cellSize),
                (int)MathF.Floor(bounds.Min.Y / _cellSize),
                (int)MathF.Floor(bounds.Max.Y / _cellSize)
            );
        }

        private void InsertToGrid(ICollisionActor actor, (int minX, int maxX, int minY, int maxY) b)
        {
            for (int x = b.minX; x <= b.maxX; x++)
            {
                for (int y = b.minY; y <= b.maxY; y++)
                {
                    int hash = GetIndex(x, y);
                    if (!_collisionActors.TryGetValue(hash, out var list))
                    {
                        list = new List<ICollisionActor>(16);
                        _collisionActors.Add(hash, list);
                    }
                    list.Add(actor);
                }
            }
        }

        private void RemoveFromGrid(ICollisionActor actor, (int minX, int maxX, int minY, int maxY) b)
        {
            for (int x = b.minX; x <= b.maxX; x++)
            {
                for (int y = b.minY; y <= b.maxY; y++)
                {
                    int hash = GetIndex(x, y);
                    if (_collisionActors.TryGetValue(hash, out var list))
                    {
                        list.Remove(actor);
                    }
                }
            }
        }

        private int GetIndex(int x, int y)
        {
            unchecked
            {
                return (x * 73856093) ^ (y * 19349663);
            }
        }
    }
}
