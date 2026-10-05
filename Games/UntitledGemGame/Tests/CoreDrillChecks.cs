using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class CoreDrillChecks
{
  public static void Run(Upgrades upgrades)
  {
    var previousManager = UpgradeManager.Instance;
    try
    {
      CheckLayers();
      CheckTradeOff(upgrades);
      CheckFinishers();
      CheckTunnels();
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Core drill checks passed: layers, fewer but deeper gems than Genesis Pulse, finishers, tunnels and save/load.");
  }

  private static void CheckLayers()
  {
    Check(CoreDrill.Deeper(10, 0) == 10 && CoreDrill.Deeper(1, 1) == 3 && CoreDrill.Deeper(3, 2) == 10,
      "A layer must be the next gem color's fire power");
    Check(CoreDrill.Deeper(35, 1) == 43 && CoreDrill.Deeper(40, 2) == 56,
      "Past the last color, a layer must be one extra quality row");
    var a = new UpgradesGeneratorUpgrades_abilities();
    Check(CoreDrill.Layers(a, 3f) == 2 && CoreDrill.MaxLayers(a) == 2, "Without Pressure Build the depth must not ramp");
    a.CoreDrillPressure = true;
    Check(CoreDrill.Layers(a, 0.5f) == 2 && CoreDrill.Layers(a, 3.2f) == 5 && CoreDrill.MaxLayers(a) == 6,
      "Pressure Build must add a layer per second of boring");
  }

  // The drill knocks loose fewer gems than Genesis Pulse, from deeper layers: at the
  // start and with both systems fully learned.
  private static void CheckTradeOff(Upgrades upgrades)
  {
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
    Compare(manager.UGA, "unlearned");
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Abilities = upgrades.UpgradeButtonsAbilities
      .ToDictionary(pair => pair.Key, pair => pair.Value.Data.NumLevels) });
    Check(manager.UGA.CoreDrillHollowWorld && manager.UGA.GemSpawnerCosmicGenesis, "The fixture must learn both full systems");
    Compare(manager.UGA, "fully learned");
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
  }

  private static void Compare(UpgradesGeneratorUpgrades_abilities a, string stage)
  {
    int count = a.GemSpawnerNrGems, genesis = 0;
    for (int ring = 0; ring < a.GemSpawnerNumberOfRings; ring++)
    {
      genesis += count;
      count = GemSpawnerAbility.GetNextRingGemCount(count, a.GemSpawnerRingReduction);
    }
    if (a.GemSpawnerGenesisSpiral) genesis += a.GemSpawnerNrGems * (a.GemSpawnerCosmicGenesis ? 2 : 1);
    double genesisRate = genesis / (BaseStats.GemSpawnerCooldownMilliseconds / 1000.0 / a.GemSpawnerCooldown);
    double drillRate = CoreDrill.GemsPerDrill(a) / (CoreDrill.CooldownMilliseconds / 1000.0 / a.CoreDrillCooldown);
    Check(drillRate > 0 && drillRate < genesisRate * 0.8,
      $"The Core Drill must knock loose fewer gems than Genesis Pulse ({stage}: {drillRate:0.##} vs {genesisRate:0.##}/s)");
    Check(a.CoreDrillDepth >= 2, $"Drilled gems must come from deeper than the cannon ({stage})");
  }

  private static void CheckFinishers()
  {
    var a = new UpgradesGeneratorUpgrades_abilities();
    Check(CoreDrill.Faults(a) == 0 && CoreDrill.RuptureGems(a, 100) == 0 && CoreDrill.CoreTapGems(a, 100) == 0
      && CoreDrill.HollowBonus(a, 50) == 0, "Unlearned talents must do nothing");
    a.CoreDrillFaultLines = true;
    a.CoreDrillFaults = 20;
    a.CoreDrillRupture = true;
    a.CoreDrillCoreTap = true;
    Check(CoreDrill.Faults(a) == CoreDrill.MaxFaults
      && CoreDrill.RuptureGems(a, 10) == (int)MathF.Round(10 * CoreDrill.RupturePerFault * CoreDrill.MaxFaults),
      "Cracks must be bounded and scale Tectonic Rupture");
    Check(CoreDrill.CoreTapGems(a, 0) == CoreDrill.CoreTapMinimum && CoreDrill.CoreTapGems(a, 100) == 30,
      "Core Tap must always erupt and grow with the drilled gems");
    a.CoreDrillHollowWorld = true;
    Check(CoreDrill.HollowBonus(a, 2) == 2 * CoreDrill.HollowStep && CoreDrill.HollowBonus(a, 500) == CoreDrill.HollowCap,
      "Hollow World must grow per drill up to its cap");
    a.CoreDrillResonanceDepth = 3;
    a.CoreDrillResonanceLinger = 2f;
    Check(CoreDrill.ResonanceLayers(a) == 0 && CoreDrill.ResonanceLinger(a) == 0,
      "Resonance upgrades must need Seismic Resonance");
    a.CoreDrillResonance = true;
    Check(CoreDrill.ResonanceLayers(a) == 3 && CoreDrill.ResonanceLinger(a) == 2f, "Seismic Resonance must use its ranks");
  }

  private static void CheckTunnels()
  {
    var state = new GameState { CoreDrillTunnels = 9 };
    string directory = Path.Combine(Path.GetTempPath(), "core-drill-" + Guid.NewGuid());
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "progress.json"));
      Check(store.Save(new GameSave { CoreDrillTunnels = 9 }) && store.Load()?.CoreDrillTunnels == 9,
        "Hollow World tunnels must survive save/load within a run");
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    state.CompletePrestige(1);
    Check(state.CoreDrillTunnels == 0, "Extracting the core must collapse the tunnels");
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }
}
