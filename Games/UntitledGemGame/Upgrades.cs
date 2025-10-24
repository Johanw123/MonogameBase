using System;
using System.Collections.Generic;
using AsyncContent;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Microsoft.Xna.Framework;
using MonoGameGum;
using System.Text.Json;
using System.Linq;

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
    public Button Button { get; set; }
    public UpgradeData Data { get; set; }
  }

  public class UpgradeData
  {
    public string Name;
    public string ShortName;
    public string PropertyName;
    public string UpgradeId;

    public int Cost = 0;

    public int PosX;
    public int PosY;

    public string HiddenBy;
    public string LockedBy;

    public string DataType;

    public float m_upgradeAmountFloat;
    public int m_upgradeAmountInt;


    public UpgradeData(string name, string type, float upgradeAmount)
    {
      Name = name;
      m_upgradeAmountFloat = upgradeAmount;
      DataType = type;
    }

    public UpgradeData(string name, string type, int upgradeAmount)
    {
      Name = name;
      m_upgradeAmountInt = upgradeAmount;
      DataType = type;
    }
  }

  public class Upgrades
  {
    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
    // public static int HarvesterCount = 1;
    // public static int HarvesterCollectionRange = 25;
    // public static int HarvesterCapacity = 10;
    // public static float HarvesterMaximumFuel = 3000.0f;
    // Upgrade for fuel effeciency, burn less fuel per movement done
    // public static float HarvesterRefuelSpeed = 50.0f;

    // public static int GemValue = 1;
    // public static int GemSpawnCooldown = 500;
    // public static int GemSpawnRate = 1;
    // public static int MaxGemCount = 100;

    // public static int StartingGemCount = 0;

    // public static float CameraZoomScale = 2.5f;

    public static bool AutoRefuel = false;
    public static bool RefuelAtHomebase = false;

    // public static float HarvesterSpeed = 100.0f;
    // public static int HarvesterCount = 1;

    // public static float HarvesterSpeed = 100.0f;
    // public static UpgradeData<float> HarvesterSpeed = new UpgradeData<float>(nameof(HarvesterSpeed), 100.0f, 20.0f);
    // public static UpgradeData<int> HarvesterCount = new UpgradeData<int>(nameof(HarvesterCount), 1, 1);

    // Keystone Upgrade: Auto refuel
    // Instant instant or perhaps a lil dude who automatically runs out to refuel (gives some visuals)
    // Perhaps this guy can be upgraded also?
    // Keystone Upgrade: Instant collection

    // Add individual items/upgrades to harvesters, that automatically grabs gems periodically or something
    // Like attacks in vampire survivor game but each harvester has individual ones
    // Perhaps rouge-like randomized items you can buy a chest for gems and apply to a specific harvester

    public Dictionary<string, UpgradeButton> UpgradeButtons = new();
    public Dictionary<string, JsonUpgrade> UpgradeDefinitions = new();

    //Loaded from json
    public int WindowWidth = -1;
    public int WindowHeight = -1;


    public void LoadFromJson(string json)
    {
      var root = JsonSerializer.Deserialize(json, SerializerContext.Default.Root);

      UpgradeButtons.Clear();
      UpgradeDefinitions.Clear();

      foreach (var def in root.Upgrades)
      {
        UpgradeDefinitions.Add(def.ShortName, def);
      }

      foreach (var btn in root.Buttons)
      {
        Console.WriteLine($"Loading upgrade button: {btn.Name} of type {btn.Type} with value {btn.Value}");

        dynamic value;

        if (btn.Type == "int")
        {
          value = int.Parse(btn.Value);
        }
        else if (btn.Type == "float")
        {
          value = float.Parse(btn.Value);
        }
        else
        {
          Console.WriteLine($"Unknown upgrade type: {btn.Type} for button {btn.Name}, skipping...");
          continue;
        }

        var upgrade = new UpgradeData(btn.Name, btn.Type, value)
        {
          ShortName = btn.Shortname,
          PropertyName = btn.PropertyName,
          Cost = int.Parse(btn.Cost),
          PosX = int.Parse(btn.PosX),
          PosY = int.Parse(btn.PosY),
          HiddenBy = btn.HiddenBy,
          LockedBy = btn.LockedBy,
          UpgradeId = btn.Upgrade
        };

        UpgradeButtons.Add(btn.Shortname, new UpgradeButton
        {
          Button = null,
          Data = upgrade
        });
      }

      WindowWidth = int.Parse(root.WindowWidth);
      WindowHeight = int.Parse(root.WindowHeight);
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

    public static UpgradesGenerator UG = new();

    private object _lock = new object();
    private void RefreshButtons(string jsonString)
    {
      lock (_lock)
      {
        RenderGuiSystem.itemsToUpdate.Clear();

        UG = new UpgradesGenerator();

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

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          var button = new Button
          {
            Text = btnData.Value.Data.Name,
            Width = 50,
            Height = 50,
            X = btnData.Value.Data.PosX,
            Y = btnData.Value.Data.PosY,
            Name = btnData.Key
          };
          button.Visual.IsEnabled = false;
          button.Visual.Visible = false;
          button.Click += (s, e) => UpgradeClicked(s, e);
          btnData.Value.Button = button;
          window.AddChild(button);

          UG.Reset(btnData.Value.Data.UpgradeId);

          if (btnData.Value.Data.ShortName != "R")
          {
            if (btnData.Value.Data.DataType == "float")
              UG.Set(btnData.Value.Data.UpgradeId, float.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeId].BaseValue));
            else if (btnData.Value.Data.DataType == "int")
              UG.Set(btnData.Value.Data.UpgradeId, int.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeId].BaseValue));
          }
        }

        CurrentUpgrades.UpgradeButtons["R"].Button.Visual.IsEnabled = true;
        CurrentUpgrades.UpgradeButtons["R"].Button.Visual.Visible = true;

        window.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
        RenderGuiSystem.itemsToUpdate.Add(window.Visual);
      }
    }

    public void Init(GameState gameState)
    {
      AssetManager.LoadAsync<string>(ContentDirectory.Data.upgrades_buttons_json, false, RefreshButtons, RefreshButtons);

      m_gameState = gameState;
    }

    private void UpgradeClicked(object sender, EventArgs e)
    {
      Console.WriteLine("Upgrade Clicked: " + sender);
      if (sender is Button button)
      {
        CurrentUpgrades.UpgradeButtons.TryGetValue(button.Name, out var upgradeBtn);
        if (upgradeBtn != null)
        {
          Upgrade(button.Name, upgradeBtn.Data);
        }
      }
    }

    private void Upgrade(string upgradeName, UpgradeData upgradeData)
    {
      Console.WriteLine("Upgrade: " + upgradeName);
      m_gameState.CurrentGemCount -= upgradeData.Cost;

      if (upgradeData.DataType == "float")
        UG.Increment(upgradeData.UpgradeId, upgradeData.m_upgradeAmountFloat);
      else if (upgradeData.DataType == "int")
        UG.Increment(upgradeData.UpgradeId, upgradeData.m_upgradeAmountInt);

      CurrentUpgrades.UpgradeButtons[upgradeName].Button.Visual.IsEnabled = false;

      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value.Data.HiddenBy == upgradeName)
        {
          btn.Value.Button.Visual.Visible = true;
        }
        if (btn.Value.Data.LockedBy == upgradeName)
        {
          btn.Value.Button.Visual.IsEnabled = true;
        }
      }
    }

    public void Update(GameTime gameTime)
    {
      var w = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      //Update Tooltip!
      if (w != null)
      {

      }
    }
  }
}
