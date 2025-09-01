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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FrogFight.Physics;
using JapeFramework.JsonClasses;
using MonoGame.Extended.Serialization.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AsepriteFileExtensions = MonoGame.Aseprite.AsepriteFileExtensions;
using World = MonoGame.Extended.ECS.World;

namespace FrogFight
{
  public class EntityFactory
  {
    private readonly World m_ecsWorld;
    private nkast.Aether.Physics2D.Dynamics.World m_physicsWorld;
    private GraphicsDevice m_graphicsDevice;

    public EntityFactory(World ecs_world, nkast.Aether.Physics2D.Dynamics.World physicsWorld, GraphicsDevice graphicsDevice)
    {
      m_ecsWorld = ecs_world;
      m_physicsWorld = physicsWorld;
      m_graphicsDevice = graphicsDevice;
    }

    public Entity CreatePlayer(Vector2 position, int playerNumber, int networkHandle, GGPOPlayer? networkPlayerInfo, bool isLocal)
    {
      var entity = m_ecsWorld.CreateEntity();

      var img = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Game.Frog.frog_animations_combined_png);

      var jsonText = AssetManager.Load<string>(ContentDirectory.Textures.Game.Frog.frog_animations_combined_json);
      var json = JsonConvert.DeserializeObject<Root>(jsonText);

      var framesObject = JObject.Parse(json.Frames.ToString());

      List<Root2> frames = new List<Root2>();

      foreach (var framePair in framesObject)
      {
        var key = framePair.Key;
        var val = framePair.Value;

        var frameData = JsonConvert.DeserializeObject<Root2>(val.ToString());
        frames.Add(frameData);
      }

      var fileName = Path.GetFileNameWithoutExtension(json.Meta.Image);

      var dudeAtlas = Texture2DAtlas.Create($"TextureAtlas//{fileName}", img, frames[0].Frame.W, frames[0].Frame.H);
      var spriteSheet = new SpriteSheet($"SpriteSheet//{fileName}", dudeAtlas);

      foreach (var frameTag in json.Meta.FrameTags)
      {
        var animName = frameTag.Name;
        var from = frameTag.From;
        var to = frameTag.To;

        for (int i = from; i < to; i++)
        {
          dudeAtlas.CreateRegion(frames[i].Frame.X, frames[i].Frame.Y, frames[i].Frame.W, frames[i].Frame.H, animName + i);
        }

        spriteSheet.DefineAnimation(animName, builder =>
        {
          builder.IsLooping(true);
          for (int i = from; i < to; i++)
          {
            builder.AddFrame(animName + i, TimeSpan.FromMilliseconds(frames[i].Duration));
          }
        });
      }

      var animSprite = new AnimatedSprite(spriteSheet, "lick");
      //animSprite.Origin = new Vector2(11 / 2.0f, 9 / 2.0f);
      animSprite.Origin = new Vector2(frames[0].Frame.W / 2.0f, frames[0].Frame.H / 2.0f);
      entity.Attach(animSprite);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(new PhysicsBody(position, m_physicsWorld, new Vector2(11, 9) / 24.0f)); //TODO: 24 is PTM value
      entity.Attach(new Player { PlayerNumber = playerNumber, NetworkHandle = networkHandle, NetworkPlayerInfo = networkPlayerInfo, IsLocalPlayer = isLocal });
      return entity;
    }
  }
}
