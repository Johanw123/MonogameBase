using UntitledGemGame;
using UntitledGemGame.Entities;

internal static class GemQualityChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    var oldManager = UpgradeManager.Instance;
    var oldTree = UpgradeManager.CurrentUpgrades;
    try
    {
      var tree = new Upgrades();
      tree.LoadJson(File.ReadAllText("Content/Data/upgrades.json"),
        File.ReadAllText("Content/Data/upgrades_buttons.json"), tree.UpgradeButtons, tree.UpgradeDefinitions);
      UpgradeManager.CurrentUpgrades = tree;
      var buttons = tree.UpgradeButtons;
      var colors = new Dictionary<string, GemTypes>
      {
        ["ULG1"] = GemTypes.LightGreen, ["UBL1"] = GemTypes.Blue,
        ["UTE1"] = GemTypes.Teal, ["ULI1"] = GemTypes.Lilac,
        ["UPU1"] = GemTypes.Purple, ["UGO1"] = GemTypes.Gold, ["UDB1"] = GemTypes.DarkBlue
      };
      var qualityNodes = buttons.Values.Where(b => b.Data.UpgradeDefinition.ShortName == "GSQ").ToArray();
      Check(qualityNodes.Sum(b => b.Data.NumLevels) == 10 && GemQualityTable.Levels.Length == 11,
        "Ten purchasable quality ranks must each have a distinct probability row");
      foreach (var row in GemQualityTable.Levels)
      {
        Check(Math.Abs(row.Sum(e => e.ChancePercent) - 100) < .001,
          "Every quality distribution must total 100 percent");
        Check(row.All(e => e.ChancePercent > 0) && row.Select(e => e.Type).Distinct().Count() == row.Length,
          "Quality entries must have positive probabilities and unique colors");
      }

      // Walk actual purchase dependencies, which only require one parent rank.
      (int Quality, HashSet<GemTypes> Colors) Before(string id)
      {
        int quality = 1;
        var unlocked = new HashSet<GemTypes> { GemTypes.Red };
        var seen = new HashSet<string> { id };
        string parent = buttons[id].Data.BlockedBy;
        while (!string.IsNullOrEmpty(parent))
        {
          Check(seen.Add(parent) && buttons.ContainsKey(parent), "Gem branches must reach a root without cycles");
          var node = buttons[parent];
          if (colors.TryGetValue(parent, out var color)) unlocked.Add(color);
          if (node.Data.UpgradeDefinition.ShortName == "GSQ")
            quality += node.Data.LevelInfo[0].m_upgradeAmountInt;
          parent = node.Data.BlockedBy;
        }
        return (quality, unlocked);
      }
      double ExpectedValue(int quality, HashSet<GemTypes> unlocked)
        => GemQualityTable.Levels[quality - 1].Sum(e => e.ChancePercent / 100.0
          * (unlocked.Contains(e.Type) ? e.ValueMultiplier : 1));

      foreach (var (id, color) in colors)
      {
        var before = Before(id);
        Check(GemQualityTable.Levels[before.Quality - 1].Any(e => e.Type == color && e.ChancePercent > 0),
          $"{id} must be able to spawn immediately, even when only minimum prerequisites were bought");
        double oldValue = ExpectedValue(before.Quality, before.Colors);
        before.Colors.Add(color);
        Check(ExpectedValue(before.Quality, before.Colors) > oldValue, "Every color unlock must immediately improve spawn value");
      }
      foreach (var node in qualityNodes)
      {
        var before = Before(node.Data.ShortName);
        int quality = before.Quality;
        foreach (var level in node.Data.LevelInfo)
        {
          int next = quality + level.m_upgradeAmountInt;
          Check(ExpectedValue(next, before.Colors) > ExpectedValue(quality, before.Colors),
            "Every quality rank must improve already unlocked gems before the next color is purchased");
          quality = next;
        }
      }

      // Restore exactly the purchased ranks without inferring extra upgrades.
      var partial = new GameSave { Upgrades = new() { ["GSQ1"] = 1 } };
      var partialManager = new UpgradeManager();
      partialManager.RestoreProgress(partial);
      Check(partialManager.UG.GemSpawnQuality == 2, "One purchased quality rank must restore exactly once");
      var captured = new GameSave();
      partialManager.CaptureProgress(captured);
      partialManager = new UpgradeManager();
      partialManager.RestoreProgress(captured);
      Check(partialManager.UG.GemSpawnQuality == 2 && captured.Upgrades.Count == 1,
        "Save round trips must not infer additional quality purchases");
      var completed = new GameSave { Upgrades = qualityNodes.ToDictionary(b => b.Data.ShortName, b => b.Data.NumLevels) };
      foreach (var id in colors.Keys) completed.Upgrades[id] = 1;
      var completeManager = new UpgradeManager();
      completeManager.RestoreProgress(completed);
      Check(completeManager.UG.GemSpawnQuality == 11, "All ten ranks must survive save restoration");
    }
    finally
    {
      UpgradeManager.Instance = oldManager;
      UpgradeManager.CurrentUpgrades = oldTree;
    }
    Console.WriteLine("Gem quality checks passed: ten ranks, immediate color chances, useful quality purchases and current save restoration.");
  }
}
