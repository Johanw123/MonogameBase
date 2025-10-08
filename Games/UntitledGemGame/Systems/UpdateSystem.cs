using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
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

  public class UpdateSystem2 : EntityUpdateSystem
  {
    private ComponentMapper<Gem> _gemMapper;
    private Bag<int> _gems = new(500000000);


    public UpdateSystem2() : base(Aspect.All(typeof(Gem)))
    {
    }

    protected override void OnEntityAdded(int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      if(gem != null)
        _gems.Add(entityId);
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      if (gem != null)
        _gems.Remove(entityId);
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _gemMapper = mapperService.GetMapper<Gem>();
    }

    public override void Update(GameTime gameTime)
    {
      Parallel.ForEach(_gems, id =>
        {
          var e = GetEntity(id);
          var gem = e.Get<Gem>();
          gem.Update(gameTime);
        }
      );

      foreach (var id in _gems)
      {
        var e = GetEntity(id);
        var gem = e.Get<Gem>();
        if (gem.ShouldDestroy)
        {
          e.Destroy();
          EntityFactory.GemPool.Free(gem);
        }
      }
    }
  }

}
