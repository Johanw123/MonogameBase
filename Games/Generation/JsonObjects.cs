using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

[JsonSerializable(typeof(Root))]
internal sealed partial class SerializerContext : JsonSerializerContext;

public class JsonButton
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("shortname")]
  public string Shortname { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("cost")]
  public string Cost { get; set; }

  [JsonPropertyName("value")]
  public string Value { get; set; }

  [JsonPropertyName("posx")]
  public string PosX { get; set; }

  [JsonPropertyName("posy")]
  public string PosY { get; set; }

  [JsonPropertyName("propname")]
  public string PropertyName { get; set; }

  [JsonPropertyName("upgrade")]
  public string Upgrade { get; set; }

  [JsonPropertyName("hiddenby")]
  public string HiddenBy { get; set; }

  [JsonPropertyName("lockedby")]
  public string LockedBy { get; set; }
}

public class Root
{
  [JsonPropertyName("upgrades")]
  public List<JsonUpgrade> Upgrades { get; set; }

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
}
