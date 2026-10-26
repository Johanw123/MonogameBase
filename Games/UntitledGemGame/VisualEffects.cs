using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncContent;
using BloomPostprocess;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Art direction defaults, independent of player saves. Fields support ImGui's ref controls.
public sealed class VisualEffectsSettings
{
  public bool Enabled = true;
  public bool Animate = true;
  public bool SoftBloom = true;
  public bool Background = true;
  public float BackgroundBrightness = 0.78f;
  public float BackgroundSaturation = 0.8f;
  public float CloudOpacity = 0.65f;
  public float StarOpacity = 0.85f;
  public bool Fog = true;
  public float FogDensity = 0.16f;
  public float FogScale = 6.0f;
  public float FogSpeed = 0.035f;
  public float FogCoverage = 0.55f;
  public float FogRed = 0.1f;
  public float FogGreen = 0.22f;
  public float FogBlue = 0.42f;
  public bool Motion = true;
  public float DriftSpeed = 2.0f;
  public float Parallax = 0.18f;
  public float CloudWarp = 0.004f;
  public float Twinkle = 0.12f;
  public float TwinkleSpeed = 0.65f;
  public bool Bloom = true;
  public float Threshold = 0.34f;
  public float Intensity = 1.05f;
  public float Blur = 4.0f;
  public float BloomSaturation = 1.1f;
  public float SoftKnee = 0.45f;
  public float Halo = 0.22f;
  public float HaloRadius = 8.0f;
  public float Anamorphic = 1.0f;
  public bool Grading = true;
  public float Exposure = 0.05f;
  public float Contrast = 1.04f;
  public float Saturation = 1.04f;
  public float Temperature = -0.025f;
  public float Tint = 0.0f;
  public float ShadowLift = 0.0f;
  public float HighlightRolloff = 0.08f;
  public bool Vignette = true;
  public float VignetteStrength = 0.16f;
  public float VignetteRadius = 0.35f;
  public float VignetteSoftness = 0.55f;
  public bool Aberration = false;
  public float AberrationPixels = 0.6f;
  public bool Grain = false;
  public float GrainAmount = 0.012f;
  public bool Sharpen = false;
  public float SharpenAmount = 0.15f;
  public bool Lens = false;
  public float LensAmount = 0.025f;

  public VisualEffectsSettings Copy() => (VisualEffectsSettings)MemberwiseClone();
  public void Validate()
  {
    Check(BackgroundBrightness, 0.0f, 1.5f, nameof(BackgroundBrightness));
    Check(BackgroundSaturation, 0.0f, 2.0f, nameof(BackgroundSaturation));
    Check(CloudOpacity, 0.0f, 1.0f, nameof(CloudOpacity));
    Check(StarOpacity, 0.0f, 1.0f, nameof(StarOpacity));
    Check(FogDensity, 0.0f, 0.65f, nameof(FogDensity));
    Check(FogScale, 1.0f, 20.0f, nameof(FogScale));
    Check(FogSpeed, 0.0f, 0.3f, nameof(FogSpeed));
    Check(FogCoverage, 0.0f, 1.0f, nameof(FogCoverage));
    Check(FogRed, 0.0f, 1.0f, nameof(FogRed));
    Check(FogGreen, 0.0f, 1.0f, nameof(FogGreen));
    Check(FogBlue, 0.0f, 1.0f, nameof(FogBlue));
    Check(DriftSpeed, -30.0f, 30.0f, nameof(DriftSpeed));
    Check(Parallax, 0.0f, 1.0f, nameof(Parallax));
    Check(CloudWarp, 0.0f, 0.03f, nameof(CloudWarp));
    Check(Twinkle, 0.0f, 0.75f, nameof(Twinkle));
    Check(TwinkleSpeed, 0.0f, 3.0f, nameof(TwinkleSpeed));
    Check(Threshold, 0.05f, 0.9f, nameof(Threshold));
    Check(Intensity, 0.0f, 3.0f, nameof(Intensity));
    Check(Blur, 0.5f, 10.0f, nameof(Blur));
    Check(BloomSaturation, 0.0f, 2.5f, nameof(BloomSaturation));
    Check(SoftKnee, 0.0f, 1.0f, nameof(SoftKnee));
    Check(Halo, 0.0f, 1.0f, nameof(Halo));
    Check(HaloRadius, 0.0f, 18.0f, nameof(HaloRadius));
    Check(Anamorphic, 1.0f, 4.0f, nameof(Anamorphic));
    Check(Exposure, -2.0f, 2.0f, nameof(Exposure));
    Check(Contrast, 0.5f, 1.5f, nameof(Contrast));
    Check(Saturation, 0.0f, 2.0f, nameof(Saturation));
    Check(Temperature, -0.5f, 0.5f, nameof(Temperature));
    Check(Tint, -0.3f, 0.3f, nameof(Tint));
    Check(ShadowLift, -0.06f, 0.1f, nameof(ShadowLift));
    Check(HighlightRolloff, 0.0f, 1.0f, nameof(HighlightRolloff));
    Check(VignetteStrength, 0.0f, 0.8f, nameof(VignetteStrength));
    Check(VignetteRadius, 0.0f, 0.9f, nameof(VignetteRadius));
    Check(VignetteSoftness, 0.05f, 1.0f, nameof(VignetteSoftness));
    Check(AberrationPixels, 0.0f, 4.0f, nameof(AberrationPixels));
    Check(GrainAmount, 0.0f, 0.1f, nameof(GrainAmount));
    Check(SharpenAmount, 0.0f, 1.0f, nameof(SharpenAmount));
    Check(LensAmount, -0.15f, 0.15f, nameof(LensAmount));
  }
  private static void Check(float value, float min, float max, string name)
  {
    if (!float.IsFinite(value) || value < min || value > max)
      throw new InvalidDataException($"{name} must be between {min} and {max}.");
  }
}

[JsonSourceGenerationOptions(IncludeFields = true, WriteIndented = true)]
[JsonSerializable(typeof(VisualEffectsSettings))]
internal partial class VisualEffectsJson : JsonSerializerContext { }

public static class VisualEffects
{
  public const string ManifestPath = "VisualEffects.json";
  public static VisualEffectsSettings Settings { get; private set; } = new();
  private static VisualEffectsSettings saved = new();
  public static string Status { get; private set; }
  public static float Time { get; private set; }
  // Bypass previews the original background + original bloom without losing slider values.
  public static bool Bypass;
  private static Effect extract, combine;
  public static bool Active => Settings.Enabled && !Bypass;
  public static void Recommended() => Settings = new();
  public static void Original() => Settings = new() { Enabled = false };
  public static void DeepSpace()
  {
    Settings = new() { BackgroundBrightness = .58f, FogDensity = .24f, FogRed = .18f,
      FogGreen = .13f, FogBlue = .38f, VignetteStrength = .25f, Intensity = .95f };
  }
  public static void Revert() { Settings = saved.Copy(); Status = "Saved defaults restored."; }
  public static string ToJson() => JsonSerializer.Serialize(Settings, VisualEffectsJson.Default.VisualEffectsSettings);
  public static VisualEffectsSettings Read(Stream stream)
  {
    var value = JsonSerializer.Deserialize(stream, VisualEffectsJson.Default.VisualEffectsSettings)
      ?? throw new InvalidDataException("Empty visual settings.");
    value.Validate();
    return value;
  }
  public static void LoadDefaults()
  {
    try
    {
#if !KNI_WEB
      string source = Demo.IsDev ? DebugSoundEffects.FindSourceContentDirectory() : null;
      using var stream = source != null ? File.OpenRead(Path.Combine(source, ManifestPath))
        : TitleContainer.OpenStream("Content/" + ManifestPath);
#else
      using var stream = TitleContainer.OpenStream("Content/" + ManifestPath);
#endif
      Settings = Read(stream);
      saved = Settings.Copy();
    }
    catch (Exception error)
    {
      Settings = new();
      saved = Settings.Copy();
      Status = "Using recommended defaults: " + error.Message;
      Serilog.Log.Warning(error, "Could not load visual defaults");
    }
  }
  public static void Save(string path)
  {
    Settings.Validate();
    string temporary = path + ".tmp";
    try
    {
      File.WriteAllText(temporary, ToJson() + Environment.NewLine);
      File.Move(temporary, path, overwrite: true);
      saved = Settings.Copy();
      Status = "Visual defaults saved.";
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }

  // Called before BaseGame.Draw. Its final bloom combine happens before HUD composition,
  // so these effects never blur, tint or distort menus, HUD text or debug sliders.
  public static void Prepare(GameTime gameTime, int width, int height)
  {
    var s = Settings;
    if (s.Animate) Time += (float)gameTime.ElapsedGameTime.TotalSeconds;
    extract ??= AssetManager.Load<Effect>("Shaders/BloomExtract.fx");
    combine ??= AssetManager.Load<Effect>("Shaders/BloomCombine.fx");
    bool on = Active;
    var bloom = BloomSettings.PresetSettings[0];
    bloom.BloomThreshold = on ? s.Threshold : .30f;
    bloom.BloomIntensity = on ? (s.Bloom ? s.Intensity : 0) : 1.30f;
    bloom.BlurAmount = on ? s.Blur : 4;
    bloom.BloomSaturation = on ? s.BloomSaturation : 1.15f;
    Set(extract, "SoftBloom", on && s.SoftBloom ? 1 : 0);
    Set(extract, "SoftKnee", s.SoftKnee);
    Set(combine, "VisualEnabled", on ? 1 : 0);
    combine.Parameters["PixelSize"]?.SetValue(new Vector2(1f / Math.Max(1, width), 1f / Math.Max(1, height)));
    Set(combine, "VisualTime", Time);
    Set(combine, "HaloMix", on && s.Bloom && s.SoftBloom ? s.Halo : 0);
    Set(combine, "HaloRadius", s.HaloRadius);
    Set(combine, "Anamorphic", s.Anamorphic);
    Set(combine, "Exposure", s.Grading ? s.Exposure : 0);
    Set(combine, "Contrast", s.Grading ? s.Contrast : 1);
    Set(combine, "Saturation", s.Grading ? s.Saturation : 1);
    Set(combine, "Temperature", s.Grading ? s.Temperature : 0);
    Set(combine, "Tint", s.Grading ? s.Tint : 0);
    Set(combine, "ShadowLift", s.Grading ? s.ShadowLift : 0);
    Set(combine, "HighlightRolloff", s.Grading ? s.HighlightRolloff : 0);
    Set(combine, "VignetteStrength", s.Vignette ? s.VignetteStrength : 0);
    Set(combine, "VignetteRadius", s.VignetteRadius);
    Set(combine, "VignetteSoftness", s.VignetteSoftness);
    Set(combine, "AberrationPixels", s.Aberration ? s.AberrationPixels : 0);
    Set(combine, "GrainAmount", s.Grain ? s.GrainAmount : 0);
    Set(combine, "SharpenAmount", s.Sharpen ? s.SharpenAmount : 0);
    Set(combine, "LensAmount", s.Lens ? s.LensAmount : 0);
  }

  public static void BackgroundLayer(Effect effect, int layer)
  {
    var s = Settings;
    bool on = Active;
    Set(effect, "VisualTime", Time);
    Set(effect, "BackgroundBrightness", on && s.Background ? s.BackgroundBrightness : 1);
    Set(effect, "BackgroundSaturation", on && s.Background ? s.BackgroundSaturation : 1);
    Set(effect, "FogDensity", on && s.Fog && layer == 0 ? s.FogDensity : 0);
    Set(effect, "FogScale", s.FogScale);
    Set(effect, "FogSpeed", s.FogSpeed);
    Set(effect, "FogCoverage", s.FogCoverage);
    effect.Parameters["FogColor"]?.SetValue(new Vector3(s.FogRed, s.FogGreen, s.FogBlue));
    Set(effect, "CloudWarp", on && s.Motion && layer == 1 ? s.CloudWarp : 0);
    Set(effect, "Twinkle", on && s.Motion && layer == 2 ? s.Twinkle : 0);
    Set(effect, "TwinkleSpeed", s.TwinkleSpeed);
  }
  private static void Set(Effect effect, string name, float value) => effect?.Parameters[name]?.SetValue(value);
}
