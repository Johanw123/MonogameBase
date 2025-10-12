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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework.Input;
using MonoGameGum;
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
    //public static JFSpatialHash shash = new(new Vector2(100, 100));
    //public static QuadTreeSpace qtSpace = new QuadTreeSpace(new RectangleF(0, 0, 2000, 2000));

    public static SpatialTest spatialTest = new SpatialTest(100,100);

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
        spatialTest.Add(harvester);
      }
      else
      {
        var gem = _gemMapper.Get(entityId);
        if (gem != null)
        {
          gem.ID = entityId;
          m_gems2.Add(entityId);
          spatialTest.Add(gem);
        }
      }
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);

      if (harvester != null)
      {
        _harvesters.Remove(entityId);
        spatialTest.Remove(harvester);
      }
      else
      {
        var gem = _gemMapper.Get(entityId);
        if (gem != null)
        {
          //m_gems2.Remove(entityId);
        }
      }
    }

    private Vector2 GetNewTargetPosition(Harvester harvester)
    {
      var position = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));

      switch (Upgrades.HarvesterCollectionStrategy)
      {
        case HarvesterStrategy.RandomGemPosition:
          position = GetRandomGemPosition();
          break;
        case HarvesterStrategy.TargetCluster:
          var p = GetBiggestCluserPosition();
          if (p != null)
            position = p.Value;
          break;
        case HarvesterStrategy.TargetClosestCluster:
          var p2 = GetBiggestCluserPositionWithDistance(harvester);
          if (p2 != null)
            position = p2.Value;
          break;
      }

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
      foreach (var lists in spatialTest.GetBuckets())
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

    private Vector2? GetBiggestCluserPositionWithDistance(Harvester harvester)
    {
      Vector2? position = null;

      //Check for null when no gems etc

      int count = 0;
      int id = 0;
      List<ICollisionActor> actors = null;

      var list = new List<(int count, float distance, List<ICollisionActor> actors)>();

      foreach (var lists in spatialTest.GetBuckets())
      {
        count = lists.Count;
        actors = lists;

        if (actors.Any(a => a.LayerName == "Gem"))
        {
          var distance = Vector2.Distance(harvester.Bounds.Position,
            actors.First(a => a.LayerName == "Gem").Bounds.Position);
          list.Add((count, distance, actors));
        }
      }

      var l = list.OrderByDescending(a => a.count - a.distance * 0.05f);
      return l.FirstOrDefault().actors.First(a => a.LayerName == "Gem").Bounds.Position;
    }

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      if (harvester.CarryingGemCount >= Upgrades.HarvesterCapacity)
      {
        UpdateMovement(UntitledGemGameGameScreen.HomeBasePos, gameTime, transform, harvester);
      }
      else if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(transform.Position, harvester.TargetScreenPosition.Value) < Upgrades.HarvesterSpeed * 0.01f)
      {
        harvester.TargetScreenPosition = GetNewTargetPosition(harvester);
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        UpdateMovement(harvester.TargetScreenPosition.Value, gameTime, transform, harvester);
      }
    }

    private void UpdateMovement(Vector2 target, GameTime gameTime, Transform2 transform, Harvester harvester)
    {
      var dir = target - transform.Position;
      dir.Normalize();
      var movement = dir * (float)gameTime.ElapsedGameTime.TotalSeconds * Upgrades.HarvesterSpeed;

      if (harvester.Fuel > movement.Length())
      {
        transform.Position += movement;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 1, 1);
        harvester.Fuel -= movement.Length();
      }
      else
      {
        harvester.IsOutOfFuel = true;
      }
    }

    private void CollectGem(Gem gem, Harvester harvester)
    {
      if (gem.PickedUp)
        return;

      //harvester.CarryingGems.Add(gem.ID);
      gem.SetPickedUp(GetEntity(gem.ID), GetEntity(harvester.ID), () =>
      {
        //var e = GetEntity(gem.ID);
        //e.Destroy();
        //EntityFactory.GemPool.Free(e.Get<Gem>());
      });

      harvester.PickedUpGem(gem);

      ++UntitledGemGameGameScreen.Collected;
      m_gems2.Remove(gem.ID);
      spatialTest.Remove(gem);
    }

    public override void Update(GameTime gameTime)
    {
      var refuel = KeyboardExtended.GetState().WasKeyPressed(Keys.R);

      var collectedGems = new List<Gem>[_harvesters.Count];

      var p = Parallel.For(0, _harvesters.Count, (index) =>
      {
        var activeEntity = _harvesters.ElementAt(index);
        //a2[index] = new ValueTuple<int, List<Gem>>();
        collectedGems[index] = [];
        //a.AddOrUpdate(activeEntity, new List<Gem>());
        var harvester = GetEntity(activeEntity).Get<Harvester>();
        var transform = GetEntity(activeEntity).Get<Transform2>();

        UpdateHarvesterPosition(gameTime, harvester, transform);

        var q = spatialTest.Query2(transform.Position, Upgrades.HarvesterCollectionRange * 2);

        if (harvester.CarryingGemCount >= Upgrades.HarvesterCapacity)
        {
          if (Vector2.Distance(transform.Position, UntitledGemGameGameScreen.HomeBasePos) < 15)
          {
            harvester.ReachedHome = true;
          }
        }
        else
        {
          //https://www.monogameextended.net/docs/features/collision/
          // Add layer so harvester <-> harvester doesnt need to be checked?
          foreach (var qq in q)
          {
            if (harvester.CarryingGemCount >= Upgrades.HarvesterCapacity)
              break;

            if (qq is Gem { PickedUp: false } gem)
            {
              if (Vector2.Distance(harvester.Bounds.Position, gem.Bounds.Position) <
                  Upgrades.HarvesterCollectionRange)
              {
                collectedGems[index].Add(gem);
              }
            }
          }
        }
      });

      while (!p.IsCompleted){}

      for (var i = 0; i < _harvesters.Count; i++)
      {
        var activeEntity = _harvesters[i];
        var harvester = GetEntity(activeEntity).Get<Harvester>();
        if (harvester.ReachedHome)
        {
          UntitledGemGameGameScreen.Delivered += harvester.CarryingGemCount;
          harvester.CarryingGemCount = 0;
          harvester.ReachedHome = false;
          harvester.TargetScreenPosition = null;
        }
        else
        {
          foreach (var gem in collectedGems[i])
          {
            CollectGem(gem, harvester);
          }
        }

        if (harvester.IsOutOfFuel && !harvester.RequestingRefuel)
        {
          var vec = m_camera.WorldToScreen(new Vector2(harvester.Bounds.BoundingRectangle.Right, harvester.Bounds.BoundingRectangle.Top));
          harvester.ReuqestRefuel(vec);
        }

        if (refuel || Upgrades.AutoRefuel)
        {
          harvester.Refuel();
        }

        harvester.Update(gameTime);
      }

      //Single threaded version
      //foreach (var activeEntity in _harvesters)
      //{
      //  var harvester = GetEntity(activeEntity).Get<Harvester>();
      //  var transform = GetEntity(activeEntity).Get<Transform2>();

      //  var q = spatialTest.Query2(transform.Position, Upgrades.HarvesterCollectionRange * 2);

      //  if (harvester.CarryingGemCount >= harvester.CurrentCapacity)
      //  {
      //    // Return to home base
      //    if (Vector2.Distance(transform.Position, UntitledGemGameGameScreen.HomeBasePos) < 15)
      //    {
      //      //foreach (var gemId in harvester.CarryingGems)
      //      //{
      //      //  var e = GetEntity(gemId);
      //      //  if (e != null)
      //      //  {
      //      //    e.Destroy();
      //      //    EntityFactory.GemPool.Free(e.Get<Gem>());
      //      //    ++UntitledGemGameGameScreen.Delivered;
      //      //  }
      //      //}

      //      //harvester.CarryingGems.Clear();

      //      UntitledGemGameGameScreen.Delivered += harvester.CarryingGemCount;
      //      harvester.CarryingGemCount = 0;
      //    }
      //  }
      //  else
      //  {
      //    //https://www.monogameextended.net/docs/features/collision/
      //    // Add layer so harvester <-> harvester doesnt need to be checked?
      //    foreach (var qq in q)
      //    {
      //      if (harvester.CarryingGemCount >= harvester.CurrentCapacity)
      //        break;

      //      if (qq is Gem { PickedUp: false } gem)
      //      {
      //        if (Vector2.Distance(harvester.Bounds.Position, gem.Bounds.Position) <
      //            Upgrades.HarvesterCollectionRange)
      //        {
      //          CollectGem(gem, harvester);
      //        }
      //        //_activeGems.Remove(gem.ID);
      //      }
      //    }
      //  }

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
      //}

    }
  }
}