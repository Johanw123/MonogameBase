using System;

namespace UntitledGemGame;

// Effective values keep tree purchases intact and apply permanent signals at use time.
public static class SignalStats
{
  private static SignalProgression Signals => UpgradeManager.Instance.Signals;
  private static float Scale(SignalKind kind, float value) => value * Signals.Multiplier(kind);
  private static int Count(SignalKind kind, int value) => Signals.ScaleCount(kind, value);
  public static float SpawnFrequency => Scale(SignalKind.SpawnFrequency, UpgradeManager.Instance.UG.GemSpawnCooldown);
  public static int SpawnCount => Count(SignalKind.SpawnCount, UpgradeManager.Instance.UG.GemSpawnRate);
  public static int GemLimit => Count(SignalKind.GemLimit, UpgradeManager.Instance.UG.MaxGemCount);
  public static double PassiveIncome => UpgradeManager.Instance.UG.PassiveIncome * (1 + Signals.BonusPercent((int)SignalKind.PassiveIncome) / 100);
  public static int ClusterSize => Count(SignalKind.ClusterSize, UpgradeManager.Instance.UG.ClusterSize);
  public static float LuckyValue => Scale(SignalKind.LuckyValue, UpgradeManager.Instance.UG.LuckyGemValue);
  public static int ShowerCount => Count(SignalKind.ShowerCount, UpgradeManager.Instance.UG.GemShowerGemCount);
  public static float ShowerFrequency => Scale(SignalKind.ShowerFrequency, UpgradeManager.Instance.UG.GemShowerCooldown);
  public static int CometCount => Count(SignalKind.CometCount, UpgradeManager.Instance.UG.GemCometGemCount);
  public static float CometFrequency => Scale(SignalKind.CometFrequency, UpgradeManager.Instance.UG.GemCometCooldown);
  public static float ClickRadius => Scale(SignalKind.ClickRadius, UpgradeManager.Instance.UG.ClickRadius);
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
