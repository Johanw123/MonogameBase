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
  Text, MutedText, DisabledText, Uncommon, Mythic
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
    new(226, 238, 244), new(162, 185, 198), new(132, 154, 166), new(164, 224, 148), new(242, 142, 184)
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
      || current.BaseBrightness != data.BaseBrightness) BaseRevision++;
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
    float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
    float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
    float delta = max - min;
    if (delta < 0.001f || max == 0) return color;
    float hue = max == r ? 60 * ((g - b) / delta % 6)
      : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
    if (hue < 0) hue += 360;
    if (hue < 165 || hue > 255) return color;
    hue = (hue + current.BaseHueShift + 360) % 360;
    float saturation = Math.Clamp(delta / max * current.BaseSaturation, 0, 1);
    float value = Math.Clamp(max * current.BaseBrightness, 0, 1);
    float c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
    (r, g, b) = hue switch
    {
      < 60 => (c, x, 0f), < 120 => (x, c, 0f), < 180 => (0f, c, x),
      < 240 => (0f, x, c), < 300 => (x, 0f, c), _ => (c, 0f, x)
    };
    return new Color((byte)MathF.Round((r + m) * 255), (byte)MathF.Round((g + m) * 255),
      (byte)MathF.Round((b + m) * 255), color.A);
  }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GuiPaletteData))]
internal partial class GuiPaletteJson : JsonSerializerContext;
