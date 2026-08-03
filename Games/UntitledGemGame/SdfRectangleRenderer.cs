// using System;
// using Microsoft.Xna.Framework;
// using Microsoft.Xna.Framework.Graphics;
//
// public class SdfRectangleRenderer
// {
//     private Effect _rectEffect;
//     private Texture2D _blankTexture;
//
//     public SdfRectangleRenderer(GraphicsDevice graphicsDevice, Effect rectEffect)
//     {
//         _rectEffect = rectEffect;
//         _blankTexture = new Texture2D(graphicsDevice, 1, 1);
//         _blankTexture.SetData(new[] { Color.White });
//     }
//
//     public void DrawRectangle(
//         SpriteBatch spriteBatch,
//         float timeInSeconds,
//         Rectangle rect,
//         float thickness,
//         float cornerRadius,
//         Color coreColor,
//         Color glowColor,
//         float pulseProgress = -1.0f,      // 0.0 to 1.0 loops the perimeter
//         Color? pulseColor = null)
//     {
//         // 1. Calculate bounding box padding.
//         bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
//         float baseGlowPadding = 45f;
//         float pulseExtraPadding = isPulseActive ? 30f : 0f;
//
//         float padding = thickness + baseGlowPadding + pulseExtraPadding;
//
//         // 2. Expand the original rectangle by the padding so the glow doesn't get clipped
//         Rectangle boundingBox = new Rectangle(
//             (int)Math.Floor(rect.X - padding),
//             (int)Math.Floor(rect.Y - padding),
//             (int)Math.Ceiling(rect.Width + padding * 2),
//             (int)Math.Ceiling(rect.Height + padding * 2)
//         );
//
//         Vector2 resolution = new Vector2(boundingBox.Width, boundingBox.Height);
//         Vector2 rectSize = new Vector2(rect.Width, rect.Height);
//
//         // 3. Set Shader Parameters
//         _rectEffect.Parameters["Time"].SetValue(timeInSeconds);
//         _rectEffect.Parameters["Resolution"].SetValue(resolution);
//
//         // Pass the actual size of the rectangle without the padding
//         _rectEffect.Parameters["RectSize"].SetValue(rectSize); 
//         _rectEffect.Parameters["Thickness"].SetValue(thickness);
//         _rectEffect.Parameters["CornerRadius"].SetValue(cornerRadius);
//         _rectEffect.Parameters["CoreColor"].SetValue(coreColor.ToVector4());
//         _rectEffect.Parameters["GlowColor"].SetValue(glowColor.ToVector4());
//
//         // One-shot / looping pulse parameters
//         _rectEffect.Parameters["PulseProgress"].SetValue(pulseProgress);
//         _rectEffect.Parameters["PulseColor"].SetValue((pulseColor ?? Color.Gold).ToVector4());
//
//         // 4. Draw
//         spriteBatch.Draw(_blankTexture, boundingBox, Color.White);
//     }
// }

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexSdfRect : IVertexType
{
  public Vector3 Position;
  public Vector2 LocalPos;
  public Vector2 HalfSize;
  public float CornerRadius;
  public float Thickness;
  public Color CoreColor;
  public Color GlowColor;

  public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
      new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
      new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
      new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
      new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),
      new VertexElement(32, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3),
      new VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0),
      new VertexElement(40, VertexElementFormat.Color, VertexElementUsage.Color, 1)
  );

  VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

public class SdfRectRenderer
{
  private Effect _effect;
  private GraphicsDevice _graphicsDevice;
  private VertexSdfRect[] _vertices;
  private short[] _indices;
  private int _rectCount;
  private const int MAX_RECTS = 2000;

  public SdfRectRenderer(GraphicsDevice gd, Effect effect)
  {
    // Color.Gold
    _graphicsDevice = gd;
    _effect = effect;
    _vertices = new VertexSdfRect[MAX_RECTS * 4];
    _indices = new short[MAX_RECTS * 6];

    for (int i = 0, j = 0; i < MAX_RECTS * 6; i += 6, j += 4)
    {
      _indices[i + 0] = (short)(j + 0); _indices[i + 1] = (short)(j + 1); _indices[i + 2] = (short)(j + 2);
      _indices[i + 3] = (short)(j + 2); _indices[i + 4] = (short)(j + 1); _indices[i + 5] = (short)(j + 3);
    }
  }

  public void Begin(Matrix viewProjection, float timeInSeconds)
  {
    _rectCount = 0;
    _effect.Parameters["MatrixTransform"].SetValue(viewProjection);
    _effect.Parameters["Time"]?.SetValue(timeInSeconds);
  }

  public void DrawRect(Rectangle rect, float thickness,float cornerRadius, Color core, Color glow)
  {
    if (_rectCount >= MAX_RECTS) Flush();

    Vector2 center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
    Vector2 halfSize = new Vector2(rect.Width / 2f, rect.Height / 2f);

    // Pad the bounding box slightly so the glow isn't cut off
    float padding = 20f;

    int vIndex = _rectCount * 4;

    // Quad vertices (LocalPos is relative to center)
    _vertices[vIndex + 0] = new VertexSdfRect { Position = new Vector3(rect.X - padding, rect.Y - padding, 0), LocalPos = new Vector2(-halfSize.X - padding, -halfSize.Y - padding), HalfSize = halfSize, CornerRadius = cornerRadius, Thickness = thickness, CoreColor = core, GlowColor = glow };
    _vertices[vIndex + 1] = new VertexSdfRect { Position = new Vector3(rect.Right + padding, rect.Y - padding, 0), LocalPos = new Vector2(halfSize.X + padding, -halfSize.Y - padding), HalfSize = halfSize, CornerRadius = cornerRadius, Thickness = thickness, CoreColor = core, GlowColor = glow };
    _vertices[vIndex + 2] = new VertexSdfRect { Position = new Vector3(rect.X - padding, rect.Bottom + padding, 0), LocalPos = new Vector2(-halfSize.X - padding, halfSize.Y + padding), HalfSize = halfSize, CornerRadius = cornerRadius, Thickness = thickness, CoreColor = core, GlowColor = glow };
    _vertices[vIndex + 3] = new VertexSdfRect { Position = new Vector3(rect.Right + padding, rect.Bottom + padding, 0), LocalPos = new Vector2(halfSize.X + padding, halfSize.Y + padding), HalfSize = halfSize, CornerRadius = cornerRadius, Thickness = thickness, CoreColor = core, GlowColor = glow };

    _rectCount++;
  }

  public void End() => Flush();

  private void Flush()
  {
    if (_rectCount == 0) return;


    var blendState = new Microsoft.Xna.Framework.Graphics.BlendState
    {
      ColorBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Add,
      AlphaBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Max,
      ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
      ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
      AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
      AlphaDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One
    };

    _graphicsDevice.BlendState = blendState;
    _effect.CurrentTechnique.Passes[0].Apply();
    _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _rectCount * 4, _indices, 0, _rectCount * 2);
    _rectCount = 0;
  }
}
