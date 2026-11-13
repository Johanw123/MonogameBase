using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// The crit build (rules and tuning in PrestigeTalentEffects):
//  - Deadeye: every weapon hit can crit; Critical Shells, Galvanic Shock and Hot Streak add
//    to the chance, and a crit multiplies its damage (more with Executioner);
//  - Hot Streak: each crit raises every weapon's crit chance for a while;
//  - Critical Mass: each crit queues a free plasma shell at the spot it hit, and those
//    shells roll crits of their own;
//  - Supercritical: a full Hot Streak sets every weapon off, the cannon volley all crits,
//    and the streak starts over;
//  - Critical Cascade (a cannon core shard): a critical shell fires another sure crit.
// Cannon shells roll when fired (RollCriticalShell) and land here through CritLanded;
// rockets, Railgun rounds and the laser roll on impact (RollCrit); harpoon pulses and arcs
// roll in LightningHit.
public partial class UntitledGemGameGameScreen
{
  private const float CascadeSpread = 0.35f;

  // When each Hot Streak stack wears off (on hotStreakClock), oldest first.
  private readonly Queue<float> hotStreak = new();
  private float hotStreakClock;
  private readonly Queue<Vector2> criticalMassTargets = new();
  private float criticalMassTimer;
  private bool supercriticalPending;

  // A weapon hit without a crit chance of its own.
  private int RollCrit(int gems, Vector2 at)
  {
    float chance = SignalStats.CritChance(PrestigeTalentEffects.WeaponCritChance);
    if (gems <= 0 || chance <= 0f || Random.Shared.NextSingle() >= chance) return gems;
    SpawnerEffects.Add(null, at, Color.Gold, 3f, 30f, 0.25f);
    return CritLanded(CritDamage(gems), at);
  }

  // A hit's damage when it crits (Executioner, Lethal Margin).
  private static long CritDamage(long gems) => (long)Math.Min(long.MaxValue, gems * (double)SignalStats.CritMultiplier);

  // Every crit lands here with its critical damage: Hot Streak and Critical Mass answer it.
  private int CritLanded(long damage, Vector2 at)
  {
    var meta = UpgradeManager.Instance.UGM;
    if (meta.HotStreak)
    {
      if (hotStreak.Count >= PrestigeTalentEffects.HotStreakMaxStacks) hotStreak.Dequeue();
      hotStreak.Enqueue(hotStreakClock + PrestigeTalentEffects.HotStreakSeconds);
      PrestigeTalentEffects.HotStreakStacks = hotStreak.Count;
      // The burst waits for the update: crits land from inside the weapon loops.
      if (PrestigeTalentEffects.SupercriticalReady(hotStreak.Count)) supercriticalPending = true;
    }
    if (meta.CriticalMass && CombatActive && criticalMassTargets.Count < PrestigeTalentEffects.CriticalMassMaxQueued)
      criticalMassTargets.Enqueue(at);
    return (int)Math.Min(int.MaxValue, damage);
  }

  private void UpdatePrecision(float dt)
  {
    if (supercriticalPending)
    {
      supercriticalPending = false;
      if (CombatActive && PrestigeTalentEffects.SupercriticalReady(hotStreak.Count)) SupercriticalBurst();
    }
    if (hotStreak.Count == 0) hotStreakClock = 0f;
    else
    {
      hotStreakClock += dt;
      while (hotStreak.Count > 0 && hotStreak.Peek() <= hotStreakClock) hotStreak.Dequeue();
    }
    PrestigeTalentEffects.HotStreakStacks = hotStreak.Count;

    // Critical Mass: queued shells fly at a steady clip.
    criticalMassTimer = Math.Max(0f, criticalMassTimer - dt);
    while (criticalMassTargets.Count > 0 && criticalMassTimer <= 0f && CombatActive)
    {
      criticalMassTimer += 1f / PrestigeTalentEffects.CriticalMassShellsPerSecond;
      FireCriticalMassShell(criticalMassTargets.Dequeue());
    }
  }

  private void FireCriticalMassShell(Vector2 target)
  {
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = AutomaticWeaponYield(firePower, arsenal: false);
    if (gems > 0) LaunchCannonShot(PlanetShotKind.Cannon, CannonMount(), target, gems, firePower);
  }

  // Supercritical: the streak discharges into every weapon at once. The cannon's volley is
  // sure crits, so the next streak starts as the shells land.
  private void SupercriticalBurst()
  {
    hotStreak.Clear();
    hotStreakClock = 0f;
    PrestigeTalentEffects.HotStreakStacks = 0;
    AudioManager.Instance.PlaySound(SoundEvent.SupercriticalBurst);
    PulsePlanet(1f, 0.8f);
    planetShake = Math.Max(planetShake, 0.6f);
    SpawnerEffects.Add(null, PlanetPos, Color.Gold, PlanetRadius, PlanetRadius * 2.4f, 0.8f);
    SpawnerEffects.Add(null, HomeBasePos, Color.White, 10f, 120f, 0.5f);
    ShowWorldPopup(HomeBasePos - Vector2.UnitY * 70f, Loc.T("SUPERCRITICAL!"), large: true);
    FireAllWeapons(true, 0, arsenal: false);
    var upgrades = UpgradeManager.Instance.UG;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    for (int i = 0; i < PrestigeTalentEffects.SupercriticalShells; i++)
    {
      int gems = (int)Math.Min(int.MaxValue, AutomaticWeaponYield(firePower, arsenal: false)
        * (double)(SignalStats.CritMultiplier + upgrades.CannonCritDamage));
      if (gems <= 0) break;
      LaunchPlanetShot(PlanetShotKind.Cannon, CannonMount(), AutomaticPlanetTarget(0.9f), gems, firePower, critical: true);
    }
  }

  // Critical Cascade: a critical cannon shell fires another that is sure to crit.
  private void CascadeShell(PlanetShot shot, float impactAngle)
  {
    if (!UpgradeManager.Instance.UG.CannonCascade || shot.Cascade >= MainShipWeapons.CascadeShells) return;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = (int)Math.Min(int.MaxValue, AutomaticWeaponYield(firePower, arsenal: false) * (double)(SignalStats.CritMultiplier + UpgradeManager.Instance.UG.CannonCritDamage));
    if (gems <= 0) return;
    float angle = impactAngle + (Random.Shared.NextSingle() * 2f - 1f) * CascadeSpread;
    LaunchPlanetShot(PlanetShotKind.Cannon, CannonMount(), PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.9f,
      gems, firePower, critical: true);
    // Critical shells never merge, so the new shell is the last one in flight.
    planetShots[^1].Cascade = shot.Cascade + 1;
  }

  private void ClearPrecision()
  {
    hotStreak.Clear();
    criticalMassTargets.Clear();
    hotStreakClock = criticalMassTimer = 0f;
    supercriticalPending = false;
    PrestigeTalentEffects.HotStreakStacks = 0;
  }
}
