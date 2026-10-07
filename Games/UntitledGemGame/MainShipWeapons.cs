using System;

namespace UntitledGemGame;

public enum MainShipWeapon { Cannon, Laser, Harpoon, Rockets, Railgun }

// Main ship weapon tuning, shared by the game and the progression simulator.
// Every gem in the field is knocked loose by a weapon. Each weapon has its own
// Fire Rate and Fire Power upgrades; Fire Power sets how many gems a hit knocks
// loose, how far they fly and which colors come out (GemQualityTable).
public static class MainShipWeapons
{
  // The cannon fires when the player clicks the planet (or the homebase). Auto
  // Cannon makes it fire on its own, one shot per interval at fire rate 1.
  public const float CannonInterval = 0.7f;
  public const float LaserGemsPerSecond = 0.6f;
  public const float RocketSalvoSeconds = 10f;
  public const float RailgunCycleSeconds = 20f;
  // Each railgun cycle ends in a visible wind-up before the round fires (at most
  // 40% of a short cycle); the charge fills over the rest of it.
  public const float RailgunWindUpSeconds = 0.85f;
  public const float ThermalLanceValue = 1.5f;
  public const float HarpoonReloadSeconds = 3.5f;
  public const float HarpoonPulseInterval = 1f;
  public const int HarpoonBasePulses = 5;
  public const int HarpoonBarbedPulses = 2;
  public const float HarpoonForkShare = 0.5f;
  public const int HarpoonCapacitorBonusPulses = 3;
  public const int HarpoonWinchBonusPulses = 2;

  // Weapon specials: upgrades that change how a weapon hits. The simulator values
  // each by its average effect (SpecialMultiplier); the game plays them out.
  public const int RicochetBounces = 2;
  public const float RicochetShare = 0.4f;          // of the shot's gems, per bounce
  public const float CriticalChance = 0.15f;
  public const int CriticalMultiplier = 5;
  public const float MagmaScarInterval = 0.35f;     // a scar per beam this often
  public const float MagmaScarSeconds = 4f;
  public const float MagmaShare = 0.75f;            // scars ooze this share of the beam's gems again
  public const float OverheatBuildSeconds = 6f;
  public const float OverheatSurgeSeconds = 1.5f;
  public const float OverheatVentSeconds = 0.75f;
  public const float OverheatSurgeRate = 4f;
  public const int ClusterWarheadSplit = 3;
  public const float ClusterWarheadShare = 0.6f;    // of the rocket's gems, per mini-rocket
  public const float OrbitalStrikeBonus = 1.25f;
  public const float ShockwaveShare = 0.5f;         // of the payload, as a ring around the planet
  public const float SingularityShare = 1f;         // of the payload, torn out by the black hole
  public const float SingularitySeconds = 3.5f;

  // Core Shard upgrades: rare, run-defining weapon upgrades (see CoreShards).
  public const float GatlingFireRateMultiplier = 2f;
  public const int QuadLaserBeams = 4;
  public const int RocketSwarmMultiplier = 2;
  public const int DoomsdayPayloadMultiplier = 2;
  public const int TwinHarpoons = 2;

  public static double SpecialMultiplier(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon)
  {
    double multiplier = 1;
    switch (weapon)
    {
      case MainShipWeapon.Cannon:
        if (ug.CannonRicochet) multiplier *= 1 + RicochetBounces * RicochetShare;
        if (ug.CannonCritical) multiplier *= 1 + CriticalChance * (CriticalMultiplier - 1);
        break;
      case MainShipWeapon.Laser:
        if (ug.LaserMagmaScars) multiplier *= 1 + MagmaShare;
        if (ug.LaserOverheat)
          multiplier *= (OverheatBuildSeconds + OverheatSurgeSeconds * OverheatSurgeRate)
            / (OverheatBuildSeconds + OverheatSurgeSeconds + OverheatVentSeconds);
        break;
      case MainShipWeapon.Rockets:
        if (ug.RocketClusterWarheads) multiplier *= ClusterWarheadSplit * ClusterWarheadShare;
        if (ug.RocketOrbitalStrike) multiplier *= OrbitalStrikeBonus;
        break;
      case MainShipWeapon.Harpoon:
      {
        int pulses = HarpoonPulseCount(ug);
        double equivalentPulses = pulses;
        if (ug.HarpoonForkedCurrent) equivalentPulses += pulses * HarpoonForkShare;
        if (ug.HarpoonCapacitorDischarge) equivalentPulses += HarpoonCapacitorBonusPulses;
        if (ug.HarpoonTectonicWinch) equivalentPulses += HarpoonWinchBonusPulses;
        multiplier *= equivalentPulses / HarpoonBasePulses;
        break;
      }
      case MainShipWeapon.Railgun:
        if (ug.RailgunShockwave) multiplier *= 1 + ShockwaveShare;
        if (ug.RailgunSingularity) multiplier *= 1 + SingularityShare;
        break;
    }
    return multiplier;
  }

  public static readonly MainShipWeapon[] All =
    [MainShipWeapon.Cannon, MainShipWeapon.Laser, MainShipWeapon.Harpoon,
     MainShipWeapon.Rockets, MainShipWeapon.Railgun];

  // Fires on its own: the cannon once automated, the other weapons once unlocked.
  public static bool IsAutomatic(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Cannon => ug.AutoCannon,
    MainShipWeapon.Laser => ug.MiningLaser,
    MainShipWeapon.Harpoon => ug.ArcHarpoon,
    MainShipWeapon.Rockets => ug.RocketPods,
    MainShipWeapon.Railgun => ug.Railgun,
    _ => false,
  };

  // Tree values, before signals.
  public static float FireRate(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Cannon => ug.CannonFireRate * (ug.GatlingCannon ? GatlingFireRateMultiplier : 1f),
    MainShipWeapon.Laser => ug.LaserFireRate,
    MainShipWeapon.Harpoon => ug.HarpoonFireRate,
    MainShipWeapon.Rockets => ug.RocketFireRate,
    MainShipWeapon.Railgun => ug.RailgunFireRate,
    _ => 1f,
  };

  public static int FirePower(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon) => Math.Max(1, weapon switch
  {
    MainShipWeapon.Cannon => ug.CannonFirePower,
    MainShipWeapon.Laser => ug.LaserFirePower,
    MainShipWeapon.Harpoon => ug.HarpoonFirePower,
    MainShipWeapon.Rockets => ug.RocketFirePower,
    MainShipWeapon.Railgun => ug.RailgunFirePower,
    _ => 1,
  });

  public static float CannonShotInterval(float fireRate) => CannonInterval / Math.Max(0.1f, fireRate);

  public static int LaserBeams(UpgradesGeneratorUpgrades ug) => ug.LaserQuadBeam ? QuadLaserBeams : ug.LaserTwinBeam ? 2 : 1;

  // Gems per second for one laser beam; Twin Lasers fire two.
  public static double LaserGemRate(float fireRate, int firePower) => LaserGemsPerSecond * fireRate * firePower;

  public static int RocketsPerSalvo(UpgradesGeneratorUpgrades ug)
    => Math.Max(1, ug.RocketCount) * (ug.RocketSwarm ? RocketSwarmMultiplier : 1);

  public static float RocketSalvoInterval(float fireRate) => RocketSalvoSeconds / Math.Max(0.1f, fireRate);

  // Harpoons launched together; each anchors and pulses on its own spot.
  public static int HarpoonCount(UpgradesGeneratorUpgrades ug) => ug.HarpoonTwin ? TwinHarpoons : 1;

  public static int HarpoonPulseCount(UpgradesGeneratorUpgrades ug)
    => HarpoonBasePulses + (ug.HarpoonConductiveBarbs ? HarpoonBarbedPulses : 0);

  public static float HarpoonCycleTime(UpgradesGeneratorUpgrades ug, float fireRate)
    => (HarpoonReloadSeconds + HarpoonPulseCount(ug) * HarpoonPulseInterval) / Math.Max(0.1f, fireRate);

  public static float RailgunCycleTime(float fireRate) => RailgunCycleSeconds / Math.Max(0.1f, fireRate);

  public static float RailgunWindUpTime(float fireRate) => Math.Min(RailgunWindUpSeconds, RailgunCycleTime(fireRate) * 0.4f);

  public static float RailgunChargeTime(float fireRate) => RailgunCycleTime(fireRate) - RailgunWindUpTime(fireRate);

  // Each rocket breaks off one cluster of fire power gems; each railgun
  // fragment does the same, and an impact throws Fragmentation fragments.
  public static int RailgunGems(UpgradesGeneratorUpgrades ug, int firePower)
    => firePower * Math.Max(1, ug.RailgunFragments) * (ug.RailgunDoomsday ? DoomsdayPayloadMultiplier : 1);

  // Average gems per second one weapon knocks loose on its own, before field limits.
  public static double GemsPerSecond(UpgradesGeneratorUpgrades ug, MainShipWeapon weapon, float fireRate,
    int firePower)
  {
    if (!IsAutomatic(ug, weapon)) return 0;
    double gems = weapon switch
    {
      MainShipWeapon.Cannon => firePower / CannonShotInterval(fireRate),
      MainShipWeapon.Laser => LaserBeams(ug) * LaserGemRate(fireRate, firePower),
      MainShipWeapon.Harpoon => HarpoonCount(ug) * HarpoonBasePulses * firePower / HarpoonCycleTime(ug, fireRate),
      MainShipWeapon.Rockets => (double)RocketsPerSalvo(ug) * firePower / RocketSalvoInterval(fireRate),
      MainShipWeapon.Railgun => RailgunGems(ug, firePower) / RailgunCycleTime(fireRate),
      _ => 0,
    };
    return gems * SpecialMultiplier(ug, weapon);
  }

  // All weapons together. The game passes signal-scaled rates and powers; the simulator raw ones.
  public static double GemsPerSecond(UpgradesGeneratorUpgrades ug,
    Func<MainShipWeapon, float> fireRate, Func<MainShipWeapon, int> firePower)
  {
    double total = 0;
    foreach (var weapon in All)
      total += GemsPerSecond(ug, weapon, fireRate(weapon), firePower(weapon));
    return total;
  }

  public static double GemsPerSecond(UpgradesGeneratorUpgrades ug)
    => GemsPerSecond(ug, weapon => FireRate(ug, weapon), weapon => FirePower(ug, weapon));
}
