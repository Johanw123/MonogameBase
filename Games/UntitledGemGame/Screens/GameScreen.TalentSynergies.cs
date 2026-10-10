using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// The newer combo talents (rules and tuning in PrestigeTalentEffects; the older combos
// are in GameScreen.TalentCombos.cs):
//  - surges: Overdrive Protocol (the Overdrive command) and Shard Reactor's overcharge
//    double every weapon's fire rate while they last; Overclock doubles it for good;
//  - Sympathetic Fire: laser hits launch rockets and fire bonus Railgun rounds;
//  - Kinetic Harvest: gems collected by hand charge the Railgun;
//  - Cargo Catapult: fleet deliveries load slugs that hit as cannon shots;
//  - Fault Seeding: Genesis Pulses seed weak points that the next nearby hit bursts (the
//    Fault Scan command marks stronger ones);
//  - Drill Spotter: weapons aim at a boring Core Drill pod and feed it;
//  - heat: Shrapnel Shell and Molten Core leave molten craters, Chain Reaction makes
//    detonations spread;
//  - Resonant Core: the planet echoes a share of the last minute's damage as quakes;
//  - Galvanic Shock: harpoon pulses and arcs can crit, and each critical one adds a
//    stack of shock: the planet takes more damage from every weapon while it lasts.
// The free tier rewards that start each run are granted here too.
public partial class UntitledGemGameGameScreen
{
  private static readonly Color WeakPointColor = new(255, 220, 120);
  private static readonly Color ResonanceColor = new(150, 120, 255);

  private sealed class WeakPoint
  {
    // Age starts below zero for a point still waiting to be revealed (Fault Scan's sweep).
    public float Angle, Age, Lifetime;
    // A burst deals Power times the hit's fire power, or Damage if that is more.
    public float Power;
    public int Damage;
    // Marked by Fault Scan, which draws them as targets and keeps them while it runs.
    public bool Scanned;
  }

  private readonly List<WeakPoint> weakPoints = new();
  private readonly List<WeakPoint> burstingWeakPoints = new();
  private readonly List<(float Angle, float Delay)> pendingChains = new();
  private float shardOvercharge;
  private float lastArsenalSurge = 1f;
  private float sympatheticRocketCooldown;
  private float sympatheticRailCooldown;
  private long catapultLoad;
  private int catapultSlugs;
  private float catapultTimer;
  private float resonanceTimer;
  // When each stack of Galvanic Shock wears off (on shockClock), oldest first.
  private readonly Queue<float> shockStacks = new();
  private float shockClock;
  private float shockCrackleTimer;
  private const float ShockCrackleSeconds = 0.3f;

  private void UpdateTalentSynergies(float dt, PlayAreaBounds bounds)
  {
    UpdateArsenalSurge(dt);
    sympatheticRocketCooldown = Math.Max(0f, sympatheticRocketCooldown - dt);
    sympatheticRailCooldown = Math.Max(0f, sympatheticRailCooldown - dt);
    for (int i = weakPoints.Count - 1; i >= 0; i--)
    {
      var point = weakPoints[i];
      bool hidden = point.Age < 0f;
      if ((point.Age += dt) >= 0f && hidden) WeakPointFlash(point.Angle);
      if (point.Age >= point.Lifetime) weakPoints.RemoveAt(i);
    }
    UpdateChainReactions(dt, bounds);
    UpdateCargoCatapult(dt);
    UpdateResonance(dt);
    UpdatePlanetShock(dt);
    UpdatePrecision(dt);
  }

  private void ClearTalentSynergies()
  {
    weakPoints.Clear();
    pendingChains.Clear();
    shardOvercharge = sympatheticRocketCooldown = sympatheticRailCooldown = catapultTimer = resonanceTimer = 0f;
    shockStacks.Clear();
    shockClock = shockCrackleTimer = 0f;
    ClearPrecision();
    catapultLoad = catapultSlugs = 0;
    lastArsenalSurge = PrestigeTalentEffects.ArsenalSurge = 1f;
  }

  // ---- Galvanic Shock ----

  private int ShockStacks => shockStacks.Count;

  // Harpoon pulses and arcs roll for a crit: Galvanic Shock's own chance plus the crit
  // build's. With Galvanic Shock each crit adds a stack of shock; a full stack trades its
  // oldest charge for the fresh one.
  private int LightningHit(int gems, Vector2 at)
  {
    bool galvanic = PrestigeTalentEffects.LightningCrits;
    float chance = SignalStats.CritChance((galvanic ? PrestigeTalentEffects.LightningCritChance : 0f)
      + PrestigeTalentEffects.WeaponCritChance);
    if (gems <= 0 || chance <= 0f || Random.Shared.NextSingle() >= chance) return gems;
    if (galvanic)
    {
      if (shockStacks.Count == 0 && CombatActive)
        ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 40f), Loc.T("SHOCKED"), large: false);
      if (shockStacks.Count >= PrestigeTalentEffects.MaxShockStacks) shockStacks.Dequeue();
      shockStacks.Enqueue(shockClock + SignalStats.ShockSeconds);
    }
    SpawnerEffects.Add(null, at, Color.Gold, 3f, 34f, 0.25f);
    return CritLanded(CritDamage(gems), at);
  }

  // Stacks wear off on their own; while any hold, lightning crawls over the planet's face,
  // thicker the more stacks there are.
  private void UpdatePlanetShock(float dt)
  {
    if (shockStacks.Count == 0)
    {
      shockClock = 0f;
      return;
    }
    shockClock += dt;
    while (shockStacks.Count > 0 && shockStacks.Peek() <= shockClock) shockStacks.Dequeue();
    if (shockStacks.Count == 0 || (shockCrackleTimer -= dt) > 0f) return;
    shockCrackleTimer = ShockCrackleSeconds / (1f + 4f * shockStacks.Count / PrestigeTalentEffects.MaxShockStacks);
    float angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
    float turn = (0.6f + Random.Shared.NextSingle() * 1.2f) * (Random.Shared.Next(2) == 0 ? -1f : 1f);
    AddArc(PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.85f,
      PlanetPos + PlanetDirection(angle + turn) * PlanetRadius * (0.3f + 0.5f * Random.Shared.NextSingle()));
  }

  // ---- Surges ----

  private void UpdateArsenalSurge(float dt)
  {
    shardOvercharge = Math.Max(0f, shardOvercharge - dt);
    float surge = 1f;
    if (Talents.OverdriveProtocol && ManualAbilities.IsActive(ManualFleetAbilities.OverdriveSlot))
      surge *= PrestigeTalentEffects.SurgeFireRate;
    if (shardOvercharge > 0f)
      surge *= PrestigeTalentEffects.SurgeFireRate;
    if (surge > lastArsenalSurge && CombatActive)
      ShowWorldPopup(HomeBasePos - Vector2.UnitY * 70f, surge >= 4f ? Loc.T("ARSENAL x4") : Loc.T("ARSENAL OVERCHARGED"), large: true);
    lastArsenalSurge = PrestigeTalentEffects.ArsenalSurge = surge;
  }

  // Shard Reactor: collecting a Core Shard overcharges every weapon for a while.
  private void OnCoreShardCollected()
  {
    if (Talents.ShardReactor) shardOvercharge = PrestigeTalentEffects.ShardOverchargeSeconds;
  }

  // ---- Sympathetic Fire and Kinetic Harvest ----

  // Rolled for every point of laser damage, with a cooldown so a strong laser cannot flood the field.
  private void RollSympatheticFire(int beam)
  {
    if (!Talents.SympatheticFire) return;
    var upgrades = UpgradeManager.Instance.UG;
    if (upgrades.RocketPods && sympatheticRocketCooldown <= 0f
      && Random.Shared.NextSingle() < PrestigeTalentEffects.SympatheticRocketChance)
    {
      sympatheticRocketCooldown = PrestigeTalentEffects.SympatheticRocketInterval;
      int firePower = SignalStats.FirePower(MainShipWeapon.Rockets);
      int gems = AutomaticWeaponYield(MainShipWeapons.RocketGems(firePower), arsenal: false);
      if (gems > 0) LaunchPlanetShot(PlanetShotKind.Rocket, LaserMount(), LaserContact(beam), gems, firePower);
    }
    if (upgrades.Railgun && sympatheticRailCooldown <= 0f
      && Random.Shared.NextSingle() < PrestigeTalentEffects.SympatheticRailChance)
    {
      sympatheticRailCooldown = PrestigeTalentEffects.SympatheticRailInterval;
      FireRailgun(bonus: true);
    }
  }

  // Kinetic Harvest and Kinetic Battery: called for every gem collected by hand
  // (UpdateSystem.CollectManualGem).
  public void OnGemHandCollected()
  {
    if (!UpgradeManager.Instance.UG.Railgun || !CombatActive) return;
    kineticBattery = PrestigeTalentEffects.KineticBatteryCharge(kineticBattery);
    if (Talents.KineticHarvest && !RailgunWindingUp)
      railgunCharge = Math.Min(RailgunChargeCap, railgunCharge + PrestigeTalentEffects.KineticChargePerGem);
  }

  // ---- Cargo Catapult ----

  private void LoadCargoCatapult(uint cargo)
  {
    if (!CombatActive) return;
    int slugs = PrestigeTalentEffects.CargoCatapultSlugs(ref catapultLoad, cargo);
    catapultSlugs = Math.Min(PrestigeTalentEffects.CargoCatapultMaxQueued, catapultSlugs + slugs);
  }

  // Slugs leave the homebase's nose one at a time and lob high over the field.
  private void UpdateCargoCatapult(float dt)
  {
    catapultTimer = Math.Max(0f, catapultTimer - dt);
    if (catapultSlugs <= 0 || catapultTimer > 0f || !CombatActive || FracturePaused) return;
    catapultSlugs--;
    catapultTimer = PrestigeTalentEffects.CargoCatapultInterval;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = AutomaticWeaponYield((int)Math.Min(int.MaxValue, (long)firePower * PrestigeTalentEffects.CargoCatapultPower),
      arsenal: false);
    if (gems <= 0) return;
    bool critical = RollCriticalShell(ref gems);
    var start = HullMount(0f, -44f);
    var end = AutomaticPlanetTarget(0.55f);
    var travel = end - start;
    var shot = new PlanetShot
    {
      Kind = PlanetShotKind.Cannon,
      Duration = Math.Max(0.1f, travel.Length() / CannonShotSpeed * 1.6f),
      Damage = gems,
      FirePower = firePower,
      Critical = critical,
      Source = PlanetDamageSource.CargoCatapult,
    };
    SetQuadraticPath(shot, start, (start + end) * 0.5f - Vector2.UnitY * travel.Length() * 0.45f, end);
    AddPlanetShot(shot);
  }

  // ---- Weak points (Fault Seeding, Fault Scan) and Drill Spotter ----

  // Weak points sit on the near side, where the weapons hit, within this angle of its middle.
  private const float WeakPointArc = 1.3f;

  // Called for every ship system activation (HomeBase); echoes count as extra casts.
  public void OnShipSystemActivated(IHomeBaseAbility ability, int casts)
  {
    if (ability is not GemSpawnerAbility || !Talents.FaultSeeding || !CombatActive) return;
    for (int i = 0; i < PrestigeTalentEffects.WeakPointsPerPulse * Math.Max(1, casts); i++)
      AddWeakPoint(PlanetFacingAngle() + (Random.Shared.NextSingle() * 2f - 1f) * WeakPointArc,
        PrestigeTalentEffects.WeakPointSeconds, PrestigeTalentEffects.WeakPointPower * SignalStats.WeakPointMultiplier,
        scanned: false);
  }

  // The Fault Scan command: weak points spread evenly over the near side, each nudged a
  // little, revealed in a sweep from one end and kept until the scan ends. Each bursts for
  // a share of the planet's recent damage, so the scan keeps pace with the build; weak
  // points' own damage is left out so scans don't feed each other. Weapons burst the
  // points they hit; clicked shots reach the rest.
  private void ScanFaults()
  {
    if (!CombatActive) return;
    var commands = ManualAbilities;
    var meter = m_gameState.Damage;
    float multiplier = commands.FaultScanMultiplier;
    double recent = Math.Max(0d, meter.TotalPerMinute - meter.PerMinute(PlanetDamageSource.WeakPoints)) / 60d;
    int damage = (int)Math.Min(int.MaxValue, recent * ManualFleetAbilities.FaultScanSeconds * multiplier);
    float power = ManualFleetAbilities.FaultScanPower * multiplier;
    float duration = commands.CastDuration(ManualFleetAbilities.FaultScanSlot);
    int count = ManualFleetAbilities.FaultScanPoints;
    float facing = PlanetFacingAngle(), step = 2f * WeakPointArc / count;
    bool clockwise = Random.Shared.Next(2) == 0;
    for (int i = 0; i < count; i++)
    {
      float delay = ManualFleetAbilities.FaultScanSweepSeconds * (clockwise ? i : count - 1 - i) / (count - 1);
      AddWeakPoint(facing - WeakPointArc + (i + 0.5f + (Random.Shared.NextSingle() - 0.5f) * 0.5f) * step,
        duration - delay, power, scanned: true, damage, delay);
    }
    SpawnerEffects.Add(null, PlanetPos, WeakPointColor, PlanetRadius * 0.3f, PlanetRadius * 1.4f, 0.6f);
  }

  // A full set gives way oldest first, keeping a running scan's points while it can.
  private void AddWeakPoint(float angle, float lifetime, float power, bool scanned, int damage = 0, float delay = 0f)
  {
    if (weakPoints.Count >= PrestigeTalentEffects.MaxWeakPoints)
    {
      int oldest = weakPoints.FindIndex(p => !p.Scanned);
      weakPoints.RemoveAt(Math.Max(0, oldest));
    }
    weakPoints.Add(new WeakPoint
      { Angle = angle, Age = -delay, Lifetime = lifetime, Power = power, Damage = damage, Scanned = scanned });
    if (delay <= 0f) WeakPointFlash(angle);
  }

  private void WeakPointFlash(float angle)
    => SpawnerEffects.Add(null, WeakPointPosition(angle), WeakPointColor, 2f, 22f, 0.4f);

  private Vector2 WeakPointPosition(float angle) => PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.86f;

  // Every weapon hit with a known impact angle (KnockGemsLoose, KnockClusterLoose).
  private void OnPlanetHit(PlanetDamageSource source, float? facing, int damage, int firePower)
  {
    if (facing is not float angle || damage <= 0) return;
    if (weakPoints.Count > 0) BurstWeakPoints(angle, PrestigeTalentEffects.WeakPointReach, firePower);
    FeedSpottedDrills(source, angle, damage);
  }

  // A burst knocks loose its own damage around the weak point, and can set off its neighbours.
  private void BurstWeakPoints(float angle, float reach, int firePower)
  {
    burstingWeakPoints.Clear();
    for (int i = weakPoints.Count - 1; i >= 0; i--)
      if (weakPoints[i].Age >= 0f && AngleBetween(weakPoints[i].Angle, angle) <= reach)
      {
        burstingWeakPoints.Add(weakPoints[i]);
        weakPoints.RemoveAt(i);
      }
    if (burstingWeakPoints.Count == 0) return;
    // Bursts call back into OnPlanetHit, which reuses the list: copy it first.
    var bursting = burstingWeakPoints.ToArray();
    foreach (var point in bursting)
    {
      var at = WeakPointPosition(point.Angle);
      planetExplosions.Add(new PlanetExplosion { Position = at, Scale = 1.3f });
      SpawnerEffects.Add(null, at, WeakPointColor, 4f, 60f, 0.45f);
      PulsePlanet(0.6f, 0.25f);
      double fromHit = Math.Max(1, firePower) * (double)point.Power;
      // A share of recent damage already carries the weapon bonuses, so it lands raw.
      bool raw = point.Damage > fromHit;
      int gems = (int)Math.Min(int.MaxValue, raw ? point.Damage : fromHit);
      KnockGemsLoose(PlanetDamageSource.WeakPoints, gems, firePower, weaponBounds, 1.2f, point.Angle, 0.6f, raw: raw);
    }
    if (bursting.Length >= 2)
      ShowWorldPopup(WeakPointPosition(angle) + PlanetDirection(angle) * 40f, Loc.F("WEAK POINTS x{0}", bursting.Length), large: false);
  }

  // Quakes (Tectonic Shockwave, Planetary Overload, Resonant Core) burst every weak point.
  private void BurstEveryWeakPoint(int firePower)
  {
    if (weakPoints.Count > 0) BurstWeakPoints(0f, MathHelper.Pi + 0.01f, firePower);
  }

  // Drill Spotter: the first pod that is boring, where automatic weapons aim.
  private Vector2? SpottedBoreHole()
  {
    if (!Talents.DrillSpotter) return null;
    foreach (var pod in drillPods)
      if (pod.Landed && !pod.Done)
        return PlanetPos + PlanetDirection(pod.BoreAngle) * PlanetRadius * 0.9f;
    return null;
  }

  private void FeedSpottedDrills(PlanetDamageSource source, float angle, int damage)
  {
    if (!Talents.DrillSpotter || source == PlanetDamageSource.CoreDrill) return;
    foreach (var pod in drillPods)
      if (pod.Landed && !pod.Done && AngleBetween(pod.BoreAngle, angle) <= PrestigeTalentEffects.DrillSpotterReach)
      {
        pod.Drilled = (int)Math.Min(int.MaxValue, pod.Drilled + (long)(damage * PrestigeTalentEffects.DrillSpotterShare));
        return;
      }
  }

  // ---- Heat: Shrapnel Shell, Molten Core, Chain Reaction ----

  // A crater whose budget deals this much damage over its life.
  private void AddCraterDealing(Vector2 hit, double damage, int firePower, float size)
    => AddCrater(hit, (int)Math.Clamp(damage / PrestigeTalentEffects.CraterShare, 1, int.MaxValue), firePower, size);

  // Shrapnel Shell: the shell's plates crash back into the planet as it bursts.
  private void ShrapnelShellCraters(IReadOnlyList<Vector2> plates)
  {
    if (!Talents.ShrapnelShell || plates.Count == 0) return;
    double each = PlanetShell.Health * PrestigeTalentEffects.ShrapnelCraterShare / plates.Count;
    foreach (var plate in plates)
      AddCraterDealing(plate, each, StrongestFirePower(), 6f);
  }

  // Molten Core: a fracture's crack fills with craters around where it burst open.
  private void PourMoltenCore(long eruption, int firePower)
  {
    if (!Talents.MoltenCore || eruption <= 0) return;
    var focus = CrackFocusWorld() - PlanetPos;
    float angle = focus.LengthSquared() > 1f ? MathF.Atan2(focus.Y, focus.X) : PlanetFacingAngle();
    double each = eruption * (double)PrestigeTalentEffects.MoltenCoreShare / PrestigeTalentEffects.MoltenCoreCraters;
    for (int i = 0; i < PrestigeTalentEffects.MoltenCoreCraters; i++)
    {
      float along = (i - (PrestigeTalentEffects.MoltenCoreCraters - 1) / 2f) * 0.16f;
      float depth = 0.35f + 0.5f * Random.Shared.NextSingle();
      AddCraterDealing(PlanetPos + PlanetDirection(angle + along) * PlanetRadius * depth, each, firePower, 7f);
    }
  }

  // Chain Reaction: each burst spot sets off the spots around it a moment later.
  private void QueueChainReaction(float angle)
  {
    if (Talents.ChainReaction) pendingChains.Add((angle, PrestigeTalentEffects.ChainDelay));
  }

  private void UpdateChainReactions(float dt, PlayAreaBounds bounds)
  {
    for (int i = pendingChains.Count - 1; i >= 0; i--)
    {
      var (angle, delay) = pendingChains[i];
      delay -= dt;
      if (delay > 0f)
      {
        pendingChains[i] = (angle, delay);
        continue;
      }
      pendingChains.RemoveAt(i);
      // Spots are used up as they burst, so the ripple always dies out. New links are
      // appended past i and wait for the next frame.
      DetonateMolten(angle, PrestigeTalentEffects.ChainRadius, bounds, force: true);
    }
  }

  // ---- Resonant Core ----

  private void UpdateResonance(float dt)
  {
    if (!Talents.ResonantCore || !CombatActive || PlanetShelled || FracturePaused)
    {
      resonanceTimer = 0f;
      return;
    }
    resonanceTimer += dt;
    if (resonanceTimer < PrestigeTalentEffects.ResonanceSeconds) return;
    resonanceTimer -= PrestigeTalentEffects.ResonanceSeconds;
    int gems = PrestigeTalentEffects.ResonanceGems(DamagePerMinute);
    if (gems <= 0) return;
    StartShockwave(PlanetDamageSource.ResonantCore, Random.Shared.NextSingle() * MathHelper.TwoPi, gems,
      StrongestFirePower());
    SpawnerEffects.Add(null, PlanetPos, ResonanceColor, PlanetRadius, PlanetRadius * 3.2f, 0.9f);
    ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 60f), Loc.T("RESONANCE"), large: false);
  }

  // ---- Drawing ----

  // Weak points pulse gold on the planet's face. Called inside the additive weapon pass.
  private void DrawWeakPoints(float feather)
  {
    foreach (var point in weakPoints)
    {
      if (point.Age < 0f) continue;
      var at = WeakPointPosition(point.Angle);
      float life = 1f - point.Age / point.Lifetime;
      float pulse = 0.6f + 0.4f * MathF.Sin(point.Age * 7f + point.Angle * 5f);
      // Blinks faster as it is about to close.
      if (life < 0.25f) pulse *= 0.5f + 0.5f * MathF.Sin(point.Age * 30f);
      m_shapeBatch.FillCircle(at, 4f + 2f * pulse, WeakPointColor * (0.8f * pulse), Math.Max(feather, 3f));
      m_shapeBatch.BorderCircle(at, 9f + 3f * pulse, WeakPointColor * (0.55f * pulse), 1.5f, Math.Max(feather, 2f));
      if (!point.Scanned) continue;
      // Fault Scan's points read as targets: a wider ring with four ticks.
      float ring = 18f + 2f * pulse;
      m_shapeBatch.BorderCircle(at, ring, WeakPointColor * (0.6f * pulse), 1.5f, Math.Max(feather, 2f));
      for (int tick = 0; tick < 4; tick++)
      {
        var direction = PlanetDirection(tick * MathHelper.PiOver2 + MathHelper.PiOver4);
        m_shapeBatch.FillLine(at + direction * (ring + 3f), at + direction * (ring + 9f), 1f,
          WeakPointColor * (0.6f * pulse), Math.Max(feather, 1.5f));
      }
    }
  }

  // ---- Free tier rewards ----

  // A running start for the run about to begin (free rewards in PrestigeTalentLayout).
  public void GrantRunStartRewards()
  {
    var meta = UpgradeManager.Instance.UGM;
    if (meta.FreeSpareCells) m_gameState.TryRefundAbilityPoints(PrestigeTalentEffects.SpareCells);
    if (meta.FreeHeadStart)
      m_gameState.CurrentRedGemCount = PrestigeProgression.AddSaturating(m_gameState.CurrentRedGemCount,
        PrestigeTalentEffects.HeadStartGems);
    if (meta.FreeShardCache)
      m_gameState.CurrentCoreShardCount = PrestigeProgression.AddSaturating(m_gameState.CurrentCoreShardCount,
        PrestigeTalentEffects.ShardCache);
    if (meta.FreeCoreMemory) m_gameState.ShellDamage = PlanetShell.Health;
    m_upgradeManager.UpdateTooltipContent();
  }
}
