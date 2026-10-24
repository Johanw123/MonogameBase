using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private List<string> WrapTooltipText(string text, int width, float fontSize)
  {
    var lines = new List<string>();
    foreach (string paragraph in text.Replace("\r", "").Split('\n'))
    {
      string line = "";
      foreach (var (word, join) in Loc.WrapPieces(paragraph))
      {
        string next = line.Length == 0 ? word : line + join + word;
        if (line.Length > 0 && Measure2(next, Vector2.Zero, fontSize).X > width)
        {
          lines.Add(line);
          line = word;
        }
        else line = next;
      }
      lines.Add(line);
    }
    return lines;
  }

  private void DrawTooltipHeader(SpriteBatch batch, Rectangle bounds, List<string> titleLines,
    string subtitle, Color accent)
  {
    int titleExtra = Math.Max(0, titleLines.Count - 1) * TooltipLayout.TitleLineHeight;
    int divider = (subtitle == null ? 72 : TooltipLayout.DividerTop) + titleExtra;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Tooltip(batch, bounds);
    GuiSkin.TechDivider(batch, new Rectangle(bounds.X + TooltipLayout.Padding, bounds.Y + divider,
      bounds.Width - TooltipLayout.Padding * 2, 3), accent * 0.65f);
    batch.End();
    for (int i = 0; i < titleLines.Count; i++)
      DrawPrestigeText(titleLines[i], new Vector2(bounds.X + TooltipLayout.Padding,
        bounds.Y + TooltipLayout.TitleTop + i * TooltipLayout.TitleLineHeight), accent, TooltipLayout.TitleSize);
    if (subtitle != null)
      DrawPrestigeText(subtitle, new Vector2(bounds.X + TooltipLayout.Padding,
        bounds.Y + TooltipLayout.SubtitleTop + titleExtra), GuiSkin.MutedTextColor, TooltipLayout.DetailSize);
  }
}
