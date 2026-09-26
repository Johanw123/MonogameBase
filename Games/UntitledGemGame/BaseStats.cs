using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

public static class BaseStats
{
  // Speed
  public const float HarvesterSpeed = 100.0f;
  public const float DroneSpeed = 150.0f;
  public const float DroneAfterburnerSpeedMultiplier = 2f;
  public const float DroneFinalSweepRadiusMultiplier = 3f;
  public const float DroneFinalSweepDurationSeconds = 0.3f;
  public const float DroneMaxLifetimeMultiplier = 2f;
  public const float AdvancedHarvesterSpeed = 120.0f;
  public const float PerimeterHarvesterSpeed = 120.0f;
  public const float ExpertHarvesterSpeed = 150.0f;
  public const float UltimateHarvesterSpeed = 200.0f;

  // Fuel & Refueling
  public const float HarvesterMaxFuel = 100.0f;
  public const float RefuelSpeed = 50.0f;

  // Collection & Spawning
  public const int StartingGemCount = 100;
  public const float HomeBaseDockingRadius = 55.0f;
  public const float HomeBaseDepartureRadius = 80.0f;
  public const float GemSpawnCooldownSeconds = 0.7f;

  // Normal, starting, and restored gems: fraction using a center-weighted position.
  // 0 = uniform everywhere, 1 = all center-weighted; lower this for more edge spawns.
  public const float GemSpawnCenterBias = 0.8f;
  // Average this many random positions for center-weighted spawns (minimum 1).
  // 1 = uniform, 2 = gentle center preference, 3+ = tighter concentration.
  // Positions remain inside the full play area without clamping onto its edges.
  public const int GemSpawnCenterSamples = 2;

  // Spawn-event milestones. Their frequency stays predictable while the
  // normal spawn upgrades continue to improve the economy around them.
  public const float ClusterRadius = 65.0f;
  public const float CosmicClusterChanceMultiplier = 0.5f;
  public const float ClusterCoreValueMultiplier = 5.0f;
  public const float MotherlodeChance = 0.05f;
  public const int MotherlodeSizeMultiplier = 3;
  public const float SuperclusterChance = 0.08f;
  public const int SuperclusterCount = 3;
  public const float MonochromeVeinChance = 0.12f;
  public const float GemShowerCooldownSeconds = 15.0f;
  public const float GemCometCooldownSeconds = 7.0f;

  // Harvester specialization milestones.
  public const float LaunchThrusterDurationSeconds = 3.0f;
  public const float LaunchThrusterSpeedMultiplier = 2.0f;
  public const int TreasureScannerCandidateCount = 256;
  public const float TreasureScannerRefreshSeconds = 0.25f;
  public const float QuantumCargoDeliveryChance = 0.15f;
  public const int ChainCollectionBonusGems = 2;
  public const float ChainCollectionRadius = 55.0f;
  public const float WarpDriveCooldownSeconds = 10.0f;
  public const float WarpDriveMinimumDistance = 300.0f;
  public const float WarpDriveFlashDurationSeconds = 0.45f;
  public const float ReturnGateChance = 0.30f;

  // Global prestige milestones. These remain constant-time per collection or
  // delivery so their cost does not grow with the number of gems in the world.
  public const float JackpotHaulChance = 0.05f;
  public const float JackpotHaulMegaChance = 0.10f;
  public const ulong JackpotHaulMultiplier = 5;
  public const ulong JackpotHaulMegaMultiplier = 10;
  public const int ResonanceCascadeCollectionsRequired = 50;
  public const float ResonanceCascadeDurationSeconds = 6.0f;
  public const float ResonanceCascadeSpeedMultiplier = 1.75f;
  public const float ResonanceCascadeRangeMultiplier = 1.5f;
  public const float QuantumEntanglementValueShare = 0.25f;
  public const float QuantumEntanglementRefreshSeconds = 1.0f;
  public const float QuantumEntanglementPulseSeconds = 0.3f;
  public const float QuantumEntanglementPulseCooldownSeconds = 1.25f;

  // Gem size grows quickly enough to communicate value, then tapers off so
  // merged or late-game gems never dominate the screen.
  public const float GemMaxVisualScale = 2.0f;
  public const float GemScaleHalfGrowthValue = 1_000_000.0f;
  public const float GemScaleGrowthExponent = 3.0f;

  // Ability cooldowns are stored in milliseconds. Cooldown upgrades act as
  // frequency multipliers, matching GemSpawnCooldown (base cooldown / multiplier).
  public const int HomebaseMagnetizerCooldownMilliseconds = 4000;
  public const int ChainMagnetizerCooldownMilliseconds = 3000;
  public const int MaxRenderedChainMagnetizerLines = 400;
  public const int DroneAbilityCooldownMilliseconds = 5000;
  public const int GemSpawnerCooldownMilliseconds = 5000;

  public const float PassiveIncomeInterval = 1.0f;

  public static float GetHarvesterCollectionRangeSquared(Harvester harvester)
  {
    var range = GetHarvesterCollectionRange(harvester);
    return range * range;
  }

  public static float GetHarvesterBaseCollectionRange(Harvester harvester)
  {
    // Visible hull half-sizes in the source PNGs, excluding transparent padding.
    // Apply the entity's base scale. Rendering adds collection bonuses separately
    // so visual growth cannot feed back into the collection radius.
    Vector2 halfSize = harvester.Type switch
    {
      Harvester.HarvesterType.HomeBase => new Vector2(31f, 42.5f),
      Harvester.HarvesterType.Harvester => new Vector2(12f, 11f),
      Harvester.HarvesterType.AdvancedHarvester => new Vector2(15f, 13.5f),
      Harvester.HarvesterType.PerimeterHarvester => new Vector2(27f, 14f),
      Harvester.HarvesterType.ExpertHarvester => new Vector2(16f, 15f),
      Harvester.HarvesterType.UltimateHarvester => new Vector2(21f, 21f),
      Harvester.HarvesterType.Drone => new Vector2(16f, 13.5f),
      _ => Vector2.Zero,
    };
    Vector2 scale = harvester.Entity?.Get<Transform2>()?.Scale ?? Vector2.One;
    return System.MathF.Max(halfSize.X * System.MathF.Abs(scale.X),
      halfSize.Y * System.MathF.Abs(scale.Y));
  }

  public static float GetHarvesterCollectionRange(Harvester harvester)
  {
    return GetHarvesterBaseCollectionRange(harvester) * GetHarvesterCollectionRangeMultiplier(harvester);
  }

  public static float GetHarvesterCollectionRangeMultiplier(Harvester harvester)
  {
    float multiplierRange = 1.0f;

    switch (harvester.Type)
    {
      case Harvester.HarvesterType.HomeBase:
        multiplierRange = UpgradeManager.Instance.UG.HomebaseCollectionRange * UpgradeManager.Instance.Signals.Multiplier(SignalKind.HomeRange);
        break;
      case Harvester.HarvesterType.Drone:
        multiplierRange = SignalStats.DroneRange;
        break;
      case Harvester.HarvesterType.Harvester:
        multiplierRange = UpgradeManager.Instance.UG.HarvesterCollectionRange;
        break;

      case Harvester.HarvesterType.AdvancedHarvester:
        multiplierRange = UpgradeManager.Instance.UG.AdvancedHarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.PerimeterHarvester:
        multiplierRange = UpgradeManager.Instance.UG.PerimeterHarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.ExpertHarvester:
        multiplierRange = UpgradeManager.Instance.UG.ExpertHarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.UltimateHarvester:
        multiplierRange = UpgradeManager.Instance.UG.UltimateHarvesterCollectionRange;
        break;
    }

    float globalMultiplier = IsFleetHarvester(harvester)
      ? UpgradeManager.Instance.UGM.AllHarvesterCollectionRange
      : 1.0f;
    if (IsFleetHarvester(harvester))
      globalMultiplier *= HarvesterCollectionSystem.ResonanceRangeMultiplier * UpgradeManager.Instance.Signals.Multiplier(SignalKind.CollectionRange);
    if (harvester.HasModule(ShipModule.WidebandArray)) globalMultiplier *= ModuleCatalog.WidebandRangeMultiplier;
    return multiplierRange * globalMultiplier * harvester.AdditionalModuleRangeMultiplier();
  }

  public static int GetHarvesterCapacity(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    int typeCapacity = harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterCapacity,
      Harvester.HarvesterType.PerimeterHarvester => ug.PerimeterHarvesterCapacity,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterCapacity,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterCapacity,
      _ => ug.HarvesterCapacity,
    };

    if (!IsFleetHarvester(harvester))
      return typeCapacity;

    return System.Math.Max(1, (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(
      (double)typeCapacity * UpgradeManager.Instance.UGM.AllHarvesterCapacity * UpgradeManager.Instance.Signals.Multiplier(SignalKind.Capacity)
      * (harvester.HasModule(ShipModule.CargoPod) ? ModuleCatalog.CargoMultiplier : 1f)
      * harvester.AdditionalModuleCapacityMultiplier())));
  }

  public static float GetHarvesterMaxFuelMultiplier(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    float typeMultiplier = harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterMaxFuel,
      Harvester.HarvesterType.PerimeterHarvester => ug.PerimeterHarvesterMaxFuel,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterMaxFuel,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterMaxFuel,
      _ => ug.HarvesterMaxFuel,
    };

    return IsFleetHarvester(harvester)
      ? typeMultiplier * UpgradeManager.Instance.UGM.AllHarvesterMaxFuel * UpgradeManager.Instance.Signals.Multiplier(SignalKind.Fuel)
        * (harvester.HasModule(ShipModule.AuxiliaryTank) ? 1.75f : 1f)
      : typeMultiplier;
  }

  public static float GetHarvesterRefuelSpeedMultiplier(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    float multiplier = harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterRefuelSpeed,
      Harvester.HarvesterType.PerimeterHarvester => ug.PerimeterHarvesterRefuelSpeed,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterRefuelSpeed,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterRefuelSpeed,
      _ => ug.HarvesterRefuelSpeed,
    };
    return multiplier * (IsFleetHarvester(harvester) ? UpgradeManager.Instance.Signals.Multiplier(SignalKind.Refuel) : 1f)
      * (harvester.HasModule(ShipModule.QuickCoupler) ? 1.6f : 1f);
  }

  public static float GetHarvesterFuelEfficiency(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    float typeMultiplier = harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedFuelEfficiency,
      Harvester.HarvesterType.PerimeterHarvester => ug.PerimeterFuelEfficiency,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertFuelEfficiency,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateFuelEfficiency,
      _ => ug.FuelEfficiency,
    };

    return IsFleetHarvester(harvester)
      ? typeMultiplier * UpgradeManager.Instance.UGM.AllHarvesterFuelEfficiency * UpgradeManager.Instance.Signals.Multiplier(SignalKind.FuelEfficiency)
        * (harvester.HasModule(ShipModule.FuelRecycler) ? ModuleCatalog.FuelEfficiencyMultiplier : 1f)
        * (harvester.HasModule(ShipModule.StellarEngine) ? 0.5f : 1f)
      : typeMultiplier;
  }


  public static float GetHarvesterSpeed(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    var uga = UpgradeManager.Instance.UGA;

    float baseSpeed = 0f;
    float typeMultiplier = 1.0f;

    switch (harvester.Type)
    {
      case Harvester.HarvesterType.Drone:
        baseSpeed = DroneSpeed;
        typeMultiplier = SignalStats.DroneSpeed;
        if (uga.DroneAfterburners && harvester.ReturningToHomebase)
          typeMultiplier *= DroneAfterburnerSpeedMultiplier;
        break;

      case Harvester.HarvesterType.Harvester:
        baseSpeed = HarvesterSpeed;
        typeMultiplier = ug.HarvesterSpeed;
        break;

      case Harvester.HarvesterType.AdvancedHarvester:
        baseSpeed = AdvancedHarvesterSpeed;
        typeMultiplier = ug.AdvancedHarvesterSpeed;
        break;
      case Harvester.HarvesterType.PerimeterHarvester:
        baseSpeed = PerimeterHarvesterSpeed;
        typeMultiplier = ug.PerimeterHarvesterSpeed;
        break;
      case Harvester.HarvesterType.ExpertHarvester:
        baseSpeed = ExpertHarvesterSpeed;
        typeMultiplier = ug.ExpertHarvesterSpeed;
        break;
      case Harvester.HarvesterType.UltimateHarvester:
        baseSpeed = UltimateHarvesterSpeed;
        typeMultiplier = ug.UltimateHarvesterSpeed;
        break;
    }

    float globalMetaMultiplier = UpgradeManager.Instance.UGM.AllHarvesterSpeed;
    var speed = baseSpeed * typeMultiplier * globalMetaMultiplier * UpgradeManager.Instance.Signals.Multiplier(SignalKind.Speed);

    if (IsFleetHarvester(harvester))
      speed *= HarvesterCollectionSystem.ResonanceSpeedMultiplier;

    if (IsFleetHarvester(harvester) && harvester.ReturningToHomebase)
      speed *= UpgradeManager.Instance.UGM.AllHarvesterReturnSpeed * UpgradeManager.Instance.Signals.Multiplier(SignalKind.ReturnSpeed);

    if (harvester.Type == Harvester.HarvesterType.Harvester
      && UpgradeManager.Instance.UG.LaunchThrusters
      && harvester.LaunchThrusterTimeRemaining > 0f)
    {
      speed *= LaunchThrusterSpeedMultiplier;
    }

    if (harvester.HasModule(ShipModule.IonBooster)) speed *= ModuleCatalog.IonSpeedMultiplier;
    if (harvester.HasModule(ShipModule.OverdriveCoil) && harvester.OverdriveTimeRemaining > 0f)
      speed *= ModuleCatalog.OverdriveSpeedMultiplier;
    return speed * harvester.AdditionalModuleSpeedMultiplier();
  }

  public static ulong GetHarvesterDeliveryValue(Harvester harvester, ulong baseValue)
  {
    double multiplier = IsFleetHarvester(harvester)
      ? UpgradeManager.Instance.UGM.AllHarvesterValueMultiplier
      : 1.0;
    if (harvester.HasModule(ShipModule.CrystalRefinery)) multiplier *= ModuleCatalog.RefineryValueMultiplier;
    double value = System.Math.Ceiling(baseValue * multiplier * UpgradeManager.Instance.Signals.Multiplier(SignalKind.GemValue));
    return value >= ulong.MaxValue ? ulong.MaxValue : (ulong)value;
  }

  public static bool IsFleetHarvester(Harvester harvester)
  {
    return harvester.Type is Harvester.HarvesterType.Harvester
      or Harvester.HarvesterType.AdvancedHarvester
      or Harvester.HarvesterType.PerimeterHarvester
      or Harvester.HarvesterType.ExpertHarvester
      or Harvester.HarvesterType.UltimateHarvester;
  }

  public static uint GetCurrentGemValue()
  {
    var gemValue = (uint)((UpgradeManager.Instance.UG.GemValue + UpgradeManager.Instance.UGM.GemValue) * UpgradeManager.Instance.UGM.GemValueMultiplier);
    return gemValue;
  }

  public static float GetGemVisualScale(uint baseValue)
  {
    if (baseValue <= 1)
      return 1.0f;

    // Ease in across value orders of magnitude; reach half the size growth at one million.
    float valueMagnitude = System.MathF.Log10(baseValue) / System.MathF.Log10(GemScaleHalfGrowthValue);
    float growth = System.MathF.Pow(valueMagnitude, GemScaleGrowthExponent);
    float scaleProgress = growth / (1.0f + growth);
    return 1.0f + (GemMaxVisualScale - 1.0f) * scaleProgress;
  }
}
