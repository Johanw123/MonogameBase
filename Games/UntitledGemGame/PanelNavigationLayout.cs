using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Every navigation destination uses one stationary frame and the same raised tabs.
public static class PanelNavigationLayout
{
  public const int TabCount = 5;
  public static Rectangle Panel => new(200, 156, HudLayout.Width - 400, Math.Min(1744, HudLayout.ContentBottom - 180));

  public static Rectangle Watermark => new(Panel.Center.X - 360, Panel.Center.Y - 360, 720, 720);
  public static Rectangle SelectionButton(int index) => new(Panel.X + 40 + index * 168, Panel.Y + 68, 136, 136);
  public static Vector2 SelectionHeading => new(Panel.X + 40, SelectionButton(0).Bottom + 52);
  public static int SelectionSubtitleY => SelectionButton(0).Bottom + 106;

  public static Rectangle Tab(int index)
  {
    var panel = Panel;
    int width = Math.Min(336, (panel.Width - 176 - 8 * (TabCount - 1)) / TabCount);
    return new(panel.X + 28 + index * (width + 8), panel.Y - 82, width, 86);
  }
}
