using System.Collections.Generic;
using System.Linq;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

internal static partial class MenuTheme
{
  private static readonly Color OrbitText = OrbitSkin.ButtonTextColor;
  private static readonly Color OrbitPanel = OrbitSkin.PanelBackground;

  // Apply before runtime creation: Gum Forms still owns all interaction/state changes.
  private static bool ApplyOrbit(ElementSave element)
  {
    if (element.Name is not ("Controls/ButtonMainMenu" or "Controls/ButtonSettingsMenu"
      or "Controls/CheckBoxSettings" or "Controls/SliderSettings" or "Controls/SliderThumbOrbit"
      or "Controls/ComboBoxSettings" or "Controls/ListBoxItem" or "Controls/ListBox"
      or "MainMenu" or "SettingsMenu" or "CreditsMenu" or "GameMenu"))
      return false;

    var defaults = element.DefaultState;
    foreach (var state in OrbitStates(element))
    {
      // Old style categories contain atlas coordinates and tint overrides.
      state.Variables.RemoveAll(v => v.Name.EndsWith(".ColorCategoryState") ||
        v.Name.EndsWith(".StyleCategoryState"));
      foreach (var text in element.Instances.Where(i => i.BaseType == "Text"))
        SetColor(state, text.Name + ".", state.Name.Contains("Disabled")
          ? OrbitSkin.DisabledTextColor : OrbitText);
    }

    var focus = element.Instances.FirstOrDefault(i => i.Name == "FocusedIndicator");
    if (focus != null)
    {
      focus.BaseType = "ColoredRectangle";
      foreach (var state in OrbitStates(element))
      {
        state.Variables.RemoveAll(v => v.Name.StartsWith("FocusedIndicator.") &&
          (v.Name.EndsWith("SourceFile") || v.Name.EndsWith("Blend")));
        SetColor(state, "FocusedIndicator.", OrbitSkin.Accent);
      }
      Size(defaults, "FocusedIndicator", 0, 2, relativeWidth: true);
      Set(defaults, "FocusedIndicator.HasEvents", false, "bool");
    }

    switch (element.Name)
    {
      case "MainMenu":
        // Frame the main-menu button group with the Synty tactical window panel.
        var backing = element.Instances.FirstOrDefault(i => i.Name == "NineSliceInstance");
        if (backing != null)
        {
          Texture(defaults, backing.Name, "modal_info_complete", 24);
          Size(defaults, backing.Name, 668, 672);
          Set(defaults, backing.Name + ".XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Center, "HorizontalAlignment");
          Set(defaults, backing.Name + ".XUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
          Set(defaults, backing.Name + ".YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Bottom, "VerticalAlignment");
          Set(defaults, backing.Name + ".YUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
          Set(defaults, backing.Name + ".Y", -8f, "float");
          Set(defaults, "PanelInstance.Y", -36f, "float");
        }
        break;

      case "GameMenu":
        AddVisual(element, "OrbitPauseFrame", "NineSlice", 0);
        Texture(defaults, "OrbitPauseFrame", "modal_info_complete", 24);
        Size(defaults, "OrbitPauseFrame", 724, 536);
        Set(defaults, "OrbitPauseFrame.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Center, "HorizontalAlignment");
        Set(defaults, "OrbitPauseFrame.XUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
        Set(defaults, "OrbitPauseFrame.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Center, "VerticalAlignment");
        Set(defaults, "OrbitPauseFrame.YUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
        Set(defaults, "OrbitPauseFrame.Y", -68f, "float");
        break;

      case "Controls/ListBox":
        foreach (var state in OrbitStates(element))
        {
          Texture(state, "Background", "modal_info_complete", 24);
          Size(state, "Background", 0, 0, true, true);
        }
        break;

      case "Controls/ButtonMainMenu":
      case "Controls/ButtonSettingsMenu":
      case "Controls/ComboBoxSettings":
      case "Controls/ListBoxItem":
        foreach (var state in OrbitStates(element))
        {
          bool active = state.Name.Contains("Highlighted") || state.Name.Contains("Focused") ||
            state.Name.Contains("Selected") || state.Name.Contains("Pushed");
          Texture(state, "Background", active ? "button_active_blue" : "button_idle_blue", 12);
          Set(state, "Background.Alpha", state.Name.Contains("Disabled") ? 85 :
            state.Name.Contains("Pushed") ? 190 : 255, "int");
        }
        if (element.Name == "Controls/ListBoxItem")
        {
          Size(defaults, "", 0, 64, relativeWidth: true);
          Set(defaults, "TextInstance.FontSize", 40, "int");
          Set(defaults, "TextInstance.Width", -32f, "float");
        }
        if (element.Name == "Controls/ComboBoxSettings")
        {
          Set(defaults, "ListBoxInstance.Y", 68f, "float");
          Set(defaults, "TextInstance.Width", -72f, "float");
          Set(defaults, "IconInstance.X", -28f, "float");
        }
        break;

      case "Controls/SliderThumbOrbit":
        Size(defaults, "", 64, 64);
        Size(defaults, "Background", 0, 0, true, true);
        foreach (var state in OrbitStates(element))
        {
          Texture(state, "Background", "slider_knob");
          // The PNG already has a soft glow around an opaque core. Fading the
          // whole sprite makes the track show through that core.
          SetColor(state, "Background.", state.Name.Contains("Disabled")
            ? new Color(90, 130, 140) : Color.White);
        }
        break;

      case "Controls/CheckBoxSettings":
        element.Instances.First(i => i.Name == "CheckboxBackground").BaseType = "Sprite";
        AddVisual(element, "OrbitRow", "NineSlice", 0);
        AddVisual(element, "OrbitKnob", "Sprite", element.Instances.Count);
        Texture(defaults, "OrbitRow", "option_background", 12);
        Size(defaults, "OrbitRow", 0, 0, true, true);
        Set(defaults, "OrbitKnob.Parent", "CheckboxBackground", "string");
        foreach (var state in OrbitStates(element))
        {
          bool on = state.Name.EndsWith("On") || state.Name.EndsWith("Indeterminate");
          Size(state, "", 800, 88);
          Texture(state, "CheckboxBackground", on ? "toggle_on_background" : "toggle_off_background");
          Size(state, "CheckboxBackground", 104, 63);
          Set(state, "CheckboxBackground.X", -16f, "float");
          Set(state, "CheckboxBackground.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Right, "HorizontalAlignment");
          Set(state, "CheckboxBackground.XUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
          CenterVertically(state, "CheckboxBackground");
          Texture(state, "OrbitKnob", on ? "toggle_on_knob" : "toggle_off_knob");
          Size(state, "OrbitKnob", on ? 59 : 47, on ? 59 : 47);
          Set(state, "OrbitKnob.X", on ? 72.5f : 31.5f, "float");
          Set(state, "OrbitKnob.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Center, "HorizontalAlignment");
          CenterVertically(state, "OrbitKnob");
          int alpha = state.Name.Contains("Disabled") ? 85 : 255;
          Set(state, "CheckboxBackground.Alpha", alpha, "int");
          Set(state, "OrbitKnob.Alpha", alpha, "int");
          Set(state, "TextInstance.X", 20f, "float");
          Set(state, "TextInstance.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
          Set(state, "TextInstance.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
          Size(state, "TextInstance", -152, 0, true, true);
        }
        break;

      case "Controls/SliderSettings":
        element.Instances.First(i => i.Name == "ThumbInstance").BaseType = "Controls/SliderThumbOrbit";
        AddVisual(element, "OrbitFill", "Sprite", element.Instances.FindIndex(i => i.Name == "ThumbInstance"));
        Set(defaults, "OrbitFill.Parent", "TrackInstance", "string");
        Texture(defaults, "OrbitFill", "slider_foreground");
        // Stretch a central vertical strip: the export's transparent end padding
        // must not create a gap between the fill and the start of the track.
        Set(defaults, "OrbitFill.TextureAddress", Gum.Managers.TextureAddress.Custom, "TextureAddress");
        Set(defaults, "OrbitFill.TextureLeft", 143, "int");
        Set(defaults, "OrbitFill.TextureTop", 0, "int");
        Set(defaults, "OrbitFill.TextureWidth", 1, "int");
        Set(defaults, "OrbitFill.TextureHeight", 20, "int");
        Size(defaults, "OrbitFill", 0, 20);
        Set(defaults, "OrbitFill.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "OrbitFill.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        CenterVertically(defaults, "OrbitFill");
        Texture(defaults, "TrackBackground", "slider_background", 6);
        Size(defaults, "TrackBackground", 0, 20, relativeWidth: true);
        CenterVertically(defaults, "TrackBackground");
        Size(defaults, "ThumbInstance", 64, 64);
        Set(defaults, "TrackInstance.Width", -64f, "float");
        foreach (var state in OrbitStates(element))
        {
          Set(state, "TrackBackground.Alpha", state.Name.Contains("Disabled") ? 80 : 255, "int");
          Set(state, "OrbitFill.Alpha", state.Name.Contains("Disabled") ? 80 : 255, "int");
        }
        break;

      case "SettingsMenu":
      case "CreditsMenu":
        Texture(defaults, "NineSliceInstance", "modal_info_complete", 24);
        Size(defaults, "NineSliceInstance", 0, 0, true, true);
        Texture(defaults, "NineSliceInstance1", "modal_title_complete", 24);
        Set(defaults, "PanelTitle.Width", 1000f, "float");
        Set(defaults, "PanelTitle.Height", 98f, "float");
        Set(defaults, "PanelTitle.Y", 0f, "float");
        CenterVertically(defaults, "TextInstance");
        Set(defaults, "PanelInstance2.X", 70f, "float");
        Set(defaults, "PanelInstance2.Y", 140f, "float");
        Size(defaults, "PanelInstance2", -140, -210, true, true);
        Set(defaults, "PanelInstance1.Width", 860f, "float");
        Set(defaults, "PanelButtons.Width", 860f, "float");
        Set(defaults, "PanelButtons.ChildrenLayout", Gum.Managers.ChildrenLayout.LeftToRightStack, "ChildrenLayout");
        Set(defaults, "ButtonBack.Width", 260f, "float");
        Set(defaults, "ButtonBack.Height", 88f, "float");
        if (element.Name == "SettingsMenu")
        {
          Set(defaults, "PanelInstance.Height", 1500f, "float");
          Set(defaults, "CheckBoxBorderless.X", 0f, "float");
          Set(defaults, "CheckBoxBorderless.Y", 0f, "float");
          Set(defaults, "SliderMusicVolume.Width", 800f, "float");
          Set(defaults, "SliderSfxVolume.Width", 800f, "float");
          Set(defaults, "SliderMusicVolume.X", 0f, "float");
          Set(defaults, "SliderSfxVolume.X", 0f, "float");
          Set(defaults, "ComboBoxResolution.Width", 800f, "float");
          Set(defaults, "ComboBoxLanguage.Width", 800f, "float");
          Set(defaults, "TextInstance3.Text", Loc.N("Windowed Resolution"), "string");
          Set(defaults, "ButtonReset.Width", 260f, "float");
          Set(defaults, "ButtonReset.Height", 88f, "float");
        }
        break;
    }
    return true;
  }

  private static IEnumerable<StateSave> OrbitStates(ElementSave element) =>
    element.States.Concat(element.Categories.SelectMany(c => c.States));

  private static void Texture(StateSave state, string name, string asset, float frame = 0)
  {
    Set(state, name + ".SourceFile", "../Menu/" + asset + ".png", "string");
    Set(state, name + ".TextureAddress", Gum.Managers.TextureAddress.EntireTexture, "TextureAddress");
    SetColor(state, name + ".", Color.White);
    Set(state, name + ".HasEvents", false, "bool");
    if (frame > 0) Set(state, name + ".CustomFrameTextureCoordinateWidth", frame, "float?");
  }

  private static void Size(StateSave state, string name, float width, float height,
    bool relativeWidth = false, bool relativeHeight = false)
  {
    string prefix = name.Length == 0 ? "" : name + ".";
    Set(state, prefix + "Width", width, "float");
    Set(state, prefix + "Height", height, "float");
    Set(state, prefix + "WidthUnits", relativeWidth ? DimensionUnitType.RelativeToParent : DimensionUnitType.Absolute, "DimensionUnitType");
    Set(state, prefix + "HeightUnits", relativeHeight ? DimensionUnitType.RelativeToParent : DimensionUnitType.Absolute, "DimensionUnitType");
  }

  private static void CenterVertically(StateSave state, string name)
  {
    Set(state, name + ".Y", 0f, "float");
    Set(state, name + ".YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Center, "VerticalAlignment");
    Set(state, name + ".YUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
  }

  private static void AddVisual(ElementSave element, string name, string type, int index) =>
    element.Instances.Insert(index, new InstanceSave { Name = name, BaseType = type, ParentContainer = element });

  internal static void BindOrbitSlider(Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime slider)
  {
    var fill = slider.GetGraphicalUiElementByName("OrbitFill");
    void Refresh()
    {
      var control = slider.FormsControl;
      double range = control.Maximum - control.Minimum;
      fill.WidthUnits = DimensionUnitType.PercentageOfParent;
      fill.Width = range > 0 ? (float)(100 * (control.Value - control.Minimum) / range) : 0;
      fill.Visible = fill.Width > 0;
    }
    slider.FormsControl.ValueChanged += (_, _) => Refresh();
    Refresh();
  }
}
