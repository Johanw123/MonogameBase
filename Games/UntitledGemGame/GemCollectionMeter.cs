using System;
using System.Collections.Generic;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// What collected the player's gems, for the HUD's Collection panel (GameScreen.DamagePanel.cs).
// Ship modules and instant-delivery effects count toward the ship class that triggered them.
public enum GemCollectionSource
{
  ManualClicks,
  HomeBase,
  Drifter,
  Seeker,
  Prospector,
  TroveHunter,
  Rimrunner,
  CollectorDrones,
  PassiveIncome,
}

// Each source's gem collection over the last minute (RollingMinute) and over the run.
// Run totals are saved with the run and reset at extraction.
public sealed class GemCollectionMeter
{
  public static readonly int SourceCount = Enum.GetValues<GemCollectionSource>().Length;

  private readonly RollingMinute[] minute = new RollingMinute[SourceCount];
  private readonly double[] run = new double[SourceCount];

  public GemCollectionMeter()
  {
    for (int i = 0; i < SourceCount; i++) minute[i] = new RollingMinute();
  }

  public static GemCollectionSource SourceFor(Harvester.HarvesterType type) => type switch
  {
    Harvester.HarvesterType.HomeBase => GemCollectionSource.HomeBase,
    Harvester.HarvesterType.Harvester => GemCollectionSource.Drifter,
    Harvester.HarvesterType.AdvancedHarvester => GemCollectionSource.Seeker,
    Harvester.HarvesterType.ExpertHarvester => GemCollectionSource.Prospector,
    Harvester.HarvesterType.UltimateHarvester => GemCollectionSource.TroveHunter,
    Harvester.HarvesterType.PerimeterHarvester => GemCollectionSource.Rimrunner,
    Harvester.HarvesterType.Drone => GemCollectionSource.CollectorDrones,
    _ => GemCollectionSource.Drifter,
  };

  // English display name, marked for the string table; translate it where shown.
  public static string Name(GemCollectionSource source) => source switch
  {
    GemCollectionSource.ManualClicks => Loc.N("Manual Clicks"),
    GemCollectionSource.HomeBase => Loc.N("Home Base"),
    GemCollectionSource.Drifter => Loc.N("Drifter"),
    GemCollectionSource.Seeker => Loc.N("Seeker"),
    GemCollectionSource.Prospector => Loc.N("Prospector"),
    GemCollectionSource.TroveHunter => Loc.N("Trove Hunter"),
    GemCollectionSource.Rimrunner => Loc.N("Rimrunner"),
    GemCollectionSource.CollectorDrones => Loc.N("Collector Drones"),
    GemCollectionSource.PassiveIncome => Loc.N("Passive Income"),
    _ => source.ToString(),
  };

  public double PerMinute(GemCollectionSource source) => minute[(int)source].PerMinute;
  public double ThisRun(GemCollectionSource source) => run[(int)source];

  public double TotalPerMinute
  {
    get
    {
      double total = 0;
      foreach (var tracker in minute) total += tracker.PerMinute;
      return total;
    }
  }

  public double TotalThisRun
  {
    get
    {
      double total = 0;
      foreach (double collected in run) total += collected;
      return total;
    }
  }

  public void Record(GemCollectionSource source, double amount)
  {
    if (!(amount > 0) || double.IsInfinity(amount) || (uint)source >= (uint)SourceCount) return;
    minute[(int)source].Record(amount);
    run[(int)source] += amount;
  }

  public void Update(float dt)
  {
    foreach (var tracker in minute) tracker.Update(dt);
  }

  public void Reset()
  {
    foreach (var tracker in minute) tracker.Reset();
    Array.Clear(run);
  }

  // Keyed by source name, so reordering sources never mixes up a save.
  public Dictionary<string, double> RunTotals()
  {
    var totals = new Dictionary<string, double>();
    for (int i = 0; i < SourceCount; i++)
      if (run[i] > 0) totals[((GemCollectionSource)i).ToString()] = run[i];
    return totals;
  }

  public void RestoreRunTotals(Dictionary<string, double> totals)
  {
    Reset();
    if (totals == null) return;
    // Exact names only: Enum.TryParse would also take numbers.
    foreach (var (name, amount) in totals)
      if (Enum.TryParse(name, out GemCollectionSource source) && source.ToString() == name
        && double.IsFinite(amount) && amount > 0)
        run[(int)source] = amount;
  }
}
