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

  private readonly Texture2DAtlas atlas;
  private readonly Dictionary<string, Texture2DRegion> regions = new(StringComparer.Ordinal);
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
    }
  }

  public Texture2DRegion Region(string path) => regions[path];

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
