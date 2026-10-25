using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Gum;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Gum.GueDeriving;
using Gum.Wireframe;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Gum owns interaction states. Update their color channels without rebuilding controls
// or disturbing focus, selection, slider values and popup state.
internal static class GuiThemeRuntime
{
  private sealed record StateColor(VariableSave Red, VariableSave Green, VariableSave Blue, Color Original, bool Menu);
  private sealed class TextureColors
  {
    public Color[] Original;
    public string AssetName;
    public int Revision = -1;
    public readonly Dictionary<GuiColor, (Texture2D Texture, int Revision)> Variants = new();
  }
  private sealed class ElementSkinState
  {
    public Texture2D OriginalTexture;
    public float? OriginalFrame;
    public bool HasOverride;
  }
  private static readonly List<StateColor> stateColors = new();
  private static readonly List<GraphicalUiElement> roots = new();
  private static readonly HashSet<GraphicalUiElement> menuRoots = new();
  private static readonly ConditionalWeakTable<Texture2D, TextureColors> textures = new();
  private static readonly ConditionalWeakTable<Texture2D, Texture2D> surfaceTextureSources = new();
  private static readonly ConditionalWeakTable<GraphicalUiElement, ElementSkinState> elementStates = new();
  private static readonly HashSet<GraphicalUiElement> visited = new();
  private static readonly Color[] baseColors =
  [
    new(9, 17, 26), new(64, 150, 178), new(14, 28, 40), new(26, 56, 76),
    new(76, 204, 228), new(16, 31, 44), new(10, 20, 30), new(31, 65, 69)
  ];
  private static Color[] previous;
  private static int revision = -1;
  private static int skinRevision = -1;

  public static void CaptureStates(GumProjectSave project)
  {
    stateColors.Clear();
    roots.Clear();
    menuRoots.Clear();
    foreach (var element in project.Components.Cast<ElementSave>().Concat(project.Screens))
      foreach (var state in element.AllStates)
        foreach (var red in state.Variables.Where(v => v.Name.EndsWith(".Red") && v.Value is int))
        {
          string prefix = red.Name[..^3];
          var green = state.Variables.FirstOrDefault(v => v.Name == prefix + "Green");
          var blue = state.Variables.FirstOrDefault(v => v.Name == prefix + "Blue");
          if (green?.Value is int g && blue?.Value is int b)
            stateColors.Add(new(red, green, blue, new Color((int)red.Value, g, b),
              element.Name is "MainMenu" or "GameMenu" or "SettingsMenu" or "CreditsMenu"
                || element.Name.StartsWith("Controls/", System.StringComparison.Ordinal)));
        }
    previous = ResolvedColors();
    revision = -1;
    skinRevision = -1;
  }

  public static void Track(GraphicalUiElement root)
  {
    roots.Add(root);
    revision = -1;
    skinRevision = -1;
  }

  public static void TrackMenu(GraphicalUiElement root)
  {
    menuRoots.Add(root);
    Track(root);
  }

  private static Color Resolve(Color original, bool menu)
  {
    if (menu && original == GuiPalette.OriginalColor(GuiColor.Accent)) return GuiSkin.MenuInteraction;
    foreach (var role in System.Enum.GetValues<GuiColor>())
      if (GuiPalette.OriginalColor(role) == original) return GuiPalette.Get(role);
    return GuiPalette.Surface(original, menu ? GuiColor.MenuInteraction : GuiColor.Accent);
  }

  private static Color[] ResolvedColors() => System.Enum.GetValues<GuiColor>().Select(GuiPalette.Get)
    .Concat(baseColors.Select(GuiPalette.Surface))
    .Concat(baseColors.Select(color => GuiPalette.Surface(color, GuiColor.MenuInteraction))).ToArray();

  public static void Refresh()
  {
    bool changed = revision != GuiPalette.Revision;
    bool skinChanged = skinRevision != GuiSkin.Revision || GuiSkin.HasAnyOverride;
    if (!changed && GuiPalette.BaseRevision == 0 && !skinChanged) return;
    Color[] next = changed ? ResolvedColors() : previous;
    if (changed)
      foreach (var binding in stateColors)
      {
        var color = Resolve(binding.Original, binding.Menu);
        binding.Red.Value = (int)color.R;
        binding.Green.Value = (int)color.G;
        binding.Blue.Value = (int)color.B;
      }
    visited.Clear();
    foreach (var root in roots) Visit(root, changed, next, menuRoots.Contains(root));
    Visit(GumService.Default.Root, changed, next);
    Visit(GumService.Default.ModalRoot, changed, next);
    Visit(GumService.Default.PopupRoot, changed, next);
    if (RenderGuiSystem.Instance is { } gui)
    {
      foreach (var root in gui.skillTreeItems) Visit(root, changed, next);
      foreach (var root in gui.hudItems) Visit(root, changed, next);
      foreach (var root in gui.gameMenuItems) Visit(root, changed, next);
    }
    if (changed)
    {
      UpgradeManager.Instance?.RefreshPalette();
      previous = next;
      revision = GuiPalette.Revision;
    }
    skinRevision = GuiSkin.Revision;
    visited.Clear();
  }

  private static Color Remap(Color color, Color[] next, bool menu)
  {
    if (previous == null) return color;
    int menuRole = (int)GuiColor.MenuInteraction;
    if (menu && color.R == previous[menuRole].R && color.G == previous[menuRole].G && color.B == previous[menuRole].B)
      return new Color(next[menuRole].R, next[menuRole].G, next[menuRole].B, color.A);
    if (menu)
      for (int i = next.Length - baseColors.Length; i < next.Length; i++)
        if (color.R == previous[i].R && color.G == previous[i].G && color.B == previous[i].B)
          return new Color(next[i].R, next[i].G, next[i].B, color.A);
    for (int i = 0; i < previous.Length; i++)
      if (color.R == previous[i].R && color.G == previous[i].G && color.B == previous[i].B)
        return new Color(next[i].R, next[i].G, next[i].B, color.A);
    return color;
  }

  private static void Visit(GraphicalUiElement element, bool changed, Color[] next, bool menu = false)
  {
    if (element == null || !visited.Add(element)) return;
    if (element is NineSliceRuntime panel) ApplyNineSliceSkin(panel, menu);
    if (element is SpriteRuntime sprite && (element.Name is "SliderFill" or "ToggleKnob" or "CheckboxBackground" or "BackgroundSprite" ||
      (element.Name == "Background" && element.Parent?.Name == "ThumbInstance")))
      ApplySpriteSkin(sprite, menu);
    if (changed)
    {
      if (element is SpriteRuntime decoration && (element.Name?.StartsWith("GuiDecoration", System.StringComparison.Ordinal) == true || element.Name == "ProgressIcon"))
        decoration.Color = Remap(decoration.Color, next, menu);
      if (element is TextRuntime text) text.Color = Remap(text.Color, next, menu);
      else if (element is ColoredRectangleRuntime rectangle) rectangle.Color = Remap(rectangle.Color, next, menu);
      else if (element is RectangleRuntime rect) rect.Color = Remap(rect.Color, next, menu);
      else if (element is NineSliceRuntime surface)
      {
        // Neutral artwork tints are not palette roles, even when a role happens
        // to be white or black. Keep tooltip fills and their shadows intact.
        var tint = surface.Color;
        bool neutral = (tint.R == 255 && tint.G == 255 && tint.B == 255)
          || (tint.R == 0 && tint.G == 0 && tint.B == 0);
        if (!neutral) surface.Color = Remap(tint, next, menu);
      }
      if (element.RenderableComponent is FontStashSharpText custom) custom.FillColor = Remap(custom.FillColor, next, menu);
    }
    foreach (var child in element.ContainedElements) Visit(child, changed, next, menu);
    if (element.Children != null)
      foreach (var child in element.Children)
        if (child is GraphicalUiElement gue) Visit(gue, changed, next, menu);
  }

  private static void ApplyNineSliceSkin(NineSliceRuntime ns, bool menu)
  {
    var currentTex = ns.Texture;
    var state = elementStates.GetValue(ns, _ => new ElementSkinState());
    if (currentTex != null && !GuiSkin.IsGumOverrideTexture(currentTex))
    {
      state.OriginalTexture = surfaceTextureSources.TryGetValue(currentTex, out var source) ? source : currentTex;
      if (!state.HasOverride)
        state.OriginalFrame = ns.CustomFrameTextureCoordinateWidth;
    }
    if (state.OriginalTexture == null || state.OriginalTexture.IsDisposed) return;

    var slot = ResolveSlot(ns, state.OriginalTexture);
    bool hover = GuiPalette.IsHoverAsset(GetTextureData(state.OriginalTexture)?.AssetName);
    if (slot == null || GuiSkin.IsBuiltIn(slot.Value))
    {
      if (state.HasOverride)
      {
        ns.Texture = state.OriginalTexture;
        ns.CustomFrameTextureCoordinateWidth = state.OriginalFrame;
        state.HasOverride = false;
      }
      var role = hover ? GuiColor.Hover : menu ? GuiColor.MenuInteraction
        : slot == GuiSpriteSlot.TreeNodeBackground ? GuiColor.NodeBorder : GuiColor.Accent;
      ns.Texture = UpdateTexture(state.OriginalTexture, role);
      return;
    }

    var device = state.OriginalTexture.GraphicsDevice;
    var overrideTex = GuiSkin.GetGumSlotTexture(device, slot.Value, menu, hover);
    var cue = GuiSkin.GetSlotCue(slot.Value);
    ns.Texture = overrideTex;
    if (!cue.Hidden && overrideTex != null)
    {
      int minDim = System.Math.Min(overrideTex.Width, overrideTex.Height);
      float baseFrame = cue.FrameWidth > 0 ? cue.FrameWidth : (state.OriginalFrame ?? 12f);
      float scaledFrame = System.Math.Clamp(baseFrame * cue.CornerScale, 1f, System.Math.Max(1f, minDim / 2f));
      ns.CustomFrameTextureCoordinateWidth = scaledFrame;
    }
    state.HasOverride = true;
  }

  private static void ApplySpriteSkin(SpriteRuntime sp, bool menu)
  {
    var currentTex = sp.Texture;
    var state = elementStates.GetValue(sp, _ => new ElementSkinState());
    if (currentTex != null && !GuiSkin.IsGumOverrideTexture(currentTex))
      state.OriginalTexture = surfaceTextureSources.TryGetValue(currentTex, out var source) ? source : currentTex;
    if (state.OriginalTexture == null || state.OriginalTexture.IsDisposed) return;

    var slot = ResolveSlot(sp, state.OriginalTexture);
    bool hover = GuiPalette.IsHoverAsset(GetTextureData(state.OriginalTexture)?.AssetName);
    if (slot == null || GuiSkin.IsBuiltIn(slot.Value))
    {
      if (state.HasOverride)
      {
        sp.Texture = state.OriginalTexture;
        if (sp.Name == "SliderFill")
        {
          sp.TextureLeft = state.OriginalTexture.Width / 2;
          sp.TextureTop = 0;
          sp.TextureWidth = 1;
          sp.TextureHeight = state.OriginalTexture.Height;
        }
        state.HasOverride = false;
      }
      var role = slot == GuiSpriteSlot.SliderFill ? GuiColor.Progress
        : hover ? GuiColor.Hover : menu ? GuiColor.MenuInteraction
        : slot == GuiSpriteSlot.TreeNodeBackground ? GuiColor.NodeBorder : GuiColor.Accent;
      sp.Texture = UpdateTexture(state.OriginalTexture, role);
      return;
    }

    var device = state.OriginalTexture.GraphicsDevice;
    var overrideTex = GuiSkin.GetGumSlotTexture(device, slot.Value, menu, hover);
    sp.Texture = overrideTex;
    if (sp.Name == "SliderFill" && overrideTex != null)
    {
      sp.TextureLeft = overrideTex.Width / 2;
      sp.TextureTop = 0;
      sp.TextureWidth = 1;
      sp.TextureHeight = overrideTex.Height;
    }
    state.HasOverride = true;
  }

  private static GuiSpriteSlot? ResolveSlot(GraphicalUiElement element, Texture2D originalTexture)
  {
    string elName = element.Name ?? "";
    if (elName.StartsWith("GumSurface:", System.StringComparison.Ordinal))
    {
      string surfaceAsset = elName["GumSurface:".Length..];
      return surfaceAsset switch
      {
        "modal_info_complete" => GuiSpriteSlot.PanelBackground,
        "modal_title_complete" => GuiSpriteSlot.PanelHeader,
        "option_background" => GuiSpriteSlot.TreeNodeBackground,
        "radio_unchecked" => GuiSpriteSlot.AbilitySlotIdle,
        "radio_checked_background" => GuiSpriteSlot.AbilitySlotActive,
        _ => null,
      };
    }

    if (element is NineSliceRuntime)
    {
      if (elName is "NineSliceInstance" or "PauseFrame")
      {
        string asset = GetTextureData(originalTexture)?.AssetName;
        return asset == "menu_left_backdrop_blue" ? null : GuiSpriteSlot.PanelBackground;
      }
      if (elName is "MainBrackets" or "PauseBrackets" or "WindowBrackets") return GuiSpriteSlot.PanelFrameOverlay;
      if (elName == "NineSliceInstance1") return GuiSpriteSlot.PanelHeader;
      if (elName == "ToggleRow") return GuiSpriteSlot.TreeNodeBackground;
      if (elName == "TrackBackground") return GuiSpriteSlot.SliderTrack;
      if (elName == "ButtonFrame")
      {
        string asset = GetTextureData(originalTexture)?.AssetName;
        return asset == "button_frame_active_blue"
          ? GuiSpriteSlot.MenuButtonFrameActive
          : GuiSpriteSlot.MenuButtonFrameIdle;
      }
      if (elName == "Background")
      {
        string asset = GetTextureData(originalTexture)?.AssetName;
        return asset switch
        {
          "modal_info_complete" => GuiSpriteSlot.PanelBackground,
          "button_active_blue" or "menu_button_hover_blue" => GuiSpriteSlot.HudButtonActive,
          "button_idle_blue" => GuiSpriteSlot.HudButtonIdle,
          "button_active_green" => GuiSpriteSlot.HudButtonConfirmActive,
          "button_idle_green" => GuiSpriteSlot.HudButtonConfirmIdle,
          _ => GuiSpriteSlot.HudButtonIdle,
        };
      }
    }

    if (element is SpriteRuntime)
    {
      if (elName == "BackgroundSprite") return GuiSpriteSlot.TreeNodeBackground;
      if (elName == "SliderFill") return GuiSpriteSlot.SliderFill;
      if (elName == "Background" && element.Parent?.Name == "ThumbInstance") return GuiSpriteSlot.SliderKnob;
      if (elName == "CheckboxBackground")
      {
        string asset = GetTextureData(originalTexture)?.AssetName;
        return asset == "toggle_on_background"
          ? GuiSpriteSlot.ToggleBackgroundOn
          : GuiSpriteSlot.ToggleBackgroundOff;
      }
      if (elName == "ToggleKnob")
      {
        string asset = GetTextureData(originalTexture)?.AssetName;
        if (asset == "toggle_on_knob") return GuiSpriteSlot.ToggleKnobOn;
        if (asset == "toggle_off_knob") return GuiSpriteSlot.ToggleKnobOff;
        return element.Width > 52 ? GuiSpriteSlot.ToggleKnobOn : GuiSpriteSlot.ToggleKnobOff;
      }
    }

    string fallbackAsset = GetTextureData(originalTexture)?.AssetName;
    return fallbackAsset switch
    {
      "modal_info_complete" => GuiSpriteSlot.PanelBackground,
      "modal_title_complete" => GuiSpriteSlot.PanelHeader,
      "panel_frame_overlay_blue" => GuiSpriteSlot.PanelFrameOverlay,
      "option_background" => GuiSpriteSlot.TreeNodeBackground,
      "radio_unchecked" => GuiSpriteSlot.AbilitySlotIdle,
      "radio_checked_background" => GuiSpriteSlot.AbilitySlotActive,
      _ => null,
    };
  }

  private static TextureColors GetTextureData(Texture2D texture)
  {
    if (texture == null || texture.IsDisposed) return null;
    return textures.GetValue(texture, t =>
    {
      var pixels = new Color[t.Width * t.Height];
      t.GetData(pixels);
      return new TextureColors
      {
        Original = pixels,
        AssetName = GuiSkin.IdentifyPixels(t.Width, t.Height, pixels)
      };
    });
  }

  private static Texture2D UpdateTexture(Texture2D texture, GuiColor role)
  {
    if (texture == null || texture.IsDisposed) return texture;
    var data = GetTextureData(texture);
    if (data == null) return texture;
    // Shared artwork needs separate menu and hover copies so changing one role
    // cannot recolor idle controls or another interaction state.
    if (role is GuiColor.MenuInteraction or GuiColor.Hover)
    {
      data.Variants.TryGetValue(role, out var variant);
      if (variant.Texture == null || variant.Texture.IsDisposed)
      {
        var copy = new Texture2D(texture.GraphicsDevice, texture.Width, texture.Height);
        variant = (copy, -1);
        surfaceTextureSources.Add(copy, texture);
        texture.GraphicsDevice.Disposing += (_, _) => copy.Dispose();
      }
      if (variant.Revision != GuiPalette.BaseRevision)
      {
        var variantPixels = new Color[data.Original.Length];
        for (int i = 0; i < variantPixels.Length; i++)
          variantPixels[i] = data.AssetName?.EndsWith("_green", System.StringComparison.Ordinal) == true
            ? GuiPalette.ConfirmSurface(data.Original[i], role == GuiColor.Hover ? role : GuiColor.Confirm)
            : GuiPalette.Surface(data.Original[i], role);
        variant.Texture.SetData(variantPixels);
        data.Variants[role] = (variant.Texture, GuiPalette.BaseRevision);
      }
      return variant.Texture;
    }
    if (data.Revision == GuiPalette.BaseRevision) return texture;
    var colors = new Color[data.Original.Length];
    for (int i = 0; i < colors.Length; i++) colors[i] = GuiPalette.Surface(data.Original[i], role);
    texture.SetData(colors);
    data.Revision = GuiPalette.BaseRevision;
    return texture;
  }
}
