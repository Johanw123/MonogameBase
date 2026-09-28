using System.Linq;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Theme the file-defined controls before creating their visuals, so Forms keeps
// ownership of hover, pressed, focus, disabled, and checked state transitions.
internal static partial class MenuTheme
{
  public static void Apply(GumProjectSave project)
  {
    var nineSliceDefaults = project.StandardElements.First(e => e.Name == "NineSlice").DefaultState;
    foreach (var element in project.Components.Cast<ElementSave>().Concat(project.Screens))
    {
      if (ApplyOrbit(element)) continue;
      var surfaces = element.Instances.Where(i => i.BaseType == "NineSlice").ToArray();
      foreach (var surface in surfaces)
      {
        // Gum's file-state setter does not dispatch RectangleRuntime.FillColor.
        // ColoredRectangle channels are supported by that setter, including AOT.
        // Preserve layout inherited from NineSlice before changing its base type.
        // In particular panels and tracks rely on its parent-relative size.
        foreach (var property in new[] { "Width", "Height", "WidthUnits", "HeightUnits", "X", "Y", "XOrigin", "YOrigin", "XUnits", "YUnits" })
        {
          string name = surface.Name + "." + property;
          if (!element.DefaultState.Variables.Any(v => v.Name == name && v.SetsValue && v.Value != null))
          {
            var inherited = nineSliceDefaults.Variables.FirstOrDefault(v => v.Name == property && v.SetsValue);
            if (inherited?.Value != null) Set(element.DefaultState, name, inherited.Value, inherited.Type);
          }
        }
        surface.BaseType = "ColoredRectangle";
        string prefix = surface.Name + ".";
        bool focus = surface.Name.Contains("Focused");
        bool panel = element is ScreenSave;
        string fillName = surface.Name + "Fill";
        if (!focus)
        {
          element.Instances.Insert(element.Instances.IndexOf(surface) + 1,
            new InstanceSave { Name = fillName, BaseType = "ColoredRectangle", ParentContainer = element });
          var defaults = element.DefaultState;
          Set(defaults, fillName + ".Parent", surface.Name, "string");
          Set(defaults, fillName + ".X", 4f, "float");
          Set(defaults, fillName + ".Y", 4f, "float");
          Set(defaults, fillName + ".Width", -8f, "float");
          Set(defaults, fillName + ".Height", -8f, "float");
          Set(defaults, fillName + ".WidthUnits", DimensionUnitType.RelativeToParent, "DimensionUnitType");
          Set(defaults, fillName + ".HeightUnits", DimensionUnitType.RelativeToParent, "DimensionUnitType");
          Set(defaults, fillName + ".HasEvents", false, "bool");
        }
        foreach (var state in element.States.Concat(element.Categories.SelectMany(c => c.States)))
        {
          state.Variables.RemoveAll(v => v.Name.StartsWith(prefix) &&
            (v.Name.EndsWith("SourceFile") || v.Name.EndsWith("ColorCategoryState") ||
             v.Name.EndsWith("StyleCategoryState") || v.Name.EndsWith("TextureAddress")));
          string name = state.Name;
          bool disabled = name.Contains("Disabled");
          bool active = name.Contains("Highlighted") || name.Contains("Selected");
          bool on = name.EndsWith("On");
          Color fill = panel ? HudLayout.PanelColor : HudLayout.ButtonColor;
          if (active) fill = HudLayout.ButtonHoverColor;
          if (name.Contains("Pushed")) fill = HudLayout.PanelColor;
          if (on) fill = disabled ? HudLayout.ButtonBorderColor : HudLayout.AbilityAccent;
          SetColor(state, prefix, focus || active ? HudLayout.AbilityAccent : HudLayout.ButtonBorderColor);
          if (!focus) SetColor(state, fillName + ".", fill);
        }
      }

      foreach (var text in element.Instances.Where(i => i.BaseType == "Text"))
      {
        foreach (var state in element.States.Concat(element.Categories.SelectMany(c => c.States)))
        {
          string prefix = text.Name + ".";
          state.Variables.RemoveAll(v => v.Name == prefix + "ColorCategoryState");
          var color = state.Name.Contains("Disabled") ? HudLayout.ButtonBorderColor : HudLayout.ButtonTextColor;
          Set(state, prefix + "Red", (int)color.R, "int");
          Set(state, prefix + "Green", (int)color.G, "int");
          Set(state, prefix + "Blue", (int)color.B, "int");
        }
      }
    }
  }

  private static void SetColor(StateSave state, string prefix, Color color)
  {
    Set(state, prefix + "Red", (int)color.R, "int");
    Set(state, prefix + "Green", (int)color.G, "int");
    Set(state, prefix + "Blue", (int)color.B, "int");
    Set(state, prefix + "Alpha", (int)color.A, "int");
  }

  private static void Set(StateSave state, string name, object value, string type)
  {
    state.Variables.RemoveAll(v => v.Name == name);
    state.Variables.Add(new VariableSave { Name = name, Value = value, Type = type, SetsValue = true });
  }
}
