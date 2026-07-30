using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SdfRectangleRenderer
{
    private Effect _rectEffect;
    private Texture2D _blankTexture;

    public SdfRectangleRenderer(GraphicsDevice graphicsDevice, Effect rectEffect)
    {
        _rectEffect = rectEffect;
        _blankTexture = new Texture2D(graphicsDevice, 1, 1);
        _blankTexture.SetData(new[] { Color.White });
    }

    public void DrawRectangle(
        SpriteBatch spriteBatch,
        float timeInSeconds,
        Rectangle rect,
        float thickness,
        float cornerRadius,
        Color coreColor,
        Color glowColor,
        float pulseProgress = -1.0f,      // 0.0 to 1.0 loops the perimeter
        Color? pulseColor = null)
    {
        // 1. Calculate bounding box padding.
        bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
        float baseGlowPadding = 45f;
        float pulseExtraPadding = isPulseActive ? 30f : 0f;

        float padding = thickness + baseGlowPadding + pulseExtraPadding;

        // 2. Expand the original rectangle by the padding so the glow doesn't get clipped
        Rectangle boundingBox = new Rectangle(
            (int)Math.Floor(rect.X - padding),
            (int)Math.Floor(rect.Y - padding),
            (int)Math.Ceiling(rect.Width + padding * 2),
            (int)Math.Ceiling(rect.Height + padding * 2)
        );

        Vector2 resolution = new Vector2(boundingBox.Width, boundingBox.Height);
        Vector2 rectSize = new Vector2(rect.Width, rect.Height);

        // 3. Set Shader Parameters
        _rectEffect.Parameters["Time"].SetValue(timeInSeconds);
        _rectEffect.Parameters["Resolution"].SetValue(resolution);
        
        // Pass the actual size of the rectangle without the padding
        _rectEffect.Parameters["RectSize"].SetValue(rectSize); 
        _rectEffect.Parameters["Thickness"].SetValue(thickness);
        _rectEffect.Parameters["CornerRadius"].SetValue(cornerRadius);
        _rectEffect.Parameters["CoreColor"].SetValue(coreColor.ToVector4());
        _rectEffect.Parameters["GlowColor"].SetValue(glowColor.ToVector4());

        // One-shot / looping pulse parameters
        _rectEffect.Parameters["PulseProgress"].SetValue(pulseProgress);
        _rectEffect.Parameters["PulseColor"].SetValue((pulseColor ?? Color.Gold).ToVector4());

        // 4. Draw
        spriteBatch.Draw(_blankTexture, boundingBox, Color.White);
    }
}
