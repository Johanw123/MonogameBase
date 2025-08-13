using System.Numerics;
using Viewport = FrogFight.Graphics.Viewport;

namespace FrogFight.Graphics
{
    public interface IImmediateRenderer
    {
        void Begin(MonoGame.Extended.OrthographicCamera camera, bool enableDepthBuffer, bool additiveBlend, bool cullCounterClockwise);

        void Draw(in Matrix4x4 worldTransform, in Mesh mesh);

        void End();
        Viewport Viewport { get; }
        void ClearScreen();
        void ClearDepthBuffer();
    }
}
