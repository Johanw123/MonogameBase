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
  public const float CombinedArmsBonusPerExtraWeapon = 0.2f;
  public const float PhaseLogisticsValueMultiplier = 0.75f;
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
  // Ionized Magma: harpoon pulses arc to molten spots near the anchor (Tesla Coil reaches
  // every one), and each arc keeps its spot molten a little longer.
  public const float IonizedReach = 0.9f;          // radians around the anchor
  public const float IonizedSustainSeconds = 1f;
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
  public const double CoreBreakerThreshold = 0.5;

  // Free tier rewards, granted when a run starts.
  public const ulong SpareCells = 2;
  public const ulong HeadStartGems = 10_000;
  public const ulong ShardCache = 1;

  // Temporary fire-rate boosts the game screen sets every frame: Overdrive Protocol while
  // Overdrive runs, Shard Reactor's overcharge after a shard is collected.
  public static float ArsenalSurge = 1f;
  // Armed Escorts: delivery shells merge and launch at a steady cadence.
  public const float EscortShellSeconds = 0.15f;
  public const int EscortShellsPerShot = 6;
  public const int MaxQueuedEscortShells = 400;

  private static UpgradesGeneratorUpgrades_meta Meta => UpgradeManager.Instance?.UGM;

  public static float CargoCapacityMultiplier(Harvester harvester)
    => BaseStats.IsFleetHarvester(harvester) && Meta?.OverloadedHolds == true
      ? OverloadedCapacityMultiplier : 1f;

  public static float FuelCostMultiplier(Harvester harvester)
    => BaseStats.IsFleetHarvester(harvester) && harvester.CarryingGemCount > 0
      && Meta?.OverloadedHolds == true ? LoadedFuelCostMultiplier : 1f;

  // Lone Operator keeps the whole fleet docked.
  public static int FleetCount(int purchased)
  {
    purchased = Math.Max(0, purchased);
    if (Meta?.LoneOperator == true) return 0;
    if (Meta?.FleetRequisition != true) return purchased;
    return (int)Math.Min(int.MaxValue, (long)purchased + purchased / RequisitionInterval);
  }

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

  // Clicks and the gravity well: Lone Operator.
  public static float HandValueMultiplier => Meta?.LoneOperator == true ? LoneOperatorMultiplier : 1f;

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

  public static ulong PhaseLogisticsValue(ulong normalValue)
  {
    if (Meta?.HarvestersInstantCollection != true) return normalValue;
    double phased = Math.Ceiling(normalValue * (double)PhaseLogisticsValueMultiplier);
    return phased >= ulong.MaxValue ? ulong.MaxValue : (ulong)phased;
  }

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
