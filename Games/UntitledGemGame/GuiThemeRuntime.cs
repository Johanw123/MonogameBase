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
  private sealed record StateColor(VariableSave Red, VariableSave Green, VariableSave Blue, Color Original);
  private sealed class TextureColors
  {
    public Color[] Original;
    public int Revision = -1;
  }
  private static readonly List<StateColor> stateColors = new();
  private static readonly List<GraphicalUiElement> roots = new();
  private static readonly ConditionalWeakTable<Texture2D, TextureColors> textures = new();
  private static readonly HashSet<GraphicalUiElement> visited = new();
  private static readonly Color[] baseColors =
  [
    new(9, 17, 26), new(64, 150, 178), new(14, 28, 40), new(26, 56, 76),
    new(76, 204, 228), new(16, 31, 44), new(10, 20, 30), new(31, 65, 69)
  ];
  private static Color[] previous;
  private static int revision = -1;

  public static void CaptureStates(GumProjectSave project)
  {
    stateColors.Clear();
    roots.Clear();
    foreach (var element in project.Components.Cast<ElementSave>().Concat(project.Screens))
      foreach (var state in element.AllStates)
        foreach (var red in state.Variables.Where(v => v.Name.EndsWith(".Red") && v.Value is int))
        {
          string prefix = red.Name[..^3];
          var green = state.Variables.FirstOrDefault(v => v.Name == prefix + "Green");
          var blue = state.Variables.FirstOrDefault(v => v.Name == prefix + "Blue");
          if (green?.Value is int g && blue?.Value is int b)
            stateColors.Add(new(red, green, blue, new Color((int)red.Value, g, b)));
        }
    previous = ResolvedColors();
    revision = -1;
  }

  public static void Track(GraphicalUiElement root) => roots.Add(root);

  private static Color Resolve(Color original)
  {
    foreach (var role in System.Enum.GetValues<GuiColor>())
      if (GuiPalette.OriginalColor(role) == original) return GuiPalette.Get(role);
    return GuiPalette.Base(original);
  }

  private static Color[] ResolvedColors() => System.Enum.GetValues<GuiColor>().Select(GuiPalette.Get)
    .Concat(baseColors.Select(GuiPalette.Base)).ToArray();

  public static void Refresh()
  {
    bool changed = revision != GuiPalette.Revision;
    if (!changed && GuiPalette.BaseRevision == 0) return;
    Color[] next = changed ? ResolvedColors() : previous;
    if (changed)
      foreach (var binding in stateColors)
      {
        var color = Resolve(binding.Original);
        binding.Red.Value = (int)color.R;
        binding.Green.Value = (int)color.G;
        binding.Blue.Value = (int)color.B;
      }
    visited.Clear();
    foreach (var root in roots) Visit(root, changed, next);
    Visit(GumService.Default.Root, changed, next);
    Visit(GumService.Default.ModalRoot, changed, next);
    Visit(GumService.Default.PopupRoot, changed, next);
    if (RenderGuiSystem.Instance is { } gui)
    {
      foreach (var root in gui.skillTreeItems) Visit(root, changed, next);
      foreach (var root in gui.hudItems) Visit(root, changed, next);
      foreach (var root in gui.gameMenuItems) Visit(root, changed, next);
    }
    if (changed) { previous = next; revision = GuiPalette.Revision; }
    visited.Clear();
  }

  private static Color Remap(Color color, Color[] next)
  {
    if (previous == null) return color;
    for (int i = 0; i < previous.Length; i++)
      if (color.R == previous[i].R && color.G == previous[i].G && color.B == previous[i].B)
        return new Color(next[i].R, next[i].G, next[i].B, color.A);
    return color;
  }

  private static void Visit(GraphicalUiElement element, bool changed, Color[] next)
  {
    if (element == null || !visited.Add(element)) return;
    if (element is NineSliceRuntime panel) UpdateTexture(panel.Texture);
    if (element is SpriteRuntime sprite && element.Name is "OrbitFill" or "OrbitKnob" or "CheckboxBackground")
      UpdateTexture(sprite.Texture);
    if (changed)
    {
      if (element is SpriteRuntime decoration && element.Name?.StartsWith("OrbitDecoration", System.StringComparison.Ordinal) == true)
        decoration.Color = Remap(decoration.Color, next);
      if (element is TextRuntime text) text.Color = Remap(text.Color, next);
      else if (element is ColoredRectangleRuntime rectangle) rectangle.Color = Remap(rectangle.Color, next);
      else if (element is NineSliceRuntime surface) surface.Color = Remap(surface.Color, next);
      if (element.RenderableComponent is FontStashSharpText custom) custom.FillColor = Remap(custom.FillColor, next);
    }
    foreach (var child in element.ContainedElements) Visit(child, changed, next);
    if (element.Children != null)
      foreach (var child in element.Children)
        if (child is GraphicalUiElement gue) Visit(gue, changed, next);
  }

  private static void UpdateTexture(Texture2D texture)
  {
    if (GuiPalette.BaseRevision == 0 || texture == null || texture.IsDisposed) return;
    var data = textures.GetValue(texture, t =>
    {
      var pixels = new Color[t.Width * t.Height];
      t.GetData(pixels);
      return new TextureColors { Original = pixels };
    });
    if (data.Revision == GuiPalette.BaseRevision) return;
    var colors = new Color[data.Original.Length];
    for (int i = 0; i < colors.Length; i++) colors[i] = GuiPalette.Base(data.Original[i]);
    texture.SetData(colors);
    data.Revision = GuiPalette.BaseRevision;
  }
}
