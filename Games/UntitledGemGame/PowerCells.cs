using System;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

internal static class PowerCells
{
  // A shared, resolution-independent battery glyph for the wallet and purchase button.
  // The caller owns the SpriteBatch and its HUD transform.
  public static void DrawIcon(SpriteBatch batch, Rectangle bounds)
  {
    void Fill(float x, float y, float width, float height, Color color)
      => batch.Draw(AssetManager.DefaultTexture, new Rectangle(
        bounds.X + (int)(x * bounds.Width), bounds.Y + (int)(y * bounds.Height),
        Math.Max(1, (int)(width * bounds.Width)), Math.Max(1, (int)(height * bounds.Height))), color);

    Fill(.34f, 0, .32f, .12f, OrbitSkin.AbilityAccent);
    Fill(.12f, .10f, .76f, .90f, OrbitSkin.AbilityAccent);
    Fill(.21f, .18f, .58f, .74f, OrbitSkin.PanelColor);
    for (int cell = 0; cell < 3; cell++)
      Fill(.29f, .26f + cell * .22f, .42f, .14f, OrbitSkin.AbilityAccent);
    Fill(.12f, .10f, .08f, .90f, OrbitSkin.StatHeadingColor);
  }
}
