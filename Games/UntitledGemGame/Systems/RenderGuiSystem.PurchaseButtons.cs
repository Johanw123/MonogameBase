using System;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private const int PurchaseButtonHeight = 112;

  // Both gem purchases keep the action and its price inside the same hit target.
  private void DrawPurchaseButton(SpriteBatch batch, Rectangle bounds, string label, ulong? cost,
    bool enabled, bool hovered, float clickAnimation, string unavailableText, Texture2D actionIcon = null)
  {
    float pulse = enabled && clickAnimation > 0f
      ? MathF.Sin(Math.Clamp(clickAnimation, 0f, 1f) * MathHelper.Pi) : 0f;
    float iconSpace = actionIcon == null ? 0f : 42f;
    float labelSize = 28f;
    var labelMeasure = Measure2(label, Vector2.Zero, labelSize);
    labelSize *= Math.Min(1f, Math.Max(1f, bounds.Width - 40 - iconSpace) / Math.Max(1f, labelMeasure.X));
    labelMeasure = Measure2(label, Vector2.Zero, labelSize);
    float labelLeft = bounds.Center.X - (labelMeasure.X + iconSpace) / 2f;
    float labelCenterY = bounds.Y + 32;

    string price = cost is ulong amount ? NumberFormatter.AbbreviateBigNumber(amount) : unavailableText;
    float priceIconSpace = cost.HasValue ? 40f : 0f;
    float priceSize = 24f;
    var priceMeasure = Measure2(price, Vector2.Zero, priceSize);
    priceSize *= Math.Min(1f, Math.Max(1f, bounds.Width - 40 - priceIconSpace) / Math.Max(1f, priceMeasure.X));
    priceMeasure = Measure2(price, Vector2.Zero, priceSize);
    float priceLeft = bounds.Center.X - (priceMeasure.X + priceIconSpace) / 2f;
    float priceCenterY = bounds.Y + 78;
    Color iconTint = Color.White * (enabled ? 1f : 0.4f);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.HudButton(batch, bounds, GuiSkin.PanelAccent, false, enabled && hovered, pulse,
      opacity: enabled ? 1f : 0.4f);
    if (actionIcon != null)
      batch.Draw(actionIcon, new Rectangle((int)labelLeft, (int)labelCenterY - 17, 28, 34), iconTint);
    if (cost.HasValue)
      batch.Draw(GemSurfaceTexture.RedGemIcon(),
        new Rectangle((int)priceLeft, (int)priceCenterY - 17, 34, 34), iconTint);
    batch.End();

    const string font = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);
    FontManager.RenderFieldFont(font, label,
      new Vector2(labelLeft + iconSpace, labelCenterY - labelMeasure.Y / 2f),
      enabled ? GuiSkin.ButtonTextColor : GuiSkin.MutedTextColor, Color.Black, labelSize);
    FontManager.RenderFieldFont(font, price,
      new Vector2(priceLeft + priceIconSpace, priceCenterY - priceMeasure.Y / 2f),
      enabled ? GuiSkin.StatValueColor : GuiSkin.MutedTextColor, Color.Black, priceSize);
  }
}
