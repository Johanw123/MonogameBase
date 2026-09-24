namespace UntitledGemGame.Entities
{
  public static class AbilityHarvesterTargets
  {
    public static bool HasMagnetizer(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.MagnetizerHarvesters,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.MagnetizerAdvancedHarvesters,
        Harvester.HarvesterType.PerimeterHarvester => upgrades.MagnetizerPerimeterHarvesters,
        Harvester.HarvesterType.ExpertHarvester => upgrades.MagnetizerExpertHarvesters,
        Harvester.HarvesterType.UltimateHarvester => upgrades.MagnetizerUltimateHarvesters,
        Harvester.HarvesterType.Drone => upgrades.MagnetizerDrones,
        _ => false
      };

    public static bool HasGemSpawner(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.GemSpawnerHarvesters,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.GemSpawnerAdvancedHarvesters,
        Harvester.HarvesterType.PerimeterHarvester => upgrades.GemSpawnerPerimeterHarvesters,
        Harvester.HarvesterType.ExpertHarvester => upgrades.GemSpawnerExpertHarvesters,
        Harvester.HarvesterType.UltimateHarvester => upgrades.GemSpawnerUltimateHarvesters,
        Harvester.HarvesterType.Drone => upgrades.GemSpawnerDrones,
        _ => false
      };

  }
}
