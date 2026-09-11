using UntitledGemGame;

internal static class AbilityCooldownChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var manager = new UpgradeManager();
  var cooldownAbilities = new UntitledGemGame.Entities.IHomeBaseAbility[]
  {
    new UntitledGemGame.Entities.MagnetAbility(),
    new UntitledGemGame.Entities.ChainLightningAbility(),
    new UntitledGemGame.Entities.DroneAbility(),
    new UntitledGemGame.Entities.GemSpawnerAbility(),
    new UntitledGemGame.Entities.SpeedboostAbility()
  };
  int[] baseCooldowns = [4000, 3000, 5000, 5000, 5000];
  for (int i = 0; i < cooldownAbilities.Length; i++)
    Check(cooldownAbilities[i].MaxCooldownTime == baseCooldowns[i], "Unpurchased prestige cooldown must preserve defaults");
  manager.UGM.AllAbilityCooldown = 2f;
  for (int i = 0; i < cooldownAbilities.Length; i++)
    Check(cooldownAbilities[i].MaxCooldownTime == baseCooldowns[i] / 2, "Prestige cooldown must affect every ability");
  manager.UGA.HomebaseMagnetizerCooldown = 2f;
  manager.UGA.ChainMagnetizerCooldown = 2f;
  manager.UGA.DronesCooldown = 2f;
  manager.UGA.GemSpawnerCooldown = 2f;
  for (int i = 0; i < 4; i++)
  {
    Check(cooldownAbilities[i].MaxCooldownTime == baseCooldowns[i] / 4, "Individual and prestige cooldown bonuses must multiply");

  }
  Check(UpgradeValueFormatter.Format(new JsonUpgrade { ShortName = "AACM" }, 2, true) == "50%",
    "Prestige cooldown tooltip must display the actual reduction");

    manager = new UpgradeManager();
    UpgradeManager.CurrentUpgrades = new();
    var upgrades = UpgradeManager.CurrentUpgrades;
    upgrades.LoadJson(File.ReadAllText("Content/Data/upgrades_meta.json"),
      File.ReadAllText("Content/Data/upgrades_meta_buttons.json"),
      upgrades.UpgradeButtonsMeta, upgrades.UpgradeDefinitionsMeta);
    var save = new GameSave();
    save.Meta["AACM1"] = 5;
    var output = Console.Out;
    try
    {
      Console.SetOut(TextWriter.Null);
      manager.RestoreProgress(save);
    }
    finally { Console.SetOut(output); }
    Check(Math.Abs(manager.UGM.AllAbilityCooldown - 2f) < 0.00001f,
      "Restoring all five levels must give double recharge speed");
    var captured = new GameSave();
    manager.CaptureProgress(captured);
    Check(captured.Meta["AACM1"] == 5, "Cooldown levels must be captured in the prestige save tree");
    Console.WriteLine("Ability cooldown checks passed: defaults, global reduction, multiplicative stacking, tooltip and persistence.");
  }
}
