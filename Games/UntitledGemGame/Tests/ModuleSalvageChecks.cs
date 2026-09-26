using UntitledGemGame;

internal static class ModuleSalvageChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private sealed class TicketRandom(int ticket) : Random
  {
    private bool rarityRolled;
    public override double NextDouble() => 0;
    public override int Next(int maxValue)
    {
      int result = rarityRolled ? 0 : ticket;
      rarityRolled = true;
      Check(result >= 0 && result < maxValue, "Rarity ticket is in the expected remaining weight range");
      return result;
    }
  }

  private static ShipModule Find(ShipyardModules modules, Random random)
  {
    modules.DiscoveryProgressSeconds = modules.DiscoveryThresholdSeconds - 0.5;
    var rarity = modules.DiscoveryRarity;
    modules.RecordHarvest();
    Check(modules.AdvanceSalvage(0.5, random), "Completing the threshold awards a module");
    Check(ModuleCatalog.Rarities[(int)modules.PendingReveals[^1]] == rarity, "Discovery awards the advertised rarity");
    return modules.PendingReveals[^1];
  }

  public static void Run()
  {
    var manager = UpgradeManager.Instance;
    var definitions = UpgradeManager.CurrentUpgrades;
    try
    {
      CheckUnlockAndOwnership();
      CheckDiscoverAll();
      CheckRarityTiming();
      CheckTiming();
      CheckSignalScans();
      CheckWeightsAndCompletion();
      CheckPersistence();
      CheckValidation();
      Console.WriteLine("Module salvage passed: starter ownership, advertised rarity, rarity-based timers, capped harvesting progress, exact rarity weights, duplicate-free depletion, queued reveals, reload, prestige and completion.");
    }
    finally
    {
      UpgradeManager.Instance = manager;
      UpgradeManager.CurrentUpgrades = definitions;
    }
  }

  private static void CheckUnlockAndOwnership()
  {
    var manager = new UpgradeManager();
    var modules = manager.Modules;
    Check(modules.Owned.Count == 0 && !modules.GetAvailableModules().Any()
      && !modules.TryEquip(0, 0, ShipModule.PhaseAnchor), "A new game owns no modules before Shipyard");
    modules.RecordHarvest();
    Check(!modules.AdvanceSalvage(100, new Random(1)) && modules.DiscoveryProgressSeconds == 0,
      "Harvesting before Shipyard grants no progress");
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    var definitions = UpgradeManager.CurrentUpgrades = new Upgrades();
    definitions.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta.json")),
      File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta_buttons.json")),
      definitions.UpgradeButtonsMeta, definitions.UpgradeDefinitionsMeta);
    manager.RestoreProgress(new GameSave { Meta = new() { ["SYU1"] = 1 } });
    Check(modules.SalvageStarted && modules.Owned.SetEquals(new[] { ShipModule.CargoPod, ShipModule.IonBooster }),
      "Shipyard unlock grants exactly Cargo Pod and Ion Booster");
    double threshold = modules.DiscoveryThresholdSeconds;
    Check(threshold >= 60 && threshold <= 120 && !modules.StartSalvage(new Random(5))
      && modules.DiscoveryThresholdSeconds == threshold, "Repeated unlock does not regrant or reroll progress");
    Check(modules.TryEquip(0, 0, ShipModule.CargoPod) && !modules.TryEquip(1, 0, ShipModule.CargoPod),
      "Owned starter can be equipped once");
    var found = Find(modules, new Random(5));
    Check(modules.Owned.Contains(found) && !modules.IsAvailable(found)
      && !modules.TryEquip(0, 1, found), "A sealed reward is reserved but cannot be equipped or previewed in inventory");
    Check(!modules.TryAcknowledgeReveal(ShipModule.None), "Only the queued reward can be acknowledged");
    Check(modules.TryAcknowledgeReveal(found) && modules.IsAvailable(found)
      && modules.TryEquip(0, 1, found) && !modules.TryAcknowledgeReveal(found), "Acknowledgement releases exactly one usable module");
    modules.Validate();
  }

  private static void CheckDiscoverAll()
  {
    var modules = new ShipyardModules();
    modules.DiscoverAllModules();
    modules.Validate();
    Check(modules.CollectionComplete && modules.GetAvailableModules().Count() == ModuleCatalog.InventoryOrder.Length,
      "Debug discovery reveals the complete collection even before salvage starts");
    Check(modules.TryEquip(0, 0, ShipModule.CargoPod), "Debug modules can be equipped immediately");
    modules.PendingReveals.Add(ShipModule.CourierSeal);
    modules.DiscoverAllModules();
    modules.Validate();
    Check(modules.PendingReveals.Count == 0 && modules.Slots[0] == ShipModule.CargoPod,
      "Repeated debug discovery clears pending reveals and preserves equipment");
    Check(!modules.AdvanceSalvage(1, new Random(1)), "Completed debug discovery stops further finds");
    string path = Path.Combine(Path.GetTempPath(), $"debug-modules-{Guid.NewGuid():N}.json");
    try
    {
      var store = new GameSaveStore(path);
      Check(store.Save(new GameSave { Modules = modules }), "Debug discovery can be saved");
      var loaded = store.Load()!.Modules;
      Check(loaded.CollectionComplete && loaded.PendingReveals.Count == 0
        && loaded.Slots[0] == ShipModule.CargoPod, "Debug discovery and equipment survive a save round trip");
    }
    finally
    {
      foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.Delete(path + suffix);
    }
  }

  private sealed class TimingRandom(int ticket, double fraction) : Random
  {
    public override int Next(int maxValue) => maxValue == 100 ? ticket : 0;
    public override double NextDouble() => fraction;
  }

  private static void CheckRarityTiming()
  {
    int[] tickets = [0, 50, 78, 93, 99];
    foreach (var rarity in Enum.GetValues<ModuleRarity>())
    {
      foreach (double fraction in new[] { 0d, 0.999999 })
      {
        var modules = new ShipyardModules();
        var random = new TimingRandom(tickets[(int)rarity], fraction);
        modules.StartSalvage(random);
        Check(modules.DiscoveryRarity == rarity && modules.DiscoveryThresholdSeconds >= 60
          && modules.DiscoveryThresholdSeconds <= 120, "First discovery advertises rarity but stays quick");
        Find(modules, random);
        double minimum = 240 + (int)rarity * 60;
        Check(modules.DiscoveryRarity == rarity
          && Math.Abs(modules.DiscoveryThresholdSeconds - (minimum + fraction * 120)) < 0.001,
          $"{rarity} uses its own discovery time range");
        modules.Validate();
      }
    }
  }

  private static void CheckTiming()
  {
    var modules = new ShipyardModules();
    modules.StartSalvage(new TicketRandom(0));
    Check(!modules.AdvanceSalvage(10, new Random(1)) && modules.DiscoveryProgressSeconds == 0, "No progress without harvesting");
    modules.RecordHarvest();
    Check(!modules.AdvanceSalvage(1000, new Random(1)) && modules.DiscoveryProgressSeconds == 1,
      "A long frame cannot instantly grant minutes of salvage progress");
    modules.DiscoveryProgressSeconds = 0;
    for (int i = 0; i < 59; i++)
    {
      for (int pickup = 0; pickup < 1000; pickup++) modules.RecordHarvest();
      Check(!modules.AdvanceSalvage(1, new Random(i)), "Even thousands of pickups cannot bypass elapsed harvesting time");
    }
    Check(modules.DiscoveryProgressSeconds == 59 && modules.Owned.Count == 2, "First find waits for its threshold");
    modules.RecordHarvest();
    Check(modules.AdvanceSalvage(1, new Random(9)) && modules.Owned.Count == 3
      && modules.DiscoveryProgressSeconds == 0 && modules.DiscoveryThresholdSeconds >= ShipyardModules.MinimumDiscoverySeconds(modules.DiscoveryRarity!.Value)
      && modules.DiscoveryThresholdSeconds <= ShipyardModules.MaximumDiscoverySeconds(modules.DiscoveryRarity!.Value), "First find starts a timer for the next rarity");
    for (int i = 0; i < 30; i++) modules.AdvanceSalvage(1, new Random(i));
    Check(modules.DiscoveryProgressSeconds == 14, "Harvest activity expires when collections stop");
    double progress = modules.DiscoveryProgressSeconds;
    foreach (double dt in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
      Check(!modules.AdvanceSalvage(dt, new Random(1)) && modules.DiscoveryProgressSeconds == progress, "Invalid or paused time adds no progress");
  }

  private sealed class ScanRandom(double roll) : Random(73)
  {
    public override double NextDouble() => roll;
  }

  private static void CheckSignalScans()
  {
    var state = new GameState();
    var modules = state.Modules;
    var hit = new ScanRandom(0);
    Check(!modules.AdvanceDiscoveryFromSignalScan(hit) && modules.DiscoveryProgressSeconds == 0,
      "Scans before Shipyard cannot advance discovery");
    modules.StartSalvage(hit);
    Check(!state.Signals.TryScan(state, hit) && modules.DiscoveryProgressSeconds == 0,
      "Unpaid scans cannot advance discovery");
    state.CurrentRedGemCount = 10000;
    Check(!state.Signals.TryScan(state, hit, _ => false) && modules.DiscoveryProgressSeconds == 0,
      "Unavailable scans cannot advance discovery");
    Check(state.Signals.TryScan(state, hit) && modules.DiscoveryProgressSeconds == 30,
      "A successful scan roll advances discovery while harvesting is idle");
    Check(!state.Signals.TryScan(state, hit) && modules.DiscoveryProgressSeconds == 30,
      "Pending signal choices prevent repeat scan bonuses");
    Check(state.Signals.TryChoose(0) && modules.DiscoveryProgressSeconds == 30,
      "Choosing a signal does not award a second bonus");
    Check(state.Signals.TryScan(state, new ScanRandom(0.25)) && modules.DiscoveryProgressSeconds == 30,
      "A paid scan can miss the discovery bonus at the probability boundary");
    state.Signals.TryChoose(0);
    modules.DiscoveryProgressSeconds = 50;
    Check(state.Signals.TryScan(state, hit) && modules.PendingReveals.Count == 1
      && modules.DiscoveryProgressSeconds == 20 && modules.Owned.Count == 3,
      "Scan completing discovery queues one new module and carries excess progress");
    Check(!modules.AdvanceSalvage(1, hit) && modules.DiscoveryProgressSeconds == 20,
      "Scanning does not simulate harvesting activity");
    modules.Validate();
    modules.DiscoverAllModules();
    Check(!modules.AdvanceDiscoveryFromSignalScan(hit) && modules.DiscoveryProgressSeconds == 0,
      "Completed collections receive no further scan progress");
  }

  private static void CheckWeightsAndCompletion()
  {
    // Sweep every integer ticket: rarity weights must not depend on tier pool size.
    var counts = new int[5];
    for (int ticket = 0; ticket < 100; ticket++)
    {
      var modules = new ShipyardModules();
      modules.StartSalvage(new TicketRandom(ticket));
      counts[(int)ModuleCatalog.Rarities[(int)Find(modules, new Random(1))]]++;
    }
    Check(counts.SequenceEqual(new[] { 50, 28, 15, 6, 1 }), "Rarity weights are exactly 50/28/15/6/1");
    var exhausted = new ShipyardModules();
    exhausted.Owned.UnionWith(ModuleCatalog.InventoryOrder.Where(m => ModuleCatalog.Rarities[(int)m] == ModuleRarity.Common));
    exhausted.StartSalvage(new TicketRandom(0));
    Check(ModuleCatalog.Rarities[(int)Find(exhausted, new TicketRandom(0))] == ModuleRarity.Uncommon,
      "Exhausted common tier is removed, not rerolled or duplicated");
    var legendaryOnly = new ShipyardModules();
    legendaryOnly.Owned.UnionWith(ModuleCatalog.InventoryOrder.Where(m => ModuleCatalog.Rarities[(int)m] != ModuleRarity.Legendary));
    legendaryOnly.StartSalvage(new TicketRandom(0));
    Check(ModuleCatalog.Rarities[(int)Find(legendaryOnly, new TicketRandom(0))] == ModuleRarity.Legendary,
      "Remaining legendary pool becomes guaranteed once other tiers are exhausted");
    var collection = new ShipyardModules();
    collection.StartSalvage(new Random(1));
    var random = new Random(417);
    while (!collection.CollectionComplete)
    {
      int owned = collection.Owned.Count;
      var found = Find(collection, random);
      Check(collection.Owned.Count == owned + 1 && found != ShipModule.CargoPod && found != ShipModule.IonBooster,
        "Every find is new even while previous finds are still sealed");
      collection.Validate();
    }
    Check(collection.PendingReveals.Count == ModuleCatalog.Names.Length - 3 && collection.RevealedCount == 2,
      "The entire collection can queue without duplicates or gameplay interruption");
    Check(collection.DiscoveryRarity == null && collection.DiscoveryThresholdSeconds == 0
      && collection.DiscoveryProgressSeconds == 0, "The last discovery clears the active trace and timer");
    double finalProgress = collection.DiscoveryProgressSeconds;
    collection.RecordHarvest();
    Check(!collection.AdvanceSalvage(1000, random) && collection.DiscoveryProgressSeconds == finalProgress,
      "Discoveries stop when the collection is complete");
    while (collection.PendingReveals.Count > 0)
      Check(collection.TryAcknowledgeReveal(collection.PendingReveals[0]), "Reveal every queued reward");
    Check(collection.RevealedCount == ModuleCatalog.Names.Length - 1, "Completed collection contains every unique module");
  }

  private static void CheckPersistence()
  {
    var state = new GameState();
    var modules = state.Modules;
    modules.StartSalvage(new Random(3));
    var random = new Random(51);
    Find(modules, random);
    Find(modules, random);
    modules.DiscoveryProgressSeconds = 42.5;
    string path = Path.Combine(Path.GetTempPath(), "salvage-" + Guid.NewGuid() + ".json");
    try
    {
      var store = new GameSaveStore(path);
      Check(store.Save(new GameSave { Modules = modules }), "Save ownership and pending reveals");
      var loaded = store.Load()!.Modules;
      Check(loaded.PendingReveals.SequenceEqual(modules.PendingReveals) && loaded.Owned.SetEquals(modules.Owned)
        && loaded.DiscoveryProgressSeconds == 42.5 && loaded.DiscoveryThresholdSeconds == modules.DiscoveryThresholdSeconds
        && loaded.DiscoveryRarity == modules.DiscoveryRarity,
        "Reload restores exact rewards and progress rather than rolling again");
      Check(!loaded.StartSalvage(new Random(999)) && !loaded.AdvanceSalvage(1000, random)
        && loaded.DiscoveryProgressSeconds == 42.5, "Reload cannot grant extra starters or offline discovery credit");
      ShipModule reward = loaded.PendingReveals[0];
      loaded.TryAcknowledgeReveal(reward);
      Check(store.Save(new GameSave { Modules = loaded }) && store.Load()!.Modules.IsAvailable(reward),
        "Acknowledged reward stays usable after closing and reopening the game");
      var rarityBeforePrestige = modules.DiscoveryRarity;
      double thresholdBeforePrestige = modules.DiscoveryThresholdSeconds;
      modules.RecordHarvest();
      state.CompletePrestige(1);
      Check(ReferenceEquals(state.Modules, modules) && modules.PendingReveals.Count == 2
        && modules.DiscoveryProgressSeconds == 42.5 && modules.DiscoveryRarity == rarityBeforePrestige
        && modules.DiscoveryThresholdSeconds == thresholdBeforePrestige && !modules.AdvanceSalvage(1, random),
        "Prestige preserves rewards, rarity and timer while ending previous-run harvesting activity");
      var promisedRarity = loaded.DiscoveryRarity;
      var recovered = Find(loaded, new Random(999));
      Check(ModuleCatalog.Rarities[(int)recovered] == promisedRarity,
        "Completing a reloaded trace awards its saved rarity");
    }
    finally
    {
      foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.Delete(path + suffix);
    }
  }

  private static void CheckValidation()
  {
    foreach (var corrupt in new Action<ShipyardModules>[]
    {
      m => m.Owned.Add(ShipModule.None),
      m => m.Slots[0] = ShipModule.PhaseAnchor,
      m => m.PendingReveals.Add(ShipModule.PhaseAnchor),
      m => { m.PendingReveals.Add(ShipModule.CargoPod); m.PendingReveals.Add(ShipModule.CargoPod); },
      m => { m.PendingReveals.Add(ShipModule.CargoPod); m.Slots[0] = ShipModule.CargoPod; },
      m => m.DiscoveryProgressSeconds = double.NaN,
      m => m.DiscoveryThresholdSeconds = 0,
      m => m.DiscoveryRarity = null,
      m => m.DiscoveryRarity = (ModuleRarity)99,
      m => m.Owned.UnionWith(ModuleCatalog.InventoryOrder.Where(module => ModuleCatalog.Rarities[(int)module] == m.DiscoveryRarity))
    })
    {
      var modules = new ShipyardModules();
      modules.StartSalvage(new Random(1));
      corrupt(modules);
      bool rejected = false;
      try { modules.Validate(); } catch (InvalidDataException) { rejected = true; }
      Check(rejected, "Reject invalid ownership, equipment, reveal queues and progress");
    }
  }
}
