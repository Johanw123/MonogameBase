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
    using (var scene = new Scene(ShipModule.ArcEmitter, ShipModule.CascadeCapacitor))
    {
      var gems = Enumerable.Range(1, 5).Select(i => scene.AddGem(new Vector2(500 + i * 65, 500))).ToArray();
      var claimed = scene.AddGem(new Vector2(500, 510));
      scene.Prepare();
      scene.Fleet.flatSpatialHash.TryClaim(claimed.GridIndex);
      scene.Ship.ChargeTravelModules(89f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      Check(gems.All(g => !g.PickedUp), "Arc waits for travel threshold");
      scene.Ship.ChargeTravelModules(1f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      Check(gems.Take(4).All(g => g.PickedUp) && !gems[4].PickedUp && !claimed.PickedUp,
        "Arc hops beyond pickup range, respects reservations and caps at four");
      Check(scene.Ship.DirectModulePickups == 0 && scene.Ship.CascadeCharges == 4
        && scene.Ship.StormArcCount == 5, "Travel arcs are visible bonus pulls without recursive direct effects");
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.ArcTravelCharge == 0, "Trip resets travel charges");
    }
    using (var scene = new Scene(ShipModule.GravityBuoy))
    {
      var gems = Enumerable.Range(0, 10).Select(i => scene.AddGem(new Vector2(510 + i, 500))).ToArray();
      scene.Prepare();
      scene.Ship.ChargeTravelModules(180f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      scene.Ship.SetCollisionPosition(new Vector2(900, 900));
      scene.Invoke("ApplyTravelModules", scene.Ship, 0.99f);
      Check(gems.All(g => !g.PickedUp), "Buoy delays its collection");
      scene.Invoke("ApplyTravelModules", scene.Ship, 0.02f);
      Check(gems.Count(g => g.PickedUp) == 8 && scene.Ship.BuoyRemaining == 0,
        "Buoy stays behind the moving ship and caps collection at eight");
    }
    using (var scene = new Scene(ShipModule.RecallTether, ShipModule.ArcEmitter))
    {
      scene.Ship.ChargeTravelModules(120f);
      Check(scene.Ship.RecallTravelCharge == 0, "Outbound travel cannot charge recall");
      scene.Ship.CarryingGemCount = (uint)BaseStats.GetHarvesterCapacity(scene.Ship);
      scene.Ship.CollectionEndpoint = scene.Transform.Position;
      scene.Ship.SetCollisionPosition(new Vector2(900, 900));
      var gems = Enumerable.Range(0, 5).Select(i => scene.AddGem(new Vector2(510 + i, 500))).ToArray();
      scene.Prepare();
      scene.Ship.ChargeTravelModules(120f);
      scene.Invoke("ApplyTravelModules", scene.Ship, 0f);
      Check(gems.Count(g => g.PickedUp) == 3 && scene.Ship.CarryingGemCount > BaseStats.GetHarvesterCapacity(scene.Ship),
        "Recall collects remotely on return beyond full capacity");
      scene.Ship.BeginModuleTrip();
      Check(scene.Ship.RecallTravelCharge == 0 && scene.Ship.BuoyRemaining == 0,
        "New trips cancel pending travel effects");
    }
    Console.WriteLine("Travel modules passed: chain range, claims, thresholds, delayed buoys, remote return collection and trip resets.");
  }
}
