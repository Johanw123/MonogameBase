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
    //public static int HarvesterCollectionStrategyInt
    //{
    //  get => (int)HarvesterCollectionStrategy;
    //  set => HarvesterCollectionStrategy = (HarvesterStrategy)value;
    //}

    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
    public static int HarvesterCount = 1;
    public static float HarvesterSpeed = 100.0f;
    public static int HarvesterCollectionRange = 25;
    public static int HarvesterCapacity = 10;
    public static float HarvesterMaximumFuel = 1000.0f;

    public static int GemValue = 1;
    public static int GemSpawnCooldown = 500;
    public static int GemSpawnRate = 1;
    public static int MaxGemCount = 100;

    public static int StartingGemCount = 0;

    public static float CameraZoomScale = 3.5f;


    public static bool AutoRefuel = false;


    // Keystone Upgrade: Auto refuel
      // Instant instant or perhaps a lil dude who automatically runs out to refuel (gives some visuals)
      // Perhaps this guy can be upgraded also?
    // Keystone Upgrade: Instant collection


    public void LoadValues()
    {
    
    }

    public void SaveValues()
    {

    }
  }


}
