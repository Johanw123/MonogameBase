using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Every navigation destination uses one stationary frame and a shared borderless navigation strip.
public static class PanelNavigationLayout
{
  public const int TabCount = 6;
  public static Rectangle Panel => new(200, 156, HudLayout.Width - 400, Math.Min(1744, HudLayout.ContentBottom - 180));

  public static Rectangle TabStrip => new(Panel.X, Panel.Top - 82, Panel.Width, 82);

  public static Rectangle Watermark => new(Panel.Center.X - 360, Panel.Center.Y - 360, 720, 720);
  public static Rectangle SelectionButton(int index) => new(Panel.X + 40 + index * 168, Panel.Y + 68, 136, 136);
  public static Vector2 SelectionHeading => new(Panel.X + 40, SelectionButton(0).Bottom + 52);
  public static int SelectionSubtitleY => SelectionButton(0).Bottom + 106;

  public static Rectangle Tab(int index)
  {
    var panel = Panel;
    int width = Math.Min(300, (panel.Width - 176 - 8 * (TabCount - 1)) / TabCount);
    return new(panel.X + 28 + index * (width + 8), TabStrip.Y, width, TabStrip.Height);
  }
}
