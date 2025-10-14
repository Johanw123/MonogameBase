using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UntitledGemGame
{
  public enum HarvesterStrategy : int
  {
    RandomScreenPosition,
    RandomGemPosition,
    TargetCluster,
    TargetClosestCluster,
  }

  public class Upgrades
  {
    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
    public static int HarvesterCount = 1;
    public static float HarvesterSpeed = 100.0f;
    public static int HarvesterCollectionRange = 25;
    public static int HarvesterCapacity = 10;
    public static float HarvesterMaximumFuel = 3000.0f;
    // Upgrade for fuel effeciency, burn less fuel per movement done
    public static float HarvesterRefuelSpeed = 50.0f;

    public static int GemValue = 1;
    public static int GemSpawnCooldown = 500;
    public static int GemSpawnRate = 1;
    public static int MaxGemCount = 100;

    public static int StartingGemCount = 0;

    public static float CameraZoomScale = 1.5f;

    public static bool AutoRefuel = false;

    public static bool RefuelAtHomebase = false;
    


    // Keystone Upgrade: Auto refuel
      // Instant instant or perhaps a lil dude who automatically runs out to refuel (gives some visuals)
      // Perhaps this guy can be upgraded also?
    // Keystone Upgrade: Instant collection

    // Add individual items/upgrades to harvesters, that automatically grabs gems periodically or something
    // Like attacks in vampire survivor game but each harvester has individual ones
    // Perhaps rouge-like randomized items you can buy a chest for gems and apply to a specific harvester


    public void LoadValues()
    {
    
    }

    public void SaveValues()
    {

    }
  }


}
