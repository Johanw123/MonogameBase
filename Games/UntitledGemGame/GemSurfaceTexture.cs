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

  private static Texture2D redGemIcon;
  private static Texture2D coreShardIcon;
  private static Texture2D prestigeOrbIcon;

  // Tints the first 64x64 variant of the packed gem surface with the red crystal color
  // using GemShader.fx's formula (color = tint * R + G, alpha = A) for HUD and tooltip icons.
  public static Texture2D RedGemIcon(Texture2D surface = null)
  {
    surface ??= TextureCache.GemSurface?.Value
      ?? AsyncContent.AssetManager.Load<Texture2D>("Textures/Gems/plasma_orb.png");
    if (redGemIcon == null || redGemIcon.IsDisposed || redGemIcon.GraphicsDevice != surface.GraphicsDevice)
      redGemIcon = CreateTintedTile(surface, GemQualityTable.GetColor(Entities.GemTypes.Red));
    return redGemIcon;
  }

  public static Texture2D CoreShardIcon()
  {
    var source = AsyncContent.AssetManager.Load<Texture2D>(CoreShards.IconPath);
    if (coreShardIcon == null || coreShardIcon.IsDisposed || coreShardIcon.GraphicsDevice != source.GraphicsDevice)
      coreShardIcon = WithMipmaps(source);
    return coreShardIcon;
  }

  public static Texture2D PrestigeOrbIcon()
  {
    var source = AsyncContent.AssetManager.Load<Texture2D>(CoreShards.PrestigeIconPath);
    if (prestigeOrbIcon == null || prestigeOrbIcon.IsDisposed || prestigeOrbIcon.GraphicsDevice != source.GraphicsDevice)
      prestigeOrbIcon = WithMipmaps(source);
    return prestigeOrbIcon;
  }

  public static Texture2D CreateTintedTile(Texture2D surface, Color tint, int tileSize = 64)
  {
    int w = Math.Min(tileSize, surface.Width), h = Math.Min(tileSize, surface.Height);
    var pixels = new Color[w * h];
    surface.GetData(0, new Rectangle(0, 0, w, h), pixels, 0, pixels.Length);
    float tr = tint.R / 255f, tg = tint.G / 255f, tb = tint.B / 255f;
    for (int i = 0; i < pixels.Length; ++i)
    {
      var p = pixels[i];
      if (p.A == 0)
      {
        pixels[i] = Color.Transparent;
        continue;
      }
      float rw = p.R / 255f, gh = p.G / 255f, a = p.A / 255f;
      pixels[i] = new Color(
        Math.Clamp(tr * rw + gh, 0f, 1f),
        Math.Clamp(tg * rw + gh, 0f, 1f),
        Math.Clamp(tb * rw + gh, 0f, 1f),
        a);
    }
    using var flat = new Texture2D(surface.GraphicsDevice, w, h, false, SurfaceFormat.Color);
    flat.SetData(pixels);
    return WithMipmaps(flat);
  }
}
