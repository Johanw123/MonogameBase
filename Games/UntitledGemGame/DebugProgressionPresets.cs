using System;
using System.Collections.Generic;
using System.Linq;

namespace UntitledGemGame;

public static class DebugProgressionPresets
{
  public static readonly string[] Names =
    ["Beginning", "First upgrades", "First prestige", "Developing fleet", "Mid game: logistics",
     "Mid game: abilities", "Late game: production", "Late game: fleet",
     "Early game: clicking", "Mid game: clicking", "Late game: clicking", "Uber endgame"];
  public static readonly string[] Descriptions =
    ["Fresh run with one drifter.", "Small fleet, no permanent upgrades.",
     "First talents, opening Tier 3: a few ship system talents and a fresh Shipyard.", "Shipyard and signals with a modest collection.",
     "Cargo and propulsion preference; repeated common signals.", "Ship system preference with a different signal spread.",
     "Production preference, developed ship systems, incomplete module collection.",
     "Fleet preference, deeper permanent upgrades, still missing many signals.",
     "Click value, click radius and early chains, with a small supporting fleet.",
     "Click combos, sustained harvesting and the tractor belt; manual collection signals and permanent bonuses.",
     "Deep click chains, sustained harvesting and tractor belt upgrades with a focused signal collection.",
     "Everything maxed for chaos testing; deliberately unrealistic."];
  public static readonly string[] FeatureNames =
    ["Abilities: starter", "Abilities: fully upgraded", "Shipyard: Drifters", "Shipyard: Seekers",
     "Shipyard: Prospectors", "Shipyard: Trove hunters", "Shipyard: Rimrunners",
     "Modules: discovery queue", "Signals: pending choice", "Manual collection", "Weapons: all unlocked"];

  // Every main ship weapon node, bought to its last level by the weapon sandboxes.
  public static readonly string[] FullWeaponNodes =
    ["AHFC1", "AHT1", "AHE1", "ROB1", "RCS1", "ROBZ1", "RGS1", "RGM1", "RGC1", "LZS1", "LZB1", "LZV1", "LMR1", "CCP1", "CCD1", "CRN1", "CRS1", "CBR1", "CAF1", "CSE1", "AC1", "CFR1", "CFP1", "CSS1", "LZ1", "LZR1", "LZP1", "LZT1", "LZD1",
     "AH1", "AHR1", "AHP1", "AHB1", "AHD1", "AHF1", "AHDX1", "AHW1",
     "RP1", "RPR1", "RPP1", "RPC1", "RG1", "RGR1", "RGP1", "RGF1",
     "FK1", "FKR1", "FKP1", "FKC1", "FKW1", "FKF1",
     "CRB1", "CCR1", "LZM1", "LZH1", "RCW1", "ROS1", "BTS1", "RCN1"];

  public static GameSave Create(int stage, Upgrades upgrades)
  {
    if (stage < 0 || stage >= Names.Length) throw new ArgumentOutOfRangeException(nameof(stage));
    bool max = stage == Names.Length - 1;
    bool clicking = stage is >= 8 and <= 10;
    stage = ProgressionStage(stage);
    // Late stages pay the inflated prices (PriceInflation) for the same builds as before.
    ulong[] earnings = [100, 10_000, 150_000, 3_000_000, 450_000_000, 450_000_000,
      600_000_000_000, 30_000_000_000_000, 300_000_000_000_000];
    ulong[] permanentBudgets = [0, 0, 5, 20, 65, 65, 250, 700, 10_000];
    // Core fractures already reached at this stage's sustained damage per minute.
    int[] fractures = [0, 0, 1, 4, 5, 5, 6, 6, 9];
    ulong total = earnings[stage];
    // Every permanent point was earned on the prestige ladder, so the next one is as far off.
    var save = new GameSave { HasOpenedUpgrades = true, RedGems = total, RedGemsEarnedThisRun = total, PrestigePointsEarned = permanentBudgets[stage],
      CoreExtractions = permanentBudgets[stage] > 0 ? (ulong)stage - 1 : 0,
      CoreFractures = fractures[stage], ShellDamage = stage > 0 ? PlanetShell.Health : 0 };
    save.Upgrades["HB"] = 1;
    save.Upgrades["HU1"] = 1;
    if (stage == 0) { RecordHarvesterDiscoveries(save, upgrades); return save; }

    ulong upgradeBudget = total * 60 / 100;
    ulong remainder = FillBuild(upgrades.UpgradeButtons, save.Upgrades, upgradeBudget, max, clicking);
    save.RedGems -= upgradeBudget - remainder;
    save.PurpleGems = LearnTalents(upgrades, save, permanentBudgets[stage], max);

    // Purchase power cells at the real escalating gem price, then spend them on system talents.
    ulong pointBudget = save.Meta.ContainsKey(ShipSystems.UnlockTalent) ? total * (clicking ? 10UL : 20UL) / 100 : 0;
    while (AbilityPointProgression.GetPrice(save.AbilityPointsPurchased) is ulong price && price <= pointBudget)
    {
      pointBudget -= price;
      save.RedGems -= price;
      save.AbilityPointsPurchased++;
    }
    save.BlueGems = FillSystems(upgrades, save, save.AbilityPointsPurchased, max);
    if (max)
      save.AbilityPointsPurchased = Spent(upgrades.UpgradeButtonsAbilities, save.Abilities) + save.BlueGems;
    Equip(save);
    RecordHarvesterDiscoveries(save, upgrades);

    bool HasMeta(string name) => upgrades.UpgradeButtonsMeta.Values.Any(b =>
      b.Data.UpgradeDefinition.ShortName == name && save.Meta.ContainsKey(b.Data.ShortName));
    if (HasMeta("SYU"))
    {
      if (max) save.Modules.DiscoverAllModules();
      else
      {
        var random = new Random(1234 + stage);
        var pool = ModuleCatalog.InventoryOrder.Where(m => (int)ModuleCatalog.Rarities[(int)m] < Math.Max(1, stage - 2))
          .OrderBy(_ => random.Next()).Take(stage * 2 - 3);
        foreach (var module in pool) save.Modules.Owned.Add(module);
        save.Modules.StartSalvage(random);
      }
      var available = new Queue<ShipModule>(save.Modules.GetAvailableModules());
      for (int type = 0; type < ModuleCatalog.Types.Length; type++)
        for (int slot = 0; slot < ModuleCatalog.BaseSlotsPerType && available.Count > 0; slot++)
          save.Modules.Slots[type * ModuleCatalog.MaxSlotsPerType + slot] = available.Dequeue();
    }
    if (HasMeta("SGU"))
    {
      if (max)
        for (int i = 0; i < save.Signals.Counts.Length; i++)
        { save.Signals.Counts[i] = 1; save.Signals.ScansPurchased++; }
      else
      {
        // A focused player still takes useful alternatives from the three offered choices.
        SignalKind[][] builds =
        [ [SignalKind.Capacity, SignalKind.Speed, SignalKind.ReturnSpeed, SignalKind.Fuel],
          [SignalKind.AbilityCooldown, SignalKind.Speed, SignalKind.GemValue, SignalKind.Capacity],
          [SignalKind.SpawnCount, SignalKind.SpawnFrequency, SignalKind.GemValue, SignalKind.Capacity] ];
        SignalKind[] preferred = clicking
          ? [SignalKind.ClickValue, SignalKind.ClickRadius, SignalKind.ClickChainRange,
             SignalKind.HoldClickFrequency, SignalKind.ClickComboWindow, SignalKind.TractorBeltRadius,
             SignalKind.TractorBeltSpeed, SignalKind.TractorBeltDuration, SignalKind.TractorBeltCapacity]
          : builds[stage == 5 ? 1 : stage == 6 ? 2 : 0];
        var random = new Random(730 + stage + (clicking ? 100 : 0));
        var wallet = new GameState { CurrentRedGemCount = total * 10 / 100 };
        ulong before = wallet.CurrentRedGemCount;
        while (save.Signals.TryScan(wallet, random, id => preferred.Contains((SignalKind)id)
          || (clicking ? (SignalKind)id is SignalKind.GemValue or SignalKind.SpawnCount or SignalKind.SpawnFrequency
            : (SignalKind)id is SignalKind.CollectionRange or SignalKind.Refuel or SignalKind.Fuel)))
        {
          int choice = Enumerable.Range(0, 3).OrderBy(i =>
            Array.IndexOf(preferred, (SignalKind)save.Signals.PendingChoices[i].Signal) is int rank && rank >= 0 ? rank : 10).First();
          save.Signals.TryChoose(choice);
        }
        save.RedGems -= before - wallet.CurrentRedGemCount;
      }
    }
    FitPresetWeapons(save);
    return save;
  }

  public static int ProgressionStage(int preset) => preset switch
  {
    8 => 1, 9 => 4, 10 => 7, 11 => 8, _ => preset
  };

  private static bool IsClickStat(JsonUpgrade definition) => definition.PropertyName.StartsWith("Click", StringComparison.Ordinal)
    || definition.PropertyName.StartsWith("HoldClick", StringComparison.Ordinal)
    || definition.PropertyName.StartsWith("TractorBelt", StringComparison.Ordinal);

  // The order a player takes each tier's talents in: crits and the cannon's craters first, since
  // every run has them, then the fleet and the weapons and systems bought later in a run.
  // Unlisted talents come last, in layout order.
  private static readonly string[] TalentPriority =
  [
    "DEY1", "TR1", "CAT1", "TT1", "SLP1", "PRL1",
    "HSK1", "BR1", "LR1", "SYF1", "GVS1", "MCSN1",
    "CRM1", "SCR1", "MD1", "EP1", "KNB1", "KNH1",
    "IFF1", "AE1", "DG1", "FSD1", "MBR1", "RCH1", "KD1",
    "EXE1", "CHR1", "MRS1", "SR1", "CHT1",
    "OUR1", "SGR1", "RCO1", "OVC1",
  ];

  // Weapon talents a build takes only once the run has one of their weapons. Kamikaze Drones
  // needs none, but trades the drone swarm away, so builds leave it until nothing else is left.
  private static readonly Dictionary<string, MainShipWeapon[]> TalentWeapons = new()
  {
    ["PRL1"] = [MainShipWeapon.Laser], ["BR1"] = [MainShipWeapon.Laser], ["SYF1"] = [MainShipWeapon.Laser],
    ["LR1"] = [MainShipWeapon.Harpoon], ["GVS1"] = [MainShipWeapon.Harpoon], ["IOM1"] = [MainShipWeapon.Harpoon],
    ["MD1"] = [MainShipWeapon.Rockets, MainShipWeapon.Railgun],
    ["KNH1"] = [MainShipWeapon.Railgun], ["KNB1"] = [MainShipWeapon.Railgun],
    ["MBR1"] = [MainShipWeapon.Railgun], ["RCH1"] = [MainShipWeapon.Railgun],
    ["KD1"] = [],
  };

  // The free rewards that bring whole systems online; presets grant them for the tiers they reach.
  private static readonly string[] SystemUnlocks = [ShipSystems.UnlockTalent, "SYU1", "SGU1"];

  // Learn talents the way a player climbs the tree: each point goes to the deepest open tier
  // with a talent this build can use, so tiers open early without buying talents for weapons
  // the run lacks. Presets never take the talents that change how a run is played (Lone
  // Operator docks the fleet), and take the first side of every either/or choice. Returns the
  // points left unspent.
  private static ulong LearnTalents(Upgrades upgrades, GameSave save, ulong points, bool max)
  {
    var meta = save.Meta;
    var tiers = PrestigeTalentLayout.Tiers;
    var spent = new ulong[tiers.Length];
    var weapons = Weaponry.Tabs.Where(tab => save.Upgrades.GetValueOrDefault(tab.Root) > 0)
      .Select(tab => tab.Weapon).ToHashSet();
    bool Reached(int tier) => spent.Take(tier).Aggregate(0UL, (sum, tierSpent) => sum + tierSpent)
      >= (ulong)tiers[tier].RequiredEarlierPoints;
    bool Learnable(string id) => PrestigeTalentLayout.CompatibleTalents.Contains(id)
      && Array.IndexOf(PrestigeTalentLayout.PlaystyleTalents, id) < 0
      && upgrades.UpgradeButtonsMeta.TryGetValue(id, out var button)
      && meta.GetValueOrDefault(id) < Math.Min(button.Data.NumLevels, button.Data.LevelInfo.Count);
    bool Useful(string id) => !TalentWeapons.TryGetValue(id, out var needs) || needs.Any(weapons.Contains);
    int Priority(string id) => Array.IndexOf(TalentPriority, id) is int rank && rank >= 0 ? rank : int.MaxValue;
    while (true)
    {
      var (tier, id) = Enumerable.Range(0, tiers.Length).Where(Reached)
        .SelectMany(index => tiers[index].Talents.Where(Learnable).Select(talent => (index, talent)))
        .OrderBy(pick => !Useful(pick.talent)).ThenByDescending(pick => pick.index)
        .ThenBy(pick => Priority(pick.talent)).FirstOrDefault();
      if (id == null) break;
      ulong price = upgrades.UpgradeButtonsMeta[id].Data.LevelInfo[meta.GetValueOrDefault(id)].Cost;
      if (!max && price > points) break;
      if (!max) points -= price;
      meta[id] = meta.GetValueOrDefault(id) + 1;
      spent[tier] += price;
    }
    for (int tier = 0; tier < tiers.Length; tier++)
      if (Reached(tier))
        foreach (string id in tiers[tier].FreeRewards.Intersect(SystemUnlocks))
          meta[id] = 1;
    return points;
  }

  private static ulong FillBuild(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels,
    ulong budget, bool max, bool clicking)
  {
    if (!clicking) return Fill(buttons, levels, budget, max);
    var focus = new HashSet<string>();
    void Include(string id)
    {
      if (!focus.Add(id) || !buttons.TryGetValue(id, out var node)) return;
      if (!string.IsNullOrEmpty(node.Data.BlockedBy)) Include(node.Data.BlockedBy);
    }
    foreach (var (id, node) in buttons)
      if (IsClickStat(node.Data.UpgradeDefinition)) Include(id);
    // Reserve most of the tree's budget for manual collection, then develop supporting systems.
    ulong focusedBudget = budget * 75 / 100;
    ulong remaining = Fill(buttons, levels, focusedBudget, false, focus);
    return Fill(buttons, levels, budget - focusedBudget + remaining, false);
  }

  public static GameSave CreateFeature(int feature, Upgrades upgrades)
  {
    if (feature < 0 || feature >= FeatureNames.Length) throw new ArgumentOutOfRangeException(nameof(feature));
    var save = Create(4, upgrades);
    save.Signals = new();
    save.Modules = new();
    save.Abilities.Clear();
    save.EquippedAbilities.Clear();
    save.BlueGems = 0;
    save.AbilityPointsPurchased = 0;
    save.Meta.Clear();
    save.Upgrades.Clear();
    void Buy(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels, string id, int level = 1)
    {
      if (!buttons.TryGetValue(id, out var button)) return;
      if (!string.IsNullOrEmpty(button.Data.BlockedBy)) Buy(buttons, levels, button.Data.BlockedBy);
      levels[id] = Math.Max(levels.GetValueOrDefault(id), Math.Min(level, button.Data.NumLevels));
    }
    Buy(upgrades.UpgradeButtons, save.Upgrades, "HB");
    Buy(upgrades.UpgradeButtons, save.Upgrades, "CFR1", 3);
    Buy(upgrades.UpgradeButtons, save.Upgrades, "CFP1", 3);
    save.CoreExtractions = 1;
    save.CoreFractures = feature is 1 or 10 ? 4 : 0;
    if (feature is 0 or 1)
    {
      if (feature == 1)
      {
        // Give fully upgraded abilities a busy gem field without unlocking a fleet.
        foreach (string id in FullWeaponNodes)
          Buy(upgrades.UpgradeButtons, save.Upgrades, id, upgrades.UpgradeButtons[id].Data.NumLevels);
      }
      Buy(upgrades.UpgradeButtonsMeta, save.Meta, ShipSystems.UnlockTalent);
      FillSystems(upgrades, save, feature == 0 ? 8UL : 0, feature == 1);
      save.BlueGems = feature == 0 ? 3UL : 25;
      save.AbilityPointsPurchased = Spent(upgrades.UpgradeButtonsAbilities, save.Abilities) + save.BlueGems;
      Equip(save);
    }
    else if (feature >= 2 && feature <= 7)
    {
      string[] unlocks = ["HU1", "AHU1", "EHU1", "UHU1", "PHU1"];
      string[] counts = ["HC1", "AHFC1", "EHC1", "UHC1", "PHC1"];
      int type = feature == 7 ? 0 : feature - 2;
      Buy(upgrades.UpgradeButtons, save.Upgrades, unlocks[type]);
      Buy(upgrades.UpgradeButtons, save.Upgrades, counts[type], 2);
      // Feature sandboxes deliberately isolate one class, bypassing unlock prerequisites.
      foreach (string unlock in unlocks.Where(id => id != unlocks[type])) save.Upgrades.Remove(unlock);
      Buy(upgrades.UpgradeButtonsMeta, save.Meta, "SYU1");
      if (feature != 7)
      {
        save.Modules.DiscoverAllModules();
        save.Modules.Slots[type * ModuleCatalog.MaxSlotsPerType] = ShipModule.CargoPod;
        save.Modules.Slots[type * ModuleCatalog.MaxSlotsPerType + 1] = ShipModule.IonBooster;
      }
      else QueueDiscoveries(save.Modules);
    }
    else if (feature == 8)
    {
      Buy(upgrades.UpgradeButtonsMeta, save.Meta, "SGU1");
      var wallet = new GameState { CurrentRedGemCount = 1_000 };
      save.Signals.TryScan(wallet, new Random(42));
    }
    else
    {
      if (feature == 9)
        foreach (string id in new[] { "CVM1", "CLC1", "CR1", "CSC1", "CCB1", "TBE1" })
          Buy(upgrades.UpgradeButtons, save.Upgrades, id);
      else
        foreach (string id in FullWeaponNodes)
          Buy(upgrades.UpgradeButtons, save.Upgrades, id, upgrades.UpgradeButtons[id].Data.NumLevels);
    }
    save.HarvesterUnlockAchievements.Clear();
    RecordHarvesterDiscoveries(save, upgrades);
    if (feature == 10)
    {
      foreach (var module in WeaponSupportCatalog.All)
        for (int count = 0; count < 3; count++) save.Weapons.AwardSupport(module);
      save.Weapons.EquipSupport(WeaponSupportModule.Overcharger, 0);
      save.Weapons.EquipSupport(WeaponSupportModule.Accelerator, 3);
      save.Weapons.EquipSupport(WeaponSupportModule.Payload, 4);
    }
    FitPresetWeapons(save);
    return save;
  }

  public static void QueueDiscoveries(ShipyardModules modules)
  {
    modules.StartSalvage(new Random(42));
    foreach (var rarity in new[] { ModuleRarity.Common, ModuleRarity.Rare, ModuleRarity.Legendary })
    {
      var module = ModuleCatalog.InventoryOrder.FirstOrDefault(m => !modules.Owned.Contains(m)
        && ModuleCatalog.Rarities[(int)m] == rarity);
      if (module == ShipModule.None) continue;
      modules.Owned.Add(module);
      modules.PendingReveals.Add(module);
    }
    RepairDiscoveryTarget(modules);
  }

  public static void RepairDiscoveryTarget(ShipyardModules modules)
  {
    if (!modules.SalvageStarted) return;
    // Re-roll the next find if its last eligible module was just queued.
    if (modules.CollectionComplete)
    {
      modules.DiscoveryRarity = null;
      modules.DiscoveryProgressSeconds = 0;
      modules.DiscoveryThresholdSeconds = 0;
    }
    else if (!ModuleCatalog.InventoryOrder.Any(m => !modules.Owned.Contains(m)
      && ModuleCatalog.Rarities[(int)m] == modules.DiscoveryRarity))
    {
      modules.DiscoveryRarity = ModuleCatalog.Rarities[(int)ModuleCatalog.InventoryOrder.First(m => !modules.Owned.Contains(m))];
      modules.DiscoveryThresholdSeconds = ShipyardModules.MinimumDiscoverySeconds(modules.DiscoveryRarity.Value);
      modules.DiscoveryProgressSeconds = 0;
    }
  }

  private static ulong Spent(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels)
    => levels.Aggregate(0UL, (total, pair) => total + buttons[pair.Key].Data.LevelInfo.Take(pair.Value)
      .Aggregate(0UL, (sum, info) => sum + info.Cost));

  // Bring every ship system online before deepening any of them, as a player would.
  private static ulong FillSystems(Upgrades upgrades, GameSave save, ulong cells, bool max)
  {
    bool kamikaze = Kamikaze(save);
    foreach (var tab in ShipSystems.Tabs.Where((_, index) => ShipSystems.IsTabAvailable(index, kamikaze)))
      if (upgrades.UpgradeButtonsAbilities.TryGetValue(tab.Root, out var root) && !save.Abilities.ContainsKey(tab.Root)
        && (max || root.Data.LevelInfo[0].Cost <= cells))
      {
        if (!max) cells -= root.Data.LevelInfo[0].Cost;
        save.Abilities[tab.Root] = 1;
      }
    return Fill(upgrades.UpgradeButtonsAbilities, save.Abilities, cells, max, kamikazeDrones: kamikaze);
  }

  // Presets stage an equipped ship explicitly; ordinary blueprint purchases stay in inventory.
  private static void FitPresetWeapons(GameSave save)
  {
    foreach (var tab in Weaponry.Tabs)
      if (tab.Weapon != MainShipWeapon.Cannon && save.Upgrades.GetValueOrDefault(tab.Root) > 0)
        save.Weapons.TryAutoFit(tab.Weapon);
  }

  private static bool Kamikaze(GameSave save) => save.Meta.GetValueOrDefault(PrestigeTalentEffects.KamikazeDronesTalent) > 0;

  private static void Equip(GameSave save)
    => save.EquippedAbilities = new[] { "GS1", ShipSystems.DroneSystemRoot(Kamikaze(save)), "CM1" }
      .Where(save.Abilities.ContainsKey).ToList();

  private static void RecordHarvesterDiscoveries(GameSave save, Upgrades upgrades)
  {
    foreach (var (id, level) in save.Upgrades)
      if (level > 0 && upgrades.UpgradeButtons.TryGetValue(id, out var button)
        && UpgradeManager.GetHarvesterUnlockAchievementId(button.Data.UpgradeDefinition.ShortName) is { } achievement)
        save.HarvesterUnlockAchievements.Add(achievement);
  }

  private static ulong Fill(Dictionary<string, UpgradeButton> buttons, Dictionary<string, int> levels,
    ulong budget, bool max, ISet<string> allowed = null, bool kamikazeDrones = false)
  {
    // Buy one level at a time, cheapest first, resolving prerequisites after each purchase.
    while (true)
    {
      var next = buttons.Where(pair => allowed == null || allowed.Contains(pair.Key))
        .Where(pair => !ShipSystems.IsInTree(pair.Key)
          || ShipSystems.CanLearn(buttons, pair.Key, id => levels.GetValueOrDefault(id), kamikazeDrones))
        // Core Shards come from run objectives, which the game pays out when the preset loads.
        .Where(pair => max || pair.Value.Data.UpgradeDefinition.Currency != CoreShards.Currency)
        .Where(pair => string.IsNullOrEmpty(pair.Value.Data.BlockedBy)
          || levels.ContainsKey(pair.Value.Data.BlockedBy))
        .Where(pair => levels.GetValueOrDefault(pair.Key) < Math.Min(pair.Value.Data.NumLevels, pair.Value.Data.LevelInfo.Count))
        .OrderBy(pair => pair.Value.Data.LevelInfo[levels.GetValueOrDefault(pair.Key)].Cost)
        .ThenBy(pair => pair.Key, StringComparer.Ordinal).FirstOrDefault();
      if (next.Value == null) break;
      ulong price = next.Value.Data.LevelInfo[levels.GetValueOrDefault(next.Key)].Cost;
      if (!max && price > budget) break;
      if (!max) budget -= price;
      levels[next.Key] = levels.GetValueOrDefault(next.Key) + 1;
    }
    return budget;
  }
}
