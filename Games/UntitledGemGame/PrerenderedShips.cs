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

  // World units covered by one texel of the prerendered image. Picked so the new
  // ships cover about the same area as the old hulls (home base ~90 units tall,
  // harvester ~30 units wide); tweak to resize them without re-rendering.
  public const float HomeBaseScale = 0.19f;
  public const float HarvesterScale = 0.26f;

  // Camera elevation (65 deg above the horizon) used by tools/blender/prerender.py.
  private const float SinElevation = 0.9063078f;
  private const float CosElevation = 0.4226183f;

  private const float FrameStep = MathHelper.TwoPi / FleetAtlas.TurntableFrameCount;
  private static readonly Dictionary<string, string[]> frameNames = new(StringComparer.Ordinal);
  private static string[] boosterFrameNames;

  private static bool TryGetShipSpec(Harvester.HarvesterType type,
    out string path, out bool isTurntable, out float scale)
  {
    switch (type)
    {
      case Harvester.HarvesterType.HomeBase:
        path = FleetAtlas.PrerenderHomeBase;
        isTurntable = false;
        scale = HomeBaseScale;
        return true;
      case Harvester.HarvesterType.Harvester:
        path = FleetAtlas.PrerenderHarvesterTurntable;
        isTurntable = true;
        scale = HarvesterScale;
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
    var fleet = TextureCache.Fleet;
    if (!Enabled || fleet == null || harvester == null) return;
    if (!TryGetShipSpec(harvester.Type, out string path, out bool isTurntable, out _)) return;

    var nozzles = fleet.Nozzles(path);
    if (nozzles.IsEmpty) return;

    float frameAngle = isTurntable ? drawTransform.Rotation - drawRotation : 0f;
    bool sternFacesCamera = MathF.Cos(frameAngle) >= 0f;
    if (frontPass != sternFacesCamera) return;

    float power = 1f;
    if (harvester.CurrentState == Harvester.HarvesterState.Refueling)
    {
      power = Math.Clamp((float)harvester.refuelProgressPercent / 100f, 0f, 1f);
      if (power <= 0.02f) return;
    }

    float boost = (harvester.LaunchThrusterTimeRemaining > 0f || harvester.OverdriveTimeRemaining > 0f) ? 1.32f : 1f;
    float powerScale = 0.25f + 0.75f * power;
    Color tint = new Color(Color.White * (0.45f + 0.55f * power), 1.0f);

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

      float phase = totalTime * 26f + i * 1.9f + harvester.Id * 0.7f;
      float lenFlicker = 1f + 0.08f * MathF.Sin(phase) + 0.05f * MathF.Sin(phase * 2.3f);
      float widFlicker = 1f - 0.04f * MathF.Sin(phase);

      float effectiveWidth = nozzle.Width <= 8f ? nozzle.Width : 8f + (nozzle.Width - 8f) * 0.15f;
      float widthScale = (effectiveWidth / 5.2f) * widFlicker * powerScale;
      float lengthScale = (effectiveWidth / 5.2f) * 1.36f * lenFlicker * powerScale * boost;

      int frameIdx = Math.Abs(baseFrame + i * 3 + harvester.Id) % boosterFrameNames.Length;
      string frameKey = boosterFrameNames[frameIdx];
      var region = fleet.Region(frameKey);
      var pivot = fleet.Pivot(frameKey);

      spriteBatch.Draw(region.Texture, drawTransform.Position + rotatedOffset, region.Bounds, tint,
        drawTransform.Rotation, pivot, worldScale * new Vector2(widthScale, lengthScale),
        SpriteEffects.None, depth);
    }
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
