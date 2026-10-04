using System;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// Shared tuning and calculations for run-changing prestige talents. Keeping the
// rules here makes their tradeoffs testable without constructing a game screen.
public static class PrestigeTalentEffects
{
  public const int RequisitionInterval = 5;
  public const float OverloadedCapacityMultiplier = 2f;
  public const float LoadedFuelCostMultiplier = 2f;
  public const float DeepCoreReachMultiplier = 1.4f;
  public const float TargetPainterDuration = 8f;
  public const float TargetPainterFireRateMultiplier = 0.65f;
  public const int TargetPainterYieldMultiplier = 2;

  private static UpgradesGeneratorUpgrades_meta Meta => UpgradeManager.Instance?.UGM;

  public static float CargoCapacityMultiplier(Harvester harvester)
    => BaseStats.IsFleetHarvester(harvester) && Meta?.OverloadedHolds == true
      ? OverloadedCapacityMultiplier : 1f;

  public static float FuelCostMultiplier(Harvester harvester)
    => BaseStats.IsFleetHarvester(harvester) && harvester.CarryingGemCount > 0
      && Meta?.OverloadedHolds == true ? LoadedFuelCostMultiplier : 1f;

  public static int FleetCount(int purchased)
  {
    purchased = Math.Max(0, purchased);
    if (Meta?.FleetRequisition != true) return purchased;
    return (int)Math.Min(int.MaxValue, (long)purchased + purchased / RequisitionInterval);
  }

  // Advance to the next gem-quality row rather than adding an arbitrary amount
  // of fire power. Above the final color, extra rows continue every eight power.
  public static int DeepCoreQualityPower(int firePower)
  {
    firePower = Math.Max(1, firePower);
    if (Meta?.DeepCoreMunitions != true) return firePower;
    foreach (var (_, required) in GemQualityTable.ColorFirePower)
      if (required > firePower) return required;
    return firePower + GemQualityTable.FirePowerPerExtraRow;
  }

  public static float PlanetDebrisReachScale(float reachScale)
    => Meta?.DeepCoreMunitions == true ? reachScale * DeepCoreReachMultiplier : reachScale;

  public static float AutomaticWeaponFireRate(float fireRate)
    => Meta?.TargetPainter == true ? fireRate * TargetPainterFireRateMultiplier : fireRate;

  public static int PaintedYield(int gems, bool painted)
    => painted && Meta?.TargetPainter == true
      ? (int)Math.Min(int.MaxValue, (long)Math.Max(0, gems) * TargetPainterYieldMultiplier)
      : Math.Max(0, gems);
}
