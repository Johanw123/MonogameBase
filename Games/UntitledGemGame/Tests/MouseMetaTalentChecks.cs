using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class MouseMetaTalentChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    ComboSupernova();
    Console.WriteLine("Mouse meta talents passed: combo bursts.");
  }

  private static bool Collect(GemSpatialIndex grid, int index)
  {
    if (!grid.Gems[index].IsActive || grid.Gems[index].ClaimState != 0) return false;
    grid.Gems[index].ClaimState = 2;
    grid.RemoveFromQueries(index);
    return true;
  }

  private static void ComboSupernova()
  {
    var grid = new GemSpatialIndex(40, 30);
    var ug = new UpgradesGeneratorUpgrades();
    var meta = new UpgradesGeneratorUpgrades_meta { ClickComboSupernova = true };
    var clicks = new ClickUtility();
    int nearby = grid.AddGem(1, 100, 0, 10);
    int outside = grid.AddGem(2, 200, 0, 10);
    int reserved = grid.AddGem(3, 80, 0, 10);
    grid.Gems[reserved].ClaimState = 1;
    int bursts = 0;
    bool Pickup(int index, double multiplier)
    {
      if (!Collect(grid, index)) return false;
      if (multiplier == 3) bursts++;
      return true;
    }
    for (int click = 1; click <= 5; click++)
    {
      int direct = grid.AddGem(10 + click, 0, 0, 1);
      clicks.Activate(grid, new[] { direct }, Vector2.Zero, ug, Pickup, 1, meta: meta, clickRadius: 19);
      if (click < 5) Check(bursts == 0, "Supernova must wait for five successful clicks");
    }
    Check(bursts == 1 && grid.Gems[nearby].ClaimState == 2 && grid.Gems[outside].ClaimState == 0
      && grid.Gems[reserved].ClaimState == 1, "Fifth click must emit a triple-value bounded-radius burst");
    for (int click = 6; click <= 15; click++)
    {
      int direct = grid.AddGem(10 + click, 0, 0, 1);
      grid.AddGem(100 + click, 100, 0, 1);
      clicks.Activate(grid, new[] { direct }, Vector2.Zero, ug, Pickup, 1, meta: meta);
      if (click == 11) Check(clicks.SupernovaProgress == 1, "At max combo, Supernova must still require five more clicks");
    }
    Check(clicks.SupernovaProgress == 0, "Fifteenth consecutive click must complete the third charge cycle");
    clicks.Update(100);
    Check(clicks.SupernovaProgress == 0 && clicks.Combo == 0, "Combo expiry must clear the charge");
    int seed = grid.AddGem(30, 0, 0, 1);
    clicks.Activate(grid, new[] { seed }, Vector2.Zero, ug, Pickup, 1, meta: meta);
    clicks.Reset();
    Check(clicks.SupernovaProgress == 0, "New runs must reset Supernova charge");
  }
}
