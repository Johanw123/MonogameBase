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
using JapeFramework;
using RenderingLibrary;
using GUI.Shared.Helpers;
using System.Threading;

namespace UntitledGemGame.Systems
{
  public class HarvesterCollectionSystem : EntityUpdateSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Gem> _gemMapper;

    private OrthographicCamera m_camera;

    private ShapeBatch m_shapeBatch;

    public Bag<int> _harvesters = new(500);
    // public HashSet<int> m_gems2 = new(100000000);

    // private SpatialTest spatialTest = new SpatialTest(100, 100);
    // SpatialHashGrid grid = new SpatialHashGrid(
    //     cellSize: 64f,       // e.g., 64x64 pixel cells
    //     gridWidth: 64,       // 2048px wide world
    //     gridHeight: 64,      // 2048px high world
    //     maxActors: 10000000      // Max objects allowed simultaneously
    // );

    // private SpatialTest spatialTest = new SpatialTest(64, 10000);
    public FlatSpatialHash flatSpatialHash = new FlatSpatialHash(50000, 30);

    public static HarvesterCollectionSystem Instance;


    public HarvesterCollectionSystem(OrthographicCamera camera, ShapeBatch shapeBatch)
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite), typeof(Harvester)))
    // : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
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
        // spatialTest.Add(harvester);
      }
      else
      {
        // var gem = _gemMapper.Get(entityId);
        // if (gem != null)
        // {
        //   var gridId = HarvesterCollectionSystem.Instance.flatSpatialHash.AddGem(entityId, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y);
        //   gem.GridIndex = gridId;
        //   Console.WriteLine("Add to grid: " + gridId);
        // }
        // var gem = _gemMapper.Get(entityId);
        // if (gem != null)
        // {
        //   gem.Id = entityId;
        //   // m_gems2.Add(entityId);
        //   // spatialTest.Add(gem);
        //   var gridId = flatSpatialHash.AddGem(gem.Id, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y);
        //   gem.GridIndex = gridId;
        // }
      }
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);

      if (harvester != null)
      {
        _harvesters.Remove(entityId);
        // spatialTest.Remove(harvester);
      }
      // else
      // {
      //   var gem = _gemMapper.Get(entityId);
      //   if (gem != null)
      //   {
      //     // m_gems2.Remove(entityId);
      //   }
      // }
    }

    private Vector2 GetNewTargetPosition(Harvester harvester)
    {
      // var width = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth;
      // var height = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight;
      // var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      // var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      // var position = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(width, height)));
      var position = RandomHelper.Vector2(p0, p1);

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

    private Random m_random = new Random();

    private Vector2 GetRandomGemPosition()
    {
      var position = Vector2.Zero;

      var idx = flatSpatialHash.GetRandomActiveGemIndex(m_random);
      var x = flatSpatialHash.Gems[idx].X;
      var y = flatSpatialHash.Gems[idx].Y;
      position.X = x;
      position.Y = y;

      // int count = 0;
      // while (position == Vector2.Zero)
      // {
      //   var rand = m_gems2.GetRandom();
      //   var e = GetEntity(rand);
      //   var gem = e?.Get<Gem>();
      //   if (gem is { PickedUp: false })
      //   {
      //     position = e.Get<Transform2>().Position;
      //   }
      //
      //   ++count;
      //
      //   if (count > 100)
      //     break;
      // }

      return position;
    }

    //TODO: Calculate the clusters once per frame, not per harvester
    //TODO: Should this be random cluster?
    // private Vector2? GetBiggestCluserPosition(Harvester harvester)
    // {
    //   int[] denseBuckets = new int[flatSpatialHash._tableSize];
    //   flatSpatialHash.GetDenseBuckets(0, denseBuckets, out int bucketCount);
    //
    // }

    private Random random = new Random();
    private Vector2? GetBiggestCluserPosition(Harvester harvester)
    {
      if (flatSpatialHash.TryGetWeightedClusterPosition(m_random, out Vector2 weightedTarget, minGems: 3))
      {
        return weightedTarget;
      }

      return null;
    }


    private Vector2? GetBiggestCluserPositionWithDistance(Harvester harvester)
    {
      if (flatSpatialHash.TryGetBestScoringClusterPosition(harvester.BoundingCircle.Center, out Vector2 target, minGems: 4, minSearchRadius: 40.0f))
      {
        // Add that slight jitter we talked about so they don't stack on the exact same pixel
        float offsetX = (float)(m_random.NextDouble() * 30.0 - 15.0);
        float offsetY = (float)(m_random.NextDouble() * 30.0 - 15.0);

        return target + new Vector2(offsetX, offsetY);
      }

      return null;
    }

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      var speed = harvester.IsDrone ? UpgradeManager.UG.DroneSpeed : UpgradeManager.UG.HarvesterSpeed;

      if (harvester.ReturningToHomebase)
      {
        if (UntitledGemGameGameScreen.HomeBasePos == Vector2.Zero)
          return;

        UpdateMovement(UntitledGemGameGameScreen.HomeBasePos, gameTime, transform, harvester);
      }
      else if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(transform.Position, harvester.TargetScreenPosition.Value) < speed * 0.01f)
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
      harvester.TimeAlive += dt;

      var diff = target - transform.Position;
      var distSq = diff.LengthSquared();

      // Early exit if we are practically at the target to avoid NaN during division
      if (distSq < 0.0001f) return;

      // 1. Math Optimization: Only calculate one square root for distance
      var dist = (float)Math.Sqrt(distSq);
      var dir = diff / dist;

      // 2. Cache repeated property accesses
      var ug = UpgradeManager.UG;
      var isDrone = harvester.IsDrone;

      var speed = isDrone ? ug.DroneSpeed : ug.HarvesterSpeed;

      // Calculate movement scalar rather than doing vector.Length() multiple times
      var moveLen = dt * speed * HomeBase.BonusMoveSpeed;
      var movement = dir * moveLen;

      if (moveLen > 0.05f)
        harvester.PositionMoved = true;

      var fuelCost = isDrone ? 0f : moveLen * (2.0f - ug.FuelEfficiency);

      // Hardcode Pi/2 constant to avoid calculating it every frame
      float radians = (float)Math.Atan2(dir.Y, dir.X);
      transform.Rotation = LerpAngle(transform.Rotation, radians + 1.570796f, dt * 20.0f);

      // 3. Fix Overshoot check using simple scalars
      if (moveLen >= dist)
      {
        transform.Position = target;
        harvester.Fuel -= fuelCost;
        harvester.MovedDistance += dist; // Add exact distance to target
      }
      else if (harvester.Fuel > fuelCost)
      {
        transform.Position += movement;
        harvester.Fuel -= fuelCost;
        harvester.MovedDistance += moveLen;
      }
      else if (harvester.CurrentState == Harvester.HarvesterState.Collecting)
      {
        harvester.CurrentState = Harvester.HarvesterState.OutOfFuel;
      }

      // ⚠️ See notes below on how to optimize this further
      //        return new BoundingBox2D(position, position + size);
      // var box = BoundingBox2D.CreateFromPositionAndSize(transform.Position, Vector2.One);
      // harvester.Shape = new CollisionShape2D(box);

      // harvester.BoundingCircle.Center = transform.Position;
      harvester.SetCollisionPosition(transform.Position);

      if (isDrone && harvester.TimeAlive > ug.IncreaseDroneFuel)
      {
        harvester.MarkedForDestroy = true;
      }

      if (!isDrone && ug.HarvesterDrones > 0 && harvester.MovedDistance > ug.HarvesterDronesTravelDistance)
      {
        // 4. Eliminate LINQ allocation
        bool isDroneActive = false;
        foreach (var ability in HomeBase.Instance.ActiveAbilities)
        {
          if (ability is DroneAbility)
          {
            isDroneActive = true;
            break;
          }
        }

        if (isDroneActive)
        {
          harvester.MovedDistance = 0;
          TimerHelper.DoEndOfFrame(() =>
          {
            var spawnPos = transform.Position + new Vector2(random.NextSingle(-5, 5), random.NextSingle(-5, 5));
            var drone = EntityFactory.Instance.CreateDrone(spawnPos);
          });
        }
      }
    }
    // private void UpdateMovement(Vector2 target, GameTime gameTime, Transform2 transform, Harvester harvester)
    // {
    //   if (harvester.CurrentState == Harvester.HarvesterState.None)
    //     return;
    //
    //   var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
    //   harvester.TimeAlive += dt;
    //
    //   var dir = target - transform.Position;
    //   dir.Normalize();
    //   var speed = harvester.IsDrone ? UpgradeManager.UG.DroneSpeed : UpgradeManager.UG.HarvesterSpeed;
    //   var movement = dir * dt * speed * HomeBase.BonusMoveSpeed;
    //
    //   if(movement.Length() > 0.05f)
    //     harvester.PositionMoved = true;
    //
    //   float radians = (float)Math.Atan2(dir.Y, dir.X);
    //   var targetRotation = radians + (float)Math.PI / 2;
    //
    //   transform.Rotation = LerpAngle(transform.Rotation, radians + (float)Math.PI / 2, dt * 20.0f);
    //   // Quaternion.Slerp()
    //
    //   var fuelCost = movement.Length() * (2.0f - UpgradeManager.UG.FuelEfficiency);
    //
    //   if(harvester.IsDrone)
    //     fuelCost = 0;
    //
    //   //TODO: fix distance check, currently overshooting target
    //   var dist = Vector2.Distance(transform.Position, target);
    //   var dist2 = Vector2.Distance(transform.Position + movement, target);
    //   var dist3 = Vector2.Distance(transform.Position + movement, transform.Position);
    //   // var dist3 = Vector2.Distance(transform.Position, target);
    //   var moveLen = movement.Length();
    //   // Console.WriteLine($"Harvester moving. Dist: {dist}, dist2: {dist2}, moveLen: {moveLen} - {dt * UpgradeManager.UG.HarvesterSpeed} - {dist3}");
    //   if (dist3 > dist)
    //   {
    //     transform.Position = target;
    //     var box = BoundingBox2D.CreateFromPositionAndSize(transform.Position, Vector2.One);
    //     harvester.Shape = new CollisionShape2D(box);
    //     harvester.Fuel -= fuelCost;
    //     // Console.WriteLine("Harvester reached target position.");
    //     // movement = target - transform.Position;
    //     // fuelCost = movement.Length() * (2.0f - UpgradeManager.UG.FuelEfficiency);
    //     //
    //     harvester.MovedDistance += movement.Length();
    //   }
    //   else if (harvester.Fuel > fuelCost)
    //   {
    //     transform.Position += movement;
    //     var box = BoundingBox2D.CreateFromPositionAndSize(transform.Position, Vector2.One);
    //     harvester.Shape = new CollisionShape2D(box);
    //     harvester.Fuel -= fuelCost;
    //
    //     harvester.MovedDistance += movement.Length();
    //
    //     // harvester.m_sprite.Alpha = harvester.Fuel / UpgradeManager.UG.HarvesterMaxFuel;
    //   }
    //   else if (harvester.CurrentState == Harvester.HarvesterState.Collecting)
    //   {
    //     harvester.CurrentState = Harvester.HarvesterState.OutOfFuel;
    //   }
    //
    //   // if (harvester.MovedDistance > 105 && harvester.IsDrone)
    //   if (harvester.TimeAlive > UpgradeManager.UG.IncreaseDroneFuel && harvester.IsDrone)
    //   {
    //     harvester.MarkedForDestroy = true;
    //     // TimerHelper.DoEndOfFrame(() =>
    //     //     {
    //     //       harvester.Entity.Destroy();
    //     //     });
    //   }
    //
    //   var isDroneActive = HomeBase.Instance.ActiveAbilities.Any(a => a is DroneAbility);
    //
    //   if (harvester.MovedDistance > UpgradeManager.UG.HarvesterDronesTravelDistance && !harvester.IsDrone && UpgradeManager.UG.HarvesterDrones > 0 && isDroneActive)
    //   {
    //     harvester.MovedDistance = 0;
    //
    //     TimerHelper.DoEndOfFrame(() =>
    //         {
    //           {
    //             var drone = EntityFactory.Instance.CreateDrone(transform.Position + new Vector2(random.NextSingle(-5, 5), random.NextSingle(-5, 5)));
    //             Console.WriteLine("Created: " + drone.Id);
    //           }
    //         });
    //   }
    // }

    public void CollectGem(Gem gem, Harvester harvester)
    {
      if (gem.PickedUp) return;
      if (gem.ShouldDestroy) return;
      if (gem.WasClicked && !harvester.ForceInstantCollection) return;

      ++gemCountThisFrame;

      if (gemCountThisFrame <= 1)
      {
        AudioManager.Instance.PlaySound(AudioManager.Instance.GemPickupSoundEffect, RandomHelper.Float(-0.2f, 0.2f), 0.0f);
      }

      var gemEntity = GetEntity(gem.Id);
      var harvesterEntity = GetEntity(harvester.Id);

      if (gemEntity == null || harvesterEntity == null)
        return;

      gem.SetPickedUp(gemEntity, harvesterEntity, () =>
      {
      });

      harvester.PickedUpGem(gem);

      ++UntitledGemGameGameScreen.Collected;
      // m_gems2.Remove(gem.Id);
      // spatialTest.Remove(gem);
    }

    private void UpdateHarvesters(int index, GameTime gameTime)
    {
      var activeEntity = _harvesters[index];
      // collectedGems[index] = [];
      var harvester = GetEntity(activeEntity)?.Get<Harvester>();
      var transform = GetEntity(activeEntity)?.Get<Transform2>();
      harvester.PositionMoved = false;

      if (harvester == null || transform == null) return;

      UpdateHarvesterPosition(gameTime, harvester, transform);

      var collectionRange = harvester.CurrentState == Harvester.HarvesterState.None ?
        UpgradeManager.UG.HomebaseCollectionRange : UpgradeManager.UG.HarvesterCollectionRange;
      // var collectionRange = UpgradeManager.UG.HarvesterCollectionRange;

      // if (UpgradeManager.UG.HomebaseMagnetizer > 0 || HomeBase.BonusMagnetPower > 0)

      int[] buffer = _threadLocalBuffer.Value;
      // var q = spatialTest.Query(transform.Position, collectionRange * 0.5f);
      flatSpatialHash.QueryNearbyIndices(transform.Position.X, transform.Position.Y, buffer, out int resultCount);


      float rangeSquared = collectionRange * collectionRange;
      var harvesterCenter = harvester.BoundingCircle.Center;
      // Span<int> candidates = stackalloc int[128];
      // var q2 = grid.GetCandidates(transform.Position, collectionRange * 2.0f, candidates);

      if (harvester.CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity)
      {
        // if (UntitledGemGameGameScreen.HomeBasePos != Vector2.Zero && Vector2.Distance(transform.Position, UntitledGemGameGameScreen.HomeBasePos) < 15)
        // {
        //   harvester.ReachedHome = true;
        // }
        var homePos = UntitledGemGameGameScreen.HomeBasePos;
        // Squared distance check: 15 * 15 = 225
        if (homePos != Vector2.Zero && Vector2.DistanceSquared(transform.Position, homePos) < 225f)
        {
          harvester.ReachedHome = true;
        }
      }
      else
      {
        //https://www.monogameextended.net/docs/features/collision/
        // Add layer so harvester <-> harvester doesnt need to be checked?
        // foreach (var qq in q)
        // {
        //   if (harvester.CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity)
        //     break;
        //
        //   if (qq is Gem { PickedUp: false } gem)
        //   {
        //     // if (Vector2.Distance(harvester.Shape.BoundingBox.Center, gem.Shape.BoundingBox.Center) < collectionRange)
        //     // {
        //     //   collectedGems[index].Add(gem);
        //     // }
        //     if (Vector2.DistanceSquared(harvesterCenter, gem.BoundingCircle.Center) < rangeSquared)
        //     {
        //       collectedGems[index].Add(gem);
        //     }
        //   }
        // }

        for (int i = 0; i < resultCount; ++i)
        {
          // var r = flatSpatialHash.Gems[buffer[i]];
          int gemIndex = buffer[i];
          var r = flatSpatialHash.Gems[gemIndex];
          if (!r.IsActive) continue;
          var e = UpdateSystem2.Instance.GetEntityP(r.EntityId);
          if (e == null) continue;
          var gem = e.Get<Gem>();
          if (gem == null) continue;

          if (Vector2.DistanceSquared(harvesterCenter, e.Get<Transform2>().Position) < rangeSquared)
          {
            // collectedGems[index].Add(gem);
            if (Interlocked.CompareExchange(ref flatSpatialHash.Gems[gemIndex].ClaimState, 1, 0) == 0)
            {
              harvester.ClaimedGems.Add(r.EntityId);
            }
            if (harvester.ForceInstantCollection && harvester.CurrentState == Harvester.HarvesterState.None)
            {
              harvester.ClaimedGems.Add(r.EntityId);
            }
          }


          // Read directly from the flat array using the index
          // float distX = gemGrid.Gems[gemIndex].X - ship.X;
          // float distY = gemGrid.Gems[gemIndex].Y - ship.Y;
          //
          // if ((distX * distX) + (distY * distY) <= ship.PickupRadiusSquared)
          // {
          //     // Thread-safe claim directly inside the main array using ref
          //     if (Interlocked.CompareExchange(ref gemGrid.Gems[gemIndex].ClaimState, 1, 0) == 0)
          //     {
          //         ship.ClaimedGemIndices.Add(gemIndex);
          //     }
          // }

        }
      }
    }

    private bool IsGem(ICollisionActorJ actor)
    {
      return actor as Gem != null;
    }

    private int gemCountThisFrame = 0;

    private ThreadLocal<int[]> _threadLocalBuffer =
        new ThreadLocal<int[]>(() => new int[100000]);

    public override void Update(GameTime gameTime)
    {
      gemCountThisFrame = 0;
      var refuel = KeyboardExtended.GetState().WasKeyPressed(Keys.R);

      var destroyHarvester = new List<Entity>();

      flatSpatialHash.RebuildGrid();
      MagnetizerCache.Refresh();

      //Can we also cache all the gems transform and gem components, worth? entity.Get<Gem> it made a few times and takes time

      if (GameMain.MultiThreadingEnabled)
      {
        var p = Parallel.For(0, _harvesters.Count, (index) =>
        {
          UpdateHarvesters(index, gameTime);
        });

        while (!p.IsCompleted) { }
      }
      else
      {
        for (var i = 0; i < _harvesters.Count; i++)
        {
          UpdateHarvesters(i, gameTime);
        }
      }

      for (var i = 0; i < _harvesters.Count; i++)
      {
        var activeEntity = _harvesters[i];
        var harvester = GetEntity(activeEntity).Get<Harvester>();

        if (harvester.MarkedForDestroy)
          destroyHarvester.Add(harvester.Entity);

        if (harvester.ForceInstantCollection)
        {
          foreach (var gem in harvester.ClaimedGems)
          {
            CollectGem(GetEntity(gem).Get<Gem>(), harvester);
          }
          harvester.ClaimedGems.Clear();

          // Instant delivery for drone harvester
          UntitledGemGameGameScreen.DeliveredUncounted += harvester.CarryingGemBaseValue;
          harvester.CarryingGemCount = 0;
          harvester.CarryingGemBaseValue = 0;

          continue;
        }

        if (harvester.ReachedHome)
        {
          UntitledGemGameGameScreen.DeliveredUncounted += harvester.CarryingGemBaseValue;
          harvester.CarryingGemCount = 0;
          harvester.CarryingGemBaseValue = 0;
          harvester.ReachedHome = false;
          harvester.TargetScreenPosition = null;

          if (UpgradeManager.UG.RefuelHomebase)
          {
            harvester.IncreaseFuelPartial();
          }
        }
        else
        {
          foreach (var gem in harvester.ClaimedGems)
          {
            CollectGem(GetEntity(gem).Get<Gem>(), harvester);
          }
          harvester.ClaimedGems.Clear();
        }

        if (harvester.CurrentState == Harvester.HarvesterState.OutOfFuel)
        {
          var vec = m_camera.WorldToScreen(new System.Numerics.Vector2(harvester.BoundingCircle.Center.X, harvester.BoundingCircle.Center.Y));

          // var camera = SystemManagers.Default.Renderer.Camera;
          // camera.ScreenToWorld(vec.X, vec.Y, out float worldX, out float worldY);
          harvester.ReuqestRefuel(new Vector2(vec.X, vec.Y));
        }

        if ((refuel || UpgradeManager.UG.AutoRefuel) && harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
        {
          harvester.Refuel();
        }

        harvester.Update(gameTime);
      }

      foreach (var h in destroyHarvester)
      {
        h.Destroy();
        EntityFactory.Instance.Drones.Remove(h.Id);
      }

      if (UpgradeManager.UG.GemMerger)
      {
        ProcessGemMergers(flatSpatialHash, 20.0f, 5);
      }


      // foreach(var h in)

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

    public void ProcessGemMergers(FlatSpatialHash grid, float mergeRadius, int minThreshold = 4)
    {
      //TODO: can we optimized this? maybe we dont have to do all gems every frame, spread out multiple frames maybe?
      int[] denseBuckets = new int[grid._tableSize];
      grid.GetDenseBuckets(minThreshold, denseBuckets, out int bucketCount);

      // Increased buffer size to swallow massive clumps (like in your screenshot)
      int[] clumpBuffer = new int[512];
      float sqrRadius = mergeRadius * mergeRadius;

      for (int i = 0; i < bucketCount; i++)
      {
        int bucketIndex = denseBuckets[i];
        int centerGemIndex = grid._bucketHeads[bucketIndex];

        while (centerGemIndex != -1)
        {
          ref GemData centerGem = ref grid.Gems[centerGemIndex];

          if (centerGem.IsActive && centerGem.ClaimState == 0)
          {
            int clumpCount = 0;
            clumpBuffer[clumpCount++] = centerGemIndex;

            // FIX: Start from the very head of the bucket to ensure we don't miss any touching gems
            int neighborIndex = grid._bucketHeads[bucketIndex];

            while (neighborIndex != -1 && clumpCount < clumpBuffer.Length)
            {
              // Skip checking the center gem against itself
              if (neighborIndex != centerGemIndex)
              {
                ref GemData neighborGem = ref grid.Gems[neighborIndex];

                if (neighborGem.IsActive && neighborGem.ClaimState == 0)
                {
                  float dx = centerGem.X - neighborGem.X;
                  float dy = centerGem.Y - neighborGem.Y;

                  // Only collect gems genuinely inside the radius
                  if ((dx * dx + dy * dy) <= sqrRadius)
                  {
                    clumpBuffer[clumpCount++] = neighborIndex;
                  }
                }
              }
              neighborIndex = grid._nextIndices[neighborIndex];
            }

            // If we found enough gems to form a clump, merge ALL of them
            if (clumpCount >= minThreshold)
            {
              ExecuteMerge(grid, clumpBuffer, clumpCount);
              // We don't break here! We let the while loop continue to find other separate clumps in this same cell.
            }
          }

          // Move to the next gem in the bucket
          // (This is perfectly safe even if centerGemIndex was just recycled by ExecuteMerge)
          centerGemIndex = grid._nextIndices[centerGemIndex];
        }
      }
    }

    // Extracted the merge logic to keep it clean
    private void ExecuteMerge(FlatSpatialHash grid, int[] clumpBuffer, int count)
    {
      uint totalBaseValue = 0;

      float centerX = grid.Gems[clumpBuffer[0]].X;
      float centerY = grid.Gems[clumpBuffer[0]].Y;
      var centerPos = new Vector2(centerX, centerY);

      for (int j = 0; j < count; j++)
      {
        int indexToMerge = clumpBuffer[j];
        ref GemData gem = ref grid.Gems[indexToMerge];

        totalBaseValue += gem.BaseValue + (uint)UpgradeManager.UG.GemMergerBonus;

        var visualGem = GetEntity(gem.EntityId).Get<Gem>();

        // TODO: Look up the visual/game object using gem.EntityId to play the animation
        // var visualGem = GetVisualGemById(gem.EntityId);
        visualGem.MergeGem(centerPos);
        // visualGem.ShouldDestroy = true;

        gem.IsActive = false;
        gem.ClaimState = 1;
        // grid.RecycleIndex(indexToMerge);
      }

      uint finalValue = (uint)(totalBaseValue * UpgradeManager.UG.GemMergerBonusMultiplier);
      EntityFactory.Instance.CreateGem(centerPos, GemTypes.LightGreen, finalValue);
    }
  }
}
