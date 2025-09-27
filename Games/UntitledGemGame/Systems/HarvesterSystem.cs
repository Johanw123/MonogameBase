using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using static Assimp.Metadata;

namespace UntitledGemGame.Systems
{
  public class Harvester
  {
    public string Name { get; set; }

    public Vector2? TargetScreenPosition { get; set; } = null;
    //public Vector2 Velocity { get; set; } = Vector2.One;
  }

  public class HarvesterMoveSystem : EntityProcessingSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Transform2> _transformMapper;

    private OrthographicCamera m_camera;

    public HarvesterMoveSystem(OrthographicCamera camera) : base(Aspect.All(typeof(Harvester), typeof(Transform2)))
    {
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _harvesterMapper = mapperService.GetMapper<Harvester>();
      _transformMapper = mapperService.GetMapper<Transform2>();
    }

    public override void Process(GameTime gameTime, int entityId)
    {
      var harvester = _harvesterMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);

      if (!harvester.TargetScreenPosition.HasValue || Vector2.Distance(/*m_camera.WorldToScreen(transform.Position)*/transform.Position, harvester.TargetScreenPosition.Value) < 10)
      {
        var newTargetPos = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        harvester.TargetScreenPosition = newTargetPos;
        //Console.WriteLine("New Pos: " + harvester.TargetScreenPosition);
      }
      else if(harvester.TargetScreenPosition.HasValue)
      {
        Vector2 dir = /*m_camera.WorldToScreen(transform.Position)*/ harvester.TargetScreenPosition.Value - transform.Position;
        dir.Normalize();
        transform.Position += dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 100.0f;
       // Console.WriteLine("Move: " + transform.Position + " - " + harvester.TargetScreenPosition);
      }
    }
  }

  public class HarvesterCollectionSystem : EntityUpdateSystem
  {
    private ComponentMapper<Harvester> _harvesterMapper;
    private ComponentMapper<Gem> _gemMapper;

    private Bag<int> _harvesters = new(50);
    private Bag<int> _gems = new(5000);

    public HarvesterCollectionSystem()
      : base(Aspect.All(typeof(Transform2), typeof(AnimatedSprite)).One(typeof(Harvester), typeof(Gem)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _harvesterMapper = mapperService.GetMapper<Harvester>();
      _gemMapper = mapperService.GetMapper<Gem>();
    }

    protected override void OnEntityAdded(int entityId)
    {
      if (_harvesterMapper.Get(entityId) != null)
        _harvesters.Add(entityId);
      else if (_gemMapper.Get(entityId) != null)
        _gems.Add(entityId);
    }

    protected override void OnEntityRemoved(int entityId)
    {
      Console.WriteLine("Remove: " + entityId);
      if (_harvesterMapper.Get(entityId) != null)
        _harvesters.Remove(entityId);
      else if (_gemMapper.Get(entityId) != null)
        _gems.Remove(entityId);
    }

    public override void Update(GameTime gameTime)
    {
      foreach (var activeEntity in _harvesters)
      {
        var harvester = GetEntity(activeEntity).Get<Harvester>();
        if (harvester == null)
          continue;

        //Implement quad trees or like barnes hut collision detection here
        foreach (var gemEntityId in _gems)
        {
          var gemEntity = GetEntity(gemEntityId);
          if (gemEntity == null)
            continue;

          var gemTransform = gemEntity.Get<Transform2>();
          var harvesterTransform = GetEntity(activeEntity).Get<Transform2>();

          if (Vector2.Distance(gemTransform.Position, harvesterTransform.Position) < 55)
          {
            gemEntity.Destroy();
          }
        }
      }
    }
  }
}
