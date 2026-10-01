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
    if (state.PeakGemsPerMinute != 0 || !state.TryRefundAbilityPoints(1))
      throw new Exception("Prestige resets peak income");
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
    CheckLayout(buttons);
    var treeSizes = new[] { "Drones1", "CM1", "GS1" }.Select(root =>
    {
      var talents = buttons.Values.Where(b => Field(b, "hiddenby") == root).ToArray();
      Check(talents.Count(b => Field(b, "blockedby") == root) == 3, "Every ability needs three main arms");
      return (Nodes: talents.Length, Cost: talents.Sum(b => b.GetProperty("cost").EnumerateArray()
        .Sum(c => int.Parse(c.GetString()!))));
    }).ToArray();
    Check(treeSizes.Max(t => t.Nodes) - treeSizes.Min(t => t.Nodes) <= 3
      && treeSizes.Max(t => t.Cost) <= treeSizes.Min(t => t.Cost) * 1.15,
      "Ability trees must offer similar node counts and total point costs");
    foreach (var (midpoint, finisher, root) in new[]
    {
      ("CMCR1", "CMAvalanche1", "CM1"), ("CMA1", "CMSC1", "CM1"),
      ("CMConstellation1", "CMHorizon1", "CM1"), ("GSSpiral1", "GSCosmic1", "GS1"),
      ("GSBloom1", "GSWorldseed1", "GS1"), ("GSMidas1", "GSGoldenAge1", "GS1")
    })
    {
      var path = new List<string>();
      string id = finisher;
      while (id != root) { path.Add(id); id = Field(buttons[id], "blockedby"); }
      int index = path.IndexOf(midpoint);
      Check(index >= path.Count * 0.3 && index <= path.Count * 0.7,
        "Every arm needs a mechanic near its midpoint and a finisher at its end");
      Check(!buttons.Values.Any(b => Field(b, "blockedby") == finisher), "Cascade and Genesis finishers must end their arm");
    }
    var droneStarts = buttons.Values.Where(b => Field(b, "blockedby") == "Drones1").ToArray();
    Check(droneStarts.Length == 3, "Drones must have exactly three main routes");
    string DroneRoute(JsonElement node)
    {
      while (Field(node, "blockedby") != "Drones1") node = buttons[Field(node, "blockedby")];
      return Field(node, "shortname");
    }
    foreach (var (midpoint, capstone) in new[]
    {
      ("DroneAfterburners1", "DroneRelay1"), ("DroneR1", "DroneOvercharge1"),
      ("DroneFinalSweep1", "DroneLightning1")
    })
    {
      string route = DroneRoute(buttons[capstone]);
      var talents = buttons.Values.Where(b => Field(b, "hiddenby") == "Drones1" && DroneRoute(b) == route).ToArray();
      Check(talents.Count(b => Field(b, "upgrade") == "DroneCapacity") == 2
        && talents.Count(b => Field(b, "upgrade") == "DroneDeliveryValue") == 2,
        "Cargo and delivery value must be mixed evenly into each drone route");
      var ancestor = buttons[capstone];
      int distance = 0;
      while (Field(ancestor, "shortname") != midpoint && Field(ancestor, "shortname") != "Drones1")
      {
        ancestor = buttons[Field(ancestor, "blockedby")];
        distance++;
      }
      Check(Field(ancestor, "shortname") == midpoint && distance >= 3,
        "Every drone finisher must build on its midpoint talent through several upgrades");
      Check(buttons[capstone].GetProperty("cost")[0].GetString() == "5"
        && buttons[capstone].GetProperty("value")[0].GetString() == "true",
        "Drone finishers must be five-point unlocks");
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
      ("CMRC", 30, "30%"), ("DSE", 75, "75%"), ("GSRV", 15, "15%"),
      ("CMAC", 15, "15%"), ("CMAC", 25, "25%"),
      ("GSRR", 50, "50%"), ("GSRR", 0, "0%"),
      ("DroneSpeed", 1, "0%"), ("DroneSpeed", 1.3, "+30%"),
      ("DroneCollectionRange", 1, "0%"), ("DroneCollectionRange", 1.45, "+45%"),
      ("IDF", 1.2, "+20%"),
      ("DroneDeliveryValue", 1, "0%"), ("DroneDeliveryValue", 1.25, "+25%"),
      ("CMVelocity", 1.5, "+50%"), ("CMNetReach", 1.3, "+30%")
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
    Console.WriteLine("Ability tree checks passed: branching, reachability, definitions, ranks and ring yield.");
  }

  private static void CheckLayout(Dictionary<string, JsonElement> buttons)
  {
    var nodes = buttons.Select(pair =>
    {
      var b = pair.Value;
      float x = float.Parse(b.GetProperty("posx").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
      float y = float.Parse(b.GetProperty("posy").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
      float size = 50 * float.Parse(b.GetProperty("buttonsizescale").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
      if (bool.Parse(b.GetProperty("addmidpoint").GetString()!))
        throw new Exception("Constellation links must be straight: " + pair.Key);
      return (Id: pair.Key, Parent: b.GetProperty("blockedby").GetString()!, X: x, Y: y, Size: size,
        Center: new System.Numerics.Vector2(x + size / 2, y + size / 2));
    }).ToArray();
    var byId = nodes.ToDictionary(n => n.Id);
    var edges = nodes.Where(n => n.Parent.Length > 0)
      .Select(n => (Start: byId[n.Parent], End: n)).ToArray();

    for (int i = 0; i < nodes.Length; i++)
      for (int j = i + 1; j < nodes.Length; j++)
      {
        var a = nodes[i];
        var b = nodes[j];
        if (a.X < b.X + b.Size + 16 && b.X < a.X + a.Size + 16
          && a.Y < b.Y + b.Size + 16 && b.Y < a.Y + a.Size + 16)
          throw new Exception($"Ability nodes need clearance: {a.Id}, {b.Id}");
      }

    static float Cross(System.Numerics.Vector2 a, System.Numerics.Vector2 b, System.Numerics.Vector2 c)
      => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    static bool Intersects(System.Numerics.Vector2 a, System.Numerics.Vector2 b,
      System.Numerics.Vector2 c, System.Numerics.Vector2 d)
      => Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0
        && Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <= Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X))
        && Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <= Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));

    for (int i = 0; i < edges.Length; i++)
    {
      var edge = edges[i];
      for (int j = i + 1; j < edges.Length; j++)
      {
        var other = edges[j];
        if (edge.Start.Id == other.Start.Id || edge.Start.Id == other.End.Id
          || edge.End.Id == other.Start.Id || edge.End.Id == other.End.Id) continue;
        if (Intersects(edge.Start.Center, edge.End.Center, other.Start.Center, other.End.Center))
          throw new Exception($"Ability links cross: {edge.End.Id}, {other.End.Id}");
      }
      foreach (var node in nodes)
      {
        if (node.Id == edge.Start.Id || node.Id == edge.End.Id) continue;
        var corners = new[]
        {
          new System.Numerics.Vector2(node.X - 10, node.Y - 10),
          new System.Numerics.Vector2(node.X + node.Size + 10, node.Y - 10),
          new System.Numerics.Vector2(node.X + node.Size + 10, node.Y + node.Size + 10),
          new System.Numerics.Vector2(node.X - 10, node.Y + node.Size + 10)
        };
        for (int k = 0; k < corners.Length; k++)
          if (Intersects(edge.Start.Center, edge.End.Center, corners[k], corners[(k + 1) % corners.Length]))
            throw new Exception($"Ability link runs through a node: {edge.End.Id}, {node.Id}");
      }
    }
  }
}
