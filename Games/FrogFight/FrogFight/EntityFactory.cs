using AsepriteDotNet.Aseprite;
using AsyncContent;
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
    private readonly World m_ecsWorld;
    private nkast.Aether.Physics2D.Dynamics.World m_physicsWorld;

    public EntityFactory(World ecs_world, nkast.Aether.Physics2D.Dynamics.World physicsWorld)
    {
      m_ecsWorld = ecs_world;
      m_physicsWorld = physicsWorld;
    }

    public Entity CreatePlayer(Vector2 position, int playerNumber, int networkHandle, GGPOPlayer? networkPlayerInfo, bool isLocal)
    {
      var entity = m_ecsWorld.CreateEntity();

      int width = 50, height = 50; 

      AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.frogpack_spritesheets.full_sheet, false, texture2D =>
      {
        var dudeAtlas = Texture2DAtlas.Create("TextureAtlas//full_sheet", texture2D, width, height);
        var spriteSheet = new SpriteSheet("SpriteSheet//full_sheet", dudeAtlas);

        dudeAtlas.CreateRegion(14, 32, 11, 9, "idle0");
        dudeAtlas.CreateRegion(64, 31, 11, 10, "idle1");
        dudeAtlas.CreateRegion(114, 30, 11, 11, "idle2");
        dudeAtlas.CreateRegion(164, 30, 11, 11, "idle3");

        spriteSheet.DefineAnimation("idle", builder =>
        {
          builder.IsLooping(true);
          builder.AddFrame("idle0", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("idle1", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("idle2", TimeSpan.FromSeconds(0.1f));
          builder.AddFrame("idle3", TimeSpan.FromSeconds(0.1f));

        });

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

        var animSprite = new AnimatedSprite(spriteSheet, "idle");
        animSprite.Origin = new Vector2(11 / 2.0f, 9 / 2.0f);

        entity.Attach(animSprite);
      });

      float scale = 32.0f;
      float colliderScale = 15.0f;

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(new PhysicsBody(position, m_physicsWorld, new Vector2(11, 9) / 24.0f)); //TODO: 24 is PTM value
      entity.Attach(new Player { PlayerNumber = playerNumber, NetworkHandle = networkHandle, NetworkPlayerInfo = networkPlayerInfo, IsLocalPlayer = isLocal });
      return entity;
    }

    //private void AddAnimationCycle(Texture2DAtlas atlas, SpriteSheet spriteSheet, int x, int y, int w, int h, int size, int numFrames, string name, bool looping)
    //{
    //  for (int i = 0; i < numFrames; i++)
    //  {
    //    atlas.CreateRegion(x + i * size, y, w, h, name + i.ToString());
    //  }

    //  spriteSheet.DefineAnimation(name, builder =>
    //  {
    //    builder.IsLooping(looping);
    //    for (int i = 0; i < numFrames; i++)
    //    {
    //      builder.AddFrame(name + i.ToString(), TimeSpan.FromSeconds(0.1f));
    //    }
    //  });
    //}

    //private void AddAnimationCycle(SpriteSheet spriteSheet, string name, int[] frames, bool isLooping = true, float frameDuration = 0.1f)
    //{
    //  spriteSheet.DefineAnimation(name, builder =>
    //  {
    //    builder.IsLooping(isLooping);
    //    for (int i = 0; i < frames.Length; i++)
    //    {
    //      builder.AddFrame(frames[i], TimeSpan.FromSeconds(frameDuration));
    //    }
    //  });
    //}

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
