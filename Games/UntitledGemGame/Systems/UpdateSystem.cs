using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System.Collections.Generic;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems
{
  public class UpdateSystem2 : EntityUpdateSystem
  {
    private ComponentMapper<Gem> _gemMapper;
    private readonly OrthographicCamera m_camera;
    private readonly List<Gem> _awake = new();
    private List<Gem> _hovered = new();
    private List<Gem> _nextHovered = new();
    private uint _hoverFrame;
    private PlayAreaBounds _previousBounds;
    public static UpdateSystem2 Instance;
    public int UpdatingGemCount => _awake.Count;

    public UpdateSystem2(OrthographicCamera camera) : base(Aspect.All(typeof(Gem)))
    {
      m_camera = camera;
      Instance = this;
    }

    protected override void OnEntityAdded(int entityId)
    {
      // EntityManager broadcasts creation to all systems, regardless of Aspect.
      var gem = _gemMapper.Get(entityId);
      if (gem == null) return;
      gem.UpdateRegistered = true;
      Wake(gem);
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      if (gem == null || gem.Id != entityId) return;
      Sleep(gem);
      gem.UpdateRegistered = false;
    }

    internal void Wake(Gem gem)
    {
      if (gem.UpdateListIndex >= 0) return;
      gem.UpdateListIndex = _awake.Count;
      _awake.Add(gem);
    }

    private void Sleep(Gem gem)
    {
      int index = gem.UpdateListIndex;
      if (index < 0) return;
      var last = _awake[^1];
      _awake[index] = last;
      last.UpdateListIndex = index;
      _awake.RemoveAt(_awake.Count - 1);
      gem.UpdateListIndex = -1;
    }

    public Entity GetEntityP(int entityId) => GetEntity(entityId);

    public ulong GetUncollectedGemValue()
    {
      ulong value = 0;
      foreach (int id in ActiveEntities)
      {
        var gem = _gemMapper.Get(id);
        if (gem != null && !gem.PickedUp && !gem.ShouldDestroy)
          value = PrestigeProgression.AddSaturating(value, gem.BaseValue);
      }
      return value;
    }

    public void FinishPrestigeCollection()
    {
      foreach (int id in ActiveEntities)
        _gemMapper.Get(id).ShouldDestroy = true;
    }

    public override void Initialize(IComponentMapperService mapperService)
      => _gemMapper = mapperService.GetMapper<Gem>();

    public override void Update(GameTime gameTime)
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      var mouse = MouseExtended.GetState();
      var mousePosition = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      bool clicked = mouse.WasButtonPressed(MouseButton.Left) && !RenderGuiSystem.Instance.drawUpgradesGui;
      float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
      var bounds = PlayAreaBounds.ForCamera(m_camera);
      bool boundsChanged = bounds.Minimum != _previousBounds.Minimum || bounds.Maximum != _previousBounds.Maximum;
      _previousBounds = bounds;
      bool magnetsActive = MagnetizerCache.ActiveMagnets.Count > 0;
      bool prestiging = UntitledGemGameGameScreen.Instance.m_prestiging;

      // Idle gems sleep indefinitely. Only camera changes, prestige, or an active
      // magnet require a population-wide update; ordinary frames visit animations.
      if (boundsChanged || magnetsActive || prestiging)
        foreach (int id in ActiveEntities) Wake(_gemMapper.Get(id));

      // Hover and clicks use the same persistent index instead of touching every gem.
      ++_hoverFrame;
      _nextHovered.Clear();
      float halfWidth = TextureCache.HudRedGem.Value.Width * UpgradeManager.Instance.UG.ClickRadius * 0.5f;
      float halfHeight = TextureCache.HudRedGem.Value.Height * UpgradeManager.Instance.UG.ClickRadius * 0.5f;
      foreach (int index in grid.Query(mousePosition.X, mousePosition.Y, halfWidth, halfHeight))
      {
        var gem = _gemMapper.Get(grid.Gems[index].EntityId);
        // Factory spawns are indexed before ECS registers their components.
        if (gem == null || !gem.UpdateRegistered || gem.ShouldDestroy) continue;
        gem.SetHovered(true);
        gem.HoverFrame = _hoverFrame;
        _nextHovered.Add(gem);
        if (clicked) gem.OnClicked(true);
      }
      foreach (var gem in _hovered)
        if (gem.UpdateRegistered && gem.HoverFrame != _hoverFrame) gem.SetHovered(false);
      (_hovered, _nextHovered) = (_nextHovered, _hovered);

      for (int i = 0; i < _awake.Count;)
      {
        var gem = _awake[i];
        if (!gem.ShouldDestroy)
        {
          gem.Update(gameTime, dt);
          gem.ConstrainToPlayArea(bounds);
          RenderGemSystem.Instance?.UpdateGem(gem.Id);
          if (!gem.PickedUp && !gem.WasClicked)
          {
            grid.MoveGem(gem.GridIndex, gem.BoundingCircle.Center.X, gem.BoundingCircle.Center.Y);
            grid.SetCollectionRadius(gem.GridIndex, gem.CollectionRadius);
          }

          // Clicked gems have left the index. Deliver directly on arrival so
          // their flight never needs a spatial query or a second claim.
          if (gem.WasClicked && !gem.PickedUp)
          {
            var home = HomeBase.Instance.Entity.Get<Harvester>();
            float reach = BaseStats.GetHarvesterCollectionRange(home) + gem.CollectionRadius;
            if (Vector2.DistanceSquared(gem.BoundingCircle.Center, home.BoundingCircle.Center) <= reach * reach)
              HarvesterCollectionSystem.Instance.CollectGem(gem, home);
          }
        }

        if (gem.ShouldDestroy)
        {
          var entity = GetEntity(gem.Id);
          var sprite = entity.Get<Sprite>();
          Sleep(gem);
          gem.UpdateRegistered = false;
          grid.RecycleIndex(gem.GridIndex);
          RenderGemSystem.Instance?.RemoveGem(gem.Id);
          entity.Destroy();
          EntityFactory.Instance.GemPool.Free(gem);
          EntityFactory.Instance.SpritePoolRed.Free(sprite);
        }
        else if (!gem.NeedsUpdate && !magnetsActive)
          Sleep(gem);
        else
          ++i;
      }
    }
  }
}
