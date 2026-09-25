using System;

namespace UntitledGemGame;

public enum SignalKind
{
  Speed,
  AbilityCooldown,
  Capacity,
  GemValue,
  Fuel,
  Refuel,
  CollectionRange,
  FuelEfficiency,
  ReturnSpeed,
  HomeRange,
  ClickRadius,
  SpawnFrequency,
  SpawnCount,
  GemLimit,
  PassiveIncome,
  ClusterSize,
  LuckyValue,
  ShowerCount,
  ShowerFrequency,
  CometCount,
  CometFrequency,
  DroneCount,
  DroneLifetime,
  DroneSpeed,
  DroneRange,
  MagnetDuration,
  SpawnerCount,
  ChainValue,
  SweepValue,
  MagnetCooldown,
  ChainCooldown,
  DroneCooldown,
  SpawnerCooldown,
  ConstellationCapacity
}

public sealed record SignalDefinition(string Name, string Category, string Target, string Icon, bool Reduction)
{
  public string Description => $"{(Reduction ? "Reduces" : "Increases")} {Target}.";
}

public static class SignalCatalog
{
  public static readonly SignalDefinition[] Definitions =
  [
    new("Ion Thrusters", "PROPULSION", "all harvester speed", "Textures/scifi_icons/icon_accuracy/18_accuracy.png", false),
    new("Temporal Relay", "ABILITIES", "all ability cooldowns", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", true),
    new("Cargo Compression", "LOGISTICS", "fleet cargo capacity", "Textures/scifi_icons/icon_shield/4_shield.png", false),
    new("Prismatic Core", "REFINEMENT", "collected gem value", "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new("Zero Point Cell", "ENERGY", "fleet fuel capacity", "Textures/scifi_icons/icon_misc/9_misc.png", false),
    new("Fusion Coupler", "REFUELING", "fleet refueling speed", "Textures/scifi_icons/icon_misc/18_misc.png", false),
    new("Tractor Array", "COLLECTION", "fleet collection radius", "Textures/scifi_icons/icon_power/10_power.png", false),
    new("Efficient Burn", "ENERGY", "fleet fuel efficiency", "Textures/scifi_icons/icon_misc/8_misc.png", false),
    new("Homeward Slipstream", "PROPULSION", "fleet return speed", "Textures/scifi_icons/icon_arrow/16_arrow.png", false),
    new("Station Reach", "COLLECTION", "homebase collection radius", "Textures/scifi_icons/icon_snipe/4_snipe.png", false),
    new("Survey Beam", "COLLECTION", "click collection radius", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new("Matter Accelerator", "PRODUCTION", "ambient gem spawn frequency", "Textures/scifi_icons/icons_hexagon/11_hexagon.png", false),
    new("Matter Replicator", "PRODUCTION", "gems per ambient spawn", "Textures/scifi_icons/icons_hexagon/12_hexagon.png", false),
    new("Expanded Field", "PRODUCTION", "ambient gem capacity", "Textures/scifi_icons/icons_hexagon/18_hexagon.png", false),
    new("Orbital Synthesizer", "ECONOMY", "passive income", "Textures/scifi_icons/icon_heal/17_heal.png", false),
    new("Crystal Nursery", "CLUSTERS", "gems per cluster", "Textures/scifi_icons/icon_snipe/4_snipe.png", false),
    new("Fortune Prism", "REFINEMENT", "lucky gem value", "Textures/scifi_icons/icons_hexagon/15_hexagon.png", false),
    new("Meteor Bounty", "GEM SHOWERS", "gems per shower", "Textures/scifi_icons/icon_power/22_power.png", false),
    new("Meteor Beacon", "GEM SHOWERS", "gem shower frequency", "Textures/scifi_icons/icon_power/22_power.png", false),
    new("Comet Harvest", "GEM COMETS", "gems per comet", "Textures/scifi_icons/icon_power/6_power.png", false),
    new("Comet Beacon", "GEM COMETS", "gem comet frequency", "Textures/scifi_icons/icon_power/6_power.png", false),
    new("Swarm Fabricator", "DRONES", "drones per deployment", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Endurance Cell", "DRONES", "drone lifetime", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Micro Thrusters", "DRONES", "drone speed", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Micro Tractor", "DRONES", "drone collection radius", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Sustained Polarity", "MAGNETIZER", "magnetizer duration", "Textures/scifi_icons/icon_power/11_power.png", false),
    new("Ring Replicator", "GEM SPAWNER", "gems per spawner ring", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new("Charged Lattice", "CHAIN MAGNETIZER", "Residual Charge value bonus", "Textures/scifi_icons/icon_power/12_power.png", false),
    new("Sweep Refiner", "DRONES", "Final Sweep value bonus", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Polarity Relay", "MAGNETIZER", "magnetizer cooldown", "Textures/scifi_icons/icon_power/11_power.png", true),
    new("Arc Relay", "CHAIN MAGNETIZER", "chain magnetizer cooldown", "Textures/scifi_icons/icon_power/12_power.png", true),
    new("Launch Relay", "DRONES", "drone ability cooldown", "Textures/scifi_icons/icon_snipe/20_snipe.png", true),
    new("Genesis Relay", "GEM SPAWNER", "gem spawner cooldown", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", true),
    new("Stellar Net", "CHAIN MAGNETIZER", "Constellation capture capacity", "Textures/scifi_icons/icon_power/12_power.png", false),
  ];

  public static bool IsAvailable(int id)
  {
    var ug = UpgradeManager.Instance.UG;
    var abilities = UpgradeManager.Instance.UGA;
    return (SignalKind)id switch
    {
      SignalKind.PassiveIncome => ug.PassiveIncome > 0,
      SignalKind.ClusterSize => ug.ClusterGems,
      SignalKind.LuckyValue => ug.LuckyGems,
      SignalKind.ShowerCount or SignalKind.ShowerFrequency => ug.GemShower,
      SignalKind.CometCount or SignalKind.CometFrequency => ug.GemComet,
      SignalKind.DroneCount or SignalKind.DroneLifetime or SignalKind.DroneSpeed
        or SignalKind.DroneRange or SignalKind.DroneCooldown => abilities.Drones > 0,
      SignalKind.MagnetDuration or SignalKind.MagnetCooldown => abilities.HomebaseMagnetizer > 0,
      SignalKind.ChainCooldown => abilities.ChainMagnetizer > 0,
      SignalKind.SpawnerCount or SignalKind.SpawnerCooldown => abilities.GemSpawner > 0,
      SignalKind.ChainValue => abilities.ChainResidualCharge > 0,
      SignalKind.SweepValue => abilities.DroneFinalSweep && abilities.DroneSweepEfficiency > 0,
      SignalKind.ConstellationCapacity => abilities.ChainMagnetizerConstellation,
      _ => true
    };
  }
}
