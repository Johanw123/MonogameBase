using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

public static partial class GuiSkin
{
  public static Rectangle SelectionMedallion(SpriteBatch batch, Rectangle bounds, bool selected, bool hovered, bool unlocked = true)
  {
    int diameter = (int)MathF.Round(Math.Min(bounds.Width, bounds.Height) * (selected ? 1f : 122f / 136f));
    var medallion = new Rectangle(bounds.Center.X - diameter / 2, bounds.Center.Y - diameter / 2, diameter, diameter);
    var accent = !unlocked ? BorderColor * 0.42f : selected ? PanelAccent : ButtonBorderColor;
    if (selected)
      DrawRotatedCentered(batch, "synty_hud_ring_calibration", medallion.Center.ToVector2(), diameter + 22,
        (float)JapeFramework.BaseGame.Time.TotalGameTime.TotalSeconds * 0.65f, PanelAccent * 0.72f);
    DrawHexSocket(batch, medallion, hovered ? Color.White : accent, active: selected || hovered);
    return medallion;
  }
}
