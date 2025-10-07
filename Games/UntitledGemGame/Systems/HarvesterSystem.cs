using Assimp;
using JapeFramework.DataStructures;
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
using MonoGame.Extended.Timers;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using static Assimp.Metadata;

namespace UntitledGemGame.Systems
{
  public class HarvesterCollectionSystem : EntityUpdateSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Gem> _gemMapper;

    private OrthographicCamera m_camera;

    private Bag<int> _harvesters = new(500);
    //public static Bag<int> _gems = new(1000000);
    //public static Bag<int> _activeGems = new(1000000);

    public static HashSet<int> m_gems2 = new(100000000);

    public static JFSpatialHash shash = new(new Vector2(100, 100));

    public HarvesterCollectionSystem(OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
      m_camera = camera;
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
          //_gems.Add(entityId);
          //_activeGems.Add(entityId);
          m_gems2.Add(entityId);
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
         // _gems.Remove(entityId);
          m_gems2.Remove(entityId);
        }
      }
    }

    private Vector2 GetNewTargetPosition()
    {
      Vector2 position;


      //Random position
      position = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));

      //Random Gem location
      position = GetRandomGemPosition();

      //Target cluster
      //var p = GetBiggestCluserPosition();
      //if (p != null)
      //  position = p.Value;


      return position;
    }

    private Vector2 GetRandomGemPosition()
    {
      var position = Vector2.Zero;

      int count = 0;
      while (position == Vector2.Zero)
      {
        var rand = m_gems2.GetRandom();
        var e = GetEntity(rand);
        var gem = e?.Get<Gem>();
        if (gem is { PickedUp: false })
        {
          position = e.Get<Transform2>().Position;
        }

        ++count;

        if (count > 100)
          break;
      }


      return position;
    }

    private Vector2? GetBiggestCluserPosition()
    {
      Vector2? position = null;

      int count = 0;
      int id = 0;
      List<ICollisionActor> actors = null;
      foreach (var lists in shash.GetDictionary().Values)
      {
        if (lists.Count(actor => actor.LayerName == "Gem") > count)
        {
          count = lists.Count;
          actors = lists;
        }
      }

      foreach (var a in actors)
      {
        if (a.LayerName == "Gem")
        {
          return a.Bounds.BoundingRectangle.Center;
        }
      }


      return actors?.FirstOrDefault()?.Bounds.Position;
    }

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      //var harvester = _harvesterMapper.Get(entityId);
      //var transform = _transformMapper.Get(entityId);

      if (harvester.CarryingGems.Count >= harvester.CurrentCapacity)
      {
        Vector2 dir = UntitledGemGameGameScreen.HomeBasePos - transform.Position;
        dir.Normalize();
        transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 1000.0f;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
      }
      else if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(transform.Position, harvester.TargetScreenPosition.Value) < 10)
      {
        harvester.TargetScreenPosition = GetNewTargetPosition();
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        Vector2 dir = harvester.TargetScreenPosition.Value - transform.Position;
        dir.Normalize();
        transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 1000.0f;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 55, 55);
        // Console.WriteLine("Move: " + transform.Position + " - " + harvester.TargetScreenPosition);
      }
    }

    public override void Update(GameTime gameTime)
    {
      foreach (var activeEntity in _harvesters)
      {
        var harvester = GetEntity(activeEntity).Get<Harvester>();
        var transform = GetEntity(activeEntity).Get<Transform2>();

        UpdateHarvesterPosition(gameTime, harvester, transform);

        var rect = harvester.Bounds.BoundingRectangle;
        //new RectangleF(harvester.Bounds.Position, new SizeF(1,1))
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

            harvester.CarryingGems.Clear();
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
              //_activeGems.Remove(gem.ID);
            }
          }

          //foreach (var harvesterCarryingGem in harvester.CarryingGems)
          //{
          //  var gemEntity = GetEntity(harvesterCarryingGem);
          //  var gem = gemEntity.Get<Gem>();
          //  //gem.Update(gameTime);

          //  if (gem.ShouldDestroy)
          //  {
          //    gemEntity.Destroy();
          //    harvester.CarryingGems.Remove(harvesterCarryingGem);
          //  }
          //}
        }

        // TODO: THis should be cleared when reaching home station instead
        // TODO: Keep this for instant collection upgrade
        //foreach (var harvesterCarryingGem in harvester.CarryingGems.ToArray())
        //{
        //  var gemEntity = GetEntity(harvesterCarryingGem);
        //  var gem = gemEntity?.Get<Gem>();

        //  if (gemEntity == null || gem.ShouldDestroy)
        //  {
        //    harvester.CarryingGems.Remove(harvesterCarryingGem);
        //  }
        //}
      }
    }
  }
}