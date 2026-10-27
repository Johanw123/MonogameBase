using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame;

/// <summary>Used fleet hulls and engine frames share one texture; names retain source paths.</summary>
public sealed class FleetAtlas
{
  public const string ScoutHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Scout - Base.png";
  public const string FighterHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Fighter - Base.png";
  public const string TorpedoHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Torpedo Ship - Base.png";
  public const string BomberHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Bomber - Base.png";
  public const string FrigateHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Frigate - Base.png";
  public const string DroneHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Support Ship - Base.png";
  public const string HomeBaseHull = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Battlecruiser - Base.png";
  public const string ScoutEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png";
  public const string FighterEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Fighter - Engine.png";
  public const string TorpedoEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Torpedo Ship - Engine.png";
  public const string BomberEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Bomber - Engine.png";
  public const string FrigateEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Frigate - Engine.png";
  public const string DroneEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Support Ship - Engine.png";
  public const string HomeBaseEngine = "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Battlecruiser - Engine.png";
  public const int EngineFrameCount = 8;
  public const int EngineFrameMilliseconds = 150;

  // Prerendered 3D ships (tools/blender/prerender.py, Synty POLYGON Sci-Fi Space).
  // Turntable strips hold TurntableFrameCount square frames; frame i is the ship
  // turned i * 360 / TurntableFrameCount degrees clockwise from nose-up.
  public const string PrerenderHomeBase = "Textures/Prerender/homebase_cruiser01.png";
  public const string PrerenderHarvesterTurntable = "Textures/Prerender/Turntables/harvester_bomber01.png";
  public const string PrerenderAdvancedHarvesterTurntable = "Textures/Prerender/Turntables/harvester_fighterheavy01.png";
  public const string PrerenderExpertHarvesterTurntable = "Textures/Prerender/Turntables/harvester_stealth01.png";
  public const string PrerenderUltimateHarvesterTurntable = "Textures/Prerender/Turntables/harvester_fighterheavy04.png";
  public const string PrerenderPerimeterHarvesterTurntable = "Textures/Prerender/Turntables/harvester_stealth05.png";
  public const string PrerenderDroneTurntable = "Textures/Prerender/Turntables/harvester_drone_repair01.png";
  public const string PrerenderBoosterEngine = "Textures/Prerender/Engine Effects/synty_booster.png";
  public const string PrerenderBoosterCore = "Textures/Prerender/Engine Effects/synty_booster_core.png";
  public const int TurntableFrameCount = 32;

  public readonly record struct ShipNozzle(float X, float Y, float Z, float Width);

  // Weapon turrets on the home base (GameScreen.Turrets.cs): a static base and a
  // head turntable that follows the aim, both centred on the turret's pivot.
  public const string TurretCannon = "Textures/Prerender/Turntables/turret_cannon.png";
  public const string TurretCannonBase = "Textures/Prerender/Weapons/turret_cannon_base.png";
  public const string TurretLaser = "Textures/Prerender/Turntables/turret_laser.png";
  public const string TurretLaserBase = "Textures/Prerender/Weapons/turret_laser_base.png";
  public const string TurretHarpoon = "Textures/Prerender/Turntables/turret_harpoon.png";
  public const string TurretHarpoonBase = "Textures/Prerender/Weapons/turret_harpoon_base.png";
  public const string TurretRockets = "Textures/Prerender/Turntables/turret_rockets.png";
  public const string TurretRocketsBase = "Textures/Prerender/Weapons/turret_rockets_base.png";
  public const string TurretRailgun = "Textures/Prerender/Turntables/turret_railgun.png";
  public const string TurretRailgunBase = "Textures/Prerender/Weapons/turret_railgun_base.png";
  public const string HullTerminal = "Textures/Prerender/Weapons/hull_terminal.png";

  private readonly Texture2DAtlas atlas;
  private readonly Dictionary<string, Texture2DRegion> regions = new(StringComparer.Ordinal);
  private readonly Dictionary<string, Vector2> pivots = new(StringComparer.Ordinal);
  private readonly Dictionary<string, ShipNozzle[]> nozzles = new(StringComparer.Ordinal);
  private readonly Dictionary<string, SpriteSheet> engines = new(StringComparer.Ordinal);
  public Texture2D Texture { get; }

  public FleetAtlas(Texture2D texture, string metadata)
  {
    Texture = texture;
    atlas = new Texture2DAtlas(texture);
    using var document = JsonDocument.Parse(metadata);
    foreach (var entry in document.RootElement.EnumerateObject())
    {
      var r = entry.Value;
      int x = r[0].GetInt32(), y = r[1].GetInt32(), w = r[2].GetInt32(), h = r[3].GetInt32();
      if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > texture.Width || y + h > texture.Height)
        throw new InvalidOperationException($"Invalid fleet atlas region: {entry.Name}");
      regions.Add(entry.Name, atlas.CreateRegion(x, y, w, h, entry.Name));
      // Trimmed prerendered images also store where the model's pivot lands.
      int len = r.GetArrayLength();
      if (len >= 6)
        pivots.Add(entry.Name, new Vector2(r[4].GetSingle(), r[5].GetSingle()));
      if (len >= 10)
      {
        int count = (len - 6) / 4;
        var list = new ShipNozzle[count];
        for (int i = 0; i < count; i++)
        {
          int b = 6 + i * 4;
          list[i] = new ShipNozzle(r[b].GetSingle(), r[b + 1].GetSingle(), r[b + 2].GetSingle(), r[b + 3].GetSingle());
        }
        nozzles[entry.Name.Split('#')[0]] = list;
      }
    }
  }

  public Texture2DRegion Region(string path) => regions[path];

  /// <summary>Region-local point to draw around: the stored pivot, else the centre.</summary>
  public Vector2 Pivot(string path)
    => pivots.TryGetValue(path, out var pivot) ? pivot : regions[path].Bounds.Size.ToVector2() / 2f;

  public ReadOnlySpan<ShipNozzle> Nozzles(string path)
    => nozzles.TryGetValue(path, out var list) ? list : ReadOnlySpan<ShipNozzle>.Empty;

  public AnimatedSprite CreateEngine(string path)
  {
    if (!engines.TryGetValue(path, out var sheet))
    {
      sheet = new SpriteSheet(path, atlas);
      sheet.DefineAnimation("engine", builder =>
      {
        builder.IsLooping(true);
        for (int i = 0; i < EngineFrameCount; i++)
          builder.AddFrame(path + "#" + i, TimeSpan.FromMilliseconds(EngineFrameMilliseconds));
      });
      engines.Add(path, sheet);
    }
    var frame = Region(path + "#0");
    // Origins are frame-local, never based on the size of the shared texture.
    return new AnimatedSprite(sheet, "engine") { Origin = new Vector2(frame.Width, frame.Height) / 2 };
  }
}
