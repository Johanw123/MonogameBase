using System;
using System.Collections.Generic;
using System.Linq;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

internal static partial class MenuTheme
{
  private static Color MenuText => GuiSkin.ButtonTextColor;
  private static Color MenuPanel => GuiSkin.PanelBackground;

  // Apply before runtime creation: Gum Forms still owns all interaction/state changes.
  private static bool ApplySkin(ElementSave element)
  {
    if (element.Name is not ("Controls/ButtonMainMenu" or "Controls/ButtonSettingsMenu"
      or "Controls/CheckBoxSettings" or "Controls/SliderSettings" or "Controls/SliderThumb"
      or "Controls/ComboBoxSettings" or "Controls/ListBoxItem" or "Controls/ListBox"
      or "MainMenu" or "SettingsMenu" or "CreditsMenu" or "GameMenu"))
      return false;

    var defaults = element.DefaultState;
    foreach (var state in ElementStates(element))
    {
      // Old style categories contain atlas coordinates and tint overrides.
      state.Variables.RemoveAll(v => v.Name.EndsWith(".ColorCategoryState") ||
        v.Name.EndsWith(".StyleCategoryState"));
      foreach (var text in element.Instances.Where(i => i.BaseType == "Text"))
        SetColor(state, text.Name + ".", state.Name.Contains("Disabled")
          ? GuiSkin.DisabledTextColor : MenuText);
    }

    var focus = element.Instances.FirstOrDefault(i => i.Name == "FocusedIndicator");
    if (focus != null)
    {
      focus.BaseType = "ColoredRectangle";
      foreach (var state in ElementStates(element))
      {
        state.Variables.RemoveAll(v => v.Name.StartsWith("FocusedIndicator.") &&
          (v.Name.EndsWith("SourceFile") || v.Name.EndsWith("Blend")));
        SetColor(state, "FocusedIndicator.", GuiSkin.HoverAccent);
      }
      Size(defaults, "FocusedIndicator", 0, 2, relativeWidth: true);
      Set(defaults, "FocusedIndicator.HasEvents", false, "bool");
    }

    switch (element.Name)
    {
      case "MainMenu":
        // Left-aligned column backdrop + subtle Synty HUD divider rules.
        var backing = element.Instances.FirstOrDefault(i => i.Name == "NineSliceInstance");
        if (backing != null)
        {
          element.Instances.Remove(backing);
          element.Instances.Insert(0, backing);
          Texture(defaults, backing.Name, "menu_left_backdrop_blue", 24);
          Size(defaults, backing.Name, 1180, 2160);
          Set(defaults, backing.Name + ".X", 0f, "float");
          Set(defaults, backing.Name + ".XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
          Set(defaults, backing.Name + ".XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
          Set(defaults, backing.Name + ".Y", 0f, "float");
          Set(defaults, backing.Name + ".YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
          Set(defaults, backing.Name + ".YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        }

        AddVisual(element, "GuiDecorationTopRule", "Sprite", 1);
        Texture(defaults, "GuiDecorationTopRule", "synty_hud_frame_horiz_02");
        Size(defaults, "GuiDecorationTopRule", 680, 20);
        Set(defaults, "GuiDecorationTopRule.X", 180f, "float");
        Set(defaults, "GuiDecorationTopRule.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "GuiDecorationTopRule.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "GuiDecorationTopRule.Y", 1308f, "float");
        Set(defaults, "GuiDecorationTopRule.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "GuiDecorationTopRule.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        SetColor(defaults, "GuiDecorationTopRule.", new Color(GuiSkin.MenuInteraction.R, GuiSkin.MenuInteraction.G, GuiSkin.MenuInteraction.B, (byte)190));

        AddVisual(element, "GuiDecorationBottomRule", "Sprite", 2);
        Texture(defaults, "GuiDecorationBottomRule", "synty_hud_frame_horiz_02");
        Size(defaults, "GuiDecorationBottomRule", 680, 16);
        Set(defaults, "GuiDecorationBottomRule.X", 180f, "float");
        Set(defaults, "GuiDecorationBottomRule.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "GuiDecorationBottomRule.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "GuiDecorationBottomRule.Y", -224f, "float");
        Set(defaults, "GuiDecorationBottomRule.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Bottom, "VerticalAlignment");
        Set(defaults, "GuiDecorationBottomRule.YUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
        SetColor(defaults, "GuiDecorationBottomRule.", new Color(GuiSkin.MenuInteraction.R, GuiSkin.MenuInteraction.G, GuiSkin.MenuInteraction.B, (byte)135));

        AddVisual(element, "GuiDecorationCalibrationTicks", "Sprite", 3);
        Texture(defaults, "GuiDecorationCalibrationTicks", "synty_hud_calibration_ticks");
        Size(defaults, "GuiDecorationCalibrationTicks", 28, 240);
        Set(defaults, "GuiDecorationCalibrationTicks.X", 124f, "float");
        Set(defaults, "GuiDecorationCalibrationTicks.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "GuiDecorationCalibrationTicks.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "GuiDecorationCalibrationTicks.Y", 1360f, "float");
        Set(defaults, "GuiDecorationCalibrationTicks.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "GuiDecorationCalibrationTicks.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        SetColor(defaults, "GuiDecorationCalibrationTicks.", new Color(GuiSkin.MenuInteraction.R, GuiSkin.MenuInteraction.G, GuiSkin.MenuInteraction.B, (byte)60));

        Set(defaults, "PanelInstance.Width", 680f, "float");
        Set(defaults, "PanelInstance.WidthUnits", DimensionUnitType.Absolute, "DimensionUnitType");
        Set(defaults, "PanelInstance.Height", 0f, "float");
        Set(defaults, "PanelInstance.HeightUnits", DimensionUnitType.RelativeToChildren, "DimensionUnitType");
        Set(defaults, "PanelInstance.X", 180f, "float");
        Set(defaults, "PanelInstance.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "PanelInstance.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "PanelInstance.Y", -256f, "float");
        Set(defaults, "PanelInstance.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Bottom, "VerticalAlignment");
        Set(defaults, "PanelInstance.YUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
        Set(defaults, "PanelInstance.StackSpacing", 16f, "float");
        foreach (string btnName in new[] { "ButtonContinue", "ButtonNewGame", "ButtonSettings", "ButtonCredits", "ButtonExit" })
          Set(defaults, btnName + ".Height", 96f, "float");

        Size(defaults, "ButtonWishlist", 356, 80);
        Set(defaults, "ButtonWishlist.X", 180f, "float");
        Set(defaults, "ButtonWishlist.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "ButtonWishlist.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "ButtonWishlist.Y", -124f, "float");
        Set(defaults, "ButtonWishlist.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Bottom, "VerticalAlignment");
        Set(defaults, "ButtonWishlist.YUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
        Set(defaults, "ButtonWishlist.TextInstance.X", 92f, "float");
        Set(defaults, "ButtonWishlist.TextInstance.Width", -108f, "float");
        Set(defaults, "ButtonWishlist.TextInstance.FontScale", 0.52f, "float");
        Size(defaults, "SteamIcon", 48, 48);
        Set(defaults, "SteamIcon.X", 24f, "float");
        Set(defaults, "SteamIcon.Y", 16f, "float");
        break;

      case "GameMenu":
        AddVisual(element, "PauseFrame", "NineSlice", 0);
        Texture(defaults, "PauseFrame", "modal_info_complete", 24);
        Size(defaults, "PauseFrame", 724, 536);
        Set(defaults, "PauseFrame.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Center, "HorizontalAlignment");
        Set(defaults, "PauseFrame.XUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
        Set(defaults, "PauseFrame.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Center, "VerticalAlignment");
        Set(defaults, "PauseFrame.YUnits", Gum.Converters.GeneralUnitType.PixelsFromMiddle, "GeneralUnitType");
        Set(defaults, "PauseFrame.Y", -68f, "float");
        AddVisual(element, "PauseBrackets", "NineSlice", 1);
        Set(defaults, "PauseBrackets.Parent", "PauseFrame", "string");
        Texture(defaults, "PauseBrackets", "panel_frame_overlay_blue", 48);
        Size(defaults, "PauseBrackets", 0, 0, true, true);
        break;

      case "Controls/ListBox":
        foreach (var state in ElementStates(element))
        {
          Texture(state, "Background", "modal_info_complete", 24);
          Size(state, "Background", 0, 0, true, true);
        }
        break;

      case "Controls/ButtonMainMenu":
        Set(defaults, "TextInstance.HorizontalAlignment", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "TextInstance.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "TextInstance.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "TextInstance.X", 36f, "float");
        Set(defaults, "TextInstance.Width", -64f, "float");
        Set(defaults, "TextInstance.FontScale", 0.82f, "float");
        // MenuHoverShimmer paints the highlight so it can animate. The states keep choosing
        // its artwork, but report hover and press through these empty containers instead.
        foreach (string signal in new[] { "HoverSignal", "PressSignal" })
        {
          AddVisual(element, signal, "Container", element.Instances.Count);
          Size(defaults, signal, 0, 0);
          Set(defaults, signal + ".HasEvents", false, "bool");
        }
        foreach (var state in ElementStates(element))
        {
          bool active = state.Name.Contains("Highlighted") || state.Name.Contains("Focused") ||
            state.Name.Contains("Selected") || state.Name.Contains("Pushed");
          Texture(state, "Background", active ? "menu_button_hover_blue" : "button_idle_blue", 12);
          Set(state, "Background.Alpha", 0, "int");
          Set(state, "HoverSignal.Visible", active, "bool");
          Set(state, "PressSignal.Visible", state.Name.Contains("Pushed"), "bool");
          Set(state, "FocusedIndicator.Visible", false, "bool");
        }
        break;

      case "Controls/ButtonSettingsMenu":
      case "Controls/ComboBoxSettings":
      case "Controls/ListBoxItem":
        bool hasCornerOverlay = element.Name is "Controls/ButtonSettingsMenu" or "Controls/ComboBoxSettings";
        if (hasCornerOverlay)
        {
          int bgIdx = Math.Max(0, element.Instances.FindIndex(i => i.Name == "Background"));
          AddVisual(element, "ButtonFrame", "NineSlice", bgIdx + 1);
          Size(defaults, "ButtonFrame", 0, 0, true, true);
        }
        foreach (var state in ElementStates(element))
        {
          bool active = state.Name.Contains("Highlighted") || state.Name.Contains("Focused") ||
            state.Name.Contains("Selected") || state.Name.Contains("Pushed");
          int alpha = state.Name.Contains("Disabled") ? 85 :
            state.Name.Contains("Pushed") ? 190 : 255;
          Texture(state, "Background", active ? "button_active_blue" : "button_idle_blue", 12);
          Set(state, "Background.Alpha", alpha, "int");
          if (hasCornerOverlay)
          {
            Texture(state, "ButtonFrame", active ? "button_frame_active_blue" : "button_frame_idle_blue", 28);
            Set(state, "ButtonFrame.Alpha", active ? alpha : 0, "int");
          }
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

      case "Controls/SliderThumb":
        Size(defaults, "", 64, 64);
        Size(defaults, "Background", 0, 0, true, true);
        foreach (var state in ElementStates(element))
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
        AddVisual(element, "ToggleRow", "NineSlice", 0);
        AddVisual(element, "ToggleKnob", "Sprite", element.Instances.Count);
        Texture(defaults, "ToggleRow", "option_background", 12);
        Size(defaults, "ToggleRow", 0, 0, true, true);
        Set(defaults, "ToggleKnob.Parent", "CheckboxBackground", "string");
        foreach (var state in ElementStates(element))
        {
          bool on = state.Name.EndsWith("On") || state.Name.EndsWith("Indeterminate");
          Size(state, "", 680, 80);
          Texture(state, "CheckboxBackground", on ? "toggle_on_background" : "toggle_off_background");
          Size(state, "CheckboxBackground", 104, 63);
          Set(state, "CheckboxBackground.X", -16f, "float");
          Set(state, "CheckboxBackground.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Right, "HorizontalAlignment");
          Set(state, "CheckboxBackground.XUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
          CenterVertically(state, "CheckboxBackground");
          Texture(state, "ToggleKnob", on ? "toggle_on_knob" : "toggle_off_knob");
          Size(state, "ToggleKnob", on ? 59 : 47, on ? 59 : 47);
          Set(state, "ToggleKnob.X", on ? 72.5f : 31.5f, "float");
          Set(state, "ToggleKnob.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Center, "HorizontalAlignment");
          CenterVertically(state, "ToggleKnob");
          int alpha = state.Name.Contains("Disabled") ? 85 : 255;
          Set(state, "CheckboxBackground.Alpha", alpha, "int");
          Set(state, "ToggleKnob.Alpha", alpha, "int");
          Set(state, "TextInstance.X", 20f, "float");
          Set(state, "TextInstance.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
          Set(state, "TextInstance.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
          Size(state, "TextInstance", -152, 0, true, true);
        }
        break;

      case "Controls/SliderSettings":
        element.Instances.First(i => i.Name == "ThumbInstance").BaseType = "Controls/SliderThumb";
        AddVisual(element, "SliderFill", "Sprite", element.Instances.FindIndex(i => i.Name == "ThumbInstance"));
        Set(defaults, "SliderFill.Parent", "TrackInstance", "string");
        Texture(defaults, "SliderFill", "slider_foreground");
        // Stretch a central vertical strip: the export's transparent end padding
        // must not create a gap between the fill and the start of the track.
        Set(defaults, "SliderFill.TextureAddress", Gum.Managers.TextureAddress.Custom, "TextureAddress");
        Set(defaults, "SliderFill.TextureLeft", 143, "int");
        Set(defaults, "SliderFill.TextureTop", 0, "int");
        Set(defaults, "SliderFill.TextureWidth", 1, "int");
        Set(defaults, "SliderFill.TextureHeight", 20, "int");
        Size(defaults, "SliderFill", 0, 20);
        Set(defaults, "SliderFill.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "SliderFill.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        CenterVertically(defaults, "SliderFill");
        Texture(defaults, "TrackBackground", "slider_background", 6);
        Size(defaults, "TrackBackground", 0, 20, relativeWidth: true);
        CenterVertically(defaults, "TrackBackground");
        Size(defaults, "ThumbInstance", 64, 64);
        Set(defaults, "TrackInstance.Width", -64f, "float");
        foreach (var state in ElementStates(element))
        {
          Set(state, "TrackBackground.Alpha", state.Name.Contains("Disabled") ? 80 : 255, "int");
          Set(state, "SliderFill.Alpha", state.Name.Contains("Disabled") ? 80 : 255, "int");
        }
        break;

      case "SettingsMenu":
      case "CreditsMenu":
        bool isSettings = element.Name == "SettingsMenu";
        AddVisual(element, "LeftBackdrop", "NineSlice", 0);
        Texture(defaults, "LeftBackdrop", "menu_left_backdrop_blue", 24);
        Size(defaults, "LeftBackdrop", 1180, 2160);
        Set(defaults, "LeftBackdrop.X", 0f, "float");
        Set(defaults, "LeftBackdrop.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "LeftBackdrop.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "LeftBackdrop.Y", 0f, "float");
        Set(defaults, "LeftBackdrop.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "LeftBackdrop.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");

        AddVisual(element, "GuiDecorationTopRule", "Sprite", 1);
        Texture(defaults, "GuiDecorationTopRule", "synty_hud_frame_horiz_02");
        Size(defaults, "GuiDecorationTopRule", 680, 20);
        Set(defaults, "GuiDecorationTopRule.X", 180f, "float");
        Set(defaults, "GuiDecorationTopRule.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "GuiDecorationTopRule.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "GuiDecorationTopRule.Y", isSettings ? 608f : 448f, "float");
        Set(defaults, "GuiDecorationTopRule.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "GuiDecorationTopRule.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        SetColor(defaults, "GuiDecorationTopRule.", new Color(GuiSkin.MenuInteraction.R, GuiSkin.MenuInteraction.G, GuiSkin.MenuInteraction.B, (byte)190));

        AddVisual(element, "GuiDecorationCalibrationTicks", "Sprite", 2);
        Texture(defaults, "GuiDecorationCalibrationTicks", "synty_hud_calibration_ticks");
        Size(defaults, "GuiDecorationCalibrationTicks", 28, 240);
        Set(defaults, "GuiDecorationCalibrationTicks.X", 124f, "float");
        Set(defaults, "GuiDecorationCalibrationTicks.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "GuiDecorationCalibrationTicks.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "GuiDecorationCalibrationTicks.Y", isSettings ? 675f : 515f, "float");
        Set(defaults, "GuiDecorationCalibrationTicks.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "GuiDecorationCalibrationTicks.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        SetColor(defaults, "GuiDecorationCalibrationTicks.", new Color(GuiSkin.MenuInteraction.R, GuiSkin.MenuInteraction.G, GuiSkin.MenuInteraction.B, (byte)60));

        Set(defaults, "PanelInstance.Width", isSettings ? 680f : 960f, "float");
        Set(defaults, "PanelInstance.Height", isSettings ? 1280f : 1200f, "float");
        Set(defaults, "PanelInstance.X", 180f, "float");
        Set(defaults, "PanelInstance.XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Left, "HorizontalAlignment");
        Set(defaults, "PanelInstance.XUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
        Set(defaults, "PanelInstance.Y", isSettings ? 660f : 680f, "float");
        Set(defaults, "PanelInstance.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
        Set(defaults, "PanelInstance.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");

        Texture(defaults, "NineSliceInstance", "modal_info_complete", 24);
        Size(defaults, "NineSliceInstance", 0, 0, true, true);
        int nsIdx = Math.Max(0, element.Instances.FindIndex(i => i.Name == "NineSliceInstance"));
        AddVisual(element, "WindowBrackets", "NineSlice", nsIdx + 1);
        Set(defaults, "WindowBrackets.Parent", "NineSliceInstance", "string");
        Texture(defaults, "WindowBrackets", "panel_frame_overlay_blue", 48);
        Size(defaults, "WindowBrackets", 0, 0, true, true);
        Texture(defaults, "NineSliceInstance1", "modal_title_complete", 24);
        Set(defaults, "PanelTitle.Width", isSettings ? 800f : 960f, "float");
        Set(defaults, "PanelTitle.Height", 92f, "float");
        Set(defaults, "PanelTitle.Y", 0f, "float");
        CenterVertically(defaults, "TextInstance");
        Set(defaults, "PanelInstance2.X", isSettings ? 0f : 60f, "float");
        Set(defaults, "PanelInstance2.Y", isSettings ? 0f : 122f, "float");
        Size(defaults, "PanelInstance2", isSettings ? 0 : -120, isSettings ? 0 : -172, true, true);
        Set(defaults, "PanelInstance1.Width", isSettings ? 680f : 840f, "float");
        Set(defaults, "PanelInstance1.StackSpacing", isSettings ? 18f : 22f, "float");
        Set(defaults, "PanelButtons.Width", isSettings ? 680f : 840f, "float");
        Set(defaults, "PanelButtons.ChildrenLayout", Gum.Managers.ChildrenLayout.LeftToRightStack, "ChildrenLayout");
        Set(defaults, "ButtonBack.Width", isSettings ? 328f : 260f, "float");
        Set(defaults, "ButtonBack.Height", 84f, "float");
        if (isSettings)
        {
          int p1Idx = element.Instances.FindIndex(i => i.Name == "PanelInstance1");
          int pbIdx = element.Instances.FindIndex(i => i.Name == "PanelButtons");
          if (pbIdx >= 0 && p1Idx > pbIdx)
          {
            var pbInstance = element.Instances[pbIdx];
            element.Instances.RemoveAt(pbIdx);
            element.Instances.Insert(p1Idx, pbInstance);
          }
          AddCalibrationWatermark(element);
          Set(defaults, "NineSliceInstance.Visible", false, "bool");
          Set(defaults, "WindowBrackets.Visible", false, "bool");
          Set(defaults, "PanelTitle.Visible", false, "bool");
          Set(defaults, "GuiDecorationDial.Visible", false, "bool");
          Set(defaults, "GuiDecorationInnerDial.Visible", false, "bool");
          Set(defaults, "GuiDecorationTicks.Visible", false, "bool");
          Set(defaults, "PanelInstance1.Height", 960f, "float");
          Set(defaults, "CheckBoxBorderless.X", 0f, "float");
          Set(defaults, "CheckBoxBorderless.Y", 0f, "float");
          foreach (string cb in new[] { "CheckBoxFullscreen", "CheckBoxBorderless", "CheckBoxVsync", "CheckBoxFixedTimeStep" })
            Set(defaults, cb + ".Width", 680f, "float");
          Set(defaults, "SliderMusicVolume.Width", 680f, "float");
          Set(defaults, "SliderSfxVolume.Width", 680f, "float");
          Set(defaults, "SliderMusicVolume.X", 0f, "float");
          Set(defaults, "SliderSfxVolume.X", 0f, "float");
          Set(defaults, "ComboBoxResolution.Width", 680f, "float");
          Set(defaults, "ComboBoxLanguage.Width", 680f, "float");
          Set(defaults, "TextInstance3.Text", Loc.N("Windowed Resolution"), "string");
          Set(defaults, "PanelButtons.StackSpacing", 24f, "float");
          Set(defaults, "PanelButtons.Y", 1148f, "float");
          Set(defaults, "PanelButtons.YOrigin", RenderingLibrary.Graphics.VerticalAlignment.Top, "VerticalAlignment");
          Set(defaults, "PanelButtons.YUnits", Gum.Converters.GeneralUnitType.PixelsFromSmall, "GeneralUnitType");
          Set(defaults, "ButtonReset.Width", 328f, "float");
          Set(defaults, "ButtonReset.Height", 84f, "float");
        }
        break;
    }
    return true;
  }

  private static IEnumerable<StateSave> ElementStates(ElementSave element) =>
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

  internal static void BindSlider(Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime slider)
  {
    var fill = slider.GetGraphicalUiElementByName("SliderFill");
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
