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
    // Every gem on the field. Collected gems stay attached to their parked entities
    // (EntityFactory.ParkGem), so ActiveEntities also holds gems that are not in play.
    private readonly List<Gem> _live = new();
    private List<Gem> _hovered = new();
    private List<Gem> _nextHovered = new();
    private uint _hoverFrame;
    private readonly List<int> _directClicks = new();
    private readonly System.Func<int, double, bool> _collectManualGem;
    private ManualGravityField _manualGravity;
    private readonly System.Action<int, Vector2> _moveManualGravity;
    private readonly System.Func<int, Vector2, float, bool> _gravityOverlaps;
    private PlayAreaBounds _previousBounds;
    public static UpdateSystem2 Instance;
    public int UpdatingGemCount => _awake.Count;
    public int LiveGemCount => _live.Count;

    public UpdateSystem2(OrthographicCamera camera) : base(Aspect.All(typeof(Gem)))
    {
      m_camera = camera;
      _moveManualGravity = MoveManualGravity;
      _gravityOverlaps = GravityOverlaps;
      _collectManualGem = CollectManualGem;
      Instance = this;
    }

    protected override void OnEntityAdded(int entityId)
    {
      // EntityManager broadcasts creation to all systems, regardless of Aspect.
      var gem = _gemMapper.Get(entityId);
      if (gem == null || gem.Id != entityId) return;
      RegisterGem(gem);
    }

    // A gem entering play: a new entity (on its ECS event) or a reused one (EntityFactory).
    internal void RegisterGem(Gem gem)
    {
      if (gem.UpdateRegistered) return;
      if (_manualGravity != null && UntitledGemGameGameScreen.Instance != null)
        _manualGravity.RegisterSpawn(HarvesterCollectionSystem.Instance.flatSpatialHash, gem.GridIndex,
          UntitledGemGameGameScreen.Instance.ManualAbilities);
      gem.UpdateRegistered = true;
      gem.LiveListIndex = _live.Count;
      _live.Add(gem);
      Wake(gem);
    }

    protected override void OnEntityRemoved(int entityId)
    {
      var gem = _gemMapper.Get(entityId);
      if (gem == null || gem.Id != entityId) return;
      Sleep(gem);
      RemoveLive(gem);
      gem.UpdateRegistered = false;
    }

    private void RemoveLive(Gem gem)
    {
      int index = gem.LiveListIndex;
      if (index < 0) return;
      var last = _live[^1];
      _live[index] = last;
      last.LiveListIndex = index;
      _live.RemoveAt(_live.Count - 1);
      gem.LiveListIndex = -1;
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

    private void WakeNearMagnets(GemSpatialIndex grid)
    {
      foreach (var magnet in MagnetizerCache.ActiveMagnets)
        foreach (int index in grid.Query(magnet.Position.X, magnet.Position.Y, Gem.MagnetRange, Gem.MagnetRange))
        {
          var gem = _gemMapper.Get(grid.Gems[index].EntityId);
          if (gem != null && gem.UpdateRegistered) Wake(gem);
        }
    }

    private bool GravityOverlaps(int index, Vector2 position, float radius)
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      var gem = _gemMapper.Get(grid.Gems[index].EntityId);
      return gem != null && gem.IsLive && !gem.PickedUp && !gem.WasClicked && gem.OverlapsClick(position, radius);
    }

    private void MoveManualGravity(int index, Vector2 position)
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      var gem = _gemMapper.Get(grid.Gems[index].EntityId);
      if (gem != null && gem.IsLive && !gem.PickedUp && !gem.WasClicked)
        gem.MoveByManualGravity(position);
    }

    private bool CollectManualGem(int index, double multiplier)
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      ref var data = ref grid.Gems[index];
      if (!data.IsActive || data.ClaimState != 0) return false;
      var gem = _gemMapper.Get(data.EntityId);
      if (gem == null || !gem.UpdateRegistered || gem.PickedUp || gem.WasClicked || gem.ShouldDestroy) return false;
      gem.ManualClickBonus = UntitledGemGameGameScreen.Instance.ClickUtility.BonusValue(gem.BaseValue, multiplier);
      gem.OnClicked(false);
      UntitledGemGameGameScreen.Instance.OnGemHandCollected();
      return true;
    }

    public Entity GetEntityP(int entityId) => GetEntity(entityId);

    public ulong GetUncollectedGemValue()
    {
      ulong value = 0;
      foreach (var gem in _live)
        if (!gem.PickedUp && !gem.ShouldDestroy)
          value = PrestigeProgression.AddSaturating(value,
            PrestigeProgression.AddSaturating(gem.BaseValue, gem.ManualClickBonus));
      return value;
    }

    public void FinishPrestigeCollection()
    {
      foreach (var gem in _live)
        gem.ShouldDestroy = true;
    }

    public override void Initialize(IComponentMapperService mapperService)
      => _gemMapper = mapperService.GetMapper<Gem>();

    public override void Update(GameTime gameTime)
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      var mouse = GameInput.Mouse;
      var mousePosition = m_camera.ScreenToWorld(mouse.Position.ToVector2());
      var screen = UntitledGemGameGameScreen.Instance;
      screen.CaptureGemPointer(mouse.Position.ToVector2(), JapeFramework.BaseGame.BoxingViewportAdapter.Viewport.Bounds,
        mouse.IsButtonDown(MouseButton.Right));
      bool hovering = screen.GemClickInputEnabled;
      float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
      bool clicked = screen.WorldClickTriggered;
      SpawnerEffects.Update(dt);
      var bounds = PlayAreaBounds.ForCamera(m_camera);
      bool boundsChanged = bounds.Minimum != _previousBounds.Minimum || bounds.Maximum != _previousBounds.Maximum;
      _previousBounds = bounds;
      var commands = UntitledGemGameGameScreen.Instance.ManualAbilities;
      bool magnetsActive = !commands.IsActive(ManualFleetAbilities.MagnetizerSlot) && MagnetizerCache.ActiveMagnets.Count > 0;
      _manualGravity ??= new ManualGravityField(grid.MaxCapacity);
      _manualGravity.Update(grid, commands, UntitledGemGameGameScreen.HomeBasePos,
        BaseStats.GetHarvesterCollectionRange(HomeBase.Instance.Entity.Get<Harvester>()), _moveManualGravity);
      screen.CursorGravity.FollowPointer(mousePosition, hovering && mouse.IsButtonDown(MouseButton.Right));
      screen.CursorGravity.Update(dt, grid, _moveManualGravity, _gravityOverlaps, _collectManualGem);
      if (screen.CursorGravity.TakeCollapseNotification())
        AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect);
      if (screen.CursorGravity.TakeReadyNotification() && UpgradeManager.Instance.UG.CursorGravityEnabled)
        AudioManager.Instance.PlaySound(AudioManager.Instance.BlipSoundEffect, pitch: 0.25f);
      screen.CursorGravity.HandleInput(mouse.IsButtonDown(MouseButton.Right),
        mouse.WasButtonPressed(MouseButton.Left), hovering,
        mousePosition, screen.GemClickRadius, UpgradeManager.Instance.UG, UpgradeManager.Instance.Signals, UpgradeManager.Instance.UGM);
      bool prestiging = UntitledGemGameGameScreen.Instance.m_prestiging;

      // Idle gems sleep indefinitely. Only camera changes and prestige require a
      // population-wide update; magnets wake the gems in their reach each frame.
      if (boundsChanged || prestiging)
        foreach (var gem in _live) Wake(gem);
      else if (magnetsActive)
        WakeNearMagnets(grid);

      // Hover and clicks use the same persistent index instead of touching every gem.
      ++_hoverFrame;
      _nextHovered.Clear();
      _directClicks.Clear();
      float clickRadius = screen.GemClickRadius;
      if (hovering && !mouse.IsButtonDown(MouseButton.Right))
      {
        foreach (int index in grid.QueryClickCandidates(mousePosition.X, mousePosition.Y, clickRadius))
        {
          var gem = _gemMapper.Get(grid.Gems[index].EntityId);
          // Factory spawns are indexed before ECS registers their components.
          if (gem == null || !gem.UpdateRegistered || gem.ShouldDestroy
            || !gem.OverlapsClick(mousePosition, clickRadius)) continue;
          gem.SetHovered(true);
          gem.HoverFrame = _hoverFrame;
          _nextHovered.Add(gem);
          if (clicked) _directClicks.Add(index);
        }
      }
      if (clicked && UntitledGemGameGameScreen.Instance.ClickUtility.Activate(
        grid, _directClicks, mousePosition, UpgradeManager.Instance.UG, _collectManualGem, System.Random.Shared.NextDouble(),
        UpgradeManager.Instance.Signals, UpgradeManager.Instance.UGM,
        UntitledGemGameGameScreen.HomeBasePos, clickRadius, _gravityOverlaps))
        AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect,
          pitch: JapeFramework.Helpers.RandomHelper.Float(-0.15f, 0.15f));
      foreach (var gem in _hovered)
        if (gem.UpdateRegistered && gem.HoverFrame != _hoverFrame) gem.SetHovered(false);
      (_hovered, _nextHovered) = (_nextHovered, _hovered);

      for (int i = 0; i < _awake.Count;)
      {
        var gem = _awake[i];
        if (!gem.ShouldDestroy)
        {
          var previousPosition = gem.VisualPosition;
          var previousScale = gem.VisualScale;
          gem.Update(gameTime, dt);
          gem.ConstrainToPlayArea(bounds);
          if (previousPosition != gem.VisualPosition || previousScale != gem.VisualScale)
            RenderGemSystem.Instance?.UpdateGem(gem.Id);
          if (!gem.PickedUp && !gem.WasClicked)
            gem.SynchronizeSpatialIndex(grid);

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
          Sleep(gem);
          RemoveLive(gem);
          gem.UpdateRegistered = false;
          grid.RecycleIndex(gem.GridIndex);
          RenderGemSystem.Instance?.RemoveGem(gem.Id);
          EntityFactory.Instance.ParkGem(entity, gem);
        }
        else if (!gem.NeedsUpdate)
          Sleep(gem);
        else
          ++i;
      }
    }
  }
}
