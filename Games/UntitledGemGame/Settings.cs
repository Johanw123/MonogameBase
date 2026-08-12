using System.Text.Json.Serialization;

public class Settings
{
  public int X { get; set; } = 0;
  public int Y { get; set; } = 0;
  public int Width { get; set; } = -1;
  public int Height { get; set; } = -1;
  public bool IsFixedTimeStep { get; set; } = true;
  public bool IsVSync { get; set; } = true;
  public bool IsFullscreen { get; set; } = true;
  public bool IsBorderless { get; set; } = true;

  public float MusicVolume { get; set; } = 0.25f;
  public float SfxVolume { get; set; } = 0.5f;
}

#if !KNI_WEB
[JsonSourceGenerationOptions(
     PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
     WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsContext : JsonSerializerContext { }
#endif