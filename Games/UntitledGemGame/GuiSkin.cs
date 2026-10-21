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
public static partial class GuiSkin
{
  public static Color PanelColor => GuiPalette.Base(new Color(9, 17, 26));
  public static Color PanelBackground => GuiPalette.Base(new Color(9, 17, 26, 245));
  // SpriteBatch uses premultiplied alpha; Gum uses the straight-alpha color above.
  public static Color PanelBackgroundTint => Color.FromNonPremultiplied(PanelBackground.ToVector4());
  public const int PanelHeaderHeight = 98;
  // Neutral text carries information; cyan/teal marks interaction, amber/orange marks value,
  // violet identifies prestige, and tactical green/coral communicate success/restriction.
  public static Color StatHeadingColor => GuiPalette.Get(GuiColor.Text);
  public static Color StatValueColor => GuiPalette.Get(GuiColor.Upgrade);
  public static Color BorderColor => GuiPalette.Surface(new Color(64, 150, 178));
  public static Color ButtonColor => GuiPalette.Base(new Color(14, 28, 40));
  public static Color ButtonHoverColor => GuiPalette.Base(new Color(26, 56, 76));
  public static Color ButtonBorderColor => GuiPalette.Surface(new Color(76, 204, 228));
  public static Color ButtonTextColor => GuiPalette.Get(GuiColor.Text);
  public static Color MutedTextColor => GuiPalette.Get(GuiColor.MutedText);
  public static Color DisabledTextColor => GuiPalette.Get(GuiColor.DisabledText);
  public static Color SurfaceColor => GuiPalette.Base(new Color(16, 31, 44));
  public static Color LockedSurfaceColor => GuiPalette.Base(new Color(10, 20, 30));
  public static Color Accent => GuiPalette.Get(GuiColor.Accent);
  public static Color UpgradeAccent => GuiPalette.Get(GuiColor.Upgrade);
  public static Color PanelAccent => UpgradeAccent;
  public static Color AbilityAccent => GuiPalette.Get(GuiColor.Ability);
  public static Color PrestigeAccent => GuiPalette.Get(GuiColor.Prestige);
  public static Color LockedTextColor => GuiPalette.Get(GuiColor.Restricted);
  public static Color ConfirmAccent => GuiPalette.Get(GuiColor.Confirm);
  public static Color CommonRarity => GuiPalette.Get(GuiColor.Text);
  public static Color UncommonRarity => GuiPalette.Get(GuiColor.Uncommon);
  public static Color RareRarity => GuiPalette.Get(GuiColor.Ability);
  public static Color EpicRarity => GuiPalette.Get(GuiColor.Prestige);
  public static Color LegendaryRarity => GuiPalette.Get(GuiColor.Upgrade);
  public static Color MythicRarity => GuiPalette.Get(GuiColor.Mythic);

  private sealed class DeviceTextures
  {
    public readonly Dictionary<string, Texture2D> Images = new();
    public readonly Dictionary<string, (Color[] Pixels, int Revision, bool Confirm)> RecoloredImages = new();
    public DeviceTextures(GraphicsDevice device)
    {
      device.Disposing += (_, _) =>
      {
        foreach (var texture in Images.Values) texture.Dispose();
        Images.Clear();
        RecoloredImages.Clear();
      };
    }
  }
  private static readonly ConditionalWeakTable<GraphicsDevice, DeviceTextures> textures = new();

  // Loads every skin image up front: a panel drawn for the first time otherwise decoded its
  // PNG mid-frame (the talent tree after an extraction, the first core fracture modal).
  public static void Preload(GraphicsDevice device)
  {
    EnsureInitialized();
#if !KNI_WEB
    var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Content", "Menu");
    if (!System.IO.Directory.Exists(folder)) return;
    foreach (var file in System.IO.Directory.EnumerateFiles(folder, "*.png"))
      GetTexture(device, System.IO.Path.GetFileNameWithoutExtension(file));
#endif
  }

  internal static Texture2D GetTexture(GraphicsDevice device, string name)
  {
    var deviceTextures = textures.GetValue(device, d => new DeviceTextures(d));
    var cache = deviceTextures.Images;
    if (cache.TryGetValue(name, out var texture))
    {
      if (deviceTextures.RecoloredImages.TryGetValue(name, out var rec) && rec.Revision != GuiPalette.BaseRevision)
      {
        ApplyTexturePalette(texture, rec.Pixels, rec.Confirm);
        deviceTextures.RecoloredImages[name] = (rec.Pixels, GuiPalette.BaseRevision, rec.Confirm);
      }
      return texture;
    }
    using var stream = TitleContainer.OpenStream("Content/Menu/" + name + ".png");
    texture = Texture2D.FromStream(device, stream);
    // Gum consumes the original straight-alpha PNGs. SpriteBatch's normal HUD
    // blend is premultiplied, so retain a separate converted texture for it.
    var pixels = new Color[texture.Width * texture.Height];
    texture.GetData(pixels);
    bool confirmArtwork = name.EndsWith("_green", StringComparison.Ordinal);
    bool blueArtwork = name is "hud_bar_background" or "modal_info_complete" or "modal_title_complete"
      or "option_background" or "sub_heading2" or "progress_background" or "slider_foreground"
      or "slider_background" or "slider_knob" or "toggle_off_background" or "toggle_on_background"
      or "toggle_off_knob" or "toggle_on_knob" or "radio_unchecked" or "radio_checked_background"
      || name.EndsWith("_blue", StringComparison.Ordinal);
    if (blueArtwork || confirmArtwork)
    {
      deviceTextures.RecoloredImages[name] = (pixels, GuiPalette.BaseRevision, confirmArtwork);
      ApplyTexturePalette(texture, pixels, confirmArtwork);
    }
    else
    {
      for (int i = 0; i < pixels.Length; i++)
        pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
      texture.SetData(pixels);
    }
    cache.Add(name, texture);
    return texture;
  }

  private static void ApplyTexturePalette(Texture2D texture, Color[] original, bool confirm)
  {
    var pixels = new Color[original.Length];
    for (int i = 0; i < pixels.Length; i++)
    {
      var color = confirm ? GuiPalette.ConfirmSurface(original[i]) : GuiPalette.Surface(original[i]);
      pixels[i] = Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
    }
    texture.SetData(pixels);
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
    NineSliceTextureAsymmetric(batch, texture, bounds, sl, st, sr, sb, dl, dt, dr, db, color);
  }

  internal static void NineSliceTextureAsymmetric(SpriteBatch batch, Texture2D texture, Rectangle bounds,
    int sl, int st, int sr, int sb, int dl, int dt, int dr, int db, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0 || texture == null) return;
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
    DrawTiledTexture(batch, tex, bounds, tileW, tileH, scrollX, scrollY, color);
  }

  internal static void DrawTiledTexture(SpriteBatch batch, Texture2D tex, Rectangle bounds,
    int tileW, int tileH, int scrollX, int scrollY, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0 || tileW <= 0 || tileH <= 0 || tex == null) return;
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

  // Semi-transparent HUD top bar matching the bottom HudBar's glassy dark-navy aesthetic,
  // with painted corner brackets, subtle scanlines, and a crisp horizontal tech rail.
  public static void TopBar(SpriteBatch batch, Rectangle bounds)
    => TopBar(batch, bounds, Accent);

  public static void TopBar(SpriteBatch batch, Rectangle bounds, Color accent)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    NineSliceSlot(batch, GuiSpriteSlot.TopBarBackground, bounds, 24, corner, Color.White, PanelBackgroundTint, accent);

    if (bounds.Width > 48 && bounds.Height > 28)
    {
      var interior = new Rectangle(bounds.X + 18, bounds.Y + 10, bounds.Width - 36, bounds.Height - 20);
      DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, interior, 256, 256, 0, 0,
        GuiPalette.Surface(new Color(68, 165, 220)) * 0.055f, accent);
    }

    NineSliceSlot(batch, GuiSpriteSlot.PanelFrameOverlay, bounds, 48, corner * 2, Color.White * 0.92f, PanelBackgroundTint, accent);

    if (bounds.Width >= 320)
    {
      int lineW = Math.Min(bounds.Width - 160, 1120);
      TechDivider(batch, new Rectangle(bounds.Center.X - lineW / 2, bounds.Bottom - 3, lineW, 2), accent * 0.78f);
      var horizTex = GetTexture(batch.GraphicsDevice, "synty_hud_frame_horiz_02");
      int hW = Math.Min(260, lineW / 3);
      batch.Draw(horizTex, new Rectangle(bounds.Center.X - hW / 2, bounds.Bottom - 9, hW, 8), accent * 0.55f);
    }
  }

  // Centered octagonal glass title plate inside the TopBar using Box_04 + inset parallel bezel + Frame_08 corner armor + Frame_06.
  public static void TitleBadge(SpriteBatch batch, Rectangle bounds, Color accent)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    int corner = Math.Clamp(minDim / 4, 10, 14);
    var fill = GuiPalette.Base(new Color(6, 22, 40, 228));
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBackground, bounds, 54, corner, fill, fill, accent);

    batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * 0.52f);
    NineSliceScaled(batch, "synty_hud_glass_02", bounds, 54, corner, Color.Lerp(accent, Color.White, 0.35f) * 0.14f);

    if (minDim >= 44)
    {
      var innerRect = new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10);
      NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, innerRect, 54, Math.Max(6, corner - 3), accent * 0.42f, fill, accent);
    }

    NineSliceSlot(batch, GuiSpriteSlot.TechCardCornerArmor, bounds, 108, corner * 2, Color.Lerp(accent, Color.White, 0.25f) * 0.85f, fill, accent);
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, bounds, 54, corner, Color.Lerp(accent, Color.White, 0.20f) * 0.92f, fill, accent);
  }

  public static void Panel(SpriteBatch batch, Rectangle bounds, bool header = false)
    => Panel(batch, bounds, header, Accent);

  public static void Panel(SpriteBatch batch, Rectangle bounds, bool header, Color accent)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;

    // Short full-width banners (such as the 132px top title bar) use the semi-transparent TopBar style
    // so the top and bottom HUD bars match seamlessly.
    if (header && bounds.Height <= PanelHeaderHeight * 2)
    {
      TopBar(batch, bounds, accent);
      return;
    }

    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    NineSliceSlot(batch, GuiSpriteSlot.PanelBackground, bounds, 24, corner, Color.White, PanelBackgroundTint, accent);

    int topInset = header ? PanelHeaderHeight + 6 : 14;
    if (bounds.Width > 48 && bounds.Height > topInset + 24)
    {
      var bodyInterior = new Rectangle(bounds.X + 14, bounds.Y + topInset, bounds.Width - 28, bounds.Height - topInset - 14);
      DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, bodyInterior, 256, 256, 0, 0,
        GuiPalette.Surface(new Color(64, 155, 210)) * 0.045f, accent);
    }

    if (header)
    {
      int headerH = PanelHeaderHeight;
      var headerRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, headerH);
      int hCorner = Math.Clamp(headerH / 3, 14, 24);
      NineSliceSlot(batch, GuiSpriteSlot.PanelHeader, headerRect, 24, hCorner, Color.White, PanelBackgroundTint, accent);

      var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
      int glowW = Math.Min(bounds.Width - 32, 640);
      batch.Draw(glow, new Rectangle(bounds.Center.X - glowW / 2, bounds.Y + 4, Math.Max(1, glowW), headerH - 8),
        accent * 0.14f);

      if (bounds.Width >= 240 && headerH < bounds.Height)
      {
        int lineW = Math.Min(bounds.Width - 96, 680);
        TechDivider(batch, new Rectangle(bounds.Center.X - lineW / 2, headerRect.Bottom - 3, lineW, 2), accent * 0.88f);
      }
    }

    NineSliceSlot(batch, GuiSpriteSlot.PanelFrameOverlay, bounds, 48, corner * 2, Color.White, PanelBackgroundTint, accent);
  }

  public static void HudBar(SpriteBatch batch, Rectangle bounds)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    NineSliceSlot(batch, GuiSpriteSlot.HudBarBackground, bounds, 24, corner, Color.White, PanelBackgroundTint, Accent);
    // Inset the texture so it cannot spill across the chamfers. Sparse, stationary
    // hex cells and scanlines add surface detail while the original glass alpha stays intact.
    var interior = new Rectangle(bounds.X + 18, bounds.Y + 12, Math.Max(1, bounds.Width - 36), Math.Max(1, bounds.Height - 24));
    DrawTiledSlot(batch, GuiSpriteSlot.HexagonsOverlay, interior, 384, 384, 0, 0,
      GuiPalette.Surface(new Color(76, 140, 170)) * 0.085f, Accent);
    DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, interior, 256, 256, 0, 0,
      GuiPalette.Surface(new Color(64, 160, 215)) * 0.045f, Accent);

    NineSliceSlot(batch, GuiSpriteSlot.PanelFrameOverlay, bounds, 48, corner * 2, Color.White * 0.92f, PanelBackgroundTint, Accent);
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

    int corner = Math.Clamp(minDim / 4, 8, 12);
    NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBackground, bounds, 54, corner, fill, fill, border);

    if (minDim >= 28)
    {
      batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * 0.45f);
      NineSliceScaled(batch, "synty_hud_glass_04", bounds, 54, corner, Color.Lerp(border, Color.White, 0.35f) * 0.14f);
    }

    if (minDim >= 36)
    {
      var innerRect = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
      NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, innerRect, 54, Math.Max(6, corner - 2), border * 0.36f, fill, border);
    }

    if (minDim >= 52 && bounds.Width >= 90)
    {
      int fCorner = (int)MathF.Round(28f * corner / 11f);
      NineSliceSlot(batch, GuiSpriteSlot.HudButtonCornersIdle, bounds, 28, fCorner, Color.White * 0.78f, fill, border);
    }

    NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, bounds, 54, corner, border * 0.94f, fill, border);
    if (thickness >= 2 && minDim >= 48)
      NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, bounds, 54, corner, Color.Lerp(border, Color.White, 0.28f) * (thickness >= 3 ? 0.55f : 0.35f), fill, border);
  }

  // Octagonal 4-corner chamfered Synty technical card (`Box_04` + inset parallel bezel + `Frame_08` corner armor + `Frame_06`)
  // with recessed vignette, subtle scanlines, stepped corner tech stripes, and optional radial hex-patch watermark.
  public static void TechCard(SpriteBatch batch, Rectangle bounds, Color fill, Color accent,
    bool hovered = false, bool selected = false, bool cornerGraphic = true, bool hexWatermark = false)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    if (minDim < 28)
    {
      FlatPanel(batch, bounds, fill, accent, selected ? 2 : 1);
      return;
    }

    int corner = Math.Clamp(minDim / 4, 10, 15);
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBackground, bounds, 54, corner, fill, fill, accent);
    batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * 0.52f);

    if (bounds.Width >= 100 && bounds.Height >= 64)
    {
      var scanRect = new Rectangle(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20);
      DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, scanRect, 256, 256, 0, 0, accent * 0.05f, accent);
    }

    NineSliceScaled(batch, "synty_hud_glass_02", bounds, 54, corner,
      Color.Lerp(accent, Color.White, 0.3f) * (selected ? 0.15f : hovered ? 0.11f : 0.06f));

    if (minDim >= 56)
    {
      var innerRect = new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10);
      NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, innerRect, 54, Math.Max(6, corner - 3),
        accent * (selected ? 0.45f : hovered ? 0.35f : 0.25f), fill, accent);
    }

    if (selected || hovered || cornerGraphic)
    {
      var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
      float glowAlpha = selected ? 0.22f : hovered ? 0.16f : 0.09f;
      int glowH = Math.Min(bounds.Height - 8, Math.Max(48, bounds.Height * 2 / 3));
      batch.Draw(glow, new Rectangle(bounds.X + 8, bounds.Y + 4, Math.Max(1, bounds.Width - 16), Math.Max(1, glowH)),
        accent * glowAlpha);
    }

    if (hexWatermark && bounds.Width >= 140 && bounds.Height >= 90)
    {
      var hexPatch = GetTexture(batch.GraphicsDevice, "synty_fx_hexpatch");
      int inset = corner + 4;
      int patchSize = Math.Clamp(Math.Min(bounds.Width, bounds.Height) - inset * 2, 48, 164);
      batch.Draw(hexPatch, new Rectangle(bounds.Right - patchSize - inset, bounds.Bottom - patchSize - inset, patchSize, patchSize),
        accent * (selected ? 0.16f : 0.10f));
    }

    Color rimColor = selected ? Color.Lerp(accent, Color.White, 0.22f)
      : hovered ? Color.Lerp(accent, Color.White, 0.12f)
      : accent * 0.88f;
    NineSliceSlot(batch, GuiSpriteSlot.TechCardCornerArmor, bounds, 108, corner * 2,
      rimColor * (selected ? 0.92f : hovered ? 0.78f : 0.55f), fill, rimColor);
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, bounds, 54, corner, rimColor, fill, rimColor);
  }

  // Horizontal tier band for ShipSystems and PrestigeTalents with a soft radial glow in the left
  // header compartment, parallel inner bezel, and corner brackets.
  public static void TierBand(SpriteBatch batch, Rectangle bounds, int headerWidth, Color fill, Color accent, bool locked = false)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    int corner = Math.Clamp(minDim / 4, 10, 12);
    NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBackground, bounds, 54, corner, fill, fill, accent);
    batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * 0.45f);

    if (headerWidth > 40)
    {
      int glowW = Math.Min(bounds.Width - 16, headerWidth + 32);
      var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
      batch.Draw(glow, new Rectangle(bounds.X + 6, bounds.Y + 6, glowW, Math.Max(1, bounds.Height - 12)),
        accent * (locked ? 0.08f : 0.18f));
    }

    if (minDim >= 44)
    {
      var innerRect = new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10);
      NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, innerRect, 54, Math.Max(6, corner - 2),
        accent * (locked ? 0.22f : 0.38f), fill, accent);
    }

    int fCorner = (int)MathF.Round(28f * corner / 11f);
    NineSliceSlot(batch, GuiSpriteSlot.HudButtonCornersIdle, bounds, 28, fCorner, Color.White * (locked ? 0.5f : 0.85f), fill, accent);
    NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, bounds, 54, corner, accent * (locked ? 0.52f : 0.88f), fill, accent);

    if (headerWidth > 40 && headerWidth < bounds.Width - 40 && bounds.Height >= 40)
    {
      var pixel = Pixel(batch.GraphicsDevice);
      int railH = bounds.Height - 28;
      int railX = bounds.X + headerWidth;
      batch.Draw(pixel, new Rectangle(railX, bounds.Center.Y - railH / 2, 1, railH),
        accent * (locked ? 0.22f : 0.42f));
    }
  }

  // Recessed octagonal blueprint/scanner viewport with subtle tiled registration crosses, radial glow, and corner armor.
  public static void BlueprintBay(SpriteBatch batch, Rectangle bounds, Color fill, Color accent)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    int corner = Math.Clamp(minDim / 4, 12, 16);
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBackground, bounds, 54, corner, fill, fill, accent);
    batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * 0.58f);

    var inner = new Rectangle(bounds.X + 14, bounds.Y + 14, Math.Max(1, bounds.Width - 28), Math.Max(1, bounds.Height - 28));
    DrawTiled(batch, "synty_hud_crosses", inner, 160, 160, 0, 0, accent * 0.12f);

    var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
    int glowW = Math.Min(bounds.Width - 16, Math.Max(bounds.Height, bounds.Width * 2 / 3));
    int glowH = Math.Max(1, bounds.Height - 16);
    batch.Draw(glow, new Rectangle(bounds.Center.X - glowW / 2, bounds.Center.Y - glowH / 2, glowW, glowH),
      accent * 0.14f);

    if (minDim >= 64)
    {
      var innerRect = new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10);
      NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, innerRect, 54, Math.Max(6, corner - 3), accent * 0.35f, fill, accent);
    }

    NineSliceSlot(batch, GuiSpriteSlot.TechCardCornerArmor, bounds, 108, corner * 2, accent * 0.72f, fill, accent);
    NineSliceSlot(batch, GuiSpriteSlot.TechCardBorder, bounds, 54, corner, accent * 0.90f, fill, accent);
  }

  public static void DrawScannerReticle(SpriteBatch batch, Point center, int diameter, Color accent, float opacity = 1f)
  {
    if (diameter <= 16) return;
    var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
    batch.Draw(glow, new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter),
      accent * (0.12f * opacity));

    var outerRing = GetTexture(batch.GraphicsDevice, "synty_hud_ring_large_02");
    batch.Draw(outerRing, new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter),
      accent * (0.24f * opacity));

    int innerD = diameter * 68 / 100;
    var innerRing = GetTexture(batch.GraphicsDevice, "synty_hud_ring_large_01");
    batch.Draw(innerRing, new Rectangle(center.X - innerD / 2, center.Y - innerD / 2, innerD, innerD),
      Color.Lerp(accent, Color.White, 0.35f) * (0.22f * opacity));
  }

  public static void DrawHexSocket(SpriteBatch batch, Rectangle bounds, Color accent, bool active = false, bool filled = true)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    if (filled)
    {
      var underlay = GetTexture(batch.GraphicsDevice, "synty_hud_hex_underlay");
      Color fill = active
        ? Color.Lerp(GuiPalette.Base(new Color(14, 42, 68, 235)), accent, 0.28f)
        : GuiPalette.Base(new Color(8, 24, 42, 215));
      batch.Draw(underlay, bounds, fill);
    }
    var ring = GetTexture(batch.GraphicsDevice, "synty_hud_ring_hex_med");
    Color rim = active ? Color.Lerp(accent, Color.White, 0.30f) : accent * 0.78f;
    batch.Draw(ring, bounds, rim);
  }

  public static void DrawHexPip(SpriteBatch batch, Rectangle bounds, Color accent, bool active)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    var pip = GetTexture(batch.GraphicsDevice, active ? "synty_hud_pip_hex_active" : "synty_hud_pip_hex_inactive");
    Color tint = active ? Color.Lerp(accent, Color.White, 0.22f) : accent * 0.45f;
    batch.Draw(pip, bounds, tint);
  }

  public static void TechDivider(SpriteBatch batch, Rectangle bounds, Color accent)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int h = Math.Clamp(bounds.Height, 2, 3);
    var lineTex = GetTexture(batch.GraphicsDevice, "synty_hud_line_02");
    batch.Draw(lineTex, new Rectangle(bounds.X, bounds.Center.Y - h / 2, bounds.Width, h), accent);
  }

  public static void Tooltip(SpriteBatch batch, Rectangle bounds)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 14, 24);
    var shadowRect = new Rectangle(bounds.X + 8, bounds.Y + 10, Math.Max(1, bounds.Width - 8), Math.Max(1, bounds.Height - 8));
    NineSliceScaled(batch, "synty_hud_box_02", shadowRect, 54, corner, Color.Black * 0.68f);
    NineSliceSlot(batch, GuiSpriteSlot.PanelBackground, bounds, 24, corner, Color.White, PanelBackgroundTint, Accent);
    NineSliceSlot(batch, GuiSpriteSlot.PanelFrameOverlay, bounds, 48, corner * 2, Color.White * 0.9f, PanelBackgroundTint, Accent);
  }

  public static void Button(SpriteBatch batch, Rectangle bounds, bool active, float pulse = 0,
    bool tab = false, string modalAsset = null, bool confirm = false)
    => HudButton(batch, bounds, confirm ? ConfirmAccent : Accent, active, active, pulse, modalAsset, confirm);

  public static void HudButton(SpriteBatch batch, Rectangle bounds, Color accent, bool selected, bool hovered,
    float pulse = 0, string modalAsset = null, bool confirm = false)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    bool active = selected || hovered;
    int minDim = Math.Min(bounds.Width, bounds.Height);
    if (modalAsset != null)
    {
      int corner = Math.Clamp(minDim / 4, 8, 12);
      Color baseFill = confirm
        ? (active ? GuiPalette.Base(new Color(16, 58, 46, 244)) : GuiPalette.Base(new Color(8, 32, 28, 238)))
        : (active ? GuiPalette.Base(new Color(18, 46, 72, 242)) : GuiPalette.Base(new Color(6, 20, 38, 236)));
      NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBackground, bounds, 54, corner, baseFill, baseFill, accent);
      NineSlice(batch, modalAsset, bounds, 12);
      if (minDim >= 44)
      {
        int fCorner = (int)MathF.Round(28f * corner / 11f);
        NineSliceSlot(batch, active ? GuiSpriteSlot.HudButtonCornersActive : GuiSpriteSlot.HudButtonCornersIdle,
          bounds, 28, fCorner, Color.White, baseFill, accent);
      }
      return;
    }

    Color fill = confirm
      ? (active ? GuiPalette.Base(new Color(16, 58, 46, 244)) : GuiPalette.Base(new Color(8, 32, 28, 238)))
      : selected ? Color.Lerp(GuiPalette.Base(new Color(18, 46, 72, 246)), accent, 0.08f)
      : hovered ? GuiPalette.Base(new Color(18, 46, 72, 242))
      : GuiPalette.Base(new Color(6, 20, 38, 236));
    Color border = confirm
      ? (active ? ConfirmAccent : ConfirmAccent * 0.8f)
      : selected ? accent
      : hovered ? Color.Lerp(accent, Color.White, 0.14f)
      : BorderColor * 0.85f;

    if (pulse > 0f)
    {
      fill = Color.Lerp(fill, border, pulse * 0.25f);
      border = Color.Lerp(border, Color.White, pulse * 0.5f);
    }

    if (minDim < 32)
    {
      FlatPanel(batch, bounds, fill, border, selected ? 2 : 1);
      return;
    }

    int btnCorner = Math.Clamp(minDim / 4, 10, 12);
    var btnSlot = confirm
      ? (active ? GuiSpriteSlot.HudButtonConfirmActive : GuiSpriteSlot.HudButtonConfirmIdle)
      : (active ? GuiSpriteSlot.HudButtonActive : GuiSpriteSlot.HudButtonIdle);
    NineSliceSlot(batch, btnSlot, bounds, 12, btnCorner, Color.White, fill, border);

    // 1:1 native pixel scanlines and vignette so tall cards/buttons never stretch scanlines vertically
    if (bounds.Width > 24 && bounds.Height > 20)
    {
      var interior = new Rectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height - 16);
      DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, interior, 256, 256, 0, 0, border * (active ? 0.085f : 0.045f), border);
      if (bounds.Height >= 160 && bounds.Width >= 160)
        DrawTiledSlot(batch, GuiSpriteSlot.HexagonsOverlay, interior, 384, 384, 0, 0, border * (active ? 0.075f : 0.04f), border);
    }

    batch.Draw(GetTexture(batch.GraphicsDevice, "synty_fx_vignette"), bounds, Color.Black * (active ? 0.42f : 0.52f));
    NineSliceScaled(batch, "synty_hud_glass_04", bounds, 54, btnCorner - 1,
      Color.Lerp(border, Color.White, 0.35f) * (active ? 0.16f : 0.08f));

    if (active)
    {
      var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
      batch.Draw(glow, new Rectangle(bounds.X + 8, bounds.Y + 4, Math.Max(1, bounds.Width - 16), Math.Max(1, bounds.Height - 8)),
        border * (selected ? 0.18f : 0.11f));
    }

    if (minDim >= 44 && bounds.Width >= 80)
    {
      int fCorner = (int)MathF.Round(28f * btnCorner / 12f);
      NineSliceSlot(batch, active ? GuiSpriteSlot.HudButtonCornersActive : GuiSpriteSlot.HudButtonCornersIdle,
        bounds, 28, fCorner, Color.White, fill, border);
    }

    NineSliceSlot(batch, GuiSpriteSlot.FlatPanelBorder, bounds, 54, btnCorner - 1, border * 0.95f, fill, border);
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
    NineSliceSlot(batch, GuiSpriteSlot.SliderTrack, drawRect, 6, 6, Color.White, PanelBackgroundTint, Accent);
  }

  // Fills the track between two shares of it, for bars made of several parts.
  public static void ProgressFill(SpriteBatch batch, Rectangle track, float from, float to, Color tint)
  {
    int left = (int)(track.Width * Math.Clamp(from, 0, 1));
    int right = (int)(track.Width * Math.Clamp(to, 0, 1));
    if (right <= left) return;
    if (IsBuiltIn(GuiSpriteSlot.SliderFill))
    {
      var fill = GetTexture(batch.GraphicsDevice, "slider_foreground");
      int h = Math.Clamp(Math.Max(track.Height + 6, 14), 12, 20);
      batch.Draw(fill, new Rectangle(track.X + left, track.Center.Y - h / 2, right - left, h),
        new Rectangle(fill.Width / 2, 0, 1, fill.Height), tint);
      return;
    }
    var cue = GetSlotCue(GuiSpriteSlot.SliderFill);
    if (cue.Hidden || cue.Opacity <= 0.001f) return;
    var tex = GetSlotTexture(batch.GraphicsDevice, GuiSpriteSlot.SliderFill);
    int barH = Math.Clamp(Math.Max(track.Height + 6, 14), 12, 20);
    Color drawColor = tint * cue.Opacity;
    batch.Draw(tex, new Rectangle(track.X + left, track.Center.Y - barH / 2, right - left, barH),
      new Rectangle(tex.Width / 2, 0, 1, tex.Height), drawColor);
  }

  public static void Slider(SpriteBatch batch, Rectangle track, float fraction)
  {
    Progress(batch, track, fraction);
    int x = track.Left + (int)(track.Width * Math.Clamp(fraction, 0, 1));
    if (IsBuiltIn(GuiSpriteSlot.SliderKnob))
    {
      batch.Draw(GetTexture(batch.GraphicsDevice, "slider_knob"),
        new Rectangle(x - 24, track.Center.Y - 24, 48, 48), Color.White);
      return;
    }
    var cue = GetSlotCue(GuiSpriteSlot.SliderKnob);
    if (cue.Hidden || cue.Opacity <= 0.001f) return;
    var knobTex = GetSlotTexture(batch.GraphicsDevice, GuiSpriteSlot.SliderKnob);
    int size = Math.Max(8, (int)MathF.Round(48 * cue.CornerScale));
    Color drawColor = ResolveBatchColor(GuiSpriteSlot.SliderKnob, Color.White, PanelBackgroundTint, Accent);
    batch.Draw(knobTex, new Rectangle(x - size / 2, track.Center.Y - size / 2, size, size), drawColor);
  }

  public static SpriteRuntime GumDecoration(string asset, Rectangle bounds, Color accent, byte opacity)
  {
    var sprite = new SpriteRuntime
    {
      Name = "GuiDecoration" + asset,
      WidthUnits = DimensionUnitType.Absolute, HeightUnits = DimensionUnitType.Absolute,
      X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
      Color = new Color(accent.R, accent.G, accent.B, opacity)
    };
    sprite.SetProperty("SourceFile", "../Menu/" + asset + ".png");
    return sprite;
  }

  public static ContainerRuntime GumLaunchEmblem()
  {
    var emblem = new ContainerRuntime { Name = "LaunchEmblem", Width = 440, Height = 440, HasEvents = false };
    emblem.Children.Add(GumDecoration("synty_hud_ring_calibration", new Rectangle(0, 0, 440, 440), AbilityAccent, 20));
    emblem.Children.Add(GumDecoration("synty_map_ship", new Rectangle(105, 105, 230, 230), AbilityAccent, 17));
    return emblem;
  }

  public static NineSliceRuntime GumSurface(string asset, float width, float height)
  {
    float frame = asset == "modal_info_complete" ? 24f
      : asset == "modal_title_complete" ? 24f
      : asset.StartsWith("radio_") ? 24f
      : 12f;
    var surface = new NineSliceRuntime
    {
      Name = "GumSurface:" + asset,
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
