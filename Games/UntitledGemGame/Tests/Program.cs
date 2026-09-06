using UntitledGemGame;

int checks = 0;
void Check(bool condition, string message)
{
  if (!condition) throw new Exception(message);
  checks++;
}

string directory = Path.Combine(Path.GetTempPath(), "gem-save-checks-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
  string path = Path.Combine(directory, "progress.json");
  var store = new GameSaveStore(path);
  Check(store.Load() == null && store.CanSave, "First launch should allow a new save");
  var original = new GameSave
  {
    Upgrades = new() { ["HB"] = 1, ["GQ1"] = 2 },
    Abilities = new() { ["GS1"] = 1 },
    Meta = new() { ["RH1"] = 1 },
    RedGems = ulong.MaxValue - 17,
    BlueGems = 3,
    PurpleGems = 42,
    RedGemsEarnedThisRun = ulong.MaxValue,
    EquippedAbilities = new() { "GS1", "", "Drones1" },
    PostPrestige = true,
    CreatedInitialGems = false
  };
  Check(store.Save(original), "First save failed");
  var loaded = new GameSaveStore(path).Load();
  Check(loaded.Upgrades["GQ1"] == 2 && loaded.Abilities["GS1"] == 1 && loaded.Meta["RH1"] == 1,
    "All three trees must round-trip");
  Check(loaded.RedGems == original.RedGems && loaded.BlueGems == 3 && loaded.PurpleGems == 42
    && loaded.RedGemsEarnedThisRun == ulong.MaxValue, "Currency and earnings must retain 64-bit precision");
  Check(loaded.PostPrestige && !loaded.CreatedInitialGems
    && loaded.EquippedAbilities.SequenceEqual(original.EquippedAbilities), "Run state and slot order must round-trip");

  original.RedGems = 123;
  Check(store.Save(original), "Second save failed");
  Check(new GameSaveStore(path + ".bak").Load().RedGems == ulong.MaxValue - 17, "Backup must contain previous complete save");
  File.WriteAllText(path + ".tmp", "partial interrupted write");
  Check(new GameSaveStore(path).Load().RedGems == 123, "Interrupted temp write must not affect primary save");
  File.WriteAllText(path, "{broken");
  var recovered = new GameSaveStore(path);
  Check(recovered.Load().RedGems == ulong.MaxValue - 17, "Corrupt primary must recover backup");
  Check(recovered.Save(original), "Saving after backup recovery failed");
  Check(new GameSaveStore(path + ".bak").Load().RedGems == ulong.MaxValue - 17, "Recovery must not replace backup with corrupt primary");

  string futurePath = Path.Combine(directory, "future.json");
  var futureStore = new GameSaveStore(futurePath);
  original.Version = 99;
  futureStore.Save(original);
  string futureContents = File.ReadAllText(futurePath);
  futureStore = new GameSaveStore(futurePath);
  Check(futureStore.Load() == null && !futureStore.CanSave && !futureStore.Save(new()), "Unknown versions must not be overwritten");
  Check(File.ReadAllText(futurePath) == futureContents, "Future save was changed");
  string corruptPath = Path.Combine(directory, "corrupt.json");
  File.WriteAllText(corruptPath, "{}");
  var corruptStore = new GameSaveStore(corruptPath);
  Check(corruptStore.Load() == null && !corruptStore.CanSave, "Incomplete saves must be rejected and preserved");

  File.WriteAllText(futurePath, "{\"Version\":99,\"NewSchema\":true}");
  futureStore = new GameSaveStore(futurePath);
  Check(futureStore.Load() == null && !futureStore.CanSave, "Future schemas must be protected before decoding old fields");
  string blocked = Path.Combine(directory, "not-a-directory");
  File.WriteAllText(blocked, "occupied");
  var failedStore = new GameSaveStore(Path.Combine(blocked, "progress.json"));
  Check(!failedStore.Save(new()) && !string.IsNullOrEmpty(failedStore.Error), "I/O failure must report an error without crashing");

  var state = new GameState();
  state.Restore(20, 4, 6, 100_000);
  state.EarnRedGems(3);
  Check(state.CurrentRedGemCount == 23 && state.RedGemsEarnedThisRun == 100_003,
    "Restored earnings must continue accumulating independently of wallet balance");
  state.CompletePrestige(2);
  Check(state.CurrentRedGemCount == 0 && state.RedGemsEarnedThisRun == 0
    && state.CurrentBlueGemCount == 4 && state.CurrentPurpleGemCount == 8, "Prestige after load must retain permanent currencies");

  // Restore real upgrade definitions without purchasing anything or creating a game window.
  string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
  var manager = new UpgradeManager();
  UpgradeManager.CurrentUpgrades = new();
  var upgrades = UpgradeManager.CurrentUpgrades;
  foreach (var (suffix, buttons, definitions) in new[]
  {
    ("", upgrades.UpgradeButtons, upgrades.UpgradeDefinitions),
    ("_abilities", upgrades.UpgradeButtonsAbilities, upgrades.UpgradeDefinitionsAbilities),
    ("_meta", upgrades.UpgradeButtonsMeta, upgrades.UpgradeDefinitionsMeta)
  })
  {
    upgrades.LoadJson(File.ReadAllText(Path.Combine(root, $"Content/Data/upgrades{suffix}.json")),
      File.ReadAllText(Path.Combine(root, $"Content/Data/upgrades{suffix}_buttons.json")), buttons, definitions);
  }
  foreach (var (buttons, joints) in new[]
  {
    (upgrades.UpgradeButtons, upgrades.UpgradeJoints),
    (upgrades.UpgradeButtonsAbilities, upgrades.UpgradeJointsAbilities),
    (upgrades.UpgradeButtonsMeta, upgrades.UpgradeJointsMeta)
  })
    foreach (var (id, button) in buttons)
      if (!string.IsNullOrEmpty(button.Data.BlockedBy) && buttons.TryGetValue(button.Data.BlockedBy, out var parent))
        joints.Add(id, new UpgradeJoint { StartButton = parent, EndButton = button });

  var progress = new GameSave();
  var gemQuality = upgrades.UpgradeButtons.Values.First(b => b.Data.UpgradeDefinition.PropertyName == "GemSpawnQuality");
  var gemSpawner = upgrades.UpgradeButtonsAbilities["GS1"];
  var meta = upgrades.UpgradeButtonsMeta.Values.First(b => b.Data.UpgradeDefinition.Type == "float");
  progress.Upgrades["HB"] = 1;
  progress.Upgrades[gemQuality.Data.ShortName] = int.MaxValue;
  progress.Upgrades["removed-upgrade"] = 5;
  var negative = upgrades.UpgradeButtons.Values.First(b => b.Data.UpgradeDefinition.PropertyName == "GemValue");
  progress.Upgrades[negative.Data.ShortName] = -10;
  progress.Abilities["GS1"] = 1;
  progress.Meta[meta.Data.ShortName] = 1;
  float metaBefore = manager.GetFloat(meta.Data.UpgradeDefinition.ShortName);
  manager.OnUpgrade += _ => throw new Exception("Load replayed a purchase event");
  manager.OnUpgradeRoot += () => throw new Exception("Load replayed a root purchase event");
  // No GUI buttons exist in this test; suppress their diagnostic messages.
  var output = Console.Out;
  try
  {
    Console.SetOut(TextWriter.Null);
    manager.RestoreProgress(progress);
  }
  finally { Console.SetOut(output); }
  Check(manager.UG.HomeBase && manager.UG.HarvesterCount == 1, "Home base's starter harvester must be restored exactly once");
  Check(gemQuality.CurrentLevel == gemQuality.Data.NumLevels && manager.UG.GemSpawnQuality > 1,
    "Regular upgrade effects and clamped levels must restore");
  Check(negative.CurrentLevel == 0 && manager.UG.GemValue == 1, "Negative levels must not apply effects");
  Check(gemSpawner.CurrentLevel == 1 && manager.UGA.GemSpawner > 0, "Ability upgrade effects must restore");
  Check(Math.Abs(manager.GetFloat(meta.Data.UpgradeDefinition.ShortName)
    - metaBefore - meta.Data.LevelInfo[0].m_upgradeAmountFloat) < 0.0001f, "Meta upgrade effects must restore");
  var captured = new GameSave();
  manager.CaptureProgress(captured);
  Check(captured.Upgrades["HB"] == 1 && captured.Abilities["GS1"] == 1
    && captured.Meta[meta.Data.ShortName] == 1 && !captured.Upgrades.ContainsKey("removed-upgrade"),
    "Capture must include all trees and discard removed upgrades");
  // Regression: loaded partial/maxed purchases used to have invisible connections (zero animation progress).
  progress = new GameSave
  {
    Upgrades = new() { ["HB"] = 1, ["HS1"] = 1, ["HC1"] = 5, ["GSC1"] = 1 },
    Abilities = new() { ["AS1"] = 1, ["GS1"] = 1, ["GSCD1"] = 1 },
    Meta = new() { ["RH1"] = 1 },
    RedGems = 15
  };
  manager = new UpgradeManager();
  manager.RestoreProgress(progress);
  Check(upgrades.UpgradeButtons["HB"].State == UpgradeButton.UnlockState.MaxedOut,
    "Loaded root should be maxed");
  Check(upgrades.UpgradeButtons["HS1"].State == UpgradeButton.UnlockState.Purchased,
    "Loaded partial upgrade should be purchased");
  Check(upgrades.UpgradeButtons["HS1"].CanAfford && !upgrades.UpgradeButtons["PI1"].CanAfford,
    "Button affordability must match the restored wallet before the first update");
  Check(upgrades.UpgradeButtons["HC1"].State == UpgradeButton.UnlockState.MaxedOut,
    "Loaded final level should be maxed");
  Check(upgrades.UpgradeButtons["GSR1"].State == UpgradeButton.UnlockState.Unlocked,
    "Unbought child of a purchased upgrade should unlock");
  Check(upgrades.UpgradeButtonsAbilities["GS1"].State == UpgradeButton.UnlockState.MaxedOut,
    "Ability level should determine the restored button state");
  Check(upgrades.UpgradeButtonsMeta["RH1"].CurrentLevel > 0
    && upgrades.UpgradeButtonsMeta["RH1"].State >= UpgradeButton.UnlockState.Purchased,
    "Meta purchases should restore button state");
  foreach (var joints in new[] { upgrades.UpgradeJoints, upgrades.UpgradeJointsAbilities, upgrades.UpgradeJointsMeta })
    foreach (var joint in joints.Values)
    {
      bool purchased = joint.EndButton.State is UpgradeButton.UnlockState.Purchased or UpgradeButton.UnlockState.MaxedOut;
      Check(!purchased || (joint.State == UpgradeJoint.JointState.Purchased
        && joint.UnlockingTime == 1f && joint.PurchasingTime == 1f),
        "Every purchased/maxed connection must be fully drawn on the first frame");
      Check(joint.State != UpgradeJoint.JointState.Unlocked || (joint.UnlockingTime == 1f && joint.PurchasingTime == 0f),
        "Available connections must draw completely without a purchase overlay");
      Check(joint.State != UpgradeJoint.JointState.Hidden || (joint.UnlockingTime == 0f && joint.PurchasingTime == 0f),
        "Hidden connections must have no visible progress");
    }
  manager = new UpgradeManager();
  manager.RestoreProgress(new GameSave { Upgrades = new() { ["CZS1"] = 1 } });
  Check(upgrades.UpgradeButtons["CZS1"].State == UpgradeButton.UnlockState.Invisible
    && upgrades.UpgradeJoints["CZS1"].State == UpgradeJoint.JointState.Hidden,
    "Retained prestige levels must stay hidden until the home base is repurchased");
  Check(upgrades.UpgradeButtons["HS1"].State == UpgradeButton.UnlockState.Invisible
    && upgrades.UpgradeJoints["HS1"].UnlockingTime == 0f
    && upgrades.UpgradeJoints["HS1"].PurchasingTime == 0f,
    "Restoring a reset tree must clear previous purchase visuals");
  Console.WriteLine($"Passed {checks} persistence checks.");
}
finally
{
  Directory.Delete(directory, recursive: true);
}
