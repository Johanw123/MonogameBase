using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// All HUD drawing and hit targets use the same virtual coordinates as Gum.
internal static class HudLayout
{
  public const int SlotPadding = 16;
  public const int Height = 100 + SlotPadding * 2;
  public const int Left = 24;
  public static readonly Color PanelColor = new Color(15, 13, 27, 255);
  public static readonly Color BorderColor = new Color(100, 78, 125, 180);
  public static readonly Color MutedTextColor = new Color(170, 163, 183);
  public static int Width => BaseGame.BoxingViewportAdapterGui.VirtualWidth;
  public static int Bottom => BaseGame.BoxingViewportAdapterGui.VirtualHeight;
  public static int Top => Bottom - Height;
  public static int ResourceWidth => (int)System.Math.Min(180, Width * 0.045f);
  public static Rectangle NavigationButton(int index) => new Rectangle(
    PrestigePanel.Right + 24 + index * 216, Top + (Height - 60) / 2, 200, 60);
  public static Rectangle PrestigePanel => new Rectangle(
    Left + ResourceWidth * 4 + 24, Top + (Height - 76) / 2, 304, 76);
}
