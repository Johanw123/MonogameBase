using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

internal static partial class MenuTheme
{
  // The backing owns these visuals, so they remain behind all interactive controls.
  // Keep them in the margins and at low alpha, like the Signals scanner watermark.
  private static void AddCalibrationWatermark(ElementSave element)
  {
    AddCalibrationMark(element, "Dial", "synty_hud_ring_calibration", 560, 560, -30, 230, 20);
    AddCalibrationMark(element, "InnerDial", "synty_hud_ring_calibration", 400, 400, -110, 310, 12);
    AddCalibrationMark(element, "Ticks", "synty_hud_calibration_ticks", 36, 250, -44, 825, 24);
  }

  private static void AddCalibrationMark(ElementSave element, string suffix, string asset,
    float width, float height, float right, float top, byte opacity)
  {
    string name = "GuiDecoration" + suffix;
    int index = element.Instances.FindIndex(i => i.Name == "NineSliceInstance") + 1;
    AddVisual(element, name, "Sprite", index);
    StateSave state = element.DefaultState;
    Texture(state, name, asset);
    Size(state, name, width, height);
    Set(state, name + ".Parent", "NineSliceInstance", "string");
    Set(state, name + ".X", right, "float");
    Set(state, name + ".Y", top, "float");
    Set(state, name + ".XOrigin", RenderingLibrary.Graphics.HorizontalAlignment.Right, "HorizontalAlignment");
    Set(state, name + ".XUnits", Gum.Converters.GeneralUnitType.PixelsFromLarge, "GeneralUnitType");
    SetColor(state, name + ".", new Color(GuiSkin.MenuButtonBorderColor.R,
      GuiSkin.MenuButtonBorderColor.G, GuiSkin.MenuButtonBorderColor.B, opacity));
  }
}
