using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace UntitledGemGame
{
  public enum HarvesterStrategy : int
  {
    RandomScreenPosition,
    RandomGemPosition,
    TargetCluster,
    TargetClosestCluster,
  }

  public abstract class UpgradeButton
  {
    public Button Button { get; set; }
  }

  public class UpgradeButton<T> : UpgradeButton
  {
    // public string Name;
    // public string Description;
    public UpgradeData<T> Data;

    public UpgradeButton(Button button)
    {
      Button = button;
    }
  }

  public class UpgradeRank
  {
    public int Rank;
    public int Cost;
  }

  public class UpgradeData<T>
  {
    public string Name;
    public string ShortName;
    private int m_rank;
    public List<UpgradeRank> Ranks = new List<UpgradeRank>();

    // Hidden by
    // Locked by

    public int GetCost()
    {
      return (m_rank + 1) * 10;
    }

    public T Value;

    public UpgradeData(string name, T value)
    {
      Name = name;
      Value = value;
    }

    public void Increment(T amount)
    {
      if (amount is int or float)
      {
        dynamic t = Value;
        t += amount;
        Value = t;
      }
      if (amount is bool)
      {
        Value = amount;
      }
    }

    // public static implicit operator UpgradeData<T>(T v)
    // {
    //   return new UpgradeData<T>("name", v);
    // }
  }

  //
  //
  //
  public class Upgrades
  {
    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
    // public static int HarvesterCount = 1;
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

    // public static float HarvesterSpeed = 100.0f;
    public static UpgradeData<float> HarvesterSpeed = new UpgradeData<float>(nameof(HarvesterSpeed), 100.0f);
    public static UpgradeData<int> HarvesterCount = new UpgradeData<int>(nameof(HarvesterCount), 1);


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


  public class UpgradeManager
  {
    public static Upgrades CurrentUpgrades = new Upgrades();
    private GameState m_gameState;

    public void Init(GameState gameState)
    {
      var stack = new StackPanel();
      stack.Orientation = Orientation.Horizontal;

      var button = new Button();
      button.Text = "Upgrade Harvester Speed";
      button.Width = 200;
      button.Height = 50;
      button.Name = nameof(Upgrades.HarvesterSpeed);
      button.Click += (s, e) => UpgradeClicked(s, e);
      stack.AddChild(button);

      var button2 = new Button();
      button2.Text = "Upgrade Gem Capacity";
      button2.Name = nameof(Upgrades.HarvesterCapacity);
      button2.Click += (s, e) => UpgradeClicked(s, e);
      button2.Width = 200;
      button2.Height = 50;
      stack.AddChild(button2);


      var button3 = new Button();
      button3.Text = "Upgrade Gem Capacity";
      button3.Name = nameof(Upgrades.HarvesterCapacity);
      button3.Click += (s, e) => UpgradeClicked(s, e);
      button3.Width = 200;
      button3.Height = 50;
      stack.AddChild(button2);

      upgradeButtonsFloat.Add(new UpgradeButton<float>(button) { Data = Upgrades.HarvesterSpeed });
      // upgradeButtons.Add(new UpgradeButton<float>(button2));
      upgradeButtonsInt.Add(new UpgradeButton<int>(button3) { Data = Upgrades.HarvesterCount });

      // var d = new UpgradeData<float>(nameof(Upgrades.HarvesterSpeed), Upgrades.HarvesterSpeed);

      stack.AddToRoot();

      m_gameState = gameState;
    }

    // private List<UpgradeButton> upgradeButtons = new List<UpgradeButton>();
    // private List<object> upgradeButtons = new();
    private List<UpgradeButton<float>> upgradeButtonsFloat = new();
    private List<UpgradeButton<int>> upgradeButtonsInt = new();
    private UpgradeData<T> GetData<T>(Button button)
    {
      foreach (var b in upgradeButtonsFloat)
      {
        if (b.Button == button)
        {
          Console.WriteLine("abo: " + b.Data);
          Console.WriteLine("return: " + b.Data as UpgradeData<T>);
          return b.Data as UpgradeData<T>;
        }
      }
      Console.WriteLine("return null");
      return null;
    }

    private void UpgradeClicked(object sender, EventArgs e)
    {
      Console.WriteLine("Upgrade Clicked: " + sender);
      if (sender is Button button)
      {
        // var upgradeBtn = upgradeButtons.FirstOrDefault(b => b.Button == button);
        var f = GetData<float>(button);
        if (f != null)
          Upgrade<float>(button.Name, f);
        else
        {
          var i = GetData<int>(button);
          Upgrade<int>(button.Name, i);
        }
      }
    }

    private void Upgrade<T>(string upgradeName, UpgradeData<T> upgradeData)
    {
      Console.WriteLine("Upgrade: " + upgradeName);
      m_gameState.CurrentGemCount -= upgradeData.GetCost();

      // upgradeButton.Data.Value += 100.0f;
    }

    public void Update(GameTime gameTime)
    {
      foreach (var btn in upgradeButtonsFloat)
      {
        // btn.Button.IsEnabled = m_gameState.CurrentGemCount >= btn.GetCost();
        btn.Button.IsEnabled = m_gameState.CurrentGemCount >= btn.Data.GetCost();
      }
    }
  }
}
