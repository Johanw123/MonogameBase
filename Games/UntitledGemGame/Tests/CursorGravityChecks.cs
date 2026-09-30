using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class CursorGravityChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var upgrades = new UpgradesGeneratorUpgrades();
    var well = new CursorGravityWell();
    Check(!well.TryActivate(Vector2.Zero, 19, upgrades), "Gravity well requires its unlock");
    upgrades.CursorGravityEnabled = true;
    var center = new Vector2(100, 200);
    Check(!well.HandleInput(true, false, true, center, 19, upgrades) && !well.IsActive
      && well.CooldownRemaining == 0, "Holding right mouse only previews the well");
    Check(!well.HandleInput(false, true, true, center, 19, upgrades)
      && !well.HandleInput(true, true, false, center, 19, upgrades),
      "A normal left click and blocked world input cannot cast gravity");
    var clicks = new ClickUtility();
    upgrades.HoldClickEnabled = true;
    Check(clicks.ShouldClick(true, true, true, 0, upgrades), "Left click initially collects normally");
    Check(!clicks.ShouldClick(true, true, false, 1, upgrades) && !clicks.IsHolding,
      "Switching to gravity aim cancels held collection and suppresses normal clicks");
    Check(well.HandleInput(true, true, true, center, 19, upgrades) && well.Radius == 57
      && well.CooldownRemaining == 20 && well.IsActive, "Base gravity cast has 3x radius and long cooldown");
    Check(well.ActivationPending && well.ActivationGlow == 1, "Gravity cast latches visual activation until drawn");
    well.AcknowledgeVisual();
    Check(!well.ActivationPending, "Gravity visual acknowledges the cast");
    Check(!well.HandleInput(true, true, true, Vector2.Zero, 19, upgrades) && well.Position == center
      && well.DenialGlow == 1 && well.CooldownRemaining == 20,
      "A failed aimed click flashes red without moving the well or restarting cooldown");
    var grid = new GemSpatialIndex(8, 30);
    int inside = grid.AddGem(1, 150, 200, 1);
    int edge = grid.AddGem(2, 164, 200, 1, 15);
    int outside = grid.AddGem(3, 180, 200, 1);
    int reserved = grid.AddGem(4, 130, 200, 1);
    grid.Gems[reserved].ClaimState = 1;
    int moved = 0;
    void Move(int slot, Vector2 position)
    {
      ++moved;
      grid.MoveGem(slot, position.X, position.Y);
    }
    bool Overlaps(int slot, Vector2 position, float radius)
      => ClickUtility.ContainsTarget(position, new Vector2(grid.Gems[slot].X, grid.Gems[slot].Y), radius,
        slot == edge ? new Vector2(9, 15) : Vector2.Zero);
    well.Update(1, grid, Move, Overlaps);
    Check(well.DenialGlow == 0, "Cooldown denial fades back to the usual ring color");
    Check(moved == 2 && grid.Gems[inside].X < 150 && grid.Gems[edge].X < 164,
      "Well pulls loose gems, including visible edge overlap, while safely mutating query lists");
    Check(grid.Gems[outside].X == 180 && grid.Gems[reserved].X == 130,
      "Well leaves gems outside its radius and harvester reservations untouched");
    Check(Math.Abs(grid.Gems[inside].X - (100 + 50 * MathF.Exp(-0.45f))) < 0.001
      && well.CooldownRemaining == 19, "Attraction and cooldown advance by elapsed game time");
    float before = grid.Gems[inside].X;
    well.Update(4, grid, Move, Overlaps);
    Check(!well.IsActive && Math.Abs(grid.Gems[inside].X - (100 + (before - 100) * MathF.Exp(-0.45f))) < 0.001,
      "Long frames apply only the remaining active duration and never overshoot the center");
    moved = 0;
    well.Update(15, grid, Move, Overlaps);
    Check(well.CooldownRemaining == 0 && well.RechargeProgress == 1 && moved == 0,
      "Expired fields stop moving gems; cooldown still recharges");
    upgrades.CursorGravityRadiusMultiplier = 5.5f;
    upgrades.CursorGravityStrengthMultiplier = 1.75f;
    upgrades.CursorGravityDuration = 4.5f;
    upgrades.CursorGravityFrequencyMultiplier = 2;
    Check(well.TryActivate(center, 38, upgrades) && well.Radius == 209 && well.CooldownRemaining == 10,
      "Gravity ranks stack with click radius and halve recharge time");
    Check(CursorGravityWell.BaseStrength * upgrades.CursorGravityStrengthMultiplier < 1.1f,
      "Fully upgraded local attraction stays weaker than the base home magnetizer");
    upgrades.CursorGravityRadiusMultiplier = 3;
    upgrades.CursorGravityDuration = 2;
    well.Update(4, grid, Move);
    Check(well.IsActive && well.Radius == 209, "An active cast retains its original radius and duration");
    well.Reset();
    Check(!well.IsActive && well.CooldownRemaining == 0 && !well.ActivationPending && well.Radius == 0
      && well.DenialGlow == 0,
      "New runs clear gravity fields, cooldowns and visual state");
    var dense = new GemSpatialIndex(CursorGravityWell.FrameBudget + 100, 30);
    for (int i = 0; i < dense.MaxCapacity; ++i) dense.AddGem(i, 1, 1, 1);
    well.TryActivate(Vector2.Zero, 19, upgrades);
    moved = 0;
    well.Update(0.1f, dense, (_, _) => ++moved);
    Check(moved == CursorGravityWell.FrameBudget, "Dense gravity fields bound movement work per frame");
    Console.WriteLine("Cursor gravity checks passed: targeting, attraction, reservations, ranks, cooldown, reset and bounded work.");
  }
}
