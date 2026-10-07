using System.Text.Json.Serialization;

public class Settings
{
  public int X { get; set; } = 0;
  public int Y { get; set; } = 0;
  public int Width { get; set; } = -1;
  public int Height { get; set; } = -1;
  public int? PopoutX { get; set; }
  public int? PopoutY { get; set; }
  public int PopoutWidth { get; set; } = 1280;
  public int PopoutHeight { get; set; } = 720;
  public bool IsFixedTimeStep { get; set; } = true;
  public bool IsVSync { get; set; } = true;
  public bool IsFullscreen { get; set; } = true;
  public bool IsBorderless { get; set; } = true;

  public float MusicVolume { get; set; } = 0.25f;
  public float SfxVolume { get; set; } = 0.5f;

  // A GameLanguage code, or empty to follow the Steam game language (else the system's).
  public string Language { get; set; } = "";

  // Whether the HUD's Damage panel is open.
  public bool DamagePanelOpen { get; set; }
}

#if !KNI_WEB
[JsonSourceGenerationOptions(
     PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
     WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsContext : JsonSerializerContext { }
#endif
