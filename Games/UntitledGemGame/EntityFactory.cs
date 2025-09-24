using System;
using JapeFramework.Aseprite;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame.Systems;
using World = MonoGame.Extended.ECS.World;

namespace UntitledGemGame
{
  public enum GemTypes
  {
    Blue,
    DarkBlue,
    Gold,
    LightGreen,
    Lilac,
    Purple,
    Red,
    Teal
  }

  public class Gem
  {
    public string Name { get; set; }
  }

  public class EntityFactory
  {
    private readonly World m_ecsWorld;
    private GraphicsDevice m_graphicsDevice;

    public EntityFactory(World ecs_world, GraphicsDevice graphicsDevice)
    {
      m_ecsWorld = ecs_world;

      m_graphicsDevice = graphicsDevice;
    }

    public Entity CreateHarvester(Vector2 position)
    {
      var entity = m_ecsWorld.CreateEntity();

      var animatedSprite = AsepriteHelper.LoadAnimation(
        ContentDirectory.Textures.Gems.Gem1.GEM1_BLUE_Spritesheet_png,
        true,
        10,
        100);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(animatedSprite);
      entity.Attach(new Harvester());

      return entity;
    }

    public Entity CreateGem(Vector2 position, GemTypes type)
    {
      var entity = m_ecsWorld.CreateEntity();

      string sheet = "";

      switch (type)
      {
        case GemTypes.Blue:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_BLUE_Spritesheet_png;
          break;
        case GemTypes.DarkBlue:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_DARKBLUE_Spritesheet_png;
          break;
        case GemTypes.Gold:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_GOLD_Spritesheet_png;
          break;
        case GemTypes.LightGreen:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_LIGHTGREEN_Spritesheet_png;
          break;
        case GemTypes.Lilac:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_LILAC_Spritesheet_png;
          break;
        case GemTypes.Purple:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_PURPLE_Spritesheet_png;
          break;
        case GemTypes.Red:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_RED_Spritesheet_png;
          break;
        case GemTypes.Teal:
          sheet = ContentDirectory.Textures.Gems.Gem1.GEM1_TURQUOISE_Spritesheet_png;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(type), type, null);
      }

      var animatedSprite = AsepriteHelper.LoadAnimation(
                                sheet,
                                true,
                                10,
                                100);

      entity.Attach(new Transform2(position, 0, Vector2.One));
      entity.Attach(animatedSprite);
      entity.Attach(new Gem());

      return entity;
    }
  }
}
