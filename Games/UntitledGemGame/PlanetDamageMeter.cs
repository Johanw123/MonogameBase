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

  // English display name, marked for the string table; translate it where shown.
  public static string Name(PlanetDamageSource source) => source switch
  {
    PlanetDamageSource.ManualShots => Loc.N("Manual Shots"),
    PlanetDamageSource.AutoCannon => Loc.N("Plasma Repeater"),
    PlanetDamageSource.MiningLaser => Loc.N("Mining Laser"),
    PlanetDamageSource.MagmaScars => Loc.N("Magma Scars"),
    PlanetDamageSource.ArcHarpoon => Loc.N("Arc Harpoon"),
    PlanetDamageSource.TeslaCoil => Loc.N("Tesla Coil"),
    PlanetDamageSource.RocketPods => Loc.N("Rocket Pods"),
    PlanetDamageSource.Railgun => Loc.N("Railgun"),
    PlanetDamageSource.TectonicShockwave => Loc.N("Tectonic Shockwave"),
    PlanetDamageSource.SingularityRound => Loc.N("Singularity Round"),
    PlanetDamageSource.MoltenCraters => Loc.N("Molten Craters"),
    PlanetDamageSource.MagmaDetonation => Loc.N("Magma Detonation"),
    PlanetDamageSource.PlanetaryOverload => Loc.N("Planetary Overload"),
    PlanetDamageSource.KamikazeWing => Loc.N("Kamikaze Wing"),
    PlanetDamageSource.CoreDrill => Loc.N("Core Drill"),
    PlanetDamageSource.PlanetCracker => Loc.N("Planet Cracker"),
    PlanetDamageSource.CargoCatapult => Loc.N("Cargo Catapult"),
    PlanetDamageSource.WeakPoints => Loc.N("Weak Points"),
    PlanetDamageSource.ResonantCore => Loc.N("Resonant Core"),
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
