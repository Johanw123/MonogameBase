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
    public Dictionary<int, Entity> Beacons = new();
    public Dictionary<int, Entity> Drones = new();

    public void RemoveHarvester(int id)
    {
      if (Harvesters.Remove(id, out var e))
      {
        //e.Get<Harvester>().
        e.Destroy();
      }
    }

    public void RemoveRandomHarvester()
    {
      if (Harvesters.Count == 0)
        return;

      RemoveHarvester(Harvesters.Keys.FirstOrDefault());
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

      //prite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(sprite);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(animatedSprite);

      Harvesters.Add(entity.Id, entity);

      //entity.Attach(new Harvester { Bounds = new RectangleF(position.X, position.Y, animatedSprite.TextureRegion.Width, animatedSprite.TextureRegion.Height) });
      // entity.Attach(new Harvester { Shape = new CollisionShape2D(new BoundingCircle2D(position, sprite.TextureRegion.Height)), Id = entity.Id, m_sprite = sprite });


      var harvester = new Harvester { Entity = entity, Id = entity.Id, m_sprite = sprite };
      // harvester.BoundingCircle = new BoundingCircle2D(position, sprite.TextureRegion.Height);
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);
      // harvester.Shape = new CollisionShape2D(harvester.BoundingCircle);
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
      var harvester = new Harvester { Entity = entity, IsDrone = true, Id = entity.Id, m_sprite = sprite, ForceInstantCollection = true };
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

      var harvester = new Harvester() { CurrentState = Harvester.HarvesterState.None, Id = entity.Id, ForceInstantCollection = true };
      harvester.SetCollisionPosition(position, sprite.TextureRegion.Width * scale);
      entity.Attach(harvester);

      harvester.SetCollisionPosition(position, sprite.TextureRegion.Height);

      return entity;
    }

    public void QueueGemSpawn(Vector2 position, GemTypes type, uint baseValue)
    {
      _gemSpawnQueue.Enqueue(new GemSpawnData { Position = position, Type = type, BaseValue = baseValue });
    }

    public void Update()
    {
      int spawnsThisFrame = 0;

      while (_gemSpawnQueue.Count > 0 && spawnsThisFrame < MAX_SPAWNS_PER_FRAME)
      {
        var data = _gemSpawnQueue.Dequeue();
        CreateGem(data.Position, data.Type, data.BaseValue);
        spawnsThisFrame++;
      }
    }

    public Entity CreateGem(Vector2 position, GemTypes type, uint baseValue)
    {
      var entity = m_ecsWorld.CreateEntity();




      var transform = new Transform2(position, 0, Vector2.One);
      float scaleMax = 5000000.0f;
      var b = Math.Clamp(baseValue, 0, scaleMax);
      var bc = Math.Clamp(baseValue, 0, 255.0f);

      Sprite sprite;
      switch (type)
      {
        case GemTypes.Red:
          sprite = SpritePoolRed.Obtain();
          transform.Scale += Vector2.One * (b / scaleMax);
          sprite.Color = new Color(255, 0, 0, 0);
          break;
        // case GemTypes.Blue:
        //   transform.Scale = new Vector2(0.1f, 0.5f);
        //   sprite = SpritePoolBlue.Obtain();
        //   break;
        case GemTypes.LightGreen:
          sprite = SpritePoolRed.Obtain();
          // sprite.Color = new Color(51, 180, 51, 255);
          // sprite.Color = new Color(255, 0, 0, RandomHelper.Int(0, 200));
          sprite.Color = new Color(255, 0, (int)bc, 0);

          transform.Scale += Vector2.One * (b / scaleMax);
          // Console.WriteLine("scale: " + transform.Scale);
          // transform.Scale = Vector2.One * 2.0f;

          // transform.Scale = Vector2.One * (0.9f + (baseValue / 255.0f));
          break;
        default:
          sprite = SpritePoolRed.Obtain();
          break;
      }

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(0, 0);
      var p1 = m_camera.ScreenToWorld(vp.X + vp.Width, vp.Y + vp.Height - vp.Height * 0.07f);

      if(position.Y > p1.Y)
        position.Y = p1.Y - RandomHelper.Float(0, 25.0f);

      if(position.X > p1.X)
        position.X = p1.X;

      if(position.Y < p0.Y)
        position.Y = p0.Y;

      if(position.X < p0.X)
        position.X = p0.X;

      transform.Position = position;

      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(sprite);
      entity.Attach(transform);

      var gem = GemPool.Obtain();

      gem.GemType = type;
      gem.Initialize(entity, sprite.TextureRegion.Width, baseValue);
      entity.Attach(gem);

      var gridId = HarvesterCollectionSystem.Instance.flatSpatialHash.AddGem(gem.Id, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y, gem.BaseValue);
      gem.GridIndex = gridId;

      return entity;
    }
  }
}
