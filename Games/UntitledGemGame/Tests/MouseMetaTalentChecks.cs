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
    QuantumTouch();
    ComboSupernova();
    GravityTalents();
    DenseLimits();
    Console.WriteLine("Mouse meta talents passed: mirrored clicks, combo bursts, core harvesting, steering, collapse, reservations and bounded work.");
  }

  private static bool Collect(GemSpatialIndex grid, int index)
  {
    if (!grid.Gems[index].IsActive || grid.Gems[index].ClaimState != 0) return false;
    grid.Gems[index].ClaimState = 2;
    grid.RemoveFromQueries(index);
    return true;
  }

  private static void QuantumTouch()
  {
    var grid = new GemSpatialIndex(12, 30);
    var meta = new UpgradesGeneratorUpgrades_meta { QuantumTouch = true, ClickValueMultiplier = 3 };
    var ug = new UpgradesGeneratorUpgrades();
    var clicks = new ClickUtility();
    int direct = grid.AddGem(1, 150, 50, 10);
    int mirror = grid.AddGem(2, 50, 50, 10);
    int reserved = grid.AddGem(3, 55, 50, 10);
    grid.Gems[reserved].ClaimState = 1;
    int far = grid.AddGem(4, 10, 50, 10);
    var values = new Dictionary<int, double>();
    bool Pickup(int index, double multiplier)
    {
      if (!Collect(grid, index)) return false;
      values.Add(index, multiplier);
      return true;
    }
    Check(clicks.Activate(grid, new[] { direct }, new Vector2(150, 50), ug, Pickup, 1,
      meta: meta, mirrorCenter: new Vector2(100, 50), clickRadius: 12), "Quantum click must activate");
    Check(values.Count == 2 && values[direct] == 3 && values[mirror] == 3 && clicks.Combo == 1,
      "Mirrored pickups must share value and count as one gesture");
    Check(grid.Gems[reserved].ClaimState == 1 && grid.Gems[far].ClaimState == 0,
      "Quantum Touch must leave reservations and gems beyond its radius untouched");
    int other = grid.AddGem(5, 50, 50, 10);
    Check(clicks.Activate(grid, Array.Empty<int>(), new Vector2(150, 50), ug, Pickup, 1,
      meta: meta, mirrorCenter: new Vector2(100, 50), clickRadius: 12) && values.ContainsKey(other),
      "Aiming into an empty location must still collect through the other end");
    meta.QuantumTouch = false;
    grid.AddGem(6, 50, 50, 10);
    Check(!clicks.Activate(grid, Array.Empty<int>(), new Vector2(150, 50), ug, Pickup, 1,
      meta: meta, mirrorCenter: new Vector2(100, 50), clickRadius: 12), "Unpurchased Quantum Touch must not collect");
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

  private static void GravityTalents()
  {
    var ug = new UpgradesGeneratorUpgrades { CursorGravityEnabled = true };
    var meta = new UpgradesGeneratorUpgrades_meta { CursorGravityEventHorizon = true,
      CursorGravityMobile = true, CursorGravityCollapse = true, ClickValueMultiplier = 3 };
    var well = new CursorGravityWell();
    var grid = new GemSpatialIndex(8, 30);
    int core = grid.AddGem(1, 5, 0, 10);
    int rim = grid.AddGem(2, 50, 0, 10);
    int outside = grid.AddGem(3, 100, 0, 10);
    int reserved = grid.AddGem(4, 2, 0, 10);
    grid.Gems[reserved].ClaimState = 1;
    var collected = new Dictionary<int, double>();
    void Move(int index, Vector2 position) => grid.MoveGem(index, position.X, position.Y);
    bool Pickup(int index, double multiplier)
    {
      if (!Collect(grid, index)) return false;
      collected.Add(index, multiplier);
      return true;
    }
    well.TryActivate(Vector2.Zero, 19, ug, meta: meta);
    well.Update(0.1f, grid, Move, collect: Pickup);
    Check(collected.Count == 1 && collected[core] == 6 && well.HasEventHorizon,
      "The inner core must harvest at twice effective click value");
    well.Update(3, grid, Move, collect: Pickup);
    Check(collected.Count == 2 && collected[rim] == 9 && !collected.ContainsKey(outside)
      && !collected.ContainsKey(reserved) && well.CollapseGlow == 1
      && well.TakeCollapseNotification() && !well.TakeCollapseNotification(),
      "Expiration must harvest the full radius at triple value without claiming ship targets");
    well.Update(1, grid, Move, collect: Pickup);
    Check(collected.Count == 2 && well.CollapseGlow == 0, "Collapse must fire only once and fade");
    well.Reset();
    well.TryActivate(Vector2.Zero, 19, ug, meta: meta);
    float cooldown = well.CooldownRemaining;
    well.HandleInput(true, false, true, new Vector2(200, 100), 19, ug, meta: meta);
    Check(well.Position == new Vector2(200, 100) && well.CooldownRemaining == cooldown,
      "Steering must move the well without restarting timers");
    well.HandleInput(false, false, true, Vector2.Zero, 19, ug, meta: meta);
    well.HandleInput(true, false, false, Vector2.Zero, 19, ug, meta: meta);
    Check(well.Position == new Vector2(200, 100), "Releasing aim or opening menus must park the well");
    well.Reset();
    Check(!well.TakeCollapseNotification() && well.CollapseGlow == 0,
      "Cancellation must clear the field without paying a collapse reward");
    well.TryActivate(Vector2.Zero, 19, ug);
    well.HandleInput(true, false, true, new Vector2(200, 100), 19, ug);
    Check(well.Position == Vector2.Zero, "Unpurchased wells must remain anchored");
    well.Update(3, grid, Move, collect: Pickup);
    Check(!well.TakeCollapseNotification(), "Unpurchased wells must not detonate");
  }

  private static void DenseLimits()
  {
    var grid = new GemSpatialIndex(10000, 30);
    for (int i = 0; i < 10000; i++) grid.AddGem(i, 1, 1, 1);
    var well = new CursorGravityWell();
    var ug = new UpgradesGeneratorUpgrades { CursorGravityEnabled = true };
    var meta = new UpgradesGeneratorUpgrades_meta { CursorGravityEventHorizon = true, CursorGravityCollapse = true };
    int collected = 0, moved = 0;
    bool Pickup(int index, double multiplier)
    {
      if (!Collect(grid, index)) return false;
      collected++;
      return true;
    }
    well.TryActivate(Vector2.Zero, 19, ug, meta: meta);
    well.Update(0.1f, grid, (_, _) => moved++, collect: Pickup);
    Check(collected == 16 && moved <= CursorGravityWell.FrameBudget, "Core harvesting must remain bounded in dense piles");
    collected = moved = 0;
    well.Update(3, grid, (_, _) => moved++, collect: Pickup);
    Check(collected == 128 && moved <= CursorGravityWell.FrameBudget, "Collapse must share its 128-pickup budget with core harvesting");
    var clicks = new ClickUtility();
    collected = 0;
    meta.QuantumTouch = true;
    clicks.Activate(grid, Array.Empty<int>(), new Vector2(100, 0), ug, Pickup, 1,
      meta: meta, mirrorCenter: new Vector2(50, 0), clickRadius: 19);
    Check(collected == 32, "Mirrored collection must remain bounded in dense piles");
  }
}
