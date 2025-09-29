using Assimp;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.Collisions.QuadTree;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using static Assimp.Metadata;

namespace UntitledGemGame.Systems
{
  public class HarvesterMoveSystem : EntityProcessingSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Transform2> _transformMapper;

    private OrthographicCamera m_camera;

    public HarvesterMoveSystem(OrthographicCamera camera) : base(Aspect.All(typeof(Harvester), typeof(Transform2)))
    {
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _harvesterMapper = mapperService.GetMapper<Harvester>();
      _transformMapper = mapperService.GetMapper<Transform2>();
    }

    public override void Process(GameTime gameTime, int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);

      if (harvester.CarryingGems.Count >= harvester.CurrentCapacity)
      {
        Vector2 dir = UntitledGemGameGameScreen.HomeBasePos - transform.Position;
        dir.Normalize();
        transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 100.0f;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
      }
      else if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(transform.Position, harvester.TargetScreenPosition.Value) < 10)
      {
        var newTargetPos = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        harvester.TargetScreenPosition = newTargetPos;
        //Console.WriteLine("New Pos: " + harvester.TargetScreenPosition);
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        Vector2 dir = harvester.TargetScreenPosition.Value - transform.Position;
        dir.Normalize();
        transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 100.0f;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
        // Console.WriteLine("Move: " + transform.Position + " - " + harvester.TargetScreenPosition);
      }
    }
  }

  public class HarvesterCollectionSystem : EntityUpdateSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Gem> _gemMapper;

    private Bag<int> _harvesters = new(50);
    private Bag<int> _gems = new(10000);

    SpatialHash shash = new(new Vector2(5000, 5000));

    public HarvesterCollectionSystem()
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _harvesterMapper = mapperService.GetMapper<Harvester>();
      _gemMapper = mapperService.GetMapper<Gem>();
    }

    protected override void OnEntityAdded(int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);

      if (harvester != null)
      {
        _harvesters.Add(entityId);
        shash.Insert(harvester);
      }
      else
      {
        var gem = _gemMapper.Get(entityId);
        if (gem != null)
        {
          gem.ID = entityId;
          _gems.Add(entityId);
          shash.Insert(gem);
        }
      }
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);

      if (harvester != null)
      {
        _harvesters.Remove(entityId);
        shash.Remove(harvester);
      }
      else
      {
        var gem = _gemMapper.Get(entityId);
        if (gem != null)
        {
          _gems.Remove(entityId);
          shash.Remove(gem);
        }
      }
    }

    public override void Update(GameTime gameTime)
    {
      foreach (var activeEntity in _harvesters)
      {
        var harvester = GetEntity(activeEntity).Get<Harvester>();
        var transform = GetEntity(activeEntity).Get<Transform2>();

        var rect = harvester.Bounds.BoundingRectangle;
        var q = shash.Query(rect);

        if (harvester.CarryingGems.Count >= harvester.CurrentCapacity)
        {
          // Return to home base
          if (Vector2.Distance(transform.Position, UntitledGemGameGameScreen.HomeBasePos) < 15)
          {
            foreach (var gemId in harvester.CarryingGems)
            {
              var e = GetEntity(gemId);
              if (e != null)
              {
                e.Destroy();
                EntityFactory.GemPool.Free(e.Get<Gem>());
                ++UntitledGemGameGameScreen.Delivered;
              }
            }
          }
        }
        else
        {
          //https://www.monogameextended.net/docs/features/collision/
          // Add layer so harvester <-> harvester doesnt need to be checked?
          foreach (var qq in q)
          {
            if (harvester.CarryingGems.Count >= harvester.CurrentCapacity)
              break;

            if (qq is Gem { PickedUp: false } gem)
            {
              harvester.CarryingGems.Add(gem.ID);
              gem.SetPickedUp(GetEntity(gem.ID), GetEntity(activeEntity));
              ++UntitledGemGameGameScreen.Collected;
              shash.Remove(gem);
            }
          }

          foreach (var harvesterCarryingGem in harvester.CarryingGems)
          {
            var gemEntity = GetEntity(harvesterCarryingGem);
            var gem = gemEntity.Get<Gem>();
            //gem.Update(gameTime);

            if (gem.ShouldDestroy)
            {
              gemEntity.Destroy();
            }
          }
        }

        // TODO: THis should be cleared when reaching home station instead
        // TODO: Keep this for instant collection upgrade
        foreach (var harvesterCarryingGem in harvester.CarryingGems.ToArray())
        {
          var gemEntity = GetEntity(harvesterCarryingGem);
          var gem = gemEntity?.Get<Gem>();

          if (gemEntity == null || gem.ShouldDestroy)
          {
            harvester.CarryingGems.Remove(harvesterCarryingGem);
          }
        }
      }
    }
  }
}