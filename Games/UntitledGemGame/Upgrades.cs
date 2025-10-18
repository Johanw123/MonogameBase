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

  public class UpgradeButton
  {
    // public string Name;
    // public string Description;
    private int m_rank;
    public Button Button { get; set; }
    public UpgradeData<float> Data;

    public int GetCost()
    {
      return (m_rank + 1) * 10;
    }

    public UpgradeButton(Button button)
    {
      Button = button;
      m_rank = 0;
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
    public T CurrentValue;
    public List<UpgradeRank> Ranks = new List<UpgradeRank>();

    // Hidden by
    // Locked by

    public UpgradeData(string name, ref T value)
    {
      Name = name;
      CurrentValue = value;
    }
  }

  //
  //
  //
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

      upgradeButtons.Add(new UpgradeButton(button) { Data = new UpgradeData<float>(nameof(Upgrades.HarvesterSpeed), ref Upgrades.HarvesterSpeed) });
      upgradeButtons.Add(new UpgradeButton(button2));

      var d = new UpgradeData<float>(nameof(Upgrades.HarvesterSpeed), ref Upgrades.HarvesterSpeed);

      stack.AddToRoot();

      m_gameState = gameState;
    }

    private List<UpgradeButton> upgradeButtons = new List<UpgradeButton>();

    private void UpgradeClicked(object sender, EventArgs e)
    {
      Console.WriteLine("Upgrade Clicked: " + sender);
      if (sender is Button button)
      {
        var upgradeBtn = upgradeButtons.FirstOrDefault(b => b.Button == button);
        Upgrade(button.Name, upgradeBtn);
      }
    }

    private void Upgrade(string upgradeName, UpgradeButton upgradeButton)
    {
      Console.WriteLine("Upgrade: " + upgradeName);
      m_gameState.CurrentGemCount -= upgradeButton.GetCost();

      upgradeButton.Data.CurrentValue += 100.0f;
    }

    public void Update(GameTime gameTime)
    {
      foreach (var btn in upgradeButtons)
      {
        btn.Button.IsEnabled = m_gameState.CurrentGemCount >= btn.GetCost();
      }
    }
  }
}
