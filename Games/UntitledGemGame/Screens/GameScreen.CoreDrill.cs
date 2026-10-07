using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Core Drill ship system (rules in CoreDrill.cs). A pod launches from the homebase,
// curves around the planet and lands on its far side, so its effects stay out of the
// weapon lane between ship and planet. It bores deep gems out for a few seconds,
// optionally cracking the planet, then burns out with its finishers.
public partial class UntitledGemGameGameScreen
{
  private static readonly Color DrillGlow = new(255, 120, 60);
  private const float DrillExitSeconds = 0.7f;
  private const float DrillGeyserSeconds = 0.8f;
  private const int MaxDrillPods = 6;
  private const int MaxDrillGemsPerFrame = 16;
  private const int DrillFaultSegments = 14;
  private const int DrillFaultBranches = 2;
  // Froozle heavy-weapon housing plus an animated Nairan energy auger.
  // The first seven frames keep the housing axial; later source frames swing the gun sideways.
  private const int DrillBodyFrameSize = 48, DrillBodyFrames = 7;
  private const int DrillBitFrameWidth = 18, DrillBitFrameHeight = 38, DrillBitFrames = 4;
  private const float DrillPodScale = 1.25f;

  private sealed class DrillPod
  {
    public readonly PlanetShot Path = new();
    public float Age, Drilling, Duration, BoreAngle, Carry, Exit, Geyser;
    public float[] FaultCarry = [];
    public float[] FaultBends = [];
    public int Drilled, FirePower, Layers;
    public bool Landed, Done;
  }

  private readonly List<DrillPod> drillPods = new();
  private bool drillNorth;
  private float drillResonance;

  private bool DrillResonating
  {
    get
    {
      if (drillResonance > 0f) return true;
      foreach (var pod in drillPods)
        if (pod.Landed && !pod.Done) return true;
      return false;
    }
  }

  private bool HasActiveCoreDrillBeam
  {
    get
    {
      foreach (var pod in drillPods)
        if (pod.Landed && !pod.Done) return true;
      return false;
    }
  }

  public void LaunchCoreDrill()
  {
    if (!PlanetMiningEnabled || !GameStarted || m_prestiging || m_postPrestige || planetConsumed
      || drillPods.Count >= MaxDrillPods)
      return;
    var upgrades = UpgradeManager.Instance.UGA;
    // Take turns flying over and under the planet.
    drillNorth = !drillNorth;
    float side = drillNorth ? 1f : -1f;
    var toShip = PlanetDirection(PlanetFacingAngle());
    var flank = new Vector2(-toShip.Y, toShip.X) * side;
    // Land on the far side, a little toward the flank it came around.
    float bore = PlanetFacingAngle() + MathF.PI - side * (0.15f + Random.Shared.NextSingle() * 0.35f);
    int faults = CoreDrill.Faults(upgrades);
    var pod = new DrillPod
    {
      BoreAngle = bore,
      Duration = CoreDrill.DrillSeconds(upgrades),
      FirePower = SignalStats.FirePower(MainShipWeapon.Cannon),
      FaultCarry = new float[faults],
      FaultBends = new float[faults],
    };
    for (int i = 0; i < faults; i++)
      pod.FaultBends[i] = (i % 2 == 0 ? 1f : -1f) * (0.35f + 0.22f * (i / 2) + Random.Shared.NextSingle() * 0.15f);
    var start = HullMount(-4f, drillNorth ? -44f : 30f);
    pod.Path.Start = start;
    pod.Path.Control1 = start + (PlanetPos - start) * 0.35f + flank * PlanetRadius * 2.6f;
    pod.Path.Control2 = PlanetPos + flank * PlanetRadius * 2.4f - toShip * PlanetRadius * 2.2f;
    pod.Path.End = PlanetPos + PlanetDirection(bore) * PlanetRadius * 0.96f;
    pod.Path.Duration = CoreDrill.FlightSeconds;
    drillPods.Add(pod);
  }

  public void ClearCoreDrills()
  {
    drillPods.Clear();
    drillResonance = 0f;
  }

  private void UpdateCoreDrills(float dt, PlayAreaBounds bounds)
  {
    drillResonance = Math.Max(0f, drillResonance - dt);
    var upgrades = UpgradeManager.Instance.UGA;
    for (int i = drillPods.Count - 1; i >= 0; i--)
    {
      var pod = drillPods[i];
      pod.Age += dt;
      pod.Geyser = Math.Max(0f, pod.Geyser - dt);
      if (!pod.Landed)
      {
        if (pod.Age < pod.Path.Duration) continue;
        pod.Landed = true;
        PulsePlanet(0.5f, 0.25f);
        SpawnerEffects.Add(null, pod.Path.End, DrillGlow, 4f, 34f, 0.35f);
      }
      if (pod.Done)
      {
        pod.Exit += dt;
        if (pod.Exit >= DrillExitSeconds && pod.Geyser <= 0f) drillPods.RemoveAt(i);
        continue;
      }

      pod.Drilling = Math.Min(pod.Duration, pod.Drilling + dt);
      pod.Layers = CoreDrill.Layers(upgrades, pod.Drilling);
      int power = CoreDrill.Deeper(pod.FirePower, pod.Layers);
      int bonus = Math.Max(0, upgrades.CoreDrillValue);
      float rate = SignalStats.CoreDrillRate;
      pod.Carry += rate * dt;
      int gems = (int)Math.Min(pod.Carry, MaxDrillGemsPerFrame);
      pod.Carry -= gems;
      pod.Drilled += KnockGemsLoose(PlanetDamageSource.CoreDrill, gems, power, bounds, 0.8f, pod.BoreAngle, 0.35f,
        drilled: true, bonusPercent: bonus);

      // Fault Lines: each crack's tip leaks gems from the same depth.
      float leak = rate * CoreDrill.FaultLeak(upgrades);
      for (int f = 0; f < pod.FaultCarry.Length; f++)
      {
        pod.FaultCarry[f] += leak * FaultGrowth(pod) * dt;
        int leaked = (int)Math.Min(pod.FaultCarry[f], MaxDrillGemsPerFrame);
        pod.FaultCarry[f] -= leaked;
        var tip = FaultTip(pod, f, FaultGrowth(pod));
        float tipAngle = MathF.Atan2(tip.Y - PlanetPos.Y, tip.X - PlanetPos.X);
        pod.Drilled += KnockGemsLoose(PlanetDamageSource.CoreDrill, leaked, power, bounds, 0.8f, tipAngle, 0.25f,
          drilled: true, bonusPercent: bonus);
      }
      planetShake = Math.Max(planetShake, 0.1f);
      if (pod.Drilling >= pod.Duration) FinishCoreDrill(pod, power, bounds, upgrades);
    }
  }

  private void FinishCoreDrill(DrillPod pod, int power, PlayAreaBounds bounds, UpgradesGeneratorUpgrades_abilities upgrades)
  {
    pod.Done = true;
    pod.Exit = 0f;
    PulsePlanet(0.45f, 0.2f);
    SpawnerEffects.Add(null, pod.Path.End, DrillGlow, 6f, 44f, 0.45f);
    drillResonance = Math.Max(drillResonance, CoreDrill.ResonanceLinger(upgrades));
    int bonus = Math.Max(0, upgrades.CoreDrillValue);

    int rupture = CoreDrill.RuptureGems(upgrades, pod.Drilled);
    if (rupture > 0)
    {
      // Every crack gives way at once: deep gems burst from all around the planet.
      KnockGemsLoose(PlanetDamageSource.CoreDrill, rupture, power, bounds, 1f, drilled: true, bonusPercent: bonus);
      SpawnerEffects.Add(null, PlanetPos, DrillGlow, PlanetRadius, PlanetRadius * 2.2f, 0.6f);
      PulsePlanet(1f, 0.7f);
      ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 50f), Loc.T("TECTONIC RUPTURE"), large: true);
    }
    int tap = CoreDrill.CoreTapGems(upgrades, pod.Drilled);
    if (tap > 0)
    {
      // The drill breaks into the core: a geyser of the planet's deepest gems.
      KnockGemsLoose(PlanetDamageSource.CoreDrill, tap, CoreDrill.Deeper(power, CoreDrill.CoreTapLayers), bounds, 1.3f,
        pod.BoreAngle, 0.15f, CoreDrill.CoreTapValue, drilled: true, bonusPercent: bonus);
      pod.Geyser = DrillGeyserSeconds;
      PulsePlanet(0.8f, 0.5f);
      ShowWorldPopup(pod.Path.End - Vector2.UnitY * 60f, Loc.T("CORE TAP"), large: true);
    }
    if (upgrades.CoreDrillHollowWorld && m_gameState.CoreDrillTunnels < CoreDrill.MaxTunnels)
    {
      m_gameState.CoreDrillTunnels++;
      float hollow = CoreDrill.HollowBonus(upgrades, m_gameState.CoreDrillTunnels);
      ShowWorldPopup(PlanetPos + Vector2.UnitY * (PlanetRadius + 50f),
        hollow >= CoreDrill.HollowCap ? Loc.T("HOLLOW WORLD MAX") : Loc.F("TUNNELS +{0:0}%", hollow * 100), large: false);
    }
  }

  // Cracks spread over the first part of the drilling.
  private static float FaultGrowth(DrillPod pod)
    => Math.Clamp(pod.Drilling / Math.Max(0.1f, pod.Duration * 0.45f), 0f, 1f);

  private static float FaultNoise(int fault, int step, float salt)
  {
    float value = MathF.Sin((fault + 1) * 91.73f + step * 37.19f + salt * 17.11f) * 43758.5453f;
    return value - MathF.Floor(value);
  }

  // Stable angular vertices give faults the sharp, irregular kinks of the permanent
  // planet fractures instead of the old smooth sine-wave paths.
  private static Vector2 FaultVertex(DrillPod pod, int fault, int step)
  {
    float along = step / (float)DrillFaultSegments;
    var inward = -PlanetDirection(pod.BoreAngle);
    float bend = pod.FaultBends[fault];
    float angle = MathF.Atan2(inward.Y, inward.X) + bend * (0.55f + along * 0.65f);
    var direction = PlanetDirection(angle);
    float jag = step == 0 ? 0f : (FaultNoise(fault, step, pod.BoreAngle) - 0.5f) * PlanetRadius * 0.075f;
    var start = PlanetPos + PlanetDirection(pod.BoreAngle) * PlanetRadius * 0.91f;
    return start + direction * (along * PlanetRadius * 0.74f)
      + new Vector2(-direction.Y, direction.X) * jag;
  }

  private static Vector2 FaultPoint(DrillPod pod, int fault, float along)
  {
    float scaled = Math.Clamp(along, 0f, 1f) * DrillFaultSegments;
    int from = Math.Min(DrillFaultSegments, (int)scaled);
    int to = Math.Min(DrillFaultSegments, from + 1);
    return Vector2.Lerp(FaultVertex(pod, fault, from), FaultVertex(pod, fault, to), scaled - from);
  }

  private static Vector2 FaultBranchPoint(DrillPod pod, int fault, int branch, float along)
  {
    float fork = 0.32f + branch * 0.28f;
    var origin = FaultPoint(pod, fault, fork);
    var tangent = FaultPoint(pod, fault, Math.Min(1f, fork + 0.06f)) - origin;
    if (tangent.LengthSquared() < 0.001f) tangent = -PlanetDirection(pod.BoreAngle);
    tangent.Normalize();
    float side = ((fault + branch) & 1) == 0 ? -1f : 1f;
    float turn = side * (0.65f + FaultNoise(fault, branch, pod.BoreAngle + 2f) * 0.45f);
    var direction = PlanetDirection(MathF.Atan2(tangent.Y, tangent.X) + turn);
    float length = PlanetRadius * (0.13f + FaultNoise(fault, branch, 8f) * 0.06f);
    float jag = along <= 0f ? 0f
      : (FaultNoise(fault + branch * 11, (int)(along * 5f), pod.BoreAngle + 5f) - 0.5f) * PlanetRadius * 0.035f;
    return origin + direction * (along * length) + new Vector2(-direction.Y, direction.X) * jag;
  }

  private static Vector2 FaultTip(DrillPod pod, int fault, float growth) => FaultPoint(pod, fault, growth);

  private static Color DrillDepthColor(int power)
  {
    var type = GemTypes.Red;
    foreach (var (color, required) in GemQualityTable.ColorFirePower)
      if (power >= required) type = color;
    var gem = GemQualityTable.GetColor(type);
    return Color.Lerp(new Color(gem.R, gem.G, gem.B), Color.White, 0.35f);
  }

  // Glows, cracks and trails: called inside the additive weapon shape pass.
  private void DrawCoreDrillGlows(float feather)
  {
    if (drillPods.Count == 0 && drillResonance <= 0f) return;
    if (DrillResonating)
    {
      bool seismic = CoreDrill.ResonanceLayers(UpgradeManager.Instance.UGA) > 0;
      float pulse = 0.5f + 0.5f * MathF.Sin(planetAge * 9f);
      float alpha = seismic ? 0.18f + 0.12f * pulse : 0.1f + 0.07f * pulse;
      m_shapeBatch.BorderCircle(PlanetPos, PlanetRadius + 6f + 4f * pulse, DrillGlow * alpha, seismic ? 2.5f : 1.8f,
        Math.Max(feather, 6f));
    }
    foreach (var pod in drillPods)
    {
      if (!pod.Landed)
      {
        float t = Math.Clamp(pod.Age / pod.Path.Duration, 0f, 1f);
        for (int i = 1; i <= 6; i++)
        {
          float a = Math.Max(0f, t - i * 0.025f), b = Math.Max(0f, t - (i - 1) * 0.025f);
          m_shapeBatch.FillLine(Bezier(pod.Path, a), Bezier(pod.Path, b), 3f - i * 0.35f,
            DrillGlow * (0.55f - i * 0.08f), Math.Max(feather, 4f));
        }
        continue;
      }
      var anchor = pod.Path.End;
      var outward = PlanetDirection(pod.BoreAngle);
      float fade = pod.Done ? 1f - Math.Clamp(pod.Exit / DrillExitSeconds, 0f, 1f) : 1f;
      var depth = DrillDepthColor(CoreDrill.Deeper(pod.FirePower, pod.Layers));
      if (!pod.Done)
      {
        float spin = 0.5f + 0.5f * MathF.Sin(planetAge * 31f);
        m_shapeBatch.FillCircle(anchor - outward * 3f, 7f + 3f * spin, depth * 0.85f, Math.Max(feather, 10f));
        // Ejecta spray back out of the bore hole.
        for (int s = 0; s < 4; s++)
        {
          float phase = (planetAge * 2.3f + s * 0.27f) % 1f;
          float angle = pod.BoreAngle + MathF.Sin(s * 12.9f + MathF.Floor(planetAge * 2.3f + s * 0.27f) * 3.1f) * 0.6f;
          var from = anchor + PlanetDirection(angle) * (6f + phase * 30f);
          m_shapeBatch.FillLine(from, from + PlanetDirection(angle) * 5f, 1.6f, depth * (1f - phase), feather);
        }
      }
      // Fault Lines use the fracture palette: a molten halo around an angular,
      // white-hot split, with small branches opening after the main crack reaches them.
      float growth = FaultGrowth(pod);
      for (int f = 0; f < pod.FaultBends.Length; f++)
      {
        var previous = FaultPoint(pod, f, 0f);
        float heat = 0.78f + 0.18f * (0.5f + 0.5f * MathF.Sin(planetAge * 13f + f * 2.3f));
        for (int step = 1; step <= DrillFaultSegments; step++)
        {
          var next = FaultPoint(pod, f, growth * step / DrillFaultSegments);
          DrawMoltenFault(previous, next, fade, heat, feather, false);
          previous = next;
        }
        for (int branch = 0; branch < DrillFaultBranches; branch++)
        {
          float appears = 0.32f + branch * 0.28f;
          float branchGrowth = Math.Clamp((growth - appears) / (1f - appears), 0f, 1f);
          if (branchGrowth <= 0f) continue;
          previous = FaultBranchPoint(pod, f, branch, 0f);
          for (int step = 1; step <= 4; step++)
          {
            var next = FaultBranchPoint(pod, f, branch, branchGrowth * step / 4f);
            DrawMoltenFault(previous, next, fade, heat * 0.92f, feather, true);
            previous = next;
          }
        }
      }
      if (pod.Geyser > 0f)
      {
        float life = pod.Geyser / DrillGeyserSeconds;
        float height = PlanetRadius * (0.8f + 1.8f * (1f - life));
        m_shapeBatch.FillLine(anchor, anchor + outward * height, 18f * life + 4f, depth * life, Math.Max(feather, 16f));
        m_shapeBatch.FillLine(anchor, anchor + outward * height * 0.85f, 5f * life + 2f, Color.White * life,
          Math.Max(feather, 5f));
      }
    }
  }

  private void DrawMoltenFault(Vector2 from, Vector2 to, float fade, float heat, float feather, bool branch)
  {
    float halo = branch ? 4f : 7f;
    m_shapeBatch.FillLine(from, to, halo, CrackMagmaColor * (0.24f * fade), Math.Max(feather, halo + 2f));
    m_shapeBatch.FillLine(from, to, branch ? 2f : 3.2f, DrillGlow * (0.55f * fade),
      Math.Max(feather, branch ? 3f : 4f));
    m_shapeBatch.FillLine(from, to, branch ? 0.8f : 1.25f, CrackColor(heat) * (0.95f * fade),
      Math.Max(feather, 1.25f));
  }

  // A weapon module and cutting-head effect make the pod read as mining equipment,
  // not another member of the harvester fleet. The source sprites point up, so their
  // -Y axis rotates onto the pod's direction of travel.
  private void DrawCoreDrillPods()
  {
    var fleet = TextureCache.Fleet;
    bool bodyReady = IsReady(TextureCache.CoreDrillBody);
    bool bitReady = IsReady(TextureCache.CoreDrillBit);
    if (!bodyReady) return;
    var origin = new Vector2(DrillBodyFrameSize / 2f);

    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    foreach (var pod in drillPods)
    {
      Vector2 position, heading;
      float alpha = 1f;
      if (!pod.Landed)
      {
        float t = Math.Clamp(pod.Age / pod.Path.Duration, 0f, 1f);
        position = Bezier(pod.Path, t);
        heading = BezierDirection(pod.Path, t);
      }
      else
      {
        // Half sunk into the rim, nose first, vibrating while it bores.
        heading = -PlanetDirection(pod.BoreAngle);
        float shake = pod.Done ? 0f : MathF.Sin(planetAge * 70f) * 1.1f;
        position = pod.Path.End - heading * 17f + new Vector2(-heading.Y, heading.X) * shake;
        if (pod.Done) alpha = 1f - Math.Clamp(pod.Exit / DrillExitSeconds, 0f, 1f);
      }
      float rotation = MathF.Atan2(heading.Y, heading.X) + MathHelper.PiOver2;
      if (pod.Landed && !pod.Done && bitReady)
      {
        int bitFrame = (int)(planetAge * 24f) % DrillBitFrames;
        var bitSource = new Rectangle(bitFrame * DrillBitFrameWidth, 0, DrillBitFrameWidth, DrillBitFrameHeight);
        // The auger overlaps the housing at its base and visibly penetrates the crust.
        var bitPosition = pod.Path.End + heading * 10f;
        m_spriteBatch.Draw(TextureCache.CoreDrillBit.Value, bitPosition, bitSource, Color.White * alpha, rotation,
          new Vector2(DrillBitFrameWidth / 2f, DrillBitFrameHeight / 2f), 0.85f, SpriteEffects.None, 0f);
      }
      if (!pod.Landed && fleet != null)
      {
        int frame = (int)(pod.Age * 1000f / FleetAtlas.EngineFrameMilliseconds) % FleetAtlas.EngineFrameCount;
        var engine = fleet.Region(FleetAtlas.TorpedoEngine + $"#{frame}");
        m_spriteBatch.Draw(engine.Texture, position, engine.Bounds, Color.White * alpha, rotation,
          new Vector2(engine.Width, engine.Height) / 2f, 0.72f, SpriteEffects.None, 0f);
      }
      int bodyFrame = (int)((pod.Landed ? planetAge * 10f : pod.Age * 7f)) % DrillBodyFrames;
      var bodySource = new Rectangle(bodyFrame * DrillBodyFrameSize, 0, DrillBodyFrameSize, DrillBodyFrameSize);
      m_spriteBatch.Draw(TextureCache.CoreDrillBody.Value, position, bodySource, Color.White * alpha, rotation, origin,
        DrillPodScale, SpriteEffects.None, 0f);
    }
    m_spriteBatch.End();
  }
}
