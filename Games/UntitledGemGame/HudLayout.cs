using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// All HUD drawing and hit targets use the same virtual coordinates as Gum.
internal static class HudLayout
{
  public const int SlotPadding = 16;
  public const int Height = 100 + SlotPadding * 2;
  public const int Left = 24;
  public const int AbilityPointSpace = 304 + 24;
  public static readonly Color PanelColor = new Color(15, 13, 27, 255);
  public static readonly Color BorderColor = new Color(100, 78, 125, 180);
  public static readonly Color MutedTextColor = new Color(210, 203, 222);
  public static readonly Color ButtonColor = new Color(25, 22, 39);
  public static readonly Color ButtonHoverColor = new Color(39, 33, 55);
  public static readonly Color ButtonBorderColor = new Color(86, 70, 106);
  public static readonly Color ButtonTextColor = new Color(225, 218, 233);
  public static readonly Color AbilityAccent = new Color(145, 210, 255);
  public static readonly Color UpgradeAccent = new Color(255, 215, 150);
  public static int Width => BaseGame.BoxingViewportAdapterGui.VirtualWidth;
  public static float AbilitySlotsCenterX => Width / 2f + AbilityPointSpace;
  public static int Bottom => BaseGame.BoxingViewportAdapterGui.VirtualHeight;
  public static int Top => Bottom - Height;
  public static int ResourceWidth => (int)System.Math.Min(180, Width * 0.045f);
  // Borders are drawn into the virtual HUD texture before it is downscaled.
  // Keep two display pixels of coverage so fractional sampling cannot skip them.
  public static int ButtonBorderThickness
  {
    get
    {
      var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
      float pixelsPerUnit = System.Math.Max(0.0001f,
        System.Math.Min(System.Math.Abs(scale.M11), System.Math.Abs(scale.M22)));
      return (int)System.Math.Ceiling(2f / pixelsPerUnit);
    }
  }
  public static Rectangle NavigationButton(int index) => new Rectangle(
    AbilityPointPanel.Right + 24 + index * 246, Top + (Height - 60) / 2, 230, 60);
  public static Rectangle PrestigePanel => new Rectangle(
    Left + ResourceWidth * 4 + 24, Top + (Height - 76) / 2, 304, 76);
  public static Rectangle AbilityPointPanel => new Rectangle(
    PrestigePanel.Right + 24, Top + (Height - 76) / 2, 304, 76);
}
