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
  ConstellationCapacity,
  CommandOverdriveDuration,
  CommandMagnetStrength,
  CommandAbilityRecharge,
  CommandPlanetCrackerPower,
  CommandCollectorValue,
  ClickValue,
  ClickChainRange,
  HoldClickFrequency,
  ClickComboWindow,
  CursorGravityRadius,
  CursorGravityStrength,
  CursorGravityDuration,
  CursorGravityCooldown
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
    new("Survey Beam", "LEFT CLICK", "click collection radius", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new("Autoloader", "WEAPONS", "fire rate of every main ship weapon", "Textures/scifi_icons/icons_hexagon/11_hexagon.png", false),
    new("Shaped Charges", "WEAPONS", "fire power of every main ship weapon", "Textures/scifi_icons/icons_hexagon/12_hexagon.png", false),
    new("Expanded Field", "PRODUCTION", "ambient gem capacity", "Textures/scifi_icons/icons_hexagon/18_hexagon.png", false),
    new("Orbital Synthesizer", "ECONOMY", "passive income", "Textures/scifi_icons/icon_heal/17_heal.png", false),
    new("Crystal Nursery", "CLUSTERS", "gems per cluster", "Textures/scifi_icons/icon_snipe/4_snipe.png", false),
    new("Fortune Prism", "REFINEMENT", "lucky gem value", "Textures/scifi_icons/icons_hexagon/15_hexagon.png", false),
    new("Meteor Bounty", "GEM SHOWERS", "gems per shower", "Textures/scifi_icons/icon_power/22_power.png", false),
    new("Meteor Beacon", "GEM SHOWERS", "gem shower frequency", "Textures/scifi_icons/icon_power/22_power.png", false),
    new("Comet Harvest", "GEM COMETS", "gems per comet", "Textures/scifi_icons/icon_power/6_power.png", false),
    new("Comet Beacon", "GEM COMETS", "gem comet frequency", "Textures/scifi_icons/icon_power/6_power.png", false),
    new("Swarm Fabricator", "DRONE SWARM", "drones per deployment", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Endurance Cell", "DRONE SWARM", "drone lifetime", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Micro Thrusters", "DRONE SWARM", "drone speed", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Micro Tractor", "DRONE SWARM", "drone collection radius", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Sustained Polarity", "TRACTOR FIELD", "Tractor Field duration", "Textures/scifi_icons/icon_power/11_power.png", false),
    new("Ring Replicator", "GENESIS PULSE", "gems per Genesis Pulse ring", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new("Charged Lattice", "GRAVITON CASCADE", "Residual Charge value bonus", "Textures/scifi_icons/icon_power/12_power.png", false),
    new("Sweep Refiner", "DRONE SWARM", "Final Sweep value bonus", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Polarity Relay", "TRACTOR FIELD", "Tractor Field cooldown", "Textures/scifi_icons/icon_power/11_power.png", true),
    new("Arc Relay", "GRAVITON CASCADE", "Graviton Cascade cooldown", "Textures/scifi_icons/icon_power/12_power.png", true),
    new("Launch Relay", "DRONE SWARM", "Drone Swarm cooldown", "Textures/scifi_icons/icon_snipe/20_snipe.png", true),
    new("Genesis Relay", "GENESIS PULSE", "Genesis Pulse cooldown", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", true),
    new("Stellar Net", "GRAVITON CASCADE", "Constellation capture capacity", "Textures/scifi_icons/icon_power/12_power.png", false),
    new("Afterburner Reserve", "MANUAL COMMANDS", "manual Overdrive duration", "Textures/scifi_icons/icon_accuracy/18_accuracy.png", false),
    new("Graviton Focus", "MANUAL COMMANDS", "Homebase Magnetizer pull strength", "Textures/scifi_icons/icon_power/11_power.png", false),
    new("Temporal Relay", "MANUAL COMMANDS", "Ability Surge recharge bonus", "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new("Fault Line", "MANUAL COMMANDS", "gems the Planet Cracker knocks loose", "Textures/scifi_icons/icons_hexagon/15_hexagon.png", false),
    new("Swarm Uplink", "MANUAL COMMANDS", "Collector Swarm delivery value", "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new("Touch of Fortune", "LEFT CLICK", "clicked gem value", "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new("Arc Conductor", "LEFT CLICK", "Gem Link hop range", "Textures/scifi_icons/icon_power/12_power.png", false),
    new("Pulse Accelerator", "LEFT CLICK", "held-click frequency", "Textures/scifi_icons/icon_power/4_power.png", false),
    new("Combo Echo", "LEFT CLICK", "time available to continue a click combo", "Textures/scifi_icons/icon_arrow/16_arrow.png", false),
    new("Horizon Lens", "GRAVITY WELL", "mouse gravity well radius", "Textures/scifi_icons/icon_heal/8_heal.png", false),
    new("Singularity Core", "GRAVITY WELL", "mouse gravity well pull strength", "Textures/scifi_icons/icon_power/11_power.png", false),
    new("Persistent Gravity", "GRAVITY WELL", "mouse gravity well duration", "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new("Singularity Relay", "GRAVITY WELL", "mouse gravity well cooldown", "Textures/scifi_icons/icon_heal/17_heal.png", true),
  ];

  public static bool IsAvailable(int id)
  {
    var ug = UpgradeManager.Instance.UG;
    var abilities = UpgradeManager.Instance.UGA;
    return (SignalKind)id switch
    {
      // Retired with the old spawning systems: the gem limit is a fixed performance
      // cap, and clusters, lucky gems, showers and comets are gone. Weapons decide
      // how gems come out. The kinds stay so signal ids keep their meaning.
      SignalKind.GemLimit or SignalKind.ClusterSize or SignalKind.LuckyValue
        or SignalKind.ShowerCount or SignalKind.ShowerFrequency
        or SignalKind.CometCount or SignalKind.CometFrequency => false,
      SignalKind.PassiveIncome => ug.PassiveIncome > 0,
      SignalKind.DroneCount or SignalKind.DroneLifetime or SignalKind.DroneSpeed
        or SignalKind.DroneRange or SignalKind.DroneCooldown => abilities.Drones > 0,
      SignalKind.MagnetDuration or SignalKind.MagnetCooldown => abilities.HomebaseMagnetizer > 0,
      SignalKind.ChainCooldown => abilities.ChainMagnetizer > 0,
      SignalKind.SpawnerCount or SignalKind.SpawnerCooldown => abilities.GemSpawner > 0,
      SignalKind.ChainValue => abilities.ChainResidualCharge > 0,
      SignalKind.SweepValue => abilities.DroneFinalSweep && abilities.DroneSweepEfficiency > 0,
      SignalKind.ConstellationCapacity => abilities.ChainMagnetizerConstellation,
      SignalKind.ClickChainRange => ug.ClickChainCount > 0,
      SignalKind.HoldClickFrequency => ug.HoldClickEnabled,
      SignalKind.ClickComboWindow => ug.ClickComboBonus > 0,
      SignalKind.CursorGravityRadius or SignalKind.CursorGravityStrength
        or SignalKind.CursorGravityDuration or SignalKind.CursorGravityCooldown => ug.CursorGravityEnabled,
      _ => true
    };
  }
}
