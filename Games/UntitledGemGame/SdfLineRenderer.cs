using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SdfLineRenderer
{
    private Matrix _viewProjection;
    private readonly BlendState _blendState = RenderBlendFunctions.CreateGlowState();
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

    // 10,000 lines per draw call by default. If you draw more, it will automatically flush and start a new batch.
    private const int MAX_LINES = 10000;
    private readonly int _maxLines;

    public SdfLineRenderer(GraphicsDevice graphicsDevice, Effect effect, int maxLines = MAX_LINES)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;
        _maxLines = maxLines;

        _vertices = new VertexSdfLine[_maxLines * 4];
        _indices = new short[_maxLines * 6];

        // The index pattern for quads never changes, so we pre-fill it once at startup
        for (int i = 0, j = 0; i < _maxLines * 6; i += 6, j += 4)
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
        _viewProjection = viewProjection;
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

    public void DrawLine(Vector2 start, Vector2 end, float thickness, Color coreColor, Color glowColor, float pulseProgress = -1.0f, Color? pulseColor = null, Vector2? startJoin = null, Vector2? endJoin = null)
    {
        // If we hit the cap, force a draw so we don't overflow the array
        if (_lineCount >= _maxLines) Flush();

        bool isPulseActive = pulseProgress >= -0.2f && pulseProgress <= 1.2f;
        float baseGlowPadding =
#if KNI_WEB
          Math.Min(BaseGlowPadding, 10f);
#else
          BaseGlowPadding;
#endif
        float pulseExtraPadding = isPulseActive ?
#if KNI_WEB
          Math.Min(PulseExtraPadding, 6f)
#else
          PulseExtraPadding
#endif
          : 0f;
        float padding = thickness + baseGlowPadding + pulseExtraPadding;
        if (!RenderVisibility.Intersects(Math.Min(start.X, end.X) - padding,
            Math.Min(start.Y, end.Y) - padding, Math.Max(start.X, end.X) + padding,
            Math.Max(start.Y, end.Y) + padding, _viewProjection)) return;

        Vector2 min = Vector2.Min(start, end) - new Vector2(padding);
        Vector2 max = Vector2.Max(start, end) + new Vector2(padding);

        // Joined strips tile at a shared cross-section instead of stacking
        // the round caps/glow of every small segment in a curved connection.
        Vector2 corner0 = min, corner1 = new(max.X, min.Y);
        Vector2 corner2 = new(min.X, max.Y), corner3 = max;
        if (startJoin.HasValue && endJoin.HasValue)
        {
            corner0 = start - startJoin.Value;
            corner1 = end - endJoin.Value;
            corner2 = start + startJoin.Value;
            corner3 = end + endJoin.Value;
            min = Vector2.Min(Vector2.Min(corner0, corner1), Vector2.Min(corner2, corner3));
        }

        // Local coordinates relative to the bounding box
        Vector2 localStart = start - min;
        Vector2 localEnd = end - min;

        int vIndex = _lineCount * 4;

        // Top Left Vertex
        _vertices[vIndex] = new VertexSdfLine {
            Position = new Vector3(corner0, 0), LocalPos = corner0 - min,
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Top Right Vertex
        _vertices[vIndex + 1] = new VertexSdfLine {
            Position = new Vector3(corner1, 0), LocalPos = corner1 - min,
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Bottom Left Vertex
        _vertices[vIndex + 2] = new VertexSdfLine {
            Position = new Vector3(corner2, 0), LocalPos = corner2 - min,
            PointA = localStart, PointB = localEnd, Thickness = thickness, PulseProgress = pulseProgress,
            CoreColor = coreColor, GlowColor = glowColor
        };
        // Bottom Right Vertex
        _vertices[vIndex + 3] = new VertexSdfLine {
            Position = new Vector3(corner3, 0), LocalPos = corner3 - min,
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
        if (_lineCount == 0) return;
        // Set standard alpha blending for the glow




        // _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.BlendState = _blendState;
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
