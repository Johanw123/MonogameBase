using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Systems
{
  //public class UpdateSystem : EntityProcessingSystem
  //{
  //  private ComponentMapper<Gem> _gemMapper;

  //  public UpdateSystem()
  //    : base(Aspect.All().One(typeof(Gem)/*, typeof(Sprite)*/))
  //  {

  //  }

  //  public override void Initialize(IComponentMapperService mapperService)
  //  {
  //    _gemMapper = mapperService.GetMapper<Gem>();
  //  }


  //  public override void Process(GameTime gameTime, int entityId)
  //  {
  //    //var gem = _gemMapper.Get(entityId);
  //    //gem.Update(gameTime, null, false);
  //  }
  //}

  public class UpdateSystem2 : EntityUpdateSystem
  {
    private ComponentMapper<Gem> _gemMapper;
    private OrthographicCamera m_camera;

    public static UpdateSystem2 Instance;


    public UpdateSystem2(OrthographicCamera camera) : base(Aspect.All(typeof(Gem)))
    {
      m_camera = camera;
      Instance = this;
    }

    protected override void OnEntityAdded(int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      // if (gem != null)
      //   _gems.Add(entityId);

      if (gem != null)
      {
        var gridId = HarvesterCollectionSystem.Instance.flatSpatialHash.AddGem(gem.Id, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y);
        gem.GridIndex = gridId;
        Console.WriteLine("Add to grid: " + gridId);
      }
    }

    public Entity GetEntityP(int entityId)
    {
      return GetEntity(entityId);
    }

    protected override void OnEntityRemoved(int entityId)
    {
      // var gem = _gemMapper.Get(entityId);
      // if (gem != null)
      //   _gems.Remove(entityId);
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _gemMapper = mapperService.GetMapper<Gem>();
    }

    public override void Update(GameTime gameTime)
    {
      var mouse = MouseExtended.GetState();
      var mouseWorldPos = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);


      // foreach(var a in flatSpatialHash.Gems)
      // {
      //   if(!a.IsActive) continue;
      //
      //   var e = GetEntity(a.EntityId);
      //   if(e == null) continue;
      //   var pos = e.Get<Transform2>().Position;
      //   var gem = e.Get<Gem>();
      //   if(gem == null) continue;
      //   if (gem.PositionMoved)
      //   {
      //     Console.WriteLine("Pos moved: " + a.EntityId);
      //     flatSpatialHash.Gems[gem.GridIndex].X = pos.X;
      //     flatSpatialHash.Gems[gem.GridIndex].Y = pos.Y;
      //   }
      // }


      foreach (var id in ActiveEntities)
      {
        var e = GetEntity(id);
        var gem = e.Get<Gem>();
        gem.Update(gameTime, mouseWorldPos, isMouseClicked, gameTime.GetElapsedSeconds());

        HarvesterCollectionSystem.Instance.flatSpatialHash.Gems[gem.GridIndex].X = gem.BoundingCircle.Center.X;
        HarvesterCollectionSystem.Instance.flatSpatialHash.Gems[gem.GridIndex].Y = gem.BoundingCircle.Center.Y;

        if (gem.ShouldDestroy)
        {
          e.Destroy();
          EntityFactory.Instance.GemPool.Free(gem);
          switch (gem.GemType)
          {
            case GemTypes.LightGreen:
            case GemTypes.Red:
              EntityFactory.Instance.SpritePoolRed.Free(e.Get<Sprite>());
              break;
          }
        }
      }

      // foreach (var id in ActiveEntities)
      // {
      //   var e = GetEntity(id);
      //   var gem = e.Get<Gem>();
      //   if (gem.ShouldDestroy)
      //   {
      //     e.Destroy();
      //     EntityFactory.Instance.GemPool.Free(gem);
      //     switch (gem.GemType)
      //     {
      //       case GemTypes.LightGreen:
      //       case GemTypes.Red:
      //         EntityFactory.Instance.SpritePoolRed.Free(e.Get<Sprite>());
      //         break;
      //     }
      //   }
      // }
    }
  }
}
