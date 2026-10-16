using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public static class PlanetPresentation
{
  public const float PlanetGrowthPerExpandSpace = 0.2f;

  // Expand Space enlarges the base planet; each core crack still adds its usual
  // growth on top. The shared size drives rendering, mining and ship avoidance.
  public static float PlanetSize(int fractures, int expandSpaceLevel)
    => CoreFracture.PlanetSize(fractures) * (1f + Math.Max(0, expandSpaceLevel) * PlanetGrowthPerExpandSpace);

  // Preserve the first run's composition and its visible empty space as the
  // camera zooms out. Growth adds room on top of that gap.
  public static (float Ship, float Planet) HorizontalOffsets(float logicalZoom, float planetRadius, float shipHalfWidth)
  {
    float zoomScale = Math.Max(1f, 3.5f / Math.Max(0.3f, logicalZoom));
    float growth = Math.Max(0f, planetRadius - 93f) + Math.Max(0f, shipHalfWidth - 29.2f);
    return (-240f * zoomScale - growth * 0.5f, 190f * zoomScale + growth * 0.5f);
  }

  // A rare currency pickup should remain legible at every Expand Space level.
  public static float ShardSize(float logicalZoom) => Math.Max(84f, 128f / Math.Max(0.3f, logicalZoom));
}
