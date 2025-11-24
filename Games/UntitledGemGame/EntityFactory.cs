using AsyncContent;
using JapeFramework.Aseprite;
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

    public static Pool<Gem> GemPool;
    public static Pool<Sprite> SpritePoolRed;
    public static Pool<Sprite> SpritePoolBlue;
    // private Pool<Harvester> harvesterPool;
    private Texture2D gemTextureRed;
    private Texture2D gemTextureBlue;
    private Texture2DRegion gemTextureRegionRed;
    private Texture2DRegion gemTextureRegionBlue;

    //private Texture2D m_harvesterTexture;

    //private Texture2D rtsSpriteSheet;
    //private Dictionary<string, Texture2DRegion> rtsSpriteSheetRegions;

    public EntityFactory(World ecs_world, GraphicsDevice graphicsDevice)
    {
      m_ecsWorld = ecs_world;

      m_graphicsDevice = graphicsDevice;

      GemPool = new Pool<Gem>(() => new Gem(), gem => gem.Reset(), 1000000);
      SpritePoolRed = new Pool<Sprite>(() => new Sprite(gemTextureRed), sprite => sprite.TextureRegion = gemTextureRegionRed, 100000);
      SpritePoolBlue = new Pool<Sprite>(() => new Sprite(gemTextureBlue), sprite => sprite.TextureRegion = gemTextureRegionBlue, 100000);

      gemTextureRed = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
      gemTextureRegionRed = new Texture2DRegion(gemTextureRed);

      gemTextureBlue = AssetManager.Load<Texture2D>("Textures/Gems/Gem2GrayStatic.png");
      gemTextureRegionBlue = new Texture2DRegion(gemTextureBlue);

      // m_harvesterTexture = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MarkIII_Woods_png);

      //rtsSpriteSheet = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Kenny.scifiRTS_spritesheet_png);
      //LoadFromXml(AssetManager.Load<string>(ContentDirectory.Textures.Kenny.scifiRTS_spritesheet_xml));
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

    //private Texture2D tex = AssetManager.Load<Texture2D>(ContentDirectory.Textures.isometric_vehicles.redcar_png);

    public Entity CreateHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        ContentDirectory.Textures.tiny_spaceships.tinyShip8_png,
        true,
        6,
        150);

      //var sprite = SpritePool.Obtain();

      //var sprite = new Sprite(m_harvesterTexture);
      //sprite.Color = Color.White;
      //sprite.TextureRegion = rtsSpriteSheetRegions["scifiUnit_06.png"];

      //var animatedSprite = AsepriteHelper.LoadAnimation(
      //  ContentDirectory.Textures.isometric_vehicles.redcar_png,
      //  false,
      //  8,
      //  0);

      //prite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(animatedSprite);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      //entity.Attach(animatedSprite);

      Harvesters.Add(entity.Id, entity);

      //entity.Attach(new Harvester { Bounds = new RectangleF(position.X, position.Y, animatedSprite.TextureRegion.Width, animatedSprite.TextureRegion.Height) });
      entity.Attach(new Harvester { Bounds = new CircleF(position, animatedSprite.TextureRegion.Height), ID = entity.Id, m_sprite = animatedSprite });
      return entity;
    }

    public Entity CreateHomeBase(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        ContentDirectory.Textures.Gems.Gem1.GEM1_PURPLE_Spritesheet_png,
        true,
        10,
        100);

      var scale = 2.0f;

      entity.Attach(new Transform2(position, 0, new Vector2(scale, scale)));
      entity.Attach(animatedSprite);
      //entity.Attach(new HomeBase { Bounds = new RectangleF(position.X, position.Y, animatedSprite.TextureRegion.Width * scale, animatedSprite.TextureRegion.Height * scale) });
      entity.Attach(new HomeBase { Bounds = new CircleF(position, animatedSprite.TextureRegion.Width * scale) });

      entity.Attach(new Harvester() { CurrentState = Harvester.HarvesterState.None, Bounds = new CircleF(position, animatedSprite.TextureRegion.Height), ID = entity.Id });

      return entity;
    }

    public Entity CreateGem(Vector2 position, GemTypes type)
    {
      var entity = m_ecsWorld.CreateEntity();

      // string sheet = "";
      //
      // switch (type)
      // {
      //   case GemTypes.Blue:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_BLUE_Spritesheet_png;
      //     break;
      //   case GemTypes.DarkBlue:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_DARKBLUE_Spritesheet_png;
      //     break;
      //   case GemTypes.Gold:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_GOLD_Spritesheet_png;
      //     break;
      //   case GemTypes.LightGreen:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_LIGHTGREEN_Spritesheet_png;
      //     break;
      //   case GemTypes.Lilac:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_LILAC_Spritesheet_png;
      //     break;
      //   case GemTypes.Purple:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_PURPLE_Spritesheet_png;
      //     break;
      //   case GemTypes.Red:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_RED_Spritesheet_png;
      //     break;
      //   case GemTypes.Teal:
      //     sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_TURQUOISE_Spritesheet_png;
      //     break;
      //   default:
      //     throw new ArgumentOutOfRangeException(nameof(type), type, null);
      // }

      //Cache
      //var animatedSprite = AsepriteHelper.LoadAnimation(
      //                          sheet,
      //                          true,
      //                          10,
      //                          100);

      var transform = new Transform2(position, 0, Vector2.One);
      //entity.Attach(animatedSprite);


      Sprite sprite;
      switch (type)
      {
        case GemTypes.Red:
          // transform.Scale = new Vector2(1.0f, 1.0f);
          sprite = SpritePoolRed.Obtain();
          sprite.Color = Color.Red;
          break;
        case GemTypes.Blue:
          transform.Scale = new Vector2(0.1f, 0.5f);
          sprite = SpritePoolBlue.Obtain();

          // sprite.Color = Color.Cyan;
          break;

        default:
          sprite = SpritePoolRed.Obtain();
          break;
      }
      // var sprite = SpritePool.Obtain();

      //var sprite = new Sprite(AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png))
      //  {
      //    Color = Color.Red
      //  };
      //sprite.TextureRegion = rtsSpriteSheetRegions["scifiEnvironment_02.png"];
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      entity.Attach(sprite);
      entity.Attach(transform);

      //var effect = AssetManager.Load<Effect>(ContentDirectory.Shaders.GemShader_fx);
      //entity.Attach(effect);

      //var gem = new Gem();
      var gem = GemPool.Obtain();
      //gem.Initialize(entity, new RectangleF(position.X, position.Y, animatedSprite.TextureRegion.Width,
      //  animatedSprite.TextureRegion.Height));

      gem.Initialize(entity, sprite.TextureRegion.Width);
      entity.Attach(gem);

      return entity;
    }



  }
}
