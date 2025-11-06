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

    public enum UnlockState
    {
      Hidden,
      Revealed,
      Unlocked,
      Purchased,

      SelectedInEditorMode
    }

    public UnlockState State = UnlockState.Hidden;

    public Button Button { get; set; }
    public UpgradeData Data { get; set; }
  }

  public class UpgradeData
  {
    // public string Name;
    public string ShortName;
    // public string PropertyName;
    // public string UpgradeId;

    public JsonUpgrade UpgradeDefinition;

    public int Cost = 0;

    public int PosX;
    public int PosY;

    public string HiddenBy;
    public string LockedBy;
    public string BlockedBy;

    public bool AddMidPoint;

    // public string DataType;

    public float m_upgradeAmountFloat;
    public int m_upgradeAmountInt;
    public bool m_upgradesToBool;


    public UpgradeData(string shortName, float upgradeAmount)
    {
      ShortName = shortName;
      m_upgradeAmountFloat = upgradeAmount;
      // DataType = type;
    }

    public UpgradeData(string shortName, int upgradeAmount)
    {
      ShortName = shortName;
      m_upgradeAmountInt = upgradeAmount;
      // DataType = type;
    }

    public UpgradeData(string shortName, bool upgradesTo)
    {
      ShortName = shortName;
      m_upgradesToBool = upgradesTo;
      // DataType = type;
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
        var upDef = UpgradeDefinitions[btn.Upgrade];

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
        var value = btn.Value.Data.UpgradeDefinition.Type switch
        {
          "int" => btn.Value.Data.m_upgradeAmountInt.ToString(),
          "float" => btn.Value.Data.m_upgradeAmountFloat.ToString(),
          "bool" => btn.Value.Data.m_upgradesToBool.ToString(),
          _ => "0"
        };

        json += @$"    {{" + Environment.NewLine +
                   // $@"      ""name"":""{btn.Value.Data.Name}""," + Environment.NewLine +
                   // $@"      ""propname"":""{btn.Value.Data.PropertyName}""," + Environment.NewLine +
                   $@"      ""shortname"":""{btn.Value.Data.ShortName}""," + Environment.NewLine +
                   $@"      ""upgrade"":""{btn.Value.Data.UpgradeDefinition.ShortName}""," + Environment.NewLine +
                   // $@"      ""type"":""{btn.Value.Data.DataType}""," + Environment.NewLine +
                   $@"      ""hiddenby"":""{btn.Value.Data.HiddenBy}""," + Environment.NewLine +
                   $@"      ""lockedby"":""{btn.Value.Data.LockedBy}""," + Environment.NewLine +
                   $@"      ""blockedby"":""{btn.Value.Data.BlockedBy}""," + Environment.NewLine +
                   $@"      ""cost"":""{btn.Value.Data.Cost}""," + Environment.NewLine +
                   $@"      ""posx"":""{btn.Value.Data.PosX}""," + Environment.NewLine +
                   $@"      ""posy"":""{btn.Value.Data.PosY}""," + Environment.NewLine +
                   $@"      ""value"":""{value}""" + Environment.NewLine +
                   $@"    }}," + Environment.NewLine;
      }

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
  }

  public class UpgradeManager
  {
    public static Upgrades CurrentUpgrades = new Upgrades();
    public static bool UpgradeGuiEditMode = false;

    private GameState m_gameState;
    private Window window;
    public bool UpdatingButtons = false;

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
        // var borderSprite = buttonVis.Children[2] as ButtonBorderShape;
        // if (borderSprite != null)
        // {
        //   borderSprite.Color = color;
        // }
      }
    }

    private void SetButtonState(UpgradeButton upgradeBtn, UpgradeButton.UnlockState state)
    {
      upgradeBtn.State = state;

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

            // SetHiddenIconColor(btn.Value.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));
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

    public static object _lock = new object();
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
          RenderGuiSystem.itemsToUpdate.Remove(window.Visual);
        }

        window = new Window();

        Console.WriteLine("Upgrades JSON reloaded");
        CurrentUpgrades.LoadFromJson(jsonUpgrades, jsonButtons);

        window.Width = CurrentUpgrades.WindowWidth;
        window.Height = CurrentUpgrades.WindowHeight;

        var vis = window.Visual as WindowVisual;
        vis.Background.Color = new Color(200, 0, 0, 0);

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

          // vis.Background.Texture
          Console.WriteLine("Set upgrade window background texture");

          UG.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);

          if (btnData.Value.Data.ShortName != "R")
          {
            if (btnData.Value.Data.UpgradeDefinition.Type == "float")
              UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeDefinition.ShortName].BaseValue));
            else if (btnData.Value.Data.UpgradeDefinition.Type == "int")
              UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, int.Parse(CurrentUpgrades.UpgradeDefinitions[btnData.Value.Data.UpgradeDefinition.ShortName].BaseValue));
          }
        }

        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          if (string.IsNullOrEmpty(btnData.Value.Data.LockedBy) &&
              string.IsNullOrEmpty(btnData.Value.Data.HiddenBy) &&
              string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
          {
            // btnData.Value.Button.Visual.IsEnabled = true;
            // btnData.Value.Button.Visual.Visible = true;
            // SetBorderColor(btnData.Value.Button.Visual as ButtonVisual, new Color(0, 255, 0, 255));
            // SetHiddenIconColor(btnData.Value.Button.Visual as ButtonVisual, new Color(255, 255, 255, 0));

            SetButtonState(btnData.Value, UpgradeButton.UnlockState.Unlocked);
          }

          if (!string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
          {
            var blockedBy = CurrentUpgrades.UpgradeButtons[btnData.Value.Data.BlockedBy];
            if (blockedBy != null)
            {
              float startX = blockedBy.Data.PosX + blockedBy.Button.Width / 2.0f;
              float startY = blockedBy.Data.PosY + blockedBy.Button.Height / 2.0f;
              float endX = btnData.Value.Data.PosX + btnData.Value.Button.Width / 2.0f;
              float endY = btnData.Value.Data.PosY + btnData.Value.Button.Height / 2.0f;

              var midPoints = new List<Vector2>();

              if (Math.Abs(startX - endX) > 5.0f && Math.Abs(startY - endY) > 5.0f && btnData.Value.Data.AddMidPoint)
              {
                midPoints.Add(new Vector2(endX, startY));
              }

              CurrentUpgrades.UpgradeJoints.Add(btnData.Key, new UpgradeJoint
              {
                ToUpgradeId = btnData.Key,
                Start = new Vector2(startX, startY),
                End = new Vector2(endX, endY),
                MidwayPoints = midPoints,
              });

              Console.WriteLine($"Added upgrade joint from {new Vector2(startX, startY)} to {new Vector2(endX, endY)}");
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
                var p = g.OrderByDescending(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.End.X).Where(j => j.End.X > j.Start.X);
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

                var p2 = g.OrderBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.End.X).Where(j => j.End.X < j.Start.X);
                for (int i = 0; i < p2.Count(); i++)
                {
                  var gg = p2.ElementAt(i);

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

          // json += @$"    {{" + Environment.NewLine +
          //            // $@"      ""name"":""{btn.Value.Data.Name}""," + Environment.NewLine +
          //            // $@"      ""propname"":""{btn.Value.Data.PropertyName}""," + Environment.NewLine +
          //            $@"      ""shortname"":""{btn.Value.Data.ShortName}""," + Environment.NewLine +
          //            $@"      ""upgrade"":""{btn.Value.Data.UpgradeDefinition.ShortName}""," + Environment.NewLine +
          //            $@"      ""hiddenby"":""{btn.Value.Data.HiddenBy}""," + Environment.NewLine +
          //            $@"      ""lockedby"":""{btn.Value.Data.LockedBy}""," + Environment.NewLine +
          //            $@"      ""blockedby"":""{btn.Value.Data.BlockedBy}""," + Environment.NewLine +
          //            $@"      ""cost"":""{btn.Value.Data.Cost}""," + Environment.NewLine +
          //            $@"      ""posx"":""{btn.Value.Data.PosX}""," + Environment.NewLine +
          //            $@"      ""posy"":""{btn.Value.Data.PosY}""," + Environment.NewLine +
          //            $@"      ""value"":""{value}""" + Environment.NewLine +
          //            $@"    }}," + Environment.NewLine;
          //

          ImGui.Text($"Teeeeeeest");

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

            //TODO: Dropdown of other upgrades
            ImGui.InputText("HiddenBy", ref b.Data.HiddenBy, 10);
            ImGui.InputText("LockedBy", ref b.Data.LockedBy, 10);
            ImGui.InputText("BlockedBy", ref b.Data.BlockedBy, 10);

            b.Button.X = b.Data.PosX;
            b.Button.Y = b.Data.PosY;

            SetButtonState(b, UpgradeButton.UnlockState.SelectedInEditorMode);
            // SetBorderColor(b.Button.Visual as ButtonVisual, new Color(255, 255, 255, 255));
          }

          FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"EDIT MODE ENABLED", new Vector2(10, 0), Color.Yellow, Color.Black, 35);

        }
      });
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
            Upgrade(button.Name, upgradeBtn.Data);
          }
        }
      }
    }

    private void Upgrade(string upgradeName, UpgradeData upgradeData)
    {
      Console.WriteLine("Upgrade: " + upgradeName);
      m_gameState.CurrentGemCount -= upgradeData.Cost;

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

          var joint = CurrentUpgrades.UpgradeJoints[btn.Value.Data.ShortName];
          joint.State = UpgradeJoint.JointState.Unlocked;
        }
        if (btn.Value.Data.LockedBy == upgradeName)
        {
          var joint = CurrentUpgrades.UpgradeJoints[btn.Value.Data.ShortName];
          joint.State = UpgradeJoint.JointState.Unlocked;

          SetButtonState(btn.Value, UpgradeButton.UnlockState.Revealed);
        }
        if (btn.Value.Data.BlockedBy == upgradeName)
        {
          btn.Value.Button.Visual.IsEnabled = true;

          var joint = CurrentUpgrades.UpgradeJoints[btn.Value.Data.ShortName];
          joint.State = UpgradeJoint.JointState.Unlocked;
          SetButtonState(btn.Value, UpgradeButton.UnlockState.Unlocked);
        }
      }

      if (CurrentUpgrades.UpgradeJoints.TryGetValue(upgradeName, out var j))
      {
        j.State = UpgradeJoint.JointState.Purchased;
      }

      SetButtonState(CurrentUpgrades.UpgradeButtons[upgradeName], UpgradeButton.UnlockState.Purchased);
    }

    private readonly Tweener _tweener = new Tweener();
    private string prevOverButtonName = "";
    private Window m_tooltipWindow;
    private FontStashSharpText m_tooltipLabel;
    public void Update(GameTime gameTime)
    {
      var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      _tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      var buttonVis = GumService.Default.Cursor.WindowOver as ButtonVisual;

      // if (buttonVis == null && curOverButtonName != "null")
      // {
      //   return;
      // }

      // if (!string.IsNullOrEmpty(w))
      {
        if (curOverButtonName != prevOverButtonName)
        {
          if (buttonVis != null)
          {
            Console.WriteLine("Over upgrade button: " + curOverButtonName);

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

            ShowTooltip(buttonVis, curOverButtonName);
          }
        }

        // if (curOverButtonName == "null" && prevOverButtonName != "null")
        if (curOverButtonName != prevOverButtonName && curOverButtonName != buttonVis?.Name)
        {
          HideTooltip();
        }

        prevOverButtonName = curOverButtonName;
      }

    }

    private void HideTooltip()
    {
      if (m_tooltipWindow != null)
      {
        m_tooltipWindow.IsVisible = false;
      }
    }

    private void CreateToolTipWindow()
    {
      m_tooltipWindow = new Window()
      {
        Name = "UpgradeTooltipWindow",
      };

      var vis = m_tooltipWindow.Visual as WindowVisual;
      m_tooltipWindow.Width = 300;
      m_tooltipWindow.Height = 200;


      vis.Background.Color = new Color(0, 0, 0, 0);

      // m_tooltipLabel = new Label()
      // {
      // };
      //

      m_tooltipLabel = new FontStashSharpText()
      {
        TextAlignment = TextAlignment.Center
      };

      var m_tooltipLabelContainer = new GraphicalUiElement(m_tooltipLabel);

      var stackPanel = new StackPanel()
      {
      };

      m_tooltipLabelContainer.XOrigin = HorizontalAlignment.Center;
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

      var text2 = new FontStashSharpText()
      {
        Text = "Additional info can go here. lol 123 lorem ipsum dolor sit amet consectetur adipiscing elit",
        WrapText = true
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
      var gumObject = new GraphicalUiElement(text2)
      {
        XOrigin = HorizontalAlignment.Left,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        X = 20,
        Y = 10,
      };

      background.AddChild(border);
      background.AddChild(stackPanel);

      stackPanel.AddChild(m_tooltipLabelContainer);
      stackPanel.AddChild(r);
      // stackPanel.AddChild(text);
      stackPanel.AddChild(gumObject);

      m_tooltipWindow.AddChild(background);

      // m_tooltipWindow.Visual.XOrigin = RenderingLibrary.Graphics.HorizontalAlignment.Center;
      m_tooltipWindow.AddToRoot();
      m_tooltipWindow.Visual.AddToManagers(GumService.Default.SystemManagers, RenderGuiSystem.m_upgradesLayer);
      RenderGuiSystem.itemsToUpdate.Add(m_tooltipWindow.Visual);
    }

    private void ShowTooltip(ButtonVisual buttonVis, string buttonName)
    {
      if (m_tooltipWindow == null)
      {
        CreateToolTipWindow();
      }

      var targetPosY = buttonVis.Y + 60;

      m_tooltipLabel.Text = "Tooltip for " + buttonName;
      m_tooltipWindow.IsVisible = true;
      m_tooltipWindow.X = buttonVis.X - m_tooltipWindow.Width / 2 + buttonVis.Width / 2;
      // m_tooltipWindow.Y = buttonVis.Y;
      m_tooltipWindow.Y = targetPosY;

      m_tooltipWindow.Height = 0;

      // _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Y, toValue: targetPosY, duration: 0.25f)
      //                 .Easing(EasingFunctions.CubicOut);


      _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 300, duration: 0.25f)
                      .Easing(EasingFunctions.CubicOut);

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
