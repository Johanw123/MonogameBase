using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Main ship weapons (tuning in MainShipWeapons), plus the Planet Cracker command's
// beam. Every gem in the field is
// knocked loose by one; each weapon has its own Fire Rate and Fire Power, and
// hits in its own way:
//  - Cannon: fires when the player clicks the planet or the homebase; Auto
//    Cannon makes it fire on its own. Gems scatter, or land as one cluster per
//    shot with Shatter Shells.
//  - Mining laser: a sweeping beam; a steady trickle of gems lands close by.
//  - Rocket pods: salvos; every rocket blasts off one cluster.
//  - Railgun (GameScreen.Railgun.cs): a turret that winds up and fires one
//    hypersonic round; its impact splits into fragments that land as clusters
//    across the whole field.
// Special upgrades that change how each weapon hits live in GameScreen.WeaponSpecials.cs.
public partial class UntitledGemGameGameScreen
{
  private const float CannonShotSpeed = 1500f;
  private const float ManualShotSpeed = 2600f;
  private const float RocketFlightSeconds = 0.85f;
  private const float RocketStaggerSeconds = 0.12f;
  private const float LaserReach = 0.45f;
  private const int MaxPlanetShots = 48;
  private const int MaxLaserGemsPerFrame = 64;

  // Projectile sprite strips (frame size, frame count) and the explosion frames used.
  private const int CannonFrameSize = 32, CannonFrames = 4;
  private const int RocketFrameWidth = 9, RocketFrameHeight = 16, RocketFrames = 4;
  // The asteroid strip's first three frames show the rock cracking; use only its burst.
  private const int ExplosionFrameSize = 96, ExplosionFirstFrame = 3, ExplosionFrames = 5;
  private const float ExplosionFrameSeconds = 0.06f;

  private static readonly Color CannonGlow = new(90, 210, 255);
  private static readonly Color ManualGlow = new(255, 200, 90);
  private static readonly Color LaserGlow = new(255, 170, 60);
  // The mining laser's beam (LaserBeam.fx): its body and its emitter/impact flares.
  private static readonly Color LaserBeamColor = new(255, 95, 35);
  private static readonly Color LaserFlareColor = new(255, 190, 110);
  private static readonly Color LaserHotBeamColor = new(225, 135, 70);
  private static readonly Color LaserHotFlareColor = new(240, 180, 120);
  private static readonly Color LaserSurgeBeamColor = new(230, 145, 80);
  private static readonly Color LaserSurgeFlareColor = new(245, 190, 135);
  private static readonly Color CrackerBeamColor = new(110, 215, 255);
  private static readonly Color CrackerFlareColor = new(215, 245, 255);
  private const int MaxCrackerGemsPerFrame = 96;

  private enum PlanetShotKind { Cannon, Manual, Harpoon, Rocket, Rail, Drone }
  private const float HarpoonShotSpeed = 900f;

  private sealed class PlanetShot
  {
    public PlanetShotKind Kind;
    // A cubic Bezier path; straight and simple arcing shots convert a quadratic curve.
    public Vector2 Start, Control1, Control2, End;
    public float Delay, Age, Duration;
    // The damage this shot deals (one gem per point while the field has room), and the
    // fire power that sets the gems' colors and reach.
    public int Damage, FirePower;
    // Field room held for this shot while it flies, so other weapons do not overfill it.
    public int Reserved;
    // Specials: a critical cannon shot, how often a ricochet has bounced, a Cluster
    // Warhead mini-rocket, a rocket that has split, an Orbital Strike rocket.
    public bool Critical, Mini, Split, FarSide;
    // Kamikaze Wing: the wing's last bomber with Doomsday Drone (a bomblet is Mini).
    public bool Doomsday;
    public int Bounces;
  }

  private sealed class PlanetExplosion
  {
    public Vector2 Position;
    public float Age, Scale;
  }

  private readonly List<PlanetShot> planetShots = new();
  private readonly List<PlanetExplosion> planetExplosions = new();
  private int pendingPlanetGems;
  private bool cannonLowerTurret;
  private float laserCarry;
  private float laserTime;
  private int laserNextBeam;
  private float rocketTimer;
  private SdfLineRenderer laserRenderer;
  private float crackerCarry;
  private Vector2 paintedPlanetTarget;
  private float paintedTargetRemaining;
  private int constellationRockets;
  private int constellationPayload;
  private int constellationFirePower;
  private float constellationAge;

  private bool PaintedTargetActive => UpgradeManager.Instance?.UGM.TargetPainter == true
    && paintedTargetRemaining > 0f;

  private Vector2 AutomaticPlanetTarget(float halfSpread)
    => PaintedTargetActive ? paintedPlanetTarget : PlanetFacingPoint(halfSpread);

  private int AutomaticWeaponCount
  {
    get
    {
      var upgrades = UpgradeManager.Instance.UG;
      return (upgrades.AutoCannon ? 1 : 0) + (upgrades.MiningLaser ? 1 : 0)
        + (upgrades.ArcHarpoon ? 1 : 0) + (upgrades.RocketPods ? 1 : 0)
        + (upgrades.Railgun ? 1 : 0);
    }
  }

  private int AutomaticWeaponYield(int gems)
    => PrestigeTalentEffects.CombinedArmsYield(
      PrestigeTalentEffects.PaintedYield(gems, PaintedTargetActive), AutomaticWeaponCount);

  public void ChargeWeaponsFromCargo(uint cargo)
  {
    float charge = PrestigeTalentEffects.CargoCatapultCharge(cargo);
    if (charge <= 0f) return;
    var upgrades = UpgradeManager.Instance.UG;
    if (upgrades.AutoCannon) spawnTimer += charge;
    if (upgrades.ArcHarpoon && !harpoonEmbedded && !harpoonInFlight) harpoonReload += charge;
    if (upgrades.RocketPods) rocketTimer += charge;
    if (upgrades.Railgun)
      railgunCharge = Math.Min(1f, railgunCharge + charge / RailgunChargeTime());
    if (upgrades.MiningLaser)
      laserCarry = Math.Min(MaxLaserGemsPerFrame, laserCarry + charge
        * (float)MainShipWeapons.LaserGemRate(SignalStats.FireRate(MainShipWeapon.Laser),
          SignalStats.FirePower(MainShipWeapon.Laser)));
  }

  // Mount points in homebase hull texels, measured from the hull's centre: the hull
  // faces up, spans about x -31..31 and y -49..34, and the planet is to its right.
  private float HullScale()
    => m_homeBaseEntity.Get<Transform2>().Scale.X
      * BaseStats.GetHarvesterCollectionRangeMultiplier(m_homeBaseEntity.Get<Harvester>());

  private Vector2 HullMount(float x, float y) => HomeBasePos + new Vector2(x, y) * HullScale();

  private bool IsOnHomeBase(Vector2 position)
    => Vector2.DistanceSquared(position, HomeBasePos) <= MathF.Pow(48f * HullScale(), 2);

  // Cannon turrets sit on the right wing; the laser emitter tops the central spine
  // between the prongs (Twin Lasers fan out from it in a V); rockets launch from the
  // lower wing; the railgun turret sits on the right flank (GameScreen.Railgun.cs).
  private Vector2 CannonMount() => HullMount(26f, (cannonLowerTurret = !cannonLowerTurret) ? 22f : 6f);
  private Vector2 LaserMount() => HullMount(0f, -30f);
  private Vector2 RocketMount(int index) => HullMount(24f + index % 2 * 4f, 26f + index % 3 * 3f);

  // Auto Cannon fires on the cannon's timer (GameScreen.Update).
  private void FirePlanetCannon(int shotsOwed, int firePower)
  {
    int gems = AutomaticWeaponYield((int)Math.Min(int.MaxValue, (long)shotsOwed * firePower));
    if (gems <= 0) return;
    // Low frame rates or high fire rates can owe many shots at once; merge them
    // into a few heavier shots instead of a wall of projectiles.
    int shots = Math.Clamp(shotsOwed, 1, 4);
    for (int i = 0; i < shots; i++)
    {
      int share = gems / shots + (i < gems % shots ? 1 : 0);
      if (share > 0)
        LaunchCannonShot(PlanetShotKind.Cannon, AutomaticPlanetTarget(0.55f), share, firePower);
    }
  }

  private void FireManualShot(Vector2 target)
  {
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    LaunchCannonShot(PlanetShotKind.Manual, target, firePower, firePower);
  }

  private void LaunchCannonShot(PlanetShotKind kind, Vector2 target, int gems, int firePower)
    => LaunchCannonShot(kind, CannonMount(), target, gems, firePower);

  private void LaunchCannonShot(PlanetShotKind kind, Vector2 start, Vector2 target, int gems, int firePower)
  {
    bool critical = RollCriticalShell(ref gems);
    LaunchPlanetShot(kind, start, target, gems, firePower, critical: critical);
  }

  private void FireRocketSalvo()
  {
    var upgrades = UpgradeManager.Instance.UG;
    int firePower = SignalStats.FirePower(MainShipWeapon.Rockets);
    if (UpgradeManager.Instance.UGM.ProjectConstellation)
    {
      int rockets = MainShipWeapons.RocketsPerSalvo(upgrades);
      for (int i = 0; i < rockets; i++)
        if (!StoreConstellationRocket(firePower, upgrades)) break;
      return;
    }
    for (int i = 0; i < MainShipWeapons.RocketsPerSalvo(upgrades); i++)
    {
      if (upgrades.RocketOrbitalStrike)
      {
        int orbital = AutomaticWeaponYield((int)MathF.Ceiling(firePower * MainShipWeapons.OrbitalStrikeBonus));
        LaunchOrbitalRocket(RocketMount(i), orbital, firePower, i, i * RocketStaggerSeconds);
        continue;
      }
      int gems = AutomaticWeaponYield(firePower);
      LaunchPlanetShot(PlanetShotKind.Rocket, RocketMount(i), AutomaticPlanetTarget(1.2f), gems, firePower,
        delay: i * RocketStaggerSeconds);
    }
  }

  // Project Constellation: a rocket waits in orbit instead of striking.
  private bool StoreConstellationRocket(int firePower, UpgradesGeneratorUpgrades upgrades)
  {
    if (constellationRockets >= PrestigeTalentEffects.ConstellationRocketLimit)
    {
      ReleaseConstellation(AutomaticPlanetTarget(1.2f));
      if (constellationRockets >= PrestigeTalentEffects.ConstellationRocketLimit) return false;
    }
    int payload = upgrades.RocketOrbitalStrike
      ? (int)MathF.Ceiling(firePower * MainShipWeapons.OrbitalStrikeBonus) : firePower;
    constellationPayload = (int)Math.Min(int.MaxValue,
      (long)constellationPayload + AutomaticWeaponYield(payload));
    constellationFirePower = Math.Max(constellationFirePower, firePower);
    ++constellationRockets;
    constellationAge = 0f;
    return true;
  }

  private void UpdateConstellation(float dt)
  {
    if (constellationRockets <= 0) return;
    constellationAge += dt;
    if (!UpgradeManager.Instance.UGM.ProjectConstellation
      || constellationAge >= PrestigeTalentEffects.ConstellationAutoLaunchSeconds)
      ReleaseConstellation(AutomaticPlanetTarget(1.2f));
  }

  private void ReleaseConstellation(Vector2 target)
  {
    if (constellationRockets <= 0) return;
    int gems = PrestigeTalentEffects.ConstellationPayload(constellationPayload);
    if (gems <= 0) return;
    int shots = Math.Min(8, constellationRockets);
    for (int i = 0; i < shots; i++)
    {
      int share = gems / shots + (i < gems % shots ? 1 : 0);
      if (share <= 0) continue;
      float angle = planetAge * 0.7f + i * MathHelper.TwoPi / shots;
      Vector2 start = PlanetPos + PlanetDirection(angle) * PlanetRadius * 2.1f;
      LaunchPlanetShot(PlanetShotKind.Rocket, start, target, share, Math.Max(1, constellationFirePower),
        delay: i * 0.06f);
    }
    constellationRockets = constellationPayload = constellationFirePower = 0;
    constellationAge = 0f;
  }

  private void LaunchPlanetShot(PlanetShotKind kind, Vector2 start, Vector2 end, int damage, int firePower,
    float delay = 0f, bool critical = false)
  {
    var control = (start + end) * 0.5f;
    if (kind == PlanetShotKind.Rocket)
    {
      // Rockets arc out to either side before turning in on the planet.
      var travel = end - start;
      control += new Vector2(-travel.Y, travel.X) * (Random.Shared.NextSingle() - 0.5f) * 0.9f;
    }
    float speed = kind switch
    {
      PlanetShotKind.Manual => ManualShotSpeed,
      PlanetShotKind.Harpoon => HarpoonShotSpeed,
      PlanetShotKind.Rail => RailgunRoundSpeed,
      _ => CannonShotSpeed,
    };
    var shot = new PlanetShot
    {
      Kind = kind,
      Delay = delay,
      Duration = kind == PlanetShotKind.Rocket
        ? RocketFlightSeconds
        : Math.Max(0.05f, Vector2.Distance(start, end) / speed),
      Damage = damage,
      FirePower = firePower,
      Critical = critical,
    };
    SetQuadraticPath(shot, start, control, end);
    AddPlanetShot(shot);
  }

  // The exact cubic form of a quadratic Bezier curve.
  private static void SetQuadraticPath(PlanetShot shot, Vector2 start, Vector2 control, Vector2 end)
  {
    shot.Start = start;
    shot.Control1 = start + (control - start) * (2f / 3f);
    shot.Control2 = end + (control - end) * (2f / 3f);
    shot.End = end;
  }

  private void AddPlanetShot(PlanetShot shot)
  {
    shot.Reserved = Math.Min(Math.Max(0, shot.Damage), PlanetGemRoom());
    pendingPlanetGems += shot.Reserved;
    if (planetShots.Count >= MaxPlanetShots && !shot.Critical && !shot.Doomsday)
      for (int i = planetShots.Count - 1; i >= 0; i--)
        if (planetShots[i].Kind == shot.Kind && planetShots[i].FirePower == shot.FirePower
          && planetShots[i].Bounces == shot.Bounces && !planetShots[i].Mini && !planetShots[i].FarSide)
        {
          planetShots[i].Damage = (int)Math.Min(int.MaxValue, (long)planetShots[i].Damage + shot.Damage);
          planetShots[i].Reserved += shot.Reserved;
          return;
        }
    planetShots.Add(shot);
  }

  private void UpdateWeapons(float dt, PlayAreaBounds bounds)
  {
    var upgrades = UpgradeManager.Instance.UG;
    RefreshWeaponBonuses();
    weaponBounds = bounds;
    paintedTargetRemaining = Math.Max(0f, paintedTargetRemaining - dt);

    // A core fracture holds every weapon until its shard is out; shots already in
    // flight still land.
    bool held = FracturePaused;

    // Clicking the planet fires at that spot, clicking the homebase at the near
    // side. Holding the click (Hold Click upgrade) becomes rapid fire.
    if (!held && WorldClickTriggered && (IsOnPlanet(gemPointerWorld) || IsOnHomeBase(gemPointerWorld)))
    {
      if (UpgradeManager.Instance.UGM.TargetPainter && IsOnPlanet(gemPointerWorld))
      {
        var fromCenter = gemPointerWorld - PlanetPos;
        paintedPlanetTarget = fromCenter.LengthSquared() > 0.01f
          ? PlanetPos + Vector2.Normalize(fromCenter) * PlanetRadius * 0.9f
          : PlanetPos - Vector2.UnitX * PlanetRadius * 0.9f;
        paintedTargetRemaining = PrestigeTalentEffects.TargetPainterDuration;
      }
      FireManualShot(IsOnPlanet(gemPointerWorld) ? gemPointerWorld : PlanetFacingPoint(0.55f));
    }

    if (!held)
    {
      UpdateMiningLaser(dt, bounds, upgrades);
      UpdateArcHarpoon(dt, bounds, upgrades);
      UpdatePlanetCracker(dt, bounds);
      UpdateRocketPods(dt, upgrades);
      UpdateRailgun(dt, upgrades);
      UpdateConstellation(dt);
      UpdateCoreDrills(dt, bounds);
    }

    for (int i = planetShots.Count - 1; i >= 0; i--)
    {
      var shot = planetShots[i];
      if (shot.Delay > 0f)
      {
        shot.Delay -= dt;
        continue;
      }
      shot.Age += dt;
      if (shot.Kind == PlanetShotKind.Rocket && !shot.Mini && !shot.Split && upgrades.RocketClusterWarheads
        && shot.Age >= shot.Duration * 0.55f)
      {
        planetShots.RemoveAt(i);
        pendingPlanetGems -= shot.Reserved;
        SplitRocket(shot);
        continue;
      }
      if (shot.Age < shot.Duration) continue;
      planetShots.RemoveAt(i);
      pendingPlanetGems -= shot.Reserved;
      ResolvePlanetHit(shot, bounds, upgrades);
    }
    if (!held)
    {
      UpdateWeaponSpecials(dt, bounds);
      UpdateTalentCombos(dt, bounds);
    }

    for (int i = planetExplosions.Count - 1; i >= 0; i--)
      if ((planetExplosions[i].Age += dt) >= ExplosionFrames * ExplosionFrameSeconds)
        planetExplosions.RemoveAt(i);
    UpdateRailgunEffects(dt);
  }

  private void ResolvePlanetHit(PlanetShot shot, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    var fromCenter = shot.End - PlanetPos;
    float impactAngle = MathF.Atan2(fromCenter.Y, fromCenter.X);
    switch (shot.Kind)
    {
      case PlanetShotKind.Cannon:
      case PlanetShotKind.Manual:
        if (shot.Critical)
          CriticalHitEffects(shot);
        else
        {
          PulsePlanet(shot.Damage >= 20 ? 1f : shot.Bounces > 0 ? 0.35f : 0.6f);
          SpawnerEffects.Add(null, shot.End, shot.Kind == PlanetShotKind.Manual ? Color.Gold : CannonGlow,
            3f, 18f + 4f * MathF.Sqrt(shot.Damage), 0.35f);
        }
        if (upgrades.CannonShatterShells)
          KnockClusterLoose(shot.Damage, shot.FirePower, bounds, 1f, impactAngle, 0.9f);
        else
          KnockGemsLoose(shot.Damage, shot.FirePower, bounds);
        OnCannonHit(shot);
        if (upgrades.CannonRicochet && shot.Bounces < MainShipWeapons.RicochetBounces)
          LaunchRicochet(shot, impactAngle);
        break;
      case PlanetShotKind.Rocket:
        PulsePlanet(shot.Mini ? 0.4f : 0.7f, shot.Mini ? 0.12f : 0.25f);
        planetExplosions.Add(new PlanetExplosion { Position = shot.End, Scale = shot.Mini ? 0.85f : 1.3f });
        KnockClusterLoose(shot.Damage, shot.FirePower, bounds, 1f, impactAngle, 0.8f);
        OnRocketHit(shot, impactAngle, bounds);
        break;
      case PlanetShotKind.Harpoon:
        EmbedArcHarpoon(shot.End, shot.FirePower);
        break;
      case PlanetShotKind.Drone:
        DetonateKamikazeDrone(shot, impactAngle, bounds);
        break;
      case PlanetShotKind.Rail:
        PulsePlanet(1f, 1f);
        planetExplosions.Add(new PlanetExplosion { Position = shot.End, Scale = 2.6f });
        SpawnerEffects.Add(null, shot.End, RailGlow, 8f, 160f, 0.6f);
        SpawnerEffects.Add(null, PlanetPos, Color.White, PlanetRadius, PlanetRadius * 2.6f, 0.9f);
        AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
        // The impact splits into fragments that land as clusters across the whole field.
        int fragments = Math.Max(1, upgrades.RailgunFragments);
        for (int i = 0; i < fragments; i++)
          KnockClusterLoose(shot.Damage / fragments + (i < shot.Damage % fragments ? 1 : 0),
            shot.FirePower, bounds, reachScale: 10f);
        if (upgrades.RailgunShockwave)
          StartShockwave(impactAngle, (int)(shot.Damage * MainShipWeapons.ShockwaveShare), shot.FirePower);
        if (upgrades.RailgunSingularity)
          StartSingularity(impactAngle, shot.Damage * MainShipWeapons.SingularityShare, shot.FirePower);
        DetonateMolten(impactAngle, PrestigeTalentEffects.RailgunDetonationRadius, bounds);
        break;
    }
  }

  private int LaserBeamCount => MainShipWeapons.LaserBeams(UpgradeManager.Instance.UG);

  // Per-beam sweep speed and phase, so multiple beams look independent.
  private static readonly float[] LaserSweepSpeed = [0.7f, 0.53f, 0.61f, 0.47f];
  private static readonly float[] LaserSweepPhase = [0f, 2.1f, 1.1f, 3.4f];
  private static readonly float[] LaserDepthSpeed = [1.13f, 0.91f, 1.02f, 0.83f];
  private static readonly float[] LaserDepthPhase = [0.4f, 2.6f, 1.7f, 0.9f];

  // One beam sweeps the whole near side, drifting across the planet's face as well as
  // up and down it. Twin Lasers each sweep their own half at different speeds: they
  // look independent, but always stay at least 0.36 rad apart, so they never melt the
  // same spot or cross. Quad Lasers split each half into an outer and an inner lane.
  private float LaserContactAngle(int beam)
  {
    if (PaintedTargetActive)
    {
      var painted = paintedPlanetTarget - PlanetPos;
      return MathF.Atan2(painted.Y, painted.X);
    }
    float facing = PlanetFacingAngle();
    if (LaserBeamCount == 1) return facing + MathF.Sin(laserTime * 0.7f) * 0.75f;
    beam %= LaserSweepSpeed.Length;
    float sweep = 0.5f + 0.5f * MathF.Sin(laserTime * LaserSweepSpeed[beam] + LaserSweepPhase[beam]);
    float offset = LaserBeamCount == 2 ? 0.18f + 0.62f * sweep
      : beam < 2 ? 0.51f + 0.29f * sweep : 0.18f + 0.29f * sweep;
    return beam % 2 == 0 ? facing - offset : facing + offset;
  }

  // How far from the planet's centre the beam lands, as a share of its radius.
  private float LaserContactDepth(int beam)
  {
    beam %= LaserDepthSpeed.Length;
    return 0.62f + 0.33f * MathF.Sin(laserTime * LaserDepthSpeed[beam] + LaserDepthPhase[beam]);
  }

  private Vector2 LaserContact(int beam)
    => PaintedTargetActive ? paintedPlanetTarget
      : PlanetPos + PlanetDirection(LaserContactAngle(beam)) * PlanetRadius * LaserContactDepth(beam);

  private void UpdateMiningLaser(float dt, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    if (!upgrades.MiningLaser)
    {
      laserCarry = 0f;
      return;
    }
    laserTime += dt;
    int firePower = SignalStats.FirePower(MainShipWeapon.Laser);
    int beams = LaserBeamCount;
    // Overheat Surge scales the melt rate: 1 while heating, 4 in a surge, 0 while venting.
    float beamRate = (float)MainShipWeapons.LaserGemRate(
      PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Laser)), firePower)
      * UpdateOverheat(dt, upgrades) * (PaintedTargetActive ? PrestigeTalentEffects.TargetPainterYieldMultiplier : 1)
      * PrestigeTalentEffects.CombinedArmsMultiplier(AutomaticWeaponCount) * UpdateLaserOvercharge(dt);
    laserCarry += beams * beamRate * dt;
    if (beamRate > 0f) UpdateBeamRiders(dt, upgrades, beams);
    float value = upgrades.MiningLaserThermalLance ? MainShipWeapons.ThermalLanceValue : 1f;
    UpdateMagmaScars(dt, bounds, upgrades, beams, beamRate, firePower, value);
    for (int i = 0; i < MaxLaserGemsPerFrame && laserCarry >= 1f; i++)
    {
      // A full field still takes the beam's damage; it just spills no gems.
      laserCarry -= 1f;
      // Twin beams take turns, each melting gems off its own spot.
      laserNextBeam = (laserNextBeam + 1) % beams;
      KnockGemsLoose(1, firePower, bounds, LaserReach, LaserContactAngle(laserNextBeam), 0.35f, value);
    }
    laserCarry = Math.Min(laserCarry, MaxLaserGemsPerFrame);
  }

  // Planet Cracker command: a heavy beam from the cyan core that streams gems off the
  // planet while it runs.
  private Vector2 CrackerMount() => HullMount(0f, 19f);

  private Vector2 CrackerContact() => PlanetPos + PlanetDirection(PlanetFacingAngle()) * PlanetRadius * 0.95f;

  private float PlanetCrackerGemsPerSecond(float multiplier)
    => ManualFleetAbilities.PlanetCrackerGemsPerSecond * SignalStats.FirePower(MainShipWeapon.Cannon) * multiplier;

  private void StartPlanetCracker()
  {
    crackerCarry = 0f;
    PulsePlanet(1f, 0.8f);
    SpawnerEffects.Add(null, CrackerContact(), CrackerFlareColor, 6f, 90f, 0.5f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
  }

  private void UpdatePlanetCracker(float dt, PlayAreaBounds bounds)
  {
    float multiplier = ManualAbilities.ActivePlanetCrackerMultiplier;
    if (multiplier <= 0f)
    {
      crackerCarry = 0f;
      return;
    }
    planetShake = Math.Max(planetShake, 0.55f);
    planetHitPulse = Math.Max(planetHitPulse, 0.5f);
    crackerCarry += PlanetCrackerGemsPerSecond(multiplier) * dt;
    // The beam cracks deeper than the cannon and throws gems far across the field.
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon) + ManualFleetAbilities.PlanetCrackerDepth;
    int gems = (int)Math.Min(crackerCarry, MaxCrackerGemsPerFrame);
    crackerCarry -= gems;
    KnockGemsLoose(gems, firePower, bounds, 1.6f, PlanetFacingAngle(), 1.5f);
  }

  private void UpdateRocketPods(float dt, UpgradesGeneratorUpgrades upgrades)
  {
    if (!upgrades.RocketPods)
    {
      rocketTimer = 0f;
      return;
    }
    float interval = MainShipWeapons.RocketSalvoInterval(
      PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Rockets)));
    rocketTimer = Math.Min(rocketTimer + dt, interval * 2f);
    if (rocketTimer < interval) return;
    rocketTimer -= interval;
    FireRocketSalvo();
  }

  private void ClearPlanetShots()
  {
    planetShots.Clear();
    planetExplosions.Clear();
    pendingPlanetGems = 0;
    laserCarry = rocketTimer = crackerCarry = paintedTargetRemaining = constellationAge = 0f;
    constellationRockets = constellationPayload = constellationFirePower = 0;
    paintedPlanetTarget = Vector2.Zero;
    ClearArcHarpoon();
    ClearRailgun();
    ClearWeaponSpecials();
    ClearTalentCombos();
    planetHitPulse = planetShake = 0f;
  }

  private static Vector2 Bezier(PlanetShot shot, float t)
  {
    float u = 1f - t;
    return u * u * u * shot.Start + 3f * u * u * t * shot.Control1 + 3f * u * t * t * shot.Control2
      + t * t * t * shot.End;
  }

  private static Vector2 BezierDirection(PlanetShot shot, float t)
  {
    float u = 1f - t;
    var direction = 3f * u * u * (shot.Control1 - shot.Start) + 6f * u * t * (shot.Control2 - shot.Control1)
      + 3f * t * t * (shot.End - shot.Control2);
    return direction.LengthSquared() > 1e-4f ? Vector2.Normalize(direction) : Vector2.UnitX;
  }

  private static bool IsReady(AsyncContent.AsyncAsset<Texture2D> texture)
    => texture?.IsLoaded == true && !texture.IsFailed;

  private void DrawWeapons()
  {
    if (!PlanetMiningEnabled) return;
    var upgrades = UpgradeManager.Instance.UG;
    bool laserMounted = upgrades.MiningLaser && GameStarted && !m_prestiging && !m_postPrestige && !FracturePaused;
    // The beam switches off while an overheated laser vents.
    bool laser = laserMounted && !LaserVenting;
    float feather = 1.25f / Math.Max(0.1f, m_camera.Zoom);

    DrawMagmaScars(feather);
    DrawCraters();
    // Glows and trails first (additive shapes), sprites on top.
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    DrawWeaponSpecialGlows(feather, laserMounted);
    if (laser)
      DrawLaserSparks(feather);
    foreach (var shot in planetShots)
    {
      // The railgun's round is drawn with its trail (DrawRailgun).
      if (shot.Delay > 0f || shot.Kind == PlanetShotKind.Rail) continue;
      float t = ShotProgress(shot);
      var head = Bezier(shot, t);
      var tail = Bezier(shot, Math.Max(0f, t - 0.12f));
      var (color, width) = shot.Kind switch
      {
        _ when shot.Critical => (Color.Gold, 6f),
        PlanetShotKind.Manual => (ManualGlow, 3.5f),
        PlanetShotKind.Harpoon => (ArcHarpoonGlow, 5f),
        PlanetShotKind.Rocket => (new Color(255, 140, 60), shot.Mini ? 1.8f : 2.5f),
        PlanetShotKind.Drone => shot.Doomsday ? (DoomsdayGlow, 8f) : shot.Mini ? (KamikazeGlow, 2.5f) : (KamikazeGlow, 4.5f),
        _ => (CannonGlow, 2.5f + MathF.Min(4f, MathF.Sqrt(shot.Damage) * 0.35f)),
      };
      m_shapeBatch.FillLine(tail, head, width, color * 0.45f, Math.Max(feather, width * 1.5f));
      if (shot.Kind == PlanetShotKind.Drone)
      {
        // The armed warhead blinks faster as the drone closes in.
        float blink = 0.5f + 0.5f * MathF.Sin(shot.Age * (12f + 30f * t));
        float light = shot.Doomsday ? 7f : shot.Mini ? 2f : 3f;
        m_shapeBatch.FillCircle(head, light + light * blink, (shot.Critical ? Color.Gold : KamikazeGlow) * (0.4f + 0.6f * blink),
          Math.Max(feather, light + 1f));
      }
    }
    DrawArcHarpoon(feather);
    DrawCoreDrillGlows(feather);
    DrawTalentGlows(feather);
    if (!upgrades.AutoCannon && GameStarted && !m_prestiging && !m_postPrestige)
      DrawClickToFireHint(feather);
    if (PaintedTargetActive)
      DrawPaintedTarget(feather);
    if (constellationRockets > 0)
      DrawConstellation(feather);
    m_shapeBatch.End();

    bool cracker = ManualAbilities.ActivePlanetCrackerMultiplier > 0f && GameStarted && !m_prestiging && !m_postPrestige
      && !FracturePaused;
    bool extraction = m_prestiging && m_prestigeTime < CollapseImplodeSeconds;
    if (laser || cracker || extraction || HasActiveCoreDrillBeam)
      DrawBeams(laser, cracker, extraction);
    if (drillPods.Count > 0)
      DrawCoreDrillPods();

    DrawSingularities();
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    foreach (var shot in planetShots)
      if (shot.Delay <= 0f)
        DrawPlanetShot(shot);
    if (IsReady(TextureCache.PlanetExplosion))
      foreach (var explosion in planetExplosions)
      {
        int frame = ExplosionFirstFrame
          + Math.Min(ExplosionFrames - 1, (int)(explosion.Age / ExplosionFrameSeconds));
        m_spriteBatch.Draw(TextureCache.PlanetExplosion.Value, explosion.Position,
          new Rectangle(frame * ExplosionFrameSize, 0, ExplosionFrameSize, ExplosionFrameSize), Color.White,
          0f, new Vector2(ExplosionFrameSize / 2f), explosion.Scale, SpriteEffects.None, 0f);
      }
    m_spriteBatch.End();
    DrawRailgun(feather);
    DrawShardPickups();
  }

  // How far along its path a shot is. Kamikaze drones accelerate into their dive.
  private static float ShotProgress(PlanetShot shot)
  {
    float t = Math.Clamp(shot.Age / shot.Duration, 0f, 1f);
    return shot.Kind == PlanetShotKind.Drone ? t * t * (2.2f - 1.2f * t) : t;
  }

  private void DrawPlanetShot(PlanetShot shot)
  {
    float t = ShotProgress(shot);
    var position = Bezier(shot, t);
    var direction = BezierDirection(shot, t);
    // The strips point up; rotate their -Y axis onto the direction of travel.
    float rotation = MathF.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
    switch (shot.Kind)
    {
      case PlanetShotKind.Rocket when IsReady(TextureCache.RocketProjectile):
      {
        int frame = (int)(shot.Age * 20f) % RocketFrames;
        m_spriteBatch.Draw(TextureCache.RocketProjectile.Value, position,
          new Rectangle(frame * RocketFrameWidth, 0, RocketFrameWidth, RocketFrameHeight), Color.White, rotation,
          new Vector2(RocketFrameWidth / 2f, RocketFrameHeight / 2f), shot.Mini ? 1.5f : 2.2f, SpriteEffects.None, 0f);
        break;
      }
      case PlanetShotKind.Cannon or PlanetShotKind.Manual when IsReady(TextureCache.PlanetCannonBullet):
      {
        int frame = (int)(shot.Age * 20f) % CannonFrames;
        bool manual = shot.Kind == PlanetShotKind.Manual;
        m_spriteBatch.Draw(TextureCache.PlanetCannonBullet.Value, position,
          new Rectangle(frame * CannonFrameSize, 0, CannonFrameSize, CannonFrameSize),
          shot.Critical ? Color.Gold : manual ? new Color(255, 230, 150) : Color.White, rotation,
          new Vector2(CannonFrameSize / 2f), shot.Critical ? 1.9f : manual ? 1.2f : 0.9f, SpriteEffects.None, 0f);
        break;
      }
      // Bomblets are just their glowing trail and light.
      case PlanetShotKind.Drone when !shot.Mini && TextureCache.DroneShip is { } hull:
        m_spriteBatch.Draw(hull.Texture, position, hull.Bounds, shot.Critical ? Color.Gold : Color.White, rotation,
          new Vector2(hull.Width / 2f, hull.Height / 2f), shot.Doomsday ? DoomsdayDroneScale : KamikazeDroneScale,
          SpriteEffects.None, 0f);
        break;
    }
  }

  // Until the cannon is automated, a pulsing target ring shows the planet can be clicked.
  private void DrawClickToFireHint(float feather)
  {
    bool hovered = IsOnPlanet(gemPointerWorld);
    float pulse = 0.5f + 0.5f * MathF.Sin(planetAge * 3.5f);
    float radius = PlanetRadius * (1.1f + 0.04f * pulse);
    var color = new Color(255, 120, 90) * ((hovered ? 0.65f : 0.3f) + 0.25f * pulse);
    m_shapeBatch.BorderCircle(PlanetPos, radius, color, 1.6f, Math.Max(feather, 2f));
    for (int i = 0; i < 4; i++)
    {
      var direction = PlanetDirection(MathHelper.PiOver4 + i * MathHelper.PiOver2 + planetAge * 0.4f);
      m_shapeBatch.FillLine(PlanetPos + direction * (radius + 6f), PlanetPos + direction * (radius + 22f),
        1.6f, color, feather);
    }
  }

  private void DrawPaintedTarget(float feather)
  {
    float life = paintedTargetRemaining / PrestigeTalentEffects.TargetPainterDuration;
    float pulse = 0.5f + 0.5f * MathF.Sin(planetAge * 12f);
    float radius = 13f + pulse * 4f;
    var color = Color.Lerp(new Color(255, 90, 55), Color.Gold, pulse) * (0.45f + 0.45f * life);
    m_shapeBatch.BorderCircle(paintedPlanetTarget, radius, color, 2f, Math.Max(feather, 3f));
    for (int i = 0; i < 4; i++)
    {
      var direction = PlanetDirection(i * MathHelper.PiOver2);
      m_shapeBatch.FillLine(paintedPlanetTarget + direction * (radius + 3f),
        paintedPlanetTarget + direction * (radius + 11f), 2f, color, feather);
    }
  }

  private void DrawConstellation(float feather)
  {
    int visible = Math.Min(12, constellationRockets);
    float radius = PlanetRadius * 2.1f;
    for (int i = 0; i < visible; i++)
    {
      float angle = planetAge * 0.7f + i * MathHelper.TwoPi / visible;
      Vector2 position = PlanetPos + PlanetDirection(angle) * radius;
      float pulse = 0.65f + 0.35f * MathF.Sin(planetAge * 8f + i);
      m_shapeBatch.FillCircle(position, 3.5f, new Color(255, 145, 65) * pulse, Math.Max(feather, 5f));
    }
  }

  // Beams are SDF quads shaded by LaserBeam.fx: hot core, flowing energy, heat
  // shimmer, and flares at the emitter and on the planet. The mining laser grows
  // with its fire power; the Planet Cracker is a much heavier beam that fades in
  // and out over its cast.
  private void DrawBeams(bool laser, bool cracker, bool extraction)
  {
    var effect = EffectCache.LaserBeamFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    // Quad lasers plus every active drill pod still fit in one shader batch.
    laserRenderer ??= new SdfLineRenderer(GraphicsDevice, effect.Value, maxLines: 12) { PulseExtraPadding = 0f };
    laserRenderer.Begin(m_camera.GetBoundingFrustum().Matrix, planetAge);
    if (laser)
    {
      // Overheat Surge heats the beam from orange to white and swells it during a surge.
      var (beamColor, flareColor, widthScale, intensity) = LaserHeatLook();
      float width = (2.2f + 0.35f * MathF.Sqrt(SignalStats.FirePower(MainShipWeapon.Laser))) * widthScale;
      // Room for the halo and the impact flare around the beam's quad.
      laserRenderer.BaseGlowPadding = width * 14f;
      for (int beam = 0; beam < LaserBeamCount; beam++)
        laserRenderer.DrawLine(LaserMount(), LaserContact(beam), width, beamColor, flareColor, pulseProgress: intensity);
    }
    if (cracker)
    {
      int slot = ManualFleetAbilities.PlanetCrackerSlot;
      float elapsed = ManualAbilities.CastDuration(slot) - ManualAbilities.RemainingDuration(slot);
      float intensity = Math.Clamp(elapsed / 0.15f, 0f, 1f)
        * Math.Clamp(ManualAbilities.RemainingDuration(slot) / 0.3f, 0f, 1f);
      float width = 6f * (0.6f + 0.4f * intensity);
      laserRenderer.BaseGlowPadding = width * 14f;
      // Kept below full brightness so bloom leaves the beam blue rather than white.
      laserRenderer.DrawLine(CrackerMount(), CrackerContact(), width, CrackerBeamColor, CrackerFlareColor,
        pulseProgress: 0.3f + 0.6f * intensity);
    }
    if (extraction)
    {
      // Core extraction drills a widening beam into the planet's heart until the core breaches.
      float t = m_prestigeTime;
      float intensity = Smooth(0f, 0.25f, t) * (1f - Smooth(CollapseBuildupSeconds + 0.3f, CollapseImplodeSeconds, t));
      float width = 3f + 9f * Smooth(0f, CollapseBuildupSeconds, t);
      laserRenderer.BaseGlowPadding = width * 14f;
      laserRenderer.DrawLine(CrackerMount(), PlanetPos, width, ExtractionGlow * intensity,
        Color.White * intensity, pulseProgress: 0.4f + 0.5f * intensity);
    }
    foreach (var pod in drillPods)
    {
      if (!pod.Landed || pod.Done) continue;
      var outward = PlanetDirection(pod.BoreAngle);
      var depth = DrillDepthColor(CoreDrill.Deeper(pod.FirePower, pod.Layers));
      float pulse = 0.82f + 0.18f * MathF.Sin(planetAge * 37f + pod.BoreAngle * 3f);
      float width = 2.1f + 0.25f * pod.Layers;
      laserRenderer.BaseGlowPadding = width * 12f;
      // A short shader-driven bore connects the pod's nose to the hot point below
      // the crust. The pod sprite is drawn afterward, hiding the emitter seam.
      laserRenderer.DrawLine(pod.Path.End + outward * 4f,
        pod.Path.End - outward * Math.Min(PlanetRadius * 0.22f, 42f), width,
        depth, Color.White, pulseProgress: pulse);
    }
    laserRenderer.End();
  }

  private void DrawLaserSparks(float feather)
  {
    for (int beam = 0; beam < LaserBeamCount; beam++)
      DrawLaserSparks(LaserContact(beam), beam * 0.5f, feather);
  }

  // Sparks spray off the melting spot, back toward the ship.
  private void DrawLaserSparks(Vector2 end, float phaseOffset, float feather)
  {
    var outward = Vector2.Normalize(end - PlanetPos);
    for (int i = 0; i < 6; i++)
    {
      float phase = (laserTime * 3f + i * 0.37f + phaseOffset) % 1f;
      float angle = MathF.Atan2(outward.Y, outward.X) + MathF.Sin(i * 12.9898f) * 1.1f;
      var spark = end + PlanetDirection(angle) * (6f + phase * 30f);
      m_shapeBatch.FillLine(spark, spark + PlanetDirection(angle) * 5f, 1.1f,
        Color.Lerp(LaserGlow, Color.White, 0.5f) * (1f - phase), feather);
    }
  }
}
