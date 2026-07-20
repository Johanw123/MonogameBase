using System;
using System.Collections.Generic;
using AsyncContent;
using Microsoft.Xna.Framework;
using System.Text.Json;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tweening;
using Gum.Wireframe;
using RenderingLibrary.Graphics;
using System.IO;
using ImGuiNET;
using RenderingLibrary;
using MonoGame.Extended.Input;
using MonoGame.Extended.Graphics;
using JapeFramework.Aseprite;
using UntitledGemGame.Entities;
using JapeFramework;
using System.Globalization;
using Gum.GueDeriving;
using Gum.Forms.Controls;
using Gum.Forms;
using Gum.Forms.DefaultVisuals;
using Serilog.Core;
using Serilog;
using GUI.Shared.Helpers;
using System.Text.RegularExpressions;

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
      Invisible,
      Hidden,
      Revealed,
      Unlocked,
      Purchased,

      SelectedInEditorMode,
      HoveredInEditorMode
    }

    public UnlockState State = UnlockState.Invisible;

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
    public bool SwapMidPointAxis;

    public float ButtonSizeScale = 1.0f;

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
      Unlocking,
      Unlocked,
      Purchasing,
      Purchased
    }

    public float UnlockingTime = 0.0f;
    public float PurchasingTime = 0.0f;

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
    // For every x distance launch a minidrone from a drone.
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

    public static AsyncAsset<string> JsonUpgradesAsset;
    public static AsyncAsset<string> JsonUpgradeButtonsAsset;

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

        // Console.WriteLine($"Loading upgrade button: {btn.Shortname} of type {upDef.Type} with value {btn.Value}");

        dynamic value;

        try
        {
          if (upDef.Type == "int")
          {
            value = int.Parse(btn.Value);
          }
          else if (upDef.Type == "float")
          {
            value = float.Parse(btn.Value, CultureInfo.InvariantCulture);
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
        }
        catch
        {
          Console.WriteLine($"Parsing failed: {btn.Value} - {upDef.Name} - {upDef.Type} - {btn.Shortname}");
          continue;
        }

        if (UpgradeButtons.ContainsKey(btn.Shortname))
        {
          var newName = btn.Shortname;
          int count = 1;
          while (UpgradeButtons.ContainsKey(newName))
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
          AddMidPoint = bool.Parse(btn.AddMidPoint),
          SwapMidPointAxis = bool.Parse(btn.SwapMidPointAxis),
          ButtonSizeScale = float.Parse(btn.ButtonSizeScale, CultureInfo.InvariantCulture)
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
          "float" => btn.Value.Data.m_upgradeAmountFloat.ToString(CultureInfo.InvariantCulture),
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
                   $@"      ""posx"":""{btn.Value.Data.PosX}""," + Environment.NewLine +
                   $@"      ""posy"":""{btn.Value.Data.PosY}""," + Environment.NewLine +
                   $@"      ""addmidpoint"":""{btn.Value.Data.AddMidPoint}""," + Environment.NewLine +
                   $@"      ""swapmidpointaxis"":""{btn.Value.Data.SwapMidPointAxis}""," + Environment.NewLine +
                   $@"      ""buttonsizescale"":""{btn.Value.Data.ButtonSizeScale.ToString(CultureInfo.InvariantCulture)}""," + Environment.NewLine +
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

      AssetManager.ReloadAsset(JsonUpgradeButtonsAsset);
    }

    public void LoadValues()
    {
    }

    public void SaveValues()
    {
    }

    public UpgradeButton AddNewButton(string shortName, JsonUpgrade upgradeDef = null)
    {
      var camera = SystemManagers.Default.Renderer.Camera;
      camera.ScreenToWorld(0, 0, out float screenX, out float screenY);

      if (upgradeDef == null)
      {
        upgradeDef = new JsonUpgrade
        {
          ShortName = shortName,
          Name = "New Upgrade",
          Type = "int",
          BaseValue = "0"
        };
      }

      var upgrade = new UpgradeData(shortName, 0)
      {
        UpgradeDefinition = upgradeDef,
        Cost = 0,
        PosX = (int)screenX,
        PosY = (int)screenY,
        HiddenBy = "",
        LockedBy = "",
        BlockedBy = "",
        AddMidPoint = false
      };

      UpgradeButtons.Add(shortName, new UpgradeButton
      {
        Button = null,
        Data = upgrade
      });

      return UpgradeButtons[shortName];
    }
  }

  public partial class UpgradeManager
  {
    public static Upgrades CurrentUpgrades = new();


    public event Action OnUpgradeRoot;
    public event Action<string> OnUpgrade;

    private GameState m_gameState;
    private Window window;

    public static bool UpgradeGuiEditMode = false;
    public static bool UpdatingButtons = false;

    public static UpgradesGenerator UG = new();

    private void SetBorderColor(InteractiveGue buttonVis, Color color)
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

    private void SetBackgroundColor(InteractiveGue buttonVis, Color color)
    {
      if (buttonVis.Children.Count > 0)
      {
        var backgroundSprite = buttonVis.Children[0] as SpriteRuntime;
        if (backgroundSprite != null)
        {
          backgroundSprite.Color = color;
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

      SetIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));
      SetBackgroundColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));

      Color borderColorHidden = new Color(204, 62, 62, 255);
      Color borderColorUnlocked = new Color(29, 188, 96, 255);
      Color borderColorPurchased = new Color(75, 128, 177, 255);
      // private Color greenColor = new Color(29, 188, 96);
      // private Color redColor = new Color(204, 62, 62, 255);


      Console.WriteLine($"Setting Button {upgradeBtn.Data.ShortName}: " + state);

      switch (state)
      {
        case UpgradeButton.UnlockState.Invisible:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
            SetBorderColor(upgradeBtn.Button.Visual, new Color(0, 0, 0, 0));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
            SetBackgroundColor(upgradeBtn.Button.Visual, new Color(0, 0, 0, 0));
          }
          break;
        case UpgradeButton.UnlockState.Hidden:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            // SetBorderColor(upgradeBtn.Button.Visual, borderColorHidden);
            SetBorderColor(upgradeBtn.Button.Visual, new Color(255, 0, 0, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));
          }
          break;
        case UpgradeButton.UnlockState.Revealed:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual, new Color(99, 99, 99, 255));
            // SetBorderColor(upgradeBtn.Button.Visual, borderColorUnlocked);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Unlocked:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual, borderColorUnlocked);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Purchased:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual, borderColorPurchased);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.SelectedInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual, new Color(255, 0, 255, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.HoveredInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn.Button.Visual, new Color(255, 180, 10, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
      }
    }

    private void SetHiddenIconColor(InteractiveGue buttonVis, Color color)
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

    private void SetIconColor(InteractiveGue buttonVis, Color color)
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

    private Button CreateButton(KeyValuePair<string, UpgradeButton> btnData)
    {
      float width = 50;
      float height = 50;

      // if(btnData.Value.Data.UpgradeDefinition.ShortName == "BG")
      // {
      //   width = 100;
      //   height = 100;
      // }
      //

      width *= btnData.Value.Data.ButtonSizeScale;
      height *= btnData.Value.Data.ButtonSizeScale;

      var button = new Button
      {
        Text = "",
        Width = width,
        Height = height,
        X = btnData.Value.Data.PosX,
        Y = btnData.Value.Data.PosY,
        Name = btnData.Key,
        // Visual = new ButtonVisual(false, false)
      };

      button.Visual.WidthUnits = Gum.DataTypes.DimensionUnitType.ScreenPixel;
      button.Visual.HeightUnits = Gum.DataTypes.DimensionUnitType.ScreenPixel;
      button.Visual.XOrigin = HorizontalAlignment.Left;
      button.Visual.YOrigin = VerticalAlignment.Top;
      button.Visual.IsEnabled = false;
      button.Visual.Visible = true;
      button.Click += (s, e) => UpgradeClicked(s, e);
      btnData.Value.Button = button;
      window.AddChild(button);

      Texture2D icon;
      var iconPath = btnData.Value.Data.UpgradeDefinition.Icon;
      if (iconPath == "")
        iconPath = "Textures/GUI/icon.png";

      icon = AssetManager.Load<Texture2D>(iconPath);
      var buttonVis = button.Visual;

      var border = AssetManager.Load<Texture2D>("Textures/GUI/border.png");
      var iconHidden = AssetManager.Load<Texture2D>("Textures/GUI/iconHidden.png");
      // var iconInvisible = AssetManager.Load<Texture2D>("Textures/GUI/iconHidden.png");
      var background = AssetManager.Load<Texture2D>("Textures/GUI/icon_background.png");

      buttonVis.Children.Clear();

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "BackgroundSprite",
        Texture = background,
        Color = new Color(255, 255, 255, 255),
        Width = width,
        Height = height,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XOrigin = HorizontalAlignment.Center,
        YOrigin = VerticalAlignment.Center,
        X = width / 2.0f,
        Y = height / 2.0f
      });

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "IconSprite",
        Texture = icon,
        Width = width - 10,
        Height = height - 10,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XOrigin = HorizontalAlignment.Center,
        YOrigin = VerticalAlignment.Center,
        X = width / 2.0f,
        Y = height / 2.0f
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
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        Width = width,
        Height = height,
      });


      // buttonVis.Children.Add(new ButtonBorderShape()
      // {
      //   Name = "BorderShape",
      //   Color = new Color(255, 255, 255, 255),
      // });

      // buttonVis.States.Disabled.Apply = () =>
      // {
      // };
      //
      // buttonVis.States.Focused.Apply = () =>
      // {
      //   // buttonVis.Background.Color = new Color(255, 255, 255, 255);
      // };
      //
      // buttonVis.States.Highlighted.Apply = () =>
      // {
      //   // buttonVis.Background.Color = new Color(255, 255, 255, 255);
      //   // buttonVis.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      // };
      //
      // buttonVis.States.HighlightedFocused.Apply = () =>
      // {
      //   // buttonVis.Background.Color = new Color(255, 255, 255, 255);
      //   // buttonVis.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      // };
      //
      // buttonVis.States.Pushed.Apply = () =>
      // {
      //   // buttonVis.Background.Color = new Color(255, 255, 255, 255);
      // };
      //
      // buttonVis.States.Enabled.Apply = () =>
      // {
      //   // buttonVis.Background.Color = new Color(255, 255, 255, 255);
      //   // buttonVis.Background.Texture = TextureCache.RefuelButtonBackground;
      // };
      //
      // buttonVis.States.DisabledFocused.Apply = () =>
      // {
      // };

      return button;
    }

    private void RefreshButtons(string jsonUpgrades, string jsonButtons)
    {
      lock (_lock)
      {
        var camera = SystemManagers.Default.Renderer.Camera;
        camera.Zoom = 1.0f;
        RenderGuiSystem.Instance.targetZoom = 1.0f;
        //TODO: zoom level affects buttons hover when buttons are refreshed (try zooming in/out in upgrades menu and press F5)
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
          RenderGuiSystem.Instance.skillTreeItems.Remove(window.Visual);
        }

        window = new Window();

        Console.WriteLine("Upgrades JSON reloaded");
        CurrentUpgrades.LoadFromJson(jsonUpgrades, jsonButtons);

        // window.X = -1000;
        // window.Y = -CurrentUpgrades.WindowHeight / 2;
        window.Width = CurrentUpgrades.WindowWidth / 2;
        window.Height = CurrentUpgrades.WindowHeight / 2;

        // var vis = window.Visual as WindowVisual;
        var vis = window.Visual;

        if (vis == null)
        {
          Log.Error("Couldnt get window visual");
          return;
        }

        // vis.Background.Color = new Color(0, 0, 0, 0);

        var tex = AssetManager.Load<Texture2D>("Textures/blue_pixel.png");
        var sprite = new NineSliceRuntime()
        {
          Texture = tex,
          Width = 2028,
          Height = window.Height,
          TextureAddress = Gum.Managers.TextureAddress.EntireTexture
        };

        window.AddChild(sprite);

        var tex2 = AssetManager.Load<Texture2D>("Textures/red_pixel.png");
        var sprite2 = new NineSliceRuntime()
        {
          Texture = tex2,
          Width = window.Width - 2028,
          X = 2028,
          Height = window.Height,
          TextureAddress = Gum.Managers.TextureAddress.EntireTexture
        };

        window.AddChild(sprite2);


        foreach (var btnData in CurrentUpgrades.UpgradeButtons)
        {
          CreateButton(btnData);
          SetButtonState(btnData.Value, UpgradeButton.UnlockState.Invisible);
          // vis.Background.Texture
          // Console.WriteLine("Set upgrade window background texture");

          UG.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);

          if (btnData.Value.Data.ShortName != "HB")
          {
            var b = CurrentUpgrades.UpgradeDefinitions.TryGetValue(btnData.Value.Data.UpgradeDefinition.ShortName, out var upDef);
            if (b)
            {
              if (btnData.Value.Data.UpgradeDefinition.Type == "float")
                UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(upDef.BaseValue, CultureInfo.InvariantCulture));
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
                if (btnData.Value.Data.SwapMidPointAxis)
                  midPoints.Add(new Vector2(startX, endY));
                else
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

              // Console.WriteLine($"Added upgrade joint from {new Vector2(startX, startY)} to {new Vector2(endX, endY)}");
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
            var startPointGroupingX = startPoints.GroupBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.EndButton.Data.PosX).ToList();

            foreach (var g in startPointGroupingY)
            {
              if (g.Count() > 1)
              {
                var p = g.OrderByDescending(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().X : j.EndButton.Data.PosX).Where(j => j.EndButton.Data.PosX > j.StartButton.Data.PosX);
                for (int i = 0; i < p.Count(); i++)
                {
                  var gg = p.ElementAt(i);

                  float offset = 15.0f;
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

            float offsetSpacing = 15.0f;

            foreach (var g in startPointGroupingX)
            {
              if (g.Count() > 1)
              {
                var p = g.OrderBy(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().Y : j.EndButton.Data.PosY).Where(j => j.EndButton.Data.PosY > j.StartButton.Data.PosY);

                float startOffset = -((p.Count() - 1) * offsetSpacing) / 2.0f;
                for (int i = 0; i < p.Count(); i++)
                {
                  var gg = p.ElementAt(i);
                  float offset = startOffset + i * offsetSpacing;

                  gg.StartOffset.X += offset;

                  for (int j = 0; j < gg.MidwayPoints.Count; j++)
                  {
                    Vector2 mp = gg.MidwayPoints[j];
                    mp.X += offset;
                    gg.MidwayPoints[j] = mp;
                  }
                }

                var p2 = g.OrderByDescending(j => j.MidwayPoints.Any() ? j.MidwayPoints.First().Y : j.EndButton.Data.PosY).Where(j => j.EndButton.Data.PosY < j.StartButton.Data.PosY);
                for (int i = 0; i < p2.Count(); i++)
                {
                  var gg = p2.ElementAt(i);
                  float offset = startOffset + i * offsetSpacing;

                  gg.StartOffset.X += offset;

                  for (int j = 0; j < gg.MidwayPoints.Count; j++)
                  {
                    Vector2 mp = gg.MidwayPoints[j];
                    mp.X += offset;
                    gg.MidwayPoints[j] = mp;
                  }
                }
              }
            }
          }
        }

        window.Visual.AddToManagers(MonoGameGum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_upgradesLayer);
        RenderGuiSystem.Instance.skillTreeItems.Add(window.Visual);

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

      // var camera = SystemManagers.Default.Renderer.Camera;
      // var hb = CurrentUpgrades.UpgradeButtons["HB"].Button;
      // Console.WriteLine("Centering camera on HB button at position: " + new Vector2(hb.X, hb.Y));
      // camera.Position = new System.Numerics.Vector2(.X, CurrentUpgrades.UpgradeButtons["HB"].Button.Y);


      UpdatingButtons = false;
    }

    private string jsonUpgrades = "";
    private string jsonUpgradeButtons = "";

    private UpgradeButton m_selectedButtonEditMode = null;
    private UpgradeButton m_selectedButtonEditMode2 = null;

    public void Init(GameState gameState)
    {
      RenderGuiSystem.Instance.targetZoom = 1.0f;
      Upgrades.JsonUpgradesAsset = AssetManager.LoadAsync<string>("Data/upgrades.json", false, UpdateJsonUpgrades, UpdateJsonUpgrades);
      Upgrades.JsonUpgradeButtonsAsset = AssetManager.LoadAsync<string>(ContentDirectory.Data.upgrades_buttons_json, false, UpdateJsonUpgradeButtons, UpdateJsonUpgradeButtons);

      CurrentUpgrades = new();
      UG = new();

      UpgradeGuiEditMode = false;
      UpdatingButtons = false;

      m_gameState = gameState;

      GameMain.AddCustomImGuiContent(DrawImGuiContent);
    }

    public void Finish()
    {
      GameMain.RemoveCustomImGuiContent(DrawImGuiContent);

      UpgradeGuiEditMode = false;
      UpdatingButtons = false;

      if (window != null)
      {
        window.Visual.RemoveFromManagers();
        RenderGuiSystem.Instance.skillTreeItems.Remove(window.Visual);
      }
    }

    private void DrawImGuiContent()
    {
      if (!Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
        return;
      if (!Upgrades.JsonUpgradesAsset.IsLoaded)
        return;
      if (UpdatingButtons)
        return;

      if (UpgradeGuiEditMode)
      {
        var b = m_selectedButtonEditMode;

        if (b != null)
        {
          foreach (var btn in CurrentUpgrades.UpgradeButtons)
          {
            if (btn.Value.State != UpgradeButton.UnlockState.Unlocked && btn.Value != b)
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

          if (setAny)
          {
            foreach (var btn in CurrentUpgrades.UpgradeButtons)
            {
              SetButtonState(btn.Value, UpgradeButton.UnlockState.HoveredInEditorMode);
            }

            if (m_selectedButtonEditMode2 != null)
            {
              if (setLockedBy)
                b.Data.LockedBy = m_selectedButtonEditMode2.Data.ShortName;
              if (setBlockedBy)
                b.Data.BlockedBy = m_selectedButtonEditMode2.Data.ShortName;
              if (setHiddenBy)
                b.Data.HiddenBy = m_selectedButtonEditMode2.Data.ShortName;

              setHiddenBy = false;
              setLockedBy = false;
              setBlockedBy = false;
              m_selectedButtonEditMode2 = null;
            }
          }

          AddCombo("HiddenBy", ref b.Data.HiddenBy);
          ImGui.SameLine();
          if (ImGui.Button("Set H"))
            setHiddenBy = true;

          AddCombo("LockedBy", ref b.Data.LockedBy);
          ImGui.SameLine();
          if (ImGui.Button("Set L"))
            setBlockedBy = true;

          AddCombo("BlockedBy", ref b.Data.BlockedBy);
          ImGui.SameLine();
          if (ImGui.Button("Set B"))
            setBlockedBy = true;

          ImGui.Checkbox("Add MidPoint", ref b.Data.AddMidPoint);
          ImGui.Checkbox("Swap Midpoint Axis", ref b.Data.SwapMidPointAxis);

          ImGui.InputFloat("ButtonSizeScale", ref b.Data.ButtonSizeScale);

          b.Button.X = b.Data.PosX;
          b.Button.Y = b.Data.PosY;

          if (b.State != UpgradeButton.UnlockState.SelectedInEditorMode)
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
            var button = CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));
          }

          ImGui.Button("Remove Button");
          if (ImGui.IsItemClicked())
          {
            if (CurrentUpgrades.UpgradeButtons.TryGetValue(b.Data.ShortName, out var removeButton))
            {
              removeButton.Button.RemoveFromRoot();
              CurrentUpgrades.UpgradeButtons.Remove(b.Data.ShortName);
            }

            // CurrentUpgrades.AddNewButton(newShortName);
            // var button = CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));
          }
        }

        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"EDIT MODE ENABLED", new Vector2(10, 0), Color.Yellow, Color.Black, 35);
      }
    }

    private bool setHiddenBy = false;
    private bool setLockedBy = false;
    private bool setBlockedBy = false;

    private bool setAny => setHiddenBy || setLockedBy || setBlockedBy;

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
              SetBorderColor(btn.Value.Button.Visual, new Color(0, 0, 0, 0));
              SetIconColor(btn.Value.Button.Visual, new Color(255, 255, 255, 50));
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
            if (setAny)
            {
              m_selectedButtonEditMode2 = upgradeBtn;
            }
            else
            {
              m_selectedButtonEditMode = upgradeBtn;
            }
          }
          else
          {
            Upgrade(button, upgradeBtn.Data);
          }
        }
      }
    }

    private void Unlock(UpgradeButton endButton, UpgradeJoint pJoint, string upgradeName, int delayTimeMS)
    {
      //TODO: use a good tweener to increase joint Animation value or make TimerHelper work with monogame deltatime to get each tick as callback
      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value == endButton)
        {
          if (btn.Value.Data.HiddenBy == upgradeName)
          {
            btn.Value.Button.Visual.Visible = true;
          }

          if (btn.Value.Data.BlockedBy == upgradeName)
          {
            pJoint.State = delayTimeMS > 0 ? UpgradeJoint.JointState.Unlocking : UpgradeJoint.JointState.Unlocked;
            TimerHelper.DoAfter(() =>
                {
                  SetButtonState(endButton, UpgradeButton.UnlockState.Unlocked);

                  foreach (var joint in CurrentUpgrades.UpgradeJoints)
                  {
                    if (joint.Value.StartButton.Button == endButton.Button)
                    {
                      // joint.Value.State = UpgradeJoint.JointState.Unlocking;
                      Unlock(joint.Value.EndButton, joint.Value, upgradeName, 0);
                    }
                  }

                }, delayTimeMS, true);
          }
          else if (btn.Value.Data.LockedBy == upgradeName)
          {
            pJoint.State = delayTimeMS > 0 ? UpgradeJoint.JointState.Unlocking : UpgradeJoint.JointState.Unlocked;
            TimerHelper.DoAfter(() =>
                {
                  SetButtonState(endButton, UpgradeButton.UnlockState.Revealed);

                  foreach (var joint in CurrentUpgrades.UpgradeJoints)
                  {
                    if (joint.Value.StartButton.Button == endButton.Button)
                    {
                      // joint.Value.State = UpgradeJoint.JointState.Unlocking;
                      Unlock(joint.Value.EndButton, joint.Value, upgradeName, 0);
                    }
                  }

                }, delayTimeMS, true);
          }

        }
      }
    }

    private void Upgrade(Button button, UpgradeData upgradeData)
    {
      ulong currentValue = upgradeData.UpgradeDefinition.Currency switch
      {
        "red" => m_gameState.CurrentRedGemCount,
        "blue" => m_gameState.CurrentBlueGemCount,
        _ => 0
      };

      if (currentValue < (uint)upgradeData.Cost)
      {
        Console.WriteLine("Not enough gems to purchase upgrade: " + upgradeData.ShortName);

        //TODO: Play error sound
        // AudioManager.Instance.MenuHoverButtonSoundEffect?.Play();

        return;
      }

      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);

      string upgradeName = upgradeData.ShortName;

      Console.WriteLine("Upgrade: " + upgradeName);
      switch (upgradeData.UpgradeDefinition.Currency)
      {
        case "red":
          m_gameState.CurrentRedGemCount -= (uint)upgradeData.Cost;
          break;
        case "blue":
          m_gameState.CurrentBlueGemCount -= (uint)upgradeData.Cost;
          break;
      }

      if (upgradeName == "HB")
      {
        OnUpgradeRoot?.Invoke();
      }

      if (upgradeName == "RBG1")
      {
        m_gameState.CurrentBlueGemCount = 0;
        foreach (var ub in CurrentUpgrades.UpgradeButtons)
        {
          var ud = ub.Value.Data.UpgradeDefinition;

          if (ub.Value.State == UpgradeButton.UnlockState.Purchased && ud.ShortName == "BG")
          {
            m_gameState.CurrentBlueGemCount += (uint)ub.Value.Data.m_upgradeAmountInt;
          }

          if (ud.Currency != "blue") continue;

          UG.Reset(ud.ShortName);

          bool f = CurrentUpgrades.UpgradeButtons.TryGetValue(ub.Value.Data.ShortName, out var v);
          if (f)
          {
            Console.WriteLine("Found: " + ub.Value.Data.ShortName);
            if (ub.Value.Data.UpgradeDefinition.ShortName == "HBC")
            {
              SetButtonState(ub.Value, UpgradeButton.UnlockState.Unlocked);
            }
            else
            {
              SetButtonState(ub.Value, UpgradeButton.UnlockState.Invisible);
            }

            foreach (var l in CurrentUpgrades.UpgradeJoints)
            {
              if (l.Value.StartButton == ub.Value)
              {
                l.Value.State = UpgradeJoint.JointState.Hidden;
                l.Value.UnlockingTime = 0;
                l.Value.PurchasingTime = 0;
              }

              if (l.Value.StartButton.Data.UpgradeDefinition.ShortName == "HB")
              {
                l.Value.State = UpgradeJoint.JointState.Unlocked;
                l.Value.UnlockingTime = 0;
                l.Value.PurchasingTime = 0;
              }
            }
          }
          else
          {
            Console.WriteLine("Not Found: " + ub.Value.Data.ShortName);
          }
        }

        HomeBase.Instance.ResetAbilities();
        return;
      }

      OnUpgrade?.Invoke(upgradeName);

      if (upgradeData.UpgradeDefinition.ShortName == "BG")
      {
        m_gameState.CurrentBlueGemCount += (uint)upgradeData.m_upgradeAmountInt;
      }

      if (upgradeData.UpgradeDefinition.Type == "float")
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradeAmountFloat);
      else if (upgradeData.UpgradeDefinition.Type == "int")
        UG.Increment(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradeAmountInt);
      else if (upgradeData.UpgradeDefinition.Type == "bool")
        UG.Set(upgradeData.UpgradeDefinition.ShortName, upgradeData.m_upgradesToBool);


      foreach (var joint in CurrentUpgrades.UpgradeJoints)
      {
        if (joint.Value.StartButton.Button == button)
        {
          Unlock(joint.Value.EndButton, joint.Value, upgradeName, 200);
        }
      }

      //TODO: do animation here for when unlocking new buttons etc
      // foreach (var btn in CurrentUpgrades.UpgradeButtons)
      // {
      //   if (btn.Value.Data.HiddenBy == upgradeName)
      //   {
      //     btn.Value.Button.Visual.Visible = true;
      //
      //     CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
      //     if (joint != null)
      //       joint.State = UpgradeJoint.JointState.Unlocking;
      //   }
      //   if (btn.Value.Data.LockedBy == upgradeName)
      //   {
      //     CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
      //     if (joint != null)
      //       joint.State = UpgradeJoint.JointState.Unlocking;
      //
      //     SetButtonState(btn.Value, UpgradeButton.UnlockState.Revealed);
      //   }
      //   if (btn.Value.Data.BlockedBy == upgradeName)
      //   {
      //     btn.Value.Button.Visual.IsEnabled = true;
      //
      //     CurrentUpgrades.UpgradeJoints.TryGetValue(btn.Value.Data.ShortName, out var joint);
      //     if (joint != null)
      //       joint.State = UpgradeJoint.JointState.Unlocking;
      //     SetButtonState(btn.Value, UpgradeButton.UnlockState.Unlocked);
      //   }
      // }

      if (CurrentUpgrades.UpgradeJoints.TryGetValue(upgradeName, out var j))
      {
        j.State = UpgradeJoint.JointState.Purchasing;

        TimerHelper.DoAfter(() =>
            {
              j.State = UpgradeJoint.JointState.Purchased;

              // foreach (var joint in CurrentUpgrades.UpgradeJoints)
              // {
              //   if (joint.Value.StartButton.Button == button)
              //   {
              //     Unlock(joint.Value.EndButton, joint.Value, upgradeName, 200);
              //   }
              // }

            }, 100, true);
      }

      SetButtonState(CurrentUpgrades.UpgradeButtons[upgradeName], UpgradeButton.UnlockState.Purchased);
      HideTooltip();
      ShowTooltip(button.Visual, button.Name, false);
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
    private FontStashSharpText m_tooltipValueTo;
    private FontStashSharpText m_tooltipPuchasedText;


    // private TextRuntime m_tooltipDescription;

    // private NineSliceRuntime m_tooltipValueIcon;
    private SpriteRuntime m_tooltipValueIcon;
    // private NineSliceRuntime m_tooltipCostIcon;
    private SpriteRuntime m_tooltipCostIconRed;
    private SpriteRuntime m_tooltipCostIconBlue;
    private UpgradeButton m_currentTooltipButton = null;


    private Color greenColor = new Color(29, 188, 96);
    private Color redColor = new Color(204, 62, 62, 255);


    public static List<GraphicalUiElement> m_tooltipValueElements = new();

    public void Update(GameTime gameTime)
    {
      if (UpdatingButtons)
        return;


      var ms = MouseExtended.GetState();
      var kb = KeyboardExtended.GetState();

      var curOverButtonName = MonoGameGum.GumService.Default.Cursor.VisualOver?.Name ?? "null";

      _tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      var buttonVis = MonoGameGum.GumService.Default.Cursor.VisualOver;

      // Console.WriteLine("c: " + curOverButtonName + " - p: " + buttonVis?.Parent?.Name + " - pp: " + buttonVis?.Parent?.Parent?.Name);
      bool isButton = buttonVis != null;

      foreach (var btn in CurrentUpgrades.UpgradeButtons)
      {
        if (btn.Value.Button == null)
          continue;

        var currency = btn.Value.Data.UpgradeDefinition.Currency;
        ulong gemCount = currency switch
        {
          "red" => m_gameState.CurrentRedGemCount,
          "blue" => m_gameState.CurrentBlueGemCount,
          _ => 0
        };

        var bv = btn.Value.Button.Visual;
        if ((uint)btn.Value.Data.Cost > gemCount && btn.Value.State == UpgradeButton.UnlockState.Unlocked)
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
        else if ((uint)btn.Value.Data.Cost <= gemCount && btn.Value.State == UpgradeButton.UnlockState.Unlocked)
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
              AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
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

          HideTooltip();

          if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.H))
          {
            setLockedBy = false;
            setBlockedBy = false;
            setHiddenBy = !setHiddenBy;
          }
          if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.L))
          {
            setHiddenBy = false;
            setBlockedBy = false;
            setLockedBy = !setLockedBy;
          }
          if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.B))
          {
            setHiddenBy = false;
            setLockedBy = false;
            setBlockedBy = !setBlockedBy;
          }

          if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
          {
            setBlockedBy = false;
            setLockedBy = false;
            setHiddenBy = false;
          }

          // if (curOverButtonName != "null" && curOverButtonName != null)
          // {
          //   if (kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
          //   {
          //     draggingButtonNameEditMode = curOverButtonName;
          //   }
          // }

          if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.M))
          {
            //Mode upgrade button
            if (draggingButtonNameEditMode == "" && curOverButtonName != "null" && curOverButtonName != null)
              draggingButtonNameEditMode = curOverButtonName;
            else
              draggingButtonNameEditMode = "";
          }
          else if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.C))
          {
            //Clone upgrade button
            var origShortName = m_selectedButtonEditMode.Data.ShortName;
            if (CurrentUpgrades.UpgradeButtons.TryGetValue(origShortName, out var origButton))
            {
              string newShortName = MyRegex().Replace(origShortName, match =>
              {
                int number = int.Parse(match.Value);
                return (number + 1).ToString();
              });
              var upgradeButton = CurrentUpgrades.AddNewButton(newShortName, origButton.Data.UpgradeDefinition);
              var button = CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));

              var camera = SystemManagers.Default.Renderer.Camera;
              var sp = BaseGame.BoxingViewportAdapter.PointToScreen(ms.X, ms.Y);
              camera.ScreenToWorld(sp.X, sp.Y, out var X2, out var Y2);

              button.X = X2;
              button.Y = Y2;

              upgradeButton.Data.PosX = (int)X2;
              upgradeButton.Data.PosY = (int)Y2;

              upgradeButton.Data.HiddenBy = origButton.Data.HiddenBy;
              upgradeButton.Data.LockedBy = origButton.Data.LockedBy;
              upgradeButton.Data.BlockedBy = origButton.Data.ShortName;

              upgradeButton.Data.Cost = origButton.Data.Cost;
              upgradeButton.Data.m_upgradeAmountFloat = origButton.Data.m_upgradeAmountFloat;
              upgradeButton.Data.m_upgradeAmountInt = origButton.Data.m_upgradeAmountInt;
              upgradeButton.Data.m_upgradesToBool = origButton.Data.m_upgradesToBool;

              draggingButtonNameEditMode = newShortName;
              m_selectedButtonEditMode = upgradeButton;
            }
          }

          // if (kb.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.LeftControl))
          // {
          //   draggingButtonNameEditMode = "";
          // }

          if (draggingButtonNameEditMode != "")
          {
            var camera = SystemManagers.Default.Renderer.Camera;
            // camera.ScreenToWorld(ms.X, ms.Y, out float X, out float Y);
            var sp = BaseGame.BoxingViewportAdapter.PointToScreen(ms.X, ms.Y);
            camera.ScreenToWorld(sp.X, sp.Y, out var X2, out var Y2);

            // Console.WriteLine($"{ms.X} - {X2} - {X}");

            //ms goes based on window size

            if (CurrentUpgrades.UpgradeButtons.TryGetValue(draggingButtonNameEditMode, out var button))
            {
              button.Button.X = X2;
              button.Button.Y = Y2;

              CurrentUpgrades.UpgradeButtons[draggingButtonNameEditMode].Data.PosX = (int)button.Button.X;
              CurrentUpgrades.UpgradeButtons[draggingButtonNameEditMode].Data.PosY = (int)button.Button.Y;
            }
          }
        }

      }


      // foreach (var a in HomeBase.Instance.AvailableAbilityButtons)
      // {
      //   var btn = a.Value;
      //
      //
      // }


      prevOverButtonName = curOverButtonName;
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

      var vis = m_tooltipWindow.Visual;
      m_tooltipWindow.Width = 480;
      m_tooltipWindow.Height = 350;

      // vis.Background.Color = new Color(0, 0, 0, 0);

      m_tooltipLabel = new FontStashSharpText()
      {
        TextAlignment = TextAlignment.Center,
        FontSize = 26
      };

      var m_tooltipLabelContainer = new GraphicalUiElement(m_tooltipLabel);

      var stackPanel = new StackPanel()
      {

      };

      // m_tooltipLabelContainer.XOrigin = HorizontalAlignment.Center;
      stackPanel.Visual.YOrigin = VerticalAlignment.Top;

      m_tooltipLabelContainer.XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle;
      m_tooltipLabelContainer.Y = -15;
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
        TextAlignment = TextAlignment.Left,
        FontSize = 28,
      };

      // m_tooltipDescription = new TextRuntime()
      // {
      //   Text = "Additional info can go here. lol 123 lorem ipsum dolor sit amet consectetur adipiscing elit",
      //   Wrap = true,
      //   FontSize = 25,
      //   XOrigin = HorizontalAlignment.Left,
      //   XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
      //   X = 20,
      //   Y = 10,
      // };

      var descriptionElement = new GraphicalUiElement(m_tooltipDescription)
      {
        XOrigin = HorizontalAlignment.Left,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        X = 20,
        Y = 30,

        // XOrigin = HorizontalAlignment.Center,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // X = 0,
        // Y = 30,
      };

      m_tooltipPuchasedText = new FontStashSharpText()
      {
        Text = "PURCHASED",
        FontSize = 30,
        Visible = false,
        FillColor = greenColor,
        TextAlignment = TextAlignment.Left
      };

      var purchasedElement = new GraphicalUiElement(m_tooltipPuchasedText)
      {
        XOrigin = HorizontalAlignment.Left,
        YOrigin = VerticalAlignment.Bottom,
        YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
        X = 15,
        Y = -15,
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
        Color = new Color(10, 10, 10, 0),
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 0,
        Y = 0,
        Width = 0,
        Height = 0,
      };

      var backgroundSprite = new NineSliceRuntime()
      {
        Texture = TextureCache.TooltipBackground,
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 0,
        Y = 0,
        Width = 0,
        Height = 0,
      };


      var toolTipTitleBackground = new NineSliceRuntime()
      {
        Texture = TextureCache.TooltipTitleBackground,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle,
        X = -91.5f * 2.0f,
        Y = -20,
        Width = 183 * 2.0f,
        Height = 20 * 2.0f,
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


      // var costTex = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
      // var costTex2 = AssetManager.Load<Texture2D>("Textures/Gems/Gem2GrayStatic.png");



      // gemSpriteRedHud = AsepriteHelper.LoadAnimation(
      //   "Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png",
      //   true,
      //   10,
      //   150);

      (Texture2D tex, Texture2DRegion region) red = AsepriteHelper.LoadTextureFromAnimationFrame("Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png", 0, 10);
      (Texture2D tex, Texture2DRegion region) blue = AsepriteHelper.LoadTextureFromAnimationFrame("Textures/Gems/Gem3/GEM 3 - BLUE - Spritesheet.png", 0, 11);



      m_tooltipCostIconRed = new SpriteRuntime()
      {
        // Texture = costTex,
        Texture = red.tex,
        SourceRectangle = red.region.Bounds,
        // Width = costTex.Width * 4.0f,
        // Height = costTex.Height * 2.5f,
        TextureAddress = Gum.Managers.TextureAddress.Custom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 10,
        Y = 4,
      };

      m_tooltipCostIconBlue = new SpriteRuntime()
      {
        Texture = blue.tex,
        SourceRectangle = blue.region.Bounds,
        // Width = costTex2.Width * 3.0f,
        // Height = costTex2.Height * 3.0f,
        TextureAddress = Gum.Managers.TextureAddress.Custom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 10,
        Y = 3,
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
        FillColor = greenColor
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
      // m_tooltipValueElements.Add(m_tooltipDescription);
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
      valueStackpanel.Visual.Y = -15;
      valueStackpanel.Visual.X = -15;
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
      costStackpanel.Visual.Y = -15;
      costStackpanel.Visual.X = 15;
      costStackpanel.Spacing = 10;

      // valueStackpanel.Visual.ChildrenLayout = Gum.Managers.ChildrenLayout.AutoGridHorizontal;

      // background.AddChild(border);
      background.AddChild(backgroundSprite);
      background.AddChild(toolTipTitleBackground);
      background.AddChild(stackPanel);

      background.AddChild(m_tooltipLabelContainer);

      // stackPanel.AddChild(m_tooltipLabelContainer);
      // stackPanel.AddChild(r);
      // stackPanel.AddChild(text);
      stackPanel.AddChild(descriptionElement);
      // stackPanel.AddChild(m_tooltipDescription);

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
      m_tooltipWindow.Visual.AddToManagers(MonoGameGum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_upgradesLayer);
      RenderGuiSystem.Instance.skillTreeItems.Add(m_tooltipWindow.Visual);
    }

    public void UpdateTooltipContent()
    {
      if (m_currentTooltipButton == null)
        return;

      var currency = m_currentTooltipButton.Data.UpgradeDefinition.Currency;

      switch (currency)
      {
        case "red":
          m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= (uint)m_currentTooltipButton.Data.Cost ? greenColor : redColor;
          break;
        case "blue":
          m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= (uint)m_currentTooltipButton.Data.Cost ? greenColor : redColor;
          break;
        default:
          m_tooltipCost.FillColor = Color.White;
          break;
      }
    }

    private void ShowTooltip(InteractiveGue buttonVis, string buttonName, bool doAnimation = true)
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
        var invisible = upgradeBtn.State == UpgradeButton.UnlockState.Invisible;

        if (invisible) return;

        var targetPosY = buttonVis.Y + 100;

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
              m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= (uint)upgradeBtn.Data.Cost ? greenColor : redColor;
              break;
            case "blue":
              m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= (uint)upgradeBtn.Data.Cost ? greenColor : redColor;
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
          else
          {
            var textRuntime = item.Component as TextRuntime;
            if (textRuntime != null)
            {
              var t = textRuntime.RenderableComponent as RenderingLibrary.Graphics.Text;
              var measure = RenderingLibrary.Graphics.Text.DefaultFont.MeasureString(textRuntime.Text);
              item.Width = measure.X;
              item.Height = measure.Y;
              item.UpdateLayout();
            }

          }
        }
      }
      else
      {
        if (buttonName.Contains("EmptyAbility"))
        {
          m_tooltipLabel.Text = $"Empty Ability Slot";
          m_tooltipDescription.Text = $"This is an empty ability slot.\nYou can unlock abilities to fill this slot.";

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

          m_tooltipWindow.IsVisible = true;
          var fb = HomeBase.Instance.stackPanelAvailable.Visual;
          // m_tooltipWindow.X = fb.AbsoluteX + fb.Width / 2;
          m_tooltipWindow.X = MonoGameGum.GumService.Default.CanvasWidth / 2.0f - m_tooltipWindow.Width / 2.0f;

          var y = buttonVis.AbsoluteY;

          // y = Math.Min(y, vp.Height - m_tooltipWindow.Height - 125);
          y = Math.Min(y, MonoGameGum.GumService.Default.CanvasHeight - m_tooltipWindow.Height - 260);

          m_tooltipWindow.Y = y;
          m_tooltipPuchasedText.Visible = false;

          if (doAnimation)
          {
            m_tooltipWindow.Height = 0;

            _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 300, duration: 0.25f)
                            .Easing(EasingFunctions.CubicOut);
          }
        }
        else
        {
          foreach (var a in HomeBase.Instance.AbilityButtons.Concat(HomeBase.Instance.AvailableAbilityButtons))
          {
            var btn = a.Value;
            if (btn.Name == buttonName)
            {
              var name = HomeBase.Instance.GetAbilityName(a.Key);
              var description = HomeBase.Instance.GetAbilityDescription(a.Key);

              // string formatDemo1 = $"[\u200Bstroke white][\u200Bfill #ff0000]Red[\u200Bfill 0 128 0]Green[\u200Bblue]Blue\nBecomes\n[stroke white][fill #ff0000]Red-[fill 0 128 0]Green-[blue]Blue";
              // string formatDemo2 = $"[\u200Bscale 4][\u200Brainbow][\u200Bsine]RAINBOW\nBecomes\n\n\n[scale 4][rainbow][sine]RAINBOW";
              // string formatDemo3 = $"Text can include icons\n(although this one is pure white):\nPress the [\u200bpixel] button!\nBecomes\nPress the [pixel] button!";
              //

              // string formatDemo1 = "[fill #ff0000]Test";

              m_tooltipLabel.Text = name;
              m_tooltipDescription.Text = description;

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

              m_tooltipWindow.IsVisible = true;
              // m_tooltipWindow.X = buttonVis.AbsoluteX - m_tooltipWindow.Width / 2 + buttonVis.Width / 2;
              // m_tooltipWindow.X = buttonVis.AbsoluteX + 125;
              // m_tooltipWindow.X = buttonVis.AbsoluteX + 125;
              var fb = HomeBase.Instance.stackPanelAvailable.Visual;
              // m_tooltipWindow.X = fb.AbsoluteX;
              // m_tooltipWindow.X = fb.AbsoluteX + fb.Width / 2;
              m_tooltipWindow.X = MonoGameGum.GumService.Default.CanvasWidth / 2.0f - m_tooltipWindow.Width / 2.0f;

              var y = buttonVis.AbsoluteY;

              // var vp = BaseGame.BoxingViewportAdapter.Viewport;

              y = Math.Min(y, MonoGameGum.GumService.Default.CanvasHeight - m_tooltipWindow.Height - 260);

              // y = vp.Height - m_tooltipWindow.Height;
              // y = window.AbsoluteTop;

              m_tooltipWindow.Y = y;
              //
              //
              // var root = GumService.Default.Root;
              // var idx = root.Children.IndexOf(m_tooltipWindow.Visual);
              // root.Children.Move(idx, root.Children.Count - 1);


              // var windowVis = m_tooltipWindow.Visual as WindowVisual;
              // windowVis.Z = -1;

              m_tooltipPuchasedText.Visible = false;

              // m_tooltipWindow.X = buttonVis.AbsoluteTop;
              // m_tooltipWindow.Y = 500;

              // m_tooltipWindow.IsVisible = true;
              // m_tooltipWindow.X = buttonVis.AbsoluteTop - m_tooltipWindow.Width / 2 + buttonVis.Width / 2;

              if (doAnimation)
              {
                m_tooltipWindow.Height = 0;

                _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 300, duration: 0.25f)
                                .Easing(EasingFunctions.CubicOut);
              }
            }
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

    [GeneratedRegex(@"\d+$")]
    private static partial Regex MyRegex();
  }
}

