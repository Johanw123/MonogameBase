using System;
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
    public string Name { get; set; }
  }

  public class HarvesterSystem : EntityUpdateSystem
  {
    // private ComponentMapper<Harvester> _harvesterMapper;
    // private ComponentMapper<AnimatedSprite> _spriteMapper;
    // private ComponentMapper<Transform2> _transformMapper;

    public HarvesterSystem()
        : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      // _harvesterMapper = mapperService.GetMapper<Harvester>();
      // _spriteMapper = mapperService.GetMapper<AnimatedSprite>();
      // _transformMapper = mapperService.GetMapper<Transform2>();
    }

    public override void Update(GameTime gameTime)
    {
      // var harvester = _harvesterMapper.Get(entityId);
      // var sprite = _spriteMapper.Get(entityId);
      // var transform = _transformMapper.Get(entityId);
      //
      // var keyboardState = KeyboardExtended.GetState();

      // if (harvester != null)
      {

        foreach (var activeEntity in ActiveEntities)
        {
          var harvester = GetEntity(activeEntity).Get<Harvester>();
          if (harvester == null)
            continue;

          foreach (var gemEntityId in ActiveEntities)
          {
            var gemEntity = GetEntity(gemEntityId);
            if (gemEntity.Get<Gem>() == null)
              continue;

            var gemTransform = gemEntity.Get<Transform2>();
            var harvesterTransform = GetEntity(activeEntity).Get<Transform2>();


            if (Vector2.Distance(gemTransform.Position, harvesterTransform.Position) < 25)
            {
              // Gem is close to harvester
              // You can add logic here if needed
              gemEntity.Destroy();
            }
          }
          // var e = GetEntity(activeEntity);
          //
          // if (e.Has<Gem>())
          // {
          //   //Check close to harvester
          //   if (Vector2.Distance(e.Get<Transform2>().Position, transform.Position) < 10)
          //   {
          //     e.Destroy();
          //   }
          // }
        }
      }
    }
  }
}
