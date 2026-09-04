using AsyncContent;
using JapeFramework;
using JapeFramework.Aseprite;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

using World = MonoGame.Extended.ECS.World;

//using static Assimp.Metadata;

namespace UntitledGemGame
{
  public struct GemSpawnData
  {
    public Vector2 Position;
    public GemTypes Type;
    public uint BaseValue;
    public bool IsLucky;
  }

  public class EntityFactory
  {
    private readonly World m_ecsWorld;
    private GraphicsDevice m_graphicsDevice;

    public Pool<Gem> GemPool;
    public Pool<MagnetBeacon> BeaconPool;
    public Pool<Sprite> SpritePoolRed;
    // public static Pool<Sprite> SpritePoolBlue;
    // private Pool<Harvester> harvesterPool;
    // private Texture2D gemTextureRed;
    // private Texture2D gemTextureBlue;
    private Texture2DRegion gemTextureRegionRed;
    // private Texture2DRegion gemTextureRegionBlue;

    public static EntityFactory Instance;
    OrthographicCamera m_camera;

    private Queue<GemSpawnData> _gemSpawnQueue = new Queue<GemSpawnData>(5000);
    private const int MAX_SPAWNS_PER_FRAME = 50; // Tweak this until lag disappears

    //private Texture2D m_harvesterTexture;

    //private Texture2D rtsSpriteSheet;
    //private Dictionary<string, Texture2DRegion> rtsSpriteSheetRegions;

    public EntityFactory(World ecs_world, GraphicsDevice graphicsDevice, OrthographicCamera camera)
    {
      Instance = this;

      m_ecsWorld = ecs_world;
      m_camera = camera;

      m_graphicsDevice = graphicsDevice;

      GemPool = new Pool<Gem>(() => new Gem(), gem => gem.Reset(), 1000000);
      SpritePoolRed = new Pool<Sprite>(() => new Sprite(TextureCache.HudRedGem), sprite => sprite.TextureRegion = gemTextureRegionRed, 100000);
      BeaconPool = new Pool<MagnetBeacon>(() => new MagnetBeacon());


      // SpritePoolBlue = new Pool<Sprite>(() => new Sprite(TextureCache.HudBlueGem), sprite => sprite.TextureRegion = gemTextureRegionBlue, 100000);

      gemTextureRegionRed = new Texture2DRegion(TextureCache.HudRedGem);
      // gemTextureRegionBlue = new Texture2DRegion(TextureCache.HudBlueGem);
    }

    //private void LoadFromXml(string xml)
    //{
    //  XmlDocument doc = new XmlDocument();
    //  doc.LoadXml(xml);
    //  var nodes = doc.DocumentElement.SelectNodes("SubTexture");

    //  rtsSpriteSheetRegions = new Dictionary<string, Texture2DRegion>();

    //  foreach (XmlNode node in nodes)
    //  {
    //    string name = node.Attributes["name"]?.InnerText;
    //    int x = int.Parse(node.Attributes["x"]?.InnerText);
    //    int y = int.Parse(node.Attributes["y"]?.InnerText);
    //    int w = int.Parse(node.Attributes["width"]?.InnerText);
    //    int h = int.Parse(node.Attributes["height"]?.InnerText);

    //    rtsSpriteSheetRegions.Add(name, new Texture2DRegion(rtsSpriteSheet, x, y, w, h));
    //  }
    //}

    //public static List<Texture2DRegion> m_harvesterRegions = new List<Texture2DRegion>();

    //private void LoadRegions(int numFrames)
    //{
    //  var pngPath = ContentDirectory.Textures.isometric_vehicles.redcar_png;
    //  var img = AssetManager.Load<Texture2D>(pngPath);
    //  var fileName = Path.GetFileNameWithoutExtension(pngPath);

    //  var dudeAtlas = Texture2DAtlas.Create($"TextureAtlas//{fileName}", img, img.Width, img.Height);
    //  var spriteSheet = new SpriteSheet($"SpriteSheet//{fileName}", dudeAtlas);

    //  var w = (float)spriteSheet.TextureAtlas.Texture.Width / numFrames;

    //  for (int i = 0; i < numFrames; i++)
    //  {
    //    var region = dudeAtlas.CreateRegion((int)(i * w), 0, (int)w, img.Height, "regionName" + i);
    //    m_harvesterRegions.Add(region);
    //  // var region = new Texture2DRegion((int)(i * w), 0, (int)w, img.Height, "regionName" + i);

    //  }
    //}

    public Dictionary<int, Entity> Harvesters = new();
    public Dictionary<int, Entity> AdvancedHarvesters = new();
    public Dictionary<int, Entity> ExpertHarvesters = new();
    public Dictionary<int, Entity> UltimateHarvesters = new();
    public Dictionary<int, Entity> Beacons = new();
    public Dictionary<int, Entity> Drones = new();

    public void RemoveHarvester(Dictionary<int, Entity> collection, int id)
    {
      if (collection.Remove(id, out var e))
      {
        //e.Get<Harvester>().
        e.Destroy();
      }
    }

    public void RemoveRandomHarvester(Dictionary<int, Entity> collection)
    {
      if (Harvesters.Count == 0)
        return;

      RemoveHarvester(collection, collection.Keys.FirstOrDefault());
    }

    public Entity CreateBeacon(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      // var animatedSprite = AsepriteHelper.LoadAnimation(
      //   "Textures/black_hole.png",
      //   true,
      //   12,
      //   150);

      var sprite = new Sprite(TextureCache.HomeBase);
      // var sprite = new Sprite(animatedSprite);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      var scale = 0.4f;

      entity.Attach(new Transform2(position, 0, new Vector2(scale, scale)));
      entity.Attach(sprite);

      var beacon = BeaconPool.Obtain();
      entity.Attach(beacon);

      Beacons.Add(entity.Id, entity);

      return entity;
    }

    public void DestroyBeacons()
    {
      foreach (var e in Beacons)
      {
        BeaconPool.Free(e.Value.Get<MagnetBeacon>());
        e.Value.Destroy();
      }
      Beacons.Clear();
    }


    public Entity CreateHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.HarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      entity.Attach(sprite);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(animatedSprite);

      Harvesters.Add(entity.Id, entity);

      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite, m_engineSprite = animatedSprite, CollectionStrategy = HarvesterStrategy.RandomScreenPosition, Type = Harvester.HarvesterType.Harvester };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      entity.Attach(harvester);

      return entity;
    }

    public Entity CreateAdvancedHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Fighter - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.AdvancedHarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      entity.Attach(sprite);

      entity.Attach(new Transform2(position, 0, Vector2.One * 0.8f));
      entity.Attach(animatedSprite);

      AdvancedHarvesters.Add(entity.Id, entity);

      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite, m_engineSprite = animatedSprite, CollectionStrategy = HarvesterStrategy.RandomGemPosition, Type = Harvester.HarvesterType.AdvancedHarvester };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      entity.Attach(harvester);

      return entity;
    }

    public Entity CreateExpertHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Bomber - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.ExpertHarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      entity.Attach(sprite);

      entity.Attach(new Transform2(position, 0, Vector2.One * 0.8f));
      entity.Attach(animatedSprite);

      ExpertHarvesters.Add(entity.Id, entity);

      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite, m_engineSprite = animatedSprite, CollectionStrategy = HarvesterStrategy.TargetCluster, Type = Harvester.HarvesterType.ExpertHarvester };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      entity.Attach(harvester);

      return entity;
    }

    public Entity CreateUltimateHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Frigate - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.UltimateHarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      entity.Attach(sprite);

      entity.Attach(new Transform2(position, 0, Vector2.One * 0.8f));
      entity.Attach(animatedSprite);

      UltimateHarvesters.Add(entity.Id, entity);

      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite, m_engineSprite = animatedSprite, CollectionStrategy = HarvesterStrategy.TargetClosestCluster, Type = Harvester.HarvesterType.UltimateHarvester };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      entity.Attach(harvester);

      return entity;
    }

    public Entity CreateDrone(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Support Ship - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.DroneShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      entity.Attach(sprite);
      entity.Attach(animatedSprite);
      entity.Attach(new Transform2(position, 0, Vector2.One * 0.4f));
      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite, m_engineSprite = animatedSprite, CollectionStrategy = HarvesterStrategy.RandomScreenPosition, Type = Harvester.HarvesterType.Drone };
      // harvester.BoundingCircle = new BoundingCircle2D(position, sprite.TextureRegion.Height);

      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      // harvester.Shape = new CollisionShape2D(harvester.BoundingCircle);
      entity.Attach(harvester);

      Drones.Add(entity.Id, entity);

      return entity;
    }

    // public HomeBase HomeBase;

    public Entity CreateHomeBase(Vector2 position, Vector2 initialOffsetPos)
    {
      var entity = m_ecsWorld.CreateEntity();

      var sprite = new Sprite(TextureCache.HomeBase);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      var scale = 1.0f;

      entity.Attach(new Transform2(position + initialOffsetPos, 0, new Vector2(scale, scale)));
      entity.Attach(sprite);
      var homebase = new HomeBase { Entity = entity };
      entity.Attach(homebase);

      var harvester = new Harvester() { CurrentState = Harvester.HarvesterState.None, Id = entity.Id, CollectionStrategy = HarvesterStrategy.None, Type = Harvester.HarvesterType.HomeBase };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Width * scale);
      entity.Attach(harvester);

      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);

      return entity;
    }

    public void QueueGemSpawn(Vector2 position, GemTypes type, uint baseValue, bool isLucky = false)
    {
      _gemSpawnQueue.Enqueue(new GemSpawnData { Position = position, Type = type, BaseValue = baseValue, IsLucky = isLucky });
    }

    public void Update()
    {
      int spawnsThisFrame = 0;

      while (_gemSpawnQueue.Count > 0 && spawnsThisFrame < MAX_SPAWNS_PER_FRAME)
      {
        var data = _gemSpawnQueue.Dequeue();
        CreateGem(data.Position, data.Type, data.BaseValue, data.IsLucky);
        spawnsThisFrame++;
      }
    }

    public Entity CreateGem(Vector2 position, GemTypes type, uint baseValue, bool isLucky = false)
    {
      var entity = m_ecsWorld.CreateEntity();

      float visualScale = BaseStats.GetGemVisualScale(baseValue);
      var transform = new Transform2(position, 0, Vector2.One * visualScale);
      Sprite sprite = SpritePoolRed.Obtain();
      Color gemColor = GemQualityTable.GetColor(type);
      // Alpha is intentionally used as a shader metadata channel. The gem
      // shader reconstructs visible alpha from the texture itself.
      sprite.Color = isLucky
        ? new Color(gemColor.R, gemColor.G, gemColor.B, byte.MaxValue)
        : gemColor;

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(0, 0);
      var p1 = m_camera.ScreenToWorld(vp.X + vp.Width, vp.Y + vp.Height - vp.Height * 0.07f);

      if (position.Y > p1.Y)
        position.Y = p1.Y - RandomHelper.Float(0, 25.0f);

      if (position.X > p1.X)
        position.X = p1.X;

      if (position.Y < p0.Y)
        position.Y = p0.Y;

      if (position.X < p0.X)
        position.X = p0.X;

      transform.Position = position;

      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(sprite);
      entity.Attach(transform);

      var gem = GemPool.Obtain();

      gem.GemType = type;
      gem.IsLucky = isLucky;
      gem.Initialize(entity, sprite.TextureRegion.Width, baseValue);
      entity.Attach(gem);

      var gridId = HarvesterCollectionSystem.Instance.flatSpatialHash.AddGem(gem.Id, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y, gem.BaseValue);
      gem.GridIndex = gridId;

      return entity;
    }
  }
}
