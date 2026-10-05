using System;

namespace UntitledGemGame;

// Core Drill ship system: a pod from the homebase curves around the planet, lands on
// its far side (away from the weapon lane) and bores gems out of layers deeper than
// the cannon reaches. Genesis Pulse makes many ordinary gems; the drill makes few,
// deep ones. Rendering and spawning live in GameScreen.CoreDrill.cs.
public static class CoreDrill
{
  public const int CooldownMilliseconds = 8000;
  public const float FlightSeconds = 1.5f;
  public const float BaseDrillSeconds = 4f;
  public const float BaseGemsPerSecond = 1.5f;
  public const int MaxFaults = 8;
  // Pressure Build: one layer deeper per second of boring.
  public const float PressureSecondsPerLayer = 1f;
  // Tectonic Rupture: each crack bursts this share of the pod's drilled gems, all around the planet.
  public const float RupturePerFault = 0.15f;
  // Core Tap: a final geyser of this share of the drilled gems, deeper and more valuable.
  public const float CoreTapShare = 0.3f;
  public const int CoreTapMinimum = 4;
  public const int CoreTapLayers = 4;
  public const float CoreTapValue = 3f;
  // Hollow World: every finished drill adds tunnels; planet hits knock loose more gems.
  public const float HollowStep = 0.03f;
  public const float HollowCap = 0.45f;
  public const int MaxTunnels = 1000;

  // A layer is one gem-quality row: the next color's fire power, then every eight
  // fire power past the last color (like Deep-Core Munitions).
  public static int Deeper(int firePower, int layers)
  {
    firePower = Math.Max(1, firePower);
    for (int i = 0; i < layers; i++)
    {
      int next = firePower + GemQualityTable.FirePowerPerExtraRow;
      foreach (var (_, required) in GemQualityTable.ColorFirePower)
        if (required > firePower) { next = required; break; }
      firePower = next;
    }
    return firePower;
  }

  public static float DrillSeconds(UpgradesGeneratorUpgrades_abilities a)
    => BaseDrillSeconds * Math.Max(0.1f, a.CoreDrillDuration);

  public static float GemsPerSecond(UpgradesGeneratorUpgrades_abilities a)
    => BaseGemsPerSecond * Math.Max(0f, a.CoreDrillRate);

  public static int Faults(UpgradesGeneratorUpgrades_abilities a)
    => a.CoreDrillFaultLines ? Math.Clamp(a.CoreDrillFaults, 0, MaxFaults) : 0;

  // Each crack leaks this share of the bore rate.
  public static float FaultLeak(UpgradesGeneratorUpgrades_abilities a)
    => Math.Max(0, a.CoreDrillFaultLeak) / 100f;

  public static int Layers(UpgradesGeneratorUpgrades_abilities a, float drilledSeconds)
    => Math.Max(0, a.CoreDrillDepth)
      + (a.CoreDrillPressure ? (int)(Math.Max(0f, drilledSeconds) / PressureSecondsPerLayer) : 0);

  public static int MaxLayers(UpgradesGeneratorUpgrades_abilities a) => Layers(a, DrillSeconds(a));

  public static float ValueMultiplier(UpgradesGeneratorUpgrades_abilities a)
    => 1f + Math.Max(0, a.CoreDrillValue) / 100f;

  public static int ResonanceLayers(UpgradesGeneratorUpgrades_abilities a)
    => a.CoreDrillResonance ? Math.Max(0, a.CoreDrillResonanceDepth) : 0;

  public static float ResonanceLinger(UpgradesGeneratorUpgrades_abilities a)
    => a.CoreDrillResonance ? Math.Max(0f, a.CoreDrillResonanceLinger) : 0f;

  public static float HollowBonus(UpgradesGeneratorUpgrades_abilities a, int tunnels)
    => a.CoreDrillHollowWorld ? Math.Min(HollowCap, Math.Max(0, tunnels) * HollowStep) : 0f;

  public static int RuptureGems(UpgradesGeneratorUpgrades_abilities a, int drilled)
    => a.CoreDrillRupture ? (int)MathF.Round(Math.Max(0, drilled) * RupturePerFault * Faults(a)) : 0;

  public static int CoreTapGems(UpgradesGeneratorUpgrades_abilities a, int drilled)
    => a.CoreDrillCoreTap ? Math.Max(CoreTapMinimum, (int)MathF.Round(Math.Max(0, drilled) * CoreTapShare)) : 0;

  // Expected gems from one drill, finishers included (balance checks and tooltips).
  public static float GemsPerDrill(UpgradesGeneratorUpgrades_abilities a)
  {
    float drilled = GemsPerSecond(a) * DrillSeconds(a) * (1f + Faults(a) * FaultLeak(a));
    return drilled + RuptureGems(a, (int)drilled) + CoreTapGems(a, (int)drilled);
  }
}
