using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class SignalChecks
{
  public static void Run()
  {
    static void Check(bool value, string message)
    {
      if (!value) throw new Exception(message);
    }
    static void Near(double actual, double expected, string message)
      => Check(Math.Abs(actual - expected) < 0.0001, $"{message}: {actual} != {expected}");

    var wallet = new GameState();
    var signals = wallet.Signals;
    var random = new Random(42);
    Check(!signals.TryScan(wallet, random), "Cannot scan without funds");
    Check(signals.PendingChoices.Count == 0 && signals.ScansPurchased == 0, "Rejected scans must not mutate state");
    wallet.EarnRedGems(1000);
    Check(signals.TryScan(wallet, random), "Exact funds pay for a scan");
    Check(wallet.CurrentRedGemCount == 0 && wallet.RedGemsEarnedThisRun == 1000, "Scan spends balance, not earned prestige progress");
    Check(signals.ScanCost == 1250, "Second scan costs 25% more");
    Check(signals.PendingChoices.Select(x => x.Signal).Distinct().Count() == 3, "Scan offers three distinct signals");
    Check(signals.PendingChoices.All(x => x.Rarity >= 0 && x.Rarity < 5), "All rarity rolls valid");
    wallet.EarnRedGems(10000);
    Check(!signals.TryScan(wallet, random) && wallet.CurrentRedGemCount == 10000, "Cannot pay for a reroll while choosing");
    Check(!signals.TryChoose(3) && signals.PendingChoices.Count == 3, "Invalid choice preserves offer");

    var path = Path.Combine(Path.GetTempPath(), "signals-" + Guid.NewGuid() + ".json");
    try
    {
      var store = new GameSaveStore(path);
      var save = new GameSave { Signals = signals, RedGems = wallet.CurrentRedGemCount };
      save.Meta["SYU1"] = save.Meta["SGU1"] = 1;
      Check(store.Save(save), "Save paid offer");
      var loaded = store.Load();
      Check(loaded != null && loaded.RedGems == 10000 && loaded.Signals.ScanCost == 1250, "Restore wallet and pricing");
      Check(loaded.Signals.PendingChoices.Select(x => (x.Signal, x.Rarity)).SequenceEqual(
        signals.PendingChoices.Select(x => (x.Signal, x.Rarity))), "Restore exact paid choices without rerolling");
      int id = loaded.Signals.PendingChoices[0].Signal;
      Check(loaded.Signals.TryChoose(0) && loaded.Signals.StackCount(id) == 1, "Selection adds one discovery");
      Check(!loaded.Signals.TryChoose(0), "Cannot claim the same offer twice");
      Check(store.Save(loaded) && store.Load().Signals.StackCount(id) == 1, "Discovery persists");
      wallet.CompletePrestige(1);
      Check(ReferenceEquals(wallet.Signals, signals) && signals.PendingChoices.Count == 3 && signals.ScanCost == 1250,
        "Prestige preserves signals, pending offers, and scan prices");

      var manager = new UpgradeManager();
      UpgradeManager.CurrentUpgrades = new();
      var upgrades = UpgradeManager.CurrentUpgrades;
      upgrades.LoadJson(File.ReadAllText("Content/Data/upgrades_meta.json"), File.ReadAllText("Content/Data/upgrades_meta_buttons.json"),
        upgrades.UpgradeButtonsMeta, upgrades.UpgradeDefinitionsMeta);
      var output = Console.Out;
      try { Console.SetOut(TextWriter.Null); manager.RestoreProgress(loaded); }
      finally { Console.SetOut(output); }
      Check(manager.UGM.ShipyardUnlocked && manager.UGM.SignalsUnlocked, "Prestige unlocks restore from tree purchases");

      var stats = manager.Signals;
      stats.Counts[0] = 1; stats.Counts[2] = 1; // Common + rare speed.
      Near(stats.BonusPercent(0), 17, "Mixed rarity speed total");
      stats.Counts[5] = 1; stats.Counts[7] = 1;
      Near(stats.CooldownMultiplier, 0.95 * 0.88, "Cooldown stacks multiply remaining time");
      foreach (var type in new[] { Harvester.HarvesterType.Harvester, Harvester.HarvesterType.AdvancedHarvester,
        Harvester.HarvesterType.ExpertHarvester, Harvester.HarvesterType.UltimateHarvester,
        Harvester.HarvesterType.PerimeterHarvester, Harvester.HarvesterType.Drone })
      {
        var ship = new Harvester { Type = type };
        float boosted = BaseStats.GetHarvesterSpeed(ship);
        stats.Counts[0] = stats.Counts[2] = 0;
        float baseline = BaseStats.GetHarvesterSpeed(ship);
        stats.Counts[0] = stats.Counts[2] = 1;
        Near(boosted, baseline * 1.17, $"Speed applies to {type}");
      }
      foreach (IHomeBaseAbility ability in new IHomeBaseAbility[] { new MagnetAbility(), new ChainLightningAbility(),
        new DroneAbility(), new GemSpawnerAbility(), new SpeedboostAbility() })
      {
        int boosted = ability.MaxCooldownTime;
        stats.Counts[5] = stats.Counts[7] = 0;
        int baseline = ability.MaxCooldownTime;
        stats.Counts[5] = stats.Counts[7] = 1;
        Check(boosted == Math.Max(1, (int)(baseline * 0.95 * 0.88)), "Every ability receives cooldown reduction");
      }
      var fleet = new Harvester { Type = Harvester.HarvesterType.Harvester };
      manager.UG.HarvesterCapacity = 100;
      stats.Counts[10] = 1;
      Check(BaseStats.GetHarvesterCapacity(fleet) >= 105, "Cargo signal increases capacity");
      float fuel = BaseStats.GetHarvesterMaxFuelMultiplier(fleet);
      float refuel = BaseStats.GetHarvesterRefuelSpeedMultiplier(fleet);
      stats.Counts[20] = stats.Counts[25] = 1;
      Near(BaseStats.GetHarvesterMaxFuelMultiplier(fleet), fuel * 1.05, "Fuel capacity bonus");
      Near(BaseStats.GetHarvesterRefuelSpeedMultiplier(fleet), refuel * 1.05, "Refueling bonus");
      stats.Counts[15] = 1;
      Check(BaseStats.GetHarvesterDeliveryValue(new Harvester { Type = Harvester.HarvesterType.HomeBase }, 100) == 105,
        "Gem value applies to homebase collections too");
      stats.Counts[5] = 100000;
      Check(new MagnetAbility().MaxCooldownTime == 1, "Large cooldown stacks stay positive");
      signals.ScansPurchased = ulong.MaxValue;
      Check(signals.ScanCost == null && !signals.TryScan(wallet, random), "Cost overflow cannot create free scans");
    }
    finally
    {
      foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    Console.WriteLine("Signal checks passed: costs, offers, mixed rarities, all bonuses, save/load, prestige and unlocks.");
  }
}
