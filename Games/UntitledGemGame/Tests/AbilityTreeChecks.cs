using System.Text.Json;
using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class AbilityTreeChecks
{
  public static void Run()
  {
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
    foreach (var root in new[] { "Drones1", "CM1", "GS1" })
    {
      var branch = buttons.Values.Where(b => Field(b, "hiddenby") == root).ToArray();
      Check(branch.Count(b => Field(b, "blockedby") == root) >= 3, root + " must offer distinct starting routes");
      foreach (var button in branch)
      {
        Check(upgrades.ContainsKey(Field(button, "upgrade")), "Every talent must have a runtime definition");
        Check(button.GetProperty("value").GetArrayLength() == int.Parse(Field(button, "numlevels")), "Talent ranks must have values");
        Check(button.GetProperty("cost").GetArrayLength() == int.Parse(Field(button, "numlevels")), "Talent ranks must have costs");
        var seen = new HashSet<string>();
        var current = button;
        while (Field(current, "shortname") != root)
        {
          Check(seen.Add(Field(current, "shortname")), "Talent prerequisites must not cycle");
          Check(buttons.TryGetValue(Field(current, "blockedby"), out current), "Every route must reach its ability unlock");
        }
      }
    }
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 50) == 12, "Base rings must halve yield");
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 40) == 15, "Ring upgrades must improve yield");
    Check(GemSpawnerAbility.GetNextRingGemCount(25, 0) == 25, "Full rings must retain yield");
    foreach (var (id, value, expected) in new (string, double, string)[]
    {
      ("CMAC", 15, "15%"), ("CMAC", 25, "25%"),
      ("GSRR", 50, "50%"), ("GSRR", 0, "0%"),
      ("DroneSpeed", 1, "0%"), ("DroneSpeed", 1.3, "+30%"),
      ("IDF", 1.2, "+20%"), ("HDTD", 450, "-10%"), ("HDTD", 350, "-30%")
    })
    {
      var definition = new JsonUpgrade { ShortName = id, BaseValue = Field(upgrades[id], "base") };
      Check(UpgradeValueFormatter.Format(definition, value, true) == expected,
        id + " must display its percentage in the correct units");
      Check(buttons.Values.Where(b => Field(b, "upgrade") == id)
        .All(b => bool.Parse(Field(b, "tooltippercentage"))), id + " nodes must enable percentage display");
    }
    Console.WriteLine("Ability tree checks passed: branching, reachability, definitions, ranks and ring yield.");
  }
}
