using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;

namespace UntitledGemGame.Systems
{
  public class Harvester
  {

  }

  public class HarvesterSystem : EntityProcessingSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<AnimatedSprite> _spriteMapper;
    private ComponentMapper<Transform2> _transformMapper;

    public HarvesterSystem()
        : base(Aspect.All(typeof(Harvester), typeof(Transform2), typeof(AnimatedSprite)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _harvesterMapper = mapperService.GetMapper<Harvester>();
      _spriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _transformMapper = mapperService.GetMapper<Transform2>();
    }

    public override void Process(GameTime gameTime, int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);
      var sprite = _spriteMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);
      
      var keyboardState = KeyboardExtended.GetState();

      foreach (var activeEntity in ActiveEntities)
      {
        var e = GetEntity(activeEntity);

        if (e.Has<Gem>())
        {
          //Check close to harvester
          if (Vector2.Distance(e.Get<Transform2>().Position, transform.Position) < 10)
          {
            e.Destroy();
          }
        }
      }

    }
  }
}
