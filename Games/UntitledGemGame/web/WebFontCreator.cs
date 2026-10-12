using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using RenderingLibrary.Graphics.Fonts;
using StbTrueTypeSharp;

namespace UntitledGemGame.Web;

// Pure managed browser alternative to the desktop KernSmith/FreeType adapter.
internal sealed class WebBitmapFontCreator(GraphicsDevice device) : IInMemoryFontCreator
{
  private static readonly Dictionary<string, byte[]> fonts = new();
  public static void RegisterFont(string name, byte[] bytes) => fonts[name] = bytes;

  public unsafe BitmapFont TryCreateFont(BmfcSave save)
  {
    if (!fonts.TryGetValue(save.FontName, out var bytes)) return null;
    const int pageSize = 2048;
    var pages = new List<Texture2D>();
    var pixels = new Color[pageSize * pageSize];
    int x = 1, y = 1, rowHeight = 0;
    var characters = new StringBuilder();
    void FlushPage()
    {
      var texture = new Texture2D(device, pageSize, pageSize);
      texture.SetData(pixels);
      pages.Add(texture);
      pixels = new Color[pageSize * pageSize];
      x = y = 1;
      rowHeight = 0;
    }
    fixed (byte* data = bytes)
    {
      var font = new StbTrueType.stbtt_fontinfo();
      if (StbTrueType.stbtt_InitFont(font, data, StbTrueType.stbtt_GetFontOffsetForIndex(data, 0)) == 0)
        throw new InvalidOperationException($"Could not load browser font {save.FontName}.");
      float scale = StbTrueType.stbtt_ScaleForPixelHeight(font, save.FontSize);
      int ascent = 0, descent = 0, gap = 0;
      StbTrueType.stbtt_GetFontVMetrics(font, &ascent, &descent, &gap);
      int baseline = (int)Math.Ceiling(ascent * scale);
      int lineHeight = (int)Math.Ceiling((ascent - descent + gap) * scale);
      var codepoints = Codepoints(save.Ranges).ToArray();
      foreach (int codepoint in codepoints)
      {
        int width = 0, height = 0, left = 0, top = 0, advance = 0, bearing = 0;
        StbTrueType.stbtt_GetCodepointHMetrics(font, codepoint, &advance, &bearing);
        byte* bitmap = StbTrueType.stbtt_GetCodepointBitmap(font, scale, scale, codepoint, &width, &height, &left, &top);
        try
        {
          int outline = save.OutlineThickness;
          int packedWidth = width + outline * 2, packedHeight = height + outline * 2;
          if (packedWidth + 2 >= pageSize || packedHeight + 2 >= pageSize)
            throw new InvalidOperationException("Browser font glyph exceeds the atlas size.");
          if (x + packedWidth + 1 >= pageSize) { x = 1; y += rowHeight + 1; rowHeight = 0; }
          if (y + packedHeight + 1 >= pageSize) FlushPage();
          for (int py = 0; py < packedHeight; py++)
            for (int px = 0; px < packedWidth; px++)
            {
              int gx = px - outline, gy = py - outline;
              byte fill = gx >= 0 && gx < width && gy >= 0 && gy < height ? bitmap[gy * width + gx] : (byte)0;
              byte alpha = fill;
              for (int oy = -outline; oy <= outline; oy++)
                for (int ox = -outline; ox <= outline; ox++)
                  if (gx + ox >= 0 && gx + ox < width && gy + oy >= 0 && gy + oy < height)
                    alpha = Math.Max(alpha, bitmap[(gy + oy) * width + gx + ox]);
              byte color = alpha == 0 ? (byte)0 : (byte)(255 * fill / alpha);
              pixels[(y + py) * pageSize + x + px] = new Color(color, color, color, alpha);
            }
          characters.AppendLine(FormattableString.Invariant($"char id={codepoint} x={x} y={y} width={packedWidth} height={packedHeight} xoffset={left - outline} yoffset={baseline + top - outline} xadvance={(int)Math.Round(advance * scale)} page={pages.Count} chnl=15"));
          x += packedWidth + 1;
          rowHeight = Math.Max(rowHeight, packedHeight);
        }
        finally { StbTrueType.stbtt_FreeBitmap(bitmap, null); }
      }
      FlushPage();
      string description = FormattableString.Invariant($"info face=\"{save.FontName}\" size={save.FontSize}\ncommon lineHeight={lineHeight} base={baseline} scaleW={pageSize} scaleH={pageSize} pages={pages.Count} packed=0\n")
        + string.Concat(Enumerable.Range(0, pages.Count).Select(i => $"page id={i} file=\"page{i}.png\"\n"))
        + $"chars count={codepoints.Length}\n" + characters;
      return new BitmapFont(pages.ToArray(), description);
    }
  }

  private static IEnumerable<int> Codepoints(string ranges)
  {
    foreach (string range in ranges.Split(','))
    {
      var ends = range.Split('-');
      int first = int.Parse(ends[0], CultureInfo.InvariantCulture);
      int last = ends.Length > 1 ? int.Parse(ends[1], CultureInfo.InvariantCulture) : first;
      for (int codepoint = first; codepoint <= last; codepoint++) yield return codepoint;
    }
  }
}
