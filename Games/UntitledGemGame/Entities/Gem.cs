using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UntitledGemGame.Screens;

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

    private Transform2 m_targetHarvester;

    private Entity m_entity;
    private Transform2 m_transform;

    public GemTypes GemType { get; set; }

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

      m_transform.Scale = new Vector2(0.1f, 0.1f);

      m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(1.0f, 1.0f), 0.2f)
        .Easing(EasingFunctions.Linear);

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

    private bool m_wasPickedUp = false;

    public void Update(GameTime gameTime, OrthographicCamera camera)
    {
      //if (PickedUp)
      //{
      //  //TODO: only for instant collection upgrade?
      //  if (m_tween is { IsComplete: true })
      //  {
      //    //ShouldDestroy = true;
      //  }
      //}


      var dt = (float)gameTime.GetElapsedSeconds();

      if (m_tween is { IsComplete: false })
        _tweener.Update(gameTime.GetElapsedSeconds());

      if (m_targetHarvester != null)
      {
        var distance = Vector2.Distance(m_targetHarvester.Position, m_transform.Position);

        Vector2 dir = m_targetHarvester.Position - m_transform.Position;
        dir.Normalize();
        m_transform.Position +=
          dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 8.0f * /*(1.0f / distance)*/distance;

        //harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
      }
      // else if (m_wasPickedUp)
      // {
      //   var hbPos = UntitledGemGameGameScreen.HomeBasePos;
      //
      //   var dir = hbPos - m_transform.Position;
      //   var dist = Vector2.Distance(hbPos, m_transform.Position);
      //   dir = Vector2.Normalize(dir);
      //   m_transform.Position += dir * 60.0f * dt * (1 / dist) * 10.0f;
      //
      //   BoundsCircle.Center = m_transform.Position;
      // }
      else
      {
        if (HomeBase.BonusHarvesterMagnetPower > 0)
        {
          var harvesters = EntityFactory.Instance.Harvesters;
          var closesHarvester = harvesters
            .OrderBy(h => Vector2.Distance(h.Value.Get<Transform2>().Position, m_transform.Position))
            .FirstOrDefault();

          if (harvesters.Count != 0 && closesHarvester.Value != null)
          {
            var pos = closesHarvester.Value.Get<Transform2>().Position;
            var hMag = HomeBase.BonusHarvesterMagnetPower;

            var dir = pos - m_transform.Position;
            var dist = Vector2.Distance(pos, m_transform.Position);
            dir = Vector2.Normalize(dir);
            m_transform.Position += dir * hMag * dt * (1 / dist) * 100.0f;
          }
        }

        var hbPos = UntitledGemGameGameScreen.HomeBasePos;
        var mag = HomeBase.BonusMagnetPower;

        if (mag > 0)
        {
          var dir = hbPos - m_transform.Position;
          var dist = Vector2.Distance(hbPos, m_transform.Position);
          dir = Vector2.Normalize(dir);
          m_transform.Position += dir * mag * dt * (1 / dist) * 100.0f;
          // BoundsCircle.Center = m_transform.Position;
        }

      }


      var mouse = MouseExtended.GetState();
      var mouseWorldPos = camera.ScreenToWorld(mouse.Position.ToVector2());

      // bool isMouseOver = BoundsCircle.Contains(mouseWorldPos);
      int gemWidth = 32;
      int gemHeight = 28;
      bool isMouseOver = mouseWorldPos.X >= m_transform.Position.X - gemWidth / 2 &&
                         mouseWorldPos.X <= m_transform.Position.X + gemWidth / 2 &&
                         mouseWorldPos.Y >= m_transform.Position.Y - gemHeight / 2 &&
                         mouseWorldPos.Y <= m_transform.Position.Y + gemHeight / 2;
      // bool isMouseOver = mouseWorldPos 
      bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);

      if (isMouseClicked && isMouseOver && !PickedUp && !RenderGuiSystem.Instance.drawUpgradesGui)
      {
        m_wasPickedUp = true;
        // var dir = UntitledGemGameGameScreen.HomeBasePos - gemPos.Value;
        // dir.Normalize();
        // var distance = Vector2.Distance(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos);
        // gem.Get<Transform2>().Position += dir * 6.0f * (float)gameTime.GetElapsedSeconds() * distance;

        // m_transform.Position = UntitledGemGameGameScreen.HomeBasePos;
        // SetPickedUp(m_entity, EntityFactory.Instance.HomeBaseEntity, null);

        var gemTransform = m_entity.Get<Transform2>();
        m_tween = _tweener.TweenTo(gemTransform, transform => transform.Position, UntitledGemGameGameScreen.HomeBasePos, 0.5f)
          .Easing(EasingFunctions.Linear);

        // m_tween.OnEnd(_ =>
        // {
        //   BoundsCircle.Center = m_transform.Position;
        // });
      }


      BoundsCircle.Center = m_transform.Position;
    }

    public void SetPickedUp(Entity gemEntity, Entity harvesterEntity, Action onDone)
    {
      if (PickedUp) return;

      PickedUp = true;

      m_targetHarvester = harvesterEntity.Get<Transform2>();
      var gemTransform = gemEntity.Get<Transform2>();

      _tweener.CancelAndCompleteAll();

      gemTransform.Scale = new Vector2(1.0f, 1.0f);
      m_tween = _tweener.TweenTo(gemTransform, transform => transform.Scale, new Vector2(0.1f, 0.1f), 0.5f)
        .Easing(EasingFunctions.Linear);

      m_tween.OnEnd(_ => { ShouldDestroy = true; });
    }
  }
}
