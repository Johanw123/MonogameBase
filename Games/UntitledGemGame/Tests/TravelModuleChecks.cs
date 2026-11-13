using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using Scene = ExpandedModuleChecks.Scene;

internal static class TravelModuleChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    using (var scene = new Scene(ShipModule.ArcEmitter))
    {
      var gems = Enumerable.Range(1, 6).Select(i => scene.AddGem(new Vector2(500 + i * 65, 500))).ToArray();
      var claimed = scene.AddGem(new Vector2(500, 510));
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(claimed.GridIndex);
      scene.Ship.ChargeTravelModules(89f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      Check(gems.All(g => !g.PickedUp), "Arc waits for travel threshold");
      scene.Ship.ChargeTravelModules(1f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      Check(gems.Take(5).All(g => g.PickedUp) && !gems[5].PickedUp && !claimed.PickedUp,
        "Arc hops beyond pickup range, respects reservations and caps at five");
      Check(scene.Ship.Credits[GemCollectionMethod.ArcEmitter] == 150, "Zapped gems are worth triple");
      Check(scene.Ship.DirectModulePickups == 0 && scene.Ship.StormArcCount == 6, "Travel arcs are visible bonus pulls without recursive direct effects");
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.ArcTravelCharge == 0, "Trip resets travel charges");
    }
    using (var scene = new Scene(ShipModule.GravityBuoy))
    {
      var gems = Enumerable.Range(0, 30).Select(i => scene.AddGem(new Vector2(510 + i, 500))).ToArray();
      scene.Prepare();
      scene.Ship.ChargeTravelModules(150f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      scene.Ship.SetCollisionPosition(new Vector2(900, 900));
      scene.Invoke("ApplyTravelModules", scene.Ship, 0.49f);
      Check(gems.All(g => !g.PickedUp), "Buoy delays its collection");
      scene.Invoke("ApplyTravelModules", scene.Ship, 0.02f);
      Check(gems.Count(g => g.PickedUp) == 24 && scene.Ship.BuoyRemaining == 0,
        "Buoy stays behind the moving ship and caps collection at twenty-four");
    }
    using (var scene = new Scene(ShipModule.GravityBuoy))
    {
      scene.Ship.ChargeTravelModules(150f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.BuoyTravelCharge == 0 && scene.Ship.BuoyRemaining > 0,
        "A new trip clears travel charge but leaves a dropped buoy armed");
      scene.Manager.Modules.TryEquip(0, 0, ShipModule.None);
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.BuoyRemaining == 0, "Unequipping cancels a pending buoy");
    }
    Console.WriteLine("Travel modules passed: chain range, claims, thresholds, delayed buoys and trip resets.");
  }
}
