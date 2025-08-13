using AsepriteDotNet.Aseprite;
using AsyncContent;
using Box2dNet.Interop;
using FrogFight.Collisions;
using FrogFight.Components;
using FrogFight.Entities;
using GGPOSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Graphics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrogFight.Physics;
using World = MonoGame.Extended.ECS.World;


namespace FrogFight
{
  public class EntityFactory
  {
    private readonly World _world;
    private readonly ContentManager _contentManager;
    private b2WorldId m_worldId;

    public EntityFactory(World world, ContentManager contentManager, b2WorldId worldId)
    {
      _world = world;
      _contentManager = contentManager;
      m_worldId = worldId;
    }

    public Entity CreatePlayer(Vector2 position, int playerNumber, int networkHandle, GGPOPlayer? networkPlayerInfo, bool isLocal)
    {
      var entity = _world.CreateEntity();

      int width = 50, height = 50; 

      AssetManager.Load<Texture2D>(ContentDirectory.Textures.frogpack_spritesheets.full_sheet, false, texture2D =>
      {
        var dudeAtlas = Texture2DAtlas.Create("TextureAtlas//full_sheet", texture2D, width, height);
        var spriteSheet = new SpriteSheet("SpriteSheet//full_sheet", dudeAtlas);

        //for (int i = 0; i < 4; i++)
        //{
        //  dudeAtlas.CreateRegion(14 + i * 50, 32, 11, 9, "region_idle" + i.ToString());
        //}

        //spriteSheet.DefineAnimation("idle", builder =>
        //{
        //  builder.IsLooping(true);
        //  for (int i = 0; i < 4; i++)
        //  {
        //    builder.AddFrame("region_idle" + i.ToString(), TimeSpan.FromSeconds(0.1f));
        //  }
        //});

        AddAnimationCycle(dudeAtlas, spriteSheet, 14, 40, 11, 9, 50, 4, "idle", true);
        //AddAnimationCycle(dudeAtlas, spriteSheet, 14, 190, 15, 18, 50, 6, "hop", false);


        dudeAtlas.CreateRegion(14, 183, 11, 8, "hop0");
        dudeAtlas.CreateRegion(61, 176, 15, 12, "hop1");
        dudeAtlas.CreateRegion(111, 173, 15, 12, "hop2");
        dudeAtlas.CreateRegion(163, 175, 13, 12, "hop3");
        dudeAtlas.CreateRegion(214, 178, 12, 13, "hop4");
        dudeAtlas.CreateRegion(264, 182, 11, 9, "hop5");


        spriteSheet.DefineAnimation("hop", builder =>
        {
          builder.IsLooping(false);
          builder.AddFrame("hop0", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("hop1", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("hop2", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("hop3", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("hop4", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("hop5", TimeSpan.FromSeconds(0.1f));
        });

        //AddAnimationCycle(spriteSheet, "idle", new[] { 0, 1, 2, 3, 2, 1 });
        //AddAnimationCycle(spriteSheet, "jump", new[] { 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }, false);
        //AddAnimationCycle(spriteSheet, "land", new[] { 24, 25, 26 }, false);
        //AddAnimationCycle(spriteSheet, "hop", new[] { 36, 37, 38, 39, 40, 41 }, false);
        //AddAnimationCycle(spriteSheet, "freight", new[] { 48, 49, 50, 51, 52, 53, 54, 55 }, false);

        var animSprite = new AnimatedSprite(spriteSheet, "idle");
        //animSprite.Origin = new Vector2(1, 1);
        entity.Attach(animSprite);
      });

      float scale = 3.0f;
      float colliderScale = 15.0f;

      entity.Attach(new Transform2(position, 0, Vector2.One * scale));
      entity.Attach(new PhysicsBody(position /*+ new Vector2(colliderScale, colliderScale) * 0.5f * scale*/, m_worldId, new Vector2(colliderScale, colliderScale) * scale));
      entity.Attach(new Player { PlayerNumber = playerNumber, NetworkHandle = networkHandle, NetworkPlayerInfo = networkPlayerInfo, IsLocalPlayer = isLocal });
      return entity;
    }

    private void AddAnimationCycle(Texture2DAtlas atlas, SpriteSheet spriteSheet, int x, int y, int w, int h, int size, int numFrames, string name, bool looping)
    {
      for (int i = 0; i < numFrames; i++)
      {
        atlas.CreateRegion(x + i * size, y - h, w, h, name + i.ToString());
      }

      spriteSheet.DefineAnimation(name, builder =>
      {
        builder.IsLooping(looping);
        for (int i = 0; i < numFrames; i++)
        {
          builder.AddFrame(name + i.ToString(), TimeSpan.FromSeconds(0.1f));
        }
      });
    }

    private void AddAnimationCycle(SpriteSheet spriteSheet, string name, int[] frames, bool isLooping = true, float frameDuration = 0.1f)
    {
      spriteSheet.DefineAnimation(name, builder =>
      {
        builder.IsLooping(isLooping);
        for (int i = 0; i < frames.Length; i++)
        {
          builder.AddFrame(frames[i], TimeSpan.FromSeconds(frameDuration));
        }
      });
    }

    //public void CreateTile(int x, int y, int width, int height)
    //{
    //  var entity = _world.CreateEntity();
    //  entity.Attach(new Body
    //  {
    //    Position = new Vector2(x * width - width * 0.5f, y * height - height * 0.5f),
    //    Size = new Vector2(width, height),
    //    BodyType = BodyType.Static
    //  });
    //}
  }
}
