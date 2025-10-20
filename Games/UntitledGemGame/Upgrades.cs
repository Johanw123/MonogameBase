using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private T m_upgradeAmount;

    // Hidden by
    // Locked by

    public int GetCost()
    {
      return (m_rank + 1) * 10;
    }

    public T Value;

    public UpgradeData(string name, T value, T upgradeAmount)
    {
      Name = name;
      Value = value;
      m_upgradeAmount = upgradeAmount;
    }

    public void Increment()
    {
      if (m_upgradeAmount is int or float)
      {
        dynamic t = Value;
        t += m_upgradeAmount;
        Value = t;
      }
      if (m_upgradeAmount is bool)
      {
        Value = m_upgradeAmount;
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

    public static float CameraZoomScale = 2.5f;

    public static bool AutoRefuel = false;

    public static bool RefuelAtHomebase = false;

    // public static float HarvesterSpeed = 100.0f;
    public static UpgradeData<float> HarvesterSpeed = new UpgradeData<float>(nameof(HarvesterSpeed), 100.0f, 20.0f);
    public static UpgradeData<int> HarvesterCount = new UpgradeData<int>(nameof(HarvesterCount), 1, 1);


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
      // var buttonVisual = m_refuelButton.Visual as ButtonVisual;
      // buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      // buttonVisual.Background.BorderScale = 1.0f;
      //
      // buttonVisual.Background.Texture = TextureCache.RefuelButtonBackground;
      // buttonVisual.Background.TextureAddress = TextureAddress.EntireTexture;
      //
      // buttonVisual.States.Focused.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      // };

      var window = new Window();
      window.Width = 3000;
      window.Height = 3000;
      var vis = window.Visual as WindowVisual;
      vis.Background.Color = new Color(100, 0, 0, 100);
      // vis.Background.Color = new Color(50, 50, 50, 200);
      var stack = new StackPanel();
      stack.Orientation = Orientation.Horizontal;

      var button = new Button();
      button.Text = "Upgrade Harvester Speed";
      button.Width = 200;
      button.Height = 50;
      button.Name = nameof(Upgrades.HarvesterSpeed);
      button.Click += (s, e) => UpgradeClicked(s, e);
      stack.AddChild(button);


      // var button2 = new Button();
      // button2.Text = "Upgrade Gem Capacity";
      // button2.Name = nameof(Upgrades.HarvesterCapacity);
      // button2.Click += (s, e) => UpgradeClicked(s, e);
      // button2.Width = 200;
      // button2.Height = 50;
      // stack.AddChild(button2);

      var button3 = new Button();
      button3.Text = "Harvester Count";
      button3.Name = nameof(Upgrades.HarvesterCount);
      button3.Click += (s, e) => UpgradeClicked(s, e);
      button3.Width = 200;
      button3.Height = 50;

      button3.X = 1500;
      button3.Y = 200;
      stack.AddChild(button3);

      upgradeButtonsFloat.Add(new UpgradeButton<float>(button) { Data = Upgrades.HarvesterSpeed });
      // upgradeButtons.Add(new UpgradeButton<float>(button2));
      upgradeButtonsInt.Add(new UpgradeButton<int>(button3) { Data = Upgrades.HarvesterCount });

      // var d = new UpgradeData<float>(nameof(Upgrades.HarvesterSpeed), Upgrades.HarvesterSpeed);

      // stack.AddToRoot();
      // GumService.Default.ModalRoot.Children.Add(stack.Visual);
      // RenderGuiSystem.itemsToUpdate.Add(stack.Visual);
      window.AddChild(stack);
      window.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
      RenderGuiSystem.itemsToUpdate.Add(window.Visual);

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
        if (b.Button.Name == button.Name)
        {
          Console.WriteLine("abo: " + b.Data);
          Console.WriteLine("return: " + b.Data as UpgradeData<T>);
          return b.Data as UpgradeData<T>;
        }
      }

      foreach (var b in upgradeButtonsInt)
      {
        if (b.Button.Name == button.Name)
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
      upgradeData.Increment();
      // upgradeData.Value += 100.0f;
    }

    public void Update(GameTime gameTime)
    {
      foreach (var btn in upgradeButtonsFloat)
      {
        btn.Button.IsEnabled = m_gameState.CurrentGemCount >= btn.Data.GetCost();
      }

      foreach (var btn in upgradeButtonsInt)
      {
        btn.Button.IsEnabled = m_gameState.CurrentGemCount >= btn.Data.GetCost();
      }
    }
  }
}
