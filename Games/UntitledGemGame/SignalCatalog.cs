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
  PassiveIncome,
  DroneCount,
  DroneLifetime,
  DroneSpeed,
  DroneRange,
  SpawnerCount,
  ChainValue,
  SweepValue,
  ChainCooldown,
  DroneCooldown,
  SpawnerCooldown,
  ConstellationCapacity,
  CommandOverdriveDuration,
  CommandMagnetStrength,
  CommandAbilityRecharge,
  CommandPlanetCrackerPower,
  CommandCollectorValue,
  CannonPower,
  LaserPower,
  HarpoonPower,
  RocketPower,
  RailgunPower,
  CriticalChance,
  MoltenDuration,
  OverloadPressure,
  DrillRate,
  DrillCooldown,
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
  // Name, Category and Target hold English (Name also identifies signals in capture scripts);
  // translate them where shown. Description is already in the current language.
  public string Description => Reduction ? Loc.F("Reduces {0}.", Loc.T(Target)) : Loc.F("Increases {0}.", Loc.T(Target));
}

public static class SignalCatalog
{
  public static readonly SignalDefinition[] Definitions =
  [
    new(Loc.N("Ion Thrusters"), Loc.N("PROPULSION"), Loc.N("all harvester speed"), "Textures/scifi_icons/icon_accuracy/18_accuracy.png", false),
    new(Loc.N("Temporal Relay"), Loc.N("SHIP SYSTEMS"), Loc.N("all ship system cooldowns"), "Textures/scifi_icons/icon_accuracy/14_accuracy.png", true),
    new(Loc.N("Cargo Compression"), Loc.N("LOGISTICS"), Loc.N("fleet cargo capacity"), "Textures/scifi_icons/icon_shield/4_shield.png", false),
    new(Loc.N("Prismatic Core"), Loc.N("REFINEMENT"), Loc.N("collected gem value"), "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new(Loc.N("Zero Point Cell"), Loc.N("ENERGY"), Loc.N("fleet fuel capacity"), "Textures/scifi_icons/icon_misc/9_misc.png", false),
    new(Loc.N("Fusion Coupler"), Loc.N("REFUELING"), Loc.N("fleet refueling speed"), "Textures/scifi_icons/icon_misc/18_misc.png", false),
    new(Loc.N("Tractor Array"), Loc.N("COLLECTION"), Loc.N("fleet collection radius"), "Textures/scifi_icons/icon_power/10_power.png", false),
    new(Loc.N("Efficient Burn"), Loc.N("ENERGY"), Loc.N("fleet fuel efficiency"), "Textures/scifi_icons/icon_misc/8_misc.png", false),
    new(Loc.N("Homeward Slipstream"), Loc.N("PROPULSION"), Loc.N("fleet return speed"), "Textures/scifi_icons/icon_arrow/16_arrow.png", false),
    new(Loc.N("Station Reach"), Loc.N("COLLECTION"), Loc.N("homebase collection radius"), "Textures/scifi_icons/icon_snipe/4_snipe.png", false),
    new(Loc.N("Survey Beam"), Loc.N("LEFT CLICK"), Loc.N("click collection radius"), "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new(Loc.N("Autoloader"), Loc.N("WEAPONS"), Loc.N("fire rate of every main ship weapon"), "Textures/scifi_icons/icons_hexagon/11_hexagon.png", false),
    new(Loc.N("Shaped Charges"), Loc.N("WEAPONS"), Loc.N("fire power of every main ship weapon"), "Textures/scifi_icons/icons_hexagon/12_hexagon.png", false),
    new(Loc.N("Orbital Synthesizer"), Loc.N("ECONOMY"), Loc.N("passive income"), "Textures/scifi_icons/icon_heal/17_heal.png", false),
    new(Loc.N("Swarm Fabricator"), Loc.N("DRONE SWARM"), Loc.N("drones per deployment"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Endurance Cell"), Loc.N("DRONE SWARM"), Loc.N("drone lifetime"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Micro Thrusters"), Loc.N("DRONE SWARM"), Loc.N("drone speed"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Micro Tractor"), Loc.N("DRONE SWARM"), Loc.N("drone collection radius"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Ring Replicator"), Loc.N("GENESIS PULSE"), Loc.N("gems per Genesis Pulse ring"), "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new(Loc.N("Charged Lattice"), Loc.N("GRAVITON CASCADE"), Loc.N("Residual Charge value bonus"), "Textures/scifi_icons/icon_power/12_power.png", false),
    new(Loc.N("Sweep Refiner"), Loc.N("DRONE SWARM"), Loc.N("Final Sweep value bonus"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Arc Relay"), Loc.N("GRAVITON CASCADE"), Loc.N("Graviton Cascade cooldown"), "Textures/scifi_icons/icon_power/12_power.png", true),
    new(Loc.N("Launch Relay"), Loc.N("DRONE SWARM"), Loc.N("Drone Swarm cooldown"), "Textures/scifi_icons/icon_snipe/20_snipe.png", true),
    new(Loc.N("Genesis Relay"), Loc.N("GENESIS PULSE"), Loc.N("Genesis Pulse cooldown"), "Textures/scifi_icons/icon_accuracy/14_accuracy.png", true),
    new(Loc.N("Stellar Net"), Loc.N("GRAVITON CASCADE"), Loc.N("Constellation capture capacity"), "Textures/scifi_icons/icon_power/12_power.png", false),
    new(Loc.N("Afterburner Reserve"), Loc.N("MANUAL COMMANDS"), Loc.N("manual Overdrive duration"), "Textures/scifi_icons/icon_accuracy/18_accuracy.png", false),
    new(Loc.N("Graviton Focus"), Loc.N("MANUAL COMMANDS"), Loc.N("Homebase Magnetizer pull strength"), "Textures/scifi_icons/icon_power/11_power.png", false),
    new(Loc.N("Surge Capacitor"), Loc.N("MANUAL COMMANDS"), Loc.N("System Surge recharge bonus"), "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new(Loc.N("Fault Line"), Loc.N("MANUAL COMMANDS"), Loc.N("gems the Planet Cracker knocks loose"), "Textures/scifi_icons/icons_hexagon/15_hexagon.png", false),
    new(Loc.N("Swarm Uplink"), Loc.N("MANUAL COMMANDS"), Loc.N("Collector Swarm delivery value"), "Textures/scifi_icons/icon_snipe/20_snipe.png", false),
    new(Loc.N("Bore Rifling"), Loc.N("CANNON"), Loc.N("cannon fire power"), "Textures/scifi_icons/icon_snipe/2_snipe.png", false),
    new(Loc.N("Focusing Lens"), Loc.N("MINING LASER"), Loc.N("mining laser fire power"), "Textures/scifi_icons/icon_snipe/11_snipe.png", false),
    new(Loc.N("Tether Coils"), Loc.N("ARC HARPOON"), Loc.N("Arc Harpoon fire power"), "Textures/scifi_icons/icon_arrow/18_arrow.png", false),
    new(Loc.N("Warhead Yield"), Loc.N("ROCKET PODS"), Loc.N("rocket fire power"), "Textures/scifi_icons/icon_accuracy/2_accuracy.png", false),
    new(Loc.N("Tungsten Rounds"), Loc.N("RAILGUN"), Loc.N("Railgun fire power"), "Textures/scifi_icons/icon_accuracy/4_accuracy.png", false),
    new(Loc.N("Critical Payload"), Loc.N("CANNON"), Loc.N("critical shell chance"), "Textures/scifi_icons/icon_snipe/5_snipe.png", false),
    new(Loc.N("Slag Furnace"), Loc.N("MOLTEN"), Loc.N("how long molten scars and craters burn"), "Textures/scifi_icons/icon_misc/7_misc.png", false),
    new(Loc.N("Pressure Valve"), Loc.N("PLANETARY OVERLOAD"), Loc.N("pressure needed for a Planetary Overload"), "Textures/scifi_icons/icon_power/23_power.png", true),
    new(Loc.N("Drill Bits"), Loc.N("CORE DRILL"), Loc.N("Core Drill bore rate"), "Textures/scifi_icons/icon_arrow/13_arrow.png", false),
    new(Loc.N("Drill Relay"), Loc.N("CORE DRILL"), Loc.N("Core Drill cooldown"), "Textures/scifi_icons/icon_accuracy/6_accuracy.png", true),
    new(Loc.N("Touch of Fortune"), Loc.N("LEFT CLICK"), Loc.N("clicked gem value"), "Textures/scifi_icons/icon_misc/17_misc.png", false),
    new(Loc.N("Arc Conductor"), Loc.N("LEFT CLICK"), Loc.N("Gem Link hop range"), "Textures/scifi_icons/icon_power/12_power.png", false),
    new(Loc.N("Pulse Accelerator"), Loc.N("LEFT CLICK"), Loc.N("held-click frequency"), "Textures/scifi_icons/icon_power/4_power.png", false),
    new(Loc.N("Combo Echo"), Loc.N("LEFT CLICK"), Loc.N("time available to continue a click combo"), "Textures/scifi_icons/icon_arrow/16_arrow.png", false),
    new(Loc.N("Horizon Lens"), Loc.N("GRAVITY WELL"), Loc.N("mouse gravity well radius"), "Textures/scifi_icons/icon_heal/8_heal.png", false),
    new(Loc.N("Singularity Core"), Loc.N("GRAVITY WELL"), Loc.N("mouse gravity well pull strength"), "Textures/scifi_icons/icon_power/11_power.png", false),
    new(Loc.N("Persistent Gravity"), Loc.N("GRAVITY WELL"), Loc.N("mouse gravity well duration"), "Textures/scifi_icons/icon_accuracy/14_accuracy.png", false),
    new(Loc.N("Singularity Relay"), Loc.N("GRAVITY WELL"), Loc.N("mouse gravity well cooldown"), "Textures/scifi_icons/icon_heal/17_heal.png", true)
  ];

  public static bool IsAvailable(int id)
  {
    var ug = UpgradeManager.Instance.UG;
    var abilities = UpgradeManager.Instance.UGA;
    var meta = UpgradeManager.Instance.UGM;
    return (SignalKind)id switch
    {
      SignalKind.AbilityCooldown => meta.ShipSystemsUnlocked,
      SignalKind.PassiveIncome => ug.PassiveIncome > 0,
      // The Kamikaze Wing launches more bombers with drone count and recharges with drone cooldown.
      SignalKind.DroneCount or SignalKind.DroneCooldown => abilities.Drones > 0 || abilities.KamikazeWing > 0,
      SignalKind.DroneLifetime or SignalKind.DroneSpeed or SignalKind.DroneRange => abilities.Drones > 0,
      SignalKind.ChainCooldown => abilities.ChainMagnetizer > 0,
      SignalKind.SpawnerCount or SignalKind.SpawnerCooldown => abilities.GemSpawner > 0,
      SignalKind.ChainValue => abilities.ChainResidualCharge > 0,
      SignalKind.SweepValue => abilities.DroneFinalSweep && abilities.DroneSweepEfficiency > 0,
      SignalKind.ConstellationCapacity => abilities.ChainMagnetizerConstellation,
      SignalKind.DrillRate or SignalKind.DrillCooldown => abilities.CoreDrill > 0,
      SignalKind.CommandAbilityRecharge => meta.CommandCenterUnlocked && meta.ShipSystemsUnlocked,
      SignalKind.CommandOverdriveDuration or SignalKind.CommandMagnetStrength or SignalKind.CommandPlanetCrackerPower
        or SignalKind.CommandCollectorValue => meta.CommandCenterUnlocked,
      // Weapon signals appear once you own the weapon or the special they improve.
      SignalKind.LaserPower => ug.MiningLaser,
      SignalKind.HarpoonPower => ug.ArcHarpoon,
      SignalKind.RocketPower => ug.RocketPods,
      SignalKind.RailgunPower => ug.Railgun,
      SignalKind.CriticalChance => ug.CannonCritical,
      SignalKind.MoltenDuration => ug.LaserMagmaScars || ug.RocketIncendiary || meta.ThermiteRounds,
      SignalKind.OverloadPressure => meta.PlanetaryOverload,
      SignalKind.ClickChainRange => ug.ClickChainCount > 0,
      SignalKind.HoldClickFrequency => ug.HoldClickEnabled,
      SignalKind.ClickComboWindow => ug.ClickComboBonus > 0,
      SignalKind.CursorGravityRadius or SignalKind.CursorGravityStrength
        or SignalKind.CursorGravityDuration or SignalKind.CursorGravityCooldown => ug.CursorGravityEnabled,
      _ => true
    };
  }
}
