using JapeFramework.DataStructures;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Apos.Shapes;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Systems
{
  public class HarvesterCollectionSystem : EntityUpdateSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Gem> _gemMapper;

    private OrthographicCamera m_camera;

    private ShapeBatch m_shapeBatch;

    private Bag<int> _harvesters = new(500);

    public static HashSet<int> m_gems2 = new(100000000);

    public static SpatialTest spatialTest = new SpatialTest(100, 100);

    public static HarvesterCollectionSystem Instance;

    public HarvesterCollectionSystem(OrthographicCamera camera, ShapeBatch shapeBatch)
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
      m_camera = camera;
      m_shapeBatch = shapeBatch;
      Instance = this;
    }

    public Entity GetEntityP(int entityId)
    {
      return GetEntity(entityId);
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
      var position = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(GameMain.Instance.GraphicsDevice.Viewport.Width, GameMain.Instance.GraphicsDevice.Viewport.Height)));

      switch (Upgrades.HarvesterCollectionStrategy)
      {
        case HarvesterStrategy.RandomGemPosition:
          position = GetRandomGemPosition();
          break;
        case HarvesterStrategy.TargetCluster:
          var p = GetBiggestCluserPosition(harvester);
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

    //TODO: Calculate the clusters once per frame, not per harvester
    //TODO: Should this be random cluster?
    private Vector2? GetBiggestCluserPosition(Harvester harvester)
    {
      // int count = 0;
      //
      // List<ICollisionActor> actors = null;
      // foreach (var lists in spatialTest.GetBuckets())
      // {
      //   if (lists.Count(actor => actor.LayerName == "Gem") > count)
      //   {
      //     count = lists.Count;
      //     actors = lists;
      //   }
      // }
      //
      // foreach (var actor in actors)
      // {
      //   if (actor.LayerName == "Gem")
      //   {
      //     return actor.Bounds.BoundingRectangle.Center;
      //   }
      // }
      //
      // return actors?.FirstOrDefault()?.Bounds.Position;

      int count = 0;
      List<ICollisionActor> actors = null;

      var list = new List<(int count, List<ICollisionActor> actors)>();

      foreach (var lists in spatialTest.GetBuckets())
      {
        count = lists.Count;
        actors = lists;

        if (actors.Any(a => a.LayerName == "Gem"))
        {
          var distance = Vector2.Distance(harvester.Bounds.Position,
            actors.First(a => a.LayerName == "Gem").Bounds.Position);
          list.Add((count, actors));
        }
      }

      if (list.Count == 0)
        return UntitledGemGameGameScreen.HomeBasePos;

      var l = list.OrderByDescending(a => a.count);
      var r = random.Next(0, l.Count() / 4 + 1);

      return l.ElementAt(r).actors.First(a => a.LayerName == "Gem").Bounds.Position;
    }


    private Random random = new Random();

    private Vector2? GetBiggestCluserPositionWithDistance(Harvester harvester)
    {
      //Check for null when no gems etc

      int count = 0;
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

      if (list.Count == 0)
        return UntitledGemGameGameScreen.HomeBasePos;

      var l = list.OrderByDescending(a => a.count - a.distance * 0.05f * random.NextSingle(0.5f, 1.5f));
      return l.FirstOrDefault().actors.FirstOrDefault(a => a.LayerName == "Gem").Bounds.Position;
    }

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      if (harvester.ReturningToHomebase)
      {
        if (UntitledGemGameGameScreen.HomeBasePos == Vector2.Zero)
          return;

        UpdateMovement(UntitledGemGameGameScreen.HomeBasePos, gameTime, transform, harvester);
      }
      else if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(transform.Position, harvester.TargetScreenPosition.Value) < UpgradeManager.UG.HarvesterSpeed * 0.01f)
      {
        harvester.TargetScreenPosition = GetNewTargetPosition(harvester);
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        UpdateMovement(harvester.TargetScreenPosition.Value, gameTime, transform, harvester);
      }
    }

    private float LerpAngle(float currentAngle, float targetAngle, float amount)
    {
      float difference = targetAngle - currentAngle;

      // Wrap the difference to ensure it is between -PI and PI
      while (difference < -MathHelper.Pi) difference += MathHelper.TwoPi;
      while (difference > MathHelper.Pi) difference -= MathHelper.TwoPi;

      // Apply the interpolated difference to the current angle
      return currentAngle + difference * amount;
    }

    private void UpdateMovement(Vector2 target, GameTime gameTime, Transform2 transform, Harvester harvester)
    {
      if (harvester.CurrentState == Harvester.HarvesterState.None)
        return;

      var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

      var dir = target - transform.Position;
      dir.Normalize();
      var movement = dir * dt * UpgradeManager.UG.HarvesterSpeed * HomeBase.BonusMoveSpeed;

      float radians = (float)Math.Atan2(dir.Y, dir.X);
      var targetRotation = radians + (float)Math.PI / 2;

      // transform.Rotation = targetRotation;

      // var q1 = new Quaternion(0, 0, 0, transform.Rotation);
      // // var q2 = new Quaternion(0, 0, 0, radians + (float)Math.PI / 2
      // var q2 = new Quaternion(0, 0, 0, targetRotation);
      // //
      // Quaternion.Slerp(ref q1, ref q2, dt * 20.0f, out Quaternion qResult);
      // transform.Rotation = qResult.W;
      // Console.WriteLine(qResult);

      // transform.Rotation = MathHelper.Lerp(transform.Rotation, radians + (float)Math.PI / 2, dt * 20.0f);
      transform.Rotation = LerpAngle(transform.Rotation, radians + (float)Math.PI / 2, dt * 20.0f);
      // Quaternion.Slerp()

      var fuelCost = movement.Length() * (2.0f - UpgradeManager.UG.FuelEfficiency);

      //TODO: fix distance check, currently overshooting target
      var dist = Vector2.Distance(transform.Position, target);
      var dist2 = Vector2.Distance(transform.Position + movement, target);
      var dist3 = Vector2.Distance(transform.Position + movement, transform.Position);
      // var dist3 = Vector2.Distance(transform.Position, target);
      var moveLen = movement.Length();
      // Console.WriteLine($"Harvester moving. Dist: {dist}, dist2: {dist2}, moveLen: {moveLen} - {dt * UpgradeManager.UG.HarvesterSpeed} - {dist3}");
      if (dist3 > dist)
      {
        transform.Position = target;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 1, 1);
        harvester.Fuel -= fuelCost;
        // Console.WriteLine("Harvester reached target position.");
        // movement = target - transform.Position;
        // fuelCost = movement.Length() * (2.0f - UpgradeManager.UG.FuelEfficiency);
      }
      else if (harvester.Fuel > fuelCost)
      {
        transform.Position += movement;
        harvester.Bounds = new RectangleF(transform.Position.X, transform.Position.Y, 1, 1);
        harvester.Fuel -= fuelCost;

        harvester.m_sprite.Alpha = harvester.Fuel / UpgradeManager.UG.HarvesterMaxFuel;
      }
      else if (harvester.CurrentState == Harvester.HarvesterState.Collecting)
      {
        harvester.CurrentState = Harvester.HarvesterState.OutOfFuel;
      }
    }

    private void CollectGem(Gem gem, Harvester harvester)
    {
      if (gem.PickedUp)
        return;

      var gemEntity = GetEntity(gem.ID);
      var harvesterEntity = GetEntity(harvester.ID);

      gem.SetPickedUp(gemEntity, harvesterEntity, () =>
      {
      });

      harvester.PickedUpGem(gem);

      ++UntitledGemGameGameScreen.Collected;
      m_gems2.Remove(gem.ID);
      spatialTest.Remove(gem);
    }

    private void UpdateHarvesters(int index, GameTime gameTime, List<Gem>[] collectedGems)
    {
      var activeEntity = _harvesters.ElementAt(index);
      collectedGems[index] = [];
      var harvester = GetEntity(activeEntity).Get<Harvester>();
      var transform = GetEntity(activeEntity).Get<Transform2>();

      if (harvester.CurrentState == Harvester.HarvesterState.None && !UpgradeManager.UG.HomeBaseCollector)
      {
        return;
      }

      UpdateHarvesterPosition(gameTime, harvester, transform);

      var collectionRange = harvester.CurrentState == Harvester.HarvesterState.None ?
        UpgradeManager.UG.HomebaseCollectionRange : UpgradeManager.UG.HarvesterCollectionRange;

      // if (UpgradeManager.UG.HomebaseMagnetizer > 0 || HomeBase.BonusMagnetPower > 0)

      var q = spatialTest.Query2(transform.Position, (int)(collectionRange * 2.0f));

      if (harvester.CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity)
      {
        if (UntitledGemGameGameScreen.HomeBasePos != Vector2.Zero && Vector2.Distance(transform.Position, UntitledGemGameGameScreen.HomeBasePos) < 15)
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
          if (harvester.CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity)
            break;

          if (qq is Gem { PickedUp: false } gem)
          {
            if (Vector2.Distance(harvester.Bounds.Position, gem.Bounds.Position) < collectionRange)
            {
              collectedGems[index].Add(gem);
            }
          }
        }
      }

      // m_shapeBatch.Begin();
      // m_shapeBatch.DrawLine(harvester.Bounds.Position, harvester.TargetScreenPosition.Value, 1.0f, Color.AliceBlue, Color.White, 1, 1.5f);
      // m_shapeBatch.End();
    }

    private bool MultiThreadingEnabled = false;

    public override void Update(GameTime gameTime)
    {
      var refuel = KeyboardExtended.GetState().WasKeyPressed(Keys.R);

      var collectedGems = new List<Gem>[_harvesters.Count];

      spatialTest.RefreshBuckets();

      if (MultiThreadingEnabled)
      {
        var p = Parallel.For(0, _harvesters.Count, (index) =>
        {
          UpdateHarvesters(index, gameTime, collectedGems);
        });

        while (!p.IsCompleted) { }
      }
      else
      {
        for (var i = 0; i < _harvesters.Count; i++)
        {
          UpdateHarvesters(i, gameTime, collectedGems);
        }
      }

      for (var i = 0; i < _harvesters.Count; i++)
      {
        var activeEntity = _harvesters[i];
        var harvester = GetEntity(activeEntity).Get<Harvester>();

        if (harvester.IsDrone)
        {
          foreach (var gem in collectedGems[i])
          {
            CollectGem(gem, harvester);
          }

          UntitledGemGameGameScreen.DeliveredUncounted += harvester.CarryingGemCount;
          harvester.CarryingGemCount = 0;

          continue;
        }

        if (harvester.ReachedHome)
        {
          UntitledGemGameGameScreen.DeliveredUncounted += harvester.CarryingGemCount;
          harvester.CarryingGemCount = 0;
          harvester.ReachedHome = false;
          harvester.TargetScreenPosition = null;

          if (UpgradeManager.UG.RefuelHomebase)
          {
            harvester.SetFuelMax();
          }
        }
        else
        {
          foreach (var gem in collectedGems[i])
          {
            CollectGem(gem, harvester);
          }
        }

        if (harvester.CurrentState == Harvester.HarvesterState.OutOfFuel)
        {
          var vec = m_camera.WorldToScreen(new Vector2(harvester.Bounds.BoundingRectangle.Right, harvester.Bounds.BoundingRectangle.Top));
          harvester.ReuqestRefuel(vec);
        }

        if ((refuel || UpgradeManager.UG.AutoRefuel) && harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
        {
          harvester.Refuel();
        }

        harvester.Update(gameTime);
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
      //}

    }
  }
}
