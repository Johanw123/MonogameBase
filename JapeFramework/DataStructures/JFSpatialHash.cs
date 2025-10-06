#nullable disable
using System.Runtime.CompilerServices;
using MonoGame.Extended;
using MonoGame.Extended.Collisions;

namespace JapeFramework.DataStructures
{
  public class JFSpatialHash : ISpaceAlgorithm
  {
    private readonly Dictionary<int, List<ICollisionActor>> _dictionary = new Dictionary<int, List<ICollisionActor>>();
    private readonly List<ICollisionActor> _actors = new List<ICollisionActor>();
    private readonly SizeF _size;

    public JFSpatialHash(SizeF size) => this._size = size;

    public void Insert(ICollisionActor actor)
    {
      this.InsertToHash(actor);
      this._actors.Add(actor);
    }

    public Dictionary<int, List<ICollisionActor>> GetDictionary() => _dictionary;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertToHash(ICollisionActor actor)
    {
      RectangleF boundingRectangle = actor.Bounds.BoundingRectangle;
      for (float left = boundingRectangle.Left; (double)left < (double)boundingRectangle.Right; left += this._size.Width)
      {
        for (float top = boundingRectangle.Top; (double)top < (double)boundingRectangle.Bottom; top += this._size.Height)
          this.AddToCell(left, top, actor);
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToCell(float x, float y, ICollisionActor actor)
    {
      int index = this.GetIndex(x, y);
      List<ICollisionActor> collisionActorList;
      if (this._dictionary.TryGetValue(index, out collisionActorList))
        collisionActorList.Add(actor);
      else
        this._dictionary[index] = new List<ICollisionActor>()
        {
          actor
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(float x, float y)
    {
      return (int)((double)x / (double)this._size.Width) << 16 + (int)((double)y / (double)this._size.Height);
    }

    public bool Remove(ICollisionActor actor)
    {
      foreach (List<ICollisionActor> collisionActorList in this._dictionary.Values)
        collisionActorList.Remove(actor);
      return this._actors.Remove(actor);
    }

    public IEnumerable<ICollisionActor> Query(RectangleF boundsBoundingRectangle)
    {
      HashSet<ICollisionActor> collisionActorSet = new HashSet<ICollisionActor>();
      RectangleF boundingRectangle = boundsBoundingRectangle.BoundingRectangle;
      for (float left = boundsBoundingRectangle.Left; (double)left < (double)boundsBoundingRectangle.Right; left += this._size.Width)
      {
        for (float top = boundsBoundingRectangle.Top; (double)top < (double)boundsBoundingRectangle.Bottom; top += this._size.Height)
        {
          List<ICollisionActor> collisionActorList;
          if (this._dictionary.TryGetValue(this.GetIndex(left, top), out collisionActorList))
          {
            foreach (ICollisionActor collisionActor in collisionActorList)
            {
              if (boundingRectangle.Intersects(collisionActor.Bounds))
                collisionActorSet.Add(collisionActor);
            }
          }
        }
      }
      return (IEnumerable<ICollisionActor>)collisionActorSet;
    }

    public List<ICollisionActor>.Enumerator GetEnumerator() => this._actors.GetEnumerator();

    public void Reset()
    {
      this._dictionary.Clear();
      foreach (ICollisionActor actor in this._actors)
        this.InsertToHash(actor);
    }
  }
}
