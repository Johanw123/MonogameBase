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
    using (var scene = new Scene(ShipModule.ThunderGod))
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
      Check(scene.Ship.DirectModulePickups == 0 && scene.Ship.Credits[GemCollectionMethod.ThunderGod] == 800,
        "Storm bonus pulls are worth double and cannot recursively trigger direct effects");
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
      Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 40 && ship.CarryingGemCount == 0,
        "The ghost sends its gems home and delivers the last cargo again at double value");
      scene.Invoke("ApplyMythicModules", ship, 1f);
      Check(UntitledGemGameGameScreen.DeliveredUncounted - before == 40, "Completed ghosts cannot pay twice");
      ship.RecordHeistMovement(new Vector2(500, 500), new Vector2(800, 500));
      ship.CarryingGemCount = 1;
      ship.StartHeistReplay(50);
      ship.ClearCargoForPrestige();
      Check(ship.GhostRoute == null && ship.GhostPayout == 0, "Prestige clears ghost replay");
    }
    using (var scene = new Scene(ShipModule.WorldEater))
    {
      var ship = scene.Ship;
      var gems = Enumerable.Range(0, 30).Select(i => scene.AddGem(new Vector2(520 + i, 500))).ToArray();
      scene.Prepare();
      ulong before = UntitledGemGameGameScreen.DeliveredUncounted;
      scene.Invoke("ApplyMythicModules", ship, 0f);
      Check(gems.Count(g => g.PickedUp) == 1 && UntitledGemGameGameScreen.DeliveredUncounted - before == 30
        && ship.CarryingGemCount == 0, "An idle black hole nibbles one gem and sends it home at triple value");
      for (int i = 0; i < 5; i++) ship.RegisterModulePickup(true);
      scene.Invoke("ApplyMythicModules", ship, 0.05f);
      Check(gems.Count(g => g.PickedUp) == 1, "Black hole collection has a cooldown");
      scene.Invoke("ApplyMythicModules", ship, 0.06f);
      Check(gems.Count(g => g.PickedUp) == 6 && ship.WorldEaterRadius == 92 && ship.CarryingGemCount == 0,
        "The black hole swallows one gem per gem the ship picked up, and grows");
      ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(ship);
      Check(ship.ReturningToHomebase, "World Eater leaves the hold alone");
      ship.CarryingGemBaseValue = 10;
      scene.Invoke("DeliverCargo", ship);
      Check(ship.WorldEaterRadius == 80 && ship.WorldEaterHunger == 0, "Docking resets the black hole");
    }
    Console.WriteLine("Mythic modules passed: bounded branching storms, warp-safe ghost income, prestige cleanup, ghost deliveries and a feeding black hole.");
  }
}
