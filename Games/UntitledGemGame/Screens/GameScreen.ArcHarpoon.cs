using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// The Arc Harpoon alternates between a short reload, a tethered projectile, and
// a series of electrical mining pulses while its anchor is buried in the planet.
// Twin Harpoons launches two at once, one over the top of the planet and one under
// the bottom. The pair shares one cycle: the pulses start once both have landed,
// every pulse hits at both anchors, and they let go together.
public partial class UntitledGemGameGameScreen
{
  private static readonly Color ArcHarpoonGlow = new(70, 220, 255);
  private static readonly Color ArcHarpoonCore = new(205, 250, 255);
  private const float HarpoonPulseFlashSeconds = 0.32f;
  // Twin harpoons sent at a painted target land either side of it.
  private const float HarpoonPaintedTwinSpread = 0.3f;

  // One harpoon of the volley.
  private sealed class HarpoonAnchor
  {
    // The flight curve; the tether follows it while the anchor is buried.
    public PlanetShot Path;
    public Vector2 Target;
    public bool Landed;
    // The anchor that just let go, fading out.
    public PlanetShot ReleasedPath;
    public Vector2 ReleasedTarget;
  }

  private readonly HarpoonAnchor[] harpoonAnchors = CreateHarpoonAnchors();
  // Harpoons launched in the current volley (MainShipWeapons.HarpoonCount).
  private int harpoonVolley;
  private float harpoonReload;
  private float harpoonPulseTimer;
  private float harpoonPulseFlash;
  private bool harpoonInFlight;
  private bool harpoonEmbedded;
  private int harpoonPulses;
  private int harpoonFirePower;
  private bool harpoonNorth;

  private static HarpoonAnchor[] CreateHarpoonAnchors()
  {
    var anchors = new HarpoonAnchor[MainShipWeapons.TwinHarpoons];
    for (int i = 0; i < anchors.Length; i++) anchors[i] = new HarpoonAnchor();
    return anchors;
  }

  private Vector2 HarpoonMount() => HullMount(25f, -7f);

  private void UpdateArcHarpoon(float dt, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    harpoonPulseFlash = Math.Max(0f, harpoonPulseFlash - dt);
    UpdateHarpoonRelease(dt);
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
    if (harpoonReload < reload) return;
    harpoonReload -= reload;
    harpoonInFlight = true;
    LaunchArcHarpoon(SignalStats.FirePower(MainShipWeapon.Harpoon));
  }

  private void LaunchArcHarpoon(int firePower)
  {
    harpoonVolley = MainShipWeapons.HarpoonCount(UpgradeManager.Instance.UG);
    if (harpoonVolley == 1) harpoonNorth = !harpoonNorth;
    var start = HarpoonMount();
    for (int i = 0; i < harpoonAnchors.Length; i++)
    {
      var anchor = harpoonAnchors[i];
      anchor.Landed = false;
      anchor.Path = i < harpoonVolley
        ? LaunchHarpoonShot(start, firePower, harpoonVolley == 1 ? harpoonNorth : i == 0)
        : null;
    }
  }

  // A harpoon arcs over or under the weapon lane and anchors near a pole, so its
  // tether stays clear of the laser: a single harpoon takes turns north and south,
  // twin harpoons take one pole each. A painted target is still hit head on.
  private PlanetShot LaunchHarpoonShot(Vector2 start, int firePower, bool north)
  {
    if (PaintedTargetActive)
    {
      var target = paintedPlanetTarget;
      if (harpoonVolley > 1)
      {
        var painted = paintedPlanetTarget - PlanetPos;
        float angle = MathF.Atan2(painted.Y, painted.X) + (north ? 1f : -1f) * HarpoonPaintedTwinSpread;
        target = PlanetPos + PlanetDirection(angle) * painted.Length();
      }
      LaunchPlanetShot(PlanetShotKind.Harpoon, start, target, 0, firePower);
      return planetShots[^1];
    }
    float offset = MathHelper.PiOver2 - 0.15f - Random.Shared.NextSingle() * 0.25f;
    var outward = PlanetDirection(PlanetFacingAngle() + (north ? offset : -offset));
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
    AddPlanetShot(shot);
    return shot;
  }

  private float HarpoonFireRate()
    => Math.Max(0.1f, PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Harpoon)));

  private float HarpoonPulseInterval() => MainShipWeapons.HarpoonPulseInterval / HarpoonFireRate();

  // Lightning Rod halves the reload between anchors.
  private float HarpoonReloadTime()
    => MainShipWeapons.HarpoonReloadSeconds / HarpoonFireRate() / PrestigeTalentEffects.HarpoonReloadMultiplier;

  // Each harpoon plants itself as it lands; the pulses start once the whole volley holds.
  private void EmbedArcHarpoon(PlanetShot shot)
  {
    bool matched = false, waiting = false;
    for (int i = 0; i < harpoonVolley; i++)
    {
      var anchor = harpoonAnchors[i];
      if (anchor.Path == shot)
      {
        anchor.Landed = matched = true;
        anchor.Target = shot.End;
      }
      waiting |= !anchor.Landed;
    }
    // A shot from a volley that was cleared while it flew.
    if (!matched) return;
    harpoonFirePower = shot.FirePower;
    harpoonPulseFlash = HarpoonPulseFlashSeconds;
    PulsePlanet(0.55f, 0.15f);
    SpawnerEffects.Add(null, shot.End, ArcHarpoonGlow, 3f, 30f, 0.3f);
    if (waiting) return;
    harpoonRodPulses = 0;
    harpoonInFlight = false;
    harpoonEmbedded = true;
    harpoonPulses = 0;
    harpoonPulseTimer = 0f;
  }

  private void PulseArcHarpoon(PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    int pulseNumber = harpoonPulses + 1;
    // Lightning Rod: cannon hits added extra pulses to this volley.
    int pulses = MainShipWeapons.HarpoonPulseCount(upgrades) + harpoonRodPulses;
    int qualityPower = upgrades.HarpoonDeepAnchor
      ? (int)Math.Min(int.MaxValue, (long)harpoonFirePower + harpoonPulses)
      : harpoonFirePower;
    bool last = pulseNumber >= pulses;
    for (int i = 0; i < harpoonVolley; i++)
      PulseHarpoonAnchor(harpoonAnchors[i], pulseNumber, qualityPower, last, bounds, upgrades);
    ConductPulse(AutomaticWeaponYield(harpoonFirePower), qualityPower, bounds, upgrades);

    harpoonPulses = pulseNumber;
    harpoonPulseFlash = HarpoonPulseFlashSeconds;
    PulsePlanet(0.35f + 0.05f * pulseNumber, 0.08f);
    if (!last) return;

    ReleaseArcHarpoon();
    harpoonEmbedded = false;
    harpoonPulseTimer = 0f;
    harpoonReload = 0f;
  }

  // One anchor's share of a pulse; the last pulse also sets off its finishers.
  private void PulseHarpoonAnchor(HarpoonAnchor anchor, int pulseNumber, int qualityPower, bool last,
    PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    int gems = AutomaticWeaponYield(harpoonFirePower);
    float anchorAngle = MathF.Atan2(anchor.Target.Y - PlanetPos.Y, anchor.Target.X - PlanetPos.X);
    KnockGemsLoose(PlanetDamageSource.ArcHarpoon, LightningHit(gems, anchor.Target), qualityPower, bounds, 0.78f, anchorAngle, 0.5f);
    MoltenArcs(anchor.Target, gems, qualityPower, bounds);
    if (upgrades.HarpoonForkedCurrent)
    {
      int forkGems = (int)MathF.Ceiling(gems * MainShipWeapons.HarpoonForkShare);
      float side = pulseNumber % 2 == 0 ? -1f : 1f;
      KnockGemsLoose(PlanetDamageSource.ArcHarpoon, forkGems, qualityPower, bounds, 0.9f, anchorAngle + side * 1.05f,
        0.38f);
    }
    if (!last) return;

    if (upgrades.HarpoonCapacitorDischarge)
    {
      int overload = (int)Math.Min(int.MaxValue, (long)gems * MainShipWeapons.HarpoonCapacitorBonusPulses);
      KnockGemsLoose(PlanetDamageSource.ArcHarpoon, overload, qualityPower + 2, bounds, 1.1f, anchorAngle, 1.15f);
      planetExplosions.Add(new PlanetExplosion { Position = anchor.Target, Scale = 1.6f });
      ShowWorldPopup(anchor.Target, Loc.T("OVERLOAD!"), large: true);
      PulsePlanet(1f, 0.55f);
    }
    if (upgrades.HarpoonTectonicWinch)
    {
      int torn = (int)Math.Min(int.MaxValue, (long)gems * MainShipWeapons.HarpoonWinchBonusPulses);
      KnockClusterLoose(PlanetDamageSource.ArcHarpoon, torn, qualityPower, bounds, 0.55f, PlanetFacingAngle(), 0.32f);
      ShowWorldPopup(anchor.Target, Loc.T("TECTONIC TEAR"), large: false);
    }
  }

  // The landed anchor nearest a point: Lightning Rod's arcs jump to it.
  private Vector2 NearestHarpoonAnchor(Vector2 from)
  {
    var nearest = harpoonAnchors[0].Target;
    float best = float.MaxValue;
    for (int i = 0; i < harpoonVolley; i++)
    {
      var anchor = harpoonAnchors[i];
      float distance = Vector2.DistanceSquared(from, anchor.Target);
      if (anchor.Landed && distance < best)
      {
        best = distance;
        nearest = anchor.Target;
      }
    }
    return nearest;
  }

  private void ClearArcHarpoon()
  {
    harpoonReload = harpoonPulseTimer = harpoonPulseFlash = 0f;
    harpoonInFlight = harpoonEmbedded = false;
    harpoonPulses = harpoonFirePower = harpoonVolley = 0;
    foreach (var anchor in harpoonAnchors)
    {
      anchor.Path = anchor.ReleasedPath = null;
      anchor.Target = anchor.ReleasedTarget = Vector2.Zero;
      anchor.Landed = false;
    }
    harpoonReleaseAge = -1f;
  }


  // ---- Drawing ----
  // The anchor is pure energy (ArcAnchor.fx): a lance of plasma flies tip first on
  // its tether and stays planted in the planet, inside a ring that lies on the
  // surface and turns as the charge builds. The tether is a dark cable with a cyan
  // glow; while the anchor holds, a charge packet runs down it with arcs crackling
  // around it, and every pulse forks lightning across the planet's surface and
  // throws the ring outward. When the last pulse is spent the anchor lets go in a
  // final burst and the cable goes slack and fades. Lightning Rod and Tesla Coil
  // arcs use the same lightning.

  // The lance quad's half size in hull texels, and where its tip and tail sit along
  // it (quad units, -1..1: LANCE_TIP and LANCE_TAIL in ArcAnchor.fx). The tip is at the
  // end of the flight curve; the cable meets the tail.
  private const float HarpoonLanceHalf = 16f;
  private const float HarpoonLanceTip = 0.95f;
  private const float HarpoonLanceTail = -0.5f;
  // The ring's quad half size in world units.
  private const float HarpoonRingHalf = 22f;
  private const float HarpoonReleaseSeconds = 0.4f;
  private const int HarpoonCableSegments = 28;
  private static readonly Color HarpoonCableColor = new(26, 34, 46);

  // Glows drawn with max blending: overlapping segments never stack into bright beads.
  private static readonly BlendState MaxBlend = new()
  {
    ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Max,
    AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One, AlphaBlendFunction = BlendFunction.Max,
  };

  // Additive for the anchor shader's premultiplied output.
  private static readonly BlendState EnergyBlend = new()
  {
    ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
    AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One,
  };

  private readonly Vector2[] harpoonCable = new Vector2[HarpoonCableSegments + 2];
  private readonly float[] harpoonCableLength = new float[HarpoonCableSegments + 2];
  private int harpoonCableCount;
  private readonly Vector2[] boltPoints = new Vector2[17];
  private readonly Vector2[] boltBranch = new Vector2[9];

  // How long ago the volley's anchors let go; they fade out together.
  private float harpoonReleaseAge = -1f;

  private void ReleaseArcHarpoon()
  {
    foreach (var anchor in harpoonAnchors)
    {
      anchor.ReleasedPath = anchor.Landed ? anchor.Path : null;
      anchor.ReleasedTarget = anchor.Target;
    }
    harpoonReleaseAge = 0f;
  }

  private void UpdateHarpoonRelease(float dt)
  {
    if (harpoonReleaseAge < 0f || (harpoonReleaseAge += dt) < HarpoonReleaseSeconds) return;
    harpoonReleaseAge = -1f;
    foreach (var anchor in harpoonAnchors) anchor.ReleasedPath = null;
  }

  private struct HarpoonPose
  {
    public PlanetShot Path;
    public float Head, Fade, Release;
    public Vector2 Tip, Direction, Anchor;
    public bool Anchored;
  }

  // A landed harpoon shows anchored while its twin is still flying.
  private bool TryHarpoonPose(HarpoonAnchor anchor, out HarpoonPose pose)
  {
    pose = default;
    if (anchor.Path != null && (harpoonEmbedded || harpoonInFlight && anchor.Path.Delay <= 0f))
    {
      pose.Path = anchor.Path;
      pose.Anchored = anchor.Landed;
      pose.Head = anchor.Landed ? 1f : Math.Clamp(anchor.Path.Age / anchor.Path.Duration, 0f, 1f);
      pose.Fade = 1f;
      pose.Anchor = anchor.Target;
    }
    else if (harpoonReleaseAge >= 0f && anchor.ReleasedPath != null)
    {
      pose.Path = anchor.ReleasedPath;
      pose.Anchored = true;
      pose.Head = 1f;
      pose.Release = harpoonReleaseAge / HarpoonReleaseSeconds;
      pose.Fade = 1f - pose.Release;
      pose.Anchor = anchor.ReleasedTarget;
    }
    else return false;
    pose.Tip = Bezier(pose.Path, pose.Head);
    pose.Direction = BezierDirection(pose.Path, pose.Head);
    return true;
  }

  // The cable follows the flight curve from the mount to the pod's tail, wobbling with
  // slack in flight and going limp as the anchor lets go.
  private static float HarpoonLanceLength(float scale)
    => (HarpoonLanceTip - HarpoonLanceTail) * HarpoonLanceHalf * scale;

  private void BuildHarpoonCable(in HarpoonPose pose, float scale)
  {
    float podLength = HarpoonLanceLength(scale);
    var tail = pose.Tip - pose.Direction * podLength;
    float slack = pose.Anchored ? 1.5f + 14f * pose.Release : 4f;
    harpoonCableCount = 0;
    harpoonCableLength[0] = 0f;
    for (int i = 0; i <= HarpoonCableSegments; i++)
    {
      float t = i / (float)HarpoonCableSegments;
      var tangent = BezierDirection(pose.Path, t * pose.Head);
      float wave = MathF.Sin(t * MathHelper.Pi) * MathF.Sin(t * 18f - planetAge * 11f) * slack;
      var point = Bezier(pose.Path, t * pose.Head) + new Vector2(-tangent.Y, tangent.X) * wave;
      if (i > 0 && Vector2.Distance(point, pose.Tip) < podLength) break;
      AddCablePoint(point);
    }
    AddCablePoint(tail);
  }

  private void AddCablePoint(Vector2 point)
  {
    int i = harpoonCableCount++;
    harpoonCable[i] = point;
    harpoonCableLength[i] = i == 0 ? 0f : harpoonCableLength[i - 1] + Vector2.Distance(harpoonCable[i - 1], point);
  }

  // A point a share of the way down the cable, from the ship to the pod.
  private Vector2 CablePoint(float share)
  {
    float target = Math.Clamp(share, 0f, 1f) * harpoonCableLength[harpoonCableCount - 1];
    for (int i = 1; i < harpoonCableCount; i++)
    {
      if (harpoonCableLength[i] < target) continue;
      float span = harpoonCableLength[i] - harpoonCableLength[i - 1];
      return Vector2.Lerp(harpoonCable[i - 1], harpoonCable[i],
        span > 0.001f ? (target - harpoonCableLength[i - 1]) / span : 1f);
    }
    return harpoonCable[harpoonCableCount - 1];
  }

  private void DrawCable(float width, Color color, float feather)
  {
    for (int i = 1; i < harpoonCableCount; i++)
      m_shapeBatch.FillLine(harpoonCable[i - 1], harpoonCable[i], width, color, feather);
  }

  // Drawn after the weapon sprites, in layers shared by every harpoon: cable glow, cable, the
  // anchors, then the bright electricity on top, with the Lightning Rod and Tesla Coil arcs.
  // One pass per layer keeps the batch count constant however many harpoons fly.
  private readonly HarpoonPose[] harpoonPoses = new HarpoonPose[MainShipWeapons.TwinHarpoons];
  private void DrawArcHarpoon(float feather)
  {
    float scale = HullScale();
    int flicker = (int)(planetAge * 24f);
    var view = m_camera.GetViewMatrix();
    int poses = 0;
    for (int i = 0; i < harpoonAnchors.Length && poses < harpoonPoses.Length; i++)
      if (TryHarpoonPose(harpoonAnchors[i], out var pose)) harpoonPoses[poses++] = pose;
    if (poses == 0 && arcs.Count == 0) return;

    if (poses > 0)
    {
      // The cable is rebuilt for each layer; that is cheap next to another batch.
      m_shapeBatch.Begin(view, blendState: MaxBlend);
      for (int i = 0; i < poses; i++)
      {
        BuildHarpoonCable(harpoonPoses[i], scale);
        DrawCable(3.4f, ArcHarpoonGlow * (0.32f * harpoonPoses[i].Fade), Math.Max(feather, 4f));
      }
      m_shapeBatch.End();
      m_shapeBatch.Begin(view, blendState: BlendState.AlphaBlend);
      for (int i = 0; i < poses; i++)
      {
        BuildHarpoonCable(harpoonPoses[i], scale);
        DrawCable(1.1f, HarpoonCableColor * harpoonPoses[i].Fade, feather);
      }
      m_shapeBatch.End();
      DrawHarpoonAnchors(poses, scale);
    }

    // Twin harpoons each get their own lightning shapes.
    m_shapeBatch.Begin(view, blendState: MaxBlend);
    for (int i = 0; i < poses; i++)
    {
      BuildHarpoonCable(harpoonPoses[i], scale);
      DrawHarpoonElectricity(harpoonPoses[i], flicker + i * 1013, glow: true, feather);
    }
    DrawTalentArcs(flicker, glow: true, feather);
    m_shapeBatch.End();
    m_shapeBatch.Begin(view, blendState: BlendState.Additive);
    for (int i = 0; i < poses; i++)
    {
      BuildHarpoonCable(harpoonPoses[i], scale);
      DrawHarpoonElectricity(harpoonPoses[i], flicker + i * 1013, glow: false, feather);
    }
    DrawTalentArcs(flicker, glow: false, feather);
    m_shapeBatch.End();
  }

  private void DrawTalentArcs(int flicker, bool glow, float feather)
  {
    foreach (var arc in arcs)
      DrawLightning(arc.From, arc.To, arc.Seed * 97 + flicker, 1f - arc.Age / ArcSeconds, glow, feather);
  }

  // How far the next pulse has charged: the packet's way down the cable.
  private float HarpoonCharge(in HarpoonPose pose)
    => pose.Anchored && pose.Release <= 0f ? Math.Clamp(harpoonPulseTimer / HarpoonPulseInterval(), 0f, 1f) : 0f;

  // The lances, and once planted the rings on the surface around them (ArcAnchor.fx).
  private void DrawHarpoonAnchors(int poses, float scale)
  {
    var effect = EffectCache.ArcAnchorFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    effect.Value.Parameters["view_projection"]?.SetValue(m_camera.ViewProjection());
    effect.Value.Parameters["Time"]?.SetValue(planetAge);
    m_spriteBatch.Begin(SpriteSortMode.Deferred, EnergyBlend, SamplerState.LinearClamp, effect: effect.Value);
    for (int i = 0; i < poses; i++)
      DrawHarpoonAnchor(harpoonPoses[i], scale);
    m_spriteBatch.End();
  }

  private void DrawHarpoonAnchor(in HarpoonPose pose, float scale)
  {
    float charge = HarpoonCharge(pose);
    float flash = pose.Release > 0f ? 1f - pose.Release : harpoonPulseFlash / HarpoonPulseFlashSeconds;
    var texture = AsyncContent.AssetManager.DefaultTexture;
    if (pose.Anchored)
    {
      // The ring lies on the sphere, so it is squashed toward the planet's centre.
      var outward = pose.Anchor - PlanetPos;
      float reach = Math.Clamp(outward.Length() / PlanetRadius, 0f, 0.97f);
      float squash = Math.Max(0.35f, MathF.Sqrt(1f - reach * reach));
      m_spriteBatch.Draw(texture, pose.Anchor, null, AnchorColor(charge, flash, squash, pose.Fade),
        MathF.Atan2(outward.Y, outward.X), new Vector2(0.5f), 2f * HarpoonRingHalf, SpriteEffects.None, 0f);
    }
    float half = HarpoonLanceHalf * scale;
    m_spriteBatch.Draw(texture, pose.Tip - pose.Direction * HarpoonLanceTip * half, null,
      AnchorColor(pose.Anchored ? 0.4f + 0.6f * charge : 0.5f, flash, 0f, pose.Fade),
      MathF.Atan2(pose.Direction.Y, pose.Direction.X), new Vector2(0.5f), 2f * half, SpriteEffects.None, 0f);
  }

  // Vertex colour for ArcAnchor.fx: charge, flash, the ring's squash (0 for the lance), fade.
  private static Color AnchorColor(float charge, float flash, float squash, float fade)
    => new((byte)MathF.Round(Math.Clamp(charge, 0f, 1f) * 255f), (byte)MathF.Round(Math.Clamp(flash, 0f, 1f) * 255f),
      (byte)MathF.Round(Math.Clamp(squash, 0f, 1f) * 255f), (byte)MathF.Round(Math.Clamp(fade, 0f, 1f) * 255f));

  // Each piece of electricity is drawn twice: a soft glow (max blending) and a bright
  // core (additive) from the same bolt geometry.
  private void DrawHarpoonElectricity(in HarpoonPose pose, int flicker, bool glow, float feather)
  {
    // A faint live current along the cable.
    if (glow)
      DrawCable(0.5f, ArcHarpoonGlow * (0.75f * pose.Fade), feather);

    float flash = harpoonPulseFlash / HarpoonPulseFlashSeconds;
    float packet = HarpoonCharge(pose);

    if (pose.Anchored && pose.Release <= 0f)
    {
      // The charge packet runs down the cable to the anchor with a tapering tail and
      // the odd small crackle.
      var at = CablePoint(packet);
      for (int k = 0; k < 4; k++)
      {
        var from = CablePoint(packet - 0.035f * (k + 1) / 4f);
        var to = CablePoint(packet - 0.035f * k / 4f);
        float strength = 1f - k / 4f;
        if (glow)
          m_shapeBatch.FillLine(from, to, 1.2f + 1.2f * strength, ArcHarpoonGlow * (0.9f * strength),
            Math.Max(feather, 3f));
        else
          m_shapeBatch.FillLine(from, to, 0.4f + 0.5f * strength, ArcHarpoonCore * strength, feather);
      }
      // Cable crackles change at half the lightning's flicker rate.
      int crackle = flicker / 2;
      if (FlickerNoise(crackle, 11) < 0.45f)
      {
        float angle = FlickerNoise(crackle, 12) * MathHelper.TwoPi;
        var spark = at + PlanetDirection(angle) * (4f + 4f * FlickerNoise(crackle, 21));
        DrawLightning(at, spark, crackle * 13, 0.45f, glow, feather, size: 0.5f, branches: 0);
      }
      DrawHarpoonDischarge(pose.Anchor, flash, flicker, glow, feather);
    }
    else if (pose.Release > 0f)
    {
      // Letting go: one last burst across the surface around the anchor.
      float burst = MathF.Pow(1f - pose.Release, 1.5f);
      float anchorAngle = MathF.Atan2(pose.Anchor.Y - PlanetPos.Y, pose.Anchor.X - PlanetPos.X);
      for (int i = 0; i < 5; i++)
      {
        float angle = anchorAngle + (i - 2f) * 0.32f + (FlickerNoise(flicker, 40 + i) - 0.5f) * 0.2f;
        var end = PlanetPos + PlanetDirection(angle) * PlanetRadius * (0.62f + 0.25f * FlickerNoise(flicker, 50 + i));
        DrawLightning(pose.Anchor, end, flicker * 7 + i, burst, glow, feather, branches: 1);
      }
      if (!glow)
        m_shapeBatch.FillCircle(pose.Anchor, 3.5f * burst, ArcHarpoonCore * burst, Math.Max(feather, 2f));
    }
  }

  // Every pulse forks lightning from the anchor across the planet's surface (twice as
  // many forks with Forked Current) and one bolt down into the crust.
  private void DrawHarpoonDischarge(Vector2 anchor, float flash, int flicker, bool glow, float feather)
  {
    if (flash <= 0f) return;
    float intensity = MathF.Pow(flash, 0.7f);
    int forks = UpgradeManager.Instance.UG.HarpoonForkedCurrent ? 6 : 3;
    float anchorAngle = MathF.Atan2(anchor.Y - PlanetPos.Y, anchor.X - PlanetPos.X);
    for (int fork = 0; fork < forks; fork++)
    {
      float side = fork % 2 == 0 ? 1f : -1f;
      float sweep = side * (0.22f + 0.17f * (fork / 2) + 0.12f * FlickerNoise(harpoonPulses, 60 + fork));
      var end = PlanetPos + PlanetDirection(anchorAngle + sweep)
        * PlanetRadius * (0.8f - 0.12f * FlickerNoise(harpoonPulses, 70 + fork));
      DrawLightning(anchor, end, harpoonPulses * 31 + fork * 5 + flicker, intensity, glow, feather);
    }
    var down = PlanetPos + PlanetDirection(anchorAngle) * PlanetRadius * 0.5f;
    DrawLightning(anchor, down, harpoonPulses * 31 + flicker + 99, intensity * 0.8f, glow, feather, size: 1.2f);
    if (!glow)
      m_shapeBatch.FillCircle(anchor, 1.5f + 1.5f * flash, ArcHarpoonCore * flash, Math.Max(feather, 2f));
  }

  // A jagged bolt from a to b by midpoint displacement, stable within one flicker step.
  private static int BuildBolt(Vector2 a, Vector2 b, int seed, float roughness, Vector2[] points, int levels)
  {
    points[0] = a;
    points[1] = b;
    int count = 2;
    for (int level = 0; level < levels; level++)
    {
      for (int i = count - 1; i > 0; i--)
        points[i * 2] = points[i];
      for (int i = 1; i < count * 2 - 1; i += 2)
      {
        var span = points[i + 1] - points[i - 1];
        float offset = (FlickerNoise(seed + level * 131, i) - 0.5f) * roughness;
        points[i] = (points[i - 1] + points[i + 1]) * 0.5f + new Vector2(-span.Y, span.X) * offset;
      }
      count = count * 2 - 1;
    }
    return count;
  }

  private void DrawBoltPoints(Vector2[] points, int count, float width, Color color, float feather)
  {
    for (int i = 1; i < count; i++)
      m_shapeBatch.FillLine(points[i - 1], points[i], width, color, feather);
  }

  private void DrawLightning(Vector2 from, Vector2 to, int seed, float intensity, bool glow, float feather,
    float size = 1f, int branches = 1)
  {
    if (intensity <= 0.01f) return;
    var core = Color.Lerp(ArcHarpoonGlow, Color.White, 0.75f);
    int count = BuildBolt(from, to, seed, 0.5f, boltPoints, 4);
    if (glow)
      DrawBoltPoints(boltPoints, count, 2.4f * size, ArcHarpoonGlow * (0.75f * intensity), Math.Max(feather, 4f * size));
    else
      DrawBoltPoints(boltPoints, count, 0.6f * size, core * intensity, feather);
    // Branches split off partway along and reach a little way to one side.
    float length = Vector2.Distance(from, to);
    float heading = MathF.Atan2(to.Y - from.Y, to.X - from.X);
    for (int k = 0; k < branches; k++)
    {
      var start = boltPoints[3 + (int)(FlickerNoise(seed, 80 + k) * (count - 7))];
      float turn = (k % 2 == 0 ? 1f : -1f) * (0.45f + 0.5f * FlickerNoise(seed, 90 + k));
      var end = start + PlanetDirection(heading + turn) * length * (0.2f + 0.2f * FlickerNoise(seed, 100 + k));
      int branchCount = BuildBolt(start, end, seed * 7 + k + 1, 0.55f, boltBranch, 3);
      if (glow)
        DrawBoltPoints(boltBranch, branchCount, 1.6f * size, ArcHarpoonGlow * (0.5f * intensity),
          Math.Max(feather, 3f * size));
      else
        DrawBoltPoints(boltBranch, branchCount, 0.45f * size, core * (0.7f * intensity), feather);
    }
  }
}
