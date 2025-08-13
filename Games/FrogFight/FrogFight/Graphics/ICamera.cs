using System.Numerics;
using Viewport = FrogFight.Graphics.Viewport;

namespace FrogFight.Graphics;

public interface ICamera
{
    Matrix4x4 GetViewTransform();
    Matrix4x4 GetProjectionTransform(in Viewport viewport);
}