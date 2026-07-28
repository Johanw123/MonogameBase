using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SdfLineRenderer
{
  private Effect _lineEffect;
  private Texture2D _blankTexture;

  public SdfLineRenderer(GraphicsDevice graphicsDevice, Effect lineEffect)
  {
    _lineEffect = lineEffect;
    _blankTexture = new Texture2D(graphicsDevice, 1, 1);
    _blankTexture.SetData(new[] { Color.White });
  }

  public void DrawLine(
      SpriteBatch spriteBatch,
      float timeInSeconds,
      Vector2 start,
      Vector2 end,
      float thickness,
      Color coreColor,
      Color glowColor,
      float pulseProgress = -1.0f,      // -1.0f means inactive
      Color? pulseColor = null)
  {
    // 1. Calculate bounding box padding.
    // If a pulse is traveling, add extra padding for the extra line width + bloom expansion.
    bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
    float baseGlowPadding = 45f;
    float pulseExtraPadding = isPulseActive ? 30f : 0f;

    float padding = thickness + baseGlowPadding + pulseExtraPadding;

    Vector2 min = Vector2.Min(start, end) - new Vector2(padding);
    Vector2 max = Vector2.Max(start, end) + new Vector2(padding);

    Rectangle boundingBox = new Rectangle(
        (int)Math.Floor(min.X),
        (int)Math.Floor(min.Y),
        (int)Math.Ceiling(max.X - min.X),
        (int)Math.Ceiling(max.Y - min.Y)
    );

    // 2. Local space conversion
    Vector2 localStart = start - new Vector2(boundingBox.X, boundingBox.Y);
    Vector2 localEnd = end - new Vector2(boundingBox.X, boundingBox.Y);

    // 3. Convert to UV Space (0.0 to 1.0)
    Vector2 resolution = new Vector2(boundingBox.Width, boundingBox.Height);
    Vector2 uvStart = localStart / resolution;
    Vector2 uvEnd = localEnd / resolution;

    // 4. Set Shader Parameters
    _lineEffect.Parameters["Time"].SetValue(timeInSeconds);
    _lineEffect.Parameters["Resolution"].SetValue(resolution);
    _lineEffect.Parameters["PointA"].SetValue(uvStart);
    _lineEffect.Parameters["PointB"].SetValue(uvEnd);
    _lineEffect.Parameters["Thickness"].SetValue(thickness);
    _lineEffect.Parameters["CoreColor"].SetValue(coreColor.ToVector4());
    _lineEffect.Parameters["GlowColor"].SetValue(glowColor.ToVector4());

    // One-shot pulse parameters
    _lineEffect.Parameters["PulseProgress"].SetValue(pulseProgress);
    _lineEffect.Parameters["PulseColor"].SetValue((pulseColor ?? Color.Gold).ToVector4());

    // 5. Draw
    spriteBatch.Draw(_blankTexture, boundingBox, Color.White);
  }
}
