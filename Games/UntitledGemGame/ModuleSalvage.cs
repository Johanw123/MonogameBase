using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace UntitledGemGame;

public sealed partial class ShipyardModules
{
  public const double FirstFindMinimumSeconds = 60;
  public const double FirstFindMaximumSeconds = 120;
  public const double FindMinimumSeconds = 300;
  public const double FindMaximumSeconds = 600;
  public const double HarvestActivitySeconds = 15;
  public const double SignalScanAdvanceChance = 0.25;
  public const double SignalScanProgressSeconds = 30;
  private static readonly int[] RarityWeights = [50, 28, 15, 6, 1];

  [JsonRequired] public HashSet<ShipModule> Owned { get; set; } = new();
  [JsonRequired] public List<ShipModule> PendingReveals { get; set; } = new();
  [JsonRequired] public bool SalvageStarted { get; set; }
  [JsonRequired] public double DiscoveryProgressSeconds { get; set; }
  [JsonRequired] public double DiscoveryThresholdSeconds { get; set; }
  // Recent activity is deliberately not restored: no offline harvesting credit.
  private double harvestActivityRemaining;

  [JsonIgnore] public int RevealedCount => Owned.Count - PendingReveals.Count;
  [JsonIgnore] public bool CollectionComplete => Owned.Count == ModuleCatalog.Names.Length - 1;

  public bool StartSalvage(Random random)
  {
    if (SalvageStarted) return false;
    SalvageStarted = true;
    Owned.Add(ShipModule.CargoPod);
    Owned.Add(ShipModule.IonBooster);
    DiscoveryThresholdSeconds = RollThreshold(random, true);
    return true;
  }

  public void RecordHarvest()
  {
    if (SalvageStarted && !CollectionComplete) harvestActivityRemaining = HarvestActivitySeconds;
  }

  public void EndHarvesting() => harvestActivityRemaining = 0;

  public bool AdvanceSalvage(double elapsedSeconds, Random random)
  {
    if (!SalvageStarted || CollectionComplete || !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return false;
    // At most one second of credit per simulated second, regardless of gem value,
    // fleet size, bonus pulls or collection count. Long stalls do not grant catch-up loot.
    double credit = Math.Min(Math.Min(elapsedSeconds, 1), harvestActivityRemaining);
    harvestActivityRemaining = Math.Max(0, harvestActivityRemaining - elapsedSeconds);
    return AddDiscoveryProgress(credit, random);
  }

  // Paid scans can help even when the fleet is idle; they do not start harvesting activity.
  public bool AdvanceDiscoveryFromSignalScan(Random random)
  {
    if (!SalvageStarted || CollectionComplete || random.NextDouble() >= SignalScanAdvanceChance) return false;
    AddDiscoveryProgress(SignalScanProgressSeconds, random);
    return true;
  }

  private bool AddDiscoveryProgress(double credit, Random random)
  {
    DiscoveryProgressSeconds += credit;
    if (DiscoveryProgressSeconds < DiscoveryThresholdSeconds) return false;
    var module = RollUnownedModule(random);
    if (module == ShipModule.None) return false;
    Owned.Add(module);
    PendingReveals.Add(module);
    DiscoveryProgressSeconds -= DiscoveryThresholdSeconds;
    DiscoveryThresholdSeconds = RollThreshold(random, false);
    return true;
  }

  private static double RollThreshold(Random random, bool first)
    => first ? FirstFindMinimumSeconds + random.NextDouble() * (FirstFindMaximumSeconds - FirstFindMinimumSeconds)
      : FindMinimumSeconds + random.NextDouble() * (FindMaximumSeconds - FindMinimumSeconds);

  private ShipModule RollUnownedModule(Random random)
  {
    Span<int> counts = stackalloc int[RarityWeights.Length];
    counts.Clear();
    foreach (var module in ModuleCatalog.InventoryOrder)
      if (!Owned.Contains(module)) ++counts[(int)ModuleCatalog.Rarities[(int)module]];
    int totalWeight = 0;
    for (int rarity = 0; rarity < counts.Length; rarity++)
      if (counts[rarity] > 0) totalWeight += RarityWeights[rarity];
    if (totalWeight == 0) return ShipModule.None;
    int roll = random.Next(totalWeight);
    int selectedRarity = 0;
    for (; selectedRarity < counts.Length; selectedRarity++)
    {
      if (counts[selectedRarity] == 0) continue;
      if (roll < RarityWeights[selectedRarity]) break;
      roll -= RarityWeights[selectedRarity];
    }
    int choice = random.Next(counts[selectedRarity]);
    foreach (var module in ModuleCatalog.InventoryOrder)
      if (!Owned.Contains(module) && (int)ModuleCatalog.Rarities[(int)module] == selectedRarity && choice-- == 0)
        return module;
    throw new InvalidOperationException("Module salvage pool was inconsistent.");
  }

  public bool TryAcknowledgeReveal(ShipModule module)
  {
    if (PendingReveals.Count == 0 || PendingReveals[0] != module) return false;
    PendingReveals.RemoveAt(0);
    return true;
  }

  private void ValidateSalvage()
  {
    if (Owned == null || PendingReveals == null
      || Owned.Any(module => module == ShipModule.None || !Enum.IsDefined(module))
      || PendingReveals.Any(module => !Owned.Contains(module))
      || PendingReveals.Distinct().Count() != PendingReveals.Count
      || !double.IsFinite(DiscoveryProgressSeconds) || DiscoveryProgressSeconds < 0
      || !double.IsFinite(DiscoveryThresholdSeconds)
      || (SalvageStarted && (DiscoveryThresholdSeconds < FirstFindMinimumSeconds || DiscoveryThresholdSeconds > FindMaximumSeconds
        || DiscoveryProgressSeconds >= DiscoveryThresholdSeconds
        || !Owned.Contains(ShipModule.CargoPod) || !Owned.Contains(ShipModule.IonBooster)))
      || (!SalvageStarted && (Owned.Count != 0 || PendingReveals.Count != 0
        || DiscoveryProgressSeconds != 0 || DiscoveryThresholdSeconds != 0)))
      throw new InvalidDataException("Invalid module salvage progress.");
  }
}
