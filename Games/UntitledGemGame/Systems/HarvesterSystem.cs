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

    // Treasure Scanner candidates are built once and shared by every Advanced
    // Harvester. Reservations prevent a fleet from converging on one rare gem.
    private readonly int[] _treasureScannerCandidates = new int[BaseStats.TreasureScannerCandidateCount];
    private readonly int[] _treasureScannerTargetOwners;
    private int _treasureScannerCandidateCount;
    private float _treasureScannerRefreshRemaining;

    private int _resonanceCascadeCharge;
    private float _resonanceCascadeTimeRemaining;
    public static float ResonanceSpeedMultiplier { get; private set; } = 1.0f;
    public static float ResonanceRangeMultiplier { get; private set; } = 1.0f;
    public static bool ResonanceCascadeActive => ResonanceSpeedMultiplier > 1.0f;

    private float _quantumEntanglementRefreshRemaining;
    private bool _quantumEntanglementWasActive;

    public static HarvesterCollectionSystem Instance;


    public HarvesterCollectionSystem(OrthographicCamera camera, ShapeBatch shapeBatch)
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite), typeof(Harvester)))
    // : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
      m_camera = camera;
      m_shapeBatch = shapeBatch;
      _treasureScannerTargetOwners = new int[flatSpatialHash.MaxCapacity];
      ResonanceSpeedMultiplier = 1.0f;
      ResonanceRangeMultiplier = 1.0f;
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

    public void ClearCargoForPrestige()
    {
      foreach (var id in _harvesters)
        _harvesterMapper.Get(id)?.ClearCargoForPrestige();
    }

    public ulong GetCarriedGemValue()
    {
      ulong value = 0;
      foreach (var id in _harvesters)
      {
        var harvester = _harvesterMapper.Get(id);
        if (harvester != null)
          value = PrestigeProgression.AddSaturating(value,
            BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue));
      }
      return value;
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
        ReleaseTreasureScannerTarget(harvester);
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
      ReleaseTreasureScannerTarget(harvester);

      // var width = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth;
      // var height = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight;
      // var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      // var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      var position = RandomHelper.Vector2(p0, p1);

      switch (harvester.CollectionStrategy)
      {
        case HarvesterStrategy.RandomScreenPosition:
          break;
        case HarvesterStrategy.RandomGemPosition:
          var gp = GetRandomGemPosition(harvester);
          if (gp != null)
            position = gp.Value;
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

      // switch (Upgrades.HarvesterCollectionStrategy)
      // TODO: based on harvester type instead?
      // switch ((HarvesterStrategy)UpgradeManager.Instance.UG.HarvesterCollectionStrategy)
      // {
      //   case HarvesterStrategy.RandomGemPosition:
      //     var gp = GetRandomGemPosition();
      //     if (gp != null)
      //       position = gp.Value;
      //     break;
      //   case HarvesterStrategy.TargetCluster:
      //     var p = GetBiggestCluserPosition(harvester);
      //     if (p != null)
      //       position = p.Value;
      //     break;
      //   case HarvesterStrategy.TargetClosestCluster:
      //     var p2 = GetBiggestCluserPositionWithDistance(harvester);
      //     if (p2 != null)
      //       position = p2.Value;
      //     break;
      // }

      return position;
    }

    private Random m_random = new Random();

    private Vector2? GetRandomGemPosition(Harvester harvester)
    {
      bool useTreasureScanner = harvester.Type == Harvester.HarvesterType.AdvancedHarvester
        && UpgradeManager.Instance.UG.TreasureScanner;

      if (useTreasureScanner)
      {
        var scannedTarget = GetHighestValueGemPosition(harvester);
        if (scannedTarget.HasValue)
          return scannedTarget;
      }

      // A cache can temporarily run out when every candidate is reserved.
      // Fall back to random targets, while retaining exclusive reservations.
      int attempts = useTreasureScanner ? 8 : 1;
      for (int attempt = 0; attempt < attempts; ++attempt)
      {
        var idx = flatSpatialHash.GetRandomActiveGemIndex(Random.Shared);

        if (idx < 0 || idx >= flatSpatialHash.Gems.Length)
          return null;

        if (useTreasureScanner && !TryReserveTreasureScannerTarget(harvester, idx))
          continue;

        ref GemData gem = ref flatSpatialHash.Gems[idx];
        harvester.TargetGemGridIndex = idx;
        harvester.TargetGemEntityId = gem.EntityId;
        return new Vector2(gem.X, gem.Y);
      }

      return null;
    }

    private Vector2? GetHighestValueGemPosition(Harvester harvester)
    {
      int candidateCount = _treasureScannerCandidateCount;
      if (candidateCount == 0)
        return null;

      int start = (harvester.Id & int.MaxValue) % candidateCount;
      for (int offset = 0; offset < candidateCount; ++offset)
      {
        int gemIndex = _treasureScannerCandidates[(start + offset) % candidateCount];
        ref GemData candidate = ref flatSpatialHash.Gems[gemIndex];
        if (!candidate.IsActive
          || candidate.ClaimState != 0
          || !TryReserveTreasureScannerTarget(harvester, gemIndex))
          continue;

        harvester.TargetGemGridIndex = gemIndex;
        harvester.TargetGemEntityId = candidate.EntityId;
        return new Vector2(candidate.X, candidate.Y);
      }

      return null;
    }

    private bool TryReserveTreasureScannerTarget(Harvester harvester, int gemIndex)
    {
      int ownerToken = harvester.Id + 1;
      return Interlocked.CompareExchange(ref _treasureScannerTargetOwners[gemIndex], ownerToken, 0) == 0;
    }

    private void ReleaseTreasureScannerTarget(Harvester harvester)
    {
      int gemIndex = harvester.TargetGemGridIndex;
      if (gemIndex >= 0 && gemIndex < _treasureScannerTargetOwners.Length)
      {
        int ownerToken = harvester.Id + 1;
        Interlocked.CompareExchange(ref _treasureScannerTargetOwners[gemIndex], 0, ownerToken);
      }

      harvester.TargetGemGridIndex = -1;
      harvester.TargetGemEntityId = -1;
    }

    private void RefreshTreasureScannerCache(GameTime gameTime)
    {
      if (!UpgradeManager.Instance.UG.TreasureScanner)
      {
        _treasureScannerCandidateCount = 0;
        return;
      }

      _treasureScannerRefreshRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
      if (_treasureScannerRefreshRemaining > 0f)
        return;

      _treasureScannerRefreshRemaining = BaseStats.TreasureScannerRefreshSeconds;
      int count = 0;

      // Maintain a fixed-size min-heap containing the most valuable active
      // gems. This is one bounded-memory pass, independent of harvester count.
      for (int gemIndex = 0; gemIndex < flatSpatialHash.Gems.Length; ++gemIndex)
      {
        ref GemData gem = ref flatSpatialHash.Gems[gemIndex];
        if (!gem.IsActive || gem.ClaimState != 0)
          continue;

        if (count < _treasureScannerCandidates.Length)
        {
          _treasureScannerCandidates[count] = gemIndex;
          SiftTreasureCandidateUp(count);
          ++count;
        }
        else if (gem.BaseValue > flatSpatialHash.Gems[_treasureScannerCandidates[0]].BaseValue)
        {
          _treasureScannerCandidates[0] = gemIndex;
          SiftTreasureCandidateDown(0, count);
        }
      }

      _treasureScannerCandidateCount = count;
    }

    private void SiftTreasureCandidateUp(int index)
    {
      while (index > 0)
      {
        int parent = (index - 1) / 2;
        if (GetTreasureCandidateValue(parent) <= GetTreasureCandidateValue(index))
          return;

        (_treasureScannerCandidates[parent], _treasureScannerCandidates[index]) =
          (_treasureScannerCandidates[index], _treasureScannerCandidates[parent]);
        index = parent;
      }
    }

    private void SiftTreasureCandidateDown(int index, int count)
    {
      while (true)
      {
        int left = index * 2 + 1;
        if (left >= count)
          return;

        int right = left + 1;
        int smallest = right < count && GetTreasureCandidateValue(right) < GetTreasureCandidateValue(left)
          ? right
          : left;

        if (GetTreasureCandidateValue(index) <= GetTreasureCandidateValue(smallest))
          return;

        (_treasureScannerCandidates[index], _treasureScannerCandidates[smallest]) =
          (_treasureScannerCandidates[smallest], _treasureScannerCandidates[index]);
        index = smallest;
      }
    }

    private uint GetTreasureCandidateValue(int candidateIndex)
    {
      return flatSpatialHash.Gems[_treasureScannerCandidates[candidateIndex]].BaseValue;
    }

    private void UpdateMetaFleetEffects(GameTime gameTime)
    {
      if (!UpgradeManager.Instance.UGM.ResonanceCascade)
      {
        _resonanceCascadeCharge = 0;
        _resonanceCascadeTimeRemaining = 0f;
        ResonanceSpeedMultiplier = 1.0f;
        ResonanceRangeMultiplier = 1.0f;
        return;
      }

      if (_resonanceCascadeTimeRemaining <= 0f)
        return;

      _resonanceCascadeTimeRemaining = Math.Max(0f,
        _resonanceCascadeTimeRemaining - (float)gameTime.ElapsedGameTime.TotalSeconds);

      if (_resonanceCascadeTimeRemaining <= 0f)
      {
        ResonanceSpeedMultiplier = 1.0f;
        ResonanceRangeMultiplier = 1.0f;
      }
    }

    private void ChargeResonanceCascade(Harvester harvester)
    {
      if (!UpgradeManager.Instance.UGM.ResonanceCascade
        || !BaseStats.IsFleetHarvester(harvester)
        || _resonanceCascadeTimeRemaining > 0f)
      {
        return;
      }

      ++_resonanceCascadeCharge;
      if (_resonanceCascadeCharge < BaseStats.ResonanceCascadeCollectionsRequired)
        return;

      _resonanceCascadeCharge = 0;
      _resonanceCascadeTimeRemaining = BaseStats.ResonanceCascadeDurationSeconds;
      ResonanceSpeedMultiplier = BaseStats.ResonanceCascadeSpeedMultiplier;
      ResonanceRangeMultiplier = BaseStats.ResonanceCascadeRangeMultiplier;
      UntitledGemGameGameScreen.Instance?.ShowResonanceCascade();
    }

    private void RefreshQuantumEntanglement(GameTime gameTime)
    {
      if (!UpgradeManager.Instance.UGM.QuantumEntanglement)
      {
        if (_quantumEntanglementWasActive)
          ClearQuantumEntanglementLinks();

        _quantumEntanglementWasActive = false;
        return;
      }

      _quantumEntanglementWasActive = true;
      _quantumEntanglementRefreshRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
      if (_quantumEntanglementRefreshRemaining > 0f)
        return;

      _quantumEntanglementRefreshRemaining = BaseStats.QuantumEntanglementRefreshSeconds;
      Harvester pendingPartner = null;

      for (int i = 0; i < _harvesters.Count; ++i)
      {
        var harvester = GetEntity(_harvesters[i])?.Get<Harvester>();
        if (harvester == null || harvester.MarkedForDestroy || !BaseStats.IsFleetHarvester(harvester))
          continue;

        harvester.EntangledPartnerEntityId = -1;
        if (pendingPartner == null)
        {
          pendingPartner = harvester;
          continue;
        }

        pendingPartner.EntangledPartnerEntityId = harvester.Id;
        harvester.EntangledPartnerEntityId = pendingPartner.Id;
        pendingPartner = null;
      }
    }

    private void ClearQuantumEntanglementLinks()
    {
      for (int i = 0; i < _harvesters.Count; ++i)
      {
        var harvester = GetEntity(_harvesters[i])?.Get<Harvester>();
        if (harvester != null)
          harvester.EntangledPartnerEntityId = -1;
      }
    }

    private void ShareQuantumEntanglementValue(Harvester harvester, uint gemValue)
    {
      if (!UpgradeManager.Instance.UGM.QuantumEntanglement
        || !BaseStats.IsFleetHarvester(harvester)
        || harvester.EntangledPartnerEntityId < 0)
      {
        return;
      }

      var partner = GetEntity(harvester.EntangledPartnerEntityId)?.Get<Harvester>();
      if (partner == null
        || partner.MarkedForDestroy
        || partner.EntangledPartnerEntityId != harvester.Id)
      {
        return;
      }

      partner.EntangledValueAccumulator += gemValue * BaseStats.QuantumEntanglementValueShare;
      uint wholeValue = (uint)Math.Min(uint.MaxValue, Math.Floor(partner.EntangledValueAccumulator));
      if (wholeValue == 0)
        return;

      ulong combinedValue = (ulong)partner.CarryingGemBaseValue + wholeValue;
      partner.CarryingGemBaseValue = (uint)Math.Min(uint.MaxValue, combinedValue);
      partner.EntangledValueAccumulator -= wholeValue;
      // Only the collecting harvester owns the visual pulse. This preserves
      // the direction of the value transfer instead of drawing a permanent
      // bidirectional link between every pair.
      if (harvester.EntanglementPulseCooldownRemaining <= 0f
        && partner.EntanglementPulseCooldownRemaining <= 0f)
      {
        harvester.EntanglementPulseTimeRemaining = BaseStats.QuantumEntanglementPulseSeconds;
        harvester.EntanglementPulseCooldownRemaining = BaseStats.QuantumEntanglementPulseCooldownSeconds;
        partner.EntanglementPulseCooldownRemaining = BaseStats.QuantumEntanglementPulseCooldownSeconds;
      }
    }

    private bool IsCurrentGemTargetAvailable(Harvester harvester)
    {
      // A random screen position is used as a fallback when no gem is
      // available. Let the harvester finish that trip before trying again.
      if (harvester.TargetGemGridIndex < 0)
        return true;

      if (harvester.TargetGemGridIndex >= flatSpatialHash.Gems.Length)
        return false;

      ref GemData gem = ref flatSpatialHash.Gems[harvester.TargetGemGridIndex];
      return gem.IsActive
        && gem.ClaimState == 0
        && gem.EntityId == harvester.TargetGemEntityId;
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
      // If we are looking for a new target, release our claim on the old one first
      // if (harvester._currentTargetBucket != -1)
      {
        flatSpatialHash.ReleaseBucket(harvester._currentTargetBucket);
        harvester._currentTargetBucket = -1;
      }

      // Pass in the new out parameter
      if (flatSpatialHash.TryGetBestScoringClusterPosition(harvester.BoundingCircle.Center, out Vector2 target, out int selectedBucket, minGems: 4, minSearchRadius: 30.0f))
      {
        // CLAIM THE NEW BUCKET
        harvester._currentTargetBucket = selectedBucket;
        flatSpatialHash.ReserveBucket(harvester._currentTargetBucket);

        // Add that slight jitter we talked about so they don't stack on the exact same pixel
        float offsetX = (float)(m_random.NextDouble() * 70.0 - 35.0);
        float offsetY = (float)(m_random.NextDouble() * 70.0 - 35.0);

        return target + new Vector2(offsetX, offsetY);
      }

      return null;
    }
    // private Vector2? GetBiggestCluserPositionWithDistance(Harvester harvester)
    // {
    //   if (flatSpatialHash.TryGetBestScoringClusterPosition(harvester.BoundingCircle.Center, out Vector2 target, minGems: 4, minSearchRadius: 30.0f))
    //   {
    //     // Add that slight jitter we talked about so they don't stack on the exact same pixel
    //     float offsetX = (float)(m_random.NextDouble() * 70.0 - 35.0);
    //     float offsetY = (float)(m_random.NextDouble() * 70.0 - 35.0);
    //
    //     return target + new Vector2(offsetX, offsetY);
    //   }
    //
    //   return null;
    // }

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
      harvester.LaunchThrusterTimeRemaining = Math.Max(0f, harvester.LaunchThrusterTimeRemaining - dt);
      harvester.WarpDriveCooldownRemaining = Math.Max(0f, harvester.WarpDriveCooldownRemaining - dt);
      harvester.EntanglementPulseTimeRemaining = Math.Max(0f, harvester.EntanglementPulseTimeRemaining - dt);
      harvester.EntanglementPulseCooldownRemaining = Math.Max(0f, harvester.EntanglementPulseCooldownRemaining - dt);

      //TODO: fix for advanced harvester
      var speed = BaseStats.GetHarvesterSpeed(harvester);
      float targetArrivalRadius = Math.Clamp(speed * 0.01f, 1f, 20f);
      float targetArrivalRadiusSquared = targetArrivalRadius * targetArrivalRadius;

      if (harvester.DepartingHomeBase)
      {
        Vector2 homePosition = UntitledGemGameGameScreen.HomeBasePos;
        float departureRadius = BaseStats.HomeBaseDepartureRadius;
        // The departure target lies on the radius. Floating-point rounding can
        // place it just inside, so reaching the target must also end departure.
        bool reachedDepartureTarget = harvester.TargetScreenPosition.HasValue
          && Vector2.DistanceSquared(transform.Position, harvester.TargetScreenPosition.Value)
            < targetArrivalRadiusSquared;
        if (Vector2.DistanceSquared(transform.Position, homePosition) >= departureRadius * departureRadius
          || reachedDepartureTarget)
        {
          harvester.DepartingHomeBase = false;
          harvester.TargetScreenPosition = null;
        }
        else if (harvester.TargetScreenPosition.HasValue)
        {
          UpdateMovement(harvester.TargetScreenPosition.Value, gameTime, transform, harvester);
          return;
        }
      }

      if (harvester.ReturningToHomebase)
      {
        Vector2 homePosition = UntitledGemGameGameScreen.HomeBasePos;
        if (TryDockAtHomeBase(harvester, transform, homePosition))
          return;

        UpdateMovement(homePosition, gameTime, transform, harvester);
      }
      else if (!harvester.TargetScreenPosition.HasValue
        || (harvester.CollectionStrategy == HarvesterStrategy.RandomGemPosition && !IsCurrentGemTargetAvailable(harvester))
        || Vector2.DistanceSquared(transform.Position, harvester.TargetScreenPosition.Value)
          < targetArrivalRadiusSquared)
      {
        harvester.TargetScreenPosition = GetNewTargetPosition(harvester);
        TryActivateWarpDrive(harvester, transform);
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        UpdateMovement(harvester.TargetScreenPosition.Value, gameTime, transform, harvester);
      }
    }

    private static bool TryDockAtHomeBase(Harvester harvester, Transform2 transform, Vector2 homePosition)
    {
      float dockingRadius = BaseStats.HomeBaseDockingRadius;
      if (Vector2.DistanceSquared(transform.Position, homePosition) > dockingRadius * dockingRadius)
        return false;

      // Entering the visible base hull is enough to complete delivery. Keep
      // the current position so the ship departs smoothly instead of visibly
      // snapping to the exact center of the base.
      harvester.ReachedHome = true;
      return true;
    }

    private void TryActivateWarpDrive(Harvester harvester, Transform2 transform)
    {
      if (harvester.Type != Harvester.HarvesterType.UltimateHarvester
        || !UpgradeManager.Instance.UG.WarpDrive
        || harvester.WarpDriveCooldownRemaining > 0f
        || !harvester.TargetScreenPosition.HasValue)
      {
        return;
      }

      var target = harvester.TargetScreenPosition.Value;
      if (Vector2.DistanceSquared(transform.Position, target)
        < BaseStats.WarpDriveMinimumDistance * BaseStats.WarpDriveMinimumDistance)
      {
        return;
      }

      transform.Position = target;
      harvester.SetCollisionPosition(target);
      harvester.WarpDriveCooldownRemaining = BaseStats.WarpDriveCooldownSeconds;
    }

    private float LerpAngle(float currentAngle, float targetAngle, float amount)
    {
      amount = Math.Clamp(amount, 0f, 1f);
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
      var uga = UpgradeManager.Instance.UGA;
      var isDrone = harvester.Type == Harvester.HarvesterType.Drone;

      var speed = BaseStats.GetHarvesterSpeed(harvester);
      var moveLen = dt * speed * HomeBase.BonusMoveSpeed;
      var movement = dir * moveLen;

      // Calculate movement scalar rather than doing vector.Length() multiple times
      // var moveLen = dt * speed * HomeBase.BonusMoveSpeed;
      // var movement = dir * moveLen;

      if (moveLen > 0.05f)
        harvester.PositionMoved = true;

      // var fuelCost = isDrone ? 0f : moveLen * (2.0f - ug.FuelEfficiency);
      var fuelCost = isDrone ? 0f : (moveLen * 1.5f) / BaseStats.GetHarvesterFuelEfficiency(harvester);

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

      harvester.SetCollisionPosition(transform.Position);

      if (isDrone && harvester.TimeAlive > uga.IncreaseDroneFuel)
      {
        harvester.MarkedForDestroy = true;
        if (harvester._currentTargetBucket != -1)
        {
          flatSpatialHash.ReleaseBucket(harvester._currentTargetBucket);
          harvester._currentTargetBucket = -1;
        }
      }

      if (uga.CanDeployDrones(harvester.Type) && harvester.MovedDistance > uga.HarvesterDronesTravelDistance)
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
    //   var speed = harvester.IsDrone ? UpgradeManager.Instance.UG.DroneSpeed : UpgradeManager.Instance.UG.HarvesterSpeed;
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
    //   var fuelCost = movement.Length() * (2.0f - UpgradeManager.Instance.UG.FuelEfficiency);
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
    //   // Console.WriteLine($"Harvester moving. Dist: {dist}, dist2: {dist2}, moveLen: {moveLen} - {dt * UpgradeManager.Instance.UG.HarvesterSpeed} - {dist3}");
    //   if (dist3 > dist)
    //   {
    //     transform.Position = target;
    //     var box = BoundingBox2D.CreateFromPositionAndSize(transform.Position, Vector2.One);
    //     harvester.Shape = new CollisionShape2D(box);
    //     harvester.Fuel -= fuelCost;
    //     // Console.WriteLine("Harvester reached target position.");
    //     // movement = target - transform.Position;
    //     // fuelCost = movement.Length() * (2.0f - UpgradeManager.Instance.UG.FuelEfficiency);
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
    //     // harvester.m_sprite.Alpha = harvester.Fuel / UpgradeManager.Instance.UG.HarvesterMaxFuel;
    //   }
    //   else if (harvester.CurrentState == Harvester.HarvesterState.Collecting)
    //   {
    //     harvester.CurrentState = Harvester.HarvesterState.OutOfFuel;
    //   }
    //
    //   // if (harvester.MovedDistance > 105 && harvester.IsDrone)
    //   if (harvester.TimeAlive > UpgradeManager.Instance.UG.IncreaseDroneFuel && harvester.IsDrone)
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
    //   if (harvester.MovedDistance > UpgradeManager.Instance.UG.HarvesterDronesTravelDistance && !harvester.IsDrone && UpgradeManager.Instance.UG.HarvesterDrones > 0 && isDroneActive)
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
      CollectGem(gem, harvester, allowChainCollection: true);
    }

    private void CollectGem(Gem gem, Harvester harvester, bool allowChainCollection)
    {
      if (gem.PickedUp) return;
      if (gem.ShouldDestroy) return;
      if (gem.WasClicked && !harvester.ForceInstantCollection) return;

      ++gemCountThisFrame;

      if (gemCountThisFrame <= 1)
      {
        AudioManager.Instance.PlaySound(AudioManager.Instance.GemPickupSoundEffect, AudioManager.SoundType.GemCollect, RandomHelper.Float(-0.2f, 0.2f), 0.0f, 0.5f);
      }

      var gemEntity = GetEntity(gem.Id);
      var harvesterEntity = GetEntity(harvester.Id);

      if (gemEntity == null || harvesterEntity == null)
        return;

      var pickupPosition = gem.BoundingCircle.Center;
      if (harvester.TargetGemGridIndex == gem.GridIndex
        && harvester.TargetGemEntityId == gem.Id)
      {
        ReleaseTreasureScannerTarget(harvester);
      }

      gem.SetPickedUp(gemEntity, harvesterEntity, () =>
      {
      });

      bool quantumDelivered = harvester.Type == Harvester.HarvesterType.AdvancedHarvester
        && UpgradeManager.Instance.UG.QuantumCargoHold
        && Random.Shared.NextSingle() < BaseStats.QuantumCargoDeliveryChance;

      if (quantumDelivered)
        UntitledGemGameGameScreen.DeliveredUncounted += BaseStats.GetHarvesterDeliveryValue(harvester, gem.BaseValue);
      else
        harvester.PickedUpGem(gem);

      ++UntitledGemGameGameScreen.Collected;
      ChargeResonanceCascade(harvester);
      ShareQuantumEntanglementValue(harvester, gem.BaseValue);

      if (allowChainCollection
        && harvester.Type == Harvester.HarvesterType.ExpertHarvester
        && UpgradeManager.Instance.UG.ChainCollection)
      {
        CollectChainedGems(pickupPosition, harvester);
      }
      // m_gems2.Remove(gem.Id);
      // spatialTest.Remove(gem);
    }

    private void CollectChainedGems(Vector2 origin, Harvester harvester)
    {
      int[] buffer = _threadLocalBuffer.Value;
      flatSpatialHash.QueryNearbyIndices(origin.X, origin.Y, buffer, out int resultCount);
      float radiusSquared = BaseStats.ChainCollectionRadius * BaseStats.ChainCollectionRadius;
      int collected = 0;

      for (int i = 0; i < resultCount && collected < BaseStats.ChainCollectionBonusGems; ++i)
      {
        int gemIndex = buffer[i];
        ref GemData candidate = ref flatSpatialHash.Gems[gemIndex];
        if (!candidate.IsActive || candidate.ClaimState != 0)
          continue;

        var candidatePosition = new Vector2(candidate.X, candidate.Y);
        if (Vector2.DistanceSquared(origin, candidatePosition) > radiusSquared)
          continue;

        var entity = GetEntity(candidate.EntityId);
        var chainedGem = entity?.Get<Gem>();
        if (chainedGem == null
          || Interlocked.CompareExchange(ref candidate.ClaimState, 1, 0) != 0)
        {
          continue;
        }

        CollectGem(chainedGem, harvester, allowChainCollection: false);
        ++collected;
      }
    }

    private void DeliverCargo(Harvester harvester)
    {
      ReleaseTreasureScannerTarget(harvester);
      ulong deliveryValue = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
      deliveryValue = ApplyJackpotHaul(harvester, deliveryValue);
      ulong queuedValue = UntitledGemGameGameScreen.DeliveredUncounted;
      UntitledGemGameGameScreen.DeliveredUncounted = deliveryValue > ulong.MaxValue - queuedValue
        ? ulong.MaxValue
        : queuedValue + deliveryValue;
      harvester.CarryingGemCount = 0;
      harvester.CarryingGemBaseValue = 0;
      harvester.ReachedHome = false;
      harvester.DepartingHomeBase = true;
      harvester.TargetScreenPosition = GetHomeBaseDepartureTarget(harvester);
      harvester.ReturnGateCheckedForCurrentLoad = false;

      if (harvester.Type == Harvester.HarvesterType.Harvester
        && UpgradeManager.Instance.UG.LaunchThrusters)
      {
        harvester.LaunchThrusterTimeRemaining = BaseStats.LaunchThrusterDurationSeconds;
      }

      if (UpgradeManager.Instance.UGM.RefuelHomebase)
        harvester.IncreaseFuelPartial();
    }

    private static Vector2 GetHomeBaseDepartureTarget(Harvester harvester)
    {
      Vector2 homePosition = UntitledGemGameGameScreen.HomeBasePos;
      Vector2 direction = harvester.BoundingCircle.Center - homePosition;
      float directionLengthSquared = direction.LengthSquared();

      if (directionLengthSquared > 0.0001f)
      {
        direction /= MathF.Sqrt(directionLengthSquared);
      }
      else
      {
        // Deterministic fallback keeps simultaneous ships from stacking when
        // a Return Gate places them exactly at the center.
        float angle = (harvester.Id * 2.3999632f) % MathHelper.TwoPi;
        direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      }

      return homePosition + direction * BaseStats.HomeBaseDepartureRadius;
    }

    private ulong ApplyJackpotHaul(Harvester harvester, ulong deliveryValue)
    {
      if (!UpgradeManager.Instance.UGM.JackpotHaul
        || !BaseStats.IsFleetHarvester(harvester)
        || Random.Shared.NextSingle() >= BaseStats.JackpotHaulChance)
      {
        return deliveryValue;
      }

      ulong multiplier = Random.Shared.NextSingle() < BaseStats.JackpotHaulMegaChance
        ? BaseStats.JackpotHaulMegaMultiplier
        : BaseStats.JackpotHaulMultiplier;
      bool isMegaJackpot = multiplier == BaseStats.JackpotHaulMegaMultiplier;

      // The regular delivery spring turns the larger payout into a much more
      // pronounced home-base pulse without spawning extra effect entities.
      if (UntitledGemGameGameScreen.Instance != null)
        UntitledGemGameGameScreen.Instance.ScaleVelocity += isMegaJackpot ? 8f : 4f;

      ulong jackpotValue = deliveryValue > ulong.MaxValue / multiplier
        ? ulong.MaxValue
        : deliveryValue * multiplier;
      UntitledGemGameGameScreen.Instance?.ShowJackpotHaul(
        harvester.BoundingCircle.Center, jackpotValue, isMegaJackpot);
      return jackpotValue;
    }

    private bool TryActivateReturnGate(Harvester harvester, Transform2 transform)
    {
      if (harvester.Type != Harvester.HarvesterType.UltimateHarvester
        || !UpgradeManager.Instance.UG.ReturnGate
        || !harvester.ReturningToHomebase
        || harvester.ReturnGateCheckedForCurrentLoad)
      {
        return false;
      }

      harvester.ReturnGateCheckedForCurrentLoad = true;
      if (Random.Shared.NextSingle() >= BaseStats.ReturnGateChance)
        return false;

      var homePosition = UntitledGemGameGameScreen.HomeBasePos;
      if (homePosition == Vector2.Zero)
        return false;

      transform.Position = homePosition;
      harvester.SetCollisionPosition(homePosition);
      return true;
    }


    private void UpdateHarvesters(int index, GameTime gameTime)
    {
      var activeEntity = _harvesters[index];
      // collectedGems[index] = [];
      var harvester = GetEntity(activeEntity)?.Get<Harvester>();
      var transform = GetEntity(activeEntity)?.Get<Transform2>();
      harvester.PositionMoved = false;

      if (harvester == null || transform == null || harvester.MarkedForDestroy) return;

      UpdateHarvesterPosition(gameTime, harvester, transform);

      int[] buffer = _threadLocalBuffer.Value;
      // var q = spatialTest.Query(transform.Position, collectionRange * 0.5f);
      flatSpatialHash.QueryNearbyIndices(transform.Position.X, transform.Position.Y, buffer, out int resultCount);

      float rangeSquared = BaseStats.GetHarvesterCollectionRangeSquared(harvester);

      // float rangeSquared = collectionRange * collectionRange;
      var harvesterCenter = harvester.BoundingCircle.Center;
      // Span<int> candidates = stackalloc int[128];
      // var q2 = grid.GetCandidates(transform.Position, collectionRange * 2.0f, candidates);

      if (harvester.DepartingHomeBase)
      {
        // Do not let a large collection radius refill the ship while it is
        // still inside the dock trigger; that caused repeated instant docking.
      }
      else if (harvester.ReturningToHomebase)
      {
        var homePos = UntitledGemGameGameScreen.HomeBasePos;
        TryDockAtHomeBase(harvester, transform, homePos);
      }
      else
      {
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
          // else
          {
            gem.TargetMagnetIndex = harvester.Id;
            gem.TargetMagnetPos = harvester.BoundingCircle.Center;
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

    private int gemCountThisFrame = 0;

    private ThreadLocal<int[]> _threadLocalBuffer =
        new ThreadLocal<int[]>(() => new int[100000]);

    public override void Update(GameTime gameTime)
    {
      // Only gem cleanup should run while the player spends prestige currency.
      if (UntitledGemGameGameScreen.Instance?.m_postPrestige == true)
        return;

      gemCountThisFrame = 0;
      var refuel = KeyboardExtended.GetState().WasKeyPressed(Keys.R);

      var mouse = MouseExtended.GetState();
      var mouseWorldPos = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);


      var destroyHarvester = new List<Entity>();

      flatSpatialHash.RebuildGrid();
      RefreshTreasureScannerCache(gameTime);
      UpdateMetaFleetEffects(gameTime);
      RefreshQuantumEntanglement(gameTime);
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
          DeliverCargo(harvester);
        }
        else
        {
          foreach (var gem in harvester.ClaimedGems)
          {
            CollectGem(GetEntity(gem).Get<Gem>(), harvester);
          }
          harvester.ClaimedGems.Clear();

          var transform = GetEntity(activeEntity).Get<Transform2>();
          if (TryActivateReturnGate(harvester, transform))
            DeliverCargo(harvester);
        }

        if (harvester.CurrentState == Harvester.HarvesterState.OutOfFuel)
        {

          //var vec = m_camera.WorldToScreen(new System.Numerics.Vector2(harvester.BoundingCircle.Center.X, harvester.BoundingCircle.Center.Y));
          var vec = m_camera.WorldToScreen(new Vector2(harvester.BoundingCircle.Center.X, harvester.BoundingCircle.Center.Y));

          // var camera = SystemManagers.Default.Renderer.Camera;
          // camera.ScreenToWorld(vec.X, vec.Y, out float worldX, out float worldY);
          harvester.ReuqestRefuel(new Vector2(vec.X, vec.Y));
        }
        // else if (harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
        // {
        //   var vec = m_camera.WorldToScreen(new Vector2(harvester.BoundingCircle.Center.X, harvester.BoundingCircle.Center.Y));
        //   harvester.UpdateRefuelButtonPosition(vec);
        // }

        if ((refuel || UpgradeManager.Instance.UGM.AutoRefuel) && harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
        {
          harvester.Refuel();
        }

        harvester.Update(gameTime, mouseWorldPos, isMouseClicked);
      }

      foreach (var h in destroyHarvester)
      {
        h.Destroy();
        EntityFactory.Instance.Drones.Remove(h.Id);
      }

      if (UpgradeManager.Instance.UGM.GemMerger)
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

        totalBaseValue += gem.BaseValue + (uint)UpgradeManager.Instance.UGM.GemMergerBonus;

        var visualGem = GetEntity(gem.EntityId).Get<Gem>();

        // TODO: Look up the visual/game object using gem.EntityId to play the animation
        // var visualGem = GetVisualGemById(gem.EntityId);
        visualGem.MergeGem(centerPos);
        // visualGem.ShouldDestroy = true;

        gem.IsActive = false;
        gem.ClaimState = 1;
        // grid.RecycleIndex(indexToMerge);
      }

      uint finalValue = (uint)(totalBaseValue * UpgradeManager.Instance.UGM.GemMergerBonusMultiplier);
      // EntityFactory.Instance.CreateGem(centerPos, GemTypes.LightGreen, finalValue);
      EntityFactory.Instance.QueueGemSpawn(centerPos, GemTypes.LightGreen, finalValue);
    }
  }
}
