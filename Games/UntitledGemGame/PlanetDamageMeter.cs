using System;
using System.Collections.Generic;

namespace UntitledGemGame;

// What dealt the planet's damage, for the HUD's Damage panel (GameScreen.DamagePanel.cs).
// Effects that grow out of a weapon's hit (scars, craters, shockwaves) count as their
// own source, named after the upgrade or talent that adds them.
public enum PlanetDamageSource
{
  ManualShots,
  AutoCannon,
  MiningLaser,
  MagmaScars,
  ArcHarpoon,
  TeslaCoil,
  RocketPods,
  Railgun,
  TectonicShockwave,
  SingularityRound,
  MoltenCraters,
  MagmaDetonation,
  PlanetaryOverload,
  KamikazeWing,
  CoreDrill,
  PlanetCracker,
  CargoCatapult,
  WeakPoints,
  ResonantCore,
}

// Each source's damage over the last minute (PlanetDamageTracker) and over the run.
// Run totals are saved with the run and reset at extraction.
public sealed class PlanetDamageMeter
{
  public static readonly int SourceCount = Enum.GetValues<PlanetDamageSource>().Length;

  private readonly PlanetDamageTracker[] minute = new PlanetDamageTracker[SourceCount];
  private readonly double[] run = new double[SourceCount];

  public PlanetDamageMeter()
  {
    for (int i = 0; i < SourceCount; i++) minute[i] = new PlanetDamageTracker();
  }

  public static string Name(PlanetDamageSource source) => source switch
  {
    PlanetDamageSource.ManualShots => "Manual Shots",
    PlanetDamageSource.AutoCannon => "Plasma Repeater",
    PlanetDamageSource.MiningLaser => "Mining Laser",
    PlanetDamageSource.MagmaScars => "Magma Scars",
    PlanetDamageSource.ArcHarpoon => "Arc Harpoon",
    PlanetDamageSource.TeslaCoil => "Tesla Coil",
    PlanetDamageSource.RocketPods => "Rocket Pods",
    PlanetDamageSource.Railgun => "Railgun",
    PlanetDamageSource.TectonicShockwave => "Tectonic Shockwave",
    PlanetDamageSource.SingularityRound => "Singularity Round",
    PlanetDamageSource.MoltenCraters => "Molten Craters",
    PlanetDamageSource.MagmaDetonation => "Magma Detonation",
    PlanetDamageSource.PlanetaryOverload => "Planetary Overload",
    PlanetDamageSource.KamikazeWing => "Kamikaze Wing",
    PlanetDamageSource.CoreDrill => "Core Drill",
    PlanetDamageSource.PlanetCracker => "Planet Cracker",
    PlanetDamageSource.CargoCatapult => "Cargo Catapult",
    PlanetDamageSource.WeakPoints => "Weak Points",
    PlanetDamageSource.ResonantCore => "Resonant Core",
    _ => source.ToString(),
  };

  public double PerMinute(PlanetDamageSource source) => minute[(int)source].PerMinute;
  public double ThisRun(PlanetDamageSource source) => run[(int)source];

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
      foreach (double damage in run) total += damage;
      return total;
    }
  }

  public void Record(PlanetDamageSource source, double damage)
  {
    if (!(damage > 0) || double.IsInfinity(damage) || (uint)source >= (uint)SourceCount) return;
    minute[(int)source].Record(damage);
    run[(int)source] += damage;
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
      if (run[i] > 0) totals[((PlanetDamageSource)i).ToString()] = run[i];
    return totals;
  }

  public void RestoreRunTotals(Dictionary<string, double> totals)
  {
    Reset();
    if (totals == null) return;
    // Exact names only: Enum.TryParse would also take numbers.
    foreach (var (name, damage) in totals)
      if (Enum.TryParse(name, out PlanetDamageSource source) && source.ToString() == name
        && double.IsFinite(damage) && damage > 0)
        run[(int)source] = damage;
  }
}
