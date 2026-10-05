using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// The Arc Harpoon alternates between a short reload, a tethered projectile, and
// a series of electrical mining pulses while its anchor is buried in the planet.
public partial class UntitledGemGameGameScreen
{
  private static readonly Color ArcHarpoonGlow = new(70, 220, 255);
  private static readonly Color ArcHarpoonCore = new(205, 250, 255);
  private const float HarpoonPulseFlashSeconds = 0.32f;

  private float harpoonReload;
  private float harpoonPulseTimer;
  private float harpoonPulseFlash;
  private bool harpoonInFlight;
  private bool harpoonEmbedded;
  private int harpoonPulses;
  private int harpoonFirePower;
  private Vector2 harpoonTarget;
  // The flight curve; the tether follows it while the anchor is buried.
  private PlanetShot harpoonPath;
  private bool harpoonNorth;

  private Vector2 HarpoonMount() => HullMount(25f, -7f);

  private void UpdateArcHarpoon(float dt, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    harpoonPulseFlash = Math.Max(0f, harpoonPulseFlash - dt);
    if (!upgrades.ArcHarpoon)
    {
      ClearArcHarpoon();
      return;
    }

    if (harpoonEmbedded)
    {
      float interval = HarpoonPulseInterval();
      harpoonPulseTimer += dt;
      while (harpoonEmbedded && harpoonPulseTimer >= interval)
      {
        harpoonPulseTimer -= interval;
        PulseArcHarpoon(bounds, upgrades);
      }
      return;
    }
    if (harpoonInFlight) return;

    harpoonReload += dt;
    float reload = HarpoonReloadTime();
    if (harpoonReload < reload || PlanetGemRoom() <= 0) return;
    harpoonReload -= reload;
    harpoonInFlight = true;
    LaunchArcHarpoon(SignalStats.FirePower(MainShipWeapon.Harpoon));
  }

  // The harpoon arcs over or under the weapon lane and anchors near a pole, taking
  // turns north and south, so its tether stays clear of the laser. A painted
  // target is still hit head on.
  private void LaunchArcHarpoon(int firePower)
  {
    var start = HarpoonMount();
    if (PaintedTargetActive)
    {
      LaunchPlanetShot(PlanetShotKind.Harpoon, start, paintedPlanetTarget, 0, firePower);
      harpoonPath = planetShots[^1];
      return;
    }
    harpoonNorth = !harpoonNorth;
    float offset = MathHelper.PiOver2 - 0.15f - Random.Shared.NextSingle() * 0.25f;
    var outward = PlanetDirection(PlanetFacingAngle() + (harpoonNorth ? offset : -offset));
    var shot = new PlanetShot
    {
      Kind = PlanetShotKind.Harpoon,
      FirePower = firePower,
      Start = start,
      Control1 = start + outward * PlanetRadius * 1.6f + (PlanetPos - start) * 0.25f,
      End = PlanetPos + outward * PlanetRadius * 0.92f,
    };
    shot.Control2 = shot.End + outward * PlanetRadius * 1.5f;
    float length = 0f;
    for (int i = 1; i <= 8; i++)
      length += Vector2.Distance(Bezier(shot, (i - 1) / 8f), Bezier(shot, i / 8f));
    shot.Duration = Math.Max(0.05f, length / HarpoonShotSpeed);
    harpoonPath = shot;
    AddPlanetShot(shot);
  }

  private float HarpoonFireRate()
    => Math.Max(0.1f, PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Harpoon)));

  private float HarpoonPulseInterval() => MainShipWeapons.HarpoonPulseInterval / HarpoonFireRate();

  // Lightning Rod halves the reload between anchors.
  private float HarpoonReloadTime()
    => MainShipWeapons.HarpoonReloadSeconds / HarpoonFireRate() / PrestigeTalentEffects.HarpoonReloadMultiplier;

  private void EmbedArcHarpoon(Vector2 target, int firePower)
  {
    harpoonRodPulses = 0;
    harpoonInFlight = false;
    harpoonEmbedded = true;
    harpoonTarget = target;
    harpoonFirePower = firePower;
    harpoonPulses = 0;
    harpoonPulseTimer = 0f;
    harpoonPulseFlash = HarpoonPulseFlashSeconds;
    PulsePlanet(0.55f, 0.15f);
    SpawnerEffects.Add(null, target, ArcHarpoonGlow, 3f, 30f, 0.3f);
  }

  private void PulseArcHarpoon(PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    int pulseNumber = harpoonPulses + 1;
    // Lightning Rod: cannon hits added extra pulses to this anchor.
    int pulses = MainShipWeapons.HarpoonPulseCount(upgrades) + harpoonRodPulses;
    int gems = AutomaticWeaponYield(harpoonFirePower);
    int qualityPower = upgrades.HarpoonDeepAnchor
      ? (int)Math.Min(int.MaxValue, (long)harpoonFirePower + harpoonPulses)
      : harpoonFirePower;
    float anchorAngle = MathF.Atan2(harpoonTarget.Y - PlanetPos.Y, harpoonTarget.X - PlanetPos.X);

    KnockGemsLoose(gems, qualityPower, bounds, 0.78f, anchorAngle, 0.5f);
    TeslaArcs(gems, qualityPower, bounds);
    if (upgrades.HarpoonForkedCurrent)
    {
      int forkGems = (int)MathF.Ceiling(gems * MainShipWeapons.HarpoonForkShare);
      float side = pulseNumber % 2 == 0 ? -1f : 1f;
      KnockGemsLoose(forkGems, qualityPower, bounds, 0.9f, anchorAngle + side * 1.05f, 0.38f);
    }

    harpoonPulses = pulseNumber;
    harpoonPulseFlash = HarpoonPulseFlashSeconds;
    PulsePlanet(0.35f + 0.05f * pulseNumber, 0.08f);
    SpawnerEffects.Add(null, harpoonTarget, ArcHarpoonCore, 3f, 24f + pulseNumber * 2f, 0.25f);
    if (harpoonPulses < pulses) return;

    if (upgrades.HarpoonCapacitorDischarge)
    {
      int overload = (int)Math.Min(PlanetGemRoom(), Math.Min(int.MaxValue,
        (long)gems * MainShipWeapons.HarpoonCapacitorBonusPulses));
      KnockGemsLoose(overload, qualityPower + 2, bounds, 1.1f, anchorAngle, 1.15f);
      planetExplosions.Add(new PlanetExplosion { Position = harpoonTarget, Scale = 1.6f });
      ShowWorldPopup(harpoonTarget, "OVERLOAD!", large: true);
      PulsePlanet(1f, 0.55f);
    }
    if (upgrades.HarpoonTectonicWinch)
    {
      int torn = (int)Math.Min(PlanetGemRoom(), Math.Min(int.MaxValue,
        (long)gems * MainShipWeapons.HarpoonWinchBonusPulses));
      KnockClusterLoose(torn, qualityPower, bounds, 0.55f, PlanetFacingAngle(), 0.32f);
      ShowWorldPopup(harpoonTarget, "TECTONIC TEAR", large: false);
    }

    harpoonEmbedded = false;
    harpoonPulseTimer = 0f;
    harpoonReload = 0f;
  }

  private void ClearArcHarpoon()
  {
    harpoonReload = harpoonPulseTimer = harpoonPulseFlash = 0f;
    harpoonInFlight = harpoonEmbedded = false;
    harpoonPulses = harpoonFirePower = 0;
    harpoonTarget = Vector2.Zero;
    harpoonPath = null;
  }

  // Called inside the additive weapon shape pass.
  private void DrawArcHarpoon(float feather)
  {
    if (harpoonPath == null || !harpoonEmbedded && !harpoonInFlight) return;
    if (!harpoonEmbedded && harpoonPath.Delay > 0f) return;
    float head = harpoonEmbedded ? 1f : Math.Clamp(harpoonPath.Age / harpoonPath.Duration, 0f, 1f);
    Vector2 end = Bezier(harpoonPath, head);
    Vector2 direction = BezierDirection(harpoonPath, head);

    // The tether follows the flight curve, wobbling with slack while it flies.
    Vector2 previous = harpoonPath.Start;
    const int segments = 24;
    for (int i = 1; i <= segments; i++)
    {
      float t = i / (float)segments;
      float slack = harpoonEmbedded ? 1.5f : 4f;
      float wave = MathF.Sin(t * MathHelper.Pi) * MathF.Sin(t * 18f - planetAge * 11f) * slack;
      var tangent = BezierDirection(harpoonPath, t * head);
      Vector2 next = Bezier(harpoonPath, t * head) + new Vector2(-tangent.Y, tangent.X) * wave;
      m_shapeBatch.FillLine(previous, next, 3.4f, new Color(15, 70, 95) * 0.8f,
        Math.Max(feather, 5f));
      m_shapeBatch.FillLine(previous, next, 1.15f, ArcHarpoonGlow * 0.8f,
        Math.Max(feather, 2f));
      previous = next;
    }

    if (harpoonEmbedded)
    {
      float interval = HarpoonPulseInterval();
      float packet = Math.Clamp(harpoonPulseTimer / interval, 0f, 1f);
      Vector2 charge = Bezier(harpoonPath, packet);
      m_shapeBatch.FillCircle(charge, 3.5f + packet * 2f, ArcHarpoonCore, Math.Max(feather, 8f));
      DrawEmbeddedHarpoon(end, feather);
      DrawHarpoonDischarge(feather);
    }
    else
    {
      Vector2 side = new(-direction.Y, direction.X);
      m_shapeBatch.FillLine(end - direction * 12f, end + direction * 5f, 3.2f, ArcHarpoonCore,
        Math.Max(feather, 5f));
      m_shapeBatch.FillLine(end - direction * 5f - side * 6f, end, 2.2f, ArcHarpoonGlow, feather);
      m_shapeBatch.FillLine(end - direction * 5f + side * 6f, end, 2.2f, ArcHarpoonGlow, feather);
    }
  }

  private void DrawEmbeddedHarpoon(Vector2 anchor, float feather)
  {
    Vector2 outward = anchor - PlanetPos;
    outward = outward.LengthSquared() > 0.01f ? Vector2.Normalize(outward) : -Vector2.UnitX;
    Vector2 side = new(-outward.Y, outward.X);
    m_shapeBatch.FillLine(anchor - outward * 5f, anchor + outward * 19f, 4f, ArcHarpoonCore,
      Math.Max(feather, 6f));
    m_shapeBatch.FillLine(anchor + outward * 8f - side * 8f, anchor + outward * 2f, 2.5f,
      ArcHarpoonGlow, feather);
    m_shapeBatch.FillLine(anchor + outward * 8f + side * 8f, anchor + outward * 2f, 2.5f,
      ArcHarpoonGlow, feather);
  }

  private void DrawHarpoonDischarge(float feather)
  {
    if (harpoonPulseFlash <= 0f) return;
    float life = harpoonPulseFlash / HarpoonPulseFlashSeconds;
    int branches = UpgradeManager.Instance.UG.HarpoonForkedCurrent ? 6 : 3;
    float anchorAngle = MathF.Atan2(harpoonTarget.Y - PlanetPos.Y, harpoonTarget.X - PlanetPos.X);
    for (int branch = 0; branch < branches; branch++)
    {
      float direction = branch % 2 == 0 ? 1f : -1f;
      float sweep = direction * (0.35f + 0.16f * branch);
      Vector2 previous = harpoonTarget;
      for (int segment = 1; segment <= 7; segment++)
      {
        float t = segment / 7f;
        float jitter = MathF.Sin(branch * 17.3f + segment * 8.1f + harpoonPulses) * 5f * life;
        Vector2 next = PlanetPos + PlanetDirection(anchorAngle + sweep * t)
          * (PlanetRadius * 0.93f + jitter);
        m_shapeBatch.FillLine(previous, next, 1.3f + life, ArcHarpoonCore * life,
          Math.Max(feather, 4f));
        previous = next;
      }
    }
    m_shapeBatch.BorderCircle(harpoonTarget, 10f + (1f - life) * 32f,
      ArcHarpoonGlow * life, 2f, Math.Max(feather, 4f));
  }
}
