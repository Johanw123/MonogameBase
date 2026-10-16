using System.Collections.Generic;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

/// <summary>
/// Shipyard preview of each harvester spinning on a low-angle showroom turntable,
/// prerendered by render_showcase in tools/blender/prerender.py. A sheet holds
/// <see cref="FrameCount"/> equal frames in <see cref="Columns"/> columns; frame i is
/// the ship turned i * 360 / FrameCount degrees clockwise seen from above, with
/// its pivot at the frame centre. Sheets load the first time a ship is shown.
/// </summary>
public static class ShipShowcase
{
  // Must match SHOWCASE in tools/blender/prerender.py.
  public const int FrameCount = 96;
  public const int Columns = 12;
  public const int Rows = (FrameCount + Columns - 1) / Columns;
  public const float FrameDegrees = 360f / FrameCount;
  /// <summary>Camera elevation above the horizon the frames were rendered at.</summary>
  public const float Elevation = 20f;

  private static readonly Dictionary<Harvester.HarvesterType, Texture2D> sheets = new();

  private static string SheetPath(Harvester.HarvesterType type) => type switch
  {
    Harvester.HarvesterType.Harvester => "Textures/Prerender/Showcase/harvester_bomber01.png",
    Harvester.HarvesterType.AdvancedHarvester => "Textures/Prerender/Showcase/harvester_fighterheavy01.png",
    Harvester.HarvesterType.ExpertHarvester => "Textures/Prerender/Showcase/harvester_stealth01.png",
    Harvester.HarvesterType.UltimateHarvester => "Textures/Prerender/Showcase/harvester_fighterheavy04.png",
    Harvester.HarvesterType.PerimeterHarvester => "Textures/Prerender/Showcase/harvester_stealth05.png",
    _ => null
  };

  /// <summary>The ship's spin sheet, or null if it has none.</summary>
  /// <remarks>
  /// Loaded on the game thread the first time it is asked for (one short hitch per
  /// ship). LoadAsync would deadlock: its worker holds the loader's lock while
  /// MonoGame waits on this thread to create the texture, so any synchronous load
  /// here in the meantime (core shard icons, for one) blocks both.
  /// </remarks>
  public static Texture2D Sheet(Harvester.HarvesterType type)
  {
    if (!sheets.TryGetValue(type, out var sheet))
    {
      string path = SheetPath(type);
      sheet = path == null ? null : AssetManager.Load<Texture2D>(path);
      // A missing file loads as the 1x1 default texture rather than failing.
      if (sheet != null && (sheet.Width < Columns || sheet.Height < Rows)) sheet = null;
      sheets.Add(type, sheet);
    }
    return sheet;
  }

  /// <summary>Source rectangle of a frame; any index wraps around the turn.</summary>
  public static Rectangle Frame(Texture2D sheet, int frame)
  {
    int width = sheet.Width / Columns, height = sheet.Height / Rows;
    frame %= FrameCount;
    if (frame < 0) frame += FrameCount;
    return new Rectangle(frame % Columns * width, frame / Columns * height, width, height);
  }
}
