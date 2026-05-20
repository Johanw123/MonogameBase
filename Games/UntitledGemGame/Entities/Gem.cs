using GUI.Shared.Helpers;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

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
    private Tween m_tween2;

    private Transform2 m_targetHarvester;

    private Entity m_entity;
    private Transform2 m_transform;
    private Sprite m_sprite;

    public GemTypes GemType { get; set; }

    public string LayerName => "Gem";

    private bool isTweeningStart = false;
    private bool isTweeningHarvester = false;
    private bool isTweeningClicked = false;

    public uint BaseValue = 1;

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

    private Vector2 OrigScale = Vector2.One;

    public void Initialize(Entity gemEntity, float radius, uint baseValue)
    {
      m_targetHarvester = null;
      m_entity = gemEntity;
      m_transform = m_entity.Get<Transform2>();

      OrigScale = m_transform.Scale;
      m_transform.Scale = new Vector2(0.1f, 0.1f);

      m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, OrigScale, 0.2f)
        .Easing(EasingFunctions.Linear).OnEnd((tween) =>
        {
          isTweeningStart = false;
        });

      // TweenHandler.Instance.AddTweenScale(gemEntity.Get<Transform2>(), OrigScale, 0.2f, EasingFunctions.Linear);

      BoundsCircle.Center = m_transform.Position;
      BoundsCircle.Radius = radius;

      m_sprite = gemEntity.Get<Sprite>();
      isTweeningStart = true;
      
      BaseValue = baseValue;
      //Bounds = bounds;
    }

    public void Reset(/*Entity gemEntity*/)
    {
      ShouldDestroy = false;
      PickedUp = false;
      WasClicked = false;
      ID = -1;
      m_entity = null;
      m_transform = null;
      m_targetHarvester = null;
      _tweener.CancelAndCompleteAll();
      timeSinceUpdateTweener = 0;


      //m_entity = gemEntity;
      //m_transform = m_entity.Get<Transform2>();

      // m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(1.0f, 1.0f), 2)
      //  .Easing(EasingFunctions.Linear);
    }

    public bool WasClicked = false;
    private float timeSinceUpdateTweener = 0.0f;

    public void Update(GameTime gameTime, Vector2 mouseWorldPos, bool isMouseClicked, float dt)
    {
      // WasClicked = false;
      //if (PickedUp)
      //{
      //  //TODO: only for instant collection upgrade?
      //  if (m_tween is { IsComplete: true })
      //  {
      //    //ShouldDestroy = true;
      //  }
      //}
      //


      //if (m_tween is { IsComplete: false })
      //{
      //  _tweener.Update(dt);
      //}


      if (isTweeningStart || isTweeningHarvester || isTweeningClicked)
      {
        timeSinceUpdateTweener += dt;
        // Slow down animations if fps drops, TODO: skip animation if fps drops even lopwer
        if(timeSinceUpdateTweener > 0.005f)
        {
          _tweener.Update(timeSinceUpdateTweener);
          timeSinceUpdateTweener = 0;
        }
      }

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

      // bool isMouseOver = BoundsCircle.Contains(mouseWorldPos);


      float clickRangeMultiplier = UpgradeManager.UG.ClickRadius;
      float gemWidth = TextureCache.HudRedGem.Value.Width * clickRangeMultiplier;
      float gemHeight = TextureCache.HudRedGem.Value.Height * clickRangeMultiplier;
      bool isMouseOver = mouseWorldPos.X >= m_transform.Position.X - gemWidth / 2 &&
                         mouseWorldPos.X <= m_transform.Position.X + gemWidth / 2 &&
                         mouseWorldPos.Y >= m_transform.Position.Y - gemHeight / 2 &&
                         mouseWorldPos.Y <= m_transform.Position.Y + gemHeight / 2;
      // bool isMouseOver = mouseWorldPos 


      if (isMouseOver)
      {
        m_sprite.Color = new Color(m_sprite.Color.R, m_sprite.Color.G, m_sprite.Color.B, (byte)255);
      }
      else
      {
        m_sprite.Color = new Color(m_sprite.Color.R, m_sprite.Color.G, m_sprite.Color.B, (byte)0);
      }

      if (isMouseClicked && isMouseOver && !PickedUp && !RenderGuiSystem.Instance.drawUpgradesGui)
      {
        // var dir = UntitledGemGameGameScreen.HomeBasePos - gemPos.Value;
        // dir.Normalize();
        // var distance = Vector2.Distance(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos);
        // gem.Get<Transform2>().Position += dir * 6.0f * (float)gameTime.GetElapsedSeconds() * distance;

        // m_transform.Position = UntitledGemGameGameScreen.HomeBasePos;
        // SetPickedUp(m_entity, EntityFactory.Instance.HomeBaseEntity, null);

        OnClicked(true);
      }

      BoundsCircle.Center = m_transform.Position;
    }

    public void FindOtherGems()
    {
      bool procc = RandomHelper.Int(0, 2) == 0;
      if (!procc) return;

      for (int i = 0; i < Math.Min(3, HarvesterCollectionSystem.Instance.m_gems2.Count); i++)
      {
        for (int attempt = 0; attempt < 100; attempt++)
        {
          var id = HarvesterCollectionSystem.Instance.m_gems2.GetRandom();
          var gemEntity = HarvesterCollectionSystem.Instance.GetEntityP(id);
          var gemPos = gemEntity?.Get<Transform2>()?.Position;

          if (gemPos == null)
            break;

          if (ChainLightningAbility.TargetLines.ContainsKey(id))
            continue;

          var gem = gemEntity.Get<Gem>();

          if (gem == null || gem.WasClicked)
            return;

          bool success = ChainLightningAbility.TargetLines2.TryAdd(id, new LineShape(gemPos.Value, m_transform.Position, 0.05f, Color.Yellow, Color.Yellow));

          if (success)
          {
            // TimerHelper.DoAfter(() =>
            // {
            //   ChainLightningAbility.TargetLines2.TryRemove(id, out var _);
            //
            //   var gem = gemEntity.Get<Gem>();
            //
            //   if (gem == null || gem.WasClicked)
            //     return;
            //
            //   gem.OnClicked(false);
            // }, 100, true);
            // gems.Add(id);

            gem.OnClicked(false);
            break;
          }
        }
      }
    }

    public void OnClicked(bool fromClick)
    {
      if (WasClicked && !fromClick)
        return;

      if (PickedUp)
        return;
      if (ShouldDestroy)
        return;

      WasClicked = true;
      _tweener.CancelAndCompleteAll();

      var gemTransform = m_entity.Get<Transform2>();
      m_tween = _tweener.TweenTo(gemTransform, transform => transform.Position, UntitledGemGameGameScreen.HomeBasePos, 0.5f)
        .Easing(EasingFunctions.Linear).OnEnd((tween) =>
        {
          isTweeningClicked = false;
        });

      m_tween2 = _tweener.TweenTo(gemTransform, transform => transform.Scale, Vector2.Zero, 0.5f)
        .Easing(EasingFunctions.CubicIn);

      // TweenHandler.Instance.AddTweenPosition(gemTransform, UntitledGemGameGameScreen.HomeBasePos, 0.5f, EasingFunctions.Linear);
      // TweenHandler.Instance.AddTweenScale(gemTransform, Vector2.Zero, 0.5f, EasingFunctions.CubicIn);

      isTweeningClicked = true;

      // FindOtherGems();
    }

    public void MergeGem(Vector2 position)
    {
      var gemTransform = m_entity.Get<Transform2>();

      PickedUp = true;
      _tweener.CancelAndCompleteAll();

      isTweeningHarvester = true;
      m_tween = _tweener.TweenTo(gemTransform, transform => transform.Position, position, 0.5f)
        .Easing(EasingFunctions.Linear).OnEnd((tween) =>
        {
          isTweeningHarvester = false;
          ShouldDestroy = true;
        }); ;

      m_tween2 = _tweener.TweenTo(gemTransform, transform => transform.Scale, Vector2.Zero, 0.5f)
        .Easing(EasingFunctions.CubicIn);

      // TweenHandler.Instance.AddTweenPosition(gemTransform, position, 0.5f, EasingFunctions.Linear, () => { ShouldDestroy = true; });
      // TweenHandler.Instance.AddTweenScale(gemTransform, Vector2.Zero, 0.5f, EasingFunctions.CubicIn);
    }

    public void SetPickedUp(Entity gemEntity, Entity harvesterEntity, Action onDone)
    {
      if (PickedUp) return;
      if (ShouldDestroy) return;

      PickedUp = true;
      isTweeningHarvester = true;

      m_targetHarvester = harvesterEntity.Get<Transform2>();
      var gemTransform = gemEntity.Get<Transform2>();

      _tweener.CancelAndCompleteAll();

      gemTransform.Scale = OrigScale;
      m_tween = _tweener.TweenTo(gemTransform, transform => transform.Scale, new Vector2(0.1f, 0.1f), 0.5f)
        .Easing(EasingFunctions.Linear).OnEnd((tween) =>
        {
          isTweeningHarvester = false;
        });

      m_tween.OnEnd(_ => { ShouldDestroy = true; });

      // TweenHandler.Instance.AddTweenScale(gemTransform, new Vector2(0.1f, 0.1f), 0.5f, EasingFunctions.Linear, () => { ShouldDestroy = true; });
    }
  }
}
