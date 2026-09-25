using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

public partial class HarvesterCollectionSystem
{
  private void ApplyAdditionalPickupModules(Harvester harvester, Vector2 origin, bool direct)
  {
    harvester.RegisterModulePickup(direct);
    if (!direct) return;
    // Consume charge from earlier pulls before this pickup creates any more.
    // Cascade pulls can recharge the capacitor, but never trigger another cascade here.
    if (harvester.TryConsumeCascade())
      CollectModuleArea(harvester, origin, 180f, 6, Color.Violet);
    ulong count = harvester.DirectModulePickups;
    if (harvester.HasModule(ShipModule.PulseHarvester) && count % 6 == 0)
      CollectModuleArea(harvester, origin, 120f, 3, Color.DodgerBlue);
    if (harvester.HasModule(ShipModule.TwinTractor))
      CollectModuleArea(harvester, origin, 60f, 1, Color.Cyan);
    if (harvester.HasModule(ShipModule.StormCoil) && count % 8 == 0)
      CollectStormChain(harvester, origin);
    if (harvester.HasModule(ShipModule.RiftSiphon) && count == 1)
      CollectModuleArea(harvester, origin, 220f, 10, Color.MediumPurple);
    if (harvester.HasModule(ShipModule.AstralRelay) && count % 5 == 0)
    {
      ulong value = BaseStats.GetHarvesterDeliveryValue(harvester, harvester.CarryingGemBaseValue);
      UntitledGemGameGameScreen.DeliveredUncounted = PrestigeProgression.AddSaturating(
        UntitledGemGameGameScreen.DeliveredUncounted, value);
      harvester.RelayOrigin = origin;
      harvester.RelayFlashRemaining = ModuleCatalog.PulseDuration;
      harvester.ShowModulePulse(origin, 80f, Color.Gold);
    }
  }

  private bool TryCollectModuleGem(Harvester harvester, int index, out Vector2 position)
  {
    position = default;
    ref var candidate = ref flatSpatialHash.Gems[index];
    if (!candidate.IsActive || candidate.ClaimState != 0) return false;
    var gem = GetEntity(candidate.EntityId)?.Get<Gem>();
    if (gem == null || !gem.IsLive || !flatSpatialHash.TryClaim(index)) return false;
    position = gem.BoundingCircle.Center;
    // Bonus pulls cannot recursively trigger more pulls or charge direct-pickup modules.
    CollectGem(gem, harvester, allowChainCollection: false);
    if (!gem.PickedUp && !gem.WasClicked && !gem.ShouldDestroy)
    {
      flatSpatialHash.ReleaseClaim(index);
      return false;
    }
    return true;
  }

  private void CollectModuleArea(Harvester harvester, Vector2 origin, float radius, int limit, Color color)
  {
    harvester.ShowModulePulse(origin, radius, color);
    int collected = 0;
    foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, radius))
    {
      if (collected >= limit) break;
      if (TryCollectModuleGem(harvester, index, out _)) ++collected;
    }
  }

  private void CollectStormChain(Harvester harvester, Vector2 origin)
  {
    harvester.StormArcPoints ??= new Vector2[7];
    harvester.StormArcPoints[0] = origin;
    harvester.StormArcCount = 1;
    for (int hop = 0; hop < 6; hop++)
    {
      bool found = false;
      foreach (int index in flatSpatialHash.QueryCollection(origin.X, origin.Y, 100f))
      {
        if (!TryCollectModuleGem(harvester, index, out var target)) continue;
        harvester.StormArcPoints[harvester.StormArcCount++] = target;
        origin = target;
        found = true;
        break;
      }
      if (!found) break;
    }
    harvester.StormArcRemaining = ModuleCatalog.PulseDuration;
  }

  private void ApplyFullCargoModules(Harvester harvester, Vector2 origin)
  {
    if (harvester.TryBeginReactorBloom())
      CollectModuleArea(harvester, origin, 100f, 6, Color.LimeGreen);
    if (harvester.TryBeginEventHorizon())
      CollectModuleArea(harvester, origin, 400f, 48, Color.MediumPurple);
  }
}
