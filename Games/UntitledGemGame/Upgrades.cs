using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UntitledGemGame
{
  public enum HarvesterStrategy
  {
    RandomScreenPosition,
    RandomGemPosition,
    TargetCluster,
    TargetClosestCluster,
  }

  public class Upgrades
  {
    public static int HarvesterCount = 1;
    public static float HarvesterSpeed = 1000.0f;
    public static int HarvesterCollectionRange = 25;
    public static int GemSpawnRate = 5;
    public static int GemSize = 5;

    public static float CameraZoomScale = 2.5f;

    public static int MaxGemCount = 50000;

    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;


    public void LoadValues()
    {

    }

    public void SaveValues()
    {

    }
  }


}
