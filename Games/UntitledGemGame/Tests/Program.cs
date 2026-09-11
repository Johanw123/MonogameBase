using UntitledGemGame;

if (args.Contains("--ability-cooldown-check"))
{
  AbilityCooldownChecks.Run();
  return;
}

if (args.Contains("--ability-tree-check"))
{
  AbilityTreeChecks.Run();
  return;
}

if (args.Contains("--drone-check"))
{
  DroneChecks.Run();
  return;
}

if (args.Contains("--frame-counter-check"))
{
  FrameCounterChecks.Run();
  return;
}

if (args.Length == 2 && args[0] == "--render-check")
{
  using var renderCheck = new RenderChecks(args[1]);
  renderCheck.Run();
  return;
}
if (args.Contains("--benchmark"))
{
  SpatialChecks.Benchmark();
  return;
}
FrameCounterChecks.Run();
SpatialChecks.Run();
CollectorScaleChecks.Run();
SleepingGemChecks.Run();
ChainLifetimeChecks.Run();
GemClaimChecks.Run();
DroneChecks.Run();
if (args.Contains("--spatial-check")) return;

int checks = 0;
void Check(bool condition, string message)
{
  if (!condition) throw new Exception(message);
  checks++;
}

AbilityTreeChecks.Run();

// HUD height is in virtual units; letterbox offsets and render scale must both survive conversion.
var playScreen = PlayAreaBounds.GetScreenBounds(new Microsoft.Xna.Framework.Rectangle(100, 50, 1920, 1080), 132, 2160);
Check(playScreen.Minimum == new Microsoft.Xna.Framework.Vector2(100, 50)
  && playScreen.Maximum == new Microsoft.Xna.Framework.Vector2(2020, 1064),
  "Playable bounds must exclude the scaled bottom HUD and preserve letterboxing");
var fullScreen = PlayAreaBounds.GetScreenBounds(new Microsoft.Xna.Framework.Rectangle(0, 0, 3840, 2160), 132, 2160);
Check(fullScreen.Maximum.Y == 2028, "Full-resolution bounds must end at the HUD top");
var padded = playScreen.Inset(40);
Check(padded.Clamp(new Microsoft.Xna.Framework.Vector2(-500, 5000)) == new Microsoft.Xna.Framework.Vector2(140, 1024),
  "Off-screen targets must clamp inside the sprite margin on both axes");
Check(padded.Clamp(new Microsoft.Xna.Framework.Vector2(5000, -500)) == new Microsoft.Xna.Framework.Vector2(1980, 90),
  "Targets past the top and right must respect the sprite margin");
var largerSprite = playScreen.Inset(80);
Check(largerSprite.Maximum.X < padded.Maximum.X && largerSprite.Minimum.Y > padded.Minimum.Y,
  "Larger gems and turning ships must reserve larger edge margins");
var collapsed = new PlayAreaBounds(Microsoft.Xna.Framework.Vector2.Zero, new Microsoft.Xna.Framework.Vector2(20, 10)).Inset(100);
Check(collapsed.Minimum == collapsed.Maximum && collapsed.Minimum == new Microsoft.Xna.Framework.Vector2(10, 5),
  "Oversized sprites must produce a stable center rather than inverted bounds");

// Reproduce the startup collector: the home base has neither a ship sprite nor an Entity reference.
// This path must return before consulting graphics, camera, or upgrade state.
var movementSystem = (UntitledGemGame.Systems.HarvesterCollectionSystem)
  System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGame.Systems.HarvesterCollectionSystem));
var homeCollector = new UntitledGemGame.Entities.Harvester
{
  Type = UntitledGemGame.Entities.Harvester.HarvesterType.HomeBase,
  CurrentState = UntitledGemGame.Entities.Harvester.HarvesterState.None,
};
var homeTransform = new MonoGame.Extended.Transform2(new Microsoft.Xna.Framework.Vector2(400, 1200));
movementSystem.UpdateHarvesterPosition(new Microsoft.Xna.Framework.GameTime(), homeCollector, homeTransform);
Check(homeTransform.Position == new Microsoft.Xna.Framework.Vector2(400, 1200)
  && homeCollector.TargetScreenPosition == null,
  "Startup home-base collector must not enter ship navigation or disturb its arrival animation");
var selectTarget = typeof(UntitledGemGame.Systems.HarvesterCollectionSystem).GetMethod("GetNewTargetPosition",
  System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
var shipWithoutEntityReference = new UntitledGemGame.Entities.Harvester
{
  CollectionStrategy = UntitledGemGame.HarvesterStrategy.RandomScreenPosition,
};
var selectedTarget = (Microsoft.Xna.Framework.Vector2)selectTarget.Invoke(movementSystem,
  new object[] { shipWithoutEntityReference, padded })!;
Check(selectedTarget == padded.Clamp(selectedTarget),
  "Ship target selection must use supplied bounds without dereferencing Harvester.Entity");

string directory = Path.Combine(Path.GetTempPath(), "gem-save-checks-" + Guid.NewGuid());
// Filling the grid must not corrupt subsequent rebuilds or recycled slots.
var grid = new GemSpatialIndex(2, 30);
Check(grid.AddGem(10, 0, 0, 1) == 0 && grid.AddGem(11, 1, 1, 1) == 1,
  "Grid must accept gems up to capacity");
for (int attempt = 0; attempt < 3; attempt++)
  Check(grid.AddGem(12, 2, 2, 1) == -1, "Full grid must reject additional gems");
grid.PrepareQueries();
var activeIndices = new int[3];
grid.GetActiveGems(activeIndices.Length, activeIndices, out int activeCount);
Check(activeCount == 2 && grid.NumActiveGems == 2, "Rejected gems must not change active counts");
grid.RecycleIndex(0);
Check(grid.AddGem(13, 3, 3, 1) == 0, "Full grid must reuse a recycled slot");
grid.PrepareQueries();
Check(grid.Gems[0].EntityId == 13 && grid.NumActiveGems == 2,
  "Recycled slots must remain usable after rejected insertions");
var emptyGrid = new GemSpatialIndex(0, 30);
Check(emptyGrid.AddGem(1, 0, 0, 1) == -1, "Zero-capacity grid must reject insertion");

// Pulling a spawning gem must move its animation destination and collection bounds together.
movementSystem.flatSpatialHash = new GemSpatialIndex(4, 30);
UntitledGemGame.Systems.HarvesterCollectionSystem.Instance = movementSystem;
var chainGem = new UntitledGemGame.Entities.Gem();
chainGem.GridIndex = movementSystem.flatSpatialHash.AddGem(20, -500, 0, 1);
var chainTransform = new MonoGame.Extended.Transform2(new Microsoft.Xna.Framework.Vector2(-500, 0));
var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
typeof(UntitledGemGame.Entities.Gem).GetField("m_transform", privateInstance)!.SetValue(chainGem, chainTransform);
chainGem.SetAnimation(Microsoft.Xna.Framework.Vector2.One, chainTransform.Position, false);
foreach (var destination in new[] { new Microsoft.Xna.Framework.Vector2(-100, 10), new Microsoft.Xna.Framework.Vector2(100, 10) })
{
  chainGem.MoveByChain(destination);
  Check(chainTransform.Position == destination && chainGem.BoundingCircle.Center == destination,
    "Chain movement must synchronize rendered and collision positions on both sides of home");
  Check(movementSystem.flatSpatialHash.Gems[chainGem.GridIndex].X == destination.X
    && movementSystem.flatSpatialHash.Gems[chainGem.GridIndex].Y == destination.Y,
    "Harvester spatial queries must track chain movement");
  Check((Microsoft.Xna.Framework.Vector2)typeof(UntitledGemGame.Entities.Gem)
    .GetField("m_targetPosition", privateInstance)!.GetValue(chainGem)! == destination,
    "Spawn animation must not bounce a chain gem back to its old destination");
}
emptyGrid.PrepareQueries();

Directory.CreateDirectory(directory);
try
{
  string path = Path.Combine(directory, "progress.json");
  var store = new GameSaveStore(path);
  Check(store.Load() == null && store.CanSave, "First launch should allow a new save");
  // Exercise Gum's actual button visibility and stack layout without a graphics device.
  var continueButton = new Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime(false, false)
  {
    Height = 100
  };
  continueButton.SetContainedObject(new RenderingLibrary.Graphics.InvisibleRenderable());
  continueButton.Visible = store.Load() != null;
  var menuPanel = new Gum.GueDeriving.ContainerRuntime
  {
    ChildrenLayout = Gum.Managers.ChildrenLayout.TopToBottomStack,
    StackSpacing = 20
  };
  var newGameButton = new Gum.GueDeriving.ContainerRuntime { Height = 100 };
  menuPanel.Children.Add(continueButton);
  menuPanel.Children.Add(newGameButton);
  menuPanel.UpdateLayout();
  Check(!continueButton.Visible, "No save must hide Continue without throwing");
  Check(newGameButton.AbsoluteTop == menuPanel.AbsoluteTop,
    "Hidden Continue must not leave a gap above New Game");

  var original = new GameSave
  {
    Upgrades = new() { ["HB"] = 1, ["GQ1"] = 2 },
    Abilities = new() { ["GS1"] = 1 },
    Meta = new() { ["RH1"] = 1 },
    RedGems = ulong.MaxValue - 17,
    BlueGems = 3,
    AbilityPointsPurchased = 7,
    PurpleGems = 42,
    RedGemsEarnedThisRun = ulong.MaxValue,
    EquippedAbilities = new() { "GS1", "", "Drones1" },
    CreatedInitialGems = false,
    ActiveGemCount = 1234
  };
  Check(store.Save(original), "First save failed");
  continueButton.Visible = store.Load() != null;
  menuPanel.UpdateLayout();
  Check(continueButton.Visible && newGameButton.AbsoluteTop == menuPanel.AbsoluteTop + 120,
    "A valid save must show Continue and reserve its place in the menu");

  var loaded = new GameSaveStore(path).Load();
  Check(loaded.Upgrades["GQ1"] == 2 && loaded.Abilities["GS1"] == 1 && loaded.Meta["RH1"] == 1,
    "All three trees must round-trip");
  Check(loaded.RedGems == original.RedGems && loaded.BlueGems == 3 && loaded.PurpleGems == 42
    && loaded.RedGemsEarnedThisRun == ulong.MaxValue, "Currency and earnings must retain 64-bit precision");
  Check(loaded.ActiveGemCount == 1234, "Active gem count must survive save/load");
  Check(loaded.AbilityPointsPurchased == 7, "Lifetime ability point purchases must survive save/load");
  Check(!loaded.CreatedInitialGems
    && loaded.EquippedAbilities.SequenceEqual(original.EquippedAbilities), "Run state and slot order must round-trip");

  string savedJson = File.ReadAllText(path);
  string oldAbilityPath = Path.Combine(directory, "legacy-ability-purchases.json");
  File.WriteAllText(oldAbilityPath, savedJson.Replace("\"AbilityPointsPurchased\": 7,", ""));
  Check(new GameSaveStore(oldAbilityPath).Load()?.AbilityPointsPurchased == 0,
    "Older saves must start with zero purchases through the new panel");
  string oldGemPath = Path.Combine(directory, "legacy-gem-count.json");
  File.WriteAllText(oldGemPath, savedJson.Replace("\"ActiveGemCount\": 1234,", ""));
  var oldGemSave = new GameSaveStore(oldGemPath).Load();
  Check(oldGemSave != null && oldGemSave.ActiveGemCount == null,
    "Saves without a gem count must remain loadable");
  var emptyGemStore = new GameSaveStore(Path.Combine(directory, "empty-gems.json"));
  Check(emptyGemStore.Save(new GameSave { CreatedInitialGems = true, ActiveGemCount = 0 })
    && emptyGemStore.Load().ActiveGemCount == 0,
    "An empty field must preserve zero rather than reverting to a missing count");

  Check(!savedJson.Contains("PostPrestige"), "Saves must not store the prestige screen state");
  string legacyPath = Path.Combine(directory, "legacy-screen-state.json");
  File.WriteAllText(legacyPath, savedJson.Insert(savedJson.IndexOf('{') + 1, "\"PostPrestige\":true,"));
  var legacyStore = new GameSaveStore(legacyPath);
  var legacySave = legacyStore.Load();
  Check(legacySave != null && legacySave.PurpleGems == original.PurpleGems
    && legacySave.Upgrades["GQ1"] == 2, "Old screen state must be ignored while retaining progress");
  Check(legacyStore.Save(legacySave) && !File.ReadAllText(legacyPath).Contains("PostPrestige"),
    "Resaving an old save must discard its screen state");

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
  var buyer = new GameState();
  ulong[] earlyPointPrices = [50, 200, 450, 800, 1250];
  for (int i = 0; i < earlyPointPrices.Length; ++i)
    Check(AbilityPointProgression.GetPrice((ulong)i) == earlyPointPrices[i],
      "The first five ability points must remain affordable");
  Check(AbilityPointProgression.GetPrice(19) == 1_133_879
    && AbilityPointProgression.GetPrice(29) == 73_795_402,
    "Late ability point prices must compound beyond early-game costs");
  ulong previousPointPrice = 0;
  ulong exhaustedPoint = 0;
  for (ulong purchased = 0; purchased < 1000; ++purchased)
  {
    if (AbilityPointProgression.GetPrice(purchased) is not ulong price)
    {
      exhaustedPoint = purchased;
      break;
    }
    Check(price > previousPointPrice, "Every representable ability point price must increase");
    previousPointPrice = price;
  }
  Check(exhaustedPoint > 30 && AbilityPointProgression.GetPrice(exhaustedPoint + 1) == null,
    "Exponential prices must stop safely when they exceed the currency limit");
  ulong firstPrice = AbilityPointProgression.RedGemsPerFirstPoint;
  Check(buyer.NextAbilityPointPrice == firstPrice && !buyer.TryBuyAbilityPoint(),
    "The first point uses the configured price and cannot be bought without funds");
  buyer.EarnRedGems(firstPrice - 1);
  Check(!buyer.TryBuyAbilityPoint() && buyer.CurrentRedGemCount == firstPrice - 1
    && buyer.AbilityPointsPurchased == 0, "An unaffordable purchase must not change state");
  buyer.EarnRedGems(1);
  Check(buyer.TryBuyAbilityPoint() && buyer.CurrentRedGemCount == 0
    && buyer.CurrentBlueGemCount == 1 && buyer.AbilityPointsPurchased == 1
    && buyer.RedGemsEarnedThisRun == firstPrice, "Buying must debit the wallet without reducing prestige earnings");
  ulong secondPrice = buyer.NextAbilityPointPrice.Value;
  Check(secondPrice > firstPrice, "Successive ability points must become more expensive");
  buyer.CurrentBlueGemCount = 0;
  buyer.CompletePrestige(1);
  Check(buyer.AbilityPointsPurchased == 1 && buyer.NextAbilityPointPrice == secondPrice,
    "Spending points and prestiging must preserve the next price");
  buyer.Restore(secondPrice, 0, 1, secondPrice, buyer.AbilityPointsPurchased);
  Check(buyer.TryBuyAbilityPoint() && buyer.AbilityPointsPurchased == 2
    && buyer.CurrentRedGemCount == 0, "A restored purchase count must continue the price curve");
  buyer.Restore(ulong.MaxValue, ulong.MaxValue, 0, 0);
  Check(!buyer.TryBuyAbilityPoint() && buyer.CurrentRedGemCount == ulong.MaxValue,
    "A full ability point balance must not overflow or spend gems");
  buyer.Restore(ulong.MaxValue, 0, 0, 0, ulong.MaxValue);
  Check(buyer.NextAbilityPointPrice == null && !buyer.TryBuyAbilityPoint(),
    "Exhausted prices must not wrap around into free purchases");

  string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
  AbilityCooldownChecks.Run();
  var manager = new UpgradeManager();
  var tooltipHome = (UntitledGemGame.Entities.HomeBase)
    System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGame.Entities.HomeBase));
  var droneAbility = new UntitledGemGame.Entities.DroneAbility();
  string droneDescription = tooltipHome.GetAbilityDescription(droneAbility);
  Check(droneDescription.Contains("3 drones") && droneDescription.Contains("1s[fill #E1DAE9] lifetime")
    && !droneDescription.Contains("0.001"), "Drone tooltip must use drone lifetime, not its activation timer");
  manager.UGA.IncreaseDroneCount = 8;
  manager.UGA.IncreaseDroneFuel = 2.34567f;
  manager.UGA.DronesCooldown = 2;
  manager.UGA.DroneRecharge = true;
  droneDescription = tooltipHome.GetAbilityDescription(droneAbility);
  Check(droneDescription.Contains("8 drones") && droneDescription.Contains("2.35s")
    && droneDescription.Contains("2.5 ") && droneDescription.Contains("Recharge: +0.02s per gem"),
    "Existing drone abilities must describe upgraded count, lifetime, recharge and cooldown with concise decimals");
  manager.UGA.GemSpawnerNrGems = 11;
  manager.UGA.GemSpawnerNumberOfRings = 3;
  Check(tooltipHome.GetAbilityDescription(new UntitledGemGame.Entities.GemSpawnerAbility()).Contains("18[fill"),
    "Spawner tooltip must total all rings with the same integer truncation as spawning");
  manager.UGA.ChainMagnetizerCount = 150;
  Check(tooltipHome.GetAbilityDescription(new UntitledGemGame.Entities.ChainLightningAbility()).Contains("100 "),
    "Chain tooltip must respect the runtime gem cap");
  UntitledGemGame.Entities.HomeBase.BonusMagnetPower = 0;
  Check(tooltipHome.GetAbilityDescription(new UntitledGemGame.Entities.MagnetAbility()).Contains("50 "),
    "Inactive magnet tooltip must describe its activation power");
  manager.UGA.Speedboost = 1;
  Check(tooltipHome.GetAbilityDescription(new UntitledGemGame.Entities.SpeedboostAbility()).Contains("150%"),
    "Speed tooltip must describe the additive runtime speed bonus");
  manager = new UpgradeManager();
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
  var gatedDefinitions = new Upgrades();
  gatedDefinitions.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta.json")),
    File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta_buttons.json"))
      .Replace("\"requiredexpandspacelevels\":[\"0\"]", "\"requiredexpandspacelevels\":[\"2\"]"),
    gatedDefinitions.UpgradeButtonsMeta, gatedDefinitions.UpgradeDefinitionsMeta);
  var gated = upgrades.UpgradeButtonsMeta["RH1"];
  gated.Data = gatedDefinitions.UpgradeButtonsMeta["RH1"].Data;
  Check(gated.GetNextLevelInfo().RequiredExpandSpaceLevel == 2, "Expand Space requirements must load from button JSON");
  manager = new UpgradeManager();
  manager.RestoreProgress(new GameSave { PurpleGems = ulong.MaxValue, Upgrades = new() { ["CZS1"] = 1 } });
  Check(manager.ExpandSpaceLevel == 1 && manager.IsExpandSpaceLocked(gated) && !gated.CanAfford,
    "Purple currency must not bypass an unmet Expand Space requirement");
  manager.Upgrade(gated);
  Check(gated.CurrentLevel == 0 && !manager.UGM.RefuelHomebase,
    "A locked purchase must return before applying effects, changing levels, or touching the GUI");
  upgrades.UpgradeButtons["P1"].CurrentLevel = 1;
  Check(manager.IsExpandSpaceLocked(gated), "Free prestige must not count as an Expand Space level");
  manager = new UpgradeManager();
  manager.RestoreProgress(new GameSave { PurpleGems = ulong.MaxValue, Upgrades = new() { ["CZS1"] = 2 } });
  Check(!manager.IsExpandSpaceLocked(gated) && gated.CanAfford,
    "The required Expand Space level must unlock purchases after save restoration");
  gated.GetNextLevelInfo().RequiredExpandSpaceLevel = 3;
  Check(manager.IsExpandSpaceLocked(gated), "Changing the requirement must immediately update the lock");
  gated.GetNextLevelInfo().RequiredExpandSpaceLevel = 0;
  Check(!manager.IsExpandSpaceLocked(gated), "Setting the requirement to zero must remove the lock");
  gated.GetNextLevelInfo().RequiredExpandSpaceLevel = 3;
  manager = new UpgradeManager();
  manager.RestoreProgress(new GameSave { Upgrades = new() { ["CZS1"] = 2 }, Meta = new() { ["RH1"] = 1 } });
  Check(gated.CurrentLevel == 1 && manager.UGM.RefuelHomebase,
    "Raising a requirement must retain already purchased permanent upgrade effects");
  var perLevelDefinitions = new Upgrades();
  var perLevelButtons = System.Text.Json.Nodes.JsonNode.Parse(
    File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta_buttons.json")))!;
  var startingGemsFixture = perLevelButtons["buttons"]!.AsArray()
    .Single(button => button!["shortname"]!.GetValue<string>() == "SGC1")!;
  startingGemsFixture["requiredexpandspacelevels"] = System.Text.Json.Nodes.JsonNode.Parse("[\"0\",\"0\",\"2\",\"3\",\"4\"]");
  perLevelDefinitions.LoadJson(File.ReadAllText(Path.Combine(root, "Content/Data/upgrades_meta.json")),
    perLevelButtons.ToJsonString(), perLevelDefinitions.UpgradeButtonsMeta, perLevelDefinitions.UpgradeDefinitionsMeta);
  var tiered = upgrades.UpgradeButtonsMeta["SGC1"];
  tiered.Data = perLevelDefinitions.UpgradeButtonsMeta["SGC1"].Data;
  Check(tiered.Data.LevelInfo.Select(level => level.RequiredExpandSpaceLevel).SequenceEqual(new[] { 0, 0, 2, 3, 4 }),
    "Each upgrade level must load its own Expand Space requirement");
  upgrades.UpgradeButtons["CZS1"].CurrentLevel = 0;
  tiered.CurrentLevel = 0;
  Check(!manager.IsExpandSpaceLocked(tiered), "A later level requirement must not block level one");
  tiered.CurrentLevel = 1;
  Check(!manager.IsExpandSpaceLocked(tiered), "Level two must remain available before the level three gate");
  manager = new UpgradeManager();
  manager.RestoreProgress(new GameSave { PurpleGems = ulong.MaxValue,
    Upgrades = new() { ["CZS1"] = 1 }, Meta = new() { ["RH1"] = 1, ["SGC1"] = 2 } });
  Check(tiered.CurrentLevel == 2 && manager.IsExpandSpaceLocked(tiered) && !tiered.CanAfford,
    "Restoring level two must enforce the requirement for buying level three");
  manager.Upgrade(tiered);
  Check(tiered.CurrentLevel == 2, "A blocked level three purchase must retain earlier purchases");
  upgrades.UpgradeButtons["CZS1"].CurrentLevel = 2;
  Check(!manager.IsExpandSpaceLocked(tiered), "Meeting the requirement must unlock level three");
  tiered.CurrentLevel = 3;
  Check(manager.IsExpandSpaceLocked(tiered), "Buying level three must evaluate level four's separate requirement");
  tiered.CurrentLevel = tiered.Data.NumLevels;
  Check(!manager.IsExpandSpaceLocked(tiered), "Maxed upgrades must not display a next-level requirement");
  Console.WriteLine($"Passed {checks} persistence checks.");
}
finally
{
  Directory.Delete(directory, recursive: true);
}
