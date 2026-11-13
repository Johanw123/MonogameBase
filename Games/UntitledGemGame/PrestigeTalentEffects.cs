using System;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// Shared tuning and calculations for run-changing prestige talents. Keeping the
// rules here makes their tradeoffs testable without constructing a game screen.
public static class PrestigeTalentEffects
{
  public const int RequisitionInterval = 5;
  public const float OverloadedCapacityMultiplier = 2f;
  public const float LoadedFuelCostMultiplier = 2f;
  public const float DeepCoreReachMultiplier = 1.4f;
  public const float TargetPainterDuration = 8f;
  public const float TargetPainterFireRateMultiplier = 0.65f;
  public const int TargetPainterYieldMultiplier = 2;
  // Cargo Catapult: delivered gems load slugs the homebase fires as cannon hits.
  public const int CargoCatapultLoad = 250;
  public const int CargoCatapultPower = 5;
  public const float CargoCatapultInterval = 0.25f;
  public const int CargoCatapultMaxQueued = 40;
  // Drop Pods: how fast the launched hold flies home (visual only).
  public const float DropPodLaunchSpeed = 500f;
  public const float DropPodAcceleration = 2600f;
  // Tractor Tails use a separate allowance outside the hold.
  public const int TractorTailMinimum = 3;
  public const int TractorTailCapacityDivisor = 4;
  public const int TractorTailVisualLimit = 12;

  public static int TractorTailCapacity(Harvester ship)
  {
    // Tow Winch tows without the talent, and triples the tail with it.
    bool talent = Meta?.TractorTails == true, winch = ship.HasModule(ShipModule.TowWinch);
    if ((!talent && !winch) || !BaseStats.IsFleetHarvester(ship)) return 0;
    long tail = Math.Max(TractorTailMinimum, ((long)BaseStats.GetHarvesterCapacity(ship)
      + TractorTailCapacityDivisor - 1) / TractorTailCapacityDivisor);
    return (int)Math.Min(int.MaxValue, talent && winch ? tail * 3 : tail);
  }
  // Mothership Protocol: each harvester type flies as one carrier and a swarm of shuttles
  // (Systems/HarvesterSystem.Carriers.cs). Shuttles share out a ship's hold; the carrier's
  // hold has room for a full ship's load per shuttle, so a delivery is several trips' worth.
  public const int ShuttlesPerShip = 3;
  public const float CarrierSpeedMultiplier = 0.5f;
  public const float CarrierHullScale = 3f;
  public const float ShuttleHullScale = 0.55f;
  // Shuttles range this far from the carrier, further once speed upgrades carry them there:
  // the field grows as a run goes on, and a fixed leash would strand a fast swarm.
  public const float MinShuttleLeash = 260f;
  public const float ShuttleLeashCarrierSeconds = 1.5f;
  public const float ShuttleSearchRadius = 40f;
  public const float ShuttleDockRadius = 30f;
  public const float ShuttleEscortRadius = 70f;
  // A carrier stays on station while its shuttles have gems in reach, then moves on, and
  // comes home with whatever it holds once the trip runs long.
  public const float CarrierMaxStationSeconds = 20f;
  public const float CarrierMaxTripSeconds = 60f;
  // One carrier delivery loads a whole salvo, so the catapult queues more and fires faster.
  public const int CarrierCatapultMaxQueued = 400;
  public const float CarrierCatapultInterval = 0.1f;
  public const float CombinedArmsBonusPerExtraWeapon = 0.2f;
  public const float ConstellationPayloadMultiplier = 1.5f;
  public const int ConstellationRocketLimit = 64;
  public const float ConstellationAutoLaunchSeconds = 12f;
  public const float CommandChainCooldownShare = 0.25f;
  public const float CommandNexusCooldownMultiplier = 0.7f;
  public const float CommandNexusGlobalCooldown = 10f;
  public const int MulticastMasteryLevels = 4;

  // Weapon interaction talents (rules here, effects in GameScreen.TalentCombos.cs).
  // Thermite Rounds and Incendiary Warheads leave molten craters that ooze a share of the hit again.
  public const float CraterShare = 0.5f;
  public const float CraterSeconds = 4f;
  public const float ThermiteScarDurationMultiplier = 2f;
  public const int LightningRodMaxExtraPulses = 10;
  public const int LightningRodCriticalPulses = 3;
  public const float LightningRodReloadMultiplier = 2f;
  public const float BeamRiderSeconds = 3f;
  public const float DetonationMultiplier = 2f;
  public const float RocketDetonationRadius = 0.6f; // radians around the impact
  public const float RailgunDetonationRadius = 1.3f;
  public const int EchoChancePercent = 25;
  public const int EchoVolleyShells = 3;
  public const int RelayVolleyShells = 5;
  public const float RelayLaserSeconds = 2f;
  public const float LaserOverchargeRate = 3f;
  public const int AllWeaponsVolleyShells = 3;
  public const float AllWeaponsGunCharge = 0.5f;
  public const float ShardReactorPerShard = 0.2f;
  public const float SignalResonancePerStack = 0.01f;
  public const float SignalResonanceCap = 1f;
  public const int SignalResonanceStacksPerLayer = 25;
  public const int SignalResonanceMaxLayers = 5;
  public const int OverloadPressure = 500;
  public const int OverloadGems = 120;
  // Eruptions fire every weapon, which builds pressure again: keep them apart.
  public const float OverloadCooldownSeconds = 8f;
  public const int TeslaArcLimit = 12;

  // Combo talents (effects in GameScreen.TalentCombos.cs).
  public const int PrismaticSplit = 2;
  public const float ShrapnelShellDamage = 2f;
  public const float ShrapnelCraterShare = 0.5f;   // of the shell's health, across all craters
  public const float SurgeFireRate = 2f;           // Overdrive Protocol, Shard Reactor overcharge
  public const float ShardOverchargeSeconds = 20f;
  public const float SympatheticRocketChance = 0.05f;
  public const float SympatheticRailChance = 0.01f;
  public const float SympatheticRocketInterval = 0.25f;
  public const float SympatheticRailInterval = 2f;
  public const float KineticChargePerGem = 0.005f;
  // Recoil Harvest: hand collection is worth more for a while after each Railgun round.
  public const float RecoilHarvestSeconds = 3f;
  public const float RecoilHarvestValue = 3f;
  // Kinetic Battery: each gem collected by hand stores damage for the next Railgun round.
  public const float KineticBatteryPerGem = 0.01f;
  public const float KineticBatteryMax = 2f;
  // Ionized Magma: harpoon pulses arc to molten spots near the anchor (Tesla Coil reaches
  // every one), and each arc keeps its spot molten a little longer.
  public const float IonizedReach = 0.9f;          // radians around the anchor
  public const float IonizedSustainSeconds = 1f;
  // The crit build. Deadeye lets every weapon hit crit, Hot Streak raises the chance with
  // each crit, Critical Mass answers crits with free shells and Executioner doubles what a
  // crit deals (effects in GameScreen.Precision.cs).
  public const float DeadeyeCritChance = 0.10f;
  public const float HotStreakPerCrit = 0.01f;
  public const int HotStreakMaxStacks = 50;
  public const float HotStreakSeconds = 4f;
  public const float CriticalMassShellsPerSecond = 12f;
  public const int CriticalMassMaxQueued = 24;
  public const int ExecutionerMultiplier = 10;
  // Supercritical: a maxed Hot Streak sets every weapon off, with a cannon volley of sure
  // crits, and the streak starts over (GameScreen.Precision.cs).
  public const int SupercriticalShells = 5;
  // Chaos Theory: now and then one weapon goes chaotic, and each of its hits also lands as
  // another weapon's hit (GameScreen.ChaosTheory.cs). Borrowed hits launch at a steady
  // cadence, carrying the chaotic hits' damage that has built up since the last one.
  public const float ChaosIntervalSeconds = 60f;
  public const float ChaosSeconds = 10f;
  public const float ChaosLaunchInterval = 0.2f;
  public const int ChaosMaxCharge = 5000;
  // A borrowed Railgun fires whole rounds, each with its shot sound, flash and impact,
  // so it fires them less often; the cap grows with the interval so it carries the same
  // damage per second.
  public const float ChaosRailLaunchInterval = 1f;

  public static float ChaosLaunchIntervalOf(MainShipWeapon borrowed)
    => borrowed == MainShipWeapon.Railgun ? ChaosRailLaunchInterval : ChaosLaunchInterval;

  public static int ChaosMaxChargeOf(MainShipWeapon borrowed)
    => (int)Math.Round(ChaosMaxCharge * ChaosLaunchIntervalOf(borrowed) / ChaosLaunchInterval);
  // Ouroboros: every prestige point ever earned raises weapon damage.
  public const float OuroborosPerPoint = 0.02f;
  public const ulong OuroborosMaxPoints = 100_000;
  // Hot Streak's live stacks, set by the game screen.
  public static int HotStreakStacks;

  // Crit chance every weapon hit gets on top of its own (Critical Shells, Galvanic Shock).
  public static float WeaponCritChance => (Meta?.Deadeye == true ? DeadeyeCritChance : 0f)
    + (Meta?.HotStreak == true ? HotStreakPerCrit * Math.Min(HotStreakStacks, HotStreakMaxStacks) : 0f);

  // What a crit multiplies its hit's damage by.
  public static int CritMultiplier => Meta?.Executioner == true ? ExecutionerMultiplier : MainShipWeapons.CriticalMultiplier;

  // Supercritical fires when Hot Streak is full; it needs Hot Streak to build the streak.
  public static bool SupercriticalReady(int hotStreakStacks)
    => Meta?.Supercritical == true && Meta.HotStreak && hotStreakStacks >= HotStreakMaxStacks;

  // Ouroboros: weapon damage bonus for the prestige points earned over every loop.
  public static float OuroborosBonus(ulong pointsEarned)
    => Meta?.Ouroboros == true ? Math.Min(pointsEarned, OuroborosMaxPoints) * OuroborosPerPoint : 0f;

  // The weapon a planet hit belongs to, for Chaos Theory; null for hits that are no
  // weapon's own (craters, detonations, eruptions, the fleet).
  public static MainShipWeapon? ChaosWeaponOf(PlanetDamageSource source) => source switch
  {
    PlanetDamageSource.ManualShots or PlanetDamageSource.AutoCannon => MainShipWeapon.Cannon,
    PlanetDamageSource.MiningLaser or PlanetDamageSource.MagmaScars => MainShipWeapon.Laser,
    PlanetDamageSource.ArcHarpoon or PlanetDamageSource.TeslaCoil or PlanetDamageSource.IonizedMagma
      or PlanetDamageSource.ArcLance or PlanetDamageSource.ConductorRound => MainShipWeapon.Harpoon,
    PlanetDamageSource.RocketPods => MainShipWeapon.Rockets,
    PlanetDamageSource.Railgun or PlanetDamageSource.TectonicShockwave => MainShipWeapon.Railgun,
    PlanetDamageSource.FlakBattery => MainShipWeapon.Flak,
    _ => null,
  };

  public static bool OwnsWeapon(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Cannon => true,
    MainShipWeapon.Laser => ug.MiningLaser,
    MainShipWeapon.Harpoon => ug.ArcHarpoon,
    MainShipWeapon.Rockets => ug.RocketPods,
    MainShipWeapon.Railgun => ug.Railgun,
    MainShipWeapon.Flak => ug.FlakBattery,
    _ => false,
  };

  private static readonly MainShipWeapon[] AllWeapons = Enum.GetValues<MainShipWeapon>();

  // A chaotic spell: one weapon you own goes chaotic and borrows any other weapon, owned
  // or not.
  public static (MainShipWeapon Chaotic, MainShipWeapon Borrowed) ChaosPick(UpgradesGeneratorUpgrades ug, Random random)
  {
    var owned = Array.FindAll(AllWeapons, weapon => OwnsWeapon(ug, weapon));
    var chaotic = owned[random.Next(owned.Length)];
    var others = Array.FindAll(AllWeapons, weapon => weapon != chaotic);
    return (chaotic, others[random.Next(others.Length)]);
  }

  // Magma Reservoir: molten craters and scars burn longer at the same rate (so they deal
  // more in all), and more of them can burn at once.
  public const int MoltenCapMultiplier = 2;
  public const float MoltenBurnMultiplier = 1.5f;
  public static int MoltenSpotCap(int baseCap) => Meta?.MagmaReservoir == true ? baseCap * MoltenCapMultiplier : baseCap;
  public static float MoltenBurn => Meta?.MagmaReservoir == true ? MoltenBurnMultiplier : 1f;

  // Galvanic Shock: harpoon pulses and arcs get their own crit chance, and each critical one
  // adds a stack of shock: the planet takes more damage from every weapon while it lasts.
  public const float LightningCritChance = 0.05f;
  public const float ShockDamagePerStack = 0.01f;
  public const int MaxShockStacks = 200;
  public const float ShockSeconds = 2f;
  public const int WeakPointsPerPulse = 3;
  public const float WeakPointSeconds = 10f;
  public const int WeakPointPower = 20;
  public const float WeakPointReach = 0.35f;       // radians: how close a hit must land
  public const int MaxWeakPoints = 24;
  public const float DrillSpotterShare = 0.5f;
  public const float DrillSpotterReach = 0.4f;
  public const float ChainRadius = 0.5f;
  public const float ChainDelay = 0.12f;
  public const int MoltenCoreCraters = 8;
  public const float MoltenCoreShare = 0.5f;
  public const float OverclockFireRate = 2f;
  // Heavy Ordnance, Overclock's opposite: half the shots, each twice as hard and a layer deeper.
  public const float HeavyOrdnanceFireRate = 0.5f;
  public const int HeavyOrdnanceHitMultiplier = 2;
  public const int HeavyOrdnanceLayers = 1;
  public const int LoneOperatorMultiplier = 5;
  public const float ResonanceSeconds = 10f;
  public const float ResonanceShare = 0.05f;
  public const int ResonanceMaxGems = 20_000;
  // Core Breaker: fractures at 60% of the damage per minute. Each threshold is 4–8 times the
  // last, so this brings every fracture sooner and adds one about every fourth run.
  public const double CoreBreakerThreshold = 0.6;

  // Fault Cells: each core fracture also throws out power cells for the ship systems
  // (GameScreen.FaultCells.cs).
  public const int FaultCellsPerFracture = 2;
  public static int FractureCells => Meta?.FaultCells == true ? FaultCellsPerFracture : 0;

  // Free tier rewards, granted when a run starts.
  public const ulong SpareCells = 2;
  public const ulong HeadStartGems = 10_000;
  public const ulong ShardCache = 1;

  // Temporary fire-rate boosts the game screen sets every frame: Overdrive Protocol while
  // Overdrive runs, Shard Reactor's overcharge after a shard is collected.
  public static float ArsenalSurge = 1f;
  // Set by the game screen while a Recoil Harvest window is open.
  public static bool RecoilHarvestActive;
  // Armed Escorts: delivery shells merge and launch at a steady cadence.
  public const float EscortShellSeconds = 0.15f;
  public const int EscortShellsPerShot = 6;
  public const int MaxQueuedEscortShells = 400;

  private static UpgradesGeneratorUpgrades_meta Meta => UpgradeManager.Instance?.UGM;

  public static double CargoCapacityMultiplier(Harvester harvester)
    => (BaseStats.IsFleetHarvester(harvester) && Meta?.OverloadedHolds == true ? OverloadedCapacityMultiplier : 1f)
      * harvester.RoleCapacityMultiplier;

  public static float FuelCostMultiplier(Harvester harvester)
    => BaseStats.IsFleetHarvester(harvester) && harvester.CarryingGemCount > 0
      && Meta?.OverloadedHolds == true ? LoadedFuelCostMultiplier : 1f;

  // Lone Operator keeps the whole fleet docked; Mothership Protocol turns each ship into
  // shuttles.
  public static int FleetCount(int purchased)
  {
    purchased = Math.Max(0, purchased);
    if (Meta?.LoneOperator == true) return 0;
    long ships = purchased;
    if (Meta?.FleetRequisition == true) ships += purchased / RequisitionInterval;
    if (Meta?.MothershipProtocol == true) ships *= ShuttlesPerShip;
    return (int)Math.Min(int.MaxValue, ships);
  }

  public static int CatapultQueueLimit => Meta?.MothershipProtocol == true ? CarrierCatapultMaxQueued : CargoCatapultMaxQueued;
  public static float CatapultFireInterval => Meta?.MothershipProtocol == true ? CarrierCatapultInterval : CargoCatapultInterval;

  // Advance to the next gem-quality row rather than adding an arbitrary amount
  // of fire power. Above the final color, extra rows continue every eight power.
  public static int DeepCoreQualityPower(int firePower)
  {
    firePower = Math.Max(1, firePower);
    if (Meta?.DeepCoreMunitions != true) return firePower;
    foreach (var (_, required) in GemQualityTable.ColorFirePower)
      if (required > firePower) return required;
    return firePower + GemQualityTable.FirePowerPerExtraRow;
  }

  public static float PlanetDebrisReachScale(float reachScale)
    => Meta?.DeepCoreMunitions == true ? reachScale * DeepCoreReachMultiplier : reachScale;

  // Overclock and Heavy Ordnance trade damage per hit for fire rate, keeping damage per second.
  private static float HitRateTrade => Meta?.Overclock == true ? OverclockFireRate
    : Meta?.HeavyOrdnance == true ? HeavyOrdnanceFireRate : 1f;

  // Every automatic weapon's fire rate passes through here: Overclock doubles it and Heavy
  // Ordnance halves it; Overdrive Protocol and Shard Reactor's overcharge double it while
  // they last.
  public static float AutomaticWeaponFireRate(float fireRate)
    => fireRate * (Meta?.TargetPainter == true ? TargetPainterFireRateMultiplier : 1f)
      * HitRateTrade * Math.Max(1f, ArsenalSurge);

  // Each hit of the main weapons: Overclock halves it (rounded up, so a hit never fizzles)
  // and Heavy Ordnance doubles it.
  public static int ArsenalHitYield(int gems)
    => Meta?.Overclock == true ? (gems > 1 ? (gems + 1) / 2 : gems)
      : Meta?.HeavyOrdnance == true ? (int)Math.Min(int.MaxValue, (long)gems * HeavyOrdnanceHitMultiplier) : gems;

  // The laser's damage per second stays put: more ticks each smaller, or fewer each bigger.
  public static float ArsenalLaserShare => 1f / HitRateTrade;

  // Layers every weapon hit reaches beyond its fire power.
  public static int HeavyOrdnanceBonusLayers => Meta?.HeavyOrdnance == true ? HeavyOrdnanceLayers : 0;

  // Gems collected by hand: Lone Operator, and Recoil Harvest after a Railgun round.
  public static float HandValueMultiplier => (Meta?.LoneOperator == true ? LoneOperatorMultiplier : 1f)
    * (Meta?.RecoilHarvest == true && RecoilHarvestActive ? RecoilHarvestValue : 1f);

  // The bonus stored for the next Railgun round after one more gem collected by hand.
  public static float KineticBatteryCharge(float stored)
    => Meta?.KineticBattery == true ? Math.Min(KineticBatteryMax, stored + KineticBatteryPerGem) : stored;

  public static int ManualShotMultiplier => Meta?.LoneOperator == true ? LoneOperatorMultiplier : 1;

  public static int LaserBeamSplit => Meta?.PrismaticLens == true ? PrismaticSplit : 1;

  public static float ShellDamageMultiplier => Meta?.ShrapnelShell == true ? ShrapnelShellDamage : 1f;

  public static double FractureThresholdMultiplier => Meta?.CoreBreaker == true ? CoreBreakerThreshold : 1;

  // Damage a resonance quake deals: a share of the last minute's damage, capped.
  public static int ResonanceGems(double damagePerMinute)
    => Meta?.ResonantCore == true && damagePerMinute > 0
      ? (int)Math.Min(ResonanceMaxGems, Math.Floor(damagePerMinute * ResonanceShare)) : 0;

  public static int PaintedYield(int gems, bool painted)
    => painted && Meta?.TargetPainter == true
      ? (int)Math.Min(int.MaxValue, (long)Math.Max(0, gems) * TargetPainterYieldMultiplier)
      : Math.Max(0, gems);

  // Slugs a delivery loads into the catapult, carrying the remainder over.
  public static int CargoCatapultSlugs(ref long load, uint cargo)
  {
    if (Meta?.CargoCatapult != true) return 0;
    load += cargo;
    int slugs = (int)Math.Min(int.MaxValue, load / CargoCatapultLoad);
    load -= (long)slugs * CargoCatapultLoad;
    return slugs;
  }

  public static int CombinedArmsYield(int gems, int automaticWeapons)
  {
    gems = Math.Max(0, gems);
    if (Meta?.CombinedArms != true || automaticWeapons <= 1) return gems;
    int percent = 100 + (automaticWeapons - 1) * 20;
    return (int)Math.Min(int.MaxValue, ((long)gems * percent + 99) / 100);
  }

  public static float CombinedArmsMultiplier(int automaticWeapons)
    => Meta?.CombinedArms == true && automaticWeapons > 1
      ? 1 + (automaticWeapons - 1) * CombinedArmsBonusPerExtraWeapon : 1f;

  public static uint CompressedGemValue(ulong combinedValue)
  {
    if (Meta?.WeaponizedCompression == true)
      combinedValue = combinedValue > ulong.MaxValue / 2 ? ulong.MaxValue : combinedValue * 2;
    return combinedValue >= uint.MaxValue ? uint.MaxValue : (uint)combinedValue;
  }

  public static int LightningRodPulses(bool critical, int current)
    => Meta?.LightningRod == true
      ? Math.Min(LightningRodMaxExtraPulses, Math.Max(0, current) + (critical ? LightningRodCriticalPulses : 1))
      : Math.Max(0, current);

  public static float HarpoonReloadMultiplier => Meta?.LightningRod == true ? LightningRodReloadMultiplier : 1f;

  public static float BeamRiderInterval(float laserFireRate) => BeamRiderSeconds / Math.Max(0.1f, laserFireRate);

  public static int BeamRidersPerBeam(UpgradesGeneratorUpgrades ug)
    => ug.RocketSwarm ? MainShipWeapons.RocketSwarmMultiplier : 1;

  // How far around its anchor a harpoon pulse arcs to molten spots, in radians: Tesla Coil
  // reaches the whole planet, Ionized Magma alone the spots nearby. Negative: no arcs.
  public static float MoltenArcReach(bool teslaCoil)
    => teslaCoil ? MathF.PI : Meta?.IonizedMagma == true ? IonizedReach : -1f;

  public static bool LightningCrits => Meta?.GalvanicShock == true;

  // Weapon damage on a shocked planet, multiplying every other bonus.
  public static float ShockMultiplier(int stacks) => 1f + ShockDamagePerStack * Math.Clamp(stacks, 0, MaxShockStacks);

  // Seconds each arc takes off a molten spot's age.
  public static float MoltenArcSustain => Meta?.IonizedMagma == true ? IonizedSustainSeconds : 0f;

  public static float MagmaScarSeconds(float baseSeconds)
    => Meta?.ThermiteRounds == true ? baseSeconds * ThermiteScarDurationMultiplier : baseSeconds;

  // Gems a molten scar or crater still holds: its budget oozes evenly over its life.
  public static int DetonationGems(float budget, float age, float duration)
  {
    float remaining = Math.Max(0f, budget * (1f - Math.Clamp(age / Math.Max(0.01f, duration), 0f, 1f)));
    return (int)Math.Min(int.MaxValue, Math.Ceiling(remaining * DetonationMultiplier));
  }

  // Kamikaze Drones swaps the Drone Swarm system for the Kamikaze Wing (ShipSystems).
  public const string KamikazeDronesTalent = "KD1";
  public static bool KamikazeDrones => Meta?.KamikazeDrones == true;

  public static int EchoCastCount(double rollPercent)
    => Meta?.SystemEcho == true && rollPercent < EchoChancePercent ? 2 : 1;

  public static float ShardReactorBonus(ulong heldShards)
    => Meta?.ShardReactor == true ? Math.Min(heldShards, 1000UL) * ShardReactorPerShard : 0f;

  public static float SignalResonanceBonus(long signalStacks)
    => Meta?.SignalResonance == true ? Math.Min(SignalResonanceCap, Math.Max(0, signalStacks) * SignalResonancePerStack) : 0f;

  public static int SignalResonanceLayers(long signalStacks)
    => Meta?.SignalResonance == true
      ? (int)Math.Min(SignalResonanceMaxLayers, Math.Max(0, signalStacks) / SignalResonanceStacksPerLayer) : 0;

  public static int ConstellationPayload(int storedPayload)
    => (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, storedPayload)
      * (double)ConstellationPayloadMultiplier));
}
