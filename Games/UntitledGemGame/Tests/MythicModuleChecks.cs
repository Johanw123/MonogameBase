using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using Scene = ExpandedModuleChecks.Scene;

internal static class MythicModuleChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    using (var scene = new Scene(ShipModule.ThunderGod, ShipModule.CascadeCapacitor))
    {
      var gems = Enumerable.Range(0, 80).Select(i => scene.AddGem(new Vector2(530 + i % 10 * 12, 450 + i / 10 * 12))).ToArray();
      var reserved = scene.AddGem(new Vector2(510, 500));
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(reserved.GridIndex);
      scene.Ship.ChargeTravelModules(59f);
      scene.Invoke("ApplyMythicModules", scene.Ship, 0f);
      Check(gems.All(g => !g.PickedUp), "Thunder waits for travel threshold");
      scene.Ship.ChargeTravelModules(1f);
      scene.Invoke("ApplyMythicModules", scene.Ship, 0f);
      Check(gems.Count(g => g.PickedUp) == 40 && !reserved.PickedUp, "Forking thunder caps at forty and leaves reservations alone");
      Check(scene.Ship.ThunderArcs.Count == 40 && scene.Ship.ThunderArcs.GroupBy(a => a.Start).All(g => g.Count() <= 3),
        "Each visible thunder strike forks at most three times");
      Check(scene.Ship.DirectModulePickups == 0 && scene.Ship.CascadeCharges == 24, "Storm bonus pulls cannot recursively trigger direct effects");
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.ThunderTravelCharge == 0 && scene.Ship.ThunderArcs.Count == 0, "Thunder resets on docking");
    }
    using (var scene = new Scene(ShipModule.TimeHeist))
    {
      var ship = scene.Ship;
      ship.RecordHeistMovement(new Vector2(500, 500), new Vector2(600, 500));
      ship.RecordHeistMovement(new Vector2(1200, 500), new Vector2(1300, 500));
      ship.CarryingGemCount = 1;
      ship.CarryingGemBaseValue = 10;
      scene.Invoke("DeliverCargo", ship);
      Check(ship.GhostRoute?.Length == 2 && ship.GhostSegment == 0, "Delivery launches previous route across trip reset");
      var first = scene.AddGem(new Vector2(550, 500));
      var second = scene.AddGem(new Vector2(1250, 500));
      var warpGap = scene.AddGem(new Vector2(900, 500));
      scene.Prepare();
      ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
      scene.Invoke("ApplyMythicModules", ship, 1f);
      Check(first.PickedUp && second.PickedUp && !warpGap.PickedUp && ship.GhostRoute == null,
        "Ghost follows recorded segments and never harvests a warp gap");
      Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 20 && ship.CarryingGemCount == 0,
        "Ghost income is paid once without filling the real ship's cargo");
      scene.Invoke("ApplyMythicModules", ship, 1f);
      Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 20, "Completed ghosts cannot pay twice");
      ship.RecordHeistMovement(new Vector2(500, 500), new Vector2(800, 500));
      ship.CarryingGemCount = 1;
      ship.StartHeistReplay();
      ship.ClearCargoForPrestige();
      Check(ship.GhostRoute == null, "Prestige clears ghost replay");
    }
    using (var scene = new Scene(ShipModule.WorldEater))
    {
      var ship = scene.Ship;
      ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
      Check(!ship.ReturningToHomebase && ship.ModuleReturnCapacity == 80, "World Eater stays out beyond normal cargo capacity");
      var gems = Enumerable.Range(0, 30).Select(i => scene.AddGem(new Vector2(520 + i, 500))).ToArray();
      scene.Prepare();
      scene.Invoke("ApplyMythicModules", ship, 0f);
      Check(gems.Count(g => g.PickedUp) == 16 && ship.WorldEaterRadius == 112, "Black hole swallows sixteen and grows");
      scene.Invoke("ApplyMythicModules", ship, 0.1f);
      Check(gems.Count(g => g.PickedUp) == 16, "Black hole collection has a cooldown");
      scene.Invoke("ApplyMythicModules", ship, 0.15f);
      Check(gems.All(g => g.PickedUp), "Black hole resumes collection after cooldown");
      ship.CarryingGemCount = 80;
      Check(ship.ReturningToHomebase, "World Eater returns at eight cargo loads");
      var dockGems = Enumerable.Range(0, 110).Select(i => scene.AddGem(new Vector2(650 + i * 0.2f, 500))).ToArray();
      scene.Prepare();
      scene.Invoke("DeliverCargo", ship);
      Check(dockGems.Count(g => g.PickedUp) == 96 && ship.CarryingGemCount == 0 && ship.WorldEaterRadius == 80,
        "Dock implosion collects ninety-six into the delivery and resets growth");
    }
    Console.WriteLine("Mythic modules passed: bounded branching storms, warp-safe ghost income, prestige cleanup, growing black holes and docking implosions.");
  }
}
