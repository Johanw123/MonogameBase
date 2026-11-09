using Microsoft.Xna.Framework;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

public partial class Gem
{
  // Carried by the Tractor Belt: claimed like a clicked gem, but positioned by the belt
  // until it is delivered at the home base (TractorBeltCargo).
  internal bool RidingTractorBelt;

  internal bool CanBoardTractorBelt => IsLive && !PickedUp && !WasClicked && !CollectingOnGpu && !swallowing;

  internal bool BoardTractorBelt()
  {
    if (!CanBoardTractorBelt) return false;
    TakeOverFlight();
    // The belt owns position and size from here; any grow-in finishes at once.
    m_animating = false;
    m_targetHarvester = null;
    LaunchVelocity = Vector2.Zero;
    m_transform.Scale = OrigScale;
    m_targetScale = OrigScale;
    WasClicked = true;
    RidingTractorBelt = true;
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    grid.Gems[GridIndex].ClaimState = 2;
    grid.RemoveFromQueries(GridIndex);
    RenderGemSystem.Instance?.UpdateGem(Id);
    return true;
  }

  internal bool StillRidingTractorBelt(int entityId, ulong version)
    => RidingTractorBelt && MatchesLifetime(entityId, version) && !PickedUp && !CollectingOnGpu && !swallowing;

  internal void RideTractorBelt(Vector2 position, float scale)
  {
    m_transform.Position = position;
    m_targetPosition = position;
    m_transform.Scale = OrigScale * scale;
    m_targetScale = m_transform.Scale;
    SetCollisionPosition(position);
    PositionMoved = true;
    RenderGemSystem.Instance?.UpdateGem(Id);
  }

  // Ready for the home base's ordinary hand-collection delivery.
  internal void LeaveTractorBelt() => RidingTractorBelt = false;
}
