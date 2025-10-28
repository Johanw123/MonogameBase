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
using MonoGameGum.GueDeriving;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tweening;

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

    public bool AddMidPoint;

    public string DataType;

    public float m_upgradeAmountFloat;
    public int m_upgradeAmountInt;
    public bool m_upgradesToBool;


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

    public UpgradeData(string name, string type, bool upgradesTo)
    {
      Name = name;
      m_upgradesToBool = upgradesTo;
      DataType = type;
    }
  }

  public class UpgradeJoint
  {
    public enum JointState
    {
      Hidden,
      Unlocked,
      Purchased
    }

    public string ToUpgradeId;
    public Vector2 Start;
    public List<Vector2> MidwayPoints = new();
    public Vector2 End;
    public JointState State = JointState.Hidden;
  }

  public class Upgrades
  {
    public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
    // Keystone Upgrade: Auto refuel
    // Instant instant or perhaps a lil dude who automatically runs out to refuel (gives some visuals)
    // Perhaps this guy can be upgraded also?
    // Keystone Upgrade: Instant collection

    // Add individual items/upgrades to harvesters, that automatically grabs gems periodically or something
    // Like attacks in vampire survivor game but each harvester has individual ones
    // Perhaps rouge-like randomized items you can buy a chest for gems and apply to a specific harvester
    //
    //
    // Game Names:
    // Beyond the Belt
    // Gem Hunter
    // Gem Quest
    // Gem Venture
    // Gem Odyssey
    // Gem Explorer
    // Gem Seeker
    // Gem Expedition
    // Gem Journey
    // Gem Pursuit
    // Gem Trek
    // Gem Voyage
    // Gem Safari
    // Gem Chase

    public Dictionary<string, UpgradeButton> UpgradeButtons = new();
    // public List<(Vector2, Vector2)> UpgradeJoints = new();
    public Dictionary<string, UpgradeJoint> UpgradeJoints = new();
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
        else if (btn.Type == "bool")
        {
          value = bool.Parse(btn.Value);
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
          UpgradeId = btn.Upgrade,
          AddMidPoint = bool.Parse(btn.AddMidPoint)
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


    private void SetBorderColor(ButtonVisual buttonVis, Color color)
    {
      Console.WriteLine(buttonVis.Children.Count);

      if (buttonVis.Children.Count > 2)
      {
        var borderSprite = buttonVis.Children[2] as SpriteRuntime;
        if (borderSprite != null)
        {
          borderSprite.Color = color;
        }
      }
    }

    private void SetHiddenIconColor(ButtonVisual buttonVis, Color color)
    {
      Console.WriteLine(buttonVis.Children.Count);

      if (buttonVis.Children.Count > 1)
      {
        var borderSprite = buttonVis.Children[1] as SpriteRuntime;
        if (borderSprite != null)
        {
          borderSprite.Color = color;
        }
      }
    }

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

        var border = AssetManager.Load<Texture2D>("Textures/GUI/border.png");
        var iconHidden = AssetManager.Load<Texture2D>("Textures/GUI/iconHidden.png");

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          var button = new Button
          {
            Text = "",
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

          var icon = AssetManager.Load<Texture2D>("Textures/GUI/icon.png");
          var buttonVis = button.Visual as ButtonVisual;
          // buttonVis.Background.Texture = border;
          // buttonVis.Background.TextureWidth = 50;
          // buttonVis.Background.TextureWidth = 50;
          // buttonVis.Background.TextureAddress = Gum.Managers.TextureAddress.EntireTexture;
          // buttonVis.Background.Color = new Color(

          buttonVis.Children.Clear();
          buttonVis.Children.Add(new SpriteRuntime()
          {
            Name = "IconSprite",
            Texture = icon,
            TextureAddress = Gum.Managers.TextureAddress.EntireTexture
          });

          buttonVis.Children.Add(new SpriteRuntime()
          {
            Name = "IconHiddenSprite",
            Texture = iconHidden,
            TextureAddress = Gum.Managers.TextureAddress.EntireTexture
          });

          buttonVis.Children.Add(new SpriteRuntime()
          {
            Name = "BorderSprite",
            Texture = border,
            Color = new Color(255, 255, 255, 255),
            TextureAddress = Gum.Managers.TextureAddress.EntireTexture
          });

          buttonVis.States.Disabled.Apply = () =>
          {
          };

          buttonVis.States.Focused.Apply = () =>
          {
            // buttonVis.Background.Color = new Color(255, 255, 255, 255);
          };

          buttonVis.States.Highlighted.Apply = () =>
          {
            // buttonVis.Background.Color = new Color(255, 255, 255, 255);
            // buttonVis.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
          };

          buttonVis.States.HighlightedFocused.Apply = () =>
          {
            // buttonVis.Background.Color = new Color(255, 255, 255, 255);
            // buttonVis.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
          };

          buttonVis.States.Pushed.Apply = () =>
          {
            // buttonVis.Background.Color = new Color(255, 255, 255, 255);
          };

          buttonVis.States.Enabled.Apply = () =>
          {
            // buttonVis.Background.Color = new Color(255, 255, 255, 255);
            // buttonVis.Background.Texture = TextureCache.RefuelButtonBackground;
          };

          buttonVis.States.DisabledFocused.Apply = () =>
          {
          };

          // vis.Background.Texture
          Console.WriteLine("Set upgrade window background texture");


          UG.Reset(btnData.Value.Data.UpgradeId);

          if (btnData.Value.Data.ShortName != "R")
          {
            if (btnData.Value.Data.DataType == "float")
              UG.Set(btnData.Value.Data.UpgradeId, float.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeId].BaseValue));
            else if (btnData.Value.Data.DataType == "int")
              UG.Set(btnData.Value.Data.UpgradeId, int.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeId].BaseValue));
          }
        }

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          if (string.IsNullOrEmpty(btnData.Value.Data.LockedBy) &&
              string.IsNullOrEmpty(btnData.Value.Data.HiddenBy))
          {
            btnData.Value.Button.Visual.IsEnabled = true;
            btnData.Value.Button.Visual.Visible = true;
            SetBorderColor(btnData.Value.Button.Visual as ButtonVisual, new Color(0, 255, 0, 255));
            SetHiddenIconColor(btnData.Value.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }

          if (!string.IsNullOrEmpty(btnData.Value.Data.LockedBy))
          {
            var lockedBy = CurrentUpgrades.UpgradeButtons[btnData.Value.Data.LockedBy];
            if (lockedBy != null)
            {
              float startX = lockedBy.Data.PosX + lockedBy.Button.ActualWidth / 2.0f;
              float startY = lockedBy.Data.PosY + lockedBy.Button.ActualHeight / 2.0f;
              float endX = btnData.Value.Data.PosX + btnData.Value.Button.ActualWidth / 2.0f;
              float endY = btnData.Value.Data.PosY + btnData.Value.Button.ActualHeight / 2.0f;

              var midPoints = new List<Vector2>();

              if (startX != endX && startY != endY && btnData.Value.Data.AddMidPoint)
              {
                //Add a midway point
                midPoints.Add(new Vector2(endX, startY));
              }

              CurrentUpgrades.UpgradeJoints.Add(btnData.Key, new UpgradeJoint
              {
                ToUpgradeId = btnData.Key,
                Start = new Vector2(lockedBy.Data.PosX + lockedBy.Button.ActualWidth / 2.0f, lockedBy.Data.PosY + lockedBy.Button.ActualHeight / 2.0f),
                End = new Vector2(btnData.Value.Data.PosX + btnData.Value.Button.ActualWidth / 2.0f, btnData.Value.Data.PosY + btnData.Value.Button.ActualHeight / 2.0f),
                MidwayPoints = midPoints,
              });
            }
          }
        }

        var startPosGrouping = CurrentUpgrades.UpgradeJoints.GroupBy(j => j.Value.Start);

        foreach (var startGroup in startPosGrouping)
        {
          if (startGroup.Count() > 1)
          {
            var startPoints = startGroup.Select(p => p.Value).ToList();

            var startPointGroupingY = startPoints.GroupBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().Y : j.End.Y).ToList();
            var startPointGroupingX = startPoints.GroupBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.End.X).ToList();

            foreach (var g in startPointGroupingY)
            {
              if (g.Count() > 1)
              {
                var p = g.OrderByDescending(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.End.X);
                for (int i = 0; i < p.Count(); i++)
                {
                  var gg = p.ElementAt(i);

                  float offset = 15.0f;
                  gg.Start.Y += i * offset; //Nudge it a bit to avoid exact overlap

                  for (int j = 0; j < gg.MidwayPoints.Count; j++)
                  {
                    Vector2 mp = gg.MidwayPoints[j];
                    mp.Y += i * offset;
                    gg.MidwayPoints[j] = mp;
                  }
                }
              }
            }

            // foreach (var g in b)
            // {
            //   if (g.Count() > 1)
            //   {
            //     for (int i = 0; i < g.Count(); i++)
            //     {
            //       var gg = g.ElementAt(i);
            //
            //       gg.Start.Y += i * 15.2f; //Nudge it a bit to avoid exact overlap
            //
            //       for (int j = 0; j < gg.MidwayPoints.Count; j++)
            //       {
            //         Vector2 mp = gg.MidwayPoints[j];
            //         mp.Y += i * 15.2f;
            //       }
            //     }
            //   }
            // }
          }
        }

        // CurrentUpgrades.UpgradeButtons["R"].Button.Visual.IsEnabled = true;
        // CurrentUpgrades.UpgradeButtons["R"].Button.Visual.Visible = true;

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
      else if (upgradeData.DataType == "bool")
        UG.Set(upgradeData.UpgradeId, upgradeData.m_upgradesToBool);

      CurrentUpgrades.UpgradeButtons[upgradeName].Button.Visual.IsEnabled = false;
      var v = CurrentUpgrades.UpgradeButtons[upgradeName].Button.Visual as ButtonVisual;
      // v.Background.Color = new Color(0, 200, 0, 255);

      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value.Data.HiddenBy == upgradeName)
        {
          btn.Value.Button.Visual.Visible = true;
        }
        if (btn.Value.Data.LockedBy == upgradeName)
        {
          btn.Value.Button.Visual.IsEnabled = true;

          var joint = CurrentUpgrades.UpgradeJoints[btn.Value.Data.ShortName];
          joint.State = UpgradeJoint.JointState.Unlocked;
          SetBorderColor(btn.Value.Button.Visual as ButtonVisual, new Color(0, 255, 0, 255));
          SetHiddenIconColor(btn.Value.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
        }
      }

      if (CurrentUpgrades.UpgradeJoints.TryGetValue(upgradeName, out var j))
      {
        j.State = UpgradeJoint.JointState.Purchased;
      }

      SetBorderColor(v, new Color(0, 0, 255, 255));
    }

    private readonly Tweener _tweener = new Tweener();
    private string overButtonName = "";
    public void Update(GameTime gameTime)
    {
      var w = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      if (!string.IsNullOrEmpty(w))
      {
        if (w != overButtonName)
        {
          var buttonVis = GumService.Default.Cursor.WindowOver as ButtonVisual;
          if (buttonVis != null)
          {
            Console.WriteLine("Over upgrade button: " + w);

            _tweener.CancelAndCompleteAll();

            var c = buttonVis.Children[0] as SpriteRuntime;
            var to = c.Width;
            var toX = c.X;
            c.Width = to + 40;
            c.X -= 10;
            _tweener.TweenTo(target: c, expression: button => c.Width, toValue: to, duration: 0.3f)
                            .Easing(EasingFunctions.BounceInOut);
            _tweener.TweenTo(target: c, expression: button => c.X, toValue: toX, duration: 0.3f)
                            .Easing(EasingFunctions.BounceInOut);

            var c2 = buttonVis.Children[2] as SpriteRuntime;
            var to2 = c2.Width;
            var toX2 = c2.X;
            c2.Width = to2 + 30;
            c2.X -= 10;
            _tweener.TweenTo(target: c2, expression: button => c2.Width, toValue: to2, duration: 0.3f)
                            .Easing(EasingFunctions.BounceInOut);
            _tweener.TweenTo(target: c2, expression: button => c2.X, toValue: toX2, duration: 0.3f)
                            .Easing(EasingFunctions.BounceInOut);


            //TODO: tooltip hover
          }
        }

        overButtonName = w;
      }

      _tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    }
  }
}
