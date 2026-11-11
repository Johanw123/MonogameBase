using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems
{
  // The Tractor Belt's hold on real gems: boarding claims them like a click, the belt moves
  // them, and arrival hands them to the home base as a hand delivery, so the belt's value
  // bonus rides on the gem's ManualClickBonus.
  public partial class UpdateSystem2 : ITractorBeltCargo
  {
    private void UpdateTractorBelt(UntitledGemGameGameScreen screen, GemSpatialIndex grid,
      bool rightPressed, bool rightHeld, bool inputEnabled, Vector2 pointer, float dt)
    {
      var belt = screen.TractorBelt;
      var upgrades = UpgradeManager.Instance.UG;
      if (!upgrades.TractorBeltEnabled && belt.Lines.Count == 0) return;
      var stats = TractorBeltStats.From(upgrades, screen.GemClickRadius, UpgradeManager.Instance.Signals,
        UpgradeManager.Instance.UGM);
      if (belt.HandleInput(rightPressed, rightHeld, inputEnabled, pointer, stats))
        AudioManager.Instance.PlaySound(SoundEvent.TractorBeltPlace);
      var home = HomeBase.Instance?.Entity?.Get<Transform2>()?.Position ?? UntitledGemGameGameScreen.HomeBasePos;
      belt.Update(dt, grid, this, stats, home,
        PlanetObstacle.Active ? (Vector2?)PlanetObstacle.Center : null, UntitledGemGameGameScreen.PlanetRadius);
      if (belt.TakeExpiredNotification())
        AudioManager.Instance.PlaySound(SoundEvent.TractorBeltExpire);
      // Pointing within a few pixels of the belt reveals the intake's catch ring.
      belt.UpdateHighlights(dt, inputEnabled ? pointer : null, stats.Radius,
        BeltHoverPixels / System.Math.Max(0.1f, m_camera.Zoom));
    }

    private const float BeltHoverPixels = 14f;

    private Gem BeltGem(in BeltRider rider)
    {
      var gem = _gemMapper.Get(rider.EntityId);
      return gem != null && gem.StillRidingTractorBelt(rider.EntityId, rider.Lifetime) ? gem : null;
    }

    bool ITractorBeltCargo.TryBoard(int gridIndex, out BeltRider rider, out Vector2 position)
    {
      rider = default;
      position = default;
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      if (!grid.IsAvailable(gridIndex)) return false;
      var gem = _gemMapper.Get(grid.Gems[gridIndex].EntityId);
      // Factory spawns are indexed before ECS registers their components.
      if (gem == null || !gem.UpdateRegistered || gem.GridIndex != gridIndex || !gem.BoardTractorBelt()) return false;
      rider = new BeltRider(gridIndex, gem.Id, gem.LifetimeVersion);
      position = gem.VisualPosition;
      return true;
    }

    bool ITractorBeltCargo.Ride(in BeltRider rider, Vector2 position, float scale)
    {
      var gem = BeltGem(rider);
      gem?.RideTractorBelt(position, scale);
      return gem != null;
    }

    void ITractorBeltCargo.Deliver(in BeltRider rider, double valueMultiplier)
    {
      var gem = BeltGem(rider);
      if (gem == null) return;
      gem.LeaveTractorBelt();
      valueMultiplier *= 1 + BuriedGems.Dig(UpgradeManager.Instance.UG.BeltDig);
      gem.ManualClickBonus = UntitledGemGameGameScreen.Instance.ClickUtility.BonusValue(gem.BaseValue, valueMultiplier);
      HarvesterCollectionSystem.Instance.CollectGem(gem, HomeBase.Instance.Entity.Get<Harvester>(),
        GemCollectionMethod.TractorBelt);
      // A gem the home base could not take must not linger, claimed and invisible to everything.
      if (!gem.PickedUp && !gem.ShouldDestroy) gem.ShouldDestroy = true;
    }

    void ITractorBeltCargo.Drop(in BeltRider rider)
    {
      var gem = BeltGem(rider);
      if (gem == null) return;
      gem.LeaveTractorBelt();
      gem.ShouldDestroy = true;
    }
  }
}
