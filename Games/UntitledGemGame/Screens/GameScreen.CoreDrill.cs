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
  private static readonly Color DrillHull = new(88, 96, 116);
  private static readonly Color DrillHullEdge = new(28, 32, 44);
  private const float DrillExitSeconds = 0.7f;
  private const float DrillGeyserSeconds = 0.8f;
  private const int MaxDrillPods = 6;
  private const int MaxDrillGemsPerFrame = 16;
  private const float DrillPodScale = 1.7f;

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
      pod.Drilled += KnockGemsLoose(gems, power, bounds, 0.8f, pod.BoreAngle, 0.35f, drilled: true, bonusPercent: bonus);

      // Fault Lines: each crack's tip leaks gems from the same depth.
      float leak = rate * CoreDrill.FaultLeak(upgrades);
      for (int f = 0; f < pod.FaultCarry.Length; f++)
      {
        pod.FaultCarry[f] += leak * FaultGrowth(pod) * dt;
        int leaked = (int)Math.Min(pod.FaultCarry[f], MaxDrillGemsPerFrame);
        pod.FaultCarry[f] -= leaked;
        var tip = FaultTip(pod, f, FaultGrowth(pod));
        float tipAngle = MathF.Atan2(tip.Y - PlanetPos.Y, tip.X - PlanetPos.X);
        pod.Drilled += KnockGemsLoose(leaked, power, bounds, 0.8f, tipAngle, 0.25f, drilled: true, bonusPercent: bonus);
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

    int rupture = Math.Min(CoreDrill.RuptureGems(upgrades, pod.Drilled), PlanetGemRoom());
    if (rupture > 0)
    {
      // Every crack gives way at once: deep gems burst from all around the planet.
      KnockGemsLoose(rupture, power, bounds, 1f, drilled: true, bonusPercent: bonus);
      SpawnerEffects.Add(null, PlanetPos, DrillGlow, PlanetRadius, PlanetRadius * 2.2f, 0.6f);
      PulsePlanet(1f, 0.7f);
      ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 50f), "TECTONIC RUPTURE", large: true);
    }
    int tap = Math.Min(CoreDrill.CoreTapGems(upgrades, pod.Drilled), PlanetGemRoom());
    if (tap > 0)
    {
      // The drill breaks into the core: a geyser of the planet's deepest gems.
      KnockGemsLoose(tap, CoreDrill.Deeper(power, CoreDrill.CoreTapLayers), bounds, 1.3f, pod.BoreAngle, 0.15f,
        CoreDrill.CoreTapValue, drilled: true, bonusPercent: bonus);
      pod.Geyser = DrillGeyserSeconds;
      PulsePlanet(0.8f, 0.5f);
      ShowWorldPopup(pod.Path.End - Vector2.UnitY * 60f, "CORE TAP", large: true);
    }
    if (upgrades.CoreDrillHollowWorld && m_gameState.CoreDrillTunnels < CoreDrill.MaxTunnels)
    {
      m_gameState.CoreDrillTunnels++;
      float hollow = CoreDrill.HollowBonus(upgrades, m_gameState.CoreDrillTunnels);
      ShowWorldPopup(PlanetPos + Vector2.UnitY * (PlanetRadius + 50f),
        hollow >= CoreDrill.HollowCap ? "HOLLOW WORLD MAX" : $"TUNNELS +{hollow * 100:0}%", large: false);
    }
  }

  // Cracks spread over the first part of the drilling.
  private static float FaultGrowth(DrillPod pod)
    => Math.Clamp(pod.Drilling / Math.Max(0.1f, pod.Duration * 0.45f), 0f, 1f);

  private static Vector2 FaultPoint(DrillPod pod, int fault, float along)
  {
    var inward = -PlanetDirection(pod.BoreAngle);
    float bend = pod.FaultBends[fault];
    var direction = PlanetDirection(MathF.Atan2(inward.Y, inward.X) + bend * (1f + along * 0.4f));
    float jitter = MathF.Sin(fault * 7.1f + along * 23f) * 4f;
    var start = PlanetPos + PlanetDirection(pod.BoreAngle) * PlanetRadius * 0.9f;
    return start + direction * (along * PlanetRadius * 0.75f) + new Vector2(-direction.Y, direction.X) * jitter;
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
    if (DrillResonating && CoreDrill.ResonanceLayers(UpgradeManager.Instance.UGA) > 0)
    {
      float pulse = 0.5f + 0.5f * MathF.Sin(planetAge * 9f);
      m_shapeBatch.BorderCircle(PlanetPos, PlanetRadius + 6f + 4f * pulse, DrillGlow * (0.18f + 0.12f * pulse), 2.5f,
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
      // Fault Lines glow like magma, then cool as the pod burns out.
      float growth = FaultGrowth(pod);
      for (int f = 0; f < pod.FaultBends.Length; f++)
      {
        var previous = FaultPoint(pod, f, 0f);
        for (int step = 1; step <= 8; step++)
        {
          var next = FaultPoint(pod, f, growth * step / 8f);
          m_shapeBatch.FillLine(previous, next, 5f, DrillGlow * (0.35f * fade), Math.Max(feather, 6f));
          m_shapeBatch.FillLine(previous, next, 2.2f, new Color(255, 200, 120) * (0.9f * fade), Math.Max(feather, 2f));
          previous = next;
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

  // The pods themselves are solid, so they read on top of the glow.
  private void DrawCoreDrillPods(float feather)
  {
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
        // Half sunk into the rim, nose first, shaking while it bores.
        heading = -PlanetDirection(pod.BoreAngle);
        float shake = pod.Done ? 0f : MathF.Sin(planetAge * 70f) * 0.8f;
        position = pod.Path.End - heading * 9f * DrillPodScale + new Vector2(-heading.Y, heading.X) * shake;
        if (pod.Done) alpha = 1f - Math.Clamp(pod.Exit / DrillExitSeconds, 0f, 1f);
      }
      var side = new Vector2(-heading.Y, heading.X) * DrillPodScale;
      var forward = heading * DrillPodScale;
      m_shapeBatch.FillLine(position - forward * 9f, position + forward * 5f, 8.5f * DrillPodScale, DrillHullEdge * alpha, feather);
      m_shapeBatch.FillLine(position - forward * 8f, position + forward * 4f, 6f * DrillPodScale, DrillHull * alpha, feather);
      m_shapeBatch.FillLine(position - forward * 7f - side * 6f, position - forward * 3f, 2.5f * DrillPodScale, DrillHull * alpha, feather);
      m_shapeBatch.FillLine(position - forward * 7f + side * 6f, position - forward * 3f, 2.5f * DrillPodScale, DrillHull * alpha, feather);
      m_shapeBatch.FillLine(position + forward * 4f, position + forward * 12f, 3.5f * DrillPodScale, DrillGlow * alpha, feather);
    }
  }
}
