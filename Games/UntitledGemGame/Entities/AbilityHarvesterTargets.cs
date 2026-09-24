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
  }
}
