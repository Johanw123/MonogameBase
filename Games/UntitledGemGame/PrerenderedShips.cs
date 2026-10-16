using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

/// <summary>
/// Draw-only swap from the old pixel-art hulls to ships prerendered from 3D models
/// (tools/blender/prerender.py). Gameplay keeps the old hull sizes for collision,
/// spawning and UI, so this can be toggled freely to compare the two looks.
/// </summary>
public static class PrerenderedShips
{
  public static bool Enabled = true;

  // World units covered by one texel of the prerendered image. Picked so each
  // harvester's visual radius matches its base collection range (12.0, 12.0, 12.8,
  // 16.8, 12.15, 6.4 world units in BaseStats.GetHarvesterBaseCollectionRange).
  public const float HomeBaseScale = 0.19f;
  public const float HarvesterScale = 0.216f;

  // The other harvesters are rendered at the Drifter's texel density (length_px in
  // tools/blender/prerender.py HARVESTER_SHIPS sets their size), but their entities
  // are scaled down in EntityFactory, so divide that entity scale back out.
  private const float AdvancedHarvesterScale = HarvesterScale / 0.8f;
  private const float ExpertHarvesterScale = HarvesterScale / 0.8f;
  private const float UltimateHarvesterScale = HarvesterScale / 0.8f;
  private const float PerimeterHarvesterScale = HarvesterScale / 0.45f;
  private const float DroneScale = HarvesterScale / 0.4f;

  // Camera elevation (65 deg above the horizon) used by tools/blender/prerender.py.
  private const float SinElevation = 0.9063078f;
  private const float CosElevation = 0.4226183f;

  private const float FrameStep = MathHelper.TwoPi / FleetAtlas.TurntableFrameCount;
  private static readonly Dictionary<string, string[]> frameNames = new(StringComparer.Ordinal);
  private static string[] boosterFrameNames;

  private static bool TryGetShipSpec(Harvester.HarvesterType type,
    out string path, out bool isTurntable, out float scale)
  {
    isTurntable = true;
    switch (type)
    {
      case Harvester.HarvesterType.HomeBase:
        path = FleetAtlas.PrerenderHomeBase;
        isTurntable = false;
        scale = HomeBaseScale;
        return true;
      case Harvester.HarvesterType.Harvester:
        path = FleetAtlas.PrerenderHarvesterTurntable;
        scale = HarvesterScale;
        return true;
      case Harvester.HarvesterType.AdvancedHarvester:
        path = FleetAtlas.PrerenderAdvancedHarvesterTurntable;
        scale = AdvancedHarvesterScale;
        return true;
      case Harvester.HarvesterType.ExpertHarvester:
        path = FleetAtlas.PrerenderExpertHarvesterTurntable;
        scale = ExpertHarvesterScale;
        return true;
      case Harvester.HarvesterType.UltimateHarvester:
        path = FleetAtlas.PrerenderUltimateHarvesterTurntable;
        scale = UltimateHarvesterScale;
        return true;
      case Harvester.HarvesterType.PerimeterHarvester:
        path = FleetAtlas.PrerenderPerimeterHarvesterTurntable;
        scale = PerimeterHarvesterScale;
        return true;
      case Harvester.HarvesterType.Drone:
        path = FleetAtlas.PrerenderDroneTurntable;
        scale = DroneScale;
        return true;
      default:
        path = null;
        isTurntable = false;
        scale = 1f;
        return false;
    }
  }

  /// <summary>
  /// The turntable frame nearest to a clockwise rotation (0 = nose/barrel up), its
  /// pivot, and the leftover angle (at most half a frame step) to rotate it by.
  /// </summary>
  public static Texture2DRegion Frame(FleetAtlas fleet, string turntable, float rotation,
    out Vector2 pivot, out float drawRotation)
  {
    if (!frameNames.TryGetValue(turntable, out var names))
    {
      names = new string[FleetAtlas.TurntableFrameCount];
      for (int i = 0; i < names.Length; i++) names[i] = turntable + "#" + i;
      frameNames.Add(turntable, names);
    }
    int frame = (int)MathF.Round(rotation / FrameStep);
    drawRotation = rotation - frame * FrameStep;
    frame %= names.Length;
    if (frame < 0) frame += names.Length;
    pivot = fleet.Pivot(names[frame]);
    return fleet.Region(names[frame]);
  }

  /// <summary>
  /// Image to draw for a ship at the given rotation: a turntable frame plus the
  /// leftover rotation, or a single image with the full rotation.
  /// </summary>
  public static bool TryGet(Harvester.HarvesterType type, float rotation,
    out Texture2DRegion region, out Vector2 pivot, out float drawRotation, out float scale)
  {
    region = null;
    pivot = Vector2.Zero;
    drawRotation = rotation;
    scale = 1f;
    var fleet = TextureCache.Fleet;
    if (!Enabled || fleet == null || !TryGetShipSpec(type, out string path, out bool isTurntable, out scale))
      return false;

    if (isTurntable)
    {
      region = Frame(fleet, path, rotation, out pivot, out drawRotation);
    }
    else
    {
      region = fleet.Region(path);
      pivot = fleet.Pivot(path);
    }
    return true;
  }

  /// <summary>
  /// Draws animated Synty thruster plumes at each 3D nozzle of a prerendered ship.
  /// When the ship's stern faces away from the camera (<c>cos(frameAngle) &lt; 0</c>),
  /// plumes are drawn in the back pass before the hull; otherwise in the front pass.
  /// </summary>
  public static void DrawEngine(SpriteBatch spriteBatch, Harvester harvester, Transform2 drawTransform,
    float drawRotation, float prerenderedScale, float depth, float totalTime, bool frontPass)
  {
    if (harvester == null) return;
    float power = 1f;
    if (harvester.CurrentState == Harvester.HarvesterState.Refueling)
    {
      power = Math.Clamp((float)harvester.refuelProgressPercent / 100f, 0f, 1f);
      if (power <= 0.02f) return;
    }
    float boost = (harvester.LaunchThrusterTimeRemaining > 0f || harvester.OverdriveTimeRemaining > 0f) ? 1.32f : 1f;
    DrawEngine(spriteBatch, harvester.Type, harvester.Id, drawTransform, drawRotation,
      prerenderedScale, depth, totalTime, frontPass, power, boost, alpha: 1f, shaderAlphaEncoding: true);
  }

  public static void DrawEngine(SpriteBatch spriteBatch, Harvester.HarvesterType type, int id,
    Transform2 drawTransform, float drawRotation, float prerenderedScale, float depth, float totalTime,
    bool frontPass, float power = 1f, float boost = 1f, float alpha = 1f, bool shaderAlphaEncoding = true)
  {
    var fleet = TextureCache.Fleet;
    if (!Enabled || fleet == null || alpha <= 0.005f || power <= 0.02f) return;
    if (!TryGetShipSpec(type, out string path, out bool isTurntable, out _)) return;

    var nozzles = fleet.Nozzles(path);
    if (nozzles.IsEmpty) return;

    float frameAngle = isTurntable ? drawTransform.Rotation - drawRotation : 0f;
    bool sternFacesCamera = MathF.Cos(frameAngle) >= 0f;
    if (frontPass != sternFacesCamera) return;

    float powerScale = 0.25f + 0.75f * power;
    float alphaOut = shaderAlphaEncoding ? 1.0f : alpha;

    if (boosterFrameNames == null)
    {
      boosterFrameNames = new string[FleetAtlas.EngineFrameCount];
      for (int i = 0; i < boosterFrameNames.Length; i++)
        boosterFrameNames[i] = FleetAtlas.PrerenderBoosterEngine + "#" + i;
    }

    float cosF = MathF.Cos(frameAngle), sinF = MathF.Sin(frameAngle);
    float cosR = MathF.Cos(drawRotation), sinR = MathF.Sin(drawRotation);
    Vector2 worldScale = drawTransform.Scale * prerenderedScale;
    int baseFrame = (int)(totalTime * (1000f / FleetAtlas.EngineFrameMilliseconds));

    for (int i = 0; i < nozzles.Length; i++)
    {
      ref readonly var nozzle = ref nozzles[i];
      float hx = nozzle.X * cosF - nozzle.Y * sinF;
      float hy = (nozzle.X * sinF + nozzle.Y * cosF) * SinElevation - nozzle.Z * CosElevation;
      Vector2 localOffset = new Vector2(hx, hy) * worldScale;
      Vector2 rotatedOffset = new Vector2(
        localOffset.X * cosR - localOffset.Y * sinR,
        localOffset.X * sinR + localOffset.Y * cosR);

      float phase = totalTime * 26f + i * 1.9f + id * 0.7f;
      float lenFlicker = 1f + 0.08f * MathF.Sin(phase) + 0.05f * MathF.Sin(phase * 2.3f);
      float widFlicker = 1f - 0.04f * MathF.Sin(phase);
      float glowPulse = 0.92f + 0.08f * MathF.Sin(phase * 1.4f);

      float effectiveWidth = nozzle.Width <= 8f ? nozzle.Width : 8f + (nozzle.Width - 8f) * 0.15f;
      float widthScale = (effectiveWidth / 5.2f) * widFlicker * powerScale;
      float lengthScale = (effectiveWidth / 5.2f) * 1.36f * lenFlicker * powerScale * boost;

      int frameIdx = Math.Abs(baseFrame + i * 3 + id) % boosterFrameNames.Length;
      string frameKey = boosterFrameNames[frameIdx];
      var region = fleet.Region(frameKey);
      var pivot = fleet.Pivot(frameKey);
      Vector2 nozzlePos = drawTransform.Position + rotatedOffset;

      // 1. Wide soft cyan-blue thruster glow halo around the nozzle and exhaust plume.
      float glowStrength = Math.Clamp((0.48f + 0.52f * power) * glowPulse * alpha * ThrusterGlow, 0f, 1f);
      Color glowTint = new Color(new Color(72, 194, 255) * glowStrength, alphaOut);
      spriteBatch.Draw(region.Texture, nozzlePos, region.Bounds, glowTint,
        drawTransform.Rotation, pivot, worldScale * new Vector2(widthScale * 1.78f * (0.7f + 0.3f * ThrusterGlow), lengthScale * 1.24f),
        SpriteEffects.None, depth);

      // 2. Main thruster plume.
      float mainStrength = Math.Clamp((0.55f + 0.45f * power) * alpha * (0.6f + 0.4f * ThrusterGlow), 0f, 1f);
      Color mainTint = new Color(new Color(217, 245, 255) * mainStrength, alphaOut);
      spriteBatch.Draw(region.Texture, nozzlePos, region.Bounds, mainTint,
        drawTransform.Rotation, pivot, worldScale * new Vector2(widthScale * 1.06f, lengthScale * 1.04f),
        SpriteEffects.None, depth);

      // 3. Hot incandescent white-cyan core at the nozzle exit.
      float coreStrength = Math.Clamp((0.65f + 0.35f * power) * glowPulse * alpha * ThrusterGlow, 0f, 1f);
      Color coreTint = new Color(new Color(242, 252, 255) * coreStrength, alphaOut);
      spriteBatch.Draw(region.Texture, nozzlePos, region.Bounds, coreTint,
        drawTransform.Rotation, pivot, worldScale * new Vector2(widthScale * 0.72f, lengthScale * 0.68f),
        SpriteEffects.None, depth);
    }
  }

  public static float ThrusterGlow = 3.00f;

  /// <summary>
  /// Draws a prerendered ship and its 3D nozzle thruster plumes for the main menu and transition.
  /// Uses <see cref="HarvesterScale"/> directly since menu ships do not bake per-type entity scaling into <c>Transform.Scale</c>.
  /// </summary>
  public static bool DrawMenuShip(SpriteBatch spriteBatch, Harvester.HarvesterType type, int id,
    Transform2 transform, Color color, float alpha, float totalTime, float boost = 1f)
  {
    if (!TryGet(type, transform.Rotation, out var prerendered, out var pivot, out float drawRotation, out _))
      return false;

    float scale = type == Harvester.HarvesterType.HomeBase ? HomeBaseScale : HarvesterScale;
    DrawEngine(spriteBatch, type, id, transform, drawRotation, scale, 0f, totalTime,
      frontPass: false, power: 1f, boost: boost, alpha: alpha, shaderAlphaEncoding: false);
    spriteBatch.Draw(prerendered.Texture, transform.Position, prerendered.Bounds, color * alpha,
      drawRotation, pivot, transform.Scale * scale, SpriteEffects.None, 0f);
    DrawEngine(spriteBatch, type, id, transform, drawRotation, scale, 0f, totalTime,
      frontPass: true, power: 1f, boost: boost, alpha: alpha, shaderAlphaEncoding: false);
    return true;
  }

  /// <summary>
  /// Unrotated world-unit exhaust offset for a prerendered ship (before entity scale),
  /// derived from its 3D nozzle metadata.
  /// </summary>
  public static bool TryGetExhaustOffset(Harvester.HarvesterType type, float side, out Vector2 offset)
  {
    offset = Vector2.Zero;
    var fleet = TextureCache.Fleet;
    if (!Enabled || fleet == null || !TryGetShipSpec(type, out string path, out _, out float scale))
      return false;

    var nozzles = fleet.Nozzles(path);
    if (nozzles.IsEmpty) return false;

    ref readonly var picked = ref nozzles[0];
    for (int i = 0; i < nozzles.Length; i++)
    {
      if (side < 0f && nozzles[i].X < -1f) { picked = ref nozzles[i]; break; }
      if (side > 0f && nozzles[i].X > 1f) { picked = ref nozzles[i]; break; }
    }
    offset = new Vector2(picked.X, picked.Y * SinElevation - picked.Z * CosElevation) * scale;
    return true;
  }
}
