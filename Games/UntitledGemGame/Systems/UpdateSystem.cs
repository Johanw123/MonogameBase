using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Systems
{
  public class UpdateSystem : EntityProcessingSystem
  {
    private ComponentMapper<Gem> _gemMapper;

    public UpdateSystem()
      : base(Aspect.All().One(typeof(Gem)/*, typeof(Sprite)*/))
    {

    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _gemMapper = mapperService.GetMapper<Gem>();
    }


    public override void Process(GameTime gameTime, int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      gem.Update(gameTime);
    }
  }
}
