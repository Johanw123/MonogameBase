using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public enum GuiColor
{
  Accent, Ability, Upgrade, Prestige, Confirm, Restricted,
  Text, MutedText, DisabledText, Uncommon, Mythic, Progress, Selection, NodeBorder, MenuInteraction,
  NodeProgress, Hover
}

// Source-controlled defaults are independent of player progress and settings.
public sealed record GuiPaletteData
{
  public float BaseHueShift { get; set; }
  public float BaseSaturation { get; set; } = 1f;
  public float BaseBrightness { get; set; } = 1f;
  public Dictionary<string, string> Colors { get; set; } = new();
}

public static class GuiPalette
{
  public const string ManifestPath = "GuiPalette.json";
  private static readonly Color[] original =
  [
    new(82, 228, 242), new(118, 204, 248), new(255, 190, 82), new(198, 162, 246),
    new(104, 230, 152), new(240, 146, 134),
    new(226, 238, 244), new(162, 185, 198), new(132, 154, 166), new(164, 224, 148), new(242, 142, 184),
    new(82, 228, 242), new(82, 228, 242), new(0, 0, 0), new(82, 228, 242),
    new(82, 228, 242), new(82, 228, 242)
  ];
  private static GuiPaletteData current = Original();
  private static GuiPaletteData saved = Original();
  private static Color[] resolved = (Color[])original.Clone();
  public static int Revision { get; private set; }
  public static int BaseRevision { get; private set; }
  public static string LoadError { get; private set; }
  public static Color Get(GuiColor role) => resolved[(int)role];
  public static Color OriginalColor(GuiColor role) => original[(int)role];
  public static GuiPaletteData Snapshot() => Copy(current);
  public static GuiPaletteData SavedSnapshot() => Copy(saved);
  public static bool HasChanges => !Equal(current, saved);

  public static GuiPaletteData Original() => new()
  {
    Colors = Enum.GetValues<GuiColor>().ToDictionary(role => role.ToString(), role => Hex(original[(int)role]))
  };

  private static GuiPaletteData Copy(GuiPaletteData data) => data with { Colors = new(data.Colors) };
  private static bool Equal(GuiPaletteData a, GuiPaletteData b) => a.BaseHueShift == b.BaseHueShift
    && a.BaseSaturation == b.BaseSaturation && a.BaseBrightness == b.BaseBrightness
    && a.Colors.Count == b.Colors.Count && a.Colors.All(p => b.Colors.TryGetValue(p.Key, out var v) && v == p.Value);

  public static void Apply(GuiPaletteData data)
  {
    Validate(data);
    if (Equal(current, data)) return;
    if (current.BaseHueShift != data.BaseHueShift || current.BaseSaturation != data.BaseSaturation
      || current.BaseBrightness != data.BaseBrightness
      || current.Colors[nameof(GuiColor.Accent)] != data.Colors[nameof(GuiColor.Accent)]
      || current.Colors[nameof(GuiColor.Progress)] != data.Colors[nameof(GuiColor.Progress)]
      || current.Colors[nameof(GuiColor.NodeBorder)] != data.Colors[nameof(GuiColor.NodeBorder)]
      || current.Colors[nameof(GuiColor.MenuInteraction)] != data.Colors[nameof(GuiColor.MenuInteraction)]
      || current.Colors[nameof(GuiColor.Hover)] != data.Colors[nameof(GuiColor.Hover)]
      || current.Colors[nameof(GuiColor.Confirm)] != data.Colors[nameof(GuiColor.Confirm)])
      BaseRevision++;
    current = Copy(data);
    resolved = Enum.GetValues<GuiColor>().Select(role => Parse(current.Colors[role.ToString()])).ToArray();
    Revision++;
  }

  public static void Set(GuiColor role, Color color)
  {
    var data = Snapshot();
    data.Colors[role.ToString()] = Hex(color);
    Apply(data);
  }

  public static void Revert() => Apply(saved);
  public static void ResetOriginal() => Apply(Original());
  public static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
  private static Color Parse(string value)
  {
    if (value == null || value.Length != 7 || value[0] != '#'
      || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
      throw new InvalidDataException("GUI colors must use #RRGGBB hex values.");
    return new Color((int)(rgb >> 16), (int)(rgb >> 8 & 255), (int)(rgb & 255));
  }

  public static void Validate(GuiPaletteData data)
  {
    if (data == null || !float.IsFinite(data.BaseHueShift) || Math.Abs(data.BaseHueShift) > 180
      || !float.IsFinite(data.BaseSaturation) || data.BaseSaturation < 0 || data.BaseSaturation > 2
      || !float.IsFinite(data.BaseBrightness) || data.BaseBrightness < 0.1f || data.BaseBrightness > 2
      || data.Colors == null || data.Colors.Count != original.Length)
      throw new InvalidDataException("Invalid GUI palette.");
    foreach (var role in Enum.GetValues<GuiColor>())
    {
      if (!data.Colors.TryGetValue(role.ToString(), out var value))
        throw new InvalidDataException($"Missing GUI color: {role}");
      _ = Parse(value);
    }
  }

  public static GuiPaletteData Read(Stream stream)
  {
    var data = JsonSerializer.Deserialize(stream, GuiPaletteJson.Default.GuiPaletteData);
    Validate(data);
    return data;
  }

  public static void Save(string path)
  {
    string temporary = path + ".tmp";
    try
    {
      File.WriteAllText(temporary, JsonSerializer.Serialize(current, GuiPaletteJson.Default.GuiPaletteData) + Environment.NewLine);
      File.Move(temporary, path, overwrite: true);
      saved = Snapshot();
      LoadError = null;
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
      Apply(Read(stream));
      saved = Snapshot();
      LoadError = null;
    }
    catch (Exception error)
    {
      LoadError = "Could not load GUI defaults: " + error.Message;
      Serilog.Log.Warning(error, "Could not load GUI palette; using original colors");
      ResetOriginal();
      saved = Snapshot();
    }
  }

  // Hue-shift the blue family in the original artwork, preserving shading and alpha.
  // Accent/text colors use their own controls and are never passed through this transform.
  public static Color Base(Color color)
  {
    if (current.BaseHueShift == 0 && current.BaseSaturation == 1 && current.BaseBrightness == 1) return color;
    var (hue, sat, val, delta) = ToHsv(color);
    if (delta < 0.001f || val == 0 || hue < 165 || hue > 255) return color;
    return FromHsv(hue + current.BaseHueShift, sat * current.BaseSaturation, val * current.BaseBrightness, color.A);
  }

  // Recolors baked blue/cyan Synty textures: dark navy interiors follow Base(), while cyan
  // borders, bevels, brackets, knobs and fills also respond to GuiColor.Accent.
  public static Color Surface(Color color)
    => Surface(color, GuiColor.Accent);

  public static Color Surface(Color color, GuiColor role)
  {
    var baseColor = Base(color);
    var accent = Get(role);
    var origAccent = OriginalColor(GuiColor.Accent);
    if (accent == origAccent) return baseColor;
    var (h, s, v, delta) = ToHsv(color);
    if (delta < 0.001f || v == 0f || h < 165f || h > 255f) return baseColor;
    float weight = Math.Clamp((v - 0.32f) / 0.16f, 0f, 1f);
    if (weight <= 0f) return baseColor;
    var (oh, os, ov, _) = ToHsv(origAccent);
    var (th, ts, tv, _) = ToHsv(accent);
    float ah = ts < 0.02f ? th : h + (th - oh);
    float asat = s * (ts / Math.Max(0.05f, os));
    float aval = v * (tv / Math.Max(0.05f, ov));
    var accentColor = FromHsv(ah, asat, aval, color.A);
    return weight >= 1f ? accentColor : Color.Lerp(baseColor, accentColor, weight);
  }

  internal static GuiColor AssetSurfaceRole(string asset)
    => asset == "slider_foreground" ? GuiColor.Progress
      : asset == "menu_button_hover_blue" ? GuiColor.Hover
      : asset == "menu_left_backdrop_blue" ? GuiColor.MenuInteraction
      : GuiColor.Accent;

  internal static bool IsHoverAsset(string asset)
    => asset is "menu_button_hover_blue" or "button_active_blue" or "button_active_green" or "button_frame_active_blue";

  // Recolors baked green Synty confirm buttons: dark interiors follow Base(), while green
  // borders, bevels and brackets respond to GuiColor.Confirm.
  public static Color ConfirmSurface(Color color)
    => ConfirmSurface(color, GuiColor.Confirm);

  public static Color ConfirmSurface(Color color, GuiColor role)
  {
    var confirm = Get(role);
    var origConfirm = OriginalColor(GuiColor.Confirm);
    bool baseChanged = current.BaseHueShift != 0 || current.BaseSaturation != 1f || current.BaseBrightness != 1f;
    if (!baseChanged && confirm == origConfirm) return color;
    var (h, s, v, delta) = ToHsv(color);
    if (delta < 0.001f || v == 0f || h < 110f || h > 255f) return color;
    var baseColor = baseChanged
      ? FromHsv(h + current.BaseHueShift, s * current.BaseSaturation, v * current.BaseBrightness, color.A)
      : color;
    if (confirm == origConfirm) return baseColor;
    float weight = Math.Clamp((v - 0.24f) / 0.18f, 0f, 1f);
    if (weight <= 0f) return baseColor;
    var (oh, os, ov, _) = ToHsv(origConfirm);
    var (th, ts, tv, _) = ToHsv(confirm);
    float ch = ts < 0.02f ? th : h + (th - oh);
    float csat = s * (ts / Math.Max(0.05f, os));
    float cval = v * (tv / Math.Max(0.05f, ov));
    var confirmColor = FromHsv(ch, csat, cval, color.A);
    return weight >= 1f ? confirmColor : Color.Lerp(baseColor, confirmColor, weight);
  }

  private static (float H, float S, float V, float Delta) ToHsv(Color color)
  {
    float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
    float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
    float delta = max - min;
    if (delta < 0.001f || max == 0f) return (0f, 0f, max, delta);
    float hue = max == r ? 60f * ((g - b) / delta % 6f)
      : max == g ? 60f * ((b - r) / delta + 2f) : 60f * ((r - g) / delta + 4f);
    if (hue < 0f) hue += 360f;
    return (hue, delta / max, max, delta);
  }

  private static Color FromHsv(float hue, float saturation, float value, byte alpha)
  {
    hue = (hue % 360f + 360f) % 360f;
    saturation = Math.Clamp(saturation, 0f, 1f);
    value = Math.Clamp(value, 0f, 1f);
    float c = value * saturation, x = c * (1f - Math.Abs(hue / 60f % 2f - 1f)), m = value - c;
    var (r, g, b) = hue switch
    {
      < 60f => (c, x, 0f), < 120f => (x, c, 0f), < 180f => (0f, c, x),
      < 240f => (0f, x, c), < 300f => (x, 0f, c), _ => (c, 0f, x)
    };
    return new Color((byte)MathF.Round((r + m) * 255f), (byte)MathF.Round((g + m) * 255f),
      (byte)MathF.Round((b + m) * 255f), alpha);
  }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GuiPaletteData))]
internal partial class GuiPaletteJson : JsonSerializerContext;
