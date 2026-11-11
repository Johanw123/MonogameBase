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
  public partial class HarvesterCollectionSystem : EntityUpdateSystem
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
    public GemSpatialIndex flatSpatialHash = new GemSpatialIndex(500_000, 30);

    // Treasure Scanner candidates are built once and shared by every Advanced
    // Harvester. Reservations prevent a fleet from converging on one rare gem.
    private readonly int[] _treasureScannerCandidates = new int[BaseStats.TreasureScannerCandidateCount];
    private readonly int[] _treasureScannerTargetOwners;
    private int _treasureScannerCandidateCount;
    private float _treasureScannerRefreshRemaining;

    private Random moduleRandom = Random.Shared;

    private int _resonanceCascadeCharge;
    private float _resonanceCascadeTimeRemaining;
    public static float ResonanceSpeedMultiplier { get; private set; } = 1.0f;
    public static float ResonanceRangeMultiplier { get; private set; } = 1.0f;
    public static bool ResonanceCascadeActive => ResonanceSpeedMultiplier > 1.0f;

    private float _quantumEntanglementRefreshRemaining;
    private bool _quantumEntanglementWasActive;

    public static HarvesterCollectionSystem Instance;
    public Harvester HomeBaseHarvester;

    internal Harvester GetHomeBaseHarvester()
    {
      if (HomeBaseHarvester is { MarkedForDestroy: false })
        return HomeBaseHarvester;
      for (int i = 0; i < _harvesters.Count; ++i)
      {
        var harvester = _harvesterMapper?.Get(_harvesters[i]);
        if (harvester is { Type: Harvester.HarvesterType.HomeBase, MarkedForDestroy: false })
          return HomeBaseHarvester = harvester;
      }
      if (_harvesterMapper != null && _harvesterMapper.TryGet(0, out var first)
        && first is { Type: Harvester.HarvesterType.HomeBase, MarkedForDestroy: false })
        return HomeBaseHarvester = first;
      return null;
    }


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

    public void EmergencyRefuelFleet()
    {
      foreach (var id in _harvesters)
      {
        var harvester = _harvesterMapper.Get(id);
        if (harvester != null && BaseStats.IsFleetHarvester(harvester))
          harvester.EmergencyRefuel();
      }
    }

    // Every ship in flight (not the homebase), for the core fracture's shockwave.
    public void CollectFlyingShips(List<(Harvester Ship, Transform2 Transform)> ships)
    {
      ships.Clear();
      foreach (var id in _harvesters)
      {
        var harvester = _harvesterMapper.Get(id);
        if (harvester == null || harvester.MarkedForDestroy || harvester.Type == Harvester.HarvesterType.HomeBase
          || harvester.CurrentState == Harvester.HarvesterState.None) continue;
        var transform = harvester.Entity?.Get<Transform2>() ?? GetEntity(id)?.Get<Transform2>();
        if (transform != null) ships.Add((harvester, transform));
      }
    }

    protected override void OnEntityAdded(int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);

      if (harvester != null)
      {
        harvester.BeginModuleTrip();
        _harvesters.Add(entityId);
        if (harvester.Type == Harvester.HarvesterType.HomeBase)
          HomeBaseHarvester = harvester;
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
        if (HomeBaseHarvester == harvester)
          HomeBaseHarvester = null;
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

    private Vector2 GetNewTargetPosition(Harvester harvester, PlayAreaBounds bounds)
    {
      ReleaseTreasureScannerTarget(harvester);

      if (harvester.HasModule(ShipModule.ProspectorLens))
      {
        var prospect = FindProspectorTarget(harvester);
        if (prospect.HasValue) return bounds.Clamp(prospect.Value);
      }
      var position = RandomHelper.Vector2(bounds.Minimum, bounds.Maximum);

      switch (harvester.CollectionStrategy)
      {
        case HarvesterStrategy.RandomScreenPosition:
          // Drifters roam where knocked-loose gems are landing.
          if (UntitledGemGameGameScreen.PlanetMiningEnabled)
            position = UntitledGemGameGameScreen.SamplePlanetDebris(bounds,
              SignalStats.FirePower(MainShipWeapon.Cannon));
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

      return bounds.Clamp(position);
    }

    private Vector2? FindProspectorTarget(Harvester harvester)
    {
      var origin = harvester.BoundingCircle.Center;
      int best = -1;
      uint bestValue = 0;
      float bestDistance = float.MaxValue;
      foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, ModuleCatalog.ProspectorRadius))
      {
        ref var candidate = ref flatSpatialHash.Gems[index];
        if (!candidate.IsActive || candidate.ClaimState != 0
          || Volatile.Read(ref _treasureScannerTargetOwners[index]) != 0) continue;
        float distance = Vector2.DistanceSquared(origin, new Vector2(candidate.X, candidate.Y));
        if (candidate.BaseValue > bestValue || (candidate.BaseValue == bestValue && distance < bestDistance))
        {
          best = index;
          bestValue = candidate.BaseValue;
          bestDistance = distance;
        }
      }
      if (best < 0 || !TryReserveTreasureScannerTarget(harvester, best)) return null;
      ref var gem = ref flatSpatialHash.Gems[best];
      harvester.TargetGemGridIndex = best;
      harvester.TargetGemEntityId = gem.EntityId;
      return new Vector2(gem.X, gem.Y);
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
        if (!flatSpatialHash.IsAvailable(gemIndex)
          || !TryReserveTreasureScannerTarget(harvester, gemIndex))
          continue;

        ref GemData candidate = ref flatSpatialHash.Gems[gemIndex];
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
      foreach (int gemIndex in flatSpatialHash.AvailableIndices)
      {
        if (!flatSpatialHash.IsAvailable(gemIndex))
          continue;
        ref GemData gem = ref flatSpatialHash.Gems[gemIndex];

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

      partner.CarryingGemBaseValue = PrestigeProgression.AddSaturating(partner.CarryingGemBaseValue, wholeValue);
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
      return flatSpatialHash.IsAvailable(harvester.TargetGemGridIndex)
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
      if (flatSpatialHash.TryGetWeightedClusterPosition(Random.Shared, out Vector2 weightedTarget, minGems: 3))
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
        float offsetX = (float)(Random.Shared.NextDouble() * 70.0 - 35.0);
        float offsetY = (float)(Random.Shared.NextDouble() * 70.0 - 35.0);

        return target + new Vector2(offsetX, offsetY);
      }

      return null;
    }
    // private Vector2? GetBiggestCluserPositionWithDistance(Harvester harvester)
    // {
    //   if (flatSpatialHash.TryGetBestScoringClusterPosition(harvester.BoundingCircle.Center, out Vector2 target, minGems: 4, minSearchRadius: 30.0f))
    //   {
    //     // Add that slight jitter we talked about so they don't stack on the exact same pixel
    //     float offsetX = (float)(Random.Shared.NextDouble() * 70.0 - 35.0);
    //     float offsetY = (float)(Random.Shared.NextDouble() * 70.0 - 35.0);
    //
    //     return target + new Vector2(offsetX, offsetY);
    //   }
    //
    //   return null;
    // }

    private PlayAreaBounds _playArea;

    private PlayAreaBounds GetHarvesterBounds(Harvester harvester, Transform2 transform)
    {
      return _playArea.InsetForCollection(GetHarvesterRadius(harvester, transform) + 8f,
        BaseStats.GetHarvesterCollectionRange(harvester), GetTargetArrivalRadius(harvester));
    }

    private static float GetHarvesterRadius(Harvester harvester, Transform2 transform)
    {
      var region = harvester?.m_sprite?.TextureRegion ?? TextureCache.HarvesterShip;
      // A circumscribed circle also contains the ship while it turns.
      return new Vector2(region.Width * transform.Scale.X, region.Height * transform.Scale.Y).Length() * 0.5f;
    }

    private static Vector2 KeepOffPlanet(Vector2 position, Harvester harvester, Transform2 transform)
      => PlanetObstacle.Active
        ? PlanetObstacle.PushOut(position, PlanetObstacle.Center,
          PlanetObstacle.Clearance(GetHarvesterRadius(harvester, transform)))
        : position;

    private static float GetTargetArrivalRadius(Harvester harvester)
      => Math.Min(Math.Clamp(BaseStats.GetHarvesterSpeed(harvester) * 0.01f, 1f, 20f),
        BaseStats.GetHarvesterCollectionRange(harvester) * 0.5f);

    public void UpdateHarvesterPosition(GameTime gameTime, Harvester harvester, Transform2 transform)
    {
      // The home base participates in collection, but does not use fleet movement.
      // Leave its arrival animation and position under HomeBase's control.
      if (harvester.Type == Harvester.HarvesterType.HomeBase
        || harvester.CurrentState == Harvester.HarvesterState.None)
        return;

      var bounds = GetHarvesterBounds(harvester, transform);
      transform.Position = KeepOffPlanet(bounds.Clamp(transform.Position), harvester, transform);
      harvester.SetCollisionPosition(transform.Position);
      // Targets on the planet would never be reached; use the nearest point outside it.
      if (harvester.TargetScreenPosition is Vector2 target)
        harvester.TargetScreenPosition = KeepOffPlanet(bounds.Clamp(target), harvester, transform);

      var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
      harvester.LaunchThrusterTimeRemaining = Math.Max(0f, harvester.LaunchThrusterTimeRemaining - dt);
      harvester.WarpDriveCooldownRemaining = Math.Max(0f, harvester.WarpDriveCooldownRemaining - dt);
      harvester.WarpDriveFlashTimeRemaining = Math.Max(0f, harvester.WarpDriveFlashTimeRemaining - dt);
      harvester.EntanglementPulseTimeRemaining = Math.Max(0f, harvester.EntanglementPulseTimeRemaining - dt);
      harvester.EntanglementPulseCooldownRemaining = Math.Max(0f, harvester.EntanglementPulseCooldownRemaining - dt);

      //TODO: fix for advanced harvester
      float targetArrivalRadius = GetTargetArrivalRadius(harvester);
      float targetArrivalRadiusSquared = targetArrivalRadius * targetArrivalRadius;

      if (harvester.IsShuttle)
      {
        UpdateShuttle(gameTime, harvester, transform, bounds);
        return;
      }

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
        if (TryDock(harvester, transform))
          return;

        UpdateMovement(UntitledGemGameGameScreen.HomeBasePos, gameTime, transform, harvester);
      }
      else if (harvester.CollectionStrategy == HarvesterStrategy.PatrolPerimeter
        && !harvester.HasModule(ShipModule.ProspectorLens))
      {
        var patrol = harvester.PerimeterPatrol;
        if (!patrol.IsStarted)
          patrol.Start(Random.Shared);
        var patrolTarget = patrol.GetTarget(bounds);
        if (Vector2.DistanceSquared(transform.Position, patrolTarget) < targetArrivalRadiusSquared)
        {
          if (harvester.IsCarrier && HoldingStation(harvester, transform.Position, dt))
            return;
          patrol.Advance();
          patrolTarget = patrol.GetTarget(bounds);
        }
        harvester.TargetScreenPosition = patrolTarget;
        UpdateMovement(patrolTarget, gameTime, transform, harvester);
      }
      else if (!harvester.TargetScreenPosition.HasValue
        || ((harvester.CollectionStrategy == HarvesterStrategy.RandomGemPosition
            || (harvester.HasModule(ShipModule.ProspectorLens) && harvester.TargetGemGridIndex >= 0))
          && !harvester.IsCarrier && !IsCurrentGemTargetAvailable(harvester))
        || Vector2.DistanceSquared(transform.Position, harvester.TargetScreenPosition.Value)
          < targetArrivalRadiusSquared)
      {
        if (harvester.IsCarrier && harvester.TargetScreenPosition.HasValue
          && HoldingStation(harvester, transform.Position, dt))
          return;
        harvester.TargetScreenPosition = GetNewTargetPosition(harvester, bounds);
        TryActivateWarpDrive(harvester, transform);
      }
      else if (harvester.TargetScreenPosition.HasValue)
      {
        UpdateMovement(harvester.TargetScreenPosition.Value, gameTime, transform, harvester);
      }
    }

    private static bool TryDock(Harvester harvester, Transform2 transform)
    {
      float dockingRadius = harvester.IsShuttle ? PrestigeTalentEffects.ShuttleDockRadius : BaseStats.HomeBaseDockingRadius;
      if (Vector2.DistanceSquared(transform.Position, harvester.DockPosition) > dockingRadius * dockingRadius)
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

      harvester.WarpDriveDeparturePosition = transform.Position;
      harvester.WarpDriveArrivalPosition = target;
      harvester.WarpDriveFlashTimeRemaining = BaseStats.WarpDriveFlashDurationSeconds;
      transform.Position = target;
      harvester.SetCollisionPosition(target);
      harvester.WarpDriveCooldownRemaining = BaseStats.WarpDriveCooldownSeconds;
      AudioManager.Instance.PlaySound(SoundEvent.HarvesterWarpDrive);
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

      target = GetHarvesterBounds(harvester, transform).Clamp(target);
      float planetClearance = PlanetObstacle.Clearance(GetHarvesterRadius(harvester, transform));
      if (PlanetObstacle.Active)
        target = PlanetObstacle.Steer(transform.Position,
          PlanetObstacle.PushOut(target, PlanetObstacle.Center, planetClearance),
          PlanetObstacle.Center, planetClearance);

      var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

      var movementStart = transform.Position;
      var diff = target - transform.Position;
      var distSq = diff.LengthSquared();

      // Early exit if we are practically at the target to avoid NaN during division
      if (distSq < 0.0001f) return;

      // 1. Math Optimization: Only calculate one square root for distance
      var dist = (float)Math.Sqrt(distSq);
      var dir = diff / dist;

      // 2. Cache repeated property accesses
      var isDrone = harvester.Type == Harvester.HarvesterType.Drone;

      var speed = BaseStats.GetHarvesterSpeed(harvester);
      var moveLen = dt * speed * HomeBase.BonusMoveSpeed;
      if (harvester.Type == Harvester.HarvesterType.PerimeterHarvester
        && UpgradeManager.Instance.UG.PerimeterThrusters
        && PerimeterPatrol.EdgeDistance(transform.Position, _playArea) <= PerimeterPatrol.EdgeBand(_playArea))
        moveLen *= 2f;
      var movement = dir * moveLen;

      // Calculate movement scalar rather than doing vector.Length() multiple times
      // var moveLen = dt * speed * HomeBase.BonusMoveSpeed;
      // var movement = dir * moveLen;

      if (moveLen > 0.05f)
        harvester.PositionMoved = true;

      // var fuelCost = isDrone ? 0f : moveLen * (2.0f - ug.FuelEfficiency);
      bool freeFuel = (BaseStats.IsFleetHarvester(harvester) && UpgradeManager.Instance.UGM.InfiniteFleetFuel) || harvester.IsShuttle || UntitledGemGameGameScreen.Instance?.ManualAbilities.FreeFuel == true;
      var fuelCost = isDrone || freeFuel ? 0f : (moveLen * 1.5f) / BaseStats.GetHarvesterFuelEfficiency(harvester)
        * PrestigeTalentEffects.FuelCostMultiplier(harvester);


      // Hardcode Pi/2 constant to avoid calculating it every frame
      float radians = (float)Math.Atan2(dir.Y, dir.X);
      transform.Rotation = LerpAngle(transform.Rotation, radians + 1.570796f, dt * 20.0f);

      // 3. Fix Overshoot check using simple scalars
      if (moveLen >= dist)
      {
        transform.Position = target;
        harvester.Fuel -= fuelCost;
        harvester.MovedDistance += dist; // Add exact distance to target
        harvester.ChargeTravelModules(dist);
      }
      else if (freeFuel || harvester.Fuel > fuelCost)
      {
        transform.Position += movement;
        harvester.Fuel -= fuelCost;
        harvester.MovedDistance += moveLen;
        harvester.ChargeTravelModules(moveLen);
      }
      else if (harvester.CurrentState == Harvester.HarvesterState.Collecting)
      {
        harvester.CurrentState = Harvester.HarvesterState.OutOfFuel;
      }

      if (PlanetObstacle.Active)
        transform.Position = PlanetObstacle.PushOut(transform.Position, PlanetObstacle.Center, planetClearance);
      harvester.RecordHeistMovement(movementStart, transform.Position);
      harvester.SetCollisionPosition(transform.Position);

    }

    public void CollectGem(Gem gem, Harvester harvester)
    {
      CollectGem(gem, harvester, allowChainCollection: true);
    }

    // A pickup some talent, upgrade or module brought about, credited to it in the Collection panel.
    public void CollectGem(Gem gem, Harvester harvester, GemCollectionMethod method)
      => CollectGemCore(gem, harvester, allowChainCollection: true, method);

    private void CollectGem(Gem gem, Harvester harvester, bool allowChainCollection)
      => CollectGemCore(gem, harvester, allowChainCollection, GemCollectionMethod.None);

    private void CollectGemCore(Gem gem, Harvester harvester, bool allowChainCollection, GemCollectionMethod method)
    {
      if (gem.PickedUp) return;
      if (gem.ShouldDestroy) return;
      if (gem.WasClicked && !harvester.ForceInstantCollection) return;

      ++gemCountThisFrame;

      if (gemCountThisFrame <= 1)
      {
        AudioManager.Instance.PlaySound(SoundEvent.GemPickup);
      }

      var gemEntity = GetEntity(gem.Id);
      var harvesterEntity = GetEntity(harvester.Id);

      if (gemEntity == null || harvesterEntity == null)
        return;

      var pickupPosition = gem.BoundingCircle.Center;
      bool towing = method == GemCollectionMethod.TractorTails;
      // Clicked gems only ever reach the home base: by hand, unless the belt brought them.
      if (method == GemCollectionMethod.None && gem.WasClicked) method = GemCollectionMethod.ManualClick;
      if (harvester.TargetGemGridIndex == gem.GridIndex
        && harvester.TargetGemEntityId == gem.Id)
      {
        ReleaseTreasureScannerTarget(harvester);
        if (harvester.HasModule(ShipModule.ProspectorLens) || harvester.IsShuttle) harvester.TargetScreenPosition = null;
      }

      if (towing) gem.PickUpForTractorTail();
      else gem.SetPickedUp(gemEntity, harvesterEntity, () => { });

      if (gem.WasClicked && gem.ManualClickBonus > 0)
      {
        UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
          UntitledGemGameGameScreen.DeliveredUncounted, gem.ManualClickBonus);
        RecordCollection(harvester, method, gem.ManualClickBonus);
        gem.ManualClickBonus = 0;
      }

      if (gem.TryBloom())
      {
        ++UntitledGemGameGameScreen.Collected;
        return;
      }

      bool quantumDelivered = !towing && ((harvester.Type == Harvester.HarvesterType.AdvancedHarvester
          && UpgradeManager.Instance.UG.QuantumCargoHold)
        || (harvester.Type == Harvester.HarvesterType.PerimeterHarvester
          && UpgradeManager.Instance.UG.PerimeterCargoHold))
        && Random.Shared.NextSingle() < BaseStats.QuantumCargoDeliveryChance;

      if (quantumDelivered)
      {
        ulong value = BaseStats.GetHarvesterDeliveryValue(harvester, gem.BaseValue);
        value = MultiplySaturating(value, 1UL + (ulong)DigBuriedGems(harvester));
        UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
          UntitledGemGameGameScreen.DeliveredUncounted, value);
        RecordCollection(harvester, method, value);
      }
      else
      {
        ulong held = harvester.CarryingGemBaseValue;
        if (towing) harvester.AddTowedGem(gem, gemEntity.Get<Sprite>(), pickupPosition);
        else harvester.PickedUpGem(gem);
        harvester.AddModuleGemBonus(method, gem.BaseValue);
        // Buried gems come up beside the pickup, worth what it was worth, and take no cargo space.
        int dug = DigBuriedGems(harvester);
        if (dug > 0)
          harvester.CarryingGemBaseValue = PrestigeProgression.AddSaturating(harvester.CarryingGemBaseValue,
            MultiplySaturating(harvester.CarryingGemBaseValue - held, (ulong)dug));
        harvester.Credits.Add(method, harvester.CarryingGemBaseValue - held);
      }

      ++UntitledGemGameGameScreen.Collected;
      if (BaseStats.IsFleetHarvester(harvester)) UpgradeManager.Instance.Modules.RecordHarvest();
      ChargeResonanceCascade(harvester);
      ShareQuantumEntanglementValue(harvester, gem.BaseValue);

      if (harvester.HasModule(ShipModule.OverdriveCoil))
        harvester.OverdriveTimeRemaining = ModuleCatalog.OverdriveDuration;
      ApplyAdditionalPickupModules(harvester, pickupPosition, allowChainCollection);
      if (allowChainCollection && harvester.HasModule(ShipModule.SingularityEngine)
        && ++harvester.SingularityPickups >= ModuleCatalog.SingularityInterval)
      {
        harvester.SingularityPickups = 0;
        CollectSingularityGems(pickupPosition, harvester);
      }

      if (allowChainCollection && harvester.HasModule(ShipModule.TractorLink)
        && moduleRandom.NextSingle() < harvester.Tuned(ModuleCatalog.TractorChance))
        CollectTractorGem(pickupPosition, harvester);

      if (allowChainCollection
        && harvester.Type == Harvester.HarvesterType.ExpertHarvester
        && UpgradeManager.Instance.UG.ChainCollection)
      {
        CollectChainedGems(pickupPosition, harvester);
      }
      // m_gems2.Remove(gem.Id);
      // spatialTest.Remove(gem);
    }

    // Deep Scoops: fleet ships dig up buried gems with every gem they collect.
    private static int DigBuriedGems(Harvester harvester)
      => BaseStats.IsFleetHarvester(harvester) ? BuriedGems.Dig(UpgradeManager.Instance.UG.FleetDig) : 0;

    private static ulong MultiplySaturating(ulong value, ulong factor)
      => factor != 0 && value > ulong.MaxValue / factor ? ulong.MaxValue : value * factor;

    private static void RecordCollection(Harvester collector, GemCollectionMethod method, ulong amount)
    {
      if (amount > 0)
        UpgradeManager.Instance?.Collection?.Record(GemCollectionMeter.SourceFor(collector.Type), method, amount);
    }

    // Before the hold is emptied: the delivery is split by the credits its value came in with.
    private static void RecordDelivery(Harvester collector, ulong delivered)
    {
      if (delivered > 0)
        UpgradeManager.Instance?.Collection?.RecordDelivery(GemCollectionMeter.SourceFor(collector.Type),
          delivered, collector.CarryingGemBaseValue, collector.Credits);
    }

    private void CollectSingularityGems(Vector2 origin, Harvester harvester)
      => CollectModuleArea(harvester, origin, ModuleCatalog.SingularityRadius, ModuleCatalog.SingularityGemLimit,
        Color.MediumPurple, GemCollectionMethod.SingularityEngine);

    private void CollectTractorGem(Vector2 origin, Harvester harvester)
    {
      for (int pull = ModuleLimit(harvester, 1); pull > 0; pull--)
      {
        if (!TryCollectNearbyModuleGem(harvester, origin, ModuleCatalog.TractorRadius,
          GemCollectionMethod.TractorLink, out var target)) return;
        harvester.TractorOrigin = origin;
        harvester.TractorTarget = target;
        harvester.TractorFlashRemaining = 0.25f;
      }
    }

    private void CollectChainedGems(Vector2 origin, Harvester harvester)
    {
      float radius = BaseStats.ChainCollectionRadius;
      int collected = 0;

      foreach (int gemIndex in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
      {
        if (collected >= BaseStats.ChainCollectionBonusGems) break;
        ref GemData candidate = ref flatSpatialHash.Gems[gemIndex];
        if (!candidate.IsActive || candidate.ClaimState != 0)
          continue;

        var entity = GetEntity(candidate.EntityId);
        var chainedGem = entity?.Get<Gem>();
        if (chainedGem == null
          || Interlocked.CompareExchange(ref candidate.ClaimState, 1, 0) != 0)
        {
          continue;
        }

        CollectGemCore(chainedGem, harvester, allowChainCollection: false, GemCollectionMethod.ChainCollection);
        ++collected;
      }
    }

    private ulong CalculateDeliveryValue(Harvester harvester)
    {
      ulong deliveryValue = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
      deliveryValue = harvester.ApplyAdditionalDeliveryModules(deliveryValue);
      deliveryValue = ApplyJackpotHaul(harvester, deliveryValue);
      if (deliveryValue > 0 && harvester.HasModule(ShipModule.JackpotCore)
        && moduleRandom.NextSingle() < ModuleCatalog.JackpotChance)
      {
        deliveryValue = deliveryValue > ulong.MaxValue / ModuleCatalog.JackpotMultiplier
          ? ulong.MaxValue : deliveryValue * ModuleCatalog.JackpotMultiplier;
        UntitledGemGameGameScreen.Instance?.ShowJackpotHaul(harvester.BoundingCircle.Center, deliveryValue, false);
      }
      return deliveryValue;
    }

    private void DeliverCargo(Harvester harvester)
    {
      harvester.StowTractorTail();
      if (harvester.IsShuttle)
      {
        TransferToCarrier(harvester);
        return;
      }
      uint deliveredCargo = harvester.CarryingGemCount;
      if (deliveredCargo > 0)
        AudioManager.Instance.PlaySound(SoundEvent.HarvesterDeliverCargo);
      ReleaseTreasureScannerTarget(harvester);
      ulong deliveryValue = CalculateDeliveryValue(harvester);
      // A ghost still on its route pays out before the next one sets off.
      PayGhost(harvester);
      double heistCopy = Math.Ceiling(deliveryValue * ModuleCatalog.HeistCopyMultiplier);
      harvester.StartHeistReplay(heistCopy >= ulong.MaxValue ? ulong.MaxValue : (ulong)heistCopy);
      var endpoint = harvester.CollectionEndpoint;
      ulong queuedValue = UntitledGemGameGameScreen.DeliveredUncounted;
      UntitledGemGameGameScreen.DeliveredUncounted = deliveryValue > ulong.MaxValue - queuedValue
        ? ulong.MaxValue
        : queuedValue + deliveryValue;
      RecordDelivery(harvester, deliveryValue);
      if (harvester.IsCarrier && deliveredCargo > 0)
        UntitledGemGameGameScreen.Instance?.ShowCarrierDelivery(harvester.BoundingCircle.Center, deliveryValue);
      if (BaseStats.IsFleetHarvester(harvester) && deliveredCargo > 0)
      {
        UntitledGemGameGameScreen.Instance?.ChargeWeaponsFromCargo(deliveredCargo);
        var deliveryPosition = GetEntity(harvester.Id)?.Get<Transform2>().Position ?? UntitledGemGameGameScreen.HomeBasePos;
        // Armed Escorts: one shell per fitted module; weapon modules fire on delivery too.
        UntitledGemGameGameScreen.Instance?.ArmEscort(deliveryPosition,
          System.Numerics.BitOperations.PopCount(UpgradeManager.Instance.Modules.GetLoadout(harvester.Type)));
        UntitledGemGameGameScreen.Instance?.OnFleetDelivery(harvester, deliveryPosition, deliveredCargo);
      }
      harvester.CarryingGemCount = 0;
      harvester.CarryingGemBaseValue = 0;
      harvester.Credits.Clear();
      harvester.ReachedHome = false;
      if (harvester.Type == Harvester.HarvesterType.Drone)
      {
        if (harvester.TryRelayDrone()) return;
        harvester.MarkedForDestroy = true;
        return;
      }
      harvester.BeginModuleTrip();
      if (harvester.IsCarrier) StartShuttleTrips(harvester);
      harvester.PerimeterPatrol.Reset();
      harvester.DepartingHomeBase = harvester.CollectionStrategy != HarvesterStrategy.PatrolPerimeter;
      harvester.TargetScreenPosition = harvester.DepartingHomeBase
        ? GetHomeBaseDepartureTarget(harvester) : null;
      harvester.ReturnGateCheckedForCurrentLoad = false;
      if ((harvester.HasModule(ShipModule.ReturnBeacon) || harvester.HasModule(ShipModule.PhaseAnchor))
        && endpoint is Vector2 beacon)
      {
        var transform = harvester.Entity?.Get<Transform2>();
        if (transform != null) WarpToBeacon(harvester, transform, beacon);
      }

      if (harvester.Type == Harvester.HarvesterType.Harvester
        && UpgradeManager.Instance.UG.LaunchThrusters)
      {
        harvester.LaunchThrusterTimeRemaining = BaseStats.LaunchThrusterDurationSeconds;
      }

      if (UpgradeManager.Instance.UG.RefuelHomebase)
        harvester.IncreaseFuelPartial();
    }

    private void WarpToBeacon(Harvester harvester, Transform2 transform, Vector2 beacon)
    {
      if (m_camera != null)
      {
        _playArea = PlayAreaBounds.ForCamera(m_camera);
        beacon = GetHarvesterBounds(harvester, transform).Clamp(beacon);
      }
      harvester.WarpDriveDeparturePosition = transform.Position;
      harvester.WarpDriveArrivalPosition = beacon;
      harvester.WarpDriveFlashTimeRemaining = BaseStats.WarpDriveFlashDurationSeconds;
      transform.Position = beacon;
      harvester.SetCollisionPosition(beacon);
      harvester.DepartingHomeBase = false;
      harvester.TargetScreenPosition = null;
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
      bool phaseAnchor = harvester.HasModule(ShipModule.PhaseAnchor);
      // A shuttle's way home is its carrier.
      if (harvester.IsShuttle
        || (!phaseAnchor && (harvester.Type != Harvester.HarvesterType.UltimateHarvester
        || !UpgradeManager.Instance.UG.ReturnGate))
        || !harvester.ReturningToHomebase
        || harvester.ReturnGateCheckedForCurrentLoad)
      {
        return false;
      }

      harvester.ReturnGateCheckedForCurrentLoad = true;
      if (!phaseAnchor && Random.Shared.NextSingle() >= BaseStats.ReturnGateChance)
        return false;

      var homePosition = UntitledGemGameGameScreen.HomeBasePos;
      if (homePosition == Vector2.Zero)
        return false;

      var destination = m_camera != null ? GetHarvesterBounds(harvester, transform).Clamp(homePosition) : homePosition;
      if (phaseAnchor)
      {
        harvester.WarpDriveDeparturePosition = transform.Position;
        harvester.WarpDriveArrivalPosition = destination;
        harvester.WarpDriveFlashTimeRemaining = BaseStats.WarpDriveFlashDurationSeconds;
      }
      transform.Position = destination;
      harvester.SetCollisionPosition(transform.Position);
      return true;
    }


    private void UpdateHarvesters(int index, GameTime gameTime)
    {
      int entityId = _harvesters[index];
      var harvester = _harvesterMapper.Get(entityId);
      var transform = harvester?.Entity?.Get<Transform2>() ?? GetEntity(entityId)?.Get<Transform2>();
      if (harvester == null || transform == null || harvester.MarkedForDestroy) return;
      if (harvester.Type == Harvester.HarvesterType.HomeBase)
      {
        if (!UpgradeManager.Instance.UG.HomeBase || !UpgradeManager.Instance.UG.HomeBaseCollector)
          return;
        harvester.SetCollisionPosition(transform.Position);
      }
      if (BaseStats.IsFleetHarvester(harvester) && UpgradeManager.Instance.UGM.InfiniteFleetFuel)
      {
        if (harvester.CurrentState is Harvester.HarvesterState.OutOfFuel or Harvester.HarvesterState.RequestingFuel or Harvester.HarvesterState.Refueling)
          harvester.EmergencyRefuel();
        harvester.Fuel = harvester.ModuleMaxFuel;
      }
      harvester.AdvanceDroneTimers((float)gameTime.ElapsedGameTime.TotalSeconds);
      if (harvester.MarkedForDestroy)
      {
        if (harvester._currentTargetBucket != -1)
        {
          flatSpatialHash.ReleaseBucket(harvester._currentTargetBucket);
          harvester._currentTargetBucket = -1;
        }
        return;
      }
      if (harvester.Type == Harvester.HarvesterType.Drone && harvester.ReturningToHomebase)
      {
        ReleaseTreasureScannerTarget(harvester);
        if (harvester._currentTargetBucket != -1)
        {
          flatSpatialHash.ReleaseBucket(harvester._currentTargetBucket);
          harvester._currentTargetBucket = -1;
        }
      }
      harvester.PositionMoved = false;
      if (harvester.ReturningToHomebase) harvester.CollectionEndpoint ??= transform.Position;
      Vector2 previousPosition = transform.Position;
      UpdateHarvesterPosition(gameTime, harvester, transform);

      // Departing ships skip collection; returning ships can tow gems or use Wake Collector.
      if (harvester.DepartingHomeBase) return;
      if (!harvester.ForceInstantCollection && harvester.ReturningToHomebase)
      {
        ClaimTractorTail(harvester, previousPosition, transform.Position);
        if (harvester.HasModule(ShipModule.WakeCollector))
          ClaimWake(harvester, previousPosition, transform.Position);
        TryDock(harvester, transform);
        return;
      }
      if (harvester.IsCarrier || harvester.ShuttleRecalled) return;

      float range = BaseStats.GetHarvesterCollectionRange(harvester);
      Vector2 center = harvester.BoundingCircle.Center;
      long remaining = harvester.ForceInstantCollection ? int.MaxValue
        : Math.Max(0L, (long)harvester.ModuleReturnCapacity - harvester.CarryingGemCount);
      // A fast ship covers more than its own reach in one step. Collect along the path it
      // flew, or speed past a certain point would stop adding pickups. A warp is not a path.
      Vector2 flown = transform.Position - previousPosition;
      float flownLength = flown.Length();
      float step = BaseStats.GetHarvesterSpeed(harvester) * (float)gameTime.ElapsedGameTime.TotalSeconds;
      if (!harvester.ForceInstantCollection && flownLength > range && flownLength <= step * 1.5f + 1f)
      {
        Vector2 start = center - flown;
        Vector2 middle = center - flown * 0.5f;
        foreach (int gemIndex in flatSpatialHash.QueryCollection(middle.X, middle.Y, flownLength * 0.5f + range))
        {
          if (remaining == 0) break;
          ref var swept = ref flatSpatialHash.Gems[gemIndex];
          var position = new Vector2(swept.X, swept.Y);
          float t = Math.Clamp(Vector2.Dot(position - start, flown) / (flownLength * flownLength), 0f, 1f);
          float reach = range + flatSpatialHash.CollectionRadius(gemIndex);
          if (Vector2.DistanceSquared(position, start + flown * t) > reach * reach
            || !flatSpatialHash.TryClaim(gemIndex)) continue;
          harvester.ClaimedGems.Add(swept.EntityId);
          --remaining;
        }
        return;
      }
      foreach (int gemIndex in flatSpatialHash.QueryCollection(center.X, center.Y, range))
      {
        if (remaining == 0) break;
        ref var gem = ref flatSpatialHash.Gems[gemIndex];
        if (flatSpatialHash.TryClaim(gemIndex))
        {
          harvester.ClaimedGems.Add(gem.EntityId);
          --remaining;
        }
      }
    }

    private void ClaimWake(Harvester harvester, Vector2 start, Vector2 end)
    {
      Vector2 segment = end - start;
      float lengthSquared = segment.LengthSquared();
      if (lengthSquared < 0.001f) return;
      Vector2 center = (start + end) * 0.5f;
      float radius = ModuleCatalog.WakeRadius;
      foreach (int index in flatSpatialHash.QueryCollection(center.X, center.Y, segment.Length() * 0.5f + radius))
      {
        ref var gem = ref flatSpatialHash.Gems[index];
        var position = new Vector2(gem.X, gem.Y);
        float t = Math.Clamp(Vector2.Dot(position - start, segment) / lengthSquared, 0f, 1f);
        if (Vector2.DistanceSquared(position, start + segment * t) <= radius * radius
          && flatSpatialHash.TryClaim(index))
        {
          harvester.ClaimedGems.Add(gem.EntityId);
          harvester.ClaimsMethod = GemCollectionMethod.WakeCollector;
        }
      }
      harvester.WakeStart = start;
      harvester.WakeEnd = end;
      harvester.WakeFlashRemaining = 0.15f;
    }

    private int gemCountThisFrame;

    internal void ClaimFinalSweep(Harvester harvester, Vector2 position)
    {
      if (!harvester.TryBeginFinalSweep(position)) return;
      harvester.ClaimsMethod = GemCollectionMethod.FinalSweep;
      foreach (int gemIndex in flatSpatialHash.QueryCollection(position.X, position.Y, harvester.FinalSweepRadius))
      {
        if (flatSpatialHash.TryClaim(gemIndex))
        {
          harvester.ClaimedGems.Add(flatSpatialHash.Gems[gemIndex].EntityId);
        }
      }
    }

    private void ClaimSupernova(Harvester harvester, Vector2 position)
    {
      if (!harvester.TryBeginSupernova()) return;
      harvester.ShowModulePulse(position, ModuleCatalog.SupernovaRadius, Color.Gold);
      int claimed = 0;
      // Spread the claims around the blast before falling back to the grid's own order.
      for (int misses = 0; claimed < ModuleCatalog.SupernovaGemLimit && misses < 4;)
      {
        int index = FindModuleGem(position, ModuleCatalog.SupernovaRadius);
        if (index >= 0 && flatSpatialHash.TryClaim(index))
        {
          harvester.ClaimedGems.Add(flatSpatialHash.Gems[index].EntityId);
          ++claimed;
        }
        else ++misses;
      }
      foreach (int index in flatSpatialHash.QueryCollection(position.X, position.Y, ModuleCatalog.SupernovaRadius))
      {
        if (claimed >= ModuleCatalog.SupernovaGemLimit) break;
        if (!flatSpatialHash.TryClaim(index)) continue;
        harvester.ClaimedGems.Add(flatSpatialHash.Gems[index].EntityId);
        ++claimed;
      }
      // Only claims get resolved; with none, the tag would linger onto a later pickup.
      if (claimed > 0) harvester.ClaimsMethod = GemCollectionMethod.SupernovaCore;
    }

    internal void ResolveClaimedGems(Harvester harvester)
    {
      var method = harvester.ClaimsMethod;
      harvester.ClaimsMethod = GemCollectionMethod.None;
      foreach (int entityId in harvester.ClaimedGems)
      {
        var gem = _gemMapper.Get(entityId);
        if (gem == null || gem.Id != entityId || !gem.IsLive) continue;

        if (!harvester.MarkedForDestroy
          && (!(harvester.Type == Harvester.HarvesterType.Drone && harvester.ReturningToHomebase)
            || harvester.ResolvingFinalSweep))
          CollectGemCore(gem, harvester, allowChainCollection: true, method);

        // A reservation is not a completed pickup. Rejected pickups and ships
        // retiring this frame must leave loose gems available to every collector.
        if (!gem.PickedUp && !gem.WasClicked && !gem.ShouldDestroy)
          flatSpatialHash.ReleaseClaim(gem.GridIndex);
      }
      harvester.ClaimedGems.Clear();
      harvester.FinishFinalSweep();
    }

    private readonly List<Entity> _destroyHarvesters = new();
    private int[] _mergeBuckets = Array.Empty<int>();
    private readonly int[] _mergeClump = new int[512];
    private float _mergeCooldown;

    public override void Update(GameTime gameTime)
    {
      // Only gem cleanup should run while the player spends prestige currency, and a
      // core fracture freezes the fleet until its shard is out.
      if (UntitledGemGameGameScreen.Instance?.m_postPrestige == true
        || UntitledGemGameGameScreen.Instance?.FracturePaused == true)
        return;

      gemCountThisFrame = 0;
      var refuel = KeyboardExtended.GetState().WasKeyPressed(Keys.R);

      var mouse = GameInput.Mouse;
      var mouseWorldPos = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      bool isMouseClicked = GameInput.WindowActive && UntitledGemGameGameScreen.Instance.WorldClickTriggered
        && !RenderGuiSystem.Instance.IsOverlayVisible && !RenderGuiSystem.Instance.SalvageInputCaptured
        && GumService.Default.Cursor.Y < HudLayout.ContentBottom;
      bool clickedToRefuel = false;


      var destroyHarvester = _destroyHarvesters;
      destroyHarvester.Clear();

      // Capture once before the parallel fleet update; camera access stays on the main thread.
      _playArea = PlayAreaBounds.ForCamera(m_camera);

      flatSpatialHash.PrepareQueries();
      RefreshTreasureScannerCache(gameTime);
      UpdateMetaFleetEffects(gameTime);
      RefreshQuantumEntanglement(gameTime);
      MagnetizerCache.Refresh();

      if (GameMain.MultiThreadingEnabled && _harvesters.Count >= 64)
      {
        Parallel.For(0, _harvesters.Count, (index) =>
        {
          UpdateHarvesters(index, gameTime);
        });
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

        // Resolve reservations even when this ship reached home in this frame.
        ResolveClaimedGems(harvester);
        ResolveTractorTail(harvester, (float)gameTime.ElapsedGameTime.TotalSeconds);
        ApplyDroneLightning(harvester, (float)gameTime.ElapsedGameTime.TotalSeconds);
        ApplyTravelModules(harvester, (float)gameTime.ElapsedGameTime.TotalSeconds);
        ApplyMythicModules(harvester, (float)gameTime.ElapsedGameTime.TotalSeconds);
        IgnitePhoenix(harvester);
        LaunchDropPod(harvester);
        // Capacity can be reached during pickup resolution. Sweep before Return Gate
        // can deliver this load, and remember the collection endpoint before any warp.
        if (!harvester.ForceInstantCollection && harvester.ReturningToHomebase)
        {
          harvester.CollectionEndpoint ??= harvester.BoundingCircle.Center;
          ClaimFinalSweep(harvester, harvester.CollectionEndpoint.Value);
          if (harvester.ResolvingFinalSweep) ResolveClaimedGems(harvester);
          ClaimSupernova(harvester, harvester.CollectionEndpoint.Value);
          if (harvester.ClaimedGems.Count > 0) ResolveClaimedGems(harvester);
          ApplyFullCargoModules(harvester, harvester.CollectionEndpoint.Value);
        }

        if (harvester.ForceInstantCollection)
        {
          // The home-base collector deposits immediately.
          ulong totalDelivery = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
          UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
            UntitledGemGameGameScreen.DeliveredUncounted, totalDelivery);
          // Clicked and belt-carried gems are credited to their own rows.
          RecordDelivery(harvester, totalDelivery);
          harvester.CarryingGemCount = 0;
          harvester.CarryingGemBaseValue = 0;
          harvester.Credits.Clear();

          continue;
        }

        if (harvester.ReachedHome)
        {
          DeliverCargo(harvester);
          if (harvester.MarkedForDestroy)
            destroyHarvester.Add(harvester.Entity);
        }
        else
        {
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

        if ((refuel || UpgradeManager.Instance.UG.AutoRefuel) && harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
        {
          harvester.Refuel(refuel);
        }

        clickedToRefuel |= harvester.Update(gameTime, mouseWorldPos, isMouseClicked, m_camera);
      }

      // Wait until all ships have processed fuel requests so fleet order does not matter.
      if (clickedToRefuel && UpgradeManager.Instance.UG.FleetRefuel)
      {
        for (var i = 0; i < _harvesters.Count; i++)
        {
          var harvester = _harvesterMapper.Get(_harvesters[i]);
          if (harvester.CurrentState == Harvester.HarvesterState.RequestingFuel)
            harvester.Refuel(true);
        }
      }

      foreach (var h in destroyHarvester)
      {
        h.Destroy();
        EntityFactory.Instance.Drones.Remove(h.Id);
      }

      _mergeCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
      if (UpgradeManager.Instance.UGM.GemMerger && _mergeCooldown <= 0f)
      {
        _mergeCooldown = 0.2f;
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

    public void ProcessGemMergers(GemSpatialIndex grid, float mergeRadius, int minThreshold = 4)
    {
      // Reuse merger storage; the caller runs this pass at most five times per second.
      if (_mergeBuckets.Length < grid._tableSize)
        Array.Resize(ref _mergeBuckets, Math.Max(grid._tableSize, _mergeBuckets.Length * 2));
      int[] denseBuckets = _mergeBuckets;
      grid.GetDenseBuckets(minThreshold, denseBuckets, out int bucketCount);

      // Increased buffer size to swallow massive clumps (like in your screenshot)
      int[] clumpBuffer = _mergeClump;
      float sqrRadius = mergeRadius * mergeRadius;

      for (int i = 0; i < bucketCount; i++)
      {
        int bucketIndex = denseBuckets[i];
        int centerGemIndex = grid._bucketHeads[bucketIndex];

        while (centerGemIndex != -1)
        {
          int nextCenterIndex = grid._nextIndices[centerGemIndex];
          ref GemData centerGem = ref grid.Gems[centerGemIndex];

          if (grid.IsAvailable(centerGemIndex)
            && GetEntity(centerGem.EntityId)?.Get<Gem>()?.IsBloomSeed != true)
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

                if (grid.IsAvailable(neighborIndex)
                  && GetEntity(neighborGem.EntityId)?.Get<Gem>()?.IsBloomSeed != true)
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
          centerGemIndex = nextCenterIndex;
        }
      }
    }

    // Extracted the merge logic to keep it clean
    private void ExecuteMerge(GemSpatialIndex grid, int[] clumpBuffer, int count)
    {
      AudioManager.Instance.PlaySound(SoundEvent.GemMerge);
      ulong totalBaseValue = 0;
      bool isGilded = false;

      float centerX = grid.Gems[clumpBuffer[0]].X;
      float centerY = grid.Gems[clumpBuffer[0]].Y;
      var centerPos = new Vector2(centerX, centerY);

      for (int j = 0; j < count; j++)
      {
        int indexToMerge = clumpBuffer[j];
        ref GemData gem = ref grid.Gems[indexToMerge];

        totalBaseValue = PrestigeProgression.AddSaturating(totalBaseValue,
          gem.BaseValue + (uint)Math.Max(0, UpgradeManager.Instance.UGM.GemMergerBonus));

        var visualGem = GetEntity(gem.EntityId).Get<Gem>();
        isGilded |= visualGem.IsGilded;

        // TODO: Look up the visual/game object using gem.EntityId to play the animation
        // var visualGem = GetVisualGemById(gem.EntityId);
        visualGem.MergeGem(centerPos);
        // visualGem.ShouldDestroy = true;

        gem.IsActive = false;
        gem.ClaimState = 1;
        // grid.RecycleIndex(indexToMerge);
      }

      uint mergerMultiplier = (uint)Math.Max(0, UpgradeManager.Instance.UGM.GemMergerBonusMultiplier);
      ulong combinedValue = mergerMultiplier == 0 ? 0
        : totalBaseValue > ulong.MaxValue / mergerMultiplier ? ulong.MaxValue : totalBaseValue * mergerMultiplier;
      uint finalValue = PrestigeTalentEffects.CompressedGemValue(combinedValue);
      // EntityFactory.Instance.CreateGem(centerPos, GemTypes.LightGreen, finalValue);
      EntityFactory.Instance.QueueGemSpawn(centerPos, GemTypes.LightGreen, finalValue, isGilded: isGilded);
    }
  }
}
