using System;
using System.Collections.Generic;

namespace UntitledGemGame;

public partial class UpgradeManager
{
  private HashSet<string> harvesterUnlockAchievements = new();
  // Local achievement IDs and persisted completions are ready for a future platform adapter.
  // Subscribe for new completions; enumerate earned IDs to reconcile with a platform later.
  public event Action<string> HarvesterUnlockAchievementEarned;
  public IReadOnlyCollection<string> HarvesterUnlockAchievements => harvesterUnlockAchievements;
  private static readonly string[] HarvesterAchievementIds =
    ["unlock_drifter", "unlock_seeker", "unlock_prospector", "unlock_trove_hunter", "unlock_rimrunner"];

  public bool IsHarvesterDiscovered(int typeIndex) => typeIndex >= 0
    && typeIndex < HarvesterAchievementIds.Length
    && harvesterUnlockAchievements.Contains(HarvesterAchievementIds[typeIndex]);

  internal static string GetHarvesterUnlockAchievementId(string upgrade)
  {
    int index = upgrade switch { "HU" => 0, "AHU" => 1, "EHU" => 2, "UHU" => 3, "PHU" => 4, _ => -1 };
    return index < 0 ? null : HarvesterAchievementIds[index];
  }

  private void RecordHarvesterUnlock(string upgrade)
  {
    string id = GetHarvesterUnlockAchievementId(upgrade);
    if (id == null) return;
    if (harvesterUnlockAchievements.Add(id)) HarvesterUnlockAchievementEarned?.Invoke(id);
  }
}
