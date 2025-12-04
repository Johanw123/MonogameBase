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
using Gum.Wireframe;
using RenderingLibrary.Graphics;
using System.IO;
using ImGuiNET;
using RenderingLibrary;
using MonoGame.Extended.Input;

namespace UntitledGemGame
{
  public enum HarvesterStrategy : int
  {
    None,
    RandomScreenPosition,
    RandomGemPosition,
    TargetCluster,
    TargetClosestCluster,
  }

  public class UpgradeButton
  {
    public enum UnlockState
    {
      Hidden,
      Revealed,
      Unlocked,
      Purchased,

      SelectedInEditorMode,
      HoveredInEditorMode
    }

    public UnlockState State = UnlockState.Hidden;

    public Button Button { get; set; }
    public UpgradeData Data { get; set; }
  }

  public class UpgradeData
  {
    public string ShortName;

    public JsonUpgrade UpgradeDefinition;

    public int Cost = 0;

    public int PosX;
    public int PosY;

    public string HiddenBy;
    public string LockedBy;
    public string BlockedBy;

    public bool AddMidPoint;

    public float m_upgradeAmountFloat;
    public int m_upgradeAmountInt;
    public bool m_upgradesToBool;


    public UpgradeData(string shortName, float upgradeAmount)
    {
      ShortName = shortName;
      m_upgradeAmountFloat = upgradeAmount;
    }

    public UpgradeData(string shortName, int upgradeAmount)
    {
      ShortName = shortName;
      m_upgradeAmountInt = upgradeAmount;
    }

    public UpgradeData(string shortName, bool upgradesTo)
    {
      ShortName = shortName;
      m_upgradesToBool = upgradesTo;
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
    public List<Vector2> MidwayPoints = new();

    public Vector2 StartOffset;
    public Vector2 EndOffset;
    public JointState State = JointState.Hidden;

    public UpgradeButton StartButton;
    public UpgradeButton EndButton;
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

    public Dictionary<string, UpgradeButton> UpgradeButtons = new();
    // public List<(Vector2, Vector2)> UpgradeJoints = new();
    public Dictionary<string, UpgradeJoint> UpgradeJoints = new();
    public Dictionary<string, JsonUpgrade> UpgradeDefinitions = new();

    //Loaded from json
    public int WindowWidth = -1;
    public int WindowHeight = -1;

    public void LoadFromJson(string jsonUpgrades, string jsonButtons)
    {
      var rootUpgrades = JsonSerializer.Deserialize(jsonUpgrades, SerializerContext.Default.RootUpgrades);
      var rootButtons = JsonSerializer.Deserialize(jsonButtons, SerializerContext2.Default.RootUpgradeButtons);

      UpgradeButtons.Clear();
      UpgradeDefinitions.Clear();

      foreach (var def in rootUpgrades.Upgrades)
      {
        UpgradeDefinitions.Add(def.ShortName, def);
      }

      foreach (var btn in rootButtons.Buttons)
      {
        var success = UpgradeDefinitions.TryGetValue(btn.Upgrade, out var upDef);

        if (!success)
        {
          Console.WriteLine($"Upgrade definition not found for button {btn.Shortname} with upgrade {btn.Upgrade}, skipping...");
          upDef = new JsonUpgrade
          {
            ShortName = btn.Upgrade,
            Name = "Unknown Upgrade",
            Type = "int",
            BaseValue = "0"
          };
          // continue;
        }

        Console.WriteLine($"Loading upgrade button: {btn.Shortname} of type {upDef.Type} with value {btn.Value}");

        dynamic value;

        if (upDef.Type == "int")
        {
          value = int.Parse(btn.Value);
        }
        else if (upDef.Type == "float")
        {
          value = float.Parse(btn.Value);
        }
        else if (upDef.Type == "bool")
        {
          value = bool.Parse(btn.Value);
        }
        else
        {
          Console.WriteLine($"Unknown upgrade type: {upDef.Type} for button {btn.Shortname}, skipping...");
          continue;
        }

        if (UpgradeButtons.Keys.Contains(btn.Shortname))
        {
          var newName = btn.Shortname;
          int count = 1;
          while (UpgradeButtons.Keys.Contains(newName))
          {
            newName = btn.Shortname + "_" + count.ToString();
            count++;
          }
          Console.WriteLine($"Duplicate upgrade button shortname found: {btn.Shortname}, renaming to {newName}");
          btn.Shortname = newName;
        }

        var upgrade = new UpgradeData(btn.Shortname, value)
        {
          UpgradeDefinition = upDef,
          Cost = int.Parse(btn.Cost),
          PosX = int.Parse(btn.PosX),
          PosY = int.Parse(btn.PosY),
          HiddenBy = btn.HiddenBy,
          LockedBy = btn.LockedBy,
          BlockedBy = btn.BlockedBy,
          AddMidPoint = bool.Parse(btn.AddMidPoint)
        };

        UpgradeButtons.Add(btn.Shortname, new UpgradeButton
        {
          Button = null,
          Data = upgrade
        });
      }

      WindowWidth = int.Parse(rootButtons.WindowWidth);
      WindowHeight = int.Parse(rootButtons.WindowHeight);
    }

    public void SaveToJson()
    {
      var json = @$"{{" + Environment.NewLine;
      json += $@"  ""windowwidth"": ""{WindowWidth}""," + Environment.NewLine;
      json += $@"  ""windowheight"": ""{WindowHeight}""," + Environment.NewLine;
      json += $@"  ""buttons"": [" + Environment.NewLine;

      foreach (var btn in UpgradeButtons)
      {
        if (btn.Value.Data.ShortName == "")
          continue;

        var value = btn.Value.Data.UpgradeDefinition.Type switch
        {
          "int" => btn.Value.Data.m_upgradeAmountInt.ToString(),
          "float" => btn.Value.Data.m_upgradeAmountFloat.ToString(),
          "bool" => btn.Value.Data.m_upgradesToBool.ToString(),
          _ => "0"
        };

        json += @$"    {{" + Environment.NewLine +
                   $@"      ""shortname"":""{btn.Value.Data.ShortName}""," + Environment.NewLine +
                   $@"      ""upgrade"":""{btn.Value.Data.UpgradeDefinition.ShortName}""," + Environment.NewLine +
                   $@"      ""hiddenby"":""{btn.Value.Data.HiddenBy}""," + Environment.NewLine +
                   $@"      ""lockedby"":""{btn.Value.Data.LockedBy}""," + Environment.NewLine +
                   $@"      ""blockedby"":""{btn.Value.Data.BlockedBy}""," + Environment.NewLine +
                   $@"      ""cost"":""{btn.Value.Data.Cost}""," + Environment.NewLine +
                   $@"      ""posx"":""{btn.Value.Data.PosX + 1000}""," + Environment.NewLine +
                   $@"      ""posy"":""{btn.Value.Data.PosY}""," + Environment.NewLine +
                   $@"      ""addmidpoint"":""{btn.Value.Data.AddMidPoint}""," + Environment.NewLine +
                   $@"      ""value"":""{value}""" + Environment.NewLine +
                   $@"    }}," + Environment.NewLine;
      }
      //addmidpoint
      int index = json.LastIndexOf(',');
      json = json.Remove(index, 1);

      json += @$"  ]" + Environment.NewLine;
      json += $@"}}";

      var projDir = PathHelper.FindProjectDirectory();
      var savePath = Path.Combine(projDir, "Content", "Data", "upgrades_buttons.json");
      File.WriteAllText(savePath, json);
    }

    public void LoadValues()
    {
    }

    public void SaveValues()
    {
    }

    public void AddNewButton(string shortName)
    {
      var camera = SystemManagers.Default.Renderer.Camera;
      camera.ScreenToWorld(0, 0, out float screenX, out float screenY);

      var upgrade = new UpgradeData(shortName, 0)
      {
        UpgradeDefinition = new JsonUpgrade
        {
          ShortName = shortName,
          Name = "New Upgrade",
          Type = "int",
          BaseValue = "0"
        },
        Cost = 0,
        PosX = (int)screenX,
        PosY = (int)screenY,
        HiddenBy = "",
        LockedBy = "",
        BlockedBy = "",
        AddMidPoint = true
      };

      UpgradeButtons.Add(shortName, new UpgradeButton
      {
        Button = null,
        Data = upgrade
      });
    }
  }

  public class UpgradeManager
  {
    public static Upgrades CurrentUpgrades = new();
    public static bool UpgradeGuiEditMode = false;

    public event Action OnUpgradeRoot;

    private GameState m_gameState;
    private Window window;
    public static bool UpdatingButtons = false;

    public static UpgradesGenerator UG = new();

    private void SetBorderColor(ButtonVisual buttonVis, Color color)
    {
      if (buttonVis.Children.Count > 3)
      {
        var borderSprite = buttonVis.Children[3] as SpriteRuntime;
        if (borderSprite != null)
        {
          borderSprite.Color = color;
        }
        // var borderSprite = buttonVis.Children[2] as ButtonBorderShape;
        // if (borderSprite != null)
        // {
        //   borderSprite.Color = color;
        // }
      }
    }

    private void SetButtonState(UpgradeButton upgradeBtn, UpgradeButton.UnlockState state)
    {
      if (upgradeBtn == null)
      {
        Console.WriteLine("Upgrade button is null, cannot set state");
        return;
      }

      if (upgradeBtn.Button == null)
      {
        Console.WriteLine("Upgrade button's Button is null, cannot set state");
        return;
      }

      upgradeBtn.State = state;

      SetIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 255));

      switch (state)
      {
        case UpgradeButton.UnlockState.Hidden:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = false;
            SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 0, 0, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 255));
          }
          break;
        case UpgradeButton.UnlockState.Revealed:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            // SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 0, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Unlocked:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(0, 255, 0, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Purchased:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(0, 0, 255, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.SelectedInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 0, 255, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.HoveredInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 180, 10, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
          }
          break;
      }
    }

    private void SetHiddenIconColor(ButtonVisual buttonVis, Color color)
    {
      if (buttonVis.Children.Count > 2)
      {
        var borderSprite = buttonVis.Children[2] as SpriteRuntime;
        if (borderSprite != null)
        {
          borderSprite.Color = color;
        }
      }
    }

    private void SetIconColor(ButtonVisual buttonVis, Color color)
    {
      if (buttonVis.Children.Count > 1)
      {
        var borderSprite = buttonVis.Children[1] as SpriteRuntime;
        if (borderSprite != null)
        {
          borderSprite.Color = color;
        }
      }
    }

    public static object _lock = new object();

    private void CreateButton(KeyValuePair<string, UpgradeButton> btnData)
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

      button.Visual.WidthUnits = Gum.DataTypes.DimensionUnitType.ScreenPixel;
      button.Visual.HeightUnits = Gum.DataTypes.DimensionUnitType.ScreenPixel;
      button.Visual.IsEnabled = false;
      button.Visual.Visible = false;
      button.Click += (s, e) => UpgradeClicked(s, e);
      btnData.Value.Button = button;
      window.AddChild(button);

      Texture2D icon;
      var iconPath = btnData.Value.Data.UpgradeDefinition.Icon;
      if (iconPath == "")
        iconPath = "Textures/GUI/icon.png";

      icon = AssetManager.Load<Texture2D>(iconPath);
      var buttonVis = button.Visual as ButtonVisual;

      var border = AssetManager.Load<Texture2D>("Textures/GUI/border.png");
      var iconHidden = AssetManager.Load<Texture2D>("Textures/GUI/iconHidden.png");

      var background = AssetManager.Load<Texture2D>("Textures/GUI/icon_background.png");

      buttonVis.Children.Clear();

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "BackgroundSprite",
        Texture = background,
        Color = new Color(255, 255, 255, 255),
        Width = 50,
        Height = 50,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XOrigin = HorizontalAlignment.Center,
        YOrigin = VerticalAlignment.Center,
        X = 25,
        Y = 25
      });

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "IconSprite",
        Texture = icon,
        Width = 40,
        Height = 40,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XOrigin = HorizontalAlignment.Center,
        YOrigin = VerticalAlignment.Center,
        X = 25,
        Y = 25
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

      // buttonVis.Children.Add(new ButtonBorderShape()
      // {
      //   Name = "BorderShape",
      //   Color = new Color(255, 255, 255, 255),
      // });

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
    }

    private void RefreshButtons(string jsonUpgrades, string jsonButtons)
    {
      lock (_lock)
      {
        UpdatingButtons = true;

        foreach (var item in CurrentUpgrades.UpgradeButtons)
        {
          item.Value.Button.IsEnabled = false;
        }

        CurrentUpgrades = new Upgrades();
        UG = new UpgradesGenerator();

        if (window != null)
        {
          window.Visual.RemoveFromManagers();
          RenderGuiSystem.skillTreeItems.Remove(window.Visual);
        }

        window = new Window();

        Console.WriteLine("Upgrades JSON reloaded");
        CurrentUpgrades.LoadFromJson(jsonUpgrades, jsonButtons);

        // window.X = -1000;
        // window.Y = -CurrentUpgrades.WindowHeight / 2;
        window.Width = CurrentUpgrades.WindowWidth / 2;
        window.Height = CurrentUpgrades.WindowHeight / 2;

        var vis = window.Visual as WindowVisual;
        vis.Background.Color = new Color(200, 0, 0, 0);

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          CreateButton(btnData);
          // vis.Background.Texture
          Console.WriteLine("Set upgrade window background texture");

          UG.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);

          if (btnData.Value.Data.ShortName != "HB")
          {
            var b = CurrentUpgrades.UpgradeDefinitions.TryGetValue(btnData.Value.Data.UpgradeDefinition.ShortName, out var upDef);
            if (b)
            {
              if (btnData.Value.Data.UpgradeDefinition.Type == "float")
                UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(upDef.BaseValue));
              else if (btnData.Value.Data.UpgradeDefinition.Type == "int")
                UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, int.Parse(upDef.BaseValue));
            }
          }
        }

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          if (string.IsNullOrEmpty(btnData.Value.Data.LockedBy) &&
              string.IsNullOrEmpty(btnData.Value.Data.HiddenBy) &&
              string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
          {
            SetButtonState(btnData.Value, UpgradeButton.UnlockState.Unlocked);
          }

          if (!string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
          {
            CurrentUpgrades.UpgradeButtons.TryGetValue(btnData.Value.Data.BlockedBy, out var blockedBy);
            if (blockedBy != null)
            {
              float startX = blockedBy.Data.PosX;
              float startY = blockedBy.Data.PosY;
              float endX = btnData.Value.Data.PosX;
              float endY = btnData.Value.Data.PosY;

              var midPoints = new List<Vector2>();

              if (Math.Abs(startX - endX) > 5.0f && Math.Abs(startY - endY) > 5.0f && btnData.Value.Data.AddMidPoint)
              {
                midPoints.Add(new Vector2(endX, startY));
              }

              CurrentUpgrades.UpgradeJoints.Add(btnData.Key, new UpgradeJoint
              {
                ToUpgradeId = btnData.Key,
                StartOffset = Vector2.Zero,
                EndOffset = Vector2.Zero,
                StartButton = blockedBy,
                EndButton = btnData.Value,
                MidwayPoints = midPoints,
              });

              Console.WriteLine($"Added upgrade joint from {new Vector2(startX, startY)} to {new Vector2(endX, endY)}");
            }
          }
        }

        var startPosGrouping = CurrentUpgrades.UpgradeJoints.GroupBy(j => new Vector2(j.Value.StartButton.Data.PosX, j.Value.StartButton.Data.PosY));

        foreach (var startGroup in startPosGrouping)
        {
          if (startGroup.Count() > 1)
          {
            var startPoints = startGroup.Select(p => p.Value).ToList();

            var startPointGroupingY = startPoints.GroupBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().Y : j.EndButton.Data.PosY).ToList();
            // var startPointGroupingX = startPoints.GroupBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.EndButton.Data.PosX).ToList();

            foreach (var g in startPointGroupingY)
            {
              if (g.Count() > 1)
              {
                var p = g.OrderByDescending(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.EndButton.Data.PosX).Where(j => j.EndButton.Data.PosX > j.StartButton.Data.PosX);
                for (int i = 0; i < p.Count(); i++)
                {
                  var gg = p.ElementAt(i);

                  float offset = 15.0f;
                  // gg.Start.Y += i * offset; //Nudge it a bit to avoid exact overlap
                  gg.StartOffset.Y += i * offset;

                  for (int j = 0; j < gg.MidwayPoints.Count; j++)
                  {
                    Vector2 mp = gg.MidwayPoints[j];
                    mp.Y += i * offset;
                    gg.MidwayPoints[j] = mp;
                  }
                }

                var p2 = g.OrderBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.EndButton.Data.PosX).Where(j => j.EndButton.Data.PosX < j.StartButton.Data.PosX);
                for (int i = 0; i < p2.Count(); i++)
                {
                  var gg = p2.ElementAt(i);

                  float offset = 15.0f;
                  // gg.Start.Y += i * offset; //Nudge it a bit to avoid exact overlap
                  gg.StartOffset.Y += i * offset;

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

        window.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
        RenderGuiSystem.skillTreeItems.Add(window.Visual);

        if (UpgradeGuiEditMode)
        {
          foreach (var btnData in CurrentUpgrades.UpgradeButtons)
          {
            SetButtonState(btnData.Value, UpgradeButton.UnlockState.Unlocked);
          }

          foreach (var joint in CurrentUpgrades.UpgradeJoints)
          {
            if (joint.Value.State == UpgradeJoint.JointState.Hidden)
            {
              joint.Value.State = UpgradeJoint.JointState.Unlocked;
            }
          }
        }
      }

      var camera = SystemManagers.Default.Renderer.Camera;
      var hb = CurrentUpgrades.UpgradeButtons["HB"].Button;
      Console.WriteLine("Centering camera on HB button at position: " + new Vector2(hb.X, hb.Y));
      // camera.Position = new System.Numerics.Vector2(.X, CurrentUpgrades.UpgradeButtons["HB"].Button.Y);

      camera.Position = new System.Numerics.Vector2(1000, 1000);

      UpdatingButtons = false;
    }

    private string jsonUpgrades = "";
    private string jsonUpgradeButtons = "";

    private UpgradeButton m_selectedButtonEditMode = null;

    public void Init(GameState gameState)
    {
      AssetManager.LoadAsync<string>("Data/upgrades.json", false, UpdateJsonUpgrades, UpdateJsonUpgrades);
      AssetManager.LoadAsync<string>(ContentDirectory.Data.upgrades_buttons_json, false, UpdateJsonUpgradeButtons, UpdateJsonUpgradeButtons);

      m_gameState = gameState;

      GameMain.AddCustomImGuiContent(() =>
      {
        if (UpgradeGuiEditMode)
        {
          var b = m_selectedButtonEditMode;

          if (b != null)
          {
            foreach (var btn in CurrentUpgrades.UpgradeButtons)
            {
              SetButtonState(btn.Value, UpgradeButton.UnlockState.Unlocked);
            }

            ImGui.InputText("ID/ShortName", ref b.Data.ShortName, 10);

            if (ImGui.BeginCombo("Upgrade", b.Data.UpgradeDefinition.Name))
            {
              foreach (var upg in CurrentUpgrades.UpgradeDefinitions)
              {
                bool isSelected = b.Data.UpgradeDefinition.ShortName == upg.Key;
                if (ImGui.Selectable(upg.Value.Name, isSelected))
                {
                  b.Data.UpgradeDefinition = upg.Value;
                  // Console.WriteLine(upg.Value.ShortName);

                  // if (b.Data.ShortName.Contains("NB"))
                  {
                    int c = 1;
                    b.Data.ShortName = upg.Value.ShortName + c.ToString();
                    while (CurrentUpgrades.UpgradeButtons.ContainsKey(b.Data.ShortName))
                    {
                      b.Data.ShortName = upg.Value.ShortName + c++.ToString();
                    }
                  }
                }

                if (isSelected)
                  ImGui.SetItemDefaultFocus();
              }
              ImGui.EndCombo();
            }

            ImGui.InputInt("X", ref b.Data.PosX);
            ImGui.InputInt("Y", ref b.Data.PosY);

            switch (b.Data.UpgradeDefinition.Type)
            {
              case "int":
                {
                  ImGui.InputInt("Value", ref b.Data.m_upgradeAmountInt);
                }
                break;
              case "float":
                {
                  float val = b.Data.m_upgradeAmountFloat;
                  ImGui.InputFloat("Value", ref val);
                  b.Data.m_upgradeAmountFloat = val;
                }
                break;
              case "bool":
                {
                  bool val = b.Data.m_upgradesToBool;
                  ImGui.Checkbox("Value", ref val);
                  b.Data.m_upgradesToBool = val;
                }
                break;
            }

            ImGui.InputInt("Cost", ref b.Data.Cost);

            AddCombo("HiddenBy", ref b.Data.HiddenBy);
            AddCombo("LockedBy", ref b.Data.LockedBy);
            AddCombo("BlockedBy", ref b.Data.BlockedBy);

            ImGui.Checkbox("Add MidPoint", ref b.Data.AddMidPoint);

            b.Button.X = b.Data.PosX;
            b.Button.Y = b.Data.PosY;

            SetButtonState(b, UpgradeButton.UnlockState.SelectedInEditorMode);

            ImGui.Separator();
            int count = 0;
            string newShortName = "NB0";
            while (CurrentUpgrades.UpgradeButtons.ContainsKey(newShortName))
            {
              newShortName = "NB" + count.ToString();
              ++count;
            }
            ImGui.InputText("NewButtonShortName", ref newShortName, 10);
            ImGui.Button("Add New Button");
            if (ImGui.IsItemClicked())
            {
              CurrentUpgrades.AddNewButton(newShortName);
              CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));
            }
          }

          FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"EDIT MODE ENABLED", new Vector2(10, 0), Color.Yellow, Color.Black, 35);
        }
      });
    }

    private void AddCombo(string label, ref string field)
    {
      if (ImGui.BeginCombo(label, field))
      {
        foreach (var button in CurrentUpgrades.UpgradeButtons)
        {
          bool isSelected = field == button.Value.Data.ShortName;
          if (ImGui.Selectable(button.Value.Data.ShortName, isSelected))
          {
            field = button.Value.Data.ShortName;
          }

          if (isSelected)
            ImGui.SetItemDefaultFocus();

          bool hovered = ImGui.IsItemHovered();
          if (hovered)
          {
            foreach (var btn in CurrentUpgrades.UpgradeButtons)
            {
              SetBorderColor(btn.Value.Button.Visual as ButtonVisual, new Color(0, 0, 0, 0));
              SetIconColor(btn.Value.Button.Visual as ButtonVisual, new Color(255, 255, 255, 50));
            }

            SetButtonState(button.Value, UpgradeButton.UnlockState.HoveredInEditorMode);
          }
        }
        ImGui.EndCombo();
      }
    }

    public void RefreshButtons()
    {
      RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    }

    private void UpdateJsonUpgrades(string json)
    {
      jsonUpgrades = json;
      if (string.IsNullOrEmpty(jsonUpgradeButtons))
        return;

      RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    }

    private void UpdateJsonUpgradeButtons(string json)
    {
      jsonUpgradeButtons = json;
      if (string.IsNullOrEmpty(jsonUpgrades))
        return;

      RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    }

    private void UpgradeClicked(object sender, EventArgs e)
    {
      Console.WriteLine("Upgrade Clicked: " + sender);

      if (sender is Button button)
      {
        CurrentUpgrades.UpgradeButtons.TryGetValue(button.Name, out var upgradeBtn);
        if (upgradeBtn != null)
        {
          if (UpgradeGuiEditMode)
          {
            m_selectedButtonEditMode = upgradeBtn;
          }
          else
          {
            Upgrade(button, upgradeBtn.Data);
          }
        }
      }
    }

    private void Upgrade(Button button, UpgradeData upgradeData)
    {
      var currentValue = upgradeData.UpgradeDefinition.Currency switch
      {
        "red" => m_gameState.CurrentRedGemCount,
        "blue" => m_gameState.CurrentBlueGemCount,
        _ => 0
      };

      if (currentValue < upgradeData.Cost)
      {
        Console.WriteLine("Not enough gems to purchase upgrade: " + upgradeData.ShortName);
        return;
      }

      string upgradeName = upgradeData.ShortName;

      Console.WriteLine("Upgrade: " + upgradeName);
      switch (upgradeData.UpgradeDefinition.Currency)
      {
        case "red":
          m_gameState.CurrentRedGemCount -= upgradeData.Cost;
          break;
        case "blue":
          m_gameState.CurrentBlueGemCount -= upgradeData.Cost;
          break;
      }

      if (upgradeName == "HB")
      {
        OnUpgradeRoot?.Invoke();
      }

      if (upgradeData.UpgradeDefinition.ShortName == "BG")
      {
        m_gameState.CurrentBlueGemCount += upgradeData.m_upgradeAmountInt;
      }

      if (upgradeData.UpgradeDefinition.Type == "float")
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradeAmountFloat);
      else if (upgradeData.UpgradeDefinition.Type == "int")
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradeAmountInt);
      else if (upgradeData.UpgradeDefinition.Type == "bool")
        UG.Set(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradesToBool);

      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value.Data.HiddenBy == upgradeName)
        {
          btn.Value.Button.Visual.Visible = true;

          CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
          if (joint != null)
            joint.State = UpgradeJoint.JointState.Unlocked;
        }
        if (btn.Value.Data.LockedBy == upgradeName)
        {
          CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
          if (joint != null)
            joint.State = UpgradeJoint.JointState.Unlocked;

          SetButtonState(btn.Value, UpgradeButton.UnlockState.Revealed);
        }
        if (btn.Value.Data.BlockedBy == upgradeName)
        {
          btn.Value.Button.Visual.IsEnabled = true;

          CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
          if (joint != null)
            joint.State = UpgradeJoint.JointState.Unlocked;
          SetButtonState(btn.Value, UpgradeButton.UnlockState.Unlocked);
        }
      }

      if (CurrentUpgrades.UpgradeJoints.TryGetValue(upgradeName, out var j))
      {
        j.State = UpgradeJoint.JointState.Purchased;
      }

      SetButtonState(CurrentUpgrades.UpgradeButtons[upgradeName], UpgradeButton.UnlockState.Purchased);
      HideTooltip();
      ShowTooltip(button.Visual as ButtonVisual, button.Name, false);
      // HideTooltip();
    }

    private readonly Tweener _tweener = new();
    private string prevOverButtonName = "";
    private string openTooltipButtonName = "";
    private string draggingButtonNameEditMode = "";
    private Window m_tooltipWindow;
    private FontStashSharpText m_tooltipLabel;
    private FontStashSharpText m_tooltipDescription;
    private FontStashSharpText m_tooltipCost;
    private FontStashSharpText m_tooltipValueFrom;
    // private NineSliceRuntime m_tooltipValueIcon;
    private SpriteRuntime m_tooltipValueIcon;
    private FontStashSharpText m_tooltipValueTo;

    private FontStashSharpText m_tooltipPuchasedText;
    // private NineSliceRuntime m_tooltipCostIcon;
    private SpriteRuntime m_tooltipCostIconRed;
    private SpriteRuntime m_tooltipCostIconBlue;
    private UpgradeButton m_currentTooltipButton = null;


    public static List<GraphicalUiElement> m_tooltipValueElements = new();

    public void Update(GameTime gameTime)
    {
      if (UpdatingButtons)
        return;

      var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";
      // Console.WriteLine("Over upgrade button: " + curOverButtonName);

      _tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      var buttonVis = GumService.Default.Cursor.WindowOver as ButtonVisual;
      bool isButton = buttonVis != null;

      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value.Button == null)
          continue;

        var currency = btn.Value.Data.UpgradeDefinition.Currency;
        int gemCount = currency switch
        {
          "red" => m_gameState.CurrentRedGemCount,
          "blue" => m_gameState.CurrentBlueGemCount,
          _ => 0
        };

        var bv = btn.Value.Button.Visual as ButtonVisual;
        if (btn.Value.Data.Cost > gemCount && btn.Value.State == UpgradeButton.UnlockState.Unlocked)
        {
          if (bv.Children.Count > 3)
          {
            var borderSprite = bv.Children[3] as SpriteRuntime;
            if (borderSprite != null)
            {
              borderSprite.Alpha = 50;
            }
          }
        }
        else if (btn.Value.Data.Cost <= gemCount && btn.Value.State == UpgradeButton.UnlockState.Unlocked)
        {
          if (bv.Children.Count > 3)
          {
            var borderSprite = bv.Children[3] as SpriteRuntime;
            if (borderSprite != null)
            {
              borderSprite.Alpha = 255;
            }
          }
        }
      }



      // if (!string.IsNullOrEmpty(w))
      {
        if (curOverButtonName != prevOverButtonName)
        {
          if (buttonVis != null && buttonVis.Children.Count > 1)
          {
            _tweener.CancelAndCompleteAll();

            var c = buttonVis.Children[1] as SpriteRuntime;

            if (c != null)
            {
              // var to = c.Width;
              // var toX = c.X;
              // c.Width = to + 40;
              // c.X -= 10;
              // _tweener.TweenTo(target: c, expression: button => c.Width, toValue: to, duration: 0.3f)
              //                 .Easing(EasingFunctions.BounceInOut);
              // _tweener.TweenTo(target: c, expression: button => c.X, toValue: toX, duration: 0.3f)
              //                 .Easing(EasingFunctions.BounceInOut);
              //
              // c.X = toX;
              // c.Width = to;
              //
              // var c2 = buttonVis.Children[2] as SpriteRuntime;
              // var to2 = c2.Width;
              // var toX2 = c2.X;
              // c2.Width = to2 + 30;
              // c2.X -= 10;
              // _tweener.TweenTo(target: c2, expression: button => c2.Width, toValue: to2, duration: 0.3f)
              //                 .Easing(EasingFunctions.BounceInOut);
              // _tweener.TweenTo(target: c2, expression: button => c2.X, toValue: toX2, duration: 0.3f)
              //                 .Easing(EasingFunctions.BounceInOut);

              // c2.X = toX2;
              // c2.Width = to2;

              openTooltipButtonName = curOverButtonName;
              ShowTooltip(buttonVis, curOverButtonName);
            }
          }
        }

        if (curOverButtonName != openTooltipButtonName && openTooltipButtonName != "")
        {
          HideTooltip();
          openTooltipButtonName = "";
        }
        // if (curOverButtonName != prevOverButtonName && curOverButtonName != buttonVis?.Name)
        // {
        //   HideTooltip();
        // }

        if (UpgradeGuiEditMode)
        {
          var ms = MouseExtended.GetState();
          var kb = KeyboardExtended.GetState();

          HideTooltip();

          if (curOverButtonName != "null" && curOverButtonName != null)
          {
            if (kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
            {
              draggingButtonNameEditMode = curOverButtonName;
            }
          }

          if (kb.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.LeftControl))
          {
            draggingButtonNameEditMode = "";
          }

          if (draggingButtonNameEditMode != "")
          {

            var camera = SystemManagers.Default.Renderer.Camera;
            camera.ScreenToWorld(ms.X, ms.Y, out float X, out float Y);

            if (CurrentUpgrades.UpgradeButtons.TryGetValue(draggingButtonNameEditMode, out var button))
            {
              button.Button.X = X;
              button.Button.Y = Y;

              CurrentUpgrades.UpgradeButtons[draggingButtonNameEditMode].Data.PosX = (int)button.Button.X;
              CurrentUpgrades.UpgradeButtons[draggingButtonNameEditMode].Data.PosY = (int)button.Button.Y;
            }
          }
        }

        prevOverButtonName = curOverButtonName;
      }
    }

    private void HideTooltip()
    {
      if (m_tooltipWindow != null)
      {
        m_tooltipWindow.IsVisible = false;
        m_currentTooltipButton = null;
      }
    }

    private void CreateToolTipWindow()
    {
      m_tooltipWindow = new Window()
      {
        Name = "UpgradeTooltipWindow",
      };

      var vis = m_tooltipWindow.Visual as WindowVisual;
      m_tooltipWindow.Width = 380;
      m_tooltipWindow.Height = 200;

      vis.Background.Color = new Color(0, 0, 0, 0);

      m_tooltipLabel = new FontStashSharpText()
      {
        TextAlignment = TextAlignment.Center,
        FontSize = 30
      };

      var m_tooltipLabelContainer = new GraphicalUiElement(m_tooltipLabel);

      var stackPanel = new StackPanel()
      {
      };

      // m_tooltipLabelContainer.XOrigin = HorizontalAlignment.Center;
      stackPanel.Visual.YOrigin = VerticalAlignment.Top;

      m_tooltipLabelContainer.XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle;
      stackPanel.Visual.YUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;

      stackPanel.Visual.X = 0;
      stackPanel.Visual.Y = 15;

      stackPanel.Visual.WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent;

      // var a = new ColoredRectangleRuntime()
      // {
      //   Color = new Color(50, 50, 50, 200),
      //   WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   X = 0,
      //   Y = 0,
      //   Width = 0,
      //   Height = 0,
      // };

      var r = new RectangleRuntime()
      {
        Color = new Color(100, 100, 100, 255),
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 20,
        Y = 10,
        Width = -40,
        Height = 2,
      };


      // var text = new TextRuntime()
      // {
      //   Text = "Additional info can go here.",
      //   Wrap = true,
      //   XOrigin = HorizontalAlignment.Center,
      //   XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle,
      //   Y = 10,
      // };

      m_tooltipDescription = new FontStashSharpText()
      {
        Text = "Additional info can go here. lol 123 lorem ipsum dolor sit amet consectetur adipiscing elit",
        WrapText = true,
        FontSize = 25,
      };

      var descriptionElement = new GraphicalUiElement(m_tooltipDescription)
      {
        XOrigin = HorizontalAlignment.Left,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        X = 20,
        Y = 10,
      };

      m_tooltipPuchasedText = new FontStashSharpText()
      {
        Text = "PURCHASED",
        FontSize = 30,
        Visible = false,
        FillColor = Color.Green,
        TextAlignment = TextAlignment.Left
      };

      var purchasedElement = new GraphicalUiElement(m_tooltipPuchasedText)
      {
        XOrigin = HorizontalAlignment.Left,
        YOrigin = VerticalAlignment.Bottom,
        YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
        X = 5,
        Y = -5,
      };

      var border = new RectangleRuntime()
      {
        Color = new Color(255, 100, 100, 250),
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 0,
        Y = 0,
        Width = 0,
        Height = 0,
      };

      var background = new ColoredRectangleRuntime()
      {
        Color = new Color(10, 10, 10, 250),
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 0,
        Y = 0,
        Width = 0,
        Height = 0,
      };

      // https://docs.flatredball.com/gum/code/monogame/rendering-custom-graphics


      m_tooltipCost = new FontStashSharpText()
      {
        Text = "",
        FontSize = 30,
        TextAlignment = TextAlignment.Left
      };

      // m_tooltipCostIcon = new NineSliceRuntime()
      // {
      //   Texture = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png),
      //   Width = 26 * 0.3f,
      //   Height = 38 * 0.3f,
      //   // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
      //   // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
      //   // X = 10,
      //   // Y = -44,
      // };



      var costTex = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
      var costTex2 = AssetManager.Load<Texture2D>("Textures/Gems/Gem2GrayStatic.png");

      m_tooltipCostIconRed = new SpriteRuntime()
      {
        Texture = costTex,
        Width = costTex.Width * 4.0f,
        Height = costTex.Height * 2.5f,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 10,
        // Y = -44,
      };

      m_tooltipCostIconBlue = new SpriteRuntime()
      {
        Texture = costTex2,
        Width = costTex2.Width * 3.0f,
        Height = costTex2.Height * 3.0f,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 10,
        // Y = -44,
      };

      var costElement = new GraphicalUiElement(m_tooltipCost)
      {
        // XOrigin = HorizontalAlignment.Left,
        // YOrigin = VerticalAlignment.Bottom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 50,
        // Y = -10,
      };



      var tex = AssetManager.Load<Texture2D>("Textures/icons_set/icons_128/arrow_right.png");

      // m_tooltipValueIcon = new NineSliceRuntime()
      // {
      //   // Texture = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png),
      //   Texture = tex,
      //   Width = tex.Width * 0.35f,
      //   Height = tex.Height * 0.35f,
      //   TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
      //   // TextureWidthScale = 0.5f,
      //
      //   // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
      //   // XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
      //   // X = 30,
      //   // Y = -10,
      // };

      m_tooltipValueIcon = new SpriteRuntime()
      {
        Texture = tex,
        Width = tex.Width * 0.2f,
        Height = tex.Height * 0.2f,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
      };

      m_tooltipValueFrom = new FontStashSharpText()
      {
        Text = "",
        TextAlignment = TextAlignment.Left,
        FontSize = 30
      };

      m_tooltipValueTo = new FontStashSharpText()
      {
        Text = "",
        TextAlignment = TextAlignment.Left,
        FontSize = 30,
        FillColor = Color.LimeGreen
      };

      var valueElementFrom = new GraphicalUiElement(m_tooltipValueFrom)
      {
        // XOrigin = HorizontalAlignment.Right,
        // YOrigin = VerticalAlignment.Bottom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // X = 0,
        // Y = -80,
      };

      var valueElementTo = new GraphicalUiElement(m_tooltipValueTo)
      {
        // XOrigin = HorizontalAlignment.Right,
        // YOrigin = VerticalAlignment.Bottom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // X = 50,
        // Y = -80,
      };

      m_tooltipValueElements.Add(valueElementFrom);
      m_tooltipValueElements.Add(valueElementTo);
      m_tooltipValueElements.Add(costElement);
      m_tooltipValueElements.Add(descriptionElement);
      m_tooltipValueElements.Add(purchasedElement);
      m_tooltipValueElements.Add(m_tooltipLabelContainer);

      var valueStackpanel = new StackPanel()
      {
        Orientation = Orientation.Horizontal
      };

      valueStackpanel.Visual.XOrigin = HorizontalAlignment.Right;
      valueStackpanel.Visual.YOrigin = VerticalAlignment.Bottom;
      // valueStackpanel.Visual.YUnits = Gum.Converters.GeneralUnitType.Percentage;
      // valueStackpanel.Visual.XUnits = Gum.Converters.GeneralUnitType.Percentage;
      // valueStackpanel.Visual.Y = 95;
      // valueStackpanel.Visual.X = 95;

      valueStackpanel.Visual.YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge;
      valueStackpanel.Visual.XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge;
      valueStackpanel.Visual.Y = -5;
      valueStackpanel.Visual.X = -5;
      valueStackpanel.Spacing = 10;


      var costStackpanel = new StackPanel()
      {
        Orientation = Orientation.Horizontal
      };

      costStackpanel.Visual.XOrigin = HorizontalAlignment.Left;
      costStackpanel.Visual.YOrigin = VerticalAlignment.Bottom;
      // costStackpanel.Visual.YUnits = Gum.Converters.GeneralUnitType.Percentage;
      // costStackpanel.Visual.XUnits = Gum.Converters.GeneralUnitType.Percentage;
      // costStackpanel.Visual.Y = 95;
      // costStackpanel.Visual.X = 5;
      costStackpanel.Visual.YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge;
      costStackpanel.Visual.XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall;
      costStackpanel.Visual.Y = -5;
      costStackpanel.Visual.X = 5;
      costStackpanel.Spacing = 10;

      // valueStackpanel.Visual.ChildrenLayout = Gum.Managers.ChildrenLayout.AutoGridHorizontal;

      background.AddChild(border);
      background.AddChild(stackPanel);

      stackPanel.AddChild(m_tooltipLabelContainer);
      stackPanel.AddChild(r);
      // stackPanel.AddChild(text);
      stackPanel.AddChild(descriptionElement);

      background.AddChild(costElement);

      valueStackpanel.AddChild(valueElementFrom);
      valueStackpanel.AddChild(m_tooltipValueIcon);
      valueStackpanel.AddChild(valueElementTo);

      costStackpanel.AddChild(costElement);
      costStackpanel.AddChild(m_tooltipCostIconRed);
      costStackpanel.AddChild(m_tooltipCostIconBlue);

      // background.AddChild(m_tooltipCostIcon);

      background.AddChild(costStackpanel);
      background.AddChild(purchasedElement);
      background.AddChild(valueStackpanel);

      m_tooltipWindow.AddChild(background);

      // m_tooltipWindow.Visual.XOrigin = RenderingLibrary.Graphics.HorizontalAlignment.Center;
      m_tooltipWindow.AddToRoot();
      m_tooltipWindow.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
      RenderGuiSystem.skillTreeItems.Add(m_tooltipWindow.Visual);
    }

    public void UpdateTooltipContent()
    {
      if (m_currentTooltipButton == null)
        return;

      var currency = m_currentTooltipButton.Data.UpgradeDefinition.Currency;

      switch (currency)
      {
        case "red":
          m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= m_currentTooltipButton.Data.Cost ? Color.Green : Color.Red;
          break;
        case "blue":
          m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= m_currentTooltipButton.Data.Cost ? Color.Green : Color.Red;
          break;
        default:
          m_tooltipCost.FillColor = Color.White;
          break;
      }
      // m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= m_currentTooltipButton.Data.Cost ? Color.Green : Color.Red;
    }

    private void ShowTooltip(ButtonVisual buttonVis, string buttonName, bool doAnimation = true)
    {
      if (m_tooltipWindow == null)
      {
        CreateToolTipWindow();
      }

      if (CurrentUpgrades.UpgradeButtons.TryGetValue(buttonName, out var upgradeBtn))
      {
        m_currentTooltipButton = upgradeBtn;

        var upgrade = upgradeBtn.Data.UpgradeDefinition;
        var upgradeName = upgrade.Name;
        var tooltip = upgrade.Tooltip;

        var purchased = upgradeBtn.State == UpgradeButton.UnlockState.Purchased;
        var hidden = upgradeBtn.State == UpgradeButton.UnlockState.Hidden;

        var targetPosY = buttonVis.Y + 60;

        if (hidden)
        {
          m_tooltipLabel.Text = $"HIDDEN";
          m_tooltipDescription.Text = $"???";

          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;

          m_tooltipCost.Text = "";
          // m_tooltipPuchasedText.Visible = true;
          m_tooltipCostIconRed.Visible = false;
          m_tooltipCostIconBlue.Visible = false;
          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;
        }
        else
        {
          m_tooltipLabel.Text = $"{upgradeName}";
          m_tooltipDescription.Text = $"{tooltip}";
        }

        if (purchased)
        {
          m_tooltipCost.Text = "";
          m_tooltipPuchasedText.Visible = true;
          m_tooltipCostIconRed.Visible = false;
          m_tooltipCostIconBlue.Visible = false;
          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;

          switch (upgrade.Type)
          {
            case "int":
              {
                var val = UG.GetInt(upgrade.ShortName);
                m_tooltipValueTo.Text = $"{val}";
              }
              break;
            case "float":
              {
                var val = UG.GetFloat(upgrade.ShortName);
                m_tooltipValueTo.Text = $"{val}";
              }
              break;
            default:
              m_tooltipValueFrom.Text = "";
              m_tooltipValueTo.Text = "";
              m_tooltipValueIcon.Visible = false;
              break;
          }
        }
        else if (!hidden)
        {
          m_tooltipPuchasedText.Visible = false;
          m_tooltipCost.Text = $"{upgradeBtn.Data.Cost}";

          switch (upgrade.Currency)
          {
            case "red":
              m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= upgradeBtn.Data.Cost ? Color.Green : Color.Red;
              break;
            case "blue":
              m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= upgradeBtn.Data.Cost ? Color.Green : Color.Red;
              break;
            default:
              m_tooltipCost.FillColor = Color.White;
              break;
          }

          m_tooltipValueIcon.Visible = true;

          switch (upgrade.Type)
          {
            case "int":
              {
                var val = UG.GetInt(upgrade.ShortName);
                m_tooltipValueFrom.Text = $"{val}";
                m_tooltipValueTo.Text = $"{val + upgradeBtn.Data.m_upgradeAmountInt}";
              }
              break;
            case "float":
              {
                var val = UG.GetFloat(upgrade.ShortName);
                m_tooltipValueFrom.Text = $"{val}";
                m_tooltipValueTo.Text = $"{val + upgradeBtn.Data.m_upgradeAmountFloat}";
              }
              break;
            default:
              m_tooltipValueFrom.Text = "";
              m_tooltipValueTo.Text = "";
              m_tooltipValueIcon.Visible = false;
              break;
          }

          switch (upgrade.Currency)
          {
            case "red":
              m_tooltipCostIconRed.Visible = true;
              m_tooltipCostIconBlue.Visible = false;
              break;
            case "blue":
              m_tooltipCostIconRed.Visible = false;
              m_tooltipCostIconBlue.Visible = true;
              break;
            default:
              m_tooltipCostIconRed.Visible = false;
              m_tooltipCostIconBlue.Visible = false;
              break;
          }
        }

        m_tooltipWindow.IsVisible = true;
        m_tooltipWindow.X = buttonVis.X - m_tooltipWindow.Width / 2 + buttonVis.Width / 2;
        m_tooltipWindow.Y = targetPosY;


        if (doAnimation)
        {
          m_tooltipWindow.Height = 0;

          _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 300, duration: 0.25f)
                          .Easing(EasingFunctions.CubicOut);
        }


        var camera = SystemManagers.Default.Renderer.Camera;

        foreach (var item in m_tooltipValueElements)
        {
          var child = item.Component as FontStashSharpText;

          if (child != null)
          {
            Vector2 measure = child.Measure2();
            // camera.ScreenToWorld(measure.X, measure.Y, out float worldX, out float worldY);
            // Vector2 measure = new Vector2(150, 50);
            // item.Width = worldX;
            // item.Height = worldY;

            item.Width = measure.X;
            item.Height = measure.Y;
            item.UpdateLayout();
          }
        }
      }


      // m_tooltipWindow.Width = 0;
      // m_tooltipWindow.Height = 0;
      //
      //
      // _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Width, toValue: 300, duration: 0.1f)
      //                 .Easing(EasingFunctions.BounceIn);
      //
      // _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 200, duration: 0.1f)
      //                 .Easing(EasingFunctions.BounceInOut);
    }
  }
}
