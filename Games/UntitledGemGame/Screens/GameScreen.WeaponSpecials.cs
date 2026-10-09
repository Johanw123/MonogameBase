using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Weapon specials: upgrades that change how a weapon hits (tuning in MainShipWeapons).
//  - Cannon: Ricochet Rounds bounce off the planet to new spots; Critical Shells
//    sometimes land a huge hit.
//  - Mining laser: Magma Scars keep oozing gems where the beam has been; Overheat
//    Surge heats the beam until it surges white-hot, then vents.
//  - Rocket pods: Cluster Warheads split into mini-rockets; Orbital Strike swings
//    around the planet and hits its far side.
//  - Railgun: Tectonic Shockwave races around the planet shedding a ring of
//    gems (Conductor Round lives with the harpoon, in GameScreen.Conductors.cs).
public partial class UntitledGemGameGameScreen
{
  private const float RicochetSeconds = 0.3f;
  private const float OrbitalRocketSeconds = 1.35f;
  private const float MiniRocketSeconds = 0.35f;
  private const float ShockwaveSeconds = 0.9f;
  private const int MaxMagmaScars = 32;
  private static int MagmaScarCap => PrestigeTalentEffects.MoltenSpotCap(MaxMagmaScars);
  private const float MagmaTrailInterval = 0.04f;
  // Enough for a full cut behind every beam (up to eight with Quad Lasers and Prismatic Lens).
  private const int MaxMagmaTrail = 800;
  // The cut is a seam of small molten spots (MoltenCrater.fx), this big and this far apart
  // in planet texels.
  private const float ScarSpotTexels = 1.5f;
  private const float ScarSpotSpacing = 1.4f;
  private const float ScarSoot = 0.25f;
  private const int MaxScarBeams = 16;
  private static readonly Color MagmaColor = new(255, 80, 20);
  private static readonly Color ShockwaveColor = new(255, 230, 160);

  private sealed class MagmaScar
  {
    public float Angle, Age, Budget, Carry, Value;
    public int FirePower;
  }

  // A point on the line the beam cuts into the planet.
  private struct MagmaTrailPoint
  {
    public Vector2 Position;
    public float Age;
    public int Beam;
    public bool Starts; // the first point of a new cut
    public byte Seed;   // keeps the spots drawn after it the same from frame to frame
  }

  private sealed class Shockwave
  {
    public PlanetDamageSource Source;
    public float Angle, Age;
    public int Gems, Emitted, FirePower;
  }

  private readonly List<MagmaScar> magmaScars = new();
  private readonly List<MagmaTrailPoint> magmaTrail = new();
  private readonly MagmaTrailPoint[] scarPrevious = new MagmaTrailPoint[MaxScarBeams];
  private readonly bool[] scarHasPrevious = new bool[MaxScarBeams];
  private readonly List<Shockwave> shockwaves = new();
  private float magmaTimer, magmaTrailTimer;
  private bool magmaTrailBroken = true;
  private float laserHeat, laserSurge, laserVent;

  private bool LaserVenting => laserVent > 0f;

  // Thermite Rounds make the laser's scars burn longer.
  private float MagmaScarSeconds => PrestigeTalentEffects.MagmaScarSeconds(MainShipWeapons.MagmaScarSeconds)
    * SignalStats.MoltenDurationMultiplier * PrestigeTalentEffects.MoltenBurn;

  // ---- Cannon ----

  // Critical Shells' chance, plus the crit build's (Deadeye, Hot Streak).
  private bool RollCriticalShell(ref int gems)
  {
    float chance = (UpgradeManager.Instance.UG.CannonCritical ? SignalStats.CriticalChance : 0f)
      + SignalStats.CritChance(PrestigeTalentEffects.WeaponCritChance);
    if (chance <= 0f || Random.Shared.NextSingle() >= chance) return false;
    gems = (int)Math.Min(int.MaxValue, gems * (double)SignalStats.CritMultiplier);
    return true;
  }

  private void CriticalHitEffects(PlanetShot shot)
  {
    PulsePlanet(1f, 0.5f);
    planetExplosions.Add(new PlanetExplosion { Position = shot.End, Scale = 1.7f });
    SpawnerEffects.Add(null, shot.End, Color.Gold, 6f, 80f, 0.45f);
    SpawnerEffects.Add(null, shot.End, Color.White, 3f, 40f, 0.25f);
    if (TakeCritPopup()) ShowWorldPopup(shot.End, Loc.T("CRITICAL!"), large: true);
  }

  // The shot skips off the planet and arcs to a new spot on its surface. The first
  // bounce carries 40% of the shot's gems, and later bounces carry the same on.
  private void LaunchRicochet(PlanetShot shot, float impactAngle)
  {
    int gems = shot.Bounces == 0 ? (int)MathF.Ceiling(shot.Damage * MainShipWeapons.RicochetShare) : shot.Damage;
    float side = Random.Shared.Next(2) == 0 ? -1f : 1f;
    float hop = 0.7f + Random.Shared.NextSingle() * 0.5f;
    var start = PlanetPos + PlanetDirection(impactAngle) * PlanetRadius * 0.95f;
    var apex = PlanetPos + PlanetDirection(impactAngle + side * hop * 0.5f) * PlanetRadius * 1.75f;
    var end = PlanetPos + PlanetDirection(impactAngle + side * hop) * PlanetRadius * 0.92f;
    var bounce = new PlanetShot
    {
      Kind = PlanetShotKind.Cannon,
      Duration = RicochetSeconds,
      Damage = gems,
      FirePower = shot.FirePower,
      Bounces = shot.Bounces + 1,
    };
    SetQuadraticPath(bounce, start, apex, end);
    AddPlanetShot(bounce);
    SpawnerEffects.Add(null, start, CannonGlow, 2f, 12f, 0.2f);
  }

  // ---- Rockets ----

  // Swing out to one flank, around the planet, and down onto its far side.
  private void LaunchOrbitalRocket(Vector2 start, int gems, int firePower, int index, float delay)
  {
    float side = index % 2 == 0 ? 1f : -1f;
    var toShip = PlanetDirection(PlanetFacingAngle());
    var flank = new Vector2(-toShip.Y, toShip.X) * side;
    float sweep = 0.2f + Random.Shared.NextSingle() * 0.7f;
    var target = -toShip * MathF.Cos(sweep) + flank * MathF.Sin(sweep);
    if (PaintedTargetActive)
      target = Vector2.Normalize(paintedPlanetTarget - PlanetPos);
    AddPlanetShot(new PlanetShot
    {
      Kind = PlanetShotKind.Rocket,
      Delay = delay,
      Duration = OrbitalRocketSeconds,
      Damage = gems,
      FirePower = firePower,
      FarSide = true,
      Start = start,
      Control1 = start + (PlanetPos - start) * 0.45f + flank * PlanetRadius * 2.3f,
      Control2 = PlanetPos + flank * PlanetRadius * 2.5f - toShip * PlanetRadius * 1.5f,
      End = PlanetPos + target * PlanetRadius * 0.9f,
    });
  }

  // Cluster Warheads: the rocket bursts into mini-rockets that spread over the target area.
  private void SplitRocket(PlanetShot rocket)
  {
    float t = Math.Clamp(rocket.Age / rocket.Duration, 0f, 1f);
    var position = Bezier(rocket, t);
    var heading = BezierDirection(rocket, t);
    var aim = rocket.End - PlanetPos;
    float target = MathF.Atan2(aim.Y, aim.X);
    int share = (int)MathF.Ceiling(rocket.Damage * MainShipWeapons.ClusterWarheadShare);
    SpawnerEffects.Add(null, position, new Color(255, 160, 70), 2f, 22f, 0.3f);
    for (int i = 0; i < MainShipWeapons.ClusterWarheadSplit; i++)
    {
      var end = PlanetPos + PlanetDirection(target + (i - 1) * 0.45f) * PlanetRadius * 0.9f;
      var control = position + heading * Vector2.Distance(position, end) * 0.5f;
      var mini = new PlanetShot
      {
        Kind = PlanetShotKind.Rocket,
        Duration = MiniRocketSeconds + i * 0.04f,
        Damage = share,
        FirePower = rocket.FirePower,
        Mini = true,
        Split = true,
        FarSide = rocket.FarSide,
      };
      SetQuadraticPath(mini, position, control, end);
      AddPlanetShot(mini);
    }
  }

  // ---- Mining laser ----

  // Returns how fast the laser melts gems right now: 1 while heating, the surge
  // rate during a surge and 0 while venting.
  private float UpdateOverheat(float dt, UpgradesGeneratorUpgrades upgrades)
  {
    if (!upgrades.LaserOverheat)
    {
      laserHeat = laserSurge = laserVent = 0f;
      return 1f;
    }
    if (laserVent > 0f)
    {
      laserVent = Math.Max(0f, laserVent - dt);
      return 0f;
    }
    if (laserSurge > 0f)
    {
      laserSurge -= dt;
      planetShake = Math.Max(planetShake, 0.3f);
      if (laserSurge <= 0f)
      {
        laserSurge = laserHeat = 0f;
        laserVent = MainShipWeapons.OverheatVentSeconds;
      }
      return MainShipWeapons.OverheatSurgeRate;
    }
    laserHeat += dt / MainShipWeapons.OverheatBuildSeconds;
    if (laserHeat >= 1f)
    {
      laserHeat = 1f;
      laserSurge = MainShipWeapons.OverheatSurgeSeconds;
      PulsePlanet(0.8f, 0.4f);
      for (int beam = 0; beam < LaserBeamCount; beam++)
        SpawnerEffects.Add(null, LaserContact(beam), Color.White, 4f, 45f, 0.35f);
    }
    return 1f;
  }

  private (Color Beam, Color Flare, float WidthScale, float Intensity) LaserHeatLook()
  {
    // Hot colours stay well under white: bloom already brightens them, and a white
    // beam washes out everything else in the weapon lane.
    if (laserSurge > 0f || laserOvercharge > 0f)
    {
      float flicker = 0.92f + 0.08f * MathF.Sin(planetAge * 60f);
      return (LaserSurgeBeamColor, LaserSurgeFlareColor, 1.35f * flicker, 0.72f);
    }
    float heat = laserHeat * laserHeat;
    return (Color.Lerp(LaserBeamColor, LaserHotBeamColor, heat),
      Color.Lerp(LaserFlareColor, LaserHotFlareColor, heat), 1f + 0.15f * heat, 1f + 0.05f * heat);
  }

  // Magma Scars: the beam cuts a molten line into the planet that keeps oozing gems
  // after it moves on.
  private void UpdateMagmaScars(float dt, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades,
    int beams, float beamRate, int firePower, float value)
  {
    UpdateMagmaTrail(dt, upgrades.LaserMagmaScars && beamRate > 0f, beams);
    if (upgrades.LaserMagmaScars && beamRate > 0f)
    {
      magmaTimer += dt;
      while (magmaTimer >= MainShipWeapons.MagmaScarInterval)
      {
        magmaTimer -= MainShipWeapons.MagmaScarInterval;
        for (int beam = 0; beam < beams; beam++)
        {
          if (magmaScars.Count >= MagmaScarCap) magmaScars.RemoveAt(0);
          magmaScars.Add(new MagmaScar
          {
            Angle = LaserContactAngle(beam),
            Budget = beamRate * MainShipWeapons.MagmaScarInterval * MainShipWeapons.MagmaShare * PrestigeTalentEffects.MoltenBurn,
            FirePower = firePower,
            Value = value,
          });
        }
      }
    }
    for (int i = magmaScars.Count - 1; i >= 0; i--)
    {
      var scar = magmaScars[i];
      scar.Age += dt;
      scar.Carry += scar.Budget * dt / MagmaScarSeconds;
      while (scar.Carry >= 1f)
      {
        scar.Carry -= 1f;
        KnockGemsLoose(PlanetDamageSource.MagmaScars, 1, scar.FirePower, bounds, LaserReach, scar.Angle, 0.2f, scar.Value);
      }
      if (scar.Age >= MagmaScarSeconds) magmaScars.RemoveAt(i);
    }
  }

  // The cut follows each beam closely; a vent (or the upgrade switching off) ends it.
  private void UpdateMagmaTrail(float dt, bool cutting, int beams)
  {
    for (int i = 0; i < magmaTrail.Count; i++)
    {
      var point = magmaTrail[i];
      point.Age += dt;
      magmaTrail[i] = point;
    }
    // Points are added in time order, so the oldest are at the front.
    int expired = 0;
    while (expired < magmaTrail.Count && magmaTrail[expired].Age >= MagmaScarSeconds) expired++;
    magmaTrail.RemoveRange(0, expired);

    if (!cutting)
    {
      magmaTrailBroken = true;
      return;
    }
    magmaTrailTimer += dt;
    if (magmaTrailTimer < MagmaTrailInterval) return;
    magmaTrailTimer = 0f;
    for (int beam = 0; beam < beams; beam++)
    {
      if (magmaTrail.Count >= MaxMagmaTrail) magmaTrail.RemoveAt(0);
      // A little jitter makes it a ragged crack rather than a smooth curve.
      var jitter = new Vector2(Random.Shared.NextSingle() - 0.5f, Random.Shared.NextSingle() - 0.5f) * 1.1f;
      magmaTrail.Add(new MagmaTrailPoint
      {
        Position = LaserContact(beam) + jitter,
        Beam = beam,
        Starts = magmaTrailBroken,
        Seed = (byte)Random.Shared.Next(256),
      });
    }
    magmaTrailBroken = false;
  }

  // ---- Railgun ----

  private void StartShockwave(PlanetDamageSource source, float impactAngle, int gems, int firePower)
  {
    shockwaves.Add(new Shockwave { Source = source, Angle = impactAngle, Gems = gems, FirePower = firePower });
    PulsePlanet(0.6f, 1f);
    BurstEveryWeakPoint(firePower);
  }

  private void UpdateWeaponSpecials(float dt, PlayAreaBounds bounds)
  {
    // The quake races both ways around the planet, shedding its ring of gems as it passes.
    for (int i = shockwaves.Count - 1; i >= 0; i--)
    {
      var wave = shockwaves[i];
      wave.Age += dt;
      float progress = Math.Clamp(wave.Age / ShockwaveSeconds, 0f, 1f);
      int due = (int)(wave.Gems * progress);
      float half = Math.Max(1f, wave.Gems / 2f);
      while (wave.Emitted < due)
      {
        int gem = wave.Emitted++;
        RecordPlanetDamage(wave.Source, 1);
        if (!HasGemCapacity()) continue;
        float side = gem % 2 == 0 ? 1f : -1f;
        float angle = wave.Angle + side * ((gem / 2) + 0.5f) / half * MathF.PI;
        float distance = PlanetRadius + PlanetDebrisGap + 25f + Random.Shared.NextSingle() * 30f;
        SpawnRolledGem(PlanetPos + PlanetDirection(angle) * distance, wave.FirePower, fromPlanet: true);
      }
      planetShake = Math.Max(planetShake, 0.4f * (1f - progress));
      if (wave.Age >= ShockwaveSeconds) shockwaves.RemoveAt(i);
    }
  }

  private void ClearWeaponSpecials()
  {
    magmaScars.Clear();
    magmaTrail.Clear();
    magmaTrailBroken = true;
    shockwaves.Clear();
    magmaTimer = laserHeat = laserSurge = laserVent = 0f;
  }

  // ---- Drawing ----

  // The molten cut is drawn like the craters, by MoltenCrater.fx: a seam of small spots on
  // the planet's texel grid that cool from white-hot to crust and crumble away, squashed
  // toward the limb and never past the planet's outline. Spots are spaced along each beam's
  // path between its trail points.
  private void DrawMagmaScars()
  {
    if (magmaTrail.Count == 0 || !BeginMoltenSpots(out var center, out float texel)) return;
    Array.Clear(scarHasPrevious);
    float spacing = ScarSpotSpacing * texel;
    foreach (var point in magmaTrail)
    {
      int beam = point.Beam % MaxScarBeams;
      if (scarHasPrevious[beam] && !point.Starts)
      {
        var previous = scarPrevious[beam];
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(previous.Position, point.Position) / spacing));
        for (int step = 1; step < steps; step++)
        {
          float t = step / (float)steps;
          DrawMoltenSpot(center, texel, Vector2.Lerp(previous.Position, point.Position, t), ScarSpotTexels,
            ScarLife(MathHelper.Lerp(previous.Age, point.Age, t)), (byte)(point.Seed + step * 37), ScarSoot);
        }
      }
      DrawMoltenSpot(center, texel, point.Position, ScarSpotTexels, ScarLife(point.Age), point.Seed, ScarSoot);
      scarPrevious[beam] = point;
      scarHasPrevious[beam] = true;
    }
    m_spriteBatch.End();
  }

  // A cut stays molten most of its life, then cools and crumbles near the end.
  private float ScarLife(float age)
  {
    float progress = Math.Clamp(age / MagmaScarSeconds, 0f, 1f);
    return 1f - progress * progress;
  }

  // Additive glows, drawn in the weapons' shape pass.
  private void DrawWeaponSpecialGlows(float feather, bool laserMounted)
  {
    foreach (var wave in shockwaves)
    {
      float progress = Math.Clamp(wave.Age / ShockwaveSeconds, 0f, 1f);
      float fade = 1f - progress * 0.6f;
      for (int side = -1; side <= 1; side += 2)
      {
        float front = wave.Angle + side * progress * MathF.PI;
        var previous = PlanetPos + PlanetDirection(front) * PlanetRadius * 1.03f;
        for (int segment = 1; segment <= 8; segment++)
        {
          float trail = segment / 8f;
          var next = PlanetPos + PlanetDirection(front - side * trail * 0.5f) * PlanetRadius * 1.03f;
          m_shapeBatch.FillLine(previous, next, 4f * (1f - trail * 0.7f), ShockwaveColor * (fade * (1f - trail)),
            Math.Max(feather, 3f));
          previous = next;
        }
      }
    }

    if (!laserMounted) return;
    var mount = LaserMount();
    if (laserHeat > 0f && laserSurge <= 0f)
      m_shapeBatch.FillCircle(mount, 3f + 4f * laserHeat, Color.Lerp(LaserGlow, Color.White, laserHeat) * laserHeat,
        Math.Max(feather, 3f));
    if (LaserVenting)
    {
      // Steam puffs rise from the cooling emitter.
      float vent = 1f - laserVent / MainShipWeapons.OverheatVentSeconds;
      for (int i = 0; i < 6; i++)
      {
        float rise = (vent + i / 6f) % 1f;
        var puff = mount + new Vector2(MathF.Sin(i * 2.3f) * 8f * rise, -30f * rise);
        m_shapeBatch.FillCircle(puff, 3f + 6f * rise, new Color(200, 210, 220) * (0.25f * (1f - rise)),
          Math.Max(feather, 4f));
      }
    }
  }
}
