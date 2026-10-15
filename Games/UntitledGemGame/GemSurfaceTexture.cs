using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Numeric shader data for GemShader.fx.
// R: tint weight, G: white highlight, B: inner outline, A: coverage.
// Bake derives it from a grayscale sprite (the gem shader checks use it). World gems
// ship already packed (Textures/Gems/plasma_orb.png, tools/build_gem_surface.py) and
// only get mipmaps here.
// Keep the source dimensions/UVs so geometry and the shared gem batch are unchanged.
public static class GemSurfaceTexture
{
  public static Texture2D Create(Texture2D source)
  {
    var pixels = new Color[source.Width * source.Height];
    source.GetData(pixels);
    var packed = Bake(pixels, source.Width, source.Height);
    var texture = new Texture2D(source.GraphicsDevice, source.Width, source.Height,
      false, SurfaceFormat.Color);
    texture.SetData(packed);
    return texture;
  }

  public static Color[] Bake(Color[] pixels, int width, int height)
  {
    if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
      throw new ArgumentException("Gem pixels must match the texture dimensions.");
    var result = new Color[pixels.Length];
    float Alpha(int x, int y) => pixels[Math.Clamp(y, 0, height - 1) * width
      + Math.Clamp(x, 0, width - 1)].A / 255f;
    for (int y = 0; y < height; ++y)
      for (int x = 0; x < width; ++x)
      {
        var pixel = pixels[y * width + x];
        if (pixel.A == 0) continue;
        float alpha = pixel.A / 255f;
        // Source pixels are premultiplied. Tint remains independent of coverage.
        float brightness = Math.Clamp(pixel.R / 255f / alpha, 0f, 1f);
        float highlight = Math.Clamp((brightness - 0.5f) * 2f, 0f, 1f) * 0.85f;
        float shadow = Math.Clamp(brightness * 2f, 0f, 1f);
        float left = Alpha(x - 1, y), right = Alpha(x + 1, y);
        float top = Alpha(x, y - 1), bottom = Alpha(x, y + 1);
        // A baked one-texel dark contour replaces the per-fragment Sobel filter.
        float edge = 1f - MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
        float contour = 1f - edge * 0.55f;
        // Two-texel inner outline; linear filtering supplies its soft edge.
        float interior = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
        interior = MathF.Min(interior, MathF.Min(Alpha(x - 2, y), Alpha(x + 2, y)));
        interior = MathF.Min(interior, MathF.Min(Alpha(x, y - 2), Alpha(x, y + 2)));
        result[y * width + x] = new Color(
          shadow * (1f - highlight) * contour * alpha,
          highlight * contour * alpha,
          (1f - interior) * alpha,
          alpha);
      }
    return result;
  }

  // A mipmapped copy of an already packed surface (power-of-two sides). World gems are
  // drawn at a fraction of their texel density (Gem.TexelSize), less when zoomed out, so
  // they need mips to stay smooth. The channels are premultiplied weights, so each level
  // is a plain 2x2 average of the one above.
  public static Texture2D WithMipmaps(Texture2D source)
  {
    int width = source.Width, height = source.Height;
    var pixels = new Color[width * height];
    source.GetData(pixels);
    var texture = new Texture2D(source.GraphicsDevice, width, height, true, SurfaceFormat.Color);
    texture.SetData(0, null, pixels, 0, pixels.Length);
    for (int level = 1; level < texture.LevelCount; ++level)
    {
      int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
      var next = new Color[w * h];
      for (int y = 0; y < h; ++y)
        for (int x = 0; x < w; ++x)
        {
          int x0 = Math.Min(2 * x, width - 1), x1 = Math.Min(2 * x + 1, width - 1);
          int y0 = Math.Min(2 * y, height - 1), y1 = Math.Min(2 * y + 1, height - 1);
          var sum = pixels[y0 * width + x0].ToVector4() + pixels[y0 * width + x1].ToVector4()
            + pixels[y1 * width + x0].ToVector4() + pixels[y1 * width + x1].ToVector4();
          next[y * w + x] = new Color(sum / 4f);
        }
      texture.SetData(level, null, next, 0, next.Length);
      (pixels, width, height) = (next, w, h);
    }
    return texture;
  }
}
