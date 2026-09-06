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
using UntitledGemGame.Screens;

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
      MaxedOut,
      DemoLocked,

      SelectedInEditorMode,
      HoveredInEditorMode
    }

    public UnlockState State = UnlockState.Invisible;

    public float ClickedTime = 0.0f;
    public bool CanAfford = false;
    public Color BorderColor { get; set; }
    public Button Button { get; set; }
    public UpgradeData Data { get; set; }
    public int CurrentLevel = 0;

    public UpgradeDataLevel GetNextLevelInfo()
    {
      if (Data.LevelInfo.Count <= 0) return new UpgradeDataLevel();

      int i = Math.Clamp(CurrentLevel, 0, Data.NumLevels - 1);
      var info = Data.LevelInfo[i];
      return info;
    }

    public ulong GetNextLevelCost()
    {
      if (Data.LevelInfo.Count <= 0) return 0;

      int i = Math.Clamp(CurrentLevel, 0, Data.NumLevels - 1);
      var cost = Data.LevelInfo[i].Cost;
      return cost;
    }

    public bool IsMaxLevel => CurrentLevel == Data.NumLevels;
  }

  public class UpgradeJoint
  {
    public enum JointState
    {
      Hidden,
      Unlocking,
      Unlocked,
      Purchasing,
      Purchased,
      MaxedOut
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

  // public class UpgradeWindow
  // {
  //   //Loaded from json
  //   public int WindowWidth = -1;
  //   public int WindowHeight = -1;
  //
  //
  //
  //
  // }


  public class Upgrades
  {
    // public static HarvesterStrategy HarvesterCollectionStrategy = HarvesterStrategy.RandomScreenPosition;
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

    public Dictionary<string, UpgradeButton> UpgradeButtonsAll = new();

    public Dictionary<string, UpgradeButton> UpgradeButtons = new();
    public Dictionary<string, UpgradeButton> UpgradeButtonsAbilities = new();
    public Dictionary<string, UpgradeButton> UpgradeButtonsMeta = new();

    // public List<(Vector2, Vector2)> UpgradeJoints = new();
    public Dictionary<string, UpgradeJoint> UpgradeJointsAll = new();

    public Dictionary<string, UpgradeJoint> UpgradeJoints = new();
    public Dictionary<string, UpgradeJoint> UpgradeJointsAbilities = new();
    public Dictionary<string, UpgradeJoint> UpgradeJointsMeta = new();

    public Dictionary<string, JsonUpgrade> UpgradeDefinitions = new();
    public Dictionary<string, JsonUpgrade> UpgradeDefinitionsAbilities = new();
    public Dictionary<string, JsonUpgrade> UpgradeDefinitionsMeta = new();

    public static AsyncAsset<string> JsonUpgradesAsset;
    public static AsyncAsset<string> JsonAbilitiesAsset;
    public static AsyncAsset<string> JsonMetaUpgradesAsset;

    public static AsyncAsset<string> JsonUpgradeButtonsAsset;
    public static AsyncAsset<string> JsonAbilitiesButtonsAsset;
    public static AsyncAsset<string> JsonMetaButtonsAsset;


    public Dictionary<string, UpgradeButton> GetCurrentButtons()
    {
      var buttons = RenderGuiSystem.Instance.m_upgradeWindowType switch
      {
        RenderGuiSystem.UpgradeTypes.Upgrades => UpgradeButtons,
        RenderGuiSystem.UpgradeTypes.Abilities => UpgradeButtonsAbilities,
        RenderGuiSystem.UpgradeTypes.Meta => UpgradeButtonsMeta,
        _ => UpgradeButtons
      };

      return buttons;
    }

    public Dictionary<string, UpgradeJoint> GetCurrentJoints()
    {
      var joints = RenderGuiSystem.Instance.m_upgradeWindowType switch
      {
        RenderGuiSystem.UpgradeTypes.Upgrades => UpgradeJoints,
        RenderGuiSystem.UpgradeTypes.Abilities => UpgradeJointsAbilities,
        RenderGuiSystem.UpgradeTypes.Meta => UpgradeJointsMeta,
        _ => UpgradeJoints
      };

      return joints;
    }

    public Dictionary<string, JsonUpgrade> GetCurrentUpgradeDefinitions()
    {
      var upgradeDefinitions = RenderGuiSystem.Instance.m_upgradeWindowType switch
      {
        RenderGuiSystem.UpgradeTypes.Upgrades => UpgradeDefinitions,
        RenderGuiSystem.UpgradeTypes.Abilities => UpgradeDefinitionsAbilities,
        RenderGuiSystem.UpgradeTypes.Meta => UpgradeDefinitionsMeta,
        _ => UpgradeDefinitions
      };

      return upgradeDefinitions;
    }

    public Window GetCurrentUpgradesWindow()
    {
      var window = RenderGuiSystem.Instance.m_upgradeWindowType switch
      {
        RenderGuiSystem.UpgradeTypes.Upgrades => UpgradeManager.Instance.m_upgradesWindow,
        RenderGuiSystem.UpgradeTypes.Abilities => UpgradeManager.Instance.m_upgradesWindowAbilities,
        RenderGuiSystem.UpgradeTypes.Meta => UpgradeManager.Instance.m_upgradesWindowMeta,
        _ => UpgradeManager.Instance.m_upgradesWindow
      };

      return window;
    }

    public int WindowWidth = 20000;
    public int WindowHeight = 20000;

    public void LoadAllJsons()
    {
      LoadJson(JsonUpgradesAsset.Value, JsonUpgradeButtonsAsset.Value, UpgradeButtons, UpgradeDefinitions);
      LoadJson(JsonAbilitiesAsset.Value, JsonAbilitiesButtonsAsset.Value, UpgradeButtonsAbilities, UpgradeDefinitionsAbilities);
      LoadJson(JsonMetaUpgradesAsset.Value, JsonMetaButtonsAsset.Value, UpgradeButtonsMeta, UpgradeDefinitionsMeta);
    }

    public void LoadJson(string upgrades, string buttons, Dictionary<string, UpgradeButton> upgradeButtons, Dictionary<string, JsonUpgrade> upgradeDefinitions)
    {
      var rootUpgrades = JsonSerializer.Deserialize(upgrades, SerializerContext.Default.RootUpgrades);
      var rootButtons = JsonSerializer.Deserialize(buttons, SerializerContext2.Default.RootUpgradeButtons);

      upgradeButtons.Clear();
      upgradeDefinitions.Clear();

      foreach (var def in rootUpgrades.Upgrades)
      {
        upgradeDefinitions.Add(def.ShortName, def);
      }

      foreach (var btn in rootButtons.Buttons)
      {
        var success = upgradeDefinitions.TryGetValue(btn.Upgrade, out var upDef);

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
        }

        if (upgradeButtons.ContainsKey(btn.Shortname))
        {
          var newName = btn.Shortname;
          int count = 1;
          while (upgradeButtons.ContainsKey(newName))
          {
            newName = btn.Shortname + "_" + count.ToString();
            count++;
          }
          Console.WriteLine($"Duplicate upgrade button shortname found: {btn.Shortname}, renaming to {newName}");
          btn.Shortname = newName;
        }

        UpgradeData upgrade = new UpgradeData();

        var numLevels = int.Parse(btn.NumLevels);

        for (int i = 0; i < numLevels; ++i)
        {
          var upgradeLevelInfo = new UpgradeDataLevel();

          try
          {
            // Instantiating UpgradeData explicitly based on type eliminates 'dynamic'
            if (upDef.Type == "int")
            {
              int val = int.Parse(btn.Value[i]);
              upgradeLevelInfo.m_upgradeAmountInt = val;
            }
            else if (upDef.Type == "float")
            {
              float val = float.Parse(btn.Value[i], CultureInfo.InvariantCulture);
              upgradeLevelInfo.m_upgradeAmountFloat = val;
            }
            else if (upDef.Type == "bool")
            {
              bool val = bool.Parse(btn.Value[i]);
              upgradeLevelInfo.m_upgradesToBool = val;
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

          upgradeLevelInfo.Cost = ulong.Parse(btn.Cost[i]);
          upgrade.LevelInfo.Add(upgradeLevelInfo);
        }

        // Set remaining fields on the strongly-typed object
        upgrade.UpgradeDefinition = upDef;
        upgrade.ShortName = btn.Shortname;
        upgrade.NumLevels = numLevels;
        // upgrade.Cost = ulong.Parse(btn.Cost);
        upgrade.PosX = int.Parse(btn.PosX);
        upgrade.PosY = int.Parse(btn.PosY);
        upgrade.HiddenBy = btn.HiddenBy;
        upgrade.LockedBy = btn.LockedBy;
        upgrade.BlockedBy = btn.BlockedBy;
        upgrade.AddMidPoint = bool.Parse(btn.AddMidPoint);
        upgrade.SwapMidPointAxis = bool.Parse(btn.SwapMidPointAxis);
        upgrade.LockedInDemo = bool.Parse(btn.LockedInDemo);
        upgrade.TooltipShowPercentage = bool.Parse(btn.TooltipShowPercentage);
        upgrade.ButtonSizeScale = float.Parse(btn.ButtonSizeScale, CultureInfo.InvariantCulture);

        upgradeButtons.Add(btn.Shortname, new UpgradeButton
        {
          Button = null,
          Data = upgrade
        });
      }

      // WindowWidth = int.Parse(rootButtons.WindowWidth);
      // WindowHeight = int.Parse(rootButtons.WindowHeight);
    }

    // public void LoadFromJson(string jsonUpgrades, string jsonButtons)
    // {
    //   var rootUpgrades = JsonSerializer.Deserialize(jsonUpgrades, SerializerContext.Default.RootUpgrades);
    //   var rootButtons = JsonSerializer.Deserialize(jsonButtons, SerializerContext2.Default.RootUpgradeButtons);
    //
    //   UpgradeButtons.Clear();
    //   UpgradeDefinitions.Clear();
    //
    //   foreach (var def in rootUpgrades.Upgrades)
    //   {
    //     UpgradeDefinitions.Add(def.ShortName, def);
    //   }
    //
    //   foreach (var btn in rootButtons.Buttons)
    //   {
    //     var success = UpgradeDefinitions.TryGetValue(btn.Upgrade, out var upDef);
    //
    //     if (!success)
    //     {
    //       Console.WriteLine($"Upgrade definition not found for button {btn.Shortname} with upgrade {btn.Upgrade}, skipping...");
    //       upDef = new JsonUpgrade
    //       {
    //         ShortName = btn.Upgrade,
    //         Name = "Unknown Upgrade",
    //         Type = "int",
    //         BaseValue = "0"
    //       };
    //     }
    //
    //     if (UpgradeButtons.ContainsKey(btn.Shortname))
    //     {
    //       var newName = btn.Shortname;
    //       int count = 1;
    //       while (UpgradeButtons.ContainsKey(newName))
    //       {
    //         newName = btn.Shortname + "_" + count.ToString();
    //         count++;
    //       }
    //       Console.WriteLine($"Duplicate upgrade button shortname found: {btn.Shortname}, renaming to {newName}");
    //       btn.Shortname = newName;
    //     }
    //
    //     UpgradeData upgrade = new UpgradeData();
    //
    //     var numLevels = int.Parse(btn.NumLevels);
    //
    //     for (int i = 0; i < numLevels; ++i)
    //     {
    //       var upgradeLevelInfo = new UpgradeDataLevel();
    //
    //       try
    //       {
    //         // Instantiating UpgradeData explicitly based on type eliminates 'dynamic'
    //         if (upDef.Type == "int")
    //         {
    //           int val = int.Parse(btn.Value[i]);
    //           upgradeLevelInfo.m_upgradeAmountInt = val;
    //         }
    //         else if (upDef.Type == "float")
    //         {
    //           float val = float.Parse(btn.Value[i], CultureInfo.InvariantCulture);
    //           upgradeLevelInfo.m_upgradeAmountFloat = val;
    //         }
    //         else if (upDef.Type == "bool")
    //         {
    //           bool val = bool.Parse(btn.Value[i]);
    //           upgradeLevelInfo.m_upgradesToBool = val;
    //         }
    //         else
    //         {
    //           Console.WriteLine($"Unknown upgrade type: {upDef.Type} for button {btn.Shortname}, skipping...");
    //           continue;
    //         }
    //       }
    //       catch
    //       {
    //         Console.WriteLine($"Parsing failed: {btn.Value} - {upDef.Name} - {upDef.Type} - {btn.Shortname}");
    //         continue;
    //       }
    //
    //       upgradeLevelInfo.Cost = ulong.Parse(btn.Cost[i]);
    //       upgrade.LevelInfo.Add(upgradeLevelInfo);
    //     }
    //
    //     // Set remaining fields on the strongly-typed object
    //     upgrade.UpgradeDefinition = upDef;
    //     upgrade.ShortName = btn.Shortname;
    //     upgrade.NumLevels = numLevels;
    //     // upgrade.Cost = ulong.Parse(btn.Cost);
    //     upgrade.PosX = int.Parse(btn.PosX);
    //     upgrade.PosY = int.Parse(btn.PosY);
    //     upgrade.HiddenBy = btn.HiddenBy;
    //     upgrade.LockedBy = btn.LockedBy;
    //     upgrade.BlockedBy = btn.BlockedBy;
    //     upgrade.AddMidPoint = bool.Parse(btn.AddMidPoint);
    //     upgrade.SwapMidPointAxis = bool.Parse(btn.SwapMidPointAxis);
    //     upgrade.LockedInDemo = bool.Parse(btn.LockedInDemo);
    //     upgrade.TooltipShowPercentage = bool.Parse(btn.TooltipShowPercentage);
    //     upgrade.ButtonSizeScale = float.Parse(btn.ButtonSizeScale, CultureInfo.InvariantCulture);
    //
    //     UpgradeButtons.Add(btn.Shortname, new UpgradeButton
    //     {
    //       Button = null,
    //       Data = upgrade
    //     });
    //   }
    //
    //   WindowWidth = int.Parse(rootButtons.WindowWidth);
    //   WindowHeight = int.Parse(rootButtons.WindowHeight);
    // }

    public void SaveToJson()
    {
      var json = @$"{{ " + Environment.NewLine;
      json += $@"  ""windowwidth"": ""{WindowWidth}""," + Environment.NewLine;
      json += $@"  ""windowheight"": ""{WindowHeight}""," + Environment.NewLine;
      json += $@"  ""buttons"": [" + Environment.NewLine;

      foreach (var btn in GetCurrentButtons())
      {
        if (string.IsNullOrEmpty(btn.Value.Data.ShortName))
          continue;

        var valuesList = new List<string>();
        var costsList = new List<string>();

        for (int i = 0; i < btn.Value.Data.NumLevels; ++i)
        {
          var levelInfo = btn.Value.Data.LevelInfo[i];

          var valueStr = btn.Value.Data.UpgradeDefinition.Type switch
          {
            "int" => levelInfo.m_upgradeAmountInt.ToString(),
            "float" => levelInfo.m_upgradeAmountFloat.ToString(CultureInfo.InvariantCulture),
            "bool" => levelInfo.m_upgradesToBool.ToString().ToLowerInvariant(),
            _ => "0"
          };

          valuesList.Add($"\"{valueStr}\"");
          costsList.Add($"\"{levelInfo.Cost}\"");
        }

        var valuesJson = $"[{string.Join(", ", valuesList)}]";
        var costsJson = $"[{string.Join(", ", costsList)}]";

        json += @$"    {{" + Environment.NewLine +
                $@"      ""shortname"":""{btn.Value.Data.ShortName}""," + Environment.NewLine +
                $@"      ""upgrade"":""{btn.Value.Data.UpgradeDefinition.ShortName}""," + Environment.NewLine +
                $@"      ""numlevels"":""{btn.Value.Data.NumLevels}""," + Environment.NewLine +
                $@"      ""hiddenby"":""{btn.Value.Data.HiddenBy}""," + Environment.NewLine +
                $@"      ""lockedby"":""{btn.Value.Data.LockedBy}""," + Environment.NewLine +
                $@"      ""blockedby"":""{btn.Value.Data.BlockedBy}""," + Environment.NewLine +
                $@"      ""cost"":{costsJson}," + Environment.NewLine +
                $@"      ""value"":{valuesJson}," + Environment.NewLine +
                $@"      ""posx"":""{btn.Value.Data.PosX}""," + Environment.NewLine +
                $@"      ""posy"":""{btn.Value.Data.PosY}""," + Environment.NewLine +
                $@"      ""addmidpoint"":""{btn.Value.Data.AddMidPoint}""," + Environment.NewLine +
                $@"      ""swapmidpointaxis"":""{btn.Value.Data.SwapMidPointAxis}""," + Environment.NewLine +
                $@"      ""lockedindemo"":""{btn.Value.Data.LockedInDemo}""," + Environment.NewLine +
                $@"      ""tooltippercentage"":""{btn.Value.Data.TooltipShowPercentage}""," + Environment.NewLine +
                $@"      ""buttonsizescale"":""{btn.Value.Data.ButtonSizeScale.ToString(CultureInfo.InvariantCulture)}""" + Environment.NewLine +
                $@"    }}," + Environment.NewLine;
      }

      int index = json.LastIndexOf(',');
      if (index != -1)
      {
        json = json.Remove(index, 1);
      }

      json += @$"  ]" + Environment.NewLine;
      json += $@"}}";

      var jsonFilename = RenderGuiSystem.Instance.m_upgradeWindowType switch
      {
        RenderGuiSystem.UpgradeTypes.Upgrades => "upgrades_buttons.json",
        RenderGuiSystem.UpgradeTypes.Abilities => "upgrades_abilities_buttons.json",
        RenderGuiSystem.UpgradeTypes.Meta => "upgrades_meta_buttons.json",
        _ => ""
      };

      if (!string.IsNullOrEmpty(jsonFilename))
      {

        var projDir = PathHelper.FindProjectDirectory();
        var savePath = Path.Combine(projDir, "Content", "Data", jsonFilename);
        File.WriteAllText(savePath, json);
        AssetManager.ReloadAsset(JsonUpgradeButtonsAsset);
        AssetManager.ReloadAsset(JsonAbilitiesButtonsAsset);
        AssetManager.ReloadAsset(JsonMetaButtonsAsset);
        // LoadAllJsons()
        UpgradeManager.Instance.RefreshButtons();
      }
    }

    public UpgradeButton AddNewButton(string shortName, Dictionary<string, UpgradeButton> buttons, JsonUpgrade upgradeDef = null)
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

      var upgrade = new UpgradeData
      {
        ShortName = shortName,
        UpgradeDefinition = upgradeDef,
        NumLevels = 1,
        LevelInfo = new List<UpgradeDataLevel>()
        {
          new UpgradeDataLevel()
          {
            Cost = 0,
            m_upgradeAmountInt = 0
          }
        },
        // Cost = 0,
        PosX = (int)screenX,
        PosY = (int)screenY,
        HiddenBy = "",
        LockedBy = "",
        BlockedBy = "",
        AddMidPoint = false
      };

      buttons.Add(shortName, new UpgradeButton
      {
        Button = null,
        Data = upgrade
      });

      return buttons[shortName];
    }
  }

  public partial class UpgradeManager
  {
    public static Upgrades CurrentUpgrades = new();

    public event Action OnUpgradeRoot;
    public event Action<string> OnUpgrade;

    private GameState m_gameState;
    public Window m_upgradesWindow;
    public Window m_upgradesWindowAbilities;
    public Window m_upgradesWindowMeta;

    public bool UpgradeGuiEditMode = false;
    public bool UpdatingButtons = false;

    public static UpgradeManager Instance;
    public UpgradeManager()
    {
      Instance = this;
    }

    //TODO: when upgrading we need to increment the correct "UG"
    public UpgradesGeneratorUpgrades UG = new();
    public UpgradesGeneratorUpgrades_abilities UGA = new();
    public UpgradesGeneratorUpgrades_meta UGM = new();

    public float GetFloat(string shortname)
    {
      var ug = UG.GetFloat(shortname, out float val);
      if (!ug)
      {
        var uga = UGA.GetFloat(shortname, out val);
        if (!uga)
        {
          UGM.GetFloat(shortname, out val);
        }
      }

      return val;
    }

    public int GetInt(string shortname)
    {
      var ug = UG.GetInt(shortname, out int val);
      if (!ug)
      {
        var uga = UGA.GetInt(shortname, out val);
        if (!uga)
        {
          UGM.GetInt(shortname, out val);
        }
      }

      return val;
    }

    public bool GetBool(string shortname)
    {
      var ug = UG.GetBool(shortname, out bool val);
      if (!ug)
      {
        var uga = UGA.GetBool(shortname, out val);
        if (!uga)
        {
          UGM.GetBool(shortname, out val);
        }
      }

      return val;
    }

    private void SetBorderColor(UpgradeButton button, Color color)
    {
      button.BorderColor = color;
      // if (buttonVis.Children.Count > 3)
      // {
      //   var borderSprite = buttonVis.Children[3] as SpriteRuntime;
      //   if (borderSprite != null)
      //   {
      //     borderSprite.Color = color;
      //   }
      //   // var borderSprite = buttonVis.Children[2] as ButtonBorderShape;
      //   // if (borderSprite != null)
      //   // {
      //   //   borderSprite.Color = color;
      //   // }
      // }
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

      Log.Debug("Setting button state: " + upgradeBtn.Data.ShortName + " - " + state.ToString());

      if (upgradeBtn.Data.LockedInDemo && Demo.IsDemo && !Demo.IsDev && state > UpgradeButton.UnlockState.Revealed)
      {
        state = UpgradeButton.UnlockState.DemoLocked;
      }

      upgradeBtn.State = state;

      // Keep the model state valid even before its GUI is created.
      if (upgradeBtn.Button == null)
        return;

      SetIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));
      SetBackgroundColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));

      Color borderColorHidden = new Color(204, 62, 62, 255);
      Color borderColorUnlocked = new Color(29, 188, 96, 255);
      // Color borderColorPurchased = new Color(75, 128, 177, 255);
      Color borderColorPurchased = new Color(29, 188, 96, 255);
      Color borderColorMaxedOut = new Color(75, 128, 177, 255);

      // Console.WriteLine($"Setting Button {upgradeBtn.Data.ShortName}: " + state);

      switch (state)
      {
        case UpgradeButton.UnlockState.Invisible:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
            SetBorderColor(upgradeBtn, new Color(0, 0, 0, 0));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
            SetBackgroundColor(upgradeBtn.Button.Visual, new Color(0, 0, 0, 0));
          }
          break;
        case UpgradeButton.UnlockState.Hidden:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            // SetBorderColor(upgradeBtn.Button.Visual, borderColorHidden);
            SetBorderColor(upgradeBtn, new Color(255, 0, 0, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));
          }
          break;
        case UpgradeButton.UnlockState.Revealed:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, new Color(99, 99, 99, 255));
            // SetBorderColor(upgradeBtn.Button.Visual, borderColorUnlocked);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Unlocked:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, borderColorUnlocked);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.Purchased:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, borderColorPurchased);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.MaxedOut:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, borderColorMaxedOut);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.DemoLocked:
          {
            upgradeBtn.Button.Visual.IsEnabled = false;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, borderColorHidden);
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 255));
          }
          break;
        case UpgradeButton.UnlockState.SelectedInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, new Color(255, 0, 255, 255));
            SetHiddenIconColor(upgradeBtn.Button.Visual, new Color(255, 255, 255, 0));
          }
          break;
        case UpgradeButton.UnlockState.HoveredInEditorMode:
          {
            upgradeBtn.Button.Visual.IsEnabled = true;
            upgradeBtn.Button.Visual.Visible = true;
            SetBorderColor(upgradeBtn, new Color(255, 180, 10, 255));
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

    private Button CreateButton(Window window, KeyValuePair<string, UpgradeButton> btnData)
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
        Name = "BorderSprite",
        Texture = border,
        Color = new Color(255, 255, 255, 0),
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        Width = width,
        Height = height,
        Visible = false,
        // BlendState = BlendState.Additive
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

    // private void RefreshButtons(string jsonUpgrades, string jsonButtons)
    private void PrepareWindow(Window window)
    {
      var vis = window.Visual;

      if (vis == null)
      {
        Log.Error("Couldnt get window visual");
        return;
      }

      // vis.Children.Clear();

      foreach (var a in vis.Children)
      {
        if (a.Name == "Background")
        {
          var background = a as NineSliceRuntime;
          background.Color = new Color(0, 0, 0, 240);
        }
      }

      vis.Children.RemoveAt(0);

      window.Width = CurrentUpgrades.WindowWidth / 2;
      window.Height = CurrentUpgrades.WindowHeight / 2;
      // window.IsVisible = false;
    }

    private void CleanupWindow(Window window)
    {
      if (window != null)
      {
        window.Visual.RemoveFromManagers();
        RenderGuiSystem.Instance.skillTreeItems.Remove(window.Visual);
      }
    }

    private void SetupUpgradeJoints(Window window, Dictionary<string, JsonUpgrade> upgradeDefinitions, Dictionary<string, UpgradeButton> buttons, Dictionary<string, UpgradeJoint> joints)
    {
      foreach (var btnData in buttons)
      {
        // Console.WriteLine("CreateButton: " + btnData.Key);
        var button = CreateButton(window, btnData);
        SetButtonState(btnData.Value, UpgradeButton.UnlockState.Invisible);
        // vis.Background.Texture
        // Console.WriteLine("Set upgrade window background texture");

        UG.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);
        UGA.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);
        UGM.Reset(btnData.Value.Data.UpgradeDefinition.ShortName);

        if (btnData.Value.Data.ShortName != "HB")
        {
          var b = upgradeDefinitions.TryGetValue(btnData.Value.Data.UpgradeDefinition.ShortName, out var upDef);
          if (b)
          {
            if (btnData.Value.Data.UpgradeDefinition.Type == "float")
            {
              UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(upDef.BaseValue, CultureInfo.InvariantCulture));
              UGA.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(upDef.BaseValue, CultureInfo.InvariantCulture));
              UGM.Set(btnData.Value.Data.UpgradeDefinition.ShortName, float.Parse(upDef.BaseValue, CultureInfo.InvariantCulture));
            }
            else if (btnData.Value.Data.UpgradeDefinition.Type == "int")
            {
              UG.Set(btnData.Value.Data.UpgradeDefinition.ShortName, int.Parse(upDef.BaseValue));
              UGA.Set(btnData.Value.Data.UpgradeDefinition.ShortName, int.Parse(upDef.BaseValue));
              UGM.Set(btnData.Value.Data.UpgradeDefinition.ShortName, int.Parse(upDef.BaseValue));
            }
          }
        }
      }

      foreach (var btnData in buttons)
      {
        if (string.IsNullOrEmpty(btnData.Value.Data.LockedBy) &&
            string.IsNullOrEmpty(btnData.Value.Data.HiddenBy) &&
            string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
        {
          SetButtonState(btnData.Value, UpgradeButton.UnlockState.Unlocked);
        }

        if (!string.IsNullOrEmpty(btnData.Value.Data.BlockedBy))
        {
          buttons.TryGetValue(btnData.Value.Data.BlockedBy, out var blockedBy);
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

            joints.Add(btnData.Key, new UpgradeJoint
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

      var startPosGrouping = joints.GroupBy(j => new Vector2(j.Value.StartButton.Data.PosX, j.Value.StartButton.Data.PosY));

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
    }

    public void RefreshButtons()
    {
      lock (_lock)
      {
        var progress = new GameSave
        {
          RedGems = m_gameState.CurrentRedGemCount,
          BlueGems = m_gameState.CurrentBlueGemCount,
          PurpleGems = m_gameState.CurrentPurpleGemCount
        };
        CaptureProgress(progress);
        Console.WriteLine("Refreshing Buttons");
        // if(Upgrades.JsonUpgradeButtonsAsset.)
        var camera = SystemManagers.Default.Renderer.Camera;
        camera.Zoom = 1.0f;
        RenderGuiSystem.Instance.targetZoom = 1.0f;
        //TODO: zoom level affects buttons hover when buttons are refreshed (try zooming in/out in upgrades menu and press F5)
        UpdatingButtons = true;

        foreach (var item in CurrentUpgrades.UpgradeButtons)
        {
          item.Value.Button.IsEnabled = false;
        }

        foreach (var item in CurrentUpgrades.UpgradeButtonsAbilities)
        {
          item.Value.Button.IsEnabled = false;
        }
        foreach (var item in CurrentUpgrades.UpgradeButtonsMeta)
        {
          item.Value.Button.IsEnabled = false;
        }



        CurrentUpgrades = new Upgrades();
        UG = new UpgradesGeneratorUpgrades();
        UGA = new UpgradesGeneratorUpgrades_abilities();
        UGM = new UpgradesGeneratorUpgrades_meta();

        CleanupWindow(m_upgradesWindow);
        CleanupWindow(m_upgradesWindowAbilities);
        CleanupWindow(m_upgradesWindowMeta);

        m_upgradesWindow = new Window();
        m_upgradesWindowAbilities = new Window();
        m_upgradesWindowMeta = new Window();

        PrepareWindow(m_upgradesWindow);
        PrepareWindow(m_upgradesWindowAbilities);
        PrepareWindow(m_upgradesWindowMeta);

        Console.WriteLine("Upgrades JSON reloaded");
        // CurrentUpgrades.LoadFromJson(jsonUpgrades, jsonButtons);

        CurrentUpgrades.LoadAllJsons();

        // window.X = -1000;
        // window.Y = -CurrentUpgrades.WindowHeight / 2;

        var tex = AssetManager.Load<Texture2D>("Textures/blue_pixel.png");
        var sprite = new NineSliceRuntime()
        {
          Texture = tex,
          Width = 2028,
          Height = m_upgradesWindow.Height,
          TextureAddress = Gum.Managers.TextureAddress.EntireTexture
        };

        m_upgradesWindow.AddChild(sprite);

        var tex2 = AssetManager.Load<Texture2D>("Textures/red_pixel.png");
        var sprite2 = new NineSliceRuntime()
        {
          Texture = tex2,
          Width = m_upgradesWindow.Width - 2028,
          X = 2028,
          Height = m_upgradesWindow.Height,
          TextureAddress = Gum.Managers.TextureAddress.EntireTexture
        };

        m_upgradesWindow.AddChild(sprite2);


        m_upgradesWindow.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_upgradesLayer);
        RenderGuiSystem.Instance.skillTreeItems.Add(m_upgradesWindow.Visual);

        m_upgradesWindowAbilities.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_upgradesAbilitiesLayer);
        RenderGuiSystem.Instance.skillTreeItems.Add(m_upgradesWindowAbilities.Visual);

        m_upgradesWindowMeta.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_upgradesMetaLayer);
        RenderGuiSystem.Instance.skillTreeItems.Add(m_upgradesWindowMeta.Visual);

        SetupUpgradeJoints(m_upgradesWindow, CurrentUpgrades.UpgradeDefinitions, CurrentUpgrades.UpgradeButtons, CurrentUpgrades.UpgradeJoints);
        SetupUpgradeJoints(m_upgradesWindowAbilities, CurrentUpgrades.UpgradeDefinitionsAbilities, CurrentUpgrades.UpgradeButtonsAbilities, CurrentUpgrades.UpgradeJointsAbilities);
        SetupUpgradeJoints(m_upgradesWindowMeta, CurrentUpgrades.UpgradeDefinitionsMeta, CurrentUpgrades.UpgradeButtonsMeta, CurrentUpgrades.UpgradeJointsMeta);

        RestoreProgress(progress);

        if (UpgradeGuiEditMode)
        {
          foreach (var btnData in CurrentUpgrades.GetCurrentButtons())
          {
            SetButtonState(btnData.Value, UpgradeButton.UnlockState.Unlocked);
          }

          foreach (var joint in CurrentUpgrades.GetCurrentJoints())
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

      Upgrades.JsonUpgradesAsset = AssetManager.LoadAsync<string>("Data/upgrades.json", true);
      Upgrades.JsonAbilitiesAsset = AssetManager.LoadAsync<string>("Data/upgrades_abilities.json", true);
      Upgrades.JsonMetaUpgradesAsset = AssetManager.LoadAsync<string>("Data/upgrades_meta.json", true);

      Upgrades.JsonUpgradeButtonsAsset = AssetManager.LoadAsync<string>("Data/upgrades_buttons.json", true);
      Upgrades.JsonAbilitiesButtonsAsset = AssetManager.LoadAsync<string>("Data/upgrades_abilities_buttons.json", true);
      Upgrades.JsonMetaButtonsAsset = AssetManager.LoadAsync<string>("Data/upgrades_meta_buttons.json", true);

      CurrentUpgrades = new();
      UG = new();
      UGA = new();
      UGM = new();

      UpgradeGuiEditMode = false;
      UpdatingButtons = false;

      m_gameState = gameState;

      GameMain.AddCustomImGuiContent(DrawImGuiContent);

      // #if KNI_WEB
      // UpdateJsonUpgrades(Upgrades.JsonUpgradesAsset);
      // UpdateJsonUpgradeButtons(Upgrades.JsonUpgradeButtonsAsset);
      // #endif
      RefreshButtons();
    }

    public void Finish()
    {
      GameMain.RemoveCustomImGuiContent(DrawImGuiContent);

      UpgradeGuiEditMode = false;
      UpdatingButtons = false;

      if (m_upgradesWindow != null)
      {
        m_upgradesWindow.Visual.RemoveFromManagers();
        RenderGuiSystem.Instance.skillTreeItems.Remove(m_upgradesWindow.Visual);
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
        var buttons = CurrentUpgrades.GetCurrentButtons();
        var upgradeDefinitions = CurrentUpgrades.GetCurrentUpgradeDefinitions();

        var b = m_selectedButtonEditMode;

        if (b != null)
        {
          foreach (var btn in buttons)
          {
            if (btn.Value.State != UpgradeButton.UnlockState.Unlocked && btn.Value != b)
              SetButtonState(btn.Value, UpgradeButton.UnlockState.Unlocked);
          }

          ImGui.InputText("ID/ShortName", ref b.Data.ShortName, 10);

          if (ImGui.BeginCombo("Upgrade", b.Data.UpgradeDefinition.Name))
          {
            foreach (var upg in upgradeDefinitions)
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
                  while (buttons.ContainsKey(b.Data.ShortName))
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

          ImGui.InputInt("NumLevels", ref b.Data.NumLevels);

          b.Data.NumLevels = Math.Clamp(b.Data.NumLevels, 1, 15);

          while (b.Data.LevelInfo.Count < b.Data.NumLevels)
          {
            b.Data.LevelInfo.Add(new UpgradeDataLevel());
          }

          while (b.Data.LevelInfo.Count > b.Data.NumLevels)
          {
            b.Data.LevelInfo.Remove(b.Data.LevelInfo.Last());
          }

          ImGui.Indent();
          ImGui.Text("Values");

          for (int i = 0; i < b.Data.NumLevels; ++i)
          {
            switch (b.Data.UpgradeDefinition.Type)
            {
              case "int":
                {
                  ImGui.InputInt("Value " + i, ref b.Data.LevelInfo[i].m_upgradeAmountInt);
                }
                break;
              case "float":
                {
                  ImGui.InputFloat("Value " + i, ref b.Data.LevelInfo[i].m_upgradeAmountFloat);
                }
                break;
              case "bool":
                {
                  ImGui.Checkbox("Value " + i, ref b.Data.LevelInfo[i].m_upgradesToBool);
                }
                break;
            }
          }

          ImGui.Text("Costs");

          for (int i = 0; i < b.Data.NumLevels; ++i)
          {
            ulong step = 1;
            ulong stepFast = 100;

            unsafe
            {
              fixed (ulong* pCost = &b.Data.LevelInfo[i].Cost)
              {
                ImGui.InputScalar(
                    "Cost " + i,
                    ImGuiDataType.U64,
                    (IntPtr)pCost,
                    (IntPtr)(&step),
                    (IntPtr)(&stepFast)
                );
              }
            }
          }


          ImGui.Unindent();
          // ImGui.InputScalar("Cost", ImGuiDataType.U64, ref b.Data.Cost);

          if (setAny)
          {
            foreach (var btn in buttons)
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

          ImGui.Checkbox("Show percentage in Tooltip", ref b.Data.TooltipShowPercentage);

          ImGui.Checkbox("Locked in Demo", ref b.Data.LockedInDemo);

          ImGui.InputFloat("ButtonSizeScale", ref b.Data.ButtonSizeScale);

          b.Button.X = b.Data.PosX;
          b.Button.Y = b.Data.PosY;

          if (b.State != UpgradeButton.UnlockState.SelectedInEditorMode)
            SetButtonState(b, UpgradeButton.UnlockState.SelectedInEditorMode);

          ImGui.Separator();
          int count = 0;
          string newShortName = "NB0";
          while (buttons.ContainsKey(newShortName))
          {
            newShortName = "NB" + count.ToString();
            ++count;
          }

          ImGui.InputText("NewButtonShortName", ref newShortName, 10);
          ImGui.Button("Add New Button");
          if (ImGui.IsItemClicked())
          {
            CurrentUpgrades.AddNewButton(newShortName, buttons);
            var button = CreateButton(CurrentUpgrades.GetCurrentUpgradesWindow(), new KeyValuePair<string, UpgradeButton>(newShortName, buttons[newShortName]));
          }

          ImGui.Button("Remove Button");
          if (ImGui.IsItemClicked())
          {
            if (buttons.TryGetValue(b.Data.ShortName, out var removeButton))
            {
              removeButton.Button.RemoveFromRoot();
              buttons.Remove(b.Data.ShortName);
            }

            // CurrentUpgrades.AddNewButton(newShortName);
            // var button = CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));
          }

          ImGui.Button("Upgrade All");
          if (ImGui.IsItemClicked())
          {
            m_gameState.CurrentRedGemCount = 5000000000;
            m_gameState.CurrentRedGemCount += 5000000000;
            m_gameState.CurrentRedGemCount += 50000000000000000;
            m_gameState.CurrentRedGemCount += 50000000000000;
            m_gameState.CurrentBlueGemCount = 500;
            foreach (var button in CurrentUpgrades.GetCurrentButtons())
            {
              // button.Value.Button.PerformClick();
              //
              if (button.Value.Data.ShortName == "RBG1")
                continue;
              if(button.Value.Data.ShortName == "CZS1")
                continue;

              for(int i = 0; i < 50; ++i)
                Upgrade(button.Value);
            }
          }

          ImGui.Button("Money");
          if (ImGui.IsItemClicked())
          {
            m_gameState.CurrentRedGemCount += 5000;
            m_gameState.CurrentBlueGemCount = 500;
          }

          // float spawnRate = 0;
          // float gemCooldown = 0;
          // float gemCooldownBase = 0;
          // float maxGemCount = 0;
          // float gemValue = 0;
          // float blueGem = 0;
          // foreach (var button in CurrentUpgrades.UpgradeButtons)
          // {
          //   var i = button.Value.Data.m_upgradeAmountInt;
          //   var f = button.Value.Data.m_upgradeAmountFloat;
          //   var c = f + i;
          //
          //   if (button.Value.Data.UpgradeDefinition.PropertyName == nameof(UpgradeManager.Instance.UG.GemSpawnRate))
          //     spawnRate += c;
          //   if (button.Value.Data.UpgradeDefinition.PropertyName == nameof(UpgradeManager.Instance.UG.MaxGemCount))
          //     maxGemCount += c;
          //   if (button.Value.Data.UpgradeDefinition.PropertyName == nameof(UpgradeManager.Instance.UG.GemValue))
          //     gemValue += c;
          //   if (button.Value.Data.UpgradeDefinition.PropertyName == nameof(UpgradeManager.Instance.UG.BlueGem))
          //     blueGem += c;
          //   if (button.Value.Data.UpgradeDefinition.PropertyName == nameof(UpgradeManager.Instance.UG.GemSpawnCooldown))
          //   {
          //     gemCooldownBase = float.Parse(button.Value.Data.UpgradeDefinition.BaseValue, CultureInfo.InvariantCulture);
          //     gemCooldown += c;
          //   }
          // }
          //
          // ImGui.Text("GemValue: " + gemValue);
          // ImGui.Text("SpawnRate: " + spawnRate);
          // ImGui.Text($"Gem Cooldown: {gemCooldown}  ({gemCooldownBase})");
          // ImGui.Text("MaxGemCount: " + maxGemCount);
          // ImGui.Text("BlueGems: " + blueGem);
        }

#if !KNI_WEB
        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"EDIT MODE ENABLED", new Vector2(10, 0), Color.Yellow, Color.Black, 35);
#endif
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
        foreach (var button in CurrentUpgrades.GetCurrentButtons())
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
            foreach (var btn in CurrentUpgrades.GetCurrentButtons())
            {
              SetBorderColor(btn.Value, new Color(0, 0, 0, 0));
              SetIconColor(btn.Value.Button.Visual, new Color(255, 255, 255, 50));
            }

            SetButtonState(button.Value, UpgradeButton.UnlockState.HoveredInEditorMode);
          }
        }
        ImGui.EndCombo();
      }
    }

    // public void RefreshButtons()
    // {
    //   RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    // }

    // private void UpdateJsonUpgrades(string json)
    // {
    //   try
    //   {
    //     jsonUpgrades = json;
    //     if (string.IsNullOrEmpty(jsonUpgradeButtons))
    //       return;
    //
    //     Console.WriteLine("UpdateJsonUpgrades");
    //     RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    //   }
    //   catch (Exception e)
    //   {
    //     Console.WriteLine(e);
    //   }
    // }

    // private void UpdateJsonUpgradeButtons(string json)
    // {
    //   try
    //   {
    //     jsonUpgradeButtons = json;
    //     if (string.IsNullOrEmpty(jsonUpgrades))
    //       return;
    //
    //     Console.WriteLine("UpdateJsonUpgradeButtons");
    //     RefreshButtons(jsonUpgrades, jsonUpgradeButtons);
    //   }
    //   catch (Exception e)
    //   {
    //     Console.WriteLine(e);
    //   }
    // }

    private void UpgradeClicked(object sender, EventArgs e)
    {
      Console.WriteLine("Upgrade Clicked: " + sender);

      if (sender is Button button)
      {
        CurrentUpgrades.GetCurrentButtons().TryGetValue(button.Name, out var upgradeBtn);
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
            Upgrade(upgradeBtn);
          }
        }
      }
    }

    private void Unlock(Dictionary<string, UpgradeJoint> joints, Dictionary<string, UpgradeButton> buttons, UpgradeButton endButton, UpgradeJoint pJoint, string upgradeName, int delayTimeMS)
    {
      //TODO: use a good tweener to increase joint Animation value or make TimerHelper work with monogame deltatime to get each tick as callback
      foreach (var btn in buttons)
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
                  if (endButton.Data.UpgradeDefinition.ShortName == "CZS")
                  {
                    var level = endButton.CurrentLevel;
                    var state = level == endButton.Data.NumLevels ? UpgradeButton.UnlockState.MaxedOut : level == 0 ? UpgradeButton.UnlockState.Unlocked : UpgradeButton.UnlockState.Purchased;
                    SetButtonState(endButton, state);
                  }
                  else if (endButton.State < UpgradeButton.UnlockState.Unlocked)
                    SetButtonState(endButton, UpgradeButton.UnlockState.Unlocked);


                  foreach (var joint in joints)
                  {
                    if (joint.Value.StartButton.Button == endButton.Button)
                    {
                      // joint.Value.State = UpgradeJoint.JointState.Unlocking;
                      Unlock(joints, buttons, joint.Value.EndButton, joint.Value, upgradeName, 0);
                    }
                  }

                }, delayTimeMS, true);
          }
          else if (btn.Value.Data.LockedBy == upgradeName)
          {
            pJoint.State = delayTimeMS > 0 ? UpgradeJoint.JointState.Unlocking : UpgradeJoint.JointState.Unlocked;
            TimerHelper.DoAfter(() =>
                {
                  // var endButtonState = endButton.State;
                  // var newState = endButtonState < UpgradeButton.UnlockState.Revealed ?
                  //   UpgradeButton.UnlockState.Revealed : endButtonState;

                  if (endButton.State < UpgradeButton.UnlockState.Revealed)
                    SetButtonState(endButton, UpgradeButton.UnlockState.Revealed);

                  foreach (var joint in joints)
                  {
                    if (joint.Value.StartButton.Button == endButton.Button)
                    {
                      // joint.Value.State = UpgradeJoint.JointState.Unlocking;
                      Unlock(joints, buttons, joint.Value.EndButton, joint.Value, upgradeName, 0);
                    }
                  }

                }, delayTimeMS, true);
          }

        }
      }
    }

    private void Upgrade(UpgradeButton upgradeButton)
    {
      var upgradeData = upgradeButton.Data;
      var button = upgradeButton.Button;

      if (upgradeButton.CurrentLevel >= upgradeButton.Data.LevelInfo.Count)
        return;

      var currentLevelInfo = upgradeButton.Data.LevelInfo[upgradeButton.CurrentLevel];

      ulong currentValue = upgradeData.UpgradeDefinition.Currency switch
      {
        "red" => m_gameState.CurrentRedGemCount,
        "blue" => m_gameState.CurrentBlueGemCount,
        "purple" => m_gameState.CurrentPurpleGemCount,
        _ => 0
      };

      if (currentValue < (uint)currentLevelInfo.Cost)
      {
        Console.WriteLine("Not enough gems to purchase upgrade: " + upgradeData.ShortName);

        //TODO: Play error sound
        // AudioManager.Instance.MenuHoverButtonSoundEffect?.Play();

        return;
      }

      AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeStartEffect);

      string upgradeName = upgradeData.ShortName;

      Console.WriteLine("Upgrade: " + upgradeName);
      switch (upgradeData.UpgradeDefinition.Currency)
      {
        case "red":
          m_gameState.CurrentRedGemCount -= (uint)currentLevelInfo.Cost;
          break;
        case "blue":
          m_gameState.CurrentBlueGemCount -= (uint)currentLevelInfo.Cost;
          break;
        case "purple":
          m_gameState.CurrentPurpleGemCount -= (uint)currentLevelInfo.Cost;
          break;
      }

      if (upgradeName == "HB")
      {
        OnUpgradeRoot?.Invoke();
      }


      if (upgradeName == "ResetAbilities1")
      {
        ResetAbilities();
        UntitledGemGameGameScreen.Instance.SaveProgress();
        return;
      }
      // if (upgradeName == "RBG1")
      // {
      //   m_gameState.CurrentBlueGemCount = 0;
      //   foreach (var ub in CurrentUpgrades.UpgradeButtons)
      //   {
      //     var ud = ub.Value.Data.UpgradeDefinition;
      //
      //     if (ub.Value.State == UpgradeButton.UnlockState.Purchased && ud.ShortName == "BG")
      //     {
      //       m_gameState.CurrentBlueGemCount += (uint)ub.Value.Data.m_upgradeAmountInt;
      //     }
      //
      //     if (ud.Currency != "blue") continue;
      //
      //     UG.Reset(ud.ShortName);
      //
      //     bool f = CurrentUpgrades.UpgradeButtons.TryGetValue(ub.Value.Data.ShortName, out var v);
      //     if (f)
      //     {
      //       Console.WriteLine("Found: " + ub.Value.Data.ShortName);
      //       //if (ub.Value.Data.UpgradeDefinition.ShortName == "HBC")
      //       if (ub.Value.Data.ShortName == "AS1")
      //       {
      //         SetButtonState(ub.Value, UpgradeButton.UnlockState.Unlocked);
      //       }
      //       else
      //       {
      //         SetButtonState(ub.Value, UpgradeButton.UnlockState.Invisible);
      //       }
      //
      //       foreach (var l in CurrentUpgrades.UpgradeJoints)
      //       {
      //         if (l.Value.StartButton == ub.Value)
      //         {
      //           l.Value.State = UpgradeJoint.JointState.Hidden;
      //           l.Value.UnlockingTime = 0;
      //           l.Value.PurchasingTime = 0;
      //         }
      //
      //         if (l.Value.StartButton.Data.UpgradeDefinition.ShortName == "HB")
      //         {
      //           l.Value.State = UpgradeJoint.JointState.Unlocked;
      //           l.Value.UnlockingTime = 0;
      //           l.Value.PurchasingTime = 0;
      //         }
      //       }
      //     }
      //     else
      //     {
      //       Console.WriteLine("Not Found: " + ub.Value.Data.ShortName);
      //     }
      //   }
      //
      //   HomeBase.Instance.ResetAbilities();
      //   return;
      // }

      OnUpgrade?.Invoke(upgradeName);

      if (upgradeData.UpgradeDefinition.ShortName == "AP")
      {
        m_gameState.CurrentBlueGemCount += (uint)currentLevelInfo.m_upgradeAmountInt;
      }

      ApplyUpgradeEffect(upgradeData, currentLevelInfo);

      // if (upgradeButton.CurrentLevel == 0)
      {
        var joints = CurrentUpgrades.GetCurrentJoints();
        var buttons = CurrentUpgrades.GetCurrentButtons();
        //TODO: fix handling already level 2/5 for example buttons after the one you just updated (they get their status reset if maxed etc)
        foreach (var joint in joints)
        {
          if (joint.Value.StartButton.Button == button)
          {
            Unlock(joints, buttons, joint.Value.EndButton, joint.Value, upgradeName, 200);
          }
        }

        if (joints.TryGetValue(upgradeName, out var j))
        {
          j.State = UpgradeJoint.JointState.Purchasing;
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

      // var upgradeButton = CurrentUpgrades.UpgradeButtons[upgradeName];

      ++upgradeButton.CurrentLevel;

      SetButtonState(upgradeButton, upgradeButton.IsMaxLevel ? UpgradeButton.UnlockState.MaxedOut : UpgradeButton.UnlockState.Purchased);
      HideTooltip();
      ShowTooltip(button.Visual, button.Name, false);

      if (upgradeName == "CZS1")
      {
        UntitledGemGameGameScreen.Instance.BeginPrestige();
        ResetUpgrades();
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
        HideTooltip();
      }
      UntitledGemGameGameScreen.Instance.SaveProgress();
    }

    public void ResetUpgrades()
    {
      // m_gameState.CurrentBlueGemCount = 0;

      foreach (var ub in CurrentUpgrades.GetCurrentButtons())
      {
        var ud = ub.Value.Data.UpgradeDefinition;

        // if (ub.Value.State == UpgradeButton.UnlockState.Purchased && ud.ShortName == "BG")
        // {
        //   m_gameState.CurrentBlueGemCount += (uint)ub.Value.Data.m_upgradeAmountInt;
        // }

        if (ud.ShortName != "CZS")
          UG.Reset(ud.ShortName);

        bool f = CurrentUpgrades.GetCurrentButtons().TryGetValue(ub.Value.Data.ShortName, out var v);
        if (f)
        {
          Console.WriteLine("Found: " + ub.Value.Data.ShortName);

          if (ud.ShortName != "CZS")
            ub.Value.CurrentLevel = 0;
          //if (ub.Value.Data.UpgradeDefinition.ShortName == "HBC")
          if (ub.Value.Data.ShortName == "HB")
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

            // if (l.Value.StartButton.Data.UpgradeDefinition.ShortName == "HB")
            // {
            //   l.Value.State = UpgradeJoint.JointState.Unlocked;
            //   l.Value.UnlockingTime = 0;
            //   l.Value.PurchasingTime = 0;
            // }
          }
        }
        else
        {
          Console.WriteLine("Not Found: " + ub.Value.Data.ShortName);
        }
      }
    }

    private void ResetAbilities()
    {
      // m_gameState.CurrentBlueGemCount = 0;
      foreach (var ub in CurrentUpgrades.UpgradeButtonsAbilities)
      {
        var ud = ub.Value.Data.UpgradeDefinition;

        //Refund 
        // if (ub.Value.State == UpgradeButton.UnlockState.Purchased && ud.ShortName == "BG")
        // {
        //   var numLevels = ub.Value.Data.NumLevels;
        //   for (int i = 0; i < numLevels; ++i)
        //   {
        //     m_gameState.CurrentBlueGemCount += (uint)ub.Value.Data.LevelInfo[i].m_upgradeAmountInt;
        //   }
        // }

        // if (ud.Currency != "blue") continue;

        var cur = ub.Value.CurrentLevel;
        var max = ub.Value.Data.NumLevels;

        if (cur > 0)
        {
          for (int i = 0; i < cur; ++i)
          {
            m_gameState.CurrentBlueGemCount += ub.Value.Data.LevelInfo[i].Cost;
          }
        }

        UGA.Reset(ud.ShortName);

        bool f = CurrentUpgrades.UpgradeButtonsAbilities.TryGetValue(ub.Value.Data.ShortName, out var v);
        if (f)
        {
          v.CurrentLevel = 0;
          Console.WriteLine("Found: " + ub.Value.Data.ShortName);
          //if (ub.Value.Data.UpgradeDefinition.ShortName == "HBC")
          if (ub.Value.Data.ShortName == "AS1")
          {
            SetButtonState(ub.Value, UpgradeButton.UnlockState.Unlocked);
          }
          else
          {
            SetButtonState(ub.Value, UpgradeButton.UnlockState.Invisible);
          }

          foreach (var l in CurrentUpgrades.UpgradeJointsAbilities)
          {
            if (l.Value.StartButton == ub.Value)
            {
              l.Value.State = UpgradeJoint.JointState.Hidden;
              l.Value.UnlockingTime = 0;
              l.Value.PurchasingTime = 0;
            }

            if (l.Value.StartButton.Data.UpgradeDefinition.ShortName == "AS1")
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
    }

    private readonly Tweener _tweener = new();
    private string prevOverButtonName = "";
    private string openTooltipButtonName = "";
    private string draggingButtonNameEditMode = "";

    public Window m_tooltipWindow;
    public Window m_tooltipExtraWindow;
    private FontStashSharpText m_tooltipLabel;
    private FontStashSharpText m_tooltipDescription;
    private FontStashSharpText m_tooltipCost;
    private FontStashSharpText m_tooltipValueFrom;
    private FontStashSharpText m_tooltipValueTo;
    private FontStashSharpText m_tooltipPercentage;
    private FontStashSharpText m_tooltipPuchasedText;
    public NineSliceRuntime m_toolTipTitleBackground;


    // private TextRuntime m_tooltipDescription;

    // private NineSliceRuntime m_tooltipValueIcon;
    private SpriteRuntime m_tooltipValueIcon;
    // private NineSliceRuntime m_tooltipCostIcon;
    private SpriteRuntime m_tooltipCostIconRed;
    private SpriteRuntime m_tooltipCostIconBlue;
    private SpriteRuntime m_tooltipCostIconPurple;
    private UpgradeButton m_currentTooltipButton = null;


    private Color greenColor = new Color(29, 188, 96);
    private Color redColor = new Color(204, 62, 62, 255);


    public List<GraphicalUiElement> m_tooltipValueElements = new();
    private FontStashSharpText m_tooltipExtraText;

    public void Update(GameTime gameTime)
    {
      if (UpdatingButtons)
        return;

      var buttons = CurrentUpgrades.GetCurrentButtons();

      var ms = MouseExtended.GetState();
      var kb = KeyboardExtended.GetState();

      var curOverButtonName = Gum.GumService.Default.Cursor.VisualOver?.Name ?? "null";

      _tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      var buttonVis = Gum.GumService.Default.Cursor.VisualOver;

      // Console.WriteLine("c: " + curOverButtonName + " - p: " + buttonVis?.Parent?.Name + " - pp: " + buttonVis?.Parent?.Parent?.Name);
      bool isButton = buttonVis != null;

      foreach (var btn in buttons) //TODO: or all?
      {
        if (btn.Value.Button == null)
          continue;

        var currency = btn.Value.Data.UpgradeDefinition.Currency;
        ulong gemCount = currency switch
        {
          "red" => m_gameState.CurrentRedGemCount,
          "blue" => m_gameState.CurrentBlueGemCount,
          "purple" => m_gameState.CurrentPurpleGemCount,
          _ => 0
        };

        if (!btn.Value.IsMaxLevel)
        {
          if ((uint)btn.Value.GetNextLevelCost() > gemCount)
          {
            btn.Value.CanAfford = false;
          }
          else if ((uint)btn.Value.GetNextLevelCost() <= gemCount)
          {
            btn.Value.CanAfford = true;
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
          // else if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.C))
          // {
          //   //Clone upgrade button
          //   var origShortName = m_selectedButtonEditMode.Data.ShortName;
          //   var defShortName = m_selectedButtonEditMode.Data.UpgradeDefinition.ShortName;
          //
          //   if (CurrentUpgrades.UpgradeButtons.TryGetValue(origShortName, out var origButton))
          //   {
          //     // string newShortName = MyRegex().Replace(origShortName, match =>
          //     // {
          //     //   int number = int.Parse(match.Value);
          //     //   return (number + 1).ToString();
          //     // });
          //
          //     string newShortName = defShortName + "1";
          //     int count = 1;
          //     while (CurrentUpgrades.UpgradeButtons.ContainsKey(newShortName))
          //     {
          //       newShortName = defShortName + count.ToString();
          //       ++count;
          //     }
          //
          //     var upgradeButton = CurrentUpgrades.AddNewButton(newShortName, origButton.Data.UpgradeDefinition);
          //     var button = CreateButton(new KeyValuePair<string, UpgradeButton>(newShortName, CurrentUpgrades.UpgradeButtons[newShortName]));
          //
          //     var camera = SystemManagers.Default.Renderer.Camera;
          //     var sp = BaseGame.BoxingViewportAdapter.PointToScreen(ms.X, ms.Y);
          //     camera.ScreenToWorld(sp.X, sp.Y, out var X2, out var Y2);
          //
          //     button.X = X2;
          //     button.Y = Y2;
          //
          //     upgradeButton.Data.PosX = (int)X2;
          //     upgradeButton.Data.PosY = (int)Y2;
          //
          //     upgradeButton.Data.HiddenBy = origButton.Data.HiddenBy;
          //     upgradeButton.Data.LockedBy = origButton.Data.LockedBy;
          //     upgradeButton.Data.BlockedBy = origButton.Data.ShortName;
          //
          //     upgradeButton.Data.Cost = origButton.Data.Cost;
          //     upgradeButton.Data.m_upgradeAmountFloat = origButton.Data.m_upgradeAmountFloat;
          //     upgradeButton.Data.m_upgradeAmountInt = origButton.Data.m_upgradeAmountInt;
          //     upgradeButton.Data.m_upgradesToBool = origButton.Data.m_upgradesToBool;
          //
          //     draggingButtonNameEditMode = newShortName;
          //     m_selectedButtonEditMode = upgradeButton;
          //   }
          // }

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

            if (buttons.TryGetValue(draggingButtonNameEditMode, out var button))
            {
              button.Button.X = X2;
              button.Button.Y = Y2;

              CurrentUpgrades.GetCurrentButtons()[draggingButtonNameEditMode].Data.PosX = (int)button.Button.X;
              CurrentUpgrades.GetCurrentButtons()[draggingButtonNameEditMode].Data.PosY = (int)button.Button.Y;
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
        m_tooltipExtraWindow.IsVisible = false;
        m_currentTooltipButton = null;
      }
    }

    private void CreateToolTipExtraWindow()
    {
      m_tooltipExtraWindow = new Window()
      {
        Name = "UpgradeTooltipExtraWindow",
      };

      var vis = m_tooltipExtraWindow.Visual;
      m_tooltipExtraWindow.Width = 300;
      m_tooltipExtraWindow.Height = 150;

      var sprite = new NineSliceRuntime()
      {
        Texture = AssetManager.Load<Texture2D>("Textures/GUI/Button Normal.png"),
        Width = m_tooltipExtraWindow.Width,
        Height = m_tooltipExtraWindow.Height,
        Color = new Color(0, 0, 0, 255),
      };

      for (int i = vis.Children.Count - 1; i >= 2; --i)
      {
        vis.Children.RemoveAt(i);
      }

      vis.Children.RemoveAt(0);
      vis.Children.Insert(0, sprite);

      m_tooltipExtraText = new FontStashSharpText()
      {
        Text = "",
        TextAlignment = TextAlignment.Left,
        FontSize = 18,
        WrapText = true,
        // FillColor = new Color(255, 186, 21, 255),
      };

      var tooltipElement = new GraphicalUiElement(m_tooltipExtraText)
      {
        XOrigin = HorizontalAlignment.Left,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        X = 30,
        Y = 25,
      };

      vis.AddChild(tooltipElement);

      m_tooltipExtraWindow.AddToRoot();
      m_tooltipExtraWindow.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_popupLayer);
      RenderGuiSystem.Instance.skillTreeItems.Add(m_tooltipExtraWindow.Visual);
      m_tooltipWindow.IsVisible = false;
    }

    private void CreateToolTipWindow()
    {
      m_tooltipWindow = new Window()
      {
        Name = "UpgradeTooltipWindow",
      };

      var vis = m_tooltipWindow.Visual;
      m_tooltipWindow.Width = 500;
      m_tooltipWindow.Height = 380;

      var sprite = new NineSliceRuntime()
      {
        Texture = AssetManager.Load<Texture2D>("Textures/GUI/Button Normal.png"),
        Width = m_tooltipWindow.Width,
        Height = m_tooltipWindow.Height - 30,
        Color = new Color(0, 0, 0, 255),
        // TextureAddress = Gum.Managers.TextureAddress.EntireTexture
      };


      foreach (var a in vis.Children)
      {
        //         Tooltip: Background
        // Tooltip: InnerPanelInstance
        // Tooltip: TitleBarInstance
        // Tooltip: BorderTopLeftInstance
        // Tooltip: BorderTopRightInstance
        // Tooltip: BorderBottomLeftInstance
        // Tooltip: BorderBottomRightInstance
        // Tooltip: BorderTopInstance
        // Tooltip: BorderBottomInstance
        // Tooltip: BorderLeftInstance
        // Tooltip: BorderRightInstance
        Console.WriteLine("Tooltip: " + a.Name);
        Console.WriteLine("  " + a.GetType());
        if (a.Name == "Background")
        {
          var bg = a as NineSliceRuntime;
          bg.Color = new Color(0, 0, 0, 255);
        }
      }

      for (int i = vis.Children.Count - 1; i >= 2; --i)
      {
        vis.Children.RemoveAt(i);
      }

      vis.Children.RemoveAt(0);
      vis.Children.Insert(0, sprite);

      m_tooltipLabel = new FontStashSharpText()
      {
        TextAlignment = TextAlignment.Center,
        FontSize = 32,
        FillColor = new Color(255, 186, 21, 255),
      };

      var m_tooltipLabelContainer = new GraphicalUiElement(m_tooltipLabel);

      var stackPanel = new StackPanel()
      {

      };

      // m_tooltipLabelContainer.XOrigin = HorizontalAlignment.Center;
      stackPanel.Visual.YOrigin = VerticalAlignment.Top;

      m_tooltipLabelContainer.XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle;
      m_tooltipLabelContainer.Y = 10;
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

      // var r = new RectangleRuntime()
      // {
      //   Color = new Color(100, 100, 100, 255),
      //   WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   X = 20,
      //   Y = 10,
      //   Width = -40,
      //   Height = 2,
      // };


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
        Y = 60,

        // XOrigin = HorizontalAlignment.Center,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // X = 0,
        // Y = 30,
      };

      m_tooltipPuchasedText = new FontStashSharpText()
      {
        Text = "MAXED OUT",
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

      // var border = new RectangleRuntime()
      // {
      //   StrokeColor = new Color(255, 100, 100, 250),
      //   WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   X = 0,
      //   Y = 0,
      //   Width = 0,
      //   Height = 0,
      // };

      var background = new RectangleRuntime()
      {
        FillColor = new Color(255, 0, 0, 255),
        StrokeColor = new Color(0, 0, 0, 0),
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        X = 0,
        Y = 0,
        Width = 0,
        Height = 0,
      };

      // var backgroundSprite = new NineSliceRuntime()
      // {
      //   Texture = TextureCache.TooltipBackground,
      //   WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      //   X = 0,
      //   Y = 0,
      //   Width = 0,
      //   Height = 0,
      // };


      m_toolTipTitleBackground = new NineSliceRuntime()
      {
        // Texture = TextureCache.TooltipTitleBackground,
        Texture = AssetManager.Load<Texture2D>("Textures/GUI/Button Normal.png"),
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromMiddle,
        X = -m_tooltipWindow.Width / 2.0f + 25,
        // X = -91.5f * 2.0f,
        Y = 30,
        Width = m_tooltipWindow.Width - 50,
        Height = 50,
        Color = new Color(0, 0, 0, 255)
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
      (Texture2D tex, Texture2DRegion region) purple = AsepriteHelper.LoadTextureFromAnimationFrame("Textures/Gems/Gem5/GEM 5 - LILAC - Spritesheet.png", 0, 11);

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

      m_tooltipCostIconPurple = new SpriteRuntime()
      {
        Texture = purple.tex,
        SourceRectangle = purple.region.Bounds,
        // Width = costTex2.Width * 3.0f,
        // Height = costTex2.Height * 3.0f,
        TextureAddress = Gum.Managers.TextureAddress.Custom,
        // YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        // XUnits = Gum.Converters.GeneralUnitType.PixelsFromBaseline,
        // X = 10,
        Y = 4,
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


      m_tooltipPercentage = new FontStashSharpText()
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

      var percentageElement = new GraphicalUiElement(m_tooltipPercentage)
      {
        XOrigin = HorizontalAlignment.Right,
        YOrigin = VerticalAlignment.Bottom,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        YUnits = Gum.Converters.GeneralUnitType.PixelsFromLarge,
        X = -15,
        Y = -55
      };

      m_tooltipValueElements.Add(valueElementFrom);
      m_tooltipValueElements.Add(valueElementTo);
      m_tooltipValueElements.Add(percentageElement);
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
      // background.AddChild(backgroundSprite);
      background.AddChild(m_toolTipTitleBackground);
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
      costStackpanel.AddChild(m_tooltipCostIconPurple);

      // background.AddChild(m_tooltipCostIcon);

      background.AddChild(costStackpanel);
      background.AddChild(purchasedElement);
      background.AddChild(percentageElement);
      background.AddChild(valueStackpanel);

      m_tooltipWindow.AddChild(background);

      // m_tooltipWindow.Visual.XOrigin = RenderingLibrary.Graphics.HorizontalAlignment.Center;
      m_tooltipWindow.AddToRoot();
      m_tooltipWindow.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, RenderGuiSystem.Instance.m_popupLayer);
      RenderGuiSystem.Instance.skillTreeItems.Add(m_tooltipWindow.Visual);
    }

    public void UpdateTooltipContent()
    {
      if (m_currentTooltipButton == null)
        return;

      var currency = m_currentTooltipButton.Data.UpgradeDefinition.Currency;
      var currentLevelInfo = m_currentTooltipButton.GetNextLevelInfo();

      switch (currency)
      {
        case "red":
          m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
          break;
        case "blue":
          m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
          break;
        case "purple":
          m_tooltipCost.FillColor = m_gameState.CurrentPurpleGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
          break;
        default:
          m_tooltipCost.FillColor = Color.White;
          break;
      }
    }

    public static double GetUpgradePercentage(int oldValue, int newValue)
    {
      // Prevent division by zero if the base value is 0
      if (oldValue == 0)
      {
        return 0.0; // Or handle based on your game logic
      }

      // Cast to double so C# doesn't truncate the decimal points
      return ((double)(newValue - oldValue) / oldValue) * 100.0;
    }
    public static double GetUpgradePercentage(float oldValue, float newValue)
    {
      // Prevent division by zero if the base value is 0
      if (oldValue == 0)
      {
        return 0.0; // Or handle based on your game logic
      }

      // Cast to double so C# doesn't truncate the decimal points
      return ((double)(newValue - oldValue) / oldValue) * 100.0;
    }

    private string SpecialCaseTooltip(string tooltip, bool purchased)
    {
      string s = "";
      if (tooltip == "CollectionStrategy")
      {
        if (purchased)
        {
          s = "Current Strategy: " + Enum.GetName(typeof(HarvesterStrategy), UG.HarvesterCollectionStrategy);
        }
        else
        {
          s = Enum.GetName(typeof(HarvesterStrategy), UG.HarvesterCollectionStrategy)
            + Environment.NewLine
            + " -> "
            + Environment.NewLine
            + Enum.GetName(typeof(HarvesterStrategy), UG.HarvesterCollectionStrategy + 1);
        }

        tooltip = "Upgrades how the harvesters and drones find their next position to move to.";
        tooltip += Environment.NewLine;
        tooltip += Environment.NewLine;
        tooltip += s;
      }

      return tooltip;
    }

    private void ShowTooltip(InteractiveGue buttonVis, string buttonName, bool doAnimation = true)
    {
      if (m_tooltipWindow == null)
      {
        CreateToolTipWindow();
        CreateToolTipExtraWindow();
      }

      var buttons = CurrentUpgrades.GetCurrentButtons();

      if (buttons.TryGetValue(buttonName, out var upgradeBtn))
      {
        m_currentTooltipButton = upgradeBtn;

        var upgrade = upgradeBtn.Data.UpgradeDefinition;
        var upgradeName = upgrade.Name;

        var purchased = upgradeBtn.State == UpgradeButton.UnlockState.Purchased;
        var maxedOut = upgradeBtn.State == UpgradeButton.UnlockState.MaxedOut;
        var hidden = upgradeBtn.State == UpgradeButton.UnlockState.Hidden;
        var invisible = upgradeBtn.State == UpgradeButton.UnlockState.Invisible;
        var demoLocked = upgradeBtn.State == UpgradeButton.UnlockState.DemoLocked;


        var tooltip = SpecialCaseTooltip(upgrade.Tooltip, purchased);
        if (upgrade.ShortName == "CZS")
        {
          ulong reward = PrestigeProgression.GetReward(UntitledGemGameGameScreen.Instance.GetPrestigeEarnings());
          tooltip += Environment.NewLine + Environment.NewLine
            + $"Prestige now: +{reward:N0} purple gems"
            + Environment.NewLine + "Includes spent red gems, carried cargo and gems on the field.";
        }
        if (upgrade.ShortName == "MA" || upgrade.ShortName == "MAC")
        {
          int multicastLevel = UGM.MulticastAbilitiesLevel;
          string label = UGM.MulticastAbilities ? "Current chances:" : "Chances when unlocked:";
          if (upgrade.ShortName == "MAC")
          {
            bool showNextLevel = !maxedOut && multicastLevel < MulticastTable.MaxLevel;
            label = showNextLevel ? "Next level:" : "Current chances:";
            if (showNextLevel)
              ++multicastLevel;
          }
          tooltip += Environment.NewLine + Environment.NewLine + label + Environment.NewLine
            + MulticastTable.GetChances(multicastLevel).Describe();
        }


        if (invisible) return;
        if (m_tooltipPercentage == null) return;

        var targetPosY = buttonVis.Y + 100;

        m_tooltipPercentage.Text = "";

        if (demoLocked)
        {
          m_tooltipLabel.Text = $"Not available in Demo Mode";
          m_tooltipDescription.Text = $"???";

          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;

          m_tooltipCost.Text = "";
          // m_tooltipPuchasedText.Visible = true;
          m_tooltipCostIconRed.Visible = false;
          m_tooltipCostIconBlue.Visible = false;
          m_tooltipCostIconPurple.Visible = false;
          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;
        }
        else if (hidden)
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
          m_tooltipCostIconPurple.Visible = false;
          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;
        }
        else
        {
          var level = upgradeBtn.CurrentLevel + " / " + upgradeBtn.Data.NumLevels;
          m_tooltipLabel.Text = $"{upgradeName}" + " - " + level;
          m_tooltipDescription.Text = $"{tooltip}";
        }

        if (maxedOut)
        {
          m_tooltipCost.Text = "";
          m_tooltipPuchasedText.Visible = true;
          m_tooltipCostIconRed.Visible = false;
          m_tooltipCostIconBlue.Visible = false;
          m_tooltipCostIconPurple.Visible = false;
          m_tooltipValueFrom.Text = "";
          m_tooltipValueTo.Text = "";
          m_tooltipValueIcon.Visible = false;

          switch (upgrade.Type)
          {
            case "int":
              {
                var val = GetInt(upgrade.ShortName);
                m_tooltipValueTo.Text = $"{val}";

                if (upgradeBtn.Data.TooltipShowPercentage)
                {
                  m_tooltipValueTo.Text = $"+{val * 100.0f:0.##}%";
                }

              }
              break;
            case "float":
              {
                var val = GetFloat(upgrade.ShortName);
                m_tooltipValueTo.Text = $"{val}";

                if (upgradeBtn.Data.TooltipShowPercentage)
                {
                  m_tooltipValueTo.Text = $"+{val * 100.0f:0.##}%";
                }
              }
              break;
            default:
              m_tooltipValueFrom.Text = "";
              m_tooltipValueTo.Text = "";
              m_tooltipValueIcon.Visible = false;
              m_tooltipPercentage.Text = "";
              break;
          }
        }
        else if (!hidden)
        {
          var currentLevelInfo = upgradeBtn.GetNextLevelInfo();

          m_tooltipPuchasedText.Visible = false;
          ulong cost = currentLevelInfo.Cost;
          var s = NumberFormatter.AbbreviateBigNumber(cost, true);
          m_tooltipCost.Text = s;

          switch (upgrade.Currency)
          {
            case "red":
              m_tooltipCost.FillColor = m_gameState.CurrentRedGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
              break;
            case "blue":
              m_tooltipCost.FillColor = m_gameState.CurrentBlueGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
              break;
            case "purple":
              m_tooltipCost.FillColor = m_gameState.CurrentPurpleGemCount >= (uint)currentLevelInfo.Cost ? greenColor : redColor;
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
                var val = GetInt(upgrade.ShortName);
                m_tooltipValueFrom.Text = $"{val}";
                m_tooltipValueTo.Text = $"{val + currentLevelInfo.m_upgradeAmountInt}";

                if (upgradeBtn.Data.TooltipShowPercentage)
                {
                  // var percentChange = GetUpgradePercentage(val, val + currentLevelInfo.m_upgradeAmountInt);
                  // m_tooltipPercentage.Text = $"+{percentChange:0.##}%";

                  m_tooltipValueFrom.Text = $"+{currentLevelInfo.m_upgradeAmountInt * 100.0f:0.##}%";
                  m_tooltipValueTo.Text = $"+{(val + currentLevelInfo.m_upgradeAmountInt) * 100.0f:0.##}%";
                }
              }
              break;
            case "float":
              {
                var val = GetFloat(upgrade.ShortName);
                m_tooltipValueFrom.Text = $"{val}";
                m_tooltipValueTo.Text = $"{val + currentLevelInfo.m_upgradeAmountFloat}";

                if (upgradeBtn.Data.TooltipShowPercentage)
                {
                  // var percentChange = GetUpgradePercentage(val, val + currentLevelInfo.m_upgradeAmountFloat);
                  // m_tooltipPercentage.Text = $"+{percentChange:0.##}%";

                  m_tooltipValueFrom.Text = $"+{val * 100.0f:0.##}%";
                  m_tooltipValueTo.Text = $"+{(val + currentLevelInfo.m_upgradeAmountFloat) * 100.0f:0.##}%";

                }
              }
              break;
            default:
              m_tooltipValueFrom.Text = "";
              m_tooltipValueTo.Text = "";
              m_tooltipValueIcon.Visible = false;
              m_tooltipPercentage.Text = "";
              break;
          }

          switch (upgrade.Currency)
          {
            case "red":
              m_tooltipCostIconRed.Visible = true;
              m_tooltipCostIconBlue.Visible = false;
              m_tooltipCostIconPurple.Visible = false;
              break;
            case "blue":
              m_tooltipCostIconRed.Visible = false;
              m_tooltipCostIconBlue.Visible = true;
              m_tooltipCostIconPurple.Visible = false;
              break;
            case "purple":
              m_tooltipCostIconRed.Visible = false;
              m_tooltipCostIconBlue.Visible = false;
              m_tooltipCostIconPurple.Visible = true;
              break;
            default:
              m_tooltipCostIconRed.Visible = false;
              m_tooltipCostIconBlue.Visible = false;
              m_tooltipCostIconPurple.Visible = false;
              break;
          }
        }

        m_tooltipExtraText.Text = upgrade.TooltipExtra;


        m_tooltipWindow.IsVisible = true;
        m_tooltipWindow.X = buttonVis.X - m_tooltipWindow.Width / 2 + buttonVis.Width / 2;
        m_tooltipWindow.Y = targetPosY;

        if (!string.IsNullOrWhiteSpace(upgrade.TooltipExtra))
        {
          m_tooltipExtraWindow.IsVisible = true;
          m_tooltipExtraWindow.X = m_tooltipWindow.X + m_tooltipWindow.Width + 25;
          m_tooltipExtraWindow.Y = targetPosY;
        }
        else
        {
          m_tooltipExtraWindow.IsVisible = false;
        }

        AudioManager.Instance.PlaySound(AudioManager.Instance.ToolTipShowEffect);

        if (doAnimation)
        {
          m_tooltipWindow.Height = 0;

          _tweener.TweenTo(target: m_tooltipWindow, expression: win => win.Height, toValue: 350, duration: 0.25f)
                          .Easing(EasingFunctions.CubicOut);
        }


        var camera = SystemManagers.Default.Renderer.Camera;
#if !KNI_WEB
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
#endif
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
          m_tooltipWindow.X = Gum.GumService.Default.CanvasWidth / 2.0f - m_tooltipWindow.Width / 2.0f;

          var y = buttonVis.AbsoluteY;

          // y = Math.Min(y, vp.Height - m_tooltipWindow.Height - 125);
          y = Math.Min(y, Gum.GumService.Default.CanvasHeight - m_tooltipWindow.Height - 260);

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
              m_tooltipCostIconPurple.Visible = false;
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
              m_tooltipWindow.X = Gum.GumService.Default.CanvasWidth / 2.0f - m_tooltipWindow.Width / 2.0f;

              var y = buttonVis.AbsoluteY;

              // var vp = BaseGame.BoxingViewportAdapter.Viewport;

              y = Math.Min(y, Gum.GumService.Default.CanvasHeight - m_tooltipWindow.Height - 260);

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
