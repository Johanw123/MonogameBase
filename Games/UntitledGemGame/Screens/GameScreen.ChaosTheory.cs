using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Chaos Theory (tuning in PrestigeTalentEffects): every so often one weapon you own goes
// chaotic for a spell and borrows another weapon, owned or not. Each of the chaotic
// weapon's hits charges the spell with its damage, and the charge launches again as the
// borrowed weapon's hits at the spot the chaotic weapon last struck: cannon shells,
// rockets, flak and Railgun rounds fly from their mounts and land with every special of
// their kind; a borrowed laser leaves magma scars, a borrowed harpoon pulses lightning.
// Borrowed hits come from a different weapon than the chaotic one, so they never feed
// the spell themselves.
public partial class UntitledGemGameGameScreen
{
  private float chaosTimer = PrestigeTalentEffects.ChaosIntervalSeconds;
  private float chaosRemaining;
  private float chaosLaunchTimer;
  private MainShipWeapon chaosWeapon, chaosBorrowed;
  // Damage of the chaotic weapon's hits waiting to land again, and where it last hit.
  private double chaosCharge;
  private float chaosAngle;
  private int chaosFirePower;
  private int chaosRocketTube;

  private void UpdateChaosTheory(float dt, PlayAreaBounds bounds)
  {
    if (!Talents.ChaosTheory)
    {
      chaosRemaining = 0f;
      chaosCharge = 0;
      return;
    }
    if (!CombatActive) return;
    if (chaosRemaining > 0f)
    {
      chaosRemaining -= dt;
      chaosLaunchTimer = Math.Max(0f, chaosLaunchTimer - dt);
      if (chaosLaunchTimer <= 0f && chaosCharge >= 1)
      {
        chaosLaunchTimer = PrestigeTalentEffects.ChaosLaunchInterval;
        int damage = (int)Math.Min(int.MaxValue, chaosCharge);
        chaosCharge = 0;
        LaunchBorrowedHit(chaosBorrowed, chaosAngle, damage, chaosFirePower, bounds);
      }
      if (chaosRemaining <= 0f)
      {
        chaosCharge = 0;
        chaosTimer = PrestigeTalentEffects.ChaosIntervalSeconds;
      }
      return;
    }
    chaosTimer -= dt;
    if (chaosTimer <= 0f) StartChaos();
  }

  private void StartChaos()
  {
    (chaosWeapon, chaosBorrowed) = PrestigeTalentEffects.ChaosPick(UpgradeManager.Instance.UG, Random.Shared);
    chaosRemaining = PrestigeTalentEffects.ChaosSeconds;
    chaosLaunchTimer = 0f;
    chaosCharge = 0;
    chaosFirePower = SignalStats.FirePower(chaosWeapon);
    chaosAngle = PlanetFacingAngle();
    AudioManager.Instance.PlaySound(SoundEvent.ChaosTheoryStart);
    SpawnerEffects.Add(null, HomeBasePos, Color.Violet, 10f, 140f, 0.6f);
    ShowWorldPopup(HomeBasePos - Vector2.UnitY * 70f,
      Loc.F("CHAOS: {0} AS {1}", Loc.T(Weaponry.ForWeapon(chaosWeapon).Name), Loc.T(Weaponry.ForWeapon(chaosBorrowed).Name)),
      large: true);
  }

  // Every planet hit passes through here (OnPlanetHit): the chaotic weapon's charge the spell.
  private void FeedChaos(PlanetDamageSource source, float angle, int damage, int firePower)
  {
    if (chaosRemaining <= 0f || PrestigeTalentEffects.ChaosWeaponOf(source) != chaosWeapon) return;
    chaosCharge = Math.Min(PrestigeTalentEffects.ChaosMaxCharge, chaosCharge + damage);
    chaosAngle = angle;
    chaosFirePower = firePower;
  }

  // The borrowed weapon's hit, carrying the chaotic weapon's damage to where it last struck.
  private void LaunchBorrowedHit(MainShipWeapon weapon, float angle, int damage, int firePower, PlayAreaBounds bounds)
  {
    var target = PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.9f;
    switch (weapon)
    {
      case MainShipWeapon.Cannon:
        AudioManager.Instance.PlaySound(SoundEvent.CannonFireAuto);
        LaunchCannonShot(PlanetShotKind.Cannon, target, damage, firePower);
        break;
      case MainShipWeapon.Rockets:
        AudioManager.Instance.PlaySound(SoundEvent.RocketLaunch);
        LaunchPlanetShot(PlanetShotKind.Rocket, RocketMount(chaosRocketTube++), target, damage, firePower);
        break;
      case MainShipWeapon.Railgun:
        AudioManager.Instance.PlaySound(SoundEvent.RailgunFire);
        railgunAim = RailgunAimAt(target);
        LaunchPlanetShot(PlanetShotKind.Rail, RailgunMuzzlePoint(), target, damage, firePower);
        break;
      case MainShipWeapon.Flak:
        AudioManager.Instance.PlaySound(SoundEvent.FlakFire);
        LaunchPlanetShot(PlanetShotKind.Flak, FlakMount(target), target, damage, firePower);
        break;
      case MainShipWeapon.Laser:
        // A scar that oozes the damage over its life, as the beam's own scars do.
        if (magmaScars.Count >= MagmaScarCap) magmaScars.RemoveAt(0);
        magmaScars.Add(new MagmaScar
        {
          Normal = WorldToPlanetNormal(target),
          Angle = angle,
          Budget = damage * PrestigeTalentEffects.MoltenBurn,
          FirePower = firePower,
          Value = MainShipWeapons.LaserGemValue(UpgradeManager.Instance.UG),
        });
        SpawnerEffects.Add(null, target, MagmaColor, 3f, 26f, 0.3f);
        break;
      case MainShipWeapon.Harpoon:
        AudioManager.Instance.PlaySound(SoundEvent.ArcHarpoonPulse);
        AddArc(HarpoonMount(target), target);
        KnockGemsLoose(PlanetDamageSource.ArcHarpoon, LightningHit(damage, target), firePower, bounds, 0.78f, angle, 0.5f);
        MoltenArcs(target, damage, firePower, bounds);
        PulsePlanet(0.35f, 0.08f);
        break;
    }
  }

  private void ClearChaosTheory()
  {
    chaosTimer = PrestigeTalentEffects.ChaosIntervalSeconds;
    chaosRemaining = chaosLaunchTimer = 0f;
    chaosCharge = 0;
    chaosRocketTube = 0;
  }
}
