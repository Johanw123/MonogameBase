using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

[JsonSerializable(typeof(JsonUpgrade))]
[JsonSerializable(typeof(UpgradeData))]
[JsonSerializable(typeof(RootUpgrades))]
internal sealed partial class SerializerContext : JsonSerializerContext;

[JsonSerializable(typeof(JsonUpgrade))]
[JsonSerializable(typeof(UpgradeData))]
[JsonSerializable(typeof(RootUpgradeButtons))]
internal sealed partial class SerializerContext2 : JsonSerializerContext;

public class UpgradeDataLevel
{
  public ulong Cost = 0;
  public int RequiredExpandSpaceLevel;
  public float m_upgradeAmountFloat;
  public int m_upgradeAmountInt;
  public bool m_upgradesToBool;
}

public class UpgradeData
{
  public string ShortName;

  public JsonUpgrade UpgradeDefinition;

  public int PosX;
  public int PosY;

  public string HiddenBy;
  public string LockedBy;
  public string BlockedBy;

  public int NumLevels;

  public List<UpgradeDataLevel> LevelInfo = new List<UpgradeDataLevel>();

  public bool AddMidPoint;
  public bool SwapMidPointAxis;
  public bool LockedInDemo;
  public bool TooltipShowPercentage;

  public float ButtonSizeScale = 1.0f;

  // public UpgradeData(string shortName, float upgradeAmount)
  // {
  //   ShortName = shortName;
  //   m_upgradeAmountFloat = upgradeAmount;
  // }
  //
  // public UpgradeData(string shortName, int upgradeAmount)
  // {
  //   ShortName = shortName;
  //   m_upgradeAmountInt = upgradeAmount;
  // }
  //
  // public UpgradeData(string shortName, bool upgradesTo)
  // {
  //   ShortName = shortName;
  //   m_upgradesToBool = upgradesTo;
  // }
}

public class JsonButton
{
  // [JsonPropertyName("name")]
  // public string Name { get; set; }

  [JsonPropertyName("shortname")]
  public string Shortname { get; set; }

  // [JsonPropertyName("type")]
  // public string Type { get; set; }

  [JsonPropertyName("cost")]
  public List<string> Cost { get; set; }

  [JsonPropertyName("value")]
  public List<string> Value { get; set; }

  [JsonPropertyName("posx")]
  public string PosX { get; set; }

  [JsonPropertyName("posy")]
  public string PosY { get; set; }

  // [JsonPropertyName("propname")]
  // public string PropertyName { get; set; }

  [JsonPropertyName("upgrade")]
  public string Upgrade { get; set; }

  [JsonPropertyName("hiddenby")]
  public string HiddenBy { get; set; }

  [JsonPropertyName("lockedby")]
  public string LockedBy { get; set; }

  [JsonPropertyName("blockedby")]
  public string BlockedBy { get; set; }

  [JsonPropertyName("addmidpoint")]
  public string AddMidPoint { get; set; } = "true";

  [JsonPropertyName("swapmidpointaxis")]
  public string SwapMidPointAxis { get; set; } = "true";

  [JsonPropertyName("lockedindemo")]
  public string LockedInDemo { get; set; } = "false";

  [JsonPropertyName("requiredexpandspacelevels")]
  public List<string> RequiredExpandSpaceLevels { get; set; } = new();

  // Legacy node requirement, used when no per-level requirements are present.
  [JsonPropertyName("requiredexpandspacelevel")]
  public string RequiredExpandSpaceLevel { get; set; } = "0";

  [JsonPropertyName("tooltippercentage")]
  public string TooltipShowPercentage { get; set; } = "true";

  [JsonPropertyName("buttonsizescale")]
  public string ButtonSizeScale { get; set; } = "1.0";

  [JsonPropertyName("numlevels")]
  public string NumLevels { get; set; } = "1";

}

public class RootUpgrades
{
  [JsonPropertyName("upgrades")]
  public List<JsonUpgrade> Upgrades { get; set; }
}


public class RootUpgradeButtons
{
  [JsonPropertyName("windowwidth")]
  public string WindowWidth { get; set; }

  [JsonPropertyName("windowheight")]
  public string WindowHeight { get; set; }

  [JsonPropertyName("buttons")]
  public List<JsonButton> Buttons { get; set; }
}

public class JsonUpgrade
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("propname")]
  public string PropertyName { get; set; }

  [JsonPropertyName("shortname")]
  public string ShortName { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("base")]
  public string BaseValue { get; set; }

  [JsonPropertyName("tooltip")]
  public string Tooltip { get; set; }

  [JsonPropertyName("tooltip_extra")]
  public string TooltipExtra { get; set; }

  [JsonPropertyName("currency")]
  public string Currency { get; set; } = "red";

  [JsonPropertyName("icon")]
  public string Icon { get; set; } = "";
}
