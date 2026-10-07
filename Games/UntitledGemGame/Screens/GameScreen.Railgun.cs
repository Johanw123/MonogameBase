using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

// The railgun (tuning in MainShipWeapons): a turret on the homebase's right flank.
// Its charge fills the capacitor cells in the breech; the end of each cycle is a
// visible wind-up: it locks onto its target, energy runs up the rails from the
// breech to the muzzle, arcs crackle between the rails and light gathers at the
// muzzle while the turret shudders. Then it fires one hypersonic round: a muzzle
// flash and shock ring, a hard recoil, rails glowing white-hot and cooling through
// orange, and a straight beam with a spiralling ion trail that lingers and fades.
public partial class UntitledGemGameGameScreen
{
  // Turret sprite (Textures/Railgun/railgun.png): it points right and turns on the
  // centre of its breech. Positions below are hull texels from that pivot: along the
  // barrel, and across it (positive is the lower rail when the gun points right).
  private static readonly Vector2 RailgunOrigin = new(7.5f, 8.5f);
  private const float RailgunMuzzle = 56.5f;
  private const float RailgunBreechFront = 7.5f;
  private const float RailgunConductor = 3f;
  private const float RailgunRail = 5f;
  private static readonly float[] RailgunCoils = [16f, 26f];
  private static readonly float[] RailgunCells = [-3f, 1f, 5f];
  private static readonly float[] RailgunSides = [-1f, 1f];

  private const float RailgunRoundSpeed = 3600f;
  private const float RailgunRecoilTexels = 9f;
  private const float RailgunRecoilSeconds = 0.7f;
  private const float RailgunHeatSeconds = 1.6f;
  private const float RailgunTrailSeconds = 1.3f;
  private const int MaxRailTrails = 6;

  private static readonly Color RailGlow = new(110, 180, 255);
  private static readonly Color RailArcColor = new(190, 230, 255);
  private static readonly Color RailHeatWhite = new(255, 240, 220);
  private static readonly Color RailHeatOrange = new(255, 140, 50);
  private static readonly Color RailHeatRed = new(170, 40, 25);

  private sealed class RailTrail
  {
    public Vector2 Start, End;
    public float Age, Flight, Phase;
  }

  private float railgunCharge;
  // Seconds into the wind-up; negative while the gun is charging.
  private float railgunWindUp = -1f;
  private Vector2 railgunTarget;
  private float railgunAim;
  private float railgunSinceFire = float.MaxValue;
  private Vector2 railgunFiredFrom;
  private readonly List<RailTrail> railTrails = new();

  private bool RailgunWindingUp => railgunWindUp >= 0f;

  private float RailgunFireRate()
    => PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Railgun));

  private float RailgunChargeTime() => MainShipWeapons.RailgunChargeTime(RailgunFireRate());

  private float RailgunWindUpTime() => MainShipWeapons.RailgunWindUpTime(RailgunFireRate());

  private Vector2 RailgunPivot() => HullMount(14f, 14f);

  private Vector2 RailgunDirection() => PlanetDirection(railgunAim);

  private Vector2 RailgunMuzzlePoint() => RailgunPivot() + RailgunDirection() * RailgunMuzzle * HullScale();

  // Between shots the gun tracks the near side of the planet.
  private Vector2 RailgunIdleTarget()
    => PaintedTargetActive ? paintedPlanetTarget
      : PlanetPos + PlanetDirection(PlanetFacingAngle()) * PlanetRadius * 0.9f;

  private float RailgunAimAt(Vector2 target)
  {
    var toTarget = target - RailgunPivot();
    return MathF.Atan2(toTarget.Y, toTarget.X);
  }

  private void UpdateRailgun(float dt, UpgradesGeneratorUpgrades upgrades)
  {
    if (!upgrades.Railgun)
    {
      railgunCharge = 0f;
      railgunWindUp = -1f;
      return;
    }
    if (!RailgunWindingUp)
    {
      railgunCharge = Math.Min(1f, railgunCharge + dt / RailgunChargeTime());
      if (railgunCharge >= 1f)
        StartRailgunWindUp();
    }
    else if ((railgunWindUp += dt) >= RailgunWindUpTime())
      FireRailgun();
  }

  private void StartRailgunWindUp()
  {
    railgunCharge = 1f;
    railgunWindUp = 0f;
    railgunTarget = AutomaticPlanetTarget(0.3f);
  }

  // A bonus round (Sympathetic Fire) leaves the gun's own charge alone.
  private void FireRailgun(bool bonus = false)
  {
    int firePower = SignalStats.FirePower(MainShipWeapon.Railgun);
    int gems = AutomaticWeaponYield(MainShipWeapons.RailgunGems(UpgradeManager.Instance.UG, firePower));
    Vector2 target = PaintedTargetActive ? paintedPlanetTarget : bonus ? AutomaticPlanetTarget(0.3f) : railgunTarget;
    if (!bonus)
    {
      railgunCharge = 0f;
      railgunWindUp = -1f;
    }
    // The round leaves straight down the barrel.
    railgunAim = RailgunAimAt(target);
    Vector2 muzzle = RailgunMuzzlePoint();
    ReleaseConstellation(target);
    LaunchPlanetShot(PlanetShotKind.Rail, muzzle, target, gems, firePower);
    if (railTrails.Count >= MaxRailTrails) railTrails.RemoveAt(0);
    railTrails.Add(new RailTrail
    {
      Start = muzzle,
      End = target,
      Flight = Math.Max(0.05f, Vector2.Distance(muzzle, target) / RailgunRoundSpeed),
      Phase = Random.Shared.NextSingle() * MathHelper.TwoPi,
    });
    railgunSinceFire = 0f;
    railgunFiredFrom = muzzle;
    // Main Battery Relay: the rest of the arsenal answers the railgun.
    if (UpgradeManager.Instance.UGM.MainBatteryRelay)
      FireAllWeapons(false, PrestigeTalentEffects.RelayVolleyShells);
  }

  // Capture `event: railgun`: an owned gun winds up as its own charge would; one
  // that is not owned fires at once.
  private void CaptureFireRailgun()
  {
    if (!UpgradeManager.Instance.UG.Railgun)
    {
      StartRailgunWindUp();
      FireRailgun();
    }
    else if (!RailgunWindingUp)
      railgunCharge = 1f;
  }

  // Runs even while weapons are held, so trails, recoil and heat play out.
  private void UpdateRailgunEffects(float dt)
  {
    if (railgunSinceFire < float.MaxValue) railgunSinceFire += dt;
    for (int i = railTrails.Count - 1; i >= 0; i--)
      if ((railTrails[i].Age += dt) >= railTrails[i].Flight + RailgunTrailSeconds)
        railTrails.RemoveAt(i);
    if (m_homeBaseEntity == null) return;
    // The turret swings onto its target quickly once it starts winding up.
    float desired = RailgunAimAt(RailgunWindingUp && !PaintedTargetActive ? railgunTarget : RailgunIdleTarget());
    railgunAim += MathHelper.WrapAngle(desired - railgunAim) * (1f - MathF.Exp(-dt * (RailgunWindingUp ? 14f : 4f)));
  }

  private void ClearRailgun()
  {
    railgunCharge = 0f;
    railgunWindUp = -1f;
    railgunSinceFire = float.MaxValue;
    railTrails.Clear();
  }

  // ---- Drawing ----

  // The gun slams back at once, then eases forward into battery.
  private float RailgunRecoil()
  {
    if (railgunSinceFire >= RailgunRecoilSeconds) return 0f;
    float home = 1f - railgunSinceFire / RailgunRecoilSeconds;
    return Math.Clamp(railgunSinceFire / 0.03f, 0f, 1f) * home * home * RailgunRecoilTexels;
  }

  // A repeatable random value in [0, 1) for flickering arcs and bolts.
  private static float FlickerNoise(int a, int b)
  {
    uint h = unchecked((uint)(a * 374761393 + b * 668265263));
    h = unchecked((h ^ (h >> 13)) * 1274126177u);
    return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
  }

  private void DrawRailgun(float feather)
  {
    bool mounted = UpgradeManager.Instance.UG.Railgun && GameStarted;
    if (!mounted && railTrails.Count == 0) return;

    float scale = HullScale();
    Vector2 direction = RailgunDirection();
    Vector2 across = new(-direction.Y, direction.X);
    float windUp = RailgunWindingUp ? Math.Clamp(railgunWindUp / Math.Max(0.01f, RailgunWindUpTime()), 0f, 1f) : 0f;
    // The turret shudders harder as the wind-up peaks.
    int frame = (int)(planetAge * 60f);
    Vector2 shudder = windUp > 0f
      ? new Vector2(FlickerNoise(frame, 1) - 0.5f, FlickerNoise(frame, 2) - 0.5f) * (1.2f * windUp * windUp * scale)
      : Vector2.Zero;
    Vector2 pivot = RailgunPivot() - direction * RailgunRecoil() * scale + shudder;
    Vector2 At(float along, float side) => pivot + (direction * along + across * side) * scale;

    if (mounted && IsReady(TextureCache.Railgun))
    {
      m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        transformMatrix: m_camera.GetViewMatrix());
      m_spriteBatch.Draw(TextureCache.Railgun.Value, pivot, null, Color.White, railgunAim, RailgunOrigin, scale,
        SpriteEffects.None, 0f);
      m_spriteBatch.End();
    }

    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    foreach (var trail in railTrails)
      DrawRailTrail(trail, feather);
    if (mounted)
    {
      DrawRailgunCharge(At, windUp, feather, scale);
      if (windUp > 0f)
        DrawRailgunWindUp(At, across, windUp, feather, scale);
      DrawRailgunHeat(At, feather, scale);
    }
    DrawRailgunRelease(direction, across, feather, scale);
    m_shapeBatch.End();
  }

  // The capacitor cells in the breech fill as the gun charges.
  private void DrawRailgunCharge(Func<float, float, Vector2> at, float windUp, float feather, float scale)
  {
    float charge = RailgunWindingUp ? 1f : railgunCharge;
    for (int i = 0; i < RailgunCells.Length; i++)
    {
      float fill = Math.Clamp(charge * (RailgunCells.Length + 1) - (i + 1), 0f, 1f);
      if (fill <= 0f) continue;
      float pulse = 0.75f + 0.25f * MathF.Sin(planetAge * (4f + 20f * windUp) + i * 1.7f);
      m_shapeBatch.FillCircle(at(RailgunCells[i], 0f), (1.1f + 0.6f * windUp) * scale,
        Color.Lerp(RailGlow, Color.White, windUp) * (fill * pulse * (0.55f + 0.45f * windUp)),
        Math.Max(feather, 1.5f * scale));
    }
    // A faint hum along the rails as the charge nears full.
    float hum = MathF.Pow(charge, 4f) * (1f - windUp) * 0.25f;
    if (hum > 0.01f)
      foreach (float sign in RailgunSides)
      {
        float side = sign * RailgunConductor;
        m_shapeBatch.FillLine(at(RailgunBreechFront, side), at(RailgunMuzzle - 1f, side), 0.5f * scale,
          RailGlow * hum, Math.Max(feather, scale));
      }
  }

  private void DrawRailgunWindUp(Func<float, float, Vector2> at, Vector2 across, float windUp, float feather,
    float scale)
  {
    // Energy runs up the rails from the breech, reaching the muzzle at 70%.
    float run = Smooth(0f, 0.7f, windUp);
    float front = MathHelper.Lerp(RailgunBreechFront, RailgunMuzzle - 1f, run);
    float brightness = 0.45f + 0.55f * windUp;
    foreach (float sign in RailgunSides)
    {
      float side = sign * RailgunConductor;
      m_shapeBatch.FillLine(at(RailgunBreechFront, side), at(front, side), 0.7f * scale,
        Color.Lerp(RailGlow, Color.White, windUp * 0.5f) * brightness, Math.Max(feather, 1.6f * scale));
      if (run < 1f)
        m_shapeBatch.FillCircle(at(front, side), 1.8f * scale, Color.White * 0.9f, Math.Max(feather, 1.5f * scale));
    }

    // Each coil flashes white as the energy reaches it, then keeps glowing.
    foreach (float coil in RailgunCoils)
    {
      if (front < coil) continue;
      float flash = Math.Clamp(1f - (front - coil) / 12f, 0f, 1f);
      foreach (float sign in RailgunSides)
        m_shapeBatch.FillCircle(at(coil, sign * (RailgunConductor + 1f)), (1.2f + 2.2f * flash) * scale,
          Color.Lerp(RailGlow, Color.White, flash) * (0.6f + 0.4f * flash), Math.Max(feather, 2f * scale));
    }

    // Arcs jump between the rails behind the front, more and brighter as it builds.
    int arcs = 1 + (int)(windUp * 6f);
    int flicker = (int)(planetAge * 30f);
    for (int i = 0; i < arcs; i++)
    {
      float x = MathHelper.Lerp(RailgunBreechFront + 1f, front, FlickerNoise(i, flicker));
      var previous = at(x, -RailgunConductor);
      for (int step = 1; step <= 3; step++)
      {
        float side = -RailgunConductor + step * (2f * RailgunConductor / 3f);
        float drift = step < 3 ? (FlickerNoise(i * 7 + step, flicker) - 0.5f) * 4f : (FlickerNoise(i, flicker + 1) - 0.5f) * 2f;
        var next = at(x + drift, side);
        m_shapeBatch.FillLine(previous, next, 0.35f * scale, RailArcColor * (0.55f + 0.45f * windUp), feather);
        previous = next;
      }
    }

    // Light gathers at the muzzle, fed by motes spiralling in.
    float gather = Smooth(0.35f, 1f, windUp);
    if (gather <= 0f) return;
    var muzzle = at(RailgunMuzzle, 0f);
    m_shapeBatch.FillCircle(muzzle, (1.5f + 7f * gather) * scale, RailGlow * (0.5f * gather),
      Math.Max(feather, 5f * scale));
    m_shapeBatch.FillCircle(muzzle, (0.6f + 2.8f * gather) * scale, Color.White * gather,
      Math.Max(feather, 1.5f * scale));
    for (int i = 0; i < 8; i++)
    {
      float phase = (planetAge * 2.4f + i / 8f) % 1f;
      float angle = i * 2.39996f + phase * 2.2f;
      var mote = muzzle + PlanetDirection(angle) * ((1f - phase) * 22f + 3f) * scale;
      m_shapeBatch.FillCircle(mote, (0.5f + 0.6f * phase) * scale, RailArcColor * (gather * phase),
        Math.Max(feather, scale));
    }
    // A flare streak across the muzzle in the last moments.
    float flare = Smooth(0.8f, 1f, windUp);
    if (flare > 0f)
      m_shapeBatch.FillLine(muzzle - across * 16f * flare * scale, muzzle + across * 16f * flare * scale,
        0.5f * scale, Color.White * flare, Math.Max(feather, 1.5f * scale));
  }

  // After a shot the rails glow white-hot and cool through orange to dull red.
  private void DrawRailgunHeat(Func<float, float, Vector2> at, float feather, float scale)
  {
    if (railgunSinceFire >= RailgunHeatSeconds) return;
    float heat = 1f - railgunSinceFire / RailgunHeatSeconds;
    var color = heat > 0.6f
      ? Color.Lerp(RailHeatOrange, RailHeatWhite, (heat - 0.6f) / 0.4f)
      : Color.Lerp(RailHeatRed, RailHeatOrange, heat / 0.6f);
    // The muzzle end cools last.
    foreach (float sign in RailgunSides)
    {
      float side = sign * RailgunRail;
      m_shapeBatch.FillLine(at(RailgunBreechFront + 2f, side), at(RailgunMuzzle - 1f, side), 2.5f * scale,
        color * (0.35f * heat * heat), Math.Max(feather, 4f * scale));
      m_shapeBatch.FillLine(at(RailgunBreechFront + 2f, side), at(RailgunMuzzle - 1f, side), 1f * scale,
        color * (0.8f * heat * heat), Math.Max(feather, 1.5f * scale));
      m_shapeBatch.FillLine(at(RailgunMuzzle - 20f, side), at(RailgunMuzzle - 1f, side), 1.1f * scale,
        color * heat, Math.Max(feather, 1.5f * scale));
    }
  }

  // The muzzle flash, side jets and shock ring where the round left the barrel.
  private void DrawRailgunRelease(Vector2 direction, Vector2 across, float feather, float scale)
  {
    float t = railgunSinceFire;
    if (t >= 0.45f) return;
    var muzzle = railgunFiredFrom;
    float flash = 1f - Math.Clamp(t / 0.14f, 0f, 1f);
    if (flash > 0f)
    {
      m_shapeBatch.FillCircle(muzzle, (16f + 20f * flash) * scale, RailGlow * (0.3f * flash),
        Math.Max(feather, 16f * scale));
      m_shapeBatch.FillCircle(muzzle, (2f + 4f * flash) * scale, Color.White * flash, Math.Max(feather, 2f * scale));
      // A spike of light down the line of fire, and a thinner one back over the gun.
      m_shapeBatch.FillLine(muzzle, muzzle + direction * 110f * scale, (1f + 3f * flash) * scale,
        Color.Lerp(RailGlow, Color.White, flash) * flash, Math.Max(feather, 3f * scale));
      m_shapeBatch.FillLine(muzzle, muzzle - direction * 30f * scale, (0.5f + 1.5f * flash) * scale,
        RailArcColor * (0.7f * flash), Math.Max(feather, 2f * scale));
    }
    // Gas vents sideways out of the muzzle.
    float jets = Math.Clamp(t / 0.2f, 0f, 1f);
    if (jets < 1f)
      foreach (float sign in RailgunSides)
      {
        var jetStart = muzzle + across * sign * 3f * scale;
        var jetEnd = jetStart + (across * sign * (6f + 20f * jets) - direction * 4f * jets) * scale;
        m_shapeBatch.FillLine(jetStart, jetEnd, (2f - 1.4f * jets) * scale, RailArcColor * (1f - jets),
          Math.Max(feather, 2f * scale));
      }
    float ring = t / 0.45f;
    float ringEase = 1f - (1f - ring) * (1f - ring);
    m_shapeBatch.BorderCircle(muzzle, (4f + 60f * ringEase) * scale, RailGlow * (0.8f * (1f - ring)),
      (2.2f - 1.4f * ring) * scale, Math.Max(feather, 1.5f * scale));
  }

  // The round streaks to the planet; a beam flashes along its path and an ion
  // spiral lingers around it, widening as it fades.
  private void DrawRailTrail(RailTrail trail, float feather)
  {
    var path = trail.End - trail.Start;
    float length = path.Length();
    if (length < 1f) return;
    var direction = path / length;
    var across = new Vector2(-direction.Y, direction.X);
    float head = length * Math.Clamp(trail.Age / trail.Flight, 0f, 1f);
    var headPoint = trail.Start + direction * head;

    float life = Math.Clamp(trail.Age / (trail.Flight + RailgunTrailSeconds), 0f, 1f);
    float fade = MathF.Pow(1f - life, 1.3f);
    float beam = 1f - Math.Clamp(trail.Age / 0.4f, 0f, 1f);
    m_shapeBatch.FillLine(trail.Start, headPoint, 8f * beam + 2f, RailGlow * (0.4f * beam + 0.12f * fade),
      Math.Max(feather, 8f));
    m_shapeBatch.FillLine(trail.Start, headPoint, 0.8f + 2f * beam,
      Color.Lerp(RailGlow, Color.White, beam) * (beam + 0.3f * fade), feather);

    // The spiral starts tight at the muzzle, is widest down range and widens as it fades.
    float radius = 4f + 12f * (1f - (1f - life) * (1f - life));
    for (float s = 3f; s < head; s += 4.5f)
    {
      float angle = s * 0.085f + trail.Phase;
      float depth = MathF.Cos(angle);
      float reach = Math.Min(1f, 0.3f + s / 100f);
      var point = trail.Start + direction * s + across * (MathF.Sin(angle) * radius * reach);
      m_shapeBatch.FillCircle(point, 1.6f + 0.8f * depth,
        Color.Lerp(RailGlow, RailArcColor, 0.5f + 0.5f * depth) * (fade * (0.65f + 0.35f * depth)),
        Math.Max(feather, 1.5f));
    }

    if (trail.Age < trail.Flight)
    {
      var tail = trail.Start + direction * Math.Max(0f, head - 120f);
      m_shapeBatch.FillLine(tail, headPoint, 6f, RailGlow * 0.6f, 10f);
      m_shapeBatch.FillLine(Vector2.Lerp(tail, headPoint, 0.4f), headPoint, 3f, Color.White, feather);
    }
  }
}
