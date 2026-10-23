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

  public static Texture2D Pixel(GraphicsDevice device)
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

  internal static void NineSliceTexture(SpriteBatch batch, Texture2D texture, Rectangle bounds, int srcFrame, int dstFrame, Color color)
    => NineSliceTextureAsymmetric(batch, texture, bounds, srcFrame, srcFrame, srcFrame, srcFrame, dstFrame, dstFrame, dstFrame, dstFrame, color);

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

  internal static (Texture2D Armor, Texture2D Rim) GetDualToneTextures(GraphicsDevice device, string name)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    string armorKey = name + "#armor";
    string rimKey = name + "#rim";
    if (cache.TryGetValue(armorKey, out var armorTex) && cache.TryGetValue(rimKey, out var rimTex))
      return (armorTex, rimTex);

    var baseTex = GetTexture(device, name);
    var src = new Color[baseTex.Width * baseTex.Height];
    baseTex.GetData(src);
    int w = baseTex.Width;
    int h = baseTex.Height;
    var armorPixels = new Color[src.Length];
    var rimPixels = new Color[src.Length];
    for (int y = 0; y < h; y++)
    {
      int rowOffset = y * w;
      for (int x = 0; x < w; x++)
      {
        int i = rowOffset + x;
        byte a = src[i].A;
        if (a <= 3) continue;

        if (a > 40)
        {
          byte rimA = (byte)Math.Clamp((int)MathF.Round((a - 38f) * (255f / 217f)), 0, 255);
          rimPixels[i] = new Color(rimA, rimA, rimA, rimA);

          bool bordersArmor = false;
          int yMin = Math.Max(0, y - 2), yMax = Math.Min(h - 1, y + 2);
          int xMin = Math.Max(0, x - 2), xMax = Math.Min(w - 1, x + 2);
          for (int ny = yMin; ny <= yMax && !bordersArmor; ny++)
          {
            int nRow = ny * w;
            for (int nx = xMin; nx <= xMax; nx++)
            {
              byte na = src[nRow + nx].A;
              if (na >= 16 && na <= 40) { bordersArmor = true; break; }
            }
          }
          if (bordersArmor)
            armorPixels[i] = new Color(255, 255, 255, 255);
        }
        else
        {
          byte armA = (byte)Math.Clamp((int)MathF.Round(a * (255f / 38f)), 0, 255);
          armorPixels[i] = new Color(armA, armA, armA, armA);
        }
      }
    }
    armorTex = new Texture2D(device, w, h);
    armorTex.SetData(armorPixels);
    rimTex = new Texture2D(device, w, h);
    rimTex.SetData(rimPixels);
    cache[armorKey] = armorTex;
    cache[rimKey] = rimTex;
    return (armorTex, rimTex);
  }

  internal static (Texture2D Fill, Texture2D Vignette, Texture2D Glass) GetChamferedBoxTextures(GraphicsDevice device)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    const string fillKey = "synty_hud_box_04#eroded_fill";
    const string vigKey = "synty_hud_box_04#chamfer_vig";
    const string glassKey = "synty_hud_glass_02#chamfer_glass";
    if (cache.TryGetValue(fillKey, out var fillTex)
      && cache.TryGetValue(vigKey, out var vigTex)
      && cache.TryGetValue(glassKey, out var glassTex))
      return (fillTex, vigTex, glassTex);

    var baseBox = GetTexture(device, "synty_hud_box_04");
    var baseGlass = GetTexture(device, "synty_hud_glass_02");
    int w = baseBox.Width;
    int h = baseBox.Height;
    var boxPixels = new Color[w * h];
    var glassSrc = new Color[w * h];
    baseBox.GetData(boxPixels);
    baseGlass.GetData(glassSrc);

    var fillPixels = new Color[w * h];
    var vigPixels = new Color[w * h];
    var glassPixels = new Color[w * h];

    for (int y = 0; y < h; y++)
    {
      int rowOffset = y * w;
      for (int x = 0; x < w; x++)
      {
        int i = rowOffset + x;
        if (boxPixels[i].A < 128) continue;

        fillPixels[i] = Color.White;
        glassPixels[i] = glassSrc[i];

        float dLeft = x - 6f;
        float dRight = (w - 7f) - x;
        float dTop = y - 5f;
        float dBottom = (h - 6f) - y;
        float dTL = (x + y - 42f) * 0.70710678f;
        float dTR = ((w - 1 - x) + y - 42f) * 0.70710678f;
        float dBL = (x + (h - 1 - y) - 42f) * 0.70710678f;
        float dBR = ((w - 1 - x) + (h - 1 - y) - 42f) * 0.70710678f;
        float minDist = MathF.Min(MathF.Min(MathF.Min(dLeft, dRight), MathF.Min(dTop, dBottom)),
          MathF.Min(MathF.Min(dTL, dTR), MathF.Min(dBL, dBR)));
        float v = Math.Clamp(1f - Math.Max(0f, minDist) / 34f, 0f, 1f);
        byte vigA = (byte)Math.Clamp((int)MathF.Round(v * v * 255f), 0f, 255f);
        vigPixels[i] = new Color(vigA, vigA, vigA, vigA);
      }
    }

    fillTex = new Texture2D(device, w, h);
    fillTex.SetData(fillPixels);
    vigTex = new Texture2D(device, w, h);
    vigTex.SetData(vigPixels);
    glassTex = new Texture2D(device, w, h);
    glassTex.SetData(glassPixels);

    cache[fillKey] = fillTex;
    cache[vigKey] = vigTex;
    cache[glassKey] = glassTex;
    return (fillTex, vigTex, glassTex);
  }

  internal static Texture2D GetBox02VignetteTexture(GraphicsDevice device)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    const string vigKey = "synty_hud_box_02#chamfer_vig";
    if (cache.TryGetValue(vigKey, out var vigTex))
      return vigTex;

    var baseBox = GetTexture(device, "synty_hud_box_02");
    int w = baseBox.Width;
    int h = baseBox.Height;
    var boxPixels = new Color[w * h];
    baseBox.GetData(boxPixels);

    var vigPixels = new Color[w * h];
    for (int y = 0; y < h; y++)
    {
      int rowOffset = y * w;
      for (int x = 0; x < w; x++)
      {
        int i = rowOffset + x;
        if (boxPixels[i].A < 128) continue;

        float dLeft = x - 7f;
        float dRight = (w - 8f) - x;
        float dTop = y - 6f;
        float dBottom = (h - 7f) - y;
        float dTR = ((w - 1 - x) + y - 58f) * 0.70710678f;
        float dBL = (x + (h - 1 - y) - 58f) * 0.70710678f;
        float minDist = MathF.Min(MathF.Min(MathF.Min(dLeft, dRight), MathF.Min(dTop, dBottom)),
          MathF.Min(dTR, dBL));
        float v = Math.Clamp(1f - Math.Max(0f, minDist) / 36f, 0f, 1f);
        byte vigA = (byte)Math.Clamp((int)MathF.Round(v * v * 255f), 0f, 255f);
        vigPixels[i] = new Color(vigA, vigA, vigA, vigA);
      }
    }

    vigTex = new Texture2D(device, w, h);
    vigTex.SetData(vigPixels);
    cache[vigKey] = vigTex;
    return vigTex;
  }

  public static Texture2D GetClamshellHoverAuraTexture(GraphicsDevice device)
  {
    var cache = textures.GetValue(device, d => new DeviceTextures(d)).Images;
    const string auraKey = "synty_hud_frame_large_01#hover_sdf";
    if (cache.TryGetValue(auraKey, out var auraTex))
      return auraTex;

    var frameTex = GetTexture(device, "synty_hud_frame_large_01");
    int fw = frameTex.Width;
    int fh = frameTex.Height;
    var framePixels = new Color[fw * fh];
    frameTex.GetData(framePixels);

    const int CW = 764, CH = 964, Pad = 32, W = CW + Pad * 2, H = CH + Pad * 2;
    float s = Math.Min(CW / 780f, CH / 1000f) * 0.50f;
    int dTop = (int)MathF.Round(110 * s);
    int dMid = (int)MathF.Round(550 * s);
    int dBot = (int)MathF.Round(234 * s);
    int remH = Math.Max(0, CH - dTop - dMid - dBot);
    int stretchTop = remH / 2;
    int stretchBot = remH - stretchTop;
    int y1 = dTop;
    int y2 = y1 + stretchTop;
    int y3 = y2 + dMid;
    int y4 = y3 + stretchBot;

    var inside = new bool[W * H];

    for (int dy = 0; dy < CH; dy++)
    {
      int sy;
      if (dy < y1) sy = dy * 110 / Math.Max(1, dTop);
      else if (dy < y2) sy = 110 + (dy - y1) * 60 / Math.Max(1, stretchTop);
      else if (dy < y3) sy = 170 + (dy - y2) * 550 / Math.Max(1, dMid);
      else if (dy < y4) sy = 720 + (dy - y3) * 70 / Math.Max(1, stretchBot);
      else sy = 790 + (dy - y4) * 234 / Math.Max(1, dBot);
      sy = Math.Clamp(sy, 0, fh - 1);

      int rowStart = sy * fw;
      int dstRow = (Pad + dy) * W;
      if (sy <= 970)
      {
        int firstSx = -1;
        for (int sx = 0; sx < 600; sx++)
        {
          if (framePixels[rowStart + sx].A > 24) { firstSx = sx; break; }
        }
        if (firstSx >= 0)
        {
          int firstDx = Math.Clamp((int)MathF.Round(firstSx * s), 0, CW / 2);
          int x0 = Pad + firstDx;
          int x1 = Pad + CW - 1 - firstDx;
          for (int x = x0; x <= x1; x++)
            inside[dstRow + x] = true;
        }
      }
      else
      {
        int firstSx = -1;
        for (int sx = 0; sx < 600; sx++)
        {
          if (framePixels[rowStart + sx].A > 24) { firstSx = sx; break; }
        }
        int minTabDx = (int)MathF.Round(350 * s);
        int tabDx = firstSx >= 0 ? Math.Clamp((int)MathF.Round(firstSx * s), minTabDx, CW / 2) : minTabDx + 4;
        if (sy <= 1018)
        {
          int x0 = Pad + tabDx;
          int x1 = Pad + CW - 1 - tabDx;
          for (int x = x0; x <= x1; x++)
            inside[dstRow + x] = true;
        }
      }
    }

    static int[] ComputeSqDist(bool[] mask, bool targetState)
    {
      const int Inf = 10000;
      var d1 = new int[W * H];
      var d2 = new int[W * H];
      for (int y = 0; y < H; y++)
      {
        int row = y * W;
        int cur = Inf;
        for (int x = 0; x < W; x++)
        {
          cur = mask[row + x] == targetState ? 0 : cur + 1;
          d1[row + x] = cur;
        }
        cur = Inf;
        for (int x = W - 1; x >= 0; x--)
        {
          cur = Math.Min(d1[row + x], cur + 1);
          d1[row + x] = cur * cur;
        }
      }
      for (int y = 0; y < H; y++)
      {
        int yMin = Math.Max(0, y - 32), yMax = Math.Min(H - 1, y + 32);
        int row = y * W;
        for (int x = 0; x < W; x++)
        {
          int best = Inf * Inf;
          for (int ny = yMin; ny <= yMax; ny++)
          {
            int dy = y - ny;
            int cand = d1[ny * W + x] + dy * dy;
            if (cand < best) best = cand;
          }
          d2[row + x] = best;
        }
      }
      return d2;
    }

    var outSq = ComputeSqDist(inside, true);
    var inSq = ComputeSqDist(inside, false);
    var rawSd = new float[W * H];
    for (int i = 0; i < rawSd.Length; i++)
    {
      float outD = MathF.Sqrt(outSq[i]);
      float inD = MathF.Sqrt(inSq[i]);
      rawSd[i] = inside[i] ? -(inD - 0.5f) : (outD - 0.5f);
    }

    // Separable [1, 4, 6, 4, 1]/16 smoothing on the distance field itself removes raster stair-steps
    // on 45-degree chamfers and side notches while keeping the level sets 100% razor-sharp.
    var tempSd = new float[W * H];
    var smoothSd = new float[W * H];
    ReadOnlySpan<float> kernel = [1f / 16f, 4f / 16f, 6f / 16f, 4f / 16f, 1f / 16f];
    for (int y = 0; y < H; y++)
    {
      int row = y * W;
      for (int x = 0; x < W; x++)
      {
        float sum = 0f;
        for (int k = -2; k <= 2; k++)
          sum += rawSd[row + Math.Clamp(x + k, 0, W - 1)] * kernel[k + 2];
        tempSd[row + x] = sum;
      }
    }
    for (int y = 0; y < H; y++)
    {
      int row = y * W;
      for (int x = 0; x < W; x++)
      {
        float sum = 0f;
        for (int k = -2; k <= 2; k++)
          sum += tempSd[Math.Clamp(y + k, 0, H - 1) * W + x] * kernel[k + 2];
        smoothSd[row + x] = sum;
      }
    }

    var pixels = new Color[W * H];
    for (int i = 0; i < pixels.Length; i++)
    {
      float sd = smoothSd[i];
      if (sd > 26.0f || sd < -4.0f) continue;

      float normSd = Math.Clamp((sd + 4.0f) / 30.0f, 0f, 1f);
      // G: Bold, razor-sharp contour frame centered at sd = 5.0 (separated from the card border by a clean 2px gap)
      float primaryRim = Math.Clamp(1f - (MathF.Abs(sd - 5.0f) - 1.35f) / 1.35f, 0f, 1f);
      // B: Crisp parallel outer tech-trace contour at sd = 13.0
      float outerTrace = Math.Clamp(1f - (MathF.Abs(sd - 13.0f) - 0.65f) / 1.30f, 0f, 1f);
      // A: Tight neon edge falloff around the primary contour frame (masked inside the card)
      float tightGlow = sd >= -1.0f
        ? MathF.Exp(-MathF.Abs(sd - 5.0f) * 0.28f) * Math.Clamp((sd + 0.5f) / 2.0f, 0f, 1f)
        : 0f;

      byte r = (byte)Math.Clamp((int)MathF.Round(normSd * 255f), 0, 255);
      byte g = (byte)Math.Clamp((int)MathF.Round(primaryRim * 255f), 0, 255);
      byte b = (byte)Math.Clamp((int)MathF.Round(outerTrace * 255f), 0, 255);
      byte a = (byte)Math.Clamp((int)MathF.Round(tightGlow * 255f), 0, 255);
      pixels[i] = new Color(r, g, b, a);
    }

    auraTex = new Texture2D(device, W, H);
    auraTex.SetData(pixels);
    cache[auraKey] = auraTex;
    return auraTex;
  }

  internal static void NineSliceTextureAsymmetric(SpriteBatch batch, Texture2D texture, Rectangle bounds,
    int sl, int st, int sr, int sb, int dl, int dt, int dr, int db, Color color)
  {
    if (texture == null) return;
    NineSliceTextureSubrect(batch, texture, new Rectangle(0, 0, texture.Width, texture.Height),
      bounds, sl, st, sr, sb, dl, dt, dr, db, color);
  }

  internal static void NineSliceTextureSubrect(SpriteBatch batch, Texture2D texture, Rectangle srcBounds, Rectangle bounds,
    int sl, int st, int sr, int sb, int dl, int dt, int dr, int db, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0 || srcBounds.Width <= 0 || srcBounds.Height <= 0 || texture == null) return;
    sl = Math.Clamp(sl, 0, srcBounds.Width / 2);
    sr = Math.Clamp(sr, 0, srcBounds.Width - sl);
    st = Math.Clamp(st, 0, srcBounds.Height / 2);
    sb = Math.Clamp(sb, 0, srcBounds.Height - st);

    dl = Math.Clamp(dl, 0, bounds.Width / 2);
    dr = Math.Clamp(dr, 0, bounds.Width - dl);
    dt = Math.Clamp(dt, 0, bounds.Height / 2);
    db = Math.Clamp(db, 0, bounds.Height - dt);

    int[] sx = { srcBounds.X, srcBounds.X + sl, srcBounds.Right - sr, srcBounds.Right };
    int[] sy = { srcBounds.Y, srcBounds.Y + st, srcBounds.Bottom - sb, srcBounds.Bottom };
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

  public static void DrawLargeClamshellFrame(SpriteBatch batch, Rectangle bounds, Color armorColor, Color rimColor,
    int gap = 0, bool drawArmor = true, bool drawRim = true)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    int halfW = (bounds.Width - gap) / 2;
    if (halfW <= 0) return;

    float s = Math.Min(bounds.Width / 780f, bounds.Height / 1000f) * 0.50f;
    int dCap = Math.Min(halfW, (int)MathF.Round(570 * s));
    int dTop = (int)MathF.Round(110 * s);
    int dMid = (int)MathF.Round(550 * s);
    int dBot = (int)MathF.Round(234 * s);
    int remH = Math.Max(0, bounds.Height - dTop - dMid - dBot);
    int stretchTop = remH / 2;

    ReadOnlySpan<int> sy = [0, 110, 170, 720, 790, 1024];
    ReadOnlySpan<int> dy =
    [
      bounds.Y,
      bounds.Y + dTop,
      bounds.Y + dTop + stretchTop,
      bounds.Y + dTop + stretchTop + dMid,
      bounds.Bottom - dBot,
      bounds.Bottom
    ];
    ReadOnlySpan<int> lsx = [0, 570, 596];
    ReadOnlySpan<int> ldx = [bounds.X, bounds.X + dCap, bounds.X + halfW];
    ReadOnlySpan<int> rsx = [428, 454, 1024];
    ReadOnlySpan<int> rdx = [bounds.Right - halfW, bounds.Right - dCap, bounds.Right];

    var (leftArmor, leftRim) = GetDualToneTextures(batch.GraphicsDevice, "synty_hud_frame_large_01");
    var (rightArmor, rightRim) = GetDualToneTextures(batch.GraphicsDevice, "synty_hud_frame_large_01_r");

    static void DrawGrid(SpriteBatch sb, Texture2D tex, ReadOnlySpan<int> sx, ReadOnlySpan<int> sy,
      ReadOnlySpan<int> dx, ReadOnlySpan<int> dy, Color col)
    {
      for (int row = 0; row < 5; row++)
      {
        int sh = sy[row + 1] - sy[row];
        int h = dy[row + 1] - dy[row];
        if (sh <= 0 || h <= 0) continue;
        for (int c = 0; c < 2; c++)
        {
          int sw = sx[c + 1] - sx[c];
          int w = dx[c + 1] - dx[c];
          if (sw > 0 && w > 0)
            sb.Draw(tex, new Rectangle(dx[c], dy[row], w, h), new Rectangle(sx[c], sy[row], sw, sh), col);
        }
      }
    }

    if (drawArmor)
    {
      DrawGrid(batch, leftArmor, lsx, sy, ldx, dy, armorColor);
      DrawGrid(batch, rightArmor, rsx, sy, rdx, dy, armorColor);
    }
    if (drawRim)
    {
      DrawGrid(batch, leftRim, lsx, sy, ldx, dy, rimColor);
      DrawGrid(batch, rightRim, rsx, sy, rdx, dy, rimColor);
    }
  }

  public static void DrawSymmetricHorizRail(SpriteBatch batch, string asset, int centerX, int y, int width, int height,
    Color armorColor, Color rimColor, bool flipVertical = false, bool drawArmor = true, bool drawRim = true)
  {
    if (width <= 0 || height <= 0) return;
    var (armorTex, rimTex) = GetDualToneTextures(batch.GraphicsDevice, asset);
    bool isHoriz02 = asset.EndsWith("_02", StringComparison.Ordinal);
    int srcY = 8;
    int srcH = isHoriz02 ? 58 : 112;
    int capSrcW = isHoriz02 ? 290 : 396;
    int flatSrcX = isHoriz02 ? 295 : 398;
    int flatSrcW = isHoriz02 ? 150 : 92;

    float s = (float)height / srcH;
    int capDstW = Math.Min(width / 2, (int)MathF.Round(capSrcW * s));
    int midDstW = Math.Max(0, width - capDstW * 2);
    int leftX = centerX - width / 2;

    var capSrc = new Rectangle(0, srcY, capSrcW, srcH);
    var midSrc = new Rectangle(flatSrcX, srcY, flatSrcW, srcH);
    var leftDst = new Rectangle(leftX, y, capDstW, height);
    var midDst = new Rectangle(leftX + capDstW, y, midDstW, height);
    var rightDst = new Rectangle(leftX + capDstW + midDstW, y, capDstW, height);
    var vFlip = flipVertical ? SpriteEffects.FlipVertically : SpriteEffects.None;

    static void Draw3Slice(SpriteBatch sb, Texture2D tex, Rectangle lDst, Rectangle mDst, Rectangle rDst,
      Rectangle cSrc, Rectangle mSrc, SpriteEffects vf, Color col)
    {
      sb.Draw(tex, lDst, cSrc, col, 0f, Vector2.Zero, vf, 0f);
      if (mDst.Width > 0)
        sb.Draw(tex, mDst, mSrc, col, 0f, Vector2.Zero, vf, 0f);
      sb.Draw(tex, rDst, cSrc, col, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally | vf, 0f);
    }

    if (drawArmor)
      Draw3Slice(batch, armorTex, leftDst, midDst, rightDst, capSrc, midSrc, vFlip, armorColor);
    if (drawRim)
      Draw3Slice(batch, rimTex, leftDst, midDst, rightDst, capSrc, midSrc, vFlip, rimColor);
  }

  public static void DrawWingedDetail07(SpriteBatch batch, int centerX, int y, int width, int height, Color color,
    bool flipVertical = false, int gap = 0)
  {
    if (width <= 0 || height <= 0) return;
    var tex = GetTexture(batch.GraphicsDevice, "synty_hud_frame_detail_07");
    const int srcY = 6;
    const int srcH = 246;
    const int capSrcW = 315;
    const int flatSrcX = 315;
    const int flatSrcW = 410;

    float s = (float)height / srcH;
    int capDstW = Math.Min(width / 2, (int)MathF.Round(capSrcW * s));
    int midDstW = Math.Max(0, width - capDstW * 2);
    int leftX = centerX - width / 2;

    var capSrc = new Rectangle(0, srcY, capSrcW, srcH);
    var midSrc = new Rectangle(flatSrcX, srcY, flatSrcW, srcH);
    var leftDst = new Rectangle(leftX, y, capDstW, height);
    var rightDst = new Rectangle(leftX + capDstW + midDstW, y, capDstW, height);
    var vFlip = flipVertical ? SpriteEffects.FlipVertically : SpriteEffects.None;

    batch.Draw(tex, leftDst, capSrc, color, 0f, Vector2.Zero, vFlip, 0f);
    if (midDstW > 0 && gap <= 0)
    {
      var midDst = new Rectangle(leftX + capDstW, y, midDstW, height);
      batch.Draw(tex, midDst, midSrc, color, 0f, Vector2.Zero, vFlip, 0f);
    }
    else if (midDstW > 0 && gap > 0)
    {
      int sideSpan = (midDstW - gap) / 2;
      if (sideSpan > 0)
      {
        batch.Draw(tex, new Rectangle(leftX + capDstW, y, sideSpan, height), midSrc, color, 0f, Vector2.Zero, vFlip, 0f);
        batch.Draw(tex, new Rectangle(centerX + gap / 2, y, sideSpan, height), midSrc, color, 0f, Vector2.Zero, vFlip, 0f);
      }
    }
    batch.Draw(tex, rightDst, capSrc, color, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally | vFlip, 0f);
  }

  public static void DrawChamferedBoxBase(SpriteBatch batch, Rectangle bounds, int corner, Color fill, float vignetteAlpha = 0.45f)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    const int srcCorner = 44;
    var (fillTex, vigTex, _) = GetChamferedBoxTextures(batch.GraphicsDevice);
    NineSliceTextureAsymmetric(batch, fillTex, bounds, srcCorner, srcCorner, srcCorner, srcCorner,
      corner, corner, corner, corner, fill);
    if (vignetteAlpha > 0f)
    {
      NineSliceTextureAsymmetric(batch, vigTex, bounds, srcCorner, srcCorner, srcCorner, srcCorner,
        corner, corner, corner, corner, Color.Black * vignetteAlpha);
    }
  }

  public static void DrawChamferedBoxGlass(SpriteBatch batch, Rectangle bounds, int corner, Color color)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    const int srcCorner = 44;
    var (_, _, glassTex) = GetChamferedBoxTextures(batch.GraphicsDevice);
    NineSliceTextureAsymmetric(batch, glassTex, bounds, srcCorner, srcCorner, srcCorner, srcCorner,
      corner, corner, corner, corner, color);
  }

  public static void DrawChamferedBox(SpriteBatch batch, Rectangle bounds, int corner, Color fill, Color borderColor,
    float glassAlpha = 0f, float vignetteAlpha = 0.45f)
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    const int srcCorner = 44;
    DrawChamferedBoxBase(batch, bounds, corner, fill, vignetteAlpha);
    if (glassAlpha > 0f)
      DrawChamferedBoxGlass(batch, bounds, corner, Color.Lerp(borderColor, Color.White, 0.35f) * glassAlpha);
    NineSliceScaled(batch, "synty_hud_frame_06", bounds, srcCorner, corner, borderColor);
  }

  public static void DrawRotatedCentered(SpriteBatch batch, string asset, Vector2 center, float size, float rotation, Color color)
  {
    if (size <= 0f) return;
    var tex = GetTexture(batch.GraphicsDevice, asset);
    var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
    var scale = new Vector2(size / tex.Width, size / tex.Height);
    batch.Draw(tex, center, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
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

    var (_, vig04, _) = GetChamferedBoxTextures(batch.GraphicsDevice);
    NineSliceTexture(batch, vig04, bounds, 54, corner, Color.Black * 0.52f);
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
      NineSliceTexture(batch, GetBox02VignetteTexture(batch.GraphicsDevice), bounds, 54, corner, Color.Black * 0.45f);
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
    var (_, vig04, _) = GetChamferedBoxTextures(batch.GraphicsDevice);
    NineSliceTexture(batch, vig04, bounds, 54, corner, Color.Black * 0.52f);

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
    NineSliceTexture(batch, GetBox02VignetteTexture(batch.GraphicsDevice), bounds, 54, corner, Color.Black * 0.45f);

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
    var (_, vig04, _) = GetChamferedBoxTextures(batch.GraphicsDevice);
    NineSliceTexture(batch, vig04, bounds, 54, corner, Color.Black * 0.58f);

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
    if (bounds.Width > 28 && bounds.Height > 24)
    {
      var interior = new Rectangle(bounds.X + 12, bounds.Y + 12, bounds.Width - 24, bounds.Height - 24);
      DrawTiledSlot(batch, GuiSpriteSlot.ScanlinesOverlay, interior, 256, 256, 0, 0, border * (active ? 0.085f : 0.045f), border);
      if (bounds.Height >= 160 && bounds.Width >= 160)
        DrawTiledSlot(batch, GuiSpriteSlot.HexagonsOverlay, interior, 384, 384, 0, 0, border * (active ? 0.075f : 0.04f), border);
    }

    NineSliceTexture(batch, GetBox02VignetteTexture(batch.GraphicsDevice), bounds, 54, btnCorner - 1,
      Color.Black * (active ? 0.42f : 0.52f));
    NineSliceScaled(batch, "synty_hud_glass_04", bounds, 54, btnCorner - 1,
      Color.Lerp(border, Color.White, 0.35f) * (active ? 0.16f : 0.08f));

    if (active)
    {
      var glow = GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
      batch.Draw(glow, new Rectangle(bounds.X + 12, bounds.Y + 6, Math.Max(1, bounds.Width - 24), Math.Max(1, bounds.Height - 12)),
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
