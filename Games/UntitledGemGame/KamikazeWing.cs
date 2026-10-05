using System;

namespace UntitledGemGame;

// Kamikaze Wing ship system: with the Kamikaze Drones talent it replaces Drone Swarm.
// A wing of bomber drones swings out from the homebase and dives into the planet; every
// bomber detonates on impact. Flight and blasts live in GameScreen.KamikazeWing.cs.
public static class KamikazeWing
{
  public const int CooldownMilliseconds = 12000;
  public const float FlightSeconds = 1.3f;
  public const float LaunchStaggerSeconds = 0.14f;
  // A bomber's warhead: this many times the strongest weapon's fire power.
  public const int FirePowerMultiplier = 12;
  public const int MaxBombers = 64;
  // Cluster Bombs: each bomblet bursts for this share of its bomber's blast.
  public const float BombletShare = 0.3f;
  public const int MaxBomblets = 12;
  // Doomsday Drone: the wing's last bomber.
  public const int DoomsdayMultiplier = 8;
  public const float DoomsdayQuakeShare = 0.5f;
  // Hive Mind: cooldown taken off the other ship systems per blast.
  public const int HiveMindMilliseconds = 1000;

  public static int Bombers() => Math.Clamp(SignalStats.KamikazeWingSize, 1, MaxBombers);

  public static float DamageMultiplier(UpgradesGeneratorUpgrades_abilities a)
    => Math.Max(0.1f, a.KamikazeWingDamage);

  public static int Damage(UpgradesGeneratorUpgrades_abilities a, int strongestFirePower)
    => (int)Math.Min(int.MaxValue, Math.Max(1, strongestFirePower) * (double)FirePowerMultiplier * DamageMultiplier(a));

  public static int Depth(UpgradesGeneratorUpgrades_abilities a) => Math.Max(0, a.KamikazeWingDepth);

  public static float DiveSeconds(UpgradesGeneratorUpgrades_abilities a)
    => FlightSeconds / Math.Max(0.25f, a.KamikazeWingSpeed);

  public static float CriticalChance(UpgradesGeneratorUpgrades_abilities a)
    => a.KamikazeWingVolatile ? Math.Clamp(a.KamikazeWingCriticalChance, 0, 100) / 100f : 0f;

  public static float CriticalMultiplier(UpgradesGeneratorUpgrades_abilities a)
    => Math.Max(1f, a.KamikazeWingCriticalPower);

  public static int Bomblets(UpgradesGeneratorUpgrades_abilities a)
    => a.KamikazeWingBomblets ? Math.Clamp(a.KamikazeWingBombletCount, 0, MaxBomblets) : 0;

  public static float SortieChance(UpgradesGeneratorUpgrades_abilities a)
    => a.KamikazeWingSortie ? Math.Clamp(a.KamikazeWingSortieChance, 0, 90) / 100f : 0f;
}
