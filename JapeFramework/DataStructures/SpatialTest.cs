using MonoGame.Extended.Collisions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace JapeFramework.DataStructures
{
  //https://www.youtube.com/watch?v=vxZx_PXo-yo
  public class SpatialTest
  {
    private Dictionary<int, List<ICollisionActor>> _collisionActors;

    public SpatialTest(int xSize, int ySize)
    {
      _collisionActors = new Dictionary<int, List<ICollisionActor>>();
    }

    public void Add(ICollisionActor actor)
    {
      var gridPosition = GridPosition(actor.Bounds.Position.X, actor.Bounds.Position.Y);
      var id = GetIndex(gridPosition.x, gridPosition.y);

      if(!_collisionActors.ContainsKey(id))
        _collisionActors.Add(id, []);

      _collisionActors[id].Add(actor);
    }

    private int GetIndex(int x, int y)
    {
      unchecked
      {
        return x * 73856093 ^ y * 19349663;
      }
    }


    private float cellSize = 25;
    private (int x, int y) GridPosition(float x, float y)
    {
      return ((int)MathF.Floor(x / cellSize), (int)MathF.Floor(y / cellSize));
    }

    private (int x, int y) GridPosition(Vector2 v)
    {
      return ((int)MathF.Floor(v.X / cellSize), (int)MathF.Floor(v.Y / cellSize));
    }

    public void Remove(ICollisionActor actor)
    {
      var gridPosition = GridPosition(actor.Bounds.Position.X, actor.Bounds.Position.Y);
      var id = GetIndex(gridPosition.x, gridPosition.y);
      _collisionActors[id].Remove(actor);
    }

    public IEnumerable<List<ICollisionActor>> GetBuckets()
    {
      return _collisionActors.Values;
      //foreach (var value in _collisionActors.Values)
      //{
        
      //}
    }

    public void Query(ICollisionActor actor)
    {

    }

    public IEnumerable<ICollisionActor> Query2(Vector2 position, int querySize)
    {
      var finalList = new List<ICollisionActor>();

      var queryRadius = new Vector2(querySize, querySize);
      var minGridPos = GridPosition(position - queryRadius);
      var maxGridPos = GridPosition(position + queryRadius);

      for (int x = minGridPos.x; x < maxGridPos.x; x++)
      {
        for (int y = minGridPos.y; y < maxGridPos.y; y++)
        {
          var hash = GetIndex(x, y);

          _collisionActors.TryGetValue(hash, out var l);
          if (l != null)
          {
            finalList.AddRange(l);
          }
        }
      }

      return finalList;
    }

    public IEnumerable<ICollisionActor> Query(Vector2 position)
    {
      var gridPosition = GridPosition(position.X, position.Y);

      var id = GetIndex(gridPosition.x, gridPosition.y);
      var id2 = GetIndex(gridPosition.x + 1, gridPosition.y);
      var id3 = GetIndex(gridPosition.x - 1, gridPosition.y);
      var id4 = GetIndex(gridPosition.x, gridPosition.y + 1);
      var id5 = GetIndex(gridPosition.x, gridPosition.y - 1);

      var id6 = GetIndex(gridPosition.x + 1, gridPosition.y - 1);
      var id7 = GetIndex(gridPosition.x - 1, gridPosition.y + 1);

      var id8 = GetIndex(gridPosition.x - 1, gridPosition.y - 1);
      var id9 = GetIndex(gridPosition.x + 1, gridPosition.y + 1);

      var ids = new List<int>(){id, id2, id3, id4, id5, id6, id7, id8, id9};

      var finalList = new List<ICollisionActor>();

      foreach (var i in ids)
      {
        _collisionActors.TryGetValue(i, out var l);
        if (l != null)
        {
          finalList.AddRange(l);
        }
      }

      //_collisionActors.TryGetValue(id, out var list);
      //_collisionActors.TryGetValue(id2, out var list2);
      //_collisionActors.TryGetValue(id3, out var list3);


      //if (list != null)
      //  finalList.AddRange(list);
      //if (list2 != null)
      //  finalList.AddRange(list2);
      //if (list3 != null)
      //  finalList.AddRange(list3);

      return finalList;
    }

  }
}
