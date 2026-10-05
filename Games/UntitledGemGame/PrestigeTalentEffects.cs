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
  public const float CargoCatapultSecondsPerGem = 0.08f;
  public const float CargoCatapultMaxSeconds = 4f;
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
  public const float ShellDetonationRadius = 1.3f;
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

  public static int FleetCount(int purchased)
  {
    purchased = Math.Max(0, purchased);
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

  public static float AutomaticWeaponFireRate(float fireRate)
    => Meta?.TargetPainter == true ? fireRate * TargetPainterFireRateMultiplier : fireRate;

  public static int PaintedYield(int gems, bool painted)
    => painted && Meta?.TargetPainter == true
      ? (int)Math.Min(int.MaxValue, (long)Math.Max(0, gems) * TargetPainterYieldMultiplier)
      : Math.Max(0, gems);

  public static float CargoCatapultCharge(uint cargo)
    => Meta?.CargoCatapult == true
      ? Math.Min(CargoCatapultMaxSeconds, cargo * CargoCatapultSecondsPerGem) : 0f;

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
