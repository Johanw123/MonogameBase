using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexTractorBelt : IVertexType
{
  public Vector3 Position;
  public Vector4 Shape;
  public Vector4 Shape2;
  public Vector4 Style;
  public Color Tint;

  public static readonly VertexDeclaration VertexDeclaration = new(
    new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
    new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
    new VertexElement(28, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
    new VertexElement(44, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
    new VertexElement(60, VertexElementFormat.Color, VertexElementUsage.Color, 0));

  VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

// Line widths for one frame: world sizes that never drop below a few pixels when zoomed out.
public readonly record struct TractorBeltWidths(float Pixel, float RailOffset, float RailHalfWidth, float BedRadius,
  float ChevronSize, float ChevronHalfWidth, float Margin)
{
  public static TractorBeltWidths For(float pixel)
  {
    float Width(float world, float pixels) => Math.Max(world, pixels * pixel);
    float railOffset = Width(4.5f, 3.5f), railHalf = Width(0.45f, 0.55f), bed = Width(6f, 5f);
    return new(pixel, railOffset, railHalf, bed, Width(3f, 2.5f), Width(0.4f, 0.5f),
      Math.Max(bed * 2.6f, railOffset + railHalf + 2f * pixel));
  }
}

// Batches TractorBelt.fx quads: belt pieces that tile at shared cross-sections, and the
// intake's marks. A few dozen quads a frame, drawn in one call.
public sealed class TractorBeltRenderer
{
  private readonly GraphicsDevice device;
  private readonly Effect effect;
  private VertexTractorBelt[] vertices = new VertexTractorBelt[128 * 4];
  private short[] indices = Array.Empty<short>();
  private int quads;

  public TractorBeltRenderer(GraphicsDevice device, Effect effect)
  {
    this.device = device;
    this.effect = effect;
    BuildIndices(vertices.Length / 4);
  }

  private void BuildIndices(int count)
  {
    indices = new short[count * 6];
    for (int i = 0, j = 0; i < indices.Length; i += 6, j += 4)
    {
      indices[i] = (short)j;
      indices[i + 1] = (short)(j + 1);
      indices[i + 2] = (short)(j + 2);
      indices[i + 3] = (short)(j + 2);
      indices[i + 4] = (short)(j + 1);
      indices[i + 5] = (short)(j + 3);
    }
  }

  public void Begin(Matrix viewProjection, in TractorBeltWidths widths)
  {
    quads = 0;
    var parameters = effect.Parameters;
    parameters["MatrixTransform"]?.SetValue(viewProjection);
    parameters["PixelSize"]?.SetValue(widths.Pixel);
    parameters["RailOffset"]?.SetValue(widths.RailOffset);
    parameters["RailHalfWidth"]?.SetValue(widths.RailHalfWidth);
    parameters["BedRadius"]?.SetValue(widths.BedRadius);
    parameters["ChevronSize"]?.SetValue(widths.ChevronSize);
    parameters["ChevronHalfWidth"]?.SetValue(widths.ChevronHalfWidth);
    parameters["ChevronSpacing"]?.SetValue(TractorBelt.PodSpacing);
    parameters["EndReach"]?.SetValue(TractorBelt.EndReach);
  }

  // Corners in strip order: start-left, end-left, start-right, end-right.
  public void Quad(Vector2 startLeft, Vector2 endLeft, Vector2 startRight, Vector2 endRight,
    Vector4 shape, Vector4 shape2, Vector4 style, Color tint)
  {
    if ((quads + 1) * 4 > vertices.Length)
    {
      Array.Resize(ref vertices, vertices.Length * 2);
      BuildIndices(vertices.Length / 4);
    }
    int v = quads * 4;
    vertices[v] = new VertexTractorBelt { Position = new Vector3(startLeft, 0), Shape = shape, Shape2 = shape2, Style = style, Tint = tint };
    vertices[v + 1] = new VertexTractorBelt { Position = new Vector3(endLeft, 0), Shape = shape, Shape2 = shape2, Style = style, Tint = tint };
    vertices[v + 2] = new VertexTractorBelt { Position = new Vector3(startRight, 0), Shape = shape, Shape2 = shape2, Style = style, Tint = tint };
    vertices[v + 3] = new VertexTractorBelt { Position = new Vector3(endRight, 0), Shape = shape, Shape2 = shape2, Style = style, Tint = tint };
    quads++;
  }

  public void Square(Vector2 center, float halfSize, Vector4 shape, Vector4 shape2, Vector4 style, Color tint)
    => Quad(center + new Vector2(-halfSize, -halfSize), center + new Vector2(halfSize, -halfSize),
      center + new Vector2(-halfSize, halfSize), center + new Vector2(halfSize, halfSize), shape, shape2, style, tint);

  public void End()
  {
    if (quads == 0) return;
    device.BlendState = BlendState.Additive;
    device.DepthStencilState = DepthStencilState.None;
    device.RasterizerState = RasterizerState.CullNone;
    effect.CurrentTechnique.Passes[0].Apply();
    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, quads * 4, indices, 0, quads * 2);
    quads = 0;
  }
}
