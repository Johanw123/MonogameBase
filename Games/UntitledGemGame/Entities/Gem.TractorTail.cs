using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

public partial class Gem
{
  // A tail draws its own snapshot until delivery; the loose entity can be reused.
  internal void PickUpForTractorTail()
  {
    TakeOverFlight();
    PickedUp = true;
    HarvesterCollectionSystem.Instance.flatSpatialHash.RemoveFromQueries(GridIndex);
    ShouldDestroy = true;
  }
}
