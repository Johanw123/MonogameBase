// using System;
// using Microsoft.Xna.Framework;
// using Microsoft.Xna.Framework.Graphics;
//
// public class SdfLineRenderer
// {
//   private Effect _lineEffect;
//   private Texture2D _blankTexture;
//
//   public SdfLineRenderer(GraphicsDevice graphicsDevice, Effect lineEffect)
//   {
//     _lineEffect = lineEffect;
//     _blankTexture = new Texture2D(graphicsDevice, 1, 1);
//     _blankTexture.SetData(new[] { Color.White });
//   }
//
//   public void DrawLine(
//       SpriteBatch spriteBatch,
//       float timeInSeconds,
//       Vector2 start,
//       Vector2 end,
//       float thickness,
//       Color coreColor,
//       Color glowColor,
//       float pulseProgress = -1.0f,      // -1.0f means inactive
//       Color? pulseColor = null)
//   {
//     // 1. Calculate bounding box padding.
//     // If a pulse is traveling, add extra padding for the extra line width + bloom expansion.
//     bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
//     float baseGlowPadding = 45f;
//     float pulseExtraPadding = isPulseActive ? 30f : 0f;
//
//     float padding = thickness + baseGlowPadding + pulseExtraPadding;
//
//     Vector2 min = Vector2.Min(start, end) - new Vector2(padding);
//     Vector2 max = Vector2.Max(start, end) + new Vector2(padding);
//
//     Rectangle boundingBox = new Rectangle(
//         (int)Math.Floor(min.X),
//         (int)Math.Floor(min.Y),
//         (int)Math.Ceiling(max.X - min.X),
//         (int)Math.Ceiling(max.Y - min.Y)
//     );
//
//     // 2. Local space conversion
//     Vector2 localStart = start - new Vector2(boundingBox.X, boundingBox.Y);
//     Vector2 localEnd = end - new Vector2(boundingBox.X, boundingBox.Y);
//
//     // 3. Convert to UV Space (0.0 to 1.0)
//     Vector2 resolution = new Vector2(boundingBox.Width, boundingBox.Height);
//     Vector2 uvStart = localStart / resolution;
//     Vector2 uvEnd = localEnd / resolution;
//
//     // 4. Set Shader Parameters
//     _lineEffect.Parameters["Time"].SetValue(timeInSeconds);
//     _lineEffect.Parameters["Resolution"].SetValue(resolution);
//     _lineEffect.Parameters["PointA"].SetValue(uvStart);
//     _lineEffect.Parameters["PointB"].SetValue(uvEnd);
//     _lineEffect.Parameters["Thickness"].SetValue(thickness);
//     _lineEffect.Parameters["CoreColor"].SetValue(coreColor.ToVector4());
//     _lineEffect.Parameters["GlowColor"].SetValue(glowColor.ToVector4());
//
//     // One-shot pulse parameters
//     _lineEffect.Parameters["PulseProgress"].SetValue(pulseProgress);
//     _lineEffect.Parameters["PulseColor"].SetValue((pulseColor ?? Color.Gold).ToVector4());
//
//     // 5. Draw
//     spriteBatch.Draw(_blankTexture, boundingBox, Color.White);
//   }
// }

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SdfLineRenderer
{
    private Effect _effect;
    private GraphicsDevice _graphicsDevice;

    private VertexSdfLine[] _vertices;
    private short[] _indices;
    private int _lineCount;

    // Defaults preserve the upgrade-tree appearance. World-space effects can
    // use a much tighter profile without needing a second shader.
    public float WobbleAmount { get; set; } = 2f;
    public float ThicknessPulseAmount { get; set; } = 2f;
    public float PulseLengthScale { get; set; } = 500f;
    public float PulseWidthScale { get; set; } = 80f;
    public float PulseThicknessBoost { get; set; } = 8f;
    public float BaseGlowSpread { get; set; } = 15f;
    public float PulseGlowSpread { get; set; } = 20f;
    public float BaseGlowPadding { get; set; } = 45f;
    public float PulseExtraPadding { get; set; } = 30f;
    
    // 10,000 lines per draw call. If you draw more, it will automatically flush and start a new batch.
    private const int MAX_LINES = 10000; 

    public SdfLineRenderer(GraphicsDevice graphicsDevice, Effect effect)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;
        
        _vertices = new VertexSdfLine[MAX_LINES * 4];
        _indices = new short[MAX_LINES * 6];

        // The index pattern for quads never changes, so we pre-fill it once at startup
        for (int i = 0, j = 0; i < MAX_LINES * 6; i += 6, j += 4)
        {
            _indices[i + 0] = (short)(j + 0);
            _indices[i + 1] = (short)(j + 1);
            _indices[i + 2] = (short)(j + 2);
            _indices[i + 3] = (short)(j + 2);
            _indices[i + 4] = (short)(j + 1);
            _indices[i + 5] = (short)(j + 3);
        }
    }

    public void Begin(Matrix viewProjection, float timeInSeconds)
    {
        _lineCount = 0;
        _effect.Parameters["MatrixTransform"]?.SetValue(viewProjection);
        _effect.Parameters["Time"]?.SetValue(timeInSeconds);
        _effect.Parameters["WobbleAmount"]?.SetValue(WobbleAmount);
        _effect.Parameters["ThicknessPulseAmount"]?.SetValue(ThicknessPulseAmount);
        _effect.Parameters["PulseLengthScale"]?.SetValue(PulseLengthScale);
        _effect.Parameters["PulseWidthScale"]?.SetValue(PulseWidthScale);
        _effect.Parameters["PulseThicknessBoost"]?.SetValue(PulseThicknessBoost);
        _effect.Parameters["BaseGlowSpread"]?.SetValue(BaseGlowSpread);
        _effect.Parameters["PulseGlowSpread"]?.SetValue(PulseGlowSpread);
    }

    public void DrawLine(Vector2 start, Vector2 end, float thickness, Color coreColor, Color glowColor, float pulseProgress = -1.0f, Color? pulseColor = null)
    {
        // If we hit the cap, force a draw so we don't overflow the array
        if (_lineCount >= MAX_LINES) Flush();

        bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
        float baseGlowPadding = BaseGlowPadding;
        float pulseExtraPadding = isPulseActive ? PulseExtraPadding : 0f;
        float padding = thickness + baseGlowPadding + pulseExtraPadding;

        Vector2 min = Vector2.Min(start, end) - new Vector2(padding);
        Vector2 max = Vector2.Max(start, end) + new Vector2(padding);

        // Local coordinates relative to the bounding box
        Vector2 localStart = start - min;
        Vector2 localEnd = end - min;

        int vIndex = _lineCount * 4;
        float width = max.X - min.X;
        float height = max.Y - min.Y;

        // Top Left Vertex
        _vertices[vIndex] = new VertexSdfLine {
            Position = new Vector3(min.X, min.Y, 0), LocalPos = new Vector2(0, 0),
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Top Right Vertex
        _vertices[vIndex + 1] = new VertexSdfLine {
            Position = new Vector3(max.X, min.Y, 0), LocalPos = new Vector2(width, 0),
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Bottom Left Vertex
        _vertices[vIndex + 2] = new VertexSdfLine {
            Position = new Vector3(min.X, max.Y, 0), LocalPos = new Vector2(0, height),
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Bottom Right Vertex
        _vertices[vIndex + 3] = new VertexSdfLine {
            Position = new Vector3(max.X, max.Y, 0), LocalPos = new Vector2(width, height),
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };

        _lineCount++;
    }

    public void End()
    {
        if (_lineCount > 0) Flush();
    }

    private void Flush()
    {
        // Set standard alpha blending for the glow

      var blendState = new Microsoft.Xna.Framework.Graphics.BlendState
      {
        ColorBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Add,
        AlphaBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Max,
        ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        AlphaDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One
      };


        // _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.BlendState = blendState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        _effect.CurrentTechnique.Passes[0].Apply();
        
        _graphicsDevice.DrawUserIndexedPrimitives(
            PrimitiveType.TriangleList,
            _vertices, 0, _lineCount * 4,
            _indices, 0, _lineCount * 2
        );

        _lineCount = 0; // Reset for the next batch
    }
}
