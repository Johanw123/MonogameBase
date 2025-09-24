using FrogFight.Components;
using GGPOSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using FrogFight.Physics;
using JapeFramework.Aseprite;
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

      var animatedSprite = AsepriteHelper.LoadTaggedAnimations(ContentDirectory.Textures.Game.Frog.frog_animations_combined_png, 
                                ContentDirectory.Textures.Game.Frog.frog_animations_combined_json,
                                ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);

      entity.Attach(animatedSprite);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(new PhysicsBody(position, m_physicsWorld, new Vector2(11, 9) / 24.0f)); //TODO: 24 is PTM value
      entity.Attach(new Player { PlayerNumber = playerNumber, NetworkHandle = networkHandle, NetworkPlayerInfo = networkPlayerInfo, IsLocalPlayer = isLocal });
      return entity;
    }
  }
}
