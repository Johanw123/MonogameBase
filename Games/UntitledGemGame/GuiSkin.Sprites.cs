using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

public enum GuiSpriteSlot
{
  PanelBackground,
  PanelFrameOverlay,
  PanelHeader,
  TopBarBackground,
  HudBarBackground,
  FlatPanelBackground,
  FlatPanelBorder,
  TechCardBackground,
  TechCardBorder,
  TechCardCornerArmor,
  HudButtonIdle,
  HudButtonActive,
  HudButtonConfirmIdle,
  HudButtonConfirmActive,
  HudButtonCornersIdle,
  HudButtonCornersActive,
  MenuButtonFrameIdle,
  MenuButtonFrameActive,
  TreeNodeBackground,
  TreeNodeBorder,
  AbilitySlotIdle,
  AbilitySlotActive,
  ScanlinesOverlay,
  HexagonsOverlay,
  SliderTrack,
  SliderFill,
  SliderKnob,
  ToggleBackgroundOff,
  ToggleBackgroundOn,
  ToggleKnobOff,
  ToggleKnobOn,
}

public enum GuiSpriteColorMode
{
  Auto,
  PaletteSurface,
  FillTint,
  AccentTint,
  ConfirmSurface,
  Untinted,
}

public readonly record struct GuiSpriteSlotInfo(
  GuiSpriteSlot Slot,
  string Label,
  string Category,
  string DefaultAsset,
  int DefaultFrameWidth,
  bool IsNineSlice,
  bool IsBorderSlot,
  bool IsFillSlot,
  bool IsConfirmSlot);

public static partial class GuiSkin
{
  internal sealed class SlotCue
  {
    public string AssetName;
    public string SourceName;
    public byte[] PngBytes;
    public int FrameWidth;
    public float CornerScale = 1f;
    public float Opacity = 1f;
    public GuiSpriteColorMode ColorMode = GuiSpriteColorMode.Auto;
    public bool Hidden;

    public SlotCue Clone() => new()
    {
      AssetName = AssetName,
      SourceName = SourceName,
      PngBytes = PngBytes,
      FrameWidth = FrameWidth,
      CornerScale = CornerScale,
      Opacity = Opacity,
      ColorMode = ColorMode,
      Hidden = Hidden,
    };
  }

  internal sealed class RawSprite
  {
    public int Width;
    public int Height;
    public Color[] Pixels;
    public bool IsWhiteMask;
  }

  private sealed class SlotTextureEntry
  {
    public Texture2D Texture;
    public int SkinRevision = -1;
    public int PaletteBaseRevision = -1;
    public int PaletteRevision = -1;
  }

  private sealed class DeviceSlotCache
  {
    public readonly Dictionary<GuiSpriteSlot, SlotTextureEntry> BatchTextures = new();
    public readonly Dictionary<GuiSpriteSlot, SlotTextureEntry> GumTextures = new();
    public Texture2D TransparentPixel;

    public DeviceSlotCache(GraphicsDevice device)
    {
      device.Disposing += (_, _) =>
      {
        foreach (var entry in BatchTextures.Values) entry.Texture?.Dispose();
        foreach (var entry in GumTextures.Values) entry.Texture?.Dispose();
        TransparentPixel?.Dispose();
        BatchTextures.Clear();
        GumTextures.Clear();
      };
    }
  }

  private static readonly GuiSpriteSlotInfo[] slotInfos =
  [
    new(GuiSpriteSlot.PanelBackground, "Window & Modal Panel Background", "Panels & Bars", "modal_info_complete", 24, true, false, false, false),
    new(GuiSpriteSlot.PanelFrameOverlay, "Window & Bar Corner Frame Overlay", "Panels & Bars", "panel_frame_overlay_blue", 48, true, true, false, false),
    new(GuiSpriteSlot.PanelHeader, "Modal & Panel Title Header", "Panels & Bars", "modal_title_complete", 24, true, false, false, false),
    new(GuiSpriteSlot.TopBarBackground, "Top HUD Bar Background", "Panels & Bars", "hud_bar_background", 24, true, false, false, false),
    new(GuiSpriteSlot.HudBarBackground, "Bottom HUD Bar Background", "Panels & Bars", "hud_bar_background", 24, true, false, false, false),

    new(GuiSpriteSlot.FlatPanelBackground, "Flat Panel & Tier Band Background", "Cards & Sub-Panels", "synty_hud_box_02", 54, true, false, true, false),
    new(GuiSpriteSlot.FlatPanelBorder, "Flat Panel & Button Outer Frame", "Cards & Sub-Panels", "synty_hud_frame_02", 54, true, true, false, false),
    new(GuiSpriteSlot.TechCardBackground, "Tech Card & Title Badge Background", "Cards & Sub-Panels", "synty_hud_box_04", 54, true, false, true, false),
    new(GuiSpriteSlot.TechCardBorder, "Tech Card & Badge Bezel Frame", "Cards & Sub-Panels", "synty_hud_frame_06", 54, true, true, false, false),
    new(GuiSpriteSlot.TechCardCornerArmor, "Tech Card & Badge Corner Armor", "Cards & Sub-Panels", "synty_hud_frame_08", 108, true, true, false, false),

    new(GuiSpriteSlot.HudButtonIdle, "Button Background (Idle)", "Buttons", "button_idle_blue", 12, true, false, false, false),
    new(GuiSpriteSlot.HudButtonActive, "Button Background (Hover / Active)", "Buttons", "button_active_blue", 12, true, false, false, false),
    new(GuiSpriteSlot.HudButtonConfirmIdle, "Confirm Button Background (Idle)", "Buttons", "button_idle_green", 12, true, false, false, true),
    new(GuiSpriteSlot.HudButtonConfirmActive, "Confirm Button Background (Active)", "Buttons", "button_active_green", 12, true, false, false, true),
    new(GuiSpriteSlot.HudButtonCornersIdle, "HUD Button & Card Corners (Idle)", "Buttons", "hud_button_corners_idle_blue", 28, true, true, false, false),
    new(GuiSpriteSlot.HudButtonCornersActive, "HUD Button Corners (Active)", "Buttons", "hud_button_corners_active_blue", 28, true, true, false, false),
    new(GuiSpriteSlot.MenuButtonFrameIdle, "Menu Button Corner Frame (Idle)", "Buttons", "button_frame_idle_blue", 28, true, true, false, false),
    new(GuiSpriteSlot.MenuButtonFrameActive, "Menu Button Corner Frame (Active)", "Buttons", "button_frame_active_blue", 28, true, true, false, false),

    new(GuiSpriteSlot.TreeNodeBackground, "Upgrade Tree Node & Row Background", "Trees & Nodes", "option_background", 12, true, false, true, false),
    new(GuiSpriteSlot.TreeNodeBorder, "Upgrade Tree Node Border", "Trees & Nodes", "$vector_rect", 16, true, true, false, false),
    new(GuiSpriteSlot.AbilitySlotIdle, "Home Base Ability Slot (Unchecked)", "Trees & Nodes", "radio_unchecked", 24, true, false, true, false),
    new(GuiSpriteSlot.AbilitySlotActive, "Home Base Ability Slot (Checked)", "Trees & Nodes", "radio_checked_background", 24, true, false, true, false),

    new(GuiSpriteSlot.ScanlinesOverlay, "Scanlines Tiled Pattern", "Overlays & Controls", "synty_hud_scanlines", 0, false, false, false, false),
    new(GuiSpriteSlot.HexagonsOverlay, "Hexagons Tiled Pattern", "Overlays & Controls", "synty_hud_hexagons", 0, false, false, false, false),
    new(GuiSpriteSlot.SliderTrack, "Progress & Slider Track Background", "Overlays & Controls", "progress_background", 6, true, false, false, false),
    new(GuiSpriteSlot.SliderFill, "Progress & Slider Fill Bar", "Overlays & Controls", "slider_foreground", 0, false, false, false, false),
    new(GuiSpriteSlot.SliderKnob, "Slider Thumb Knob", "Overlays & Controls", "slider_knob", 0, false, false, false, false),
    new(GuiSpriteSlot.ToggleBackgroundOff, "Checkbox / Toggle Background (Off)", "Overlays & Controls", "toggle_off_background", 0, false, false, false, false),
    new(GuiSpriteSlot.ToggleBackgroundOn, "Checkbox / Toggle Background (On)", "Overlays & Controls", "toggle_on_background", 0, false, false, false, false),
    new(GuiSpriteSlot.ToggleKnobOff, "Checkbox / Toggle Knob (Off)", "Overlays & Controls", "toggle_off_knob", 0, false, false, false, false),
    new(GuiSpriteSlot.ToggleKnobOn, "Checkbox / Toggle Knob (On)", "Overlays & Controls", "toggle_on_knob", 0, false, false, false, false),
  ];

  private static readonly Dictionary<GuiSpriteSlot, GuiSpriteSlotInfo> slotInfoMap =
    slotInfos.ToDictionary(i => i.Slot);

  private static readonly Dictionary<GuiSpriteSlot, SlotCue> activeSlots = new();
  private static readonly Dictionary<GuiSpriteSlot, SlotCue> savedSlots = new();
  private static readonly Dictionary<GuiSpriteSlot, RawSprite> customRawSprites = new();
  private static readonly Dictionary<string, RawSprite> assetRawSprites = new(StringComparer.Ordinal);
  private static readonly ConditionalWeakTable<GraphicsDevice, DeviceSlotCache> slotCaches = new();
  private static readonly ConditionalWeakTable<Texture2D, object> gumOverrideTextures = new();
  private static string[] builtInAssets;
  private static bool initialized;

  public static int Revision { get; private set; } = 1;
  public static IReadOnlyList<GuiSpriteSlotInfo> AllSlotInfos => slotInfos;

  public static GuiSpriteSlotInfo GetSlotInfo(GuiSpriteSlot slot) => slotInfoMap[slot];

  private static void EnsureInitialized()
  {
    if (initialized) return;
    initialized = true;
    foreach (var info in slotInfos)
    {
      var cue = CreateBuiltInCue(info);
      activeSlots[info.Slot] = cue;
      savedSlots[info.Slot] = cue.Clone();
    }
    GuiSpriteDefaults.ApplySavedDefaults();
  }

  private static SlotCue CreateBuiltInCue(GuiSpriteSlotInfo info) => new()
  {
    AssetName = info.DefaultAsset,
    SourceName = info.DefaultAsset == "$vector_rect" ? "vector_rect (built-in)" : info.DefaultAsset + ".png",
    PngBytes = null,
    FrameWidth = info.DefaultFrameWidth,
    CornerScale = 1f,
    Opacity = 1f,
    ColorMode = GuiSpriteColorMode.Auto,
    Hidden = false,
  };

  internal static SlotCue GetSlotCue(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot];
  }

  public static bool IsBuiltIn(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var info = slotInfoMap[slot];
    var cue = activeSlots[slot];
    return cue.PngBytes == null
      && string.Equals(cue.AssetName, info.DefaultAsset, StringComparison.Ordinal)
      && cue.FrameWidth == info.DefaultFrameWidth
      && MathF.Abs(cue.CornerScale - 1f) < 0.001f
      && MathF.Abs(cue.Opacity - 1f) < 0.001f
      && cue.ColorMode == GuiSpriteColorMode.Auto
      && !cue.Hidden;
  }

  public static bool HasAnyOverride
  {
    get
    {
      EnsureInitialized();
      foreach (var info in slotInfos)
        if (!IsBuiltIn(info.Slot)) return true;
      return false;
    }
  }

  public static bool IsSlotReplaced(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var info = slotInfoMap[slot];
    var cue = activeSlots[slot];
    return cue.PngBytes != null || !string.Equals(cue.AssetName, info.DefaultAsset, StringComparison.Ordinal);
  }

  public static bool IsSlotDirty(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return !CuesEqual(activeSlots[slot], savedSlots[slot]);
  }

  private static bool CuesEqual(SlotCue a, SlotCue b)
  {
    if (a == null || b == null) return a == b;
    bool sameBytes = ReferenceEquals(a.PngBytes, b.PngBytes)
      || (a.PngBytes != null && b.PngBytes != null && a.PngBytes.AsSpan().SequenceEqual(b.PngBytes));
    return sameBytes
      && string.Equals(a.AssetName, b.AssetName, StringComparison.Ordinal)
      && string.Equals(a.SourceName, b.SourceName, StringComparison.Ordinal)
      && a.FrameWidth == b.FrameWidth
      && MathF.Abs(a.CornerScale - b.CornerScale) < 0.001f
      && MathF.Abs(a.Opacity - b.Opacity) < 0.001f
      && a.ColorMode == b.ColorMode
      && a.Hidden == b.Hidden;
  }

  public static string GetSlotSourceName(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].SourceName ?? activeSlots[slot].AssetName;
  }

  public static string GetSlotAssetName(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].AssetName;
  }

  public static int GetSlotFrameWidth(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].FrameWidth;
  }

  public static float GetSlotCornerScale(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].CornerScale;
  }

  public static float GetSlotOpacity(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].Opacity;
  }

  public static GuiSpriteColorMode GetSlotColorMode(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].ColorMode;
  }

  public static bool GetSlotHidden(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    return activeSlots[slot].Hidden;
  }

  public static (int Width, int Height, bool IsWhiteMask) GetSlotDimensions(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var raw = GetRawSprite(slot);
    return raw != null ? (raw.Width, raw.Height, raw.IsWhiteMask) : (0, 0, false);
  }

  public static IReadOnlyList<string> GetBuiltInAssetNames()
  {
    if (builtInAssets != null) return builtInAssets;
    var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    void ScanFolder(string folder, string prefix)
    {
      if (!Directory.Exists(folder)) return;
      foreach (var file in Directory.EnumerateFiles(folder, "*.png"))
        names.Add(prefix + Path.GetFileNameWithoutExtension(file));
    }
    ScanFolder(Path.Combine(AppContext.BaseDirectory, "Content", "Menu"), "");
    ScanFolder(Path.Combine(AppContext.BaseDirectory, "Content", "Textures", "GUI"), "GUI/");
    string repoMenu = GuiSpriteDefaults.FindRepoContentFolder("Menu");
    if (repoMenu != null) ScanFolder(repoMenu, "");
    string repoGui = GuiSpriteDefaults.FindRepoContentFolder(Path.Combine("Textures", "GUI"));
    if (repoGui != null) ScanFolder(repoGui, "GUI/");
    builtInAssets = names.ToArray();
    return builtInAssets;
  }

  public static bool DebugReplace(GuiSpriteSlot slot, byte[] pngBytes, string sourceName, out string error)
  {
    EnsureInitialized();
    error = null;
    if (pngBytes == null || pngBytes.Length == 0)
    {
      error = "PNG data is empty.";
      return false;
    }
    if (!TryDecodeRawSprite(pngBytes, out var raw, out error))
      return false;

    var info = slotInfoMap[slot];
    var cue = activeSlots[slot];
    cue.PngBytes = pngBytes;
    cue.AssetName = null;
    cue.SourceName = string.IsNullOrWhiteSpace(sourceName) ? "custom.png" : sourceName;
    cue.Hidden = false;
    if (info.IsNineSlice)
      cue.FrameWidth = DetectNineSliceFrameWidth(raw);
    customRawSprites[slot] = raw;
    BumpRevision();
    return true;
  }

  public static bool DebugSetBuiltInAsset(GuiSpriteSlot slot, string assetName)
  {
    EnsureInitialized();
    if (string.IsNullOrWhiteSpace(assetName)) return false;
    var raw = LoadRawAsset(assetName);
    if (raw == null) return false;
    var info = slotInfoMap[slot];
    var cue = activeSlots[slot];
    cue.PngBytes = null;
    cue.AssetName = assetName;
    cue.SourceName = assetName == "$vector_rect" ? "vector_rect (built-in)" : assetName + ".png";
    cue.Hidden = false;
    customRawSprites.Remove(slot);
    if (info.IsNineSlice)
    {
      int defaultForAsset = string.Equals(assetName, info.DefaultAsset, StringComparison.Ordinal)
        ? info.DefaultFrameWidth
        : DefaultFrameWidthForAsset(assetName, raw);
      cue.FrameWidth = defaultForAsset;
    }
    BumpRevision();
    return true;
  }

  private static int DefaultFrameWidthForAsset(string asset, RawSprite raw)
  {
    int minDim = Math.Min(raw.Width, raw.Height);
    if (asset == "$vector_rect") return 16;
    if (asset.StartsWith("synty_hud_frame_08", StringComparison.Ordinal)) return Math.Min(108, minDim / 2);
    if (asset.StartsWith("synty_hud_", StringComparison.Ordinal)) return Math.Min(54, minDim / 2);
    if (asset is "panel_frame_overlay_blue") return Math.Min(48, minDim / 2);
    if (asset.Contains("corners", StringComparison.Ordinal) || asset.StartsWith("button_frame_", StringComparison.Ordinal))
      return Math.Min(28, minDim / 2);
    if (asset is "modal_info_complete" or "modal_title_complete" or "hud_bar_background" || asset.StartsWith("radio_", StringComparison.Ordinal))
      return Math.Min(24, minDim / 2);
    if (asset.StartsWith("button_", StringComparison.Ordinal) || asset == "option_background")
      return Math.Min(12, minDim / 2);
    return DetectNineSliceFrameWidth(raw);
  }

  private static int DetectNineSliceFrameWidth(RawSprite raw)
  {
    if (raw == null || raw.Width < 8 || raw.Height < 8) return 4;
    int minDim = Math.Min(raw.Width, raw.Height);
    Color center = raw.Pixels[(raw.Height / 2) * raw.Width + (raw.Width / 2)];
    int midY = raw.Height / 2;
    int maxScan = raw.Width / 2 - 1;
    int borderEnd = 0;
    for (int x = maxScan; x >= 1; x--)
    {
      Color c = raw.Pixels[midY * raw.Width + x];
      int diff = Math.Abs(c.R - center.R) + Math.Abs(c.G - center.G) + Math.Abs(c.B - center.B) + Math.Abs(c.A - center.A);
      if (diff > 20)
      {
        borderEnd = x + 2;
        break;
      }
    }
    if (borderEnd <= 2)
      return Math.Clamp(minDim / 4, 4, Math.Max(4, minDim / 2));
    return Math.Clamp(borderEnd, 4, Math.Max(4, minDim / 2));
  }

  public static void DebugSetParameters(GuiSpriteSlot slot, int? frameWidth = null, float? cornerScale = null,
    float? opacity = null, GuiSpriteColorMode? colorMode = null, bool? hidden = null)
  {
    EnsureInitialized();
    var cue = activeSlots[slot];
    if (frameWidth.HasValue)
    {
      var raw = GetRawSprite(slot);
      int maxFrame = raw != null ? Math.Max(1, Math.Min(raw.Width, raw.Height) / 2) : 256;
      cue.FrameWidth = Math.Clamp(frameWidth.Value, 1, maxFrame);
    }
    if (cornerScale.HasValue)
      cue.CornerScale = Math.Clamp(cornerScale.Value, 0.25f, 4f);
    if (opacity.HasValue)
      cue.Opacity = Math.Clamp(opacity.Value, 0f, 1f);
    if (colorMode.HasValue)
      cue.ColorMode = colorMode.Value;
    if (hidden.HasValue)
      cue.Hidden = hidden.Value;
    BumpRevision();
  }

  public static void DebugRevert(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var saved = savedSlots[slot].Clone();
    activeSlots[slot] = saved;
    if (saved.PngBytes != null && TryDecodeRawSprite(saved.PngBytes, out var raw, out _))
      customRawSprites[slot] = raw;
    else
      customRawSprites.Remove(slot);
    BumpRevision();
  }

  public static void DebugResetOriginal(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var info = slotInfoMap[slot];
    activeSlots[slot] = CreateBuiltInCue(info);
    customRawSprites.Remove(slot);
    BumpRevision();
  }

  public static void DebugResetAllOriginals()
  {
    EnsureInitialized();
    foreach (var info in slotInfos)
      activeSlots[info.Slot] = CreateBuiltInCue(info);
    customRawSprites.Clear();
    BumpRevision();
  }

  public static void DebugMarkSaved(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    savedSlots[slot] = activeSlots[slot].Clone();
  }

  internal static void ApplyLoadedSlot(GuiSpriteSlot slot, SlotCue cue)
  {
    if (cue.PngBytes != null && TryDecodeRawSprite(cue.PngBytes, out var raw, out _))
      customRawSprites[slot] = raw;
    else
      customRawSprites.Remove(slot);
    activeSlots[slot] = cue.Clone();
    savedSlots[slot] = cue.Clone();
    BumpRevision();
  }

  private static void BumpRevision()
  {
    Revision++;
  }

  internal static RawSprite GetRawSprite(GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var cue = activeSlots[slot];
    if (cue.PngBytes != null && customRawSprites.TryGetValue(slot, out var custom))
      return custom;
    return LoadRawAsset(cue.AssetName ?? slotInfoMap[slot].DefaultAsset);
  }

  internal static RawSprite LoadRawAsset(string assetName)
  {
    if (string.IsNullOrWhiteSpace(assetName)) return null;
    if (assetRawSprites.TryGetValue(assetName, out var cached)) return cached;

    if (assetName == "$vector_rect")
    {
      int size = 64;
      var pixels = new Color[size * size];
      for (int y = 0; y < size; y++)
      for (int x = 0; x < size; x++)
      {
        bool border = x < 3 || x >= size - 3 || y < 3 || y >= size - 3;
        pixels[y * size + x] = border ? Color.White : Color.Transparent;
      }
      var vecRaw = new RawSprite { Width = size, Height = size, Pixels = pixels, IsWhiteMask = true };
      assetRawSprites[assetName] = vecRaw;
      return vecRaw;
    }

    byte[] bytes = ReadBuiltInAssetBytes(assetName);
    if (bytes == null || !TryDecodeRawSprite(bytes, out var raw, out _))
      return null;
    assetRawSprites[assetName] = raw;
    return raw;
  }

  private static byte[] ReadBuiltInAssetBytes(string assetName)
  {
    string relPath = assetName.StartsWith("GUI/", StringComparison.Ordinal)
      ? "Content/Textures/" + assetName + ".png"
      : "Content/Menu/" + assetName + ".png";
    try
    {
      using var stream = TitleContainer.OpenStream(relPath);
      using var ms = new MemoryStream();
      stream.CopyTo(ms);
      return ms.ToArray();
    }
    catch
    {
      string diskPath = Path.Combine(AppContext.BaseDirectory, relPath.Replace('/', Path.DirectorySeparatorChar));
      if (File.Exists(diskPath)) return File.ReadAllBytes(diskPath);
      string repoSub = assetName.StartsWith("GUI/", StringComparison.Ordinal)
        ? Path.Combine("Textures", assetName.Replace('/', Path.DirectorySeparatorChar) + ".png")
        : Path.Combine("Menu", assetName + ".png");
      string repoFile = GuiSpriteDefaults.FindRepoContentFile(repoSub);
      if (repoFile != null && File.Exists(repoFile)) return File.ReadAllBytes(repoFile);
      return null;
    }
  }

  private static bool DetectWhiteMask(Color[] pixels)
  {
    int visible = 0;
    int neutralBright = 0;
    for (int i = 0; i < pixels.Length; i++)
    {
      var c = pixels[i];
      if (c.A < 24) continue;
      visible++;
      int max = Math.Max(c.R, Math.Max(c.G, c.B));
      int min = Math.Min(c.R, Math.Min(c.G, c.B));
      if (max - min <= 18 && max >= 175)
        neutralBright++;
    }
    return visible > 0 && (float)neutralBright / visible >= 0.78f;
  }

  private static bool IsPaletteBlueAsset(string name)
  {
    if (string.IsNullOrEmpty(name)) return false;
    return name is "hud_bar_background" or "modal_info_complete" or "modal_title_complete"
      or "option_background" or "sub_heading2" or "progress_background" or "slider_foreground"
      or "slider_background" or "slider_knob" or "toggle_off_background" or "toggle_on_background"
      or "toggle_off_knob" or "toggle_on_knob" or "radio_unchecked" or "radio_checked_background"
      or "tab_active" or "tab_inactive"
      || name.EndsWith("_blue", StringComparison.Ordinal);
  }

  private static bool IsPaletteConfirmAsset(string name) =>
    !string.IsNullOrEmpty(name) && name.EndsWith("_green", StringComparison.Ordinal);

  public static GuiSpriteColorMode ResolveEffectiveColorMode(GuiSpriteSlot slot)
  {
    var cue = GetSlotCue(slot);
    if (cue.ColorMode != GuiSpriteColorMode.Auto) return cue.ColorMode;
    var info = slotInfoMap[slot];
    var raw = GetRawSprite(slot);
    if (cue.PngBytes == null && cue.AssetName != null)
    {
      if (IsPaletteConfirmAsset(cue.AssetName)) return GuiSpriteColorMode.ConfirmSurface;
      if (IsPaletteBlueAsset(cue.AssetName)) return GuiSpriteColorMode.PaletteSurface;
    }
    if (raw != null && raw.IsWhiteMask)
      return info.IsBorderSlot ? GuiSpriteColorMode.AccentTint : GuiSpriteColorMode.FillTint;
    return info.IsConfirmSlot ? GuiSpriteColorMode.ConfirmSurface : GuiSpriteColorMode.PaletteSurface;
  }

  // Returns the SpriteBatch (premultiplied alpha) texture for a slot.
  public static Texture2D GetSlotTexture(GraphicsDevice device, GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var cue = activeSlots[slot];
    var info = slotInfoMap[slot];
    var mode = ResolveEffectiveColorMode(slot);

    // Fast path for built-in unmodified asset textures when using their standard color mode
    if (cue.PngBytes == null && cue.AssetName != null && cue.AssetName != "$vector_rect"
      && !cue.AssetName.StartsWith("GUI/", StringComparison.Ordinal)
      && cue.ColorMode == GuiSpriteColorMode.Auto)
    {
      return GetTexture(device, cue.AssetName);
    }

    var cache = slotCaches.GetValue(device, d => new DeviceSlotCache(d)).BatchTextures;
    if (!cache.TryGetValue(slot, out var entry))
    {
      entry = new SlotTextureEntry();
      cache[slot] = entry;
    }

    if (entry.Texture != null && !entry.Texture.IsDisposed
      && entry.SkinRevision == Revision
      && entry.PaletteBaseRevision == GuiPalette.BaseRevision
      && entry.PaletteRevision == GuiPalette.Revision)
    {
      return entry.Texture;
    }

    var raw = GetRawSprite(slot);
    if (raw == null) return Pixel(device);

    if (entry.Texture == null || entry.Texture.IsDisposed
      || entry.Texture.Width != raw.Width || entry.Texture.Height != raw.Height)
    {
      entry.Texture?.Dispose();
      entry.Texture = new Texture2D(device, raw.Width, raw.Height);
    }

    var pixels = new Color[raw.Pixels.Length];
    for (int i = 0; i < pixels.Length; i++)
    {
      var src = raw.Pixels[i];
      Color c = mode switch
      {
        GuiSpriteColorMode.PaletteSurface => GuiPalette.Surface(src),
        GuiSpriteColorMode.ConfirmSurface => GuiPalette.ConfirmSurface(src),
        _ => src,
      };
      pixels[i] = Color.FromNonPremultiplied(c.R, c.G, c.B, c.A);
    }
    entry.Texture.SetData(pixels);
    entry.SkinRevision = Revision;
    entry.PaletteBaseRevision = GuiPalette.BaseRevision;
    entry.PaletteRevision = GuiPalette.Revision;
    return entry.Texture;
  }

  // Returns the straight-alpha Texture2D for Gum controls (or preview thumbnails).
  internal static Texture2D GetGumSlotTexture(GraphicsDevice device, GuiSpriteSlot slot)
  {
    EnsureInitialized();
    var cue = activeSlots[slot];
    var info = slotInfoMap[slot];
    var cacheObj = slotCaches.GetValue(device, d => new DeviceSlotCache(d));

    if (cue.Hidden)
    {
      if (cacheObj.TransparentPixel == null || cacheObj.TransparentPixel.IsDisposed)
      {
        cacheObj.TransparentPixel = new Texture2D(device, 1, 1);
        cacheObj.TransparentPixel.SetData(new[] { Color.Transparent });
        gumOverrideTextures.AddOrUpdate(cacheObj.TransparentPixel, null);
      }
      return cacheObj.TransparentPixel;
    }

    var cache = cacheObj.GumTextures;
    if (!cache.TryGetValue(slot, out var entry))
    {
      entry = new SlotTextureEntry();
      cache[slot] = entry;
    }

    if (entry.Texture != null && !entry.Texture.IsDisposed
      && entry.SkinRevision == Revision
      && entry.PaletteBaseRevision == GuiPalette.BaseRevision
      && entry.PaletteRevision == GuiPalette.Revision)
    {
      return entry.Texture;
    }

    var raw = GetRawSprite(slot);
    if (raw == null) return Pixel(device);

    if (entry.Texture == null || entry.Texture.IsDisposed
      || entry.Texture.Width != raw.Width || entry.Texture.Height != raw.Height)
    {
      entry.Texture?.Dispose();
      entry.Texture = new Texture2D(device, raw.Width, raw.Height);
      gumOverrideTextures.AddOrUpdate(entry.Texture, null);
    }

    var mode = ResolveEffectiveColorMode(slot);
    // Upgrade tree nodes and HomeBase ability slots already multiply vertex color by SurfaceColor.
    bool vertexAlreadyTinted = slot is GuiSpriteSlot.TreeNodeBackground
      or GuiSpriteSlot.AbilitySlotIdle or GuiSpriteSlot.AbilitySlotActive;
    Color fillRef = info.IsConfirmSlot ? GuiPalette.Base(new Color(14, 48, 38)) : ButtonColor;
    Color accentRef = info.IsConfirmSlot ? ConfirmAccent : Accent;
    float opacity = Math.Clamp(cue.Opacity, 0f, 1f);

    var pixels = new Color[raw.Pixels.Length];
    for (int i = 0; i < pixels.Length; i++)
    {
      var src = raw.Pixels[i];
      Color c = mode switch
      {
        GuiSpriteColorMode.PaletteSurface => GuiPalette.Surface(src),
        GuiSpriteColorMode.ConfirmSurface => GuiPalette.ConfirmSurface(src),
        GuiSpriteColorMode.FillTint when !vertexAlreadyTinted => MultiplyRgb(src, fillRef),
        GuiSpriteColorMode.AccentTint => MultiplyRgb(src, accentRef),
        _ => src,
      };
      byte a = (byte)Math.Clamp((int)MathF.Round(c.A * opacity), 0, 255);
      pixels[i] = new Color(c.R, c.G, c.B, a);
    }
    entry.Texture.SetData(pixels);
    entry.SkinRevision = Revision;
    entry.PaletteBaseRevision = GuiPalette.BaseRevision;
    entry.PaletteRevision = GuiPalette.Revision;
    return entry.Texture;
  }

  internal static bool IsGumOverrideTexture(Texture2D texture) =>
    texture != null && gumOverrideTextures.TryGetValue(texture, out _);

  private static Color MultiplyRgb(Color a, Color b) => new(
    (byte)(a.R * b.R / 255),
    (byte)(a.G * b.G / 255),
    (byte)(a.B * b.B / 255),
    a.A);

  internal static Color ResolveBatchColor(GuiSpriteSlot slot, Color defaultColor, Color fillColor, Color accentColor)
  {
    var cue = GetSlotCue(slot);
    float opacity = Math.Clamp(cue.Opacity, 0f, 1f);
    if (!IsSlotReplaced(slot) && cue.ColorMode == GuiSpriteColorMode.Auto)
      return defaultColor * opacity;

    float baseAlpha = defaultColor.A / 255f;
    var mode = ResolveEffectiveColorMode(slot);
    return mode switch
    {
      GuiSpriteColorMode.FillTint => fillColor * opacity,
      GuiSpriteColorMode.AccentTint => accentColor * (baseAlpha * opacity),
      _ => Color.White * (baseAlpha * opacity),
    };
  }

  internal static void NineSliceSlot(SpriteBatch batch, GuiSpriteSlot slot, Rectangle bounds,
    int defaultSrcFrame, int defaultDstFrame, Color defaultColor, Color fillColor, Color accentColor)
  {
    if (IsBuiltIn(slot))
    {
      var info = slotInfoMap[slot];
      NineSliceScaled(batch, info.DefaultAsset, bounds, defaultSrcFrame, defaultDstFrame, defaultColor);
      return;
    }
    var cue = activeSlots[slot];
    if (cue.Hidden || cue.Opacity <= 0.001f || bounds.Width <= 0 || bounds.Height <= 0) return;

    var texture = GetSlotTexture(batch.GraphicsDevice, slot);
    int srcFrame = cue.FrameWidth > 0 ? cue.FrameWidth : defaultSrcFrame;
    int dstFrame = Math.Max(1, (int)MathF.Round(defaultDstFrame * cue.CornerScale));
    Color drawColor = ResolveBatchColor(slot, defaultColor, fillColor, accentColor);
    NineSliceTextureAsymmetric(batch, texture, bounds,
      srcFrame, srcFrame, srcFrame, srcFrame,
      dstFrame, dstFrame, dstFrame, dstFrame, drawColor);
  }

  internal static void DrawTiledSlot(SpriteBatch batch, GuiSpriteSlot slot, Rectangle bounds,
    int tileW, int tileH, int scrollX, int scrollY, Color defaultColor, Color accentColor)
  {
    if (IsBuiltIn(slot))
    {
      var info = slotInfoMap[slot];
      DrawTiled(batch, info.DefaultAsset, bounds, tileW, tileH, scrollX, scrollY, defaultColor);
      return;
    }
    var cue = activeSlots[slot];
    if (cue.Hidden || cue.Opacity <= 0.001f || bounds.Width <= 0 || bounds.Height <= 0) return;

    var tex = GetSlotTexture(batch.GraphicsDevice, slot);
    int scaledW = Math.Max(8, (int)MathF.Round(tileW * cue.CornerScale));
    int scaledH = Math.Max(8, (int)MathF.Round(tileH * cue.CornerScale));
    Color drawColor = defaultColor * cue.Opacity;
    if (cue.ColorMode == GuiSpriteColorMode.AccentTint)
      drawColor = accentColor * ((defaultColor.A / 255f) * cue.Opacity);
    else if (cue.ColorMode is GuiSpriteColorMode.Untinted or GuiSpriteColorMode.PaletteSurface)
      drawColor = Color.White * ((defaultColor.A / 255f) * cue.Opacity);
    DrawTiledTexture(batch, tex, bounds, scaledW, scaledH, scrollX, scrollY, drawColor);
  }

  public static void DrawTreeNodeBorder(SpriteBatch batch, Rectangle bounds, Color borderColor)
  {
    EnsureInitialized();
    var cue = activeSlots[GuiSpriteSlot.TreeNodeBorder];
    if (cue.Hidden || cue.Opacity <= 0.001f || bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    int defaultCorner = Math.Clamp(minDim / 4, 8, 14);
    int dstCorner = Math.Max(1, (int)MathF.Round(defaultCorner * cue.CornerScale));
    var tex = GetSlotTexture(batch.GraphicsDevice, GuiSpriteSlot.TreeNodeBorder);
    int srcFrame = cue.FrameWidth > 0 ? cue.FrameWidth : Math.Max(1, Math.Min(tex.Width, tex.Height) / 3);
    Color drawColor = ResolveBatchColor(GuiSpriteSlot.TreeNodeBorder, borderColor, PanelBackgroundTint, borderColor);
    NineSliceTextureAsymmetric(batch, tex, bounds,
      srcFrame, srcFrame, srcFrame, srcFrame,
      dstCorner, dstCorner, dstCorner, dstCorner, drawColor);
  }

  internal static string IdentifyPixels(int width, int height, Color[] pixels)
  {
    if (pixels == null || pixels.Length == 0) return null;
    string[] candidates =
    [
      "modal_info_complete", "modal_title_complete", "panel_frame_overlay_blue",
      "hud_bar_background", "button_idle_blue", "button_active_blue",
      "button_idle_green", "button_active_green",
      "button_frame_idle_blue", "button_frame_active_blue",
      "hud_button_corners_idle_blue", "hud_button_corners_active_blue",
      "option_background", "radio_unchecked", "radio_checked_background",
      "progress_background", "slider_background", "slider_foreground", "slider_knob",
      "toggle_off_background", "toggle_on_background", "toggle_off_knob", "toggle_on_knob"
    ];
    int[] sampleIndices =
    [
      0,
      pixels.Length / 4,
      pixels.Length / 2,
      pixels.Length * 3 / 4,
      pixels.Length - 1,
      (height / 2) * width + (width / 2),
      (height / 4) * width + (width / 4)
    ];
    foreach (var name in candidates)
    {
      var raw = LoadRawAsset(name);
      if (raw == null || raw.Width != width || raw.Height != height || raw.Pixels.Length != pixels.Length)
        continue;
      bool match = true;
      foreach (int idx in sampleIndices)
      {
        int i = Math.Clamp(idx, 0, pixels.Length - 1);
        if (raw.Pixels[i] != pixels[i])
        {
          match = false;
          break;
        }
      }
      if (match) return name;
    }
    return null;
  }

  internal static bool TryDecodeRawSprite(byte[] pngBytes, out RawSprite sprite, out string error)
  {
    sprite = null;
    error = null;
    if (TryDecodePng(pngBytes, out int w, out int h, out var pixels, out error))
    {
      sprite = new RawSprite
      {
        Width = w,
        Height = h,
        Pixels = pixels,
        IsWhiteMask = DetectWhiteMask(pixels),
      };
      return true;
    }

    var device = GameMain.Instance?.GraphicsDevice;
    if (device != null)
    {
      try
      {
        using var ms = new MemoryStream(pngBytes, writable: false);
        using var tex = Texture2D.FromStream(device, ms);
        var texPixels = new Color[tex.Width * tex.Height];
        tex.GetData(texPixels);
        sprite = new RawSprite
        {
          Width = tex.Width,
          Height = tex.Height,
          Pixels = texPixels,
          IsWhiteMask = DetectWhiteMask(texPixels),
        };
        error = null;
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
      }
    }
    return false;
  }

  // Pure-C# 8-bit PNG decoder so headless tests and background imports work without an active GraphicsDevice.
  private static bool TryDecodePng(byte[] data, out int width, out int height, out Color[] pixels, out string error)
  {
    width = 0;
    height = 0;
    pixels = null;
    error = null;
    if (data == null || data.Length < 33)
    {
      error = "File is too short to be a valid PNG.";
      return false;
    }
    ReadOnlySpan<byte> sig = [137, 80, 78, 71, 13, 10, 26, 10];
    if (!data.AsSpan(0, 8).SequenceEqual(sig))
    {
      error = "Invalid PNG header signature.";
      return false;
    }

    int bitDepth = 0, colorType = 0, interlace = 0;
    byte[] palette = null;
    byte[] trns = null;
    using var idat = new MemoryStream();
    int pos = 8;
    while (pos + 8 <= data.Length)
    {
      int len = ReadInt32BE(data, pos);
      if (len < 0 || pos + 12 + len > data.Length) break;
      string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
      int chunkPos = pos + 8;
      if (type == "IHDR" && len >= 13)
      {
        width = ReadInt32BE(data, chunkPos);
        height = ReadInt32BE(data, chunkPos + 4);
        bitDepth = data[chunkPos + 8];
        colorType = data[chunkPos + 9];
        interlace = data[chunkPos + 12];
      }
      else if (type == "PLTE")
      {
        palette = data.AsSpan(chunkPos, len).ToArray();
      }
      else if (type == "tRNS")
      {
        trns = data.AsSpan(chunkPos, len).ToArray();
      }
      else if (type == "IDAT")
      {
        idat.Write(data, chunkPos, len);
      }
      else if (type == "IEND")
      {
        break;
      }
      pos += 12 + len;
    }

    if (width <= 0 || height <= 0 || bitDepth != 8 || interlace != 0)
    {
      error = "Unsupported PNG format (requires 8-bit non-interlaced PNG).";
      return false;
    }

    int channels = colorType switch
    {
      0 => 1,
      2 => 3,
      3 => 1,
      4 => 2,
      6 => 4,
      _ => 0,
    };
    if (channels == 0)
    {
      error = $"Unsupported PNG color type {colorType}.";
      return false;
    }

    int stride = width * channels;
    byte[] rawScanlines = new byte[(stride + 1) * height];
    idat.Position = 0;
    try
    {
      using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
      int readTotal = 0;
      while (readTotal < rawScanlines.Length)
      {
        int n = zlib.Read(rawScanlines, readTotal, rawScanlines.Length - readTotal);
        if (n <= 0) break;
        readTotal += n;
      }
      if (readTotal < rawScanlines.Length)
      {
        error = "Truncated PNG image data.";
        return false;
      }
    }
    catch (Exception ex)
    {
      error = ex.Message;
      return false;
    }

    byte[] recon = new byte[stride * height];
    for (int y = 0; y < height; y++)
    {
      int srcRow = y * (stride + 1);
      int dstRow = y * stride;
      int filter = rawScanlines[srcRow];
      for (int x = 0; x < stride; x++)
      {
        byte rawByte = rawScanlines[srcRow + 1 + x];
        int a = x >= channels ? recon[dstRow + x - channels] : 0;
        int b = y > 0 ? recon[dstRow - stride + x] : 0;
        int c = (x >= channels && y > 0) ? recon[dstRow - stride + x - channels] : 0;
        int val = filter switch
        {
          0 => rawByte,
          1 => rawByte + a,
          2 => rawByte + b,
          3 => rawByte + ((a + b) >> 1),
          4 => rawByte + PaethPredictor(a, b, c),
          _ => rawByte,
        };
        recon[dstRow + x] = (byte)(val & 0xFF);
      }
    }

    pixels = new Color[width * height];
    for (int y = 0; y < height; y++)
    {
      int rowOffset = y * stride;
      for (int x = 0; x < width; x++)
      {
        int p = rowOffset + x * channels;
        Color color;
        switch (colorType)
        {
          case 6:
            color = new Color(recon[p], recon[p + 1], recon[p + 2], recon[p + 3]);
            break;
          case 2:
            color = new Color(recon[p], recon[p + 1], recon[p + 2], (byte)255);
            break;
          case 4:
            color = new Color(recon[p], recon[p], recon[p], recon[p + 1]);
            break;
          case 0:
            color = new Color(recon[p], recon[p], recon[p], (byte)255);
            break;
          case 3:
            int idx = recon[p];
            byte r = palette != null && idx * 3 + 2 < palette.Length ? palette[idx * 3] : (byte)0;
            byte g = palette != null && idx * 3 + 2 < palette.Length ? palette[idx * 3 + 1] : (byte)0;
            byte b = palette != null && idx * 3 + 2 < palette.Length ? palette[idx * 3 + 2] : (byte)0;
            byte al = trns != null && idx < trns.Length ? trns[idx] : (byte)255;
            color = new Color(r, g, b, al);
            break;
          default:
            color = Color.Transparent;
            break;
        }
        pixels[y * width + x] = color;
      }
    }
    return true;
  }

  private static int ReadInt32BE(byte[] buf, int offset) =>
    (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

  private static int PaethPredictor(int a, int b, int c)
  {
    int p = a + b - c;
    int pa = Math.Abs(p - a);
    int pb = Math.Abs(p - b);
    int pc = Math.Abs(p - c);
    if (pa <= pb && pa <= pc) return a;
    return pb <= pc ? b : c;
  }
}

internal sealed class GuiSpriteDefaultEntry
{
  public string Asset { get; set; }
  public string File { get; set; }
  public int FrameWidth { get; set; }
  public float CornerScale { get; set; } = 1f;
  public float Opacity { get; set; } = 1f;
  public string ColorMode { get; set; } = "Auto";
  public bool Hidden { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, GuiSpriteDefaultEntry>))]
internal partial class GuiSpriteDefaultsJsonContext : JsonSerializerContext
{
}

internal static class GuiSpriteDefaults
{
  private const string FileName = "GuiSkinDefaults.json";
  private const string DropFolder = "GuiSprites";

  public static void ApplySavedDefaults()
  {
    foreach (var (name, entry) in Load())
    {
      if (!Enum.TryParse<GuiSpriteSlot>(name, out var slot) || entry == null) continue;
      var info = GuiSkin.GetSlotInfo(slot);
      byte[] pngBytes = null;
      string sourceName = null;
      string assetName = entry.Asset;
      if (!string.IsNullOrWhiteSpace(entry.File))
      {
        string path = ResolveDropFilePath(entry.File);
        if (path != null && File.Exists(path))
        {
          pngBytes = File.ReadAllBytes(path);
          sourceName = entry.File;
          assetName = null;
        }
      }
      if (pngBytes == null && string.IsNullOrWhiteSpace(assetName))
        assetName = info.DefaultAsset;
      if (sourceName == null)
        sourceName = assetName == "$vector_rect" ? "vector_rect (built-in)" : assetName + ".png";

      if (!Enum.TryParse<GuiSpriteColorMode>(entry.ColorMode, ignoreCase: true, out var mode))
        mode = GuiSpriteColorMode.Auto;

      var cue = new GuiSkin.SlotCue
      {
        AssetName = assetName,
        SourceName = sourceName,
        PngBytes = pngBytes,
        FrameWidth = entry.FrameWidth > 0 ? entry.FrameWidth : info.DefaultFrameWidth,
        CornerScale = entry.CornerScale > 0f ? entry.CornerScale : 1f,
        Opacity = Math.Clamp(entry.Opacity, 0f, 1f),
        ColorMode = mode,
        Hidden = entry.Hidden,
      };
      GuiSkin.ApplyLoadedSlot(slot, cue);
    }
  }

  public static string Save(GuiSpriteSlot slot)
  {
    var cue = GuiSkin.GetSlotCue(slot);
    var dict = Load();
    string key = slot.ToString();

    if (GuiSkin.IsBuiltIn(slot))
    {
      dict.Remove(key);
      Write(dict);
      GuiSkin.DebugMarkSaved(slot);
      return $"{key}: restored built-in default in {FileName}";
    }

    string fileName = null;
    if (cue.PngBytes != null)
    {
      fileName = $"{key}.png";
      foreach (var dir in DropDirectories())
      {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), cue.PngBytes);
      }
    }

    dict[key] = new GuiSpriteDefaultEntry
    {
      Asset = cue.PngBytes == null ? cue.AssetName : null,
      File = fileName,
      FrameWidth = cue.FrameWidth,
      CornerScale = MathF.Round(cue.CornerScale, 3),
      Opacity = MathF.Round(cue.Opacity, 3),
      ColorMode = cue.ColorMode.ToString(),
      Hidden = cue.Hidden,
    };
    string written = Write(dict);
    GuiSkin.DebugMarkSaved(slot);
    return $"{key}: saved to {written}";
  }

  private static Dictionary<string, GuiSpriteDefaultEntry> Load()
  {
    foreach (var path in CandidatePaths())
    {
      if (!File.Exists(path)) continue;
      try
      {
        return JsonSerializer.Deserialize(File.ReadAllText(path),
          GuiSpriteDefaultsJsonContext.Default.DictionaryStringGuiSpriteDefaultEntry) ?? new();
      }
      catch (Exception e)
      {
        Debug.WriteLine($"Failed to read {path}: {e.Message}");
      }
    }
    return new();
  }

  private static string Write(Dictionary<string, GuiSpriteDefaultEntry> defaults)
  {
    string json = JsonSerializer.Serialize(defaults,
      GuiSpriteDefaultsJsonContext.Default.DictionaryStringGuiSpriteDefaultEntry) + Environment.NewLine;
    string written = null;
    foreach (var path in CandidatePaths())
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, json);
      written = path;
    }
    return written;
  }

  private static string ResolveDropFilePath(string file)
  {
    foreach (var dir in DropDirectories())
    {
      string candidate = Path.Combine(dir, file);
      if (File.Exists(candidate)) return candidate;
    }
    return null;
  }

  private static IEnumerable<string> DropDirectories()
  {
    string runtimePath = Path.Combine(AppContext.BaseDirectory, "Content", DropFolder);
    string repoContent = FindRepoContentFolder("");
    if (repoContent != null)
    {
      string repoPath = Path.Combine(repoContent, DropFolder);
      if (!PathsEqual(repoPath, runtimePath)) yield return repoPath;
    }
    yield return runtimePath;
  }

  private static IEnumerable<string> CandidatePaths()
  {
    string runtimePath = Path.Combine(AppContext.BaseDirectory, "Content", FileName);
    string repoPath = FindRepoDefaultsPath();
    if (repoPath != null && !PathsEqual(repoPath, runtimePath))
      yield return repoPath;
    yield return runtimePath;
  }

  private static string FindRepoDefaultsPath()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
      string project = Path.Combine(dir.FullName, "UntitledGemGame.csproj");
      string content = Path.Combine(dir.FullName, "Content", FileName);
      if (File.Exists(project) && Directory.Exists(Path.GetDirectoryName(content)))
        return content;
      dir = dir.Parent;
    }
    return null;
  }

  internal static string FindRepoContentFolder(string relativeSubfolder)
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
      string project = Path.Combine(dir.FullName, "UntitledGemGame.csproj");
      string content = string.IsNullOrEmpty(relativeSubfolder)
        ? Path.Combine(dir.FullName, "Content")
        : Path.Combine(dir.FullName, "Content", relativeSubfolder);
      if (File.Exists(project) && Directory.Exists(content))
        return content;
      dir = dir.Parent;
    }
    return null;
  }

  internal static string FindRepoContentFile(string relativeSubfile)
  {
    var folder = FindRepoContentFolder("");
    if (folder == null) return null;
    string full = Path.Combine(folder, relativeSubfile);
    return File.Exists(full) ? full : null;
  }

  private static bool PathsEqual(string a, string b) =>
    string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}

internal static class GuiSpriteImport
{
  public static Task<byte[]> ReadPngAsync(string path) => Task.Run(() => ReadPng(path));

  public static byte[] ReadPng(string path)
  {
    path = NormalizePath(path);
    if (!File.Exists(path))
      throw new FileNotFoundException($"File not found: {path}");

    if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
      return File.ReadAllBytes(path);

    string tempPng = Path.Combine(Path.GetTempPath(), $"gem-gui-sprite-{Guid.NewGuid():N}.png");
    try
    {
      var psi = new ProcessStartInfo("ffmpeg")
      {
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
      };
      psi.ArgumentList.Add("-y");
      psi.ArgumentList.Add("-v");
      psi.ArgumentList.Add("error");
      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(path);
      psi.ArgumentList.Add("-frames:v");
      psi.ArgumentList.Add("1");
      psi.ArgumentList.Add("-pix_fmt");
      psi.ArgumentList.Add("rgba");
      psi.ArgumentList.Add(tempPng);

      using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffmpeg.");
      string stderr = proc.StandardError.ReadToEnd();
      proc.WaitForExit();
      if (proc.ExitCode != 0)
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
          ? $"ffmpeg exited with {proc.ExitCode}" : stderr.Trim());
      return File.ReadAllBytes(tempPng);
    }
    finally
    {
      if (File.Exists(tempPng)) File.Delete(tempPng);
    }
  }

  public static string NormalizePath(string path)
  {
    path = path?.Trim().Trim('"') ?? "";
    if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
      return uri.LocalPath;
    return path;
  }
}
