using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

// The railgun (tuning in MainShipWeapons): a turret on the homebase's right flank.
// Its charge fills the capacitor cells in the breech; the end of each cycle is a
// visible wind-up that follows its own sound: it locks onto its target, then as the
// sound rises energy surges up the rails from the breech to the muzzle, arcs crackle
// between the rails and a vortex of energy is drawn into the muzzle while the turret
// shudders. As the sound drops away the vortex collapses into a hard white star and
// the gun goes still for the held breath before the shot. Then it fires one
// hypersonic round: a star flash and a shock ring racing out ahead of the gun, a hard
// recoil, rails glowing white-hot and cooling through orange, and the round tearing
// an ionised channel to the planet that flashes white, cools to blue and unwinds
// into a double helix of ions that disperses as the sound dies. The muzzle and the
// channel are shaded by RailgunBolt.fx; the breech and rails are shapes on the turret.
// Capacitor Bank (a core shard) banks two full charges in the breech cells and fires
// them after the third as a barrage. Kinetic Harvest and Kinetic Battery feed the gun
// from gems collected by hand; Recoil Harvest rewards hand collection after each round.
public partial class UntitledGemGameGameScreen
{
  // Charge and heat positions along/across the barrel, measured from the breech.
  // RailgunModelScale fits these effect coordinates to the Synty turret render.
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
  // The wind-up sound (RailgunWindUp at its pitch) lasts this long. It rises to a
  // climax and drops off before its end, so it starts late in a longer wind-up: its
  // drop-off is the held breath right before the shot, whatever the fire rate.
  private const float RailgunWindUpSoundSeconds = 0.72f;
  // The shader's time spans: how long the muzzle plays out after a shot, the trail's
  // age range and how long the channel lingers after the round lands (RailgunBolt.fx:
  // MUZZLE_SPAN, TRAIL_SPAN and TRAIL_FADE).
  private const float RailgunMuzzleSeconds = 1f;
  private const float RailgunTrailSpan = 2f;
  private const float RailgunTrailSeconds = 0.9f;
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
  private bool railgunWindUpSounded;
  private Vector2 railgunTarget;
  private float railgunAim;
  private float railgunSinceFire = float.MaxValue;
  private Vector2 railgunFiredFrom, railgunFiredAlong = Vector2.UnitX;
  private readonly List<RailTrail> railTrails = new();
  private SdfLineRenderer railgunRenderer;
  // Capacitor Bank: full charges banked so far, and barrage rounds still to fire.
  private int railgunBank;
  private int railgunBarrage;
  private float railgunBarrageTimer;
  // Kinetic Battery: the damage bonus stored for the next round.
  private float kineticBattery;
  // Recoil Harvest: seconds left of the window after a round.
  private float recoilHarvest;

  private bool RailgunWindingUp => railgunWindUp >= 0f;

  private float RailgunFireRate()
    => PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Railgun));

  private float RailgunChargeTime() => MainShipWeapons.RailgunChargeTime(RailgunFireRate());

  private float RailgunWindUpTime() => MainShipWeapons.RailgunWindUpTime(RailgunFireRate());

  // Seconds into the wind-up at which its sound starts, so that the sound ends on the shot.
  private float RailgunWindUpSoundStart() => Math.Max(0f, RailgunWindUpTime() - RailgunWindUpSoundSeconds);

  // 0..1 through the wind-up sound: the effects follow the sound, not the clock.
  private float RailgunWindUpPhase()
  {
    if (!RailgunWindingUp) return 0f;
    float start = RailgunWindUpSoundStart();
    return Math.Clamp((railgunWindUp - start) / Math.Max(0.01f, RailgunWindUpTime() - start), 0f, 1f);
  }

  // Charge past full spills into the bank with Capacitor Bank.
  private float RailgunChargeCap
    => UpgradeManager.Instance.UG.RailgunCapacitor ? MainShipWeapons.CapacitorRounds - railgunBank : 1f;

  private Vector2 RailgunPivot() => TurretPivot(railgunTurret);

  // Fit the effect coordinates to the turret's physical muzzle distance.
  private float RailgunModelScale => railgunTurret.Muzzle / RailgunMuzzle;

  private Vector2 RailgunDirection() => PlanetDirection(railgunAim);

  private Vector2 RailgunMuzzlePoint()
    => RailgunPivot() + RailgunDirection() * RailgunMuzzle * HullScale() * RailgunModelScale;

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
    recoilHarvest = Math.Max(0f, recoilHarvest - dt);
    PrestigeTalentEffects.RecoilHarvestActive = recoilHarvest > 0f;
    if (!upgrades.Railgun)
    {
      railgunCharge = 0f;
      railgunWindUp = -1f;
      railgunBank = railgunBarrage = 0;
      return;
    }
    if (railgunBarrage > 0)
    {
      if ((railgunBarrageTimer -= dt) <= 0f) FireBarrageRound();
      return;
    }
    if (!RailgunWindingUp)
    {
      railgunCharge = Math.Min(RailgunChargeCap, railgunCharge + dt / RailgunChargeTime());
      // Capacitor Bank: full charges go into the breech cells until only the last is left.
      while (upgrades.RailgunCapacitor && railgunCharge >= 1f && railgunBank < MainShipWeapons.CapacitorRounds - 1)
      {
        railgunBank++;
        railgunCharge -= 1f;
      }
      if (railgunCharge >= 1f)
        StartRailgunWindUp();
    }
    else
    {
      railgunWindUp += dt;
      if (!railgunWindUpSounded && railgunWindUp >= RailgunWindUpSoundStart())
      {
        railgunWindUpSounded = true;
        AudioManager.Instance.PlaySound(SoundEvent.RailgunWindUp);
      }
      if (railgunWindUp >= RailgunWindUpTime())
        FireRailgun();
    }
  }

  private void StartRailgunWindUp()
  {
    railgunCharge = 1f;
    railgunWindUp = 0f;
    railgunWindUpSounded = false;
    railgunTarget = AutomaticPlanetTarget(0.3f);
  }

  // A bonus round (Sympathetic Fire) leaves the gun's own charge alone; barrage rounds
  // (Capacitor Bank) follow the gun's own round out.
  private void FireRailgun(bool bonus = false, bool barrage = false)
  {
    AudioManager.Instance.PlaySound(SoundEvent.RailgunFire);
    var upgrades = UpgradeManager.Instance.UG;
    int firePower = SignalStats.FirePower(MainShipWeapon.Railgun);
    double gems = AutomaticWeaponYield(MainShipWeapons.RailgunGems(upgrades, firePower), arsenal: !bonus);
    if (!bonus && upgrades.RailgunCapacitor) gems *= MainShipWeapons.CapacitorRoundBonus;
    // Kinetic Battery: the stored bonus goes into this round.
    gems *= 1f + kineticBattery;
    kineticBattery = 0f;
    if (Talents.RecoilHarvest) recoilHarvest = PrestigeTalentEffects.RecoilHarvestSeconds;
    Vector2 target = PaintedTargetActive ? paintedPlanetTarget
      : bonus || barrage ? AutomaticPlanetTarget(0.3f) : railgunTarget;
    if (!bonus && !barrage)
    {
      railgunCharge = 0f;
      railgunWindUp = -1f;
      // Capacitor Bank: the banked rounds follow this one out.
      if (upgrades.RailgunCapacitor && railgunBank > 0)
      {
        railgunBarrage = railgunBank;
        railgunBank = 0;
        railgunBarrageTimer = MainShipWeapons.CapacitorBarrageInterval;
      }
    }
    // The round leaves straight down the barrel.
    railgunAim = RailgunAimAt(target);
    Vector2 muzzle = RailgunMuzzlePoint();
    ReleaseConstellation(target);
    LaunchPlanetShot(PlanetShotKind.Rail, muzzle, target, (int)Math.Min(int.MaxValue, Math.Ceiling(gems)), firePower);
    if (railTrails.Count >= MaxRailTrails) railTrails.RemoveAt(0);
    railTrails.Add(new RailTrail
    {
      Start = muzzle,
      End = target,
      Flight = Math.Max(0.05f, Vector2.Distance(muzzle, target) / RailgunRoundSpeed),
      Phase = Random.Shared.NextSingle(),
    });
    railgunSinceFire = 0f;
    railgunFiredFrom = muzzle;
    var along = target - muzzle;
    railgunFiredAlong = along.LengthSquared() > 1e-4f ? Vector2.Normalize(along) : RailgunDirection();
    // Main Battery Relay: the rest of the arsenal answers the railgun.
    if (UpgradeManager.Instance.UGM.MainBatteryRelay)
      FireAllWeapons(false, PrestigeTalentEffects.RelayVolleyShells, arsenal: true);
  }

  private void FireBarrageRound()
  {
    railgunBarrage--;
    railgunBarrageTimer = MainShipWeapons.CapacitorBarrageInterval;
    FireRailgun(barrage: true);
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
    railgunCharge = kineticBattery = recoilHarvest = railgunBarrageTimer = 0f;
    railgunBank = railgunBarrage = 0;
    PrestigeTalentEffects.RecoilHarvestActive = false;
    railgunWindUp = -1f;
    railgunWindUpSounded = false;
    railgunSinceFire = float.MaxValue;
    railgunAim = 0f;
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
    bool mounted = UpgradeManager.Instance.UG.Railgun && m_homeBaseEntity != null && !planetConsumed;
    if (!mounted && railTrails.Count == 0) return;

    float scale = HullScale() * RailgunModelScale;
    float aim = railgunAim + HullRotation();
    Vector2 direction = PlanetDirection(aim);
    Vector2 across = new(-direction.Y, direction.X);
    float windUp = RailgunWindUpPhase();
    // The turret shudders harder as the charge builds, then goes still for the held
    // breath before the shot.
    float shudderAmount = Smooth(0.08f, 0.72f, windUp) * (1f - Smooth(0.76f, 0.9f, windUp));
    int frame = (int)(planetAge * 60f);
    Vector2 shudder = shudderAmount > 0f
      ? new Vector2(FlickerNoise(frame, 1) - 0.5f, FlickerNoise(frame, 2) - 0.5f) * (1.5f * shudderAmount * scale)
      : Vector2.Zero;
    Vector2 pivot = RailgunPivot() - direction * RailgunRecoil() * scale + shudder;
    Vector2 At(float along, float side) => pivot + (direction * along + across * side) * scale;

    // Keep the pack turret when the debug toggle selects the old pixel-art hulls too.
    if (mounted && TextureCache.Fleet != null)
    {
      m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
        transformMatrix: m_camera.GetViewMatrix());
      DrawTurretSprite(railgunTurret, RailgunPivot() + shudder, pivot, aim);
      m_spriteBatch.End();
    }

    bool live = mounted && GameStarted;
    if (live)
    {
      m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
      DrawRailgunCharge(At, windUp, feather, scale);
      if (windUp > 0f)
        DrawRailgunWindUp(At, windUp, feather, scale);
      DrawRailgunHeat(At, feather, scale);
      m_shapeBatch.End();
    }
    DrawRailgunEnergy(live, At(RailgunMuzzle, 0f), direction, windUp, scale);
  }

  // The capacitor cells in the breech fill as the gun charges.
  private void DrawRailgunCharge(Func<float, float, Vector2> at, float windUp, float feather, float scale)
  {
    float charge = RailgunWindingUp ? 1f : railgunCharge;
    bool banking = UpgradeManager.Instance.UG.RailgunCapacitor;
    // Locked on and waiting for the sound, the cells pulse fast; they flare with it.
    float locked = RailgunWindingUp ? 1f : 0f;
    for (int i = 0; i < RailgunCells.Length; i++)
    {
      // With Capacitor Bank each cell holds a banked round, and empties as the barrage fires.
      float fill = !banking ? Math.Clamp(charge * (RailgunCells.Length + 1) - (i + 1), 0f, 1f)
        : railgunBarrage > 0 ? (i < railgunBarrage ? 1f : 0f)
        : Math.Clamp(railgunBank + charge - i, 0f, 1f);
      if (fill <= 0f) continue;
      float pulse = 0.75f + 0.25f * MathF.Sin(planetAge * (4f + 16f * locked + 20f * windUp) + i * 1.7f);
      m_shapeBatch.FillCircle(at(RailgunCells[i], 0f), (1.1f + 0.3f * locked + 0.6f * windUp) * scale,
        Color.Lerp(RailGlow, Color.White, windUp) * (fill * pulse * (0.55f + 0.2f * locked + 0.45f * windUp)),
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

  // The rails and breech through the wind-up sound: energy surges up the rails on
  // its attack, arcs crackle between them as it climbs, and they settle into a hard
  // white charge as it drops away.
  private void DrawRailgunWindUp(Func<float, float, Vector2> at, float windUp, float feather, float scale)
  {
    float run = Smooth(0f, 0.2f, windUp);
    float build = Smooth(0.1f, 0.75f, windUp);
    float hold = Smooth(0.78f, 1f, windUp);
    float front = MathHelper.Lerp(RailgunBreechFront, RailgunMuzzle - 1f, run);
    float brightness = 0.5f + 0.5f * build;
    foreach (float sign in RailgunSides)
    {
      float side = sign * RailgunConductor;
      m_shapeBatch.FillLine(at(RailgunBreechFront, side), at(front, side), (0.7f + 0.3f * build - 0.4f * hold) * scale,
        Color.Lerp(RailGlow, Color.White, 0.4f * build + 0.6f * hold) * brightness, Math.Max(feather, (1.6f - 0.6f * hold) * scale));
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
          Color.Lerp(RailGlow, Color.White, flash) * ((0.6f + 0.4f * flash) * (1f - 0.5f * hold)),
          Math.Max(feather, 2f * scale));
    }

    // Arcs jump between the rails behind the front, more and brighter as the sound
    // climbs, and die away as it drops.
    float arcing = build * (1f - hold);
    int arcs = arcing > 0f ? 1 + (int)(arcing * 6f) : 0;
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
        m_shapeBatch.FillLine(previous, next, 0.35f * scale, RailArcColor * (0.55f + 0.45f * arcing), feather);
        previous = next;
      }
    }
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

  // The muzzle and the rounds' channels, shaded by RailgunBolt.fx on SDF quads: the
  // wind-up vortex and star at the muzzle, the star flash, shock ring and vents of a
  // shot where the round left, and each round with the ion channel it tears open.
  private void DrawRailgunEnergy(bool live, Vector2 muzzle, Vector2 direction, float windUp, float scale)
  {
    var effect = EffectCache.RailgunBoltFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    bool charging = live && windUp > 0f;
    bool firing = railgunSinceFire < RailgunMuzzleSeconds;
    if (!charging && !firing && railTrails.Count == 0) return;
    railgunRenderer ??= new SdfLineRenderer(GraphicsDevice, effect.Value, maxLines: MaxRailTrails + 2)
      { PulseExtraPadding = 0f };

    if (railTrails.Count > 0)
    {
      effect.Value.CurrentTechnique = effect.Value.Techniques["RailTrail"];
      // Room for the helix, the haze and the impact bloom around the line of fire.
      railgunRenderer.BaseGlowPadding = 44f * scale;
      railgunRenderer.Begin(m_camera.ViewProjection(), planetAge);
      foreach (var trail in railTrails)
      {
        // r = age over the trail's span, g = seed, b = flight time over half a second.
        var data = new Color(Math.Clamp(trail.Age / RailgunTrailSpan, 0f, 1f), trail.Phase,
          Math.Clamp(trail.Flight / 0.5f, 0f, 1f), 1f);
        railgunRenderer.DrawLine(trail.Start, trail.End, scale, data, RailGlow,
          pulseProgress: Math.Clamp(trail.Age / trail.Flight, 0f, 1f));
      }
      railgunRenderer.End();
    }

    if (!charging && !firing) return;
    effect.Value.CurrentTechnique = effect.Value.Techniques["RailMuzzle"];
    // Room for the shock ring and the vortex around the muzzle.
    railgunRenderer.BaseGlowPadding = 96f * scale;
    railgunRenderer.Begin(m_camera.ViewProjection(), planetAge);
    // r = wind-up phase, g = time since the shot over its span (a zero byte means no shot).
    if (charging)
      railgunRenderer.DrawLine(muzzle, muzzle + direction, scale, new Color(windUp, 0f, 0f, 1f), RailGlow);
    if (firing)
      railgunRenderer.DrawLine(railgunFiredFrom, railgunFiredFrom + railgunFiredAlong, scale,
        new Color(0f, Math.Clamp(railgunSinceFire / RailgunMuzzleSeconds, 1f / 255f, 1f), 0f, 1f), RailGlow);
    railgunRenderer.End();
  }
}
