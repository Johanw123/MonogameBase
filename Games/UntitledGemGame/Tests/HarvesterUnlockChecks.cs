using System.Reflection;
using UntitledGemGame;

internal static class HarvesterUnlockChecks
{
  public static void Run()
  {
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    UpgradeManager.CurrentUpgrades = new Upgrades();
    var manager = new UpgradeManager();
    int events = 0;
    manager.HarvesterUnlockAchievementEarned += _ => events++;
    var record = typeof(UpgradeManager).GetMethod("RecordHarvesterUnlock", BindingFlags.NonPublic | BindingFlags.Instance)!;
    string[] upgrades = ["HU", "AHU", "EHU", "UHU", "PHU"];
    for (int i = 0; i < upgrades.Length; i++)
    {
      Check(!manager.IsHarvesterDiscovered(i), "New ship must start locked");
      record.Invoke(manager, [upgrades[i]]);
      record.Invoke(manager, [upgrades[i]]);
      Check(manager.IsHarvesterDiscovered(i) && events == i + 1, "Only the first unlock earns an achievement");
    }
    RenderGuiSystem.Instance = (RenderGuiSystem)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RenderGuiSystem));
    manager.ResetUpgrades();
    Check(manager.HarvesterUnlockAchievements.Count == 5, "Prestige reset preserves discoveries");
    var save = new GameSave();
    manager.CaptureProgress(save);
    string directory = Path.Combine(Path.GetTempPath(), "harvester-unlocks-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try
    {
      var store = new GameSaveStore(Path.Combine(directory, "save.json"));
      Check(store.Save(save), "Save succeeds");
      var restored = new UpgradeManager();
      int restoredEvents = 0;
      restored.HarvesterUnlockAchievementEarned += _ => restoredEvents++;
      restored.RestoreProgress(store.Load());
      for (int i = 0; i < 5; i++) Check(restored.IsHarvesterDiscovered(i), "Save restores every discovery");
      record.Invoke(restored, ["PHU"]);
      Check(restoredEvents == 0, "Load and repeat purchase do not re-earn achievements");
      restored.RestoreProgress(new GameSave());
      Check(restored.HarvesterUnlockAchievements.Count == 0, "New game starts locked");
    }
    finally { Directory.Delete(directory, true); }
    Console.WriteLine("Harvester unlock checks passed: all five types, one-time events, prestige, save/load and new game.");
  }
}
