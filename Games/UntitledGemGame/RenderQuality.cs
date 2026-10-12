internal static class RenderQuality
{
  // Scale world-camera zoom with target resolution so lowering web resolution
  // does not zoom in on the world or change the visible simulation area.
  public const float WorldScale =
#if KNI_WEB
    0.5f;
#else
    1f;
#endif

  public static float WorldZoom(float logicalZoom) => logicalZoom * WorldScale;
}
