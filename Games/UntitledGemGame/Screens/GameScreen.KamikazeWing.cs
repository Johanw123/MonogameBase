using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Kamikaze Wing (tuning in KamikazeWing.cs): bomber drones are planet shots of their own
// kind, so their blasts count as damage, feed core fractures and chain into the weapon
// talents like rocket hits (Incendiary Warheads, Magma Detonation).
public partial class UntitledGemGameGameScreen
{
  private static readonly Color KamikazeGlow = new(255, 80, 50);
  private static readonly Color DoomsdayGlow = new(255, 40, 30);
  // A bit bigger than a collecting drone, so the bombers read as a threat.
  private const float KamikazeDroneScale = 0.55f;
  private const float DoomsdayDroneScale = 0.95f;

  public int StrongestWeaponFirePower => StrongestFirePower();

  // The wing peels off the homebase one bomber after another; with Doomsday Drone the
  // last one carries the big payload.
  public void LaunchKamikazeWing()
  {
    if (!CombatActive) return;
    var upgrades = UpgradeManager.Instance.UGA;
    int bombers = KamikazeWing.Bombers();
    for (int i = 0; i < bombers; i++)
    {
      var from = HomeBasePos + new Vector2(Random.Shared.NextSingle() * 100f - 50f, Random.Shared.NextSingle() * 100f - 50f);
      LaunchKamikazeDrone(from, i * KamikazeWing.LaunchStaggerSeconds, upgrades.KamikazeWingDoomsday && i == bombers - 1);
      // Drone Gunships: every bomber fires a cannon shell as it launches.
      FireDroneShell(from);
    }
  }

  private void LaunchKamikazeDrone(Vector2 from, float delay, bool doomsday)
  {
    var upgrades = UpgradeManager.Instance.UGA;
    int firePower = StrongestFirePower();
    long damage = AutomaticWeaponYield(KamikazeWing.Damage(upgrades, firePower), arsenal: false);
    bool critical = Random.Shared.NextSingle() < KamikazeWing.CriticalChance(upgrades);
    if (critical) damage = (long)(damage * KamikazeWing.CriticalMultiplier(upgrades));
    if (doomsday) damage *= KamikazeWing.DoomsdayMultiplier;
    float angle = PlanetFacingAngle() + (Random.Shared.NextSingle() * 2f - 1f) * 1.25f;
    var end = PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.85f;
    var travel = end - from;
    float side = Random.Shared.Next(2) == 0 ? -1f : 1f;
    AddPlanetShot(new PlanetShot
    {
      Kind = PlanetShotKind.Drone,
      Delay = delay,
      Duration = KamikazeWing.DiveSeconds(upgrades) * (0.85f + 0.3f * Random.Shared.NextSingle()) * (doomsday ? 1.3f : 1f),
      Damage = (int)Math.Min(int.MaxValue, damage),
      FirePower = CoreDrill.Deeper(firePower, KamikazeWing.Depth(upgrades)),
      Critical = critical,
      Doomsday = doomsday,
      Start = from,
      // Out to one side, then straight down onto the target.
      Control1 = from + new Vector2(-travel.Y, travel.X) * side * 0.55f - travel * 0.1f,
      Control2 = end + PlanetDirection(angle) * PlanetRadius * 2.2f,
      End = end,
    });
  }

  private void DetonateKamikazeDrone(PlanetShot shot, float impactAngle, PlayAreaBounds bounds)
  {
    var upgrades = UpgradeManager.Instance.UGA;
    bool bomblet = shot.Mini;
    PulsePlanet(shot.Doomsday ? 1f : bomblet ? 0.35f : 0.8f, shot.Doomsday ? 1f : bomblet ? 0.1f : 0.35f);
    planetExplosions.Add(new PlanetExplosion
    {
      Position = shot.End,
      Scale = shot.Doomsday ? 4f : bomblet ? 0.9f : shot.Critical ? 2.6f : 1.9f,
    });
    var flash = shot.Doomsday ? DoomsdayGlow : shot.Critical ? Color.Gold : KamikazeGlow;
    SpawnerEffects.Add(null, shot.End, flash, 4f, shot.Doomsday ? 260f : bomblet ? 30f : 60f, shot.Doomsday ? 0.8f : 0.35f);
    if (!bomblet) SpawnerEffects.Add(null, shot.End, Color.White, 2f, shot.Doomsday ? 120f : 28f, 0.2f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect,
      pitch: shot.Doomsday ? -0.6f : bomblet ? 0.6f : 0.3f, priority: bomblet ? 0.3f : 0.5f);
    if (shot.Critical) ShowWorldPopup(shot.End, Loc.T("CRITICAL!"), large: true);
    if (shot.Doomsday)
    {
      ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 60f), Loc.T("DOOMSDAY"), large: true);
      SpawnerEffects.Add(null, PlanetPos, DoomsdayGlow, PlanetRadius, PlanetRadius * 6f, 1f);
      StartShockwave(PlanetDamageSource.KamikazeWing, impactAngle, (int)(shot.Damage * KamikazeWing.DoomsdayQuakeShare),
        shot.FirePower);
    }

    KnockClusterLoose(PlanetDamageSource.KamikazeWing, shot.Damage, shot.FirePower, bounds, bomblet ? 0.7f : 1.1f,
      impactAngle, bomblet ? 0.5f : 0.8f);
    // Blasts count as rocket hits: Incendiary Warheads and Magma Detonation.
    OnRocketHit(shot, impactAngle, bounds);
    if (upgrades.KamikazeWingFirestorm)
      AddCrater(shot.End, shot.Damage, shot.FirePower, shot.Doomsday ? 10f : bomblet ? 3f : 6f);
    if (bomblet) return;

    LaunchBomblets(shot, impactAngle, KamikazeWing.Bomblets(upgrades));
    if (Random.Shared.NextSingle() < KamikazeWing.SortieChance(upgrades))
      LaunchKamikazeDrone(HomeBasePos + new Vector2(0f, Random.Shared.NextSingle() * 60f - 30f), 0.1f, false);
    if (upgrades.KamikazeWingHiveMind && HomeBase.Instance != null)
      foreach (var ability in HomeBase.Instance.Abilities)
        if (ability is not KamikazeWingAbility) ability.AdvanceCooldown(KamikazeWing.HiveMindMilliseconds);
  }

  // Cluster Bombs: bomblets hop across the surface to either side of the blast.
  private void LaunchBomblets(PlanetShot shot, float impactAngle, int count)
  {
    int damage = (int)MathF.Ceiling(shot.Damage * KamikazeWing.BombletShare);
    var start = PlanetPos + PlanetDirection(impactAngle) * PlanetRadius * 0.95f;
    for (int i = 0; i < count; i++)
    {
      float side = i % 2 == 0 ? 1f : -1f;
      float hop = 0.25f + 0.12f * (i / 2) + Random.Shared.NextSingle() * 0.15f;
      var bomblet = new PlanetShot
      {
        Kind = PlanetShotKind.Drone,
        Mini = true,
        Delay = i * 0.05f,
        Duration = 0.3f + hop * 0.4f,
        Damage = damage,
        FirePower = shot.FirePower,
      };
      SetQuadraticPath(bomblet, start, PlanetPos + PlanetDirection(impactAngle + side * hop * 0.5f) * PlanetRadius * (1.3f + hop),
        PlanetPos + PlanetDirection(impactAngle + side * hop) * PlanetRadius * 0.9f);
      AddPlanetShot(bomblet);
    }
  }
}
