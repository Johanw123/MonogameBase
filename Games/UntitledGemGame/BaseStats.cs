using UntitledGemGame;
using UntitledGemGame.Entities;

public static class BaseStats
{
  // Speed
  public const float HarvesterSpeed = 100.0f;
  public const float DroneSpeed = 150.0f;
  public const float AdvancedHarvesterSpeed = 120.0f;
  public const float ExpertHarvesterSpeed = 150.0f;
  public const float UltimateHarvesterSpeed = 200.0f;

  // Fuel & Refueling
  public const float HarvesterMaxFuel = 100.0f;
  public const float RefuelSpeed = 50.0f;

  // Collection & Spawning
  public const float HarvesterCollectionRange = 50.0f;
  public const float HomebaseCollectionRange = 100.0f;
  public const float GemSpawnCooldownSeconds = 0.7f;

  // Gem size grows quickly enough to communicate value, then tapers off so
  // merged or late-game gems never dominate the screen.
  public const float GemMaxVisualScale = 2.0f;
  public const float GemScaleDiminishingFactor = 12.0f;

  // Ability cooldowns are stored in milliseconds. Cooldown upgrades act as
  // frequency multipliers, matching GemSpawnCooldown (base cooldown / multiplier).
  public const int HomebaseMagnetizerCooldownMilliseconds = 4000;
  public const int ChainMagnetizerCooldownMilliseconds = 3000;
  public const int DroneAbilityCooldownMilliseconds = 5000;
  public const int GemSpawnerCooldownMilliseconds = 5000;

  public const float PassiveIncomeInterval = 1.0f;

  public static float GetHarvesterCollectionRangeSquared(Harvester harvester)
  {
    var range = GetHarvesterCollectionRange(harvester);
    return range * range;
  }

  public static float GetHarvesterCollectionRange(Harvester harvester)
  {
    float baseRange = 0.0f;
    float multiplierRange = 1.0f;

    switch (harvester.Type)
    {
      case Harvester.HarvesterType.HomeBase:
        baseRange = HomebaseCollectionRange;
        multiplierRange = UpgradeManager.Instance.UG.HomebaseCollectionRange;
        break;
      case Harvester.HarvesterType.Drone:
        baseRange = HarvesterCollectionRange;
        // multiplierRange = UpgradeManager.Instance.UGA.collection;
        break;
      case Harvester.HarvesterType.Harvester:
        baseRange = HarvesterCollectionRange;
        multiplierRange = UpgradeManager.Instance.UG.HarvesterCollectionRange;
        break;

      //TODO: fix range for different types
      case Harvester.HarvesterType.AdvancedHarvester:
        baseRange = HarvesterCollectionRange;
        multiplierRange = UpgradeManager.Instance.UG.AdvancedHarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.ExpertHarvester:
        baseRange = HarvesterCollectionRange;
        multiplierRange = UpgradeManager.Instance.UG.ExpertHarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.UltimateHarvester:
        baseRange = HarvesterCollectionRange;
        multiplierRange = UpgradeManager.Instance.UG.UltimateHarvesterCollectionRange;
        break;
    }

    return baseRange * multiplierRange;
  }

  public static int GetHarvesterCapacity(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    return harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterCapacity,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterCapacity,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterCapacity,
      _ => ug.HarvesterCapacity,
    };
  }

  public static float GetHarvesterMaxFuelMultiplier(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    return harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterMaxFuel,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterMaxFuel,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterMaxFuel,
      _ => ug.HarvesterMaxFuel,
    };
  }

  public static float GetHarvesterRefuelSpeedMultiplier(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    return harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedHarvesterRefuelSpeed,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertHarvesterRefuelSpeed,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateHarvesterRefuelSpeed,
      _ => ug.HarvesterRefuelSpeed,
    };
  }

  public static float GetHarvesterFuelEfficiency(Harvester harvester)
  {
    var ug = UpgradeManager.Instance.UG;
    return harvester.Type switch
    {
      Harvester.HarvesterType.AdvancedHarvester => ug.AdvancedFuelEfficiency,
      Harvester.HarvesterType.ExpertHarvester => ug.ExpertFuelEfficiency,
      Harvester.HarvesterType.UltimateHarvester => ug.UltimateFuelEfficiency,
      _ => ug.FuelEfficiency,
    };
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
        typeMultiplier = uga.DroneSpeed;
        break;

      case Harvester.HarvesterType.Harvester:
        baseSpeed = HarvesterSpeed;
        typeMultiplier = ug.HarvesterSpeed;
        break;

      case Harvester.HarvesterType.AdvancedHarvester:
        baseSpeed = AdvancedHarvesterSpeed;
        typeMultiplier = ug.AdvancedHarvesterSpeed;
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
    var speed = baseSpeed * typeMultiplier * globalMetaMultiplier;

    return speed;
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

    float valueMagnitude = System.MathF.Log2(baseValue);
    float scaleProgress = valueMagnitude / (valueMagnitude + GemScaleDiminishingFactor);
    return 1.0f + (GemMaxVisualScale - 1.0f) * scaleProgress;
  }
}
