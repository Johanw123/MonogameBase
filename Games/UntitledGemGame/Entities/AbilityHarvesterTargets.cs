namespace UntitledGemGame.Entities
{
  public static class AbilityHarvesterTargets
  {
    public static bool HasMagnetizer(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.MagnetizerHarvesters,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.MagnetizerAdvancedHarvesters,
        Harvester.HarvesterType.ExpertHarvester => upgrades.MagnetizerExpertHarvesters,
        Harvester.HarvesterType.UltimateHarvester => upgrades.MagnetizerUltimateHarvesters,
        Harvester.HarvesterType.Drone => upgrades.MagnetizerDrones,
        _ => false
      };

    public static bool HasChainMagnetizer(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.ChainMagnetizerHarvesters,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.ChainMagnetizerAdvancedHarvesters,
        Harvester.HarvesterType.ExpertHarvester => upgrades.ChainMagnetizerExpertHarvesters,
        Harvester.HarvesterType.UltimateHarvester => upgrades.ChainMagnetizerUltimateHarvesters,
        Harvester.HarvesterType.Drone => upgrades.ChainMagnetizerDrones,
        _ => false
      };

    public static bool HasGemSpawner(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.GemSpawnerHarvesters,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.GemSpawnerAdvancedHarvesters,
        Harvester.HarvesterType.ExpertHarvester => upgrades.GemSpawnerExpertHarvesters,
        Harvester.HarvesterType.UltimateHarvester => upgrades.GemSpawnerUltimateHarvesters,
        Harvester.HarvesterType.Drone => upgrades.GemSpawnerDrones,
        _ => false
      };

    public static bool CanDeployDrones(this UpgradesGeneratorUpgrades_abilities upgrades, Harvester.HarvesterType type)
      => type switch
      {
        Harvester.HarvesterType.Harvester => upgrades.HarvesterDrones > 0,
        Harvester.HarvesterType.AdvancedHarvester => upgrades.AdvancedHarvesterDrones,
        Harvester.HarvesterType.ExpertHarvester => upgrades.ExpertHarvesterDrones,
        Harvester.HarvesterType.UltimateHarvester => upgrades.UltimateHarvesterDrones,
        _ => false
      };
  }
}
