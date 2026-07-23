using AsyncContent;
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
using static Assimp.Metadata;
using World = MonoGame.Extended.ECS.World;

namespace UntitledGemGame
{
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

    //private Texture2D m_harvesterTexture;

    //private Texture2D rtsSpriteSheet;
    //private Dictionary<string, Texture2DRegion> rtsSpriteSheetRegions;

    public EntityFactory(World ecs_world, GraphicsDevice graphicsDevice)
    {
      Instance = this;

      m_ecsWorld = ecs_world;

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
      foreach(var e in Beacons)
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
      entity.Attach(new Harvester { Shape = new CollisionShape2D(new BoundingCircle2D(position, sprite.TextureRegion.Height)), Id = entity.Id, m_sprite = sprite });
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
      // entity.Attach(new Harvester { Bounds = new CircleF(position, sprite.TextureRegion.Height), Id = entity.Id, m_sprite = sprite, ForceInstantCollection = true });
      entity.Attach(new Harvester { Entity = entity, IsDrone = true, Shape = new CollisionShape2D(new BoundingCircle2D(position, sprite.TextureRegion.Height)), Id = entity.Id, m_sprite = sprite, ForceInstantCollection = true });

      Drones.Add(entity.Id, entity);

      return entity;
    }

    public HomeBase HomeBase;

    public Entity CreateHomeBase(Vector2 position, Vector2 initialOffsetPos)
    {
      var entity = m_ecsWorld.CreateEntity();

      var sprite = new Sprite(TextureCache.HomeBase);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      var scale = 1.0f;

      entity.Attach(new Transform2(position + initialOffsetPos, 0, new Vector2(scale, scale)));
      entity.Attach(sprite);
      HomeBase = new HomeBase { Shape = new CollisionShape2D(new BoundingCircle2D(position, sprite.TextureRegion.Width * scale)), Entity = entity };
      entity.Attach(HomeBase);

      entity.Attach(new Harvester() { CurrentState = Harvester.HarvesterState.None, Shape = new CollisionShape2D(new BoundingCircle2D(position, sprite.TextureRegion.Height)), Id = entity.Id, ForceInstantCollection = true });

      return entity;
    }

    public Entity CreateGem(Vector2 position, GemTypes type, uint baseValue)
    {
      //Add a stagger for when too many gems are created at the same time (spread out to multiple frames to avoid lag)
      var entity = m_ecsWorld.CreateEntity();
      var transform = new Transform2(position, 0, Vector2.One);
      var b = Math.Clamp(baseValue, 0, 500);
      var bc = Math.Clamp(baseValue, 0, 255.0f);

      Sprite sprite;
      switch (type)
      {
        case GemTypes.Red:
          sprite = SpritePoolRed.Obtain();
          transform.Scale += Vector2.One * (b / 500.0f);
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

          transform.Scale += Vector2.One * (b / 500.0f);
          // Console.WriteLine("scale: " + transform.Scale);
          // transform.Scale = Vector2.One * 2.0f;

          // transform.Scale = Vector2.One * (0.9f + (baseValue / 255.0f));
          break;
        default:
          sprite = SpritePoolRed.Obtain();
          break;
      }

      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(sprite);
      entity.Attach(transform);

      var gem = GemPool.Obtain();

      gem.GemType = type;
      gem.Initialize(entity, sprite.TextureRegion.Width, baseValue);
      entity.Attach(gem);

      return entity;
    }
  }
}
