using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UntitledGemGame.Entities
{
  public enum GemTypes
  {
    Blue,
    DarkBlue,
    Gold,
    LightGreen,
    Lilac,
    Purple,
    Red,
    Teal
  }

  public class Gem : ICollisionActor
  {
    //public string Name { get; set; }
    public int ID { get; set; }
    public IShapeF Bounds => BoundsCircle;
    public CircleF BoundsCircle = new CircleF();

    public bool PickedUp { get; set; }

    public bool ShouldDestroy { get; set; }

    private readonly Tweener _tweener = new();

    private Tween m_tween;

    private Entity m_targetHarvester;

    private Entity m_entity;
    private Transform2 m_transform;

    public string LayerName => "Gem";

    public void OnCollision(CollisionEventArgs collisionInfo)
    {
      //Console.WriteLine("Gem Collision");
    }

    public Gem()
    {
    }

    //public Gem(Entity gemEntity, IShapeF bounds)
    //{
    //  //m_entity = gemEntity;
    //  //m_transform = m_entity.Get<Transform2>();

    //  Initialize(gemEntity, bounds);
    //}

    public void Initialize(Entity gemEntity, float radius)
    {
      m_targetHarvester = null;
      m_entity = gemEntity;
      m_transform = m_entity.Get<Transform2>();

      //m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(1.0f, 1.0f), 2)
      //  .Easing(EasingFunctions.Linear);

      m_transform.Scale = new Vector2(1.0f, 1.0f);

      BoundsCircle.Center = m_transform.Position;
      BoundsCircle.Radius = radius;

      //Bounds = bounds;
    }

    public void Reset(/*Entity gemEntity*/)
    {
      ShouldDestroy = false;
      PickedUp = false;
      ID = -1;
      m_entity = null;
      m_transform = null;
      m_targetHarvester = null;
      _tweener.CancelAndCompleteAll();


      //m_entity = gemEntity;
      //m_transform = m_entity.Get<Transform2>();

      //m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(1.0f, 1.0f), 2)
      //  .Easing(EasingFunctions.Linear);
    }

    public void Update(GameTime gameTime)
    {
      if (PickedUp)
      {
        //TODO: only for instant collection upgrade?
        if (m_tween is { IsComplete: true })
        {
          //ShouldDestroy = true;
        }
      }

      if(m_tween is { IsComplete: false })
        _tweener.Update(gameTime.GetElapsedSeconds());

      if (m_targetHarvester != null)
      {
        var t = m_targetHarvester.Get<Transform2>();

        var distance = Vector2.Distance(t.Position, m_transform.Position);

        Vector2 dir = t.Position - m_transform.Position;
        dir.Normalize();
        m_transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 8.0f * /*(1.0f / distance)*/distance;

        //harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
      }
    }
    public void SetPickedUp(Entity gemEntity, Entity harvesterEntity, Action onDone)
    {
      if (PickedUp) return;
      
      PickedUp = true;

      m_targetHarvester = harvesterEntity;

      _tweener.CancelAndCompleteAll();
      m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(0.1f, 0.1f), 0.5f)
        .Easing(EasingFunctions.Linear);

      m_tween.OnEnd(_ => { ShouldDestroy = true; });
    }
  }
}
