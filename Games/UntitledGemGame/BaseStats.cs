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
        break;
      case Harvester.HarvesterType.ExpertHarvester:
        baseRange = HarvesterCollectionRange;
        break;
      case Harvester.HarvesterType.UltimateHarvester:
        baseRange = HarvesterCollectionRange;
        break;
    }

    return baseRange * multiplierRange;
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
}

