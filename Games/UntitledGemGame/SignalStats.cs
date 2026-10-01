using System;

namespace UntitledGemGame;

// Effective values keep tree purchases intact and apply permanent signals at use time.
public static class SignalStats
{
  private static SignalProgression Signals => UpgradeManager.Instance.Signals;
  private static float Scale(SignalKind kind, float value) => value * Signals.Multiplier(kind);
  // Controllers accept explicit tree stats and optional signals so simulations
  // and checks can use an isolated loadout without replacing the live manager.
  public static float Scale(SignalKind kind, float value, SignalProgression signals)
    => value * (signals?.Multiplier(kind) ?? 1);
  private static int Count(SignalKind kind, int value) => Signals.ScaleCount(kind, value);
  public static float SpawnFrequency => Scale(SignalKind.SpawnFrequency, UpgradeManager.Instance.UG.GemSpawnCooldown);
  public static int SpawnCount => Count(SignalKind.SpawnCount, UpgradeManager.Instance.UG.GemSpawnRate);
  public static int GemLimit => Count(SignalKind.GemLimit, UpgradeManager.Instance.UG.MaxGemCount);
  public static int ReserveCapacity => (int)Math.Clamp(Math.Ceiling((double)UpgradeManager.Instance.UG.GemReserveCapacity
    * UpgradeManager.Instance.UGM.GemReserveCapacityMultiplier), 0, int.MaxValue);
  public static double PassiveIncome => UpgradeManager.Instance.UG.PassiveIncome * (1 + Signals.BonusPercent((int)SignalKind.PassiveIncome) / 100);
  public static int ClusterSize => Count(SignalKind.ClusterSize, UpgradeManager.Instance.UG.ClusterSize);
  public static float LuckyValue => Scale(SignalKind.LuckyValue, UpgradeManager.Instance.UG.LuckyGemValue);
  public static int ShowerCount => Count(SignalKind.ShowerCount, UpgradeManager.Instance.UG.GemShowerGemCount);
  public static float ShowerFrequency => Scale(SignalKind.ShowerFrequency, UpgradeManager.Instance.UG.GemShowerCooldown);
  public static int CometCount => Count(SignalKind.CometCount, UpgradeManager.Instance.UG.GemCometGemCount);
  public static float CometFrequency => Scale(SignalKind.CometFrequency, UpgradeManager.Instance.UG.GemCometCooldown);
  public static float ClickRadius => Scale(SignalKind.ClickRadius,
    UpgradeManager.Instance.UG.ClickRadius * UpgradeManager.Instance.UGM.ClickRadiusMultiplier);
  public static float ClickValue => Scale(SignalKind.ClickValue,
    UpgradeManager.Instance.UG.ClickValueMultiplier * UpgradeManager.Instance.UGM.ClickValueMultiplier);
  public static float ClickChainRange => Scale(SignalKind.ClickChainRange, UpgradeManager.Instance.UG.ClickChainRange);
  public static float HoldClickFrequency => Scale(SignalKind.HoldClickFrequency,
    UpgradeManager.Instance.UG.HoldClickFrequencyMultiplier);
  public static float ClickComboWindow => Scale(SignalKind.ClickComboWindow, UpgradeManager.Instance.UG.ClickComboWindow);
  public static float CursorGravityRadius => Scale(SignalKind.CursorGravityRadius, UpgradeManager.Instance.UG.CursorGravityRadiusMultiplier);
  public static float CursorGravityStrength => Scale(SignalKind.CursorGravityStrength,
    UpgradeManager.Instance.UG.CursorGravityStrengthMultiplier);
  public static float CursorGravityDuration => Scale(SignalKind.CursorGravityDuration,
    UpgradeManager.Instance.UG.CursorGravityDuration);
  public static float CursorGravityCooldown => CursorGravityWell.Cooldown(UpgradeManager.Instance.UG, Signals, UpgradeManager.Instance.UGM);
  public static int DroneCount => Count(SignalKind.DroneCount, UpgradeManager.Instance.UGA.IncreaseDroneCount);
  public static float DroneLifetime => Scale(SignalKind.DroneLifetime, UpgradeManager.Instance.UGA.IncreaseDroneFuel);
  public static float DroneSpeed => Scale(SignalKind.DroneSpeed, UpgradeManager.Instance.UGA.DroneSpeed);
  public static float DroneRange => Scale(SignalKind.DroneRange, UpgradeManager.Instance.UGA.DroneCollectionRange);
  public static int MagnetDuration => Count(SignalKind.MagnetDuration, UpgradeManager.Instance.UGA.HomebaseMagnetizerDuration);
  public static int SpawnerCount => Count(SignalKind.SpawnerCount, UpgradeManager.Instance.UGA.GemSpawnerNrGems);
  public static int ChainValue => Count(SignalKind.ChainValue, UpgradeManager.Instance.UGA.ChainResidualCharge);
  public static int SweepValue => Count(SignalKind.SweepValue, UpgradeManager.Instance.UGA.DroneSweepEfficiency);
  public static int ConstellationCapacity => Count(SignalKind.ConstellationCapacity, UpgradeManager.Instance.UGA.ConstellationCapacity);
}
