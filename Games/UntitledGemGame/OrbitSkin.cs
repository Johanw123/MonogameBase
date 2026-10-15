using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Gum.DataTypes;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Synty InterfaceSciFiSoldierHUD artwork shared by the SpriteBatch HUD and code-created Gum controls.
// Menus, trees and tooltips use the same tactical sci-fi palette and multi-layered frame sprites.
internal static class OrbitSkin
{
  public static readonly Color PanelColor = new(9, 17, 26);
  public static readonly Color PanelBackground = new(9, 17, 26, 245);
  // SpriteBatch uses premultiplied alpha; Gum uses the straight-alpha color above.
  public static readonly Color PanelBackgroundTint = Color.FromNonPremultiplied(9, 17, 26, 245);
  public const int PanelHeaderHeight = 98;
  // Neutral text carries information; cyan/teal marks interaction, amber/orange marks value,
  // violet identifies prestige, and tactical green/coral communicate success/restriction.
  public static readonly Color StatHeadingColor = new(226, 238, 244);
  public static readonly Color StatValueColor = new(255, 190, 82);
  public static readonly Color BorderColor = new(64, 150, 178);
  public static readonly Color ButtonColor = new(14, 28, 40);
  public static readonly Color ButtonHoverColor = new(26, 56, 76);
  public static readonly Color ButtonBorderColor = new(76, 204, 228);
  public static readonly Color ButtonTextColor = StatHeadingColor;
  public static readonly Color MutedTextColor = new(162, 185, 198);
  public static readonly Color DisabledTextColor = new(132, 154, 166);
  public static readonly Color SurfaceColor = new(16, 31, 44);
  public static readonly Color LockedSurfaceColor = new(10, 20, 30);
  public static readonly Color Accent = new(82, 228, 242);
  public static readonly Color UpgradeAccent = StatValueColor;
  public static readonly Color AbilityAccent = new(118, 204, 248);
  public static readonly Color ShipyardAccent = new(244, 168, 96);
  public static readonly Color PrestigeAccent = new(198, 162, 246);
  public static readonly Color SignalAccent = PrestigeAccent;
  public static readonly Color LockedTextColor = new(240, 146, 134);
  public static readonly Color ConfirmAccent = new(104, 230, 152);
  public static readonly Color CommonRarity = StatHeadingColor;
  public static readonly Color UncommonRarity = new(164, 224, 148);
  public static readonly Color RareRarity = AbilityAccent;
  public static readonly Color EpicRarity = PrestigeAccent;
  public static readonly Color LegendaryRarity = StatValueColor;
  public static readonly Color MythicRarity = new(242, 142, 184);

  private sealed class DeviceTextures
  {
    public readonly Dictionary<string, Texture2D> Images = new();
    public DeviceTextures(GraphicsDevice device)
    {
      device.Disposing += (_, _) =>
      {
        foreach (var texture in Images.Values) texture.Dispose();
        Images.Clear();
      };
    }
  }
  private static readonly ConditionalWeakTable<GraphicsDevice, DeviceTextures> textures = new();

  // Loads every skin image up front: a panel drawn for the first time otherwise decoded its
  // PNG mid-frame (the talent tree after an extraction, the first core fracture modal).
  public static void Preload(GraphicsDevice device)
  {
#if !KNI_WEB
    var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Content", "Menu");
    if (!System.IO.Directory.Exists(folder)) return;
    foreach (var file in System.IO.Directory.EnumerateFiles(folder, "*.png"))
      GetTexture(device, System.IO.Path.GetFileNameWithoutExtension(file));
#endif
  }

  private static Texture2D GetTexture(GraphicsDevice device, string name)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    if (cache.TryGetValue(name, out var texture)) return texture;
    using var stream = TitleContainer.OpenStream("Content/Menu/" + name + ".png");
    texture = Texture2D.FromStream(device, stream);
    // Gum consumes the original straight-alpha PNGs. SpriteBatch's normal HUD
    // blend is premultiplied, so retain a separate converted texture for it.
    var pixels = new Color[texture.Width * texture.Height];
    texture.GetData(pixels);
    for (int i = 0; i < pixels.Length; i++)
      pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
    texture.SetData(pixels);
    cache.Add(name, texture);
    return texture;
  }

  private static Texture2D Pixel(GraphicsDevice device)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    if (cache.TryGetValue("$white", out var texture)) return texture;
    texture = new Texture2D(device, 1, 1);
    texture.SetData(new[] { Color.White });
    cache.Add("$white", texture);
    return texture;
  }

  // All draw helpers run inside the caller's existing SpriteBatch Begin/End.
  public static void NineSlice(SpriteBatch batch, string asset, Rectangle bounds, int frame = 12, float opacity = 1)
    => NineSliceScaled(batch, asset, bounds, frame, frame, Color.White * opacity);

  public static void NineSliceScaled(SpriteBatch batch, string asset, Rectangle bounds, int srcFrame, int dstFrame, Color color)
    => NineSliceAsymmetric(batch, asset, bounds, srcFrame, srcFrame, srcFrame, srcFrame, dstFrame, dstFrame, dstFrame, dstFrame, color);

  // Slices a sprite with asymmetric source borders (left, top, right, bottom) using a single
  // pixels-per-unit multiplier (like Unity's Image.Type.Sliced + pixelsPerUnitMultiplier),
  // preserving the exact 1:1 native aspect ratio of every corner.
  public static void NineSlicePpu(SpriteBatch batch, string asset, Rectangle bounds,
    int sl, int st, int sr, int sb, float ppu, Color color)
  {
    ppu = Math.Max(0.1f, ppu);
    int dl = (int)MathF.Round(sl / ppu);
    int dt = (int)MathF.Round(st / ppu);
    int dr = (int)MathF.Round(sr / ppu);
    int db = (int)MathF.Round(sb / ppu);
    NineSliceAsymmetric(batch, asset, bounds, sl, st, sr, sb, dl, dt, dr, db, color);
  }

  public static void NineSliceAsymmetric(SpriteBatch batch, string asset, Rectangle bounds,
    int sl, int st, int sr, int sb, int dl, int dt, int dr, int db, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    var texture = GetTexture(batch.GraphicsDevice, asset);
    sl = Math.Clamp(sl, 0, texture.Width / 2);
    sr = Math.Clamp(sr, 0, texture.Width - sl);
    st = Math.Clamp(st, 0, texture.Height / 2);
    sb = Math.Clamp(sb, 0, texture.Height - st);

    dl = Math.Clamp(dl, 0, bounds.Width / 2);
    dr = Math.Clamp(dr, 0, bounds.Width - dl);
    dt = Math.Clamp(dt, 0, bounds.Height / 2);
    db = Math.Clamp(db, 0, bounds.Height - dt);

    int[] sx = { 0, sl, texture.Width - sr, texture.Width };
    int[] sy = { 0, st, texture.Height - sb, texture.Height };
    int[] dx = { bounds.X, bounds.X + dl, bounds.Right - dr, bounds.Right };
    int[] dy = { bounds.Y, bounds.Y + dt, bounds.Bottom - db, bounds.Bottom };

    for (int row = 0; row < 3; row++)
    for (int col = 0; col < 3; col++)
    {
      int sw = sx[col + 1] - sx[col];
      int sh = sy[row + 1] - sy[row];
      int w = dx[col + 1] - dx[col];
      int h = dy[row + 1] - dy[row];
      if (sw > 0 && sh > 0 && w > 0 && h > 0)
        batch.Draw(texture, new Rectangle(dx[col], dy[row], w, h), new Rectangle(sx[col], sy[row], sw, sh), color);
    }
  }

  // Tiles a texture across `bounds` at exact 1:1 pixel ratio (tileW x tileH) with a scrolling UV offset,
  // clipped cleanly to `bounds` without requiring SamplerState.LinearWrap.
  public static void DrawTiled(SpriteBatch batch, string asset, Rectangle bounds,
    int tileW, int tileH, int scrollX, int scrollY, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0 || tileW <= 0 || tileH <= 0) return;
    var tex = GetTexture(batch.GraphicsDevice, asset);
    int ox = ((scrollX % tileW) + tileW) % tileW;
    int oy = ((scrollY % tileH) + tileH) % tileH;

    for (int y = bounds.Top - oy; y < bounds.Bottom; y += tileH)
    {
      int drawTop = Math.Max(y, bounds.Top);
      int drawBottom = Math.Min(y + tileH, bounds.Bottom);
      int drawH = drawBottom - drawTop;
      if (drawH <= 0) continue;
      int srcY = (int)MathF.Round((drawTop - y) * (tex.Height / (float)tileH));
      int srcH = Math.Max(1, (int)MathF.Round(drawH * (tex.Height / (float)tileH)));
      srcY = Math.Clamp(srcY, 0, tex.Height - 1);
      srcH = Math.Min(srcH, tex.Height - srcY);

      for (int x = bounds.Left - ox; x < bounds.Right; x += tileW)
      {
        int drawLeft = Math.Max(x, bounds.Left);
        int drawRight = Math.Min(x + tileW, bounds.Right);
        int drawW = drawRight - drawLeft;
        if (drawW <= 0) continue;
        int srcX = (int)MathF.Round((drawLeft - x) * (tex.Width / (float)tileW));
        int srcW = Math.Max(1, (int)MathF.Round(drawW * (tex.Width / (float)tileW)));
        srcX = Math.Clamp(srcX, 0, tex.Width - 1);
        srcW = Math.Min(srcW, tex.Width - srcX);

        batch.Draw(tex, new Rectangle(drawLeft, drawTop, drawW, drawH),
          new Rectangle(srcX, srcY, srcW, srcH), color);
      }
    }
  }

  // Renders the Unity sample's `AssetDemo_SciFiSoldier_Background_01.prefab` animated backdrop:
  // radial navy vignette + UV-scrolling hexagonal grid + scanlines + stepped outer `Frame_Blue` HUD frame.
  public static void DrawAnimatedBackdrop(SpriteBatch batch, Rectangle bounds, float timeSeconds = 0f,
    bool drawVignette = true, float opacity = 1f)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0 || opacity <= 0f) return;

    if (drawVignette)
    {
      var bg = GetTexture(batch.GraphicsDevice, "synty_example_bg");
      batch.Draw(bg, bounds, Color.White * (0.94f * opacity));
    }

    // SampleScrollUV.cs: speed = (-0.03, -0.06), size = (512, 512)
    int scrollX = (int)MathF.Round(timeSeconds * 16f);
    int scrollY = (int)MathF.Round(timeSeconds * 32f);
    DrawTiled(batch, "synty_hud_hexagons", bounds, 512, 512, scrollX, scrollY,
      new Color(14, 92, 138) * (0.28f * opacity));

    // Top and bottom scanline bands
    int scanBandH = Math.Min(bounds.Height / 2, 560);
    int scanScrollY = (int)MathF.Round(timeSeconds * -12f);
    DrawTiled(batch, "synty_hud_scanlines",
      new Rectangle(bounds.X, bounds.Y, bounds.Width, scanBandH),
      512, 512, 0, scanScrollY, new Color(40, 78, 122) * (0.32f * opacity));
    DrawTiled(batch, "synty_hud_scanlines",
      new Rectangle(bounds.X, bounds.Bottom - scanBandH, bounds.Width, scanBandH),
      512, 512, 0, -scanScrollY, new Color(40, 78, 122) * (0.32f * opacity));

    // Outer stepped blue HUD frame (`Frame_L` and `Frame_R` from `AssetDemo_SciFiSoldier_Background_01.prefab`)
    if (bounds.Width >= 600 && bounds.Height >= 300)
    {
      float scale = bounds.Width >= 2560 ? 1f : 0.5f;
      float ppu = 0.9f / scale;
      int padX = (int)(16 * scale);
      int padY = (int)(14 * scale);
      int halfW = bounds.Width / 2 - padX;
      int frameH = bounds.Height - padY * 2;
      Color frameColor = new Color(0, 128, 238) * (0.48f * opacity);

      var leftRect = new Rectangle(bounds.X + padX, bounds.Y + padY, halfW, frameH);
      var rightRect = new Rectangle(bounds.Center.X, bounds.Y + padY, halfW, frameH);
      NineSlicePpu(batch, "synty_hud_frame_large_01", leftRect, 300, 590, 706, 400, ppu, frameColor);
      NineSlicePpu(batch, "synty_hud_frame_large_01_r", rightRect, 706, 590, 300, 400, ppu, frameColor);

      // Top and bottom center triangles (`Frame_Triangle_T` & `Frame_Triangle_B`)
      var tri = GetTexture(batch.GraphicsDevice, "synty_hud_triangle_02");
      int triSz = (int)(40 * scale);
      int triInset = (int)(14 * scale);
      batch.Draw(tri, new Rectangle(bounds.Center.X - triSz / 2, bounds.Y + triInset, triSz, triSz),
        Color.White * (0.68f * opacity));
      batch.Draw(tri, new Rectangle(bounds.Center.X - triSz / 2, bounds.Bottom - triInset - triSz, triSz, triSz),
        null, Color.White * (0.68f * opacity), 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);

      // Bottom-left status segments (`Segment_00..03` from `AssetDemo_SciFiSoldier_Background_01.prefab`)
      int segW = (int)(60 * scale);
      int segH = (int)(20 * scale);
      int segGap = (int)(54 * scale);
      int segBaseX = bounds.X + (int)(86 * scale);
      int segY = bounds.Bottom - (int)(58 * scale);
      int activeSeg = ((int)(timeSeconds * 1.5f)) & 3;
      for (int i = 0; i < 4; i++)
      {
        bool lit = i == activeSeg;
        Color segCol = lit
          ? Color.White * (0.75f * opacity)
          : new Color(0, 128, 238) * (0.35f * opacity);
        NineSlicePpu(batch, "synty_hud_bar_angled_01",
          new Rectangle(segBaseX + i * segGap, segY, segW, segH),
          80, 0, 80, 0, 5f / scale, segCol);
      }
    }
  }

  public static void Panel(SpriteBatch batch, Rectangle bounds, bool header = false)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    NineSliceScaled(batch, "modal_info_complete", bounds, 24, corner, Color.White);

    // Tile un-stretched 1:1 hexagons and scanlines inside the panel interior, inset past the corner chamfers
    int inset = Math.Max(16, corner);
    var interior = new Rectangle(bounds.X + inset, bounds.Y + inset,
      Math.Max(0, bounds.Width - inset * 2), Math.Max(0, bounds.Height - inset * 2));
    if (interior.Width > 32 && interior.Height > 32)
    {
      DrawTiled(batch, "synty_hud_hexagons", interior, 384, 384, 0, 0, new Color(18, 96, 148) * 0.16f);
      DrawTiled(batch, "synty_hud_scanlines", interior, 384, 384, 0, 0, new Color(36, 118, 178) * 0.14f);
    }

    if (header)
    {
      // Short banners (such as the 132px top title bar) ARE the header itself; do not bisect them at y=98.
      int headerH = bounds.Height <= PanelHeaderHeight * 2 ? bounds.Height : PanelHeaderHeight;
      var headerRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, headerH);
      int hCorner = Math.Clamp(headerH / 3, 14, 24);
      NineSliceScaled(batch, "modal_title_complete", headerRect, 24, hCorner, Color.White);
      if (bounds.Width >= 240 && headerH < bounds.Height)
      {
        var lineTex = GetTexture(batch.GraphicsDevice, "synty_hud_line_01");
        int lineW = Math.Min(bounds.Width - 64, 720);
        batch.Draw(lineTex, new Rectangle(bounds.Center.X - lineW / 2, headerRect.Bottom - 4, lineW, 4), Accent * 0.90f);
      }
    }
  }

  public static void FlatPanel(SpriteBatch batch, Rectangle bounds, Color fill, Color border, int thickness)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    if (minDim < 18)
    {
      thickness = Math.Clamp(thickness, 1, Math.Max(1, minDim / 2));
      var pixel = Pixel(batch.GraphicsDevice);
      batch.Draw(pixel, bounds, fill);
      batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), border);
      batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), border);
      batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + thickness, thickness, bounds.Height - thickness * 2), border);
      batch.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Y + thickness, thickness, bounds.Height - thickness * 2), border);
      return;
    }

    int corner = Math.Clamp(minDim / 4, 8, 14);
    NineSliceScaled(batch, "synty_hud_box_02", bounds, 54, corner, fill);
    NineSliceScaled(batch, "synty_hud_frame_02", bounds, 54, corner, border * 0.92f);
    if (thickness >= 2 && minDim >= 56)
      NineSliceScaled(batch, "synty_hud_frame_02", bounds, 54, corner, Color.Lerp(border, Color.White, 0.25f) * (thickness >= 3 ? 0.55f : 0.35f));
  }

  public static void Tooltip(SpriteBatch batch, Rectangle bounds)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    var shadowRect = new Rectangle(bounds.X + 8, bounds.Y + 10, Math.Max(1, bounds.Width - 8), Math.Max(1, bounds.Height - 8));
    NineSliceScaled(batch, "synty_hud_box_02", shadowRect, 54, corner, Color.Black * 0.68f);
    NineSliceScaled(batch, "modal_info_complete", bounds, 24, corner, Color.White);
    int inset = Math.Max(16, corner);
    if (bounds.Width > inset * 2 && bounds.Height > inset * 2)
      DrawTiled(batch, "synty_hud_scanlines", new Rectangle(bounds.X + inset, bounds.Y + inset, bounds.Width - inset * 2, bounds.Height - inset * 2),
        384, 384, 0, 0, new Color(36, 118, 178) * 0.14f);
  }

  public static void Button(SpriteBatch batch, Rectangle bounds, bool active, float pulse = 0,
    bool tab = false, string modalAsset = null, bool confirm = false)
    => HudButton(batch, bounds, confirm ? ConfirmAccent : Accent, active, active, pulse, modalAsset, confirm);

  public static void HudButton(SpriteBatch batch, Rectangle bounds, Color accent, bool selected, bool hovered,
    float pulse = 0, string modalAsset = null, bool confirm = false)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    bool active = selected || hovered;
    if (modalAsset != null)
    {
      int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 8, 14);
      Color baseFill = confirm
        ? (active ? new Color(16, 58, 46, 244) : new Color(8, 32, 28, 238))
        : (active ? new Color(18, 46, 72, 242) : new Color(6, 20, 38, 236));
      NineSliceScaled(batch, "synty_hud_box_02", bounds, 54, corner, baseFill);
      NineSlice(batch, modalAsset, bounds, 12);
      return;
    }

    Color fill = confirm
      ? (active ? new Color(16, 58, 46, 244) : new Color(8, 32, 28, 238))
      : selected ? Color.Lerp(new Color(18, 46, 72, 246), accent, 0.16f)
      : hovered ? new Color(18, 46, 72, 242)
      : new Color(6, 20, 38, 236);
    Color border = confirm
      ? (active ? ConfirmAccent : ConfirmAccent * 0.8f)
      : selected ? accent
      : hovered ? Color.Lerp(accent, Color.White, 0.12f)
      : BorderColor * 0.85f;

    if (pulse > 0f)
    {
      fill = Color.Lerp(fill, border, pulse * 0.25f);
      border = Color.Lerp(border, Color.White, pulse * 0.5f);
    }

    FlatPanel(batch, bounds, fill, border, 1);
  }

  public static void Progress(SpriteBatch batch, Rectangle track, float fraction)
  {
    ProgressTrack(batch, track);
    ProgressFill(batch, track, 0f, fraction, Color.White);
  }

  public static void ProgressTrack(SpriteBatch batch, Rectangle track)
  {
    if (track.Width <= 0 || track.Height <= 0) return;
    int h = Math.Clamp(Math.Max(track.Height + 6, 14), 12, 20);
    var drawRect = new Rectangle(track.X, track.Center.Y - h / 2, track.Width, h);
    NineSlice(batch, "progress_background", drawRect, 6);
  }

  // Fills the track between two shares of it, for bars made of several parts.
  public static void ProgressFill(SpriteBatch batch, Rectangle track, float from, float to, Color tint)
  {
    int left = (int)(track.Width * Math.Clamp(from, 0, 1));
    int right = (int)(track.Width * Math.Clamp(to, 0, 1));
    if (right <= left) return;
    var fill = GetTexture(batch.GraphicsDevice, "slider_foreground");
    int h = Math.Clamp(Math.Max(track.Height + 6, 14), 12, 20);
    batch.Draw(fill, new Rectangle(track.X + left, track.Center.Y - h / 2, right - left, h),
      new Rectangle(fill.Width / 2, 0, 1, fill.Height), tint);
  }

  public static void Slider(SpriteBatch batch, Rectangle track, float fraction)
  {
    Progress(batch, track, fraction);
    int x = track.Left + (int)(track.Width * Math.Clamp(fraction, 0, 1));
    batch.Draw(GetTexture(batch.GraphicsDevice, "slider_knob"),
      new Rectangle(x - 24, track.Center.Y - 24, 48, 48), Color.White);
  }

  public static NineSliceRuntime GumSurface(string asset, float width, float height)
  {
    float frame = asset == "modal_info_complete" ? 24f
      : asset == "modal_title_complete" ? 24f
      : asset.StartsWith("radio_") ? 24f
      : 12f;
    var surface = new NineSliceRuntime
    {
      TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
      CustomFrameTextureCoordinateWidth = frame,
      WidthUnits = DimensionUnitType.Absolute, HeightUnits = DimensionUnitType.Absolute,
      Width = width, Height = height, HasEvents = false,
      XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
      YUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
      XOrigin = RenderingLibrary.Graphics.HorizontalAlignment.Left,
      YOrigin = RenderingLibrary.Graphics.VerticalAlignment.Top
    };
    surface.SetProperty("SourceFile", "../Menu/" + asset + ".png");
    return surface;
  }
}
