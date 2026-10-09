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
//  - Jackpot: a rare crit hits a hundred times as hard and shakes the planet;
//  - Critical Cascade (a cannon core shard): a critical shell fires another sure crit.
// Cannon shells roll when fired (RollCriticalShell) and land here through CritLanded;
// rockets, Railgun rounds and the laser roll on impact (RollCrit); harpoon pulses and arcs
// roll in LightningHit.
public partial class UntitledGemGameGameScreen
{
  private const float CritPopupSeconds = 0.25f;
  private const float CascadeSpread = 0.35f;

  // When each Hot Streak stack wears off (on hotStreakClock), oldest first.
  private readonly Queue<float> hotStreak = new();
  private float hotStreakClock;
  private readonly Queue<Vector2> criticalMassTargets = new();
  private float criticalMassTimer;
  private float critPopupCooldown;

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

  // Every crit lands here with its critical damage: Hot Streak, Critical Mass and Jackpot
  // answer it.
  private int CritLanded(long damage, Vector2 at)
  {
    var meta = UpgradeManager.Instance.UGM;
    if (meta.HotStreak)
    {
      if (hotStreak.Count >= PrestigeTalentEffects.HotStreakMaxStacks) hotStreak.Dequeue();
      hotStreak.Enqueue(hotStreakClock + PrestigeTalentEffects.HotStreakSeconds);
      PrestigeTalentEffects.HotStreakStacks = hotStreak.Count;
    }
    if (meta.CriticalMass && CombatActive && criticalMassTargets.Count < PrestigeTalentEffects.CriticalMassMaxQueued)
      criticalMassTargets.Enqueue(at);
    if (PrestigeTalentEffects.Jackpots && Random.Shared.NextSingle() < PrestigeTalentEffects.JackpotChance)
    {
      damage = (long)Math.Min(long.MaxValue, damage * (double)PrestigeTalentEffects.JackpotMultiplier / SignalStats.CritMultiplier);
      JackpotEffects(at);
    }
    return (int)Math.Min(int.MaxValue, damage);
  }

  private void JackpotEffects(Vector2 at)
  {
    PulsePlanet(1f, 1f);
    planetShake = Math.Max(planetShake, 1f);
    planetExplosions.Add(new PlanetExplosion { Position = at, Scale = 3f });
    SpawnerEffects.Add(null, at, Color.Gold, 10f, 180f, 0.7f);
    SpawnerEffects.Add(null, PlanetPos, Color.Gold, PlanetRadius, PlanetRadius * 2.4f, 0.8f);
    if (CombatActive)
      ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 70f), Loc.T("JACKPOT!"), large: true);
  }

  // Crits can come dozens a second: their popups take turns.
  private bool TakeCritPopup()
  {
    if (critPopupCooldown > 0f) return false;
    critPopupCooldown = CritPopupSeconds;
    return true;
  }

  private void UpdatePrecision(float dt)
  {
    critPopupCooldown = Math.Max(0f, critPopupCooldown - dt);
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

  // Critical Cascade: a critical cannon shell fires another that is sure to crit.
  private void CascadeShell(PlanetShot shot, float impactAngle)
  {
    if (!UpgradeManager.Instance.UG.CannonCascade || shot.Cascade >= MainShipWeapons.CascadeShells) return;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = (int)Math.Min(int.MaxValue, CritDamage(AutomaticWeaponYield(firePower, arsenal: false)));
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
    hotStreakClock = criticalMassTimer = critPopupCooldown = 0f;
    PrestigeTalentEffects.HotStreakStacks = 0;
  }
}
