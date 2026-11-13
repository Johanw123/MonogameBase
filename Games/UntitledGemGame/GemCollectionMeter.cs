using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// What collected the player's gems, for the HUD's Collection panel (GameScreen.DamagePanel.cs).
// Talents, upgrades and modules that pull gems in have rows of their own (GemCollectionMethod);
// everything else a ship brings home, value bonuses included, counts toward its class.
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
  TractorBelt,
}

// How gems came in beyond a collector's own pickups. Clicks and the Tractor Belt hand their
// gems to the home base but count as sources of their own; every later method is a talent,
// upgrade or module whose take is its own row, apart from the ship that collected it.
public enum GemCollectionMethod
{
  None,
  ManualClick,
  TractorBelt,
  TractorTails,
  ChainCollection,
  StormDrones,
  FinalSweep,
  SupernovaCore,
  WakeCollector,
  TractorLink,
  SingularityEngine,
  CascadeCapacitor,
  PulseHarvester,
  TwinTractor,
  StormCoil,
  RiftSiphon,
  AstralRelay,
  ArcEmitter,
  GravityBuoy,
  EchoChamber,
  EventHorizon,
  ThunderGod,
  WorldEater,
  TimeHeist,
}

// Gem collection over the last minute (RollingMinute) and over the run, in rows: every
// source, then every method. Methods with rows of their own (IsBreakdown) take their gems
// out of the collector's row, so the rows add up to the totals. Run totals are saved with
// the run and reset at extraction.
public sealed class GemCollectionMeter
{
  public static readonly int SourceCount = Enum.GetValues<GemCollectionSource>().Length;
  public static readonly int MethodCount = Enum.GetValues<GemCollectionMethod>().Length;
  public static readonly int RowCount = SourceCount + MethodCount;

  private readonly RollingMinute[] minute = new RollingMinute[RowCount];
  private readonly double[] run = new double[RowCount];

  public GemCollectionMeter()
  {
    for (int i = 0; i < RowCount; i++) minute[i] = new RollingMinute();
  }

  public static int Row(GemCollectionSource source) => (int)source;
  public static int Row(GemCollectionMethod method) => SourceCount + (int)method;

  // Rows that can hold gems: every source, and the methods with rows of their own.
  public static bool IsRow(int row)
    => (uint)row < (uint)SourceCount || IsBreakdown((GemCollectionMethod)(row - SourceCount));

  public static string RowName(int row)
    => row < SourceCount ? Name((GemCollectionSource)row) : Name((GemCollectionMethod)(row - SourceCount));

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

  // The panel row a collector's take lands in: clicks and the belt over the home base.
  public static GemCollectionSource SourceFor(GemCollectionSource collector, GemCollectionMethod method) => method switch
  {
    GemCollectionMethod.ManualClick => GemCollectionSource.ManualClicks,
    GemCollectionMethod.TractorBelt => GemCollectionSource.TractorBelt,
    _ => collector,
  };

  // Talents, upgrades and modules: rows of their own, apart from the collector's.
  public static bool IsBreakdown(GemCollectionMethod method)
    => method > GemCollectionMethod.TractorBelt && (uint)method < (uint)MethodCount;

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
    GemCollectionSource.TractorBelt => Loc.N("Tractor Belt"),
    _ => source.ToString(),
  };

  // The talent, upgrade or module's own name, as the player knows it from its tree or bay.
  public static string Name(GemCollectionMethod method)
    => (uint)method < (uint)MethodCount ? MethodNames[(int)method] : method.ToString();

  private static readonly string[] MethodNames = Enum.GetValues<GemCollectionMethod>().Select(OwnName).ToArray();

  // Module methods are named for their module, so they read from its catalog entry.
  private static string OwnName(GemCollectionMethod method) => method switch
  {
    GemCollectionMethod.ManualClick => Loc.N("Manual Clicks"),
    GemCollectionMethod.TractorBelt => Loc.N("Tractor Belt"),
    GemCollectionMethod.TractorTails => Loc.N("Tractor Tails"),
    GemCollectionMethod.ChainCollection => Loc.N("Chain Collection"),
    GemCollectionMethod.StormDrones => Loc.N("Storm Drones"),
    _ => Enum.TryParse(method.ToString(), out ShipModule module) && module != ShipModule.None
      ? ModuleCatalog.Names[(int)module] : method.ToString(),
  };

  public double PerMinute(int row) => minute[row].PerMinute;
  public double ThisRun(int row) => run[row];
  public double PerMinute(GemCollectionSource source) => PerMinute(Row(source));
  public double ThisRun(GemCollectionSource source) => ThisRun(Row(source));
  public double PerMinute(GemCollectionMethod method) => PerMinute(Row(method));
  public double ThisRun(GemCollectionMethod method) => ThisRun(Row(method));

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
    if ((uint)source < (uint)SourceCount) RecordRow(Row(source), amount);
  }

  // A collector's take by way of a method: the method's own row when it has one, else the
  // source row it lands in (clicks and the belt apart from the home base).
  public void Record(GemCollectionSource collector, GemCollectionMethod method, double amount)
  {
    if (IsBreakdown(method)) RecordRow(Row(method), amount);
    else Record(SourceFor(collector, method), amount);
  }

  private void RecordRow(int row, double amount)
  {
    if (!(amount > 0) || double.IsInfinity(amount)) return;
    minute[row].Record(amount);
    run[row] += amount;
  }

  // A delivered hold, split by where its base value came from (CargoCredits); the rest is
  // the collector's own pickups. Value bonuses apply to the whole hold, so each part gets
  // its share of them.
  public void RecordDelivery(GemCollectionSource collector, double delivered, ulong holdBase, CargoCredits credits)
  {
    if (!(delivered > 0) || double.IsInfinity(delivered)) return;
    double rest = delivered;
    if (holdBase > 0 && credits != null)
      for (int i = 0; i < credits.Count; i++)
      {
        double share = Math.Min(rest, delivered * Math.Min(1.0, (double)credits.ValueAt(i) / holdBase));
        Record(collector, credits.MethodAt(i), share);
        rest -= share;
      }
    Record(collector, GemCollectionMethod.None, rest);
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

  // Keyed by source or method name, so reordering either never mixes up a save.
  public Dictionary<string, double> RunTotals() => Totals<GemCollectionSource>(0, SourceCount);
  public Dictionary<string, double> MethodRunTotals() => Totals<GemCollectionMethod>(SourceCount, MethodCount);

  private Dictionary<string, double> Totals<T>(int first, int count) where T : struct, Enum
  {
    var totals = new Dictionary<string, double>();
    for (int i = 0; i < count; i++)
      if (run[first + i] > 0) totals[Enum.GetName(typeof(T), i)!] = run[first + i];
    return totals;
  }

  public void RestoreRunTotals(Dictionary<string, double> totals, Dictionary<string, double> methodTotals = null)
  {
    Reset();
    Restore<GemCollectionSource>(totals, 0);
    Restore<GemCollectionMethod>(methodTotals, SourceCount);
  }

  private void Restore<T>(Dictionary<string, double> totals, int first) where T : struct, Enum
  {
    if (totals == null) return;
    // Exact names only: Enum.TryParse would also take numbers.
    foreach (var (name, amount) in totals)
      if (Enum.TryParse(name, out T key) && key.ToString() == name && double.IsFinite(amount) && amount > 0
        && IsRow(first + Convert.ToInt32(key)))
        run[first + Convert.ToInt32(key)] = amount;
  }
}

// The part of a hold's base value that came in by a method rather than the collector's own
// pickups, kept until delivery so GemCollectionMeter.RecordDelivery can credit each part.
// A handful of methods per trip at most, so a short list without per-pickup allocation.
public sealed class CargoCredits
{
  private GemCollectionMethod[] methods;
  private ulong[] values;

  public int Count { get; private set; }
  public GemCollectionMethod MethodAt(int index) => methods[index];
  public ulong ValueAt(int index) => values[index];

  public ulong this[GemCollectionMethod method]
  {
    get
    {
      for (int i = 0; i < Count; i++)
        if (methods[i] == method) return values[i];
      return 0;
    }
  }

  public void Add(GemCollectionMethod method, ulong value)
  {
    if (method == GemCollectionMethod.None || value == 0) return;
    for (int i = 0; i < Count; i++)
      if (methods[i] == method)
      {
        values[i] = PrestigeProgression.AddSaturating(values[i], value);
        return;
      }
    if (methods == null || Count == methods.Length)
    {
      int size = Math.Max(4, Count * 2);
      Array.Resize(ref methods, size);
      Array.Resize(ref values, size);
    }
    methods[Count] = method;
    values[Count++] = value;
  }

  // A shuttle's load joins its carrier's hold with its credits.
  public void MoveTo(CargoCredits other)
  {
    for (int i = 0; i < Count; i++) other.Add(methods[i], values[i]);
    Clear();
  }

  public void Clear() => Count = 0;
}
