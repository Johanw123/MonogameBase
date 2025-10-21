using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using System.Threading.Tasks;
using AsyncContent;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using Newtonsoft.Json;

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

    public int Cost = 0;

    public int PosX;
    public int PosY;

    // Hidden by
    // Locked by

    public int GetCost()
    {
      return Cost;
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

    public Dictionary<string, UpgradeData<int>> m_upgradesInt = new Dictionary<string, UpgradeData<int>>();
    public Dictionary<string, UpgradeData<float>> m_upgradesFloat = new Dictionary<string, UpgradeData<float>>();

    public int WindowWidth = 3000;
    public int WindowHeight = 3000;

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Button
    {
      [JsonProperty("name")]
      public string Name { get; set; }

      [JsonProperty("shortname")]
      public string Shortname { get; set; }

      [JsonProperty("type")]
      public string Type { get; set; }

      [JsonProperty("cost")]
      public string Cost { get; set; }

      [JsonProperty("value")]
      public string Value { get; set; }

      [JsonProperty("base")]
      public string Base { get; set; }

      [JsonProperty("hiddenby")]
      public string Hiddenby { get; set; }

      [JsonProperty("posx")]
      public string PosX { get; set; }

      [JsonProperty("posy")]
      public string PosY { get; set; }
    }

    public class Root
    {
      [JsonProperty("buttons")]
      public List<Button> Buttons { get; set; }

      [JsonProperty("windowwidth")]
      public string WindowWidth { get; set; }

      [JsonProperty("windowheight")]
      public string windowHeight { get; set; }
    }

    public void LoadFromJson(string json)
    {
      m_upgradesInt.Clear();
      m_upgradesFloat.Clear();

      var root = JsonConvert.DeserializeObject<Root>(json);
      // var root = System.Text.Json.JsonSerializer.Deserialize<Root>(json);

      foreach (var btn in root.Buttons)
      {
        Console.WriteLine($"Loading upgrade button: {btn.Name} of type {btn.Type} with value {btn.Value}");
        if (btn.Type == "int")
        {
          var value = int.Parse(btn.Value);
          var baseValue = int.Parse(btn.Base);
          var upgrade = new UpgradeData<int>(btn.Name, baseValue, value)
          {
            ShortName = btn.Shortname,
            Cost = int.Parse(btn.Cost),
            PosX = int.Parse(btn.PosX),
            PosY = int.Parse(btn.PosY),
          };
          m_upgradesInt.Add(btn.Shortname, upgrade);
        }
        else if (btn.Type == "float")
        {
          var value = float.Parse(btn.Value);
          var baseValue = float.Parse(btn.Base);
          var upgrade = new UpgradeData<float>(btn.Name, baseValue, value)
          {
            ShortName = btn.Shortname,
            Cost = int.Parse(btn.Cost),
            PosX = int.Parse(btn.PosX),
            PosY = int.Parse(btn.PosY),
          };
          m_upgradesFloat.Add(btn.Shortname, upgrade);
        }
      }

      WindowWidth = int.Parse(root.WindowWidth);
      WindowHeight = int.Parse(root.windowHeight);

      // string name = "Root";
      // string shortname = "R";
      // string type = "int";
      // int cost = 0;
      // int value = 0;
      //
      // if (type == "int")
      // {
      //   var a = new UpgradeData<int>(name, value, 1)
      //   {
      //     ShortName = shortname,
      //     Cost = cost
      //   };
      //
      //   m_upgradesInt.Add(shortname, a);
      // }
      //
      // m_upgradesInt.Add("HC1", new UpgradeData<int>("Harvester Count", 1, 1)
      // {
      //   ShortName = "HC1",
      //   Cost = 10,
      // });
      //
      // m_upgradesFloat.Add("HS1", new UpgradeData<float>("Harvester Speed", 100.0f, 20.0f)
      // {
      //   ShortName = "HS1",
      //   Cost = 15,
      // });

    }
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
    private Window window;

    private void RefreshButtons(string jsonString)
    {
      RenderGuiSystem.itemsToUpdate.Clear();

      if (window != null)
      {
        window.Visual.RemoveFromManagers();
        window = new Window();
      }
      else
        window = new Window();

      Console.WriteLine("Upgrades JSON reloaded");
      CurrentUpgrades.LoadFromJson(jsonString);

      window.Width = CurrentUpgrades.WindowWidth;
      window.Height = CurrentUpgrades.WindowHeight;

      var vis = window.Visual as WindowVisual;
      vis.Background.Color = new Color(200, 0, 0, 255);

      foreach (var btnData in CurrentUpgrades.m_upgradesInt)
      {
        var button = new Button
        {
          Text = btnData.Value.Name,
          Width = 50,
          Height = 50,
          X = btnData.Value.PosX,
          Y = btnData.Value.PosY,
          Name = btnData.Key
        };
        button.Click += (s, e) => UpgradeClicked(s, e);
        window.AddChild(button);
      }

      foreach (var btnData in CurrentUpgrades.m_upgradesFloat)
      {
        var button = new Button
        {
          Text = btnData.Value.Name,
          Width = 50,
          Height = 50,
          X = btnData.Value.PosX,
          Y = btnData.Value.PosY,
          Name = btnData.Key
        };
        button.Click += (s, e) => UpgradeClicked(s, e);
        window.AddChild(button);
      }

      window.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
      RenderGuiSystem.itemsToUpdate.Add(window.Visual);
    }

    public void Init(GameState gameState)
    {
      AssetManager.LoadAsync<string>(ContentDirectory.Data.upgrades_buttons_json, false, RefreshButtons, RefreshButtons);

      m_gameState = gameState;
    }

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
