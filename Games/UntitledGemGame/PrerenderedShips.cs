using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
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

  private const float FrameStep = MathHelper.TwoPi / FleetAtlas.TurntableFrameCount;
  private static readonly Dictionary<string, string[]> frameNames = new(StringComparer.Ordinal);

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
    if (!Enabled || fleet == null) return false;

    switch (type)
    {
      case Harvester.HarvesterType.HomeBase:
        region = fleet.Region(FleetAtlas.PrerenderHomeBase);
        pivot = fleet.Pivot(FleetAtlas.PrerenderHomeBase);
        scale = HomeBaseScale;
        return true;
      case Harvester.HarvesterType.Harvester:
        region = Frame(fleet, FleetAtlas.PrerenderHarvesterTurntable, rotation, out pivot, out drawRotation);
        scale = HarvesterScale;
        return true;
      default:
        return false;
    }
  }
}
