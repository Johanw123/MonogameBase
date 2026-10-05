using System.Text.Json;
using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class AbilityTreeChecks
{
  public static void Run()
  {
    var state = new GameState();
    state.Restore(1000, 2, 0, 5000, 7);
    state.Restore(1000, 2, 0, 5000, 7, 1000);
    if (state.PeakGemsPerMinute != 1000) throw new Exception("Restore peak income");
    state.RecordIncome(1000);
    state.RecordIncome(100);
    if (!state.TryRefundAbilityPoints(3) || state.CurrentRedGemCount != 1000
      || state.CurrentBlueGemCount != 5 || state.AbilityPointsPurchased != 7
      || state.RedGemsEarnedThisRun != 5000) throw new Exception("Refund accounting");
    state.CurrentRedGemCount = 0;
    if (!state.TryRefundAbilityPoints(8) || state.CurrentRedGemCount != 0
      || state.CurrentBlueGemCount != 13) throw new Exception("Refunds must work with no gems");
    if (state.TryRefundAbilityPoints(0)) throw new Exception("Empty refund");
    state.RecordIncome(double.MaxValue);
    if (state.TryRefundAbilityPoints(ulong.MaxValue) || state.CurrentBlueGemCount != 13)
      throw new Exception("Overflow refunds must be atomic");
    state.CompletePrestige(1);
    if (state.PeakGemsPerMinute != 0 || state.CurrentBlueGemCount != 0 || state.AbilityPointsPurchased != 0
      || state.NextAbilityPointPrice != AbilityPointProgression.GetPrice(0))
      throw new Exception("Extracting the core must reset peak income and power cells");
    if (!state.TryRefundAbilityPoints(1)) throw new Exception("Refunds must work after an extraction");
    using var document = JsonDocument.Parse(File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"));
    using var definitions = JsonDocument.Parse(File.ReadAllText("Content/Data/upgrades_abilities.json"));
    var upgrades = definitions.RootElement.GetProperty("upgrades").EnumerateArray()
      .ToDictionary(u => u.GetProperty("shortname").GetString()!);
    var buttons = document.RootElement.GetProperty("buttons").EnumerateArray()
      .ToDictionary(b => b.GetProperty("shortname").GetString()!);
    string Field(JsonElement b, string key) => b.GetProperty(key).GetString()!;
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    CheckGrid(buttons, upgrades);
    CheckRules();
    CheckKamikazeWing();
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 50) == 12, "Base rings must halve yield");
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 40) == 15, "Ring upgrades must improve yield");
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 0) == 25, "Full rings must retain yield");
    foreach (var (id, value, expected) in new (string, double, string)[]
    {
      ("CMRC", 30, "30%"), ("DSE", 75, "75%"), ("GSRV", 15, "15%"),
      ("CMAC", 15, "15%"), ("CMAC", 25, "25%"),
      ("GSRR", 50, "50%"), ("GSRR", 0, "0%"),
      ("DroneSpeed", 1, "0%"), ("DroneSpeed", 1.3, "+30%"),
      ("DroneCollectionRange", 1, "0%"), ("DroneCollectionRange", 1.45, "+45%"),
      ("IDF", 1.2, "+20%"),
      ("DroneDeliveryValue", 1, "0%"), ("DroneDeliveryValue", 1.25, "+25%"),
      ("CMVelocity", 1.5, "+50%"), ("CMNetReach", 1.3, "+30%"),
      ("KWD", 1.5, "+50%"), ("KWSP", 1.25, "+25%"), ("KWXC", 40, "40%")
    })
    {
      var definition = new JsonUpgrade { ShortName = id, BaseValue = Field(upgrades[id], "base") };
      Check(UpgradeValueFormatter.Format(definition, value, true) == expected,
        id + " must display its percentage in the correct units");
      Check(buttons.Values.Where(b => Field(b, "upgrade") == id)
        .All(b => bool.Parse(Field(b, "tooltippercentage"))), id + " nodes must enable percentage display");
    }
    foreach (int percent in new[] { 5, 10, 25, 30, 75 })
    {
      uint total = 0;
      for (int roll = 0; roll < 100; roll++) total += AbilityGemValue.AddBonus(1, percent, roll);
      Check(total == 100 + percent, "Fractional bonuses must preserve their expected value");
    }
    Check(AbilityGemValue.AddBonus(uint.MaxValue, 100, 0) == uint.MaxValue,
      "Value bonuses must saturate instead of overflowing");
    var manager = new UpgradeManager();
    var spawn = new GemSpawnData { BaseValue = 40, IsLucky = true };
    Check(GemSpawnerAbility.ApplyRichVeins(spawn).BaseValue == 40, "Unpurchased Rich Veins must do nothing");
    manager.UGA.GemSpawnerRichVeins = 100;
    var rich = GemSpawnerAbility.ApplyRichVeins(spawn);
    Check(rich.BaseValue == 80 && rich.IsLucky, "Rich Veins must double existing value and highlight gems");
    foreach (string property in new[] { "CMRC", "DSE", "GSRV" })
      manager.UGA.Reset(property);
    Check(manager.UGA.GemSpawnerRichVeins == 0, "Ability refunds must clear Rich Veins");
    Console.WriteLine("Ship system checks passed: tabs, columns, tiers, capstones, refunds, definitions, ranks and ring yield.");
  }

  // Kamikaze Drones swaps Drone Swarm for the Kamikaze Wing, and the wing's talents shape its blasts.
  private static void CheckKamikazeWing()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var previous = UpgradeManager.Instance;
    try
    {
      var manager = new UpgradeManager();
      var definitions = new Upgrades();
      definitions.LoadJson(File.ReadAllText("Content/Data/upgrades_abilities.json"),
        File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"),
        definitions.UpgradeButtonsAbilities, definitions.UpgradeDefinitionsAbilities);
      var buttons = definitions.UpgradeButtonsAbilities;
      Func<string, int> none = _ => 0;
      Check(ShipSystems.CanLearn(buttons, "Drones1", none) && !ShipSystems.CanLearn(buttons, "KW1", none),
        "Without Kamikaze Drones only Drone Swarm can be learned");
      manager.UGM.KamikazeDrones = true;
      Check(!ShipSystems.CanLearn(buttons, "Drones1", none) && ShipSystems.CanLearn(buttons, "KW1", none)
        && !ShipSystems.CanLearn(buttons, "KWS1", none) && ShipSystems.CanLearn(buttons, "KWS1", id => id == "KW1" ? 1 : 0),
        "With Kamikaze Drones the Kamikaze Wing replaces Drone Swarm, with its own tiers");
      Check(!ShipSystems.CanLearn(buttons, "KW1", none, kamikazeDrones: false)
        && ShipSystems.CanLearn(buttons, "Drones1", none, kamikazeDrones: false),
        "Presets plan with their own talents, not the live ones");
      var wing = manager.UGA;
      Check(KamikazeWing.Bombers() == 3 && KamikazeWing.Damage(wing, 5) == 60 && KamikazeWing.Depth(wing) == 1
        && KamikazeWing.DiveSeconds(wing) == KamikazeWing.FlightSeconds,
        "A fresh wing launches three bombers of twelve times fire power, one layer deeper");
      Check(KamikazeWing.CriticalChance(wing) == 0 && KamikazeWing.Bomblets(wing) == 0 && KamikazeWing.SortieChance(wing) == 0,
        "Locked wing mechanics do nothing");
      wing.KamikazeWingDamage = 2f;
      wing.KamikazeWingSpeed = 2f;
      wing.KamikazeWingVolatile = wing.KamikazeWingBomblets = wing.KamikazeWingSortie = true;
      wing.KamikazeWingSortieChance = 500;
      Check(KamikazeWing.Damage(wing, 5) == 120 && Math.Abs(KamikazeWing.CriticalChance(wing) - 0.2f) < 1e-6
        && KamikazeWing.Bomblets(wing) == 3 && Math.Abs(KamikazeWing.SortieChance(wing) - 0.9f) < 1e-6
        && Math.Abs(KamikazeWing.DiveSeconds(wing) - KamikazeWing.FlightSeconds / 2f) < 1e-6,
        "Wing talents raise damage, open criticals and bomblets, cap sorties below a loop and speed the dive");
      Check(KamikazeWing.Damage(wing, int.MaxValue) == int.MaxValue, "Warheads saturate instead of overflowing");
    }
    finally
    {
      UpgradeManager.Instance = previous;
    }
  }

  // Every talent sits in a Ship Systems tab: a core, three columns that chain down from it,
  // and tiers that need cells spent above them, ending in three capstones to choose from.
  private static void CheckGrid(Dictionary<string, JsonElement> buttons, Dictionary<string, JsonElement> upgrades)
  {
    string Field(JsonElement b, string key) => b.GetProperty(key).GetString()!;
    string First(JsonElement b, string key) => b.GetProperty(key)[0].GetString()!;
    int Cost(string id) => buttons[id].GetProperty("cost").EnumerateArray().Sum(c => int.Parse(c.GetString()!));
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    Check(buttons.Keys.All(ShipSystems.IsInTree), "Every system talent needs a place in a tab");
    var placed = ShipSystems.Tabs.SelectMany(t => t.Rows.SelectMany(r => r)).Where(id => id != "").ToArray();
    Check(placed.Length == placed.Distinct().Count() && placed.All(buttons.ContainsKey), "Tabs must place each talent once");
    Check(ShipSystems.RowRequirements.Zip(ShipSystems.RowRequirements.Skip(1)).All(p => p.First < p.Second),
      "Tier requirements must rise");
    var totals = new List<int>();
    foreach (var tab in ShipSystems.Tabs)
    {
      var talents = tab.Rows.SelectMany(r => r).Where(id => id != "").ToArray();
      Check(tab.Rows.All(row => row.Length == 3), tab.Name + " rows need three columns");
      Check(tab.Rows[0].Count(id => id != "") == 1 && tab.Rows[0][1] == tab.Root && Field(buttons[tab.Root], "blockedby") == "",
        tab.Name + " needs one core talent in the middle, learnable straight away");
      Check(tab.Rows[2].All(id => id != "" && First(buttons[id], "value") == "true"),
        tab.Name + " needs a mechanic opening each path");
      Check(tab.Rows[5].All(id => id != "" && First(buttons[id], "value") == "true" && First(buttons[id], "cost") == "5"),
        tab.Name + " needs three five-cell capstones to choose between");
      for (int column = 0; column < 3; column++)
      {
        string above = tab.Root;
        for (int row = 1; row < tab.Rows.Length; row++)
        {
          string id = tab.Rows[row][column];
          if (id == "") continue;
          Check(Field(buttons[id], "blockedby") == above, $"{id} must require the talent above it ({above})");
          above = id;
        }
      }
      for (int row = 1; row < tab.Rows.Length; row++)
        Check(tab.Rows.Take(row).SelectMany(r => r).Where(id => id != "").Sum(Cost) >= ShipSystems.RowRequirement(row),
          $"{tab.Name} tier {row} must be reachable by spending above it");
      foreach (string id in talents)
      {
        var button = buttons[id];
        Check(upgrades.ContainsKey(Field(button, "upgrade")), "Every talent must have a runtime definition");
        Check(button.GetProperty("value").GetArrayLength() == int.Parse(Field(button, "numlevels")), "Talent ranks must have values");
        Check(button.GetProperty("cost").GetArrayLength() == int.Parse(Field(button, "numlevels")), "Talent ranks must have costs");
      }
      totals.Add(talents.Sum(Cost));
    }
    Check(totals.Max() <= totals.Min() * 1.15, "Systems must cost similar totals to complete");
  }

  private static void CheckRules()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var tree = new Upgrades();
    tree.LoadJson(File.ReadAllText("Content/Data/upgrades_abilities.json"),
      File.ReadAllText("Content/Data/upgrades_abilities_buttons.json"), tree.UpgradeButtonsAbilities, tree.UpgradeDefinitionsAbilities);
    var buttons = tree.UpgradeButtonsAbilities;
    ShipSystems.ApplyLayout(buttons);
    Check(buttons["CM1"].Data.PosX - buttons["Drones1"].Data.PosX == ShipSystems.TabStride
      && buttons["CM1"].Data.PosY == buttons["Drones1"].Data.PosY, "Tabs must sit side by side in tree space");

    Check(ShipSystems.CanLearn(buttons, "Drones1") && !ShipSystems.CanLearn(buttons, "DroneSpeed1"),
      "Talents need their system online first");
    buttons["Drones1"].CurrentLevel = 1;
    Check(ShipSystems.CanLearn(buttons, "DroneSpeed1") && !ShipSystems.CanLearn(buttons, "DroneAfterburners1"),
      "The second tier needs cells spent above it");
    buttons["DroneSpeed1"].CurrentLevel = 3;
    Check(ShipSystems.CanLearn(buttons, "DroneAfterburners1") && !ShipSystems.CanLearn(buttons, "DroneFinalSweep1"),
      "An open tier still needs the talent above in the same column");
    buttons["CM1"].CurrentLevel = 1;
    buttons["CMNC1"].CurrentLevel = 3;
    Check(ShipSystems.Spent(buttons, 0) == 4 && ShipSystems.Spent(buttons, 1) == 4,
      "Cells only count toward their own system");
    buttons["DroneAfterburners1"].CurrentLevel = 1;
    Check(!ShipSystems.CanRefund(buttons, "DroneSpeed1") && !ShipSystems.CanRefund(buttons, "Drones1")
      && ShipSystems.CanRefund(buttons, "DroneAfterburners1") && buttons["DroneSpeed1"].CurrentLevel == 3,
      "Refunds must not strand learned talents, and checking must not change levels");
    Check(!ShipSystems.CanLearn(buttons, "DroneSpeed1", id => 0)
      && ShipSystems.CanLearn(buttons, "Drones1", id => 0), "Planned levels must drive the rules");
  }
}
