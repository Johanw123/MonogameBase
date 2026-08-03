// using System.Runtime.InteropServices;
// using Microsoft.Xna.Framework;
// using Microsoft.Xna.Framework.Graphics;
//
// [StructLayout(LayoutKind.Sequential, Pack = 1)]
// public struct VertexSdfLine : IVertexType
// {
//     public Vector3 Position;     // The corner of the bounding box
//     public Vector2 PointA;       // Line start
//     public Vector2 PointB;       // Line end
//     public Color CoreColor;      // Line color
//     public float Thickness;      // Line thickness
//     public float PulseProgress;  // Pulse state
//
//     // Define the memory layout so the GPU knows how to read the struct
//     public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
//         new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
//         new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0), // PointA
//         new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1), // PointB
//         new VertexElement(28, VertexElementFormat.Color, VertexElementUsage.Color, 0),               // CoreColor
//         new VertexElement(32, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),  // Thickness
//         new VertexElement(36, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3)   // PulseProgress
//     );
//
//     VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
// }

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexSdfLine : IVertexType
{
    public Vector3 Position;
    public Vector2 LocalPos;
    public Vector2 PointA;
    public Vector2 PointB;
    public float Thickness;
    public float PulseProgress;
    public Color CoreColor;
    public Color GlowColor;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(36, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(40, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(44, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(48, VertexElementFormat.Color, VertexElementUsage.Color, 1)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
