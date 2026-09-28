using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Gum.DataTypes;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Orbit artwork shared by the SpriteBatch HUD and code-created Gum controls.
// Keep this palette separate from HudLayout's legacy upgrade-tree/tooltip colors.
internal static class OrbitSkin
{
  public static readonly Color PanelColor = new(5, 17, 22);
  public static readonly Color PanelBackground = new(5, 17, 22, 245);
  // SpriteBatch uses premultiplied alpha; Gum uses the straight-alpha color above.
  public static readonly Color PanelBackgroundTint = Color.FromNonPremultiplied(5, 17, 22, 245);
  public const int PanelHeaderHeight = 98;
  public static readonly Color StatHeadingColor = new(213, 237, 239);
  public static readonly Color StatValueColor = new(255, 211, 128);
  public static readonly Color BorderColor = new(36, 106, 111);
  public static readonly Color ButtonColor = new(7, 27, 32);
  public static readonly Color ButtonHoverColor = new(17, 57, 61);
  public static readonly Color ButtonBorderColor = new(43, 137, 140);
  public static readonly Color ButtonTextColor = new(150, 244, 239);
  public static readonly Color MutedTextColor = new(115, 167, 170);
  public static readonly Color Accent = new(43, 237, 230);
  public static readonly Color ConfirmAccent = new(79, 227, 139);
  public static readonly Color CommonRarity = new(222, 230, 239);
  public static readonly Color UncommonRarity = new(174, 242, 139);
  public static readonly Color RareRarity = new(139, 204, 255);
  public static readonly Color EpicRarity = new(225, 175, 255);
  public static readonly Color LegendaryRarity = new(255, 218, 139);

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
  {
    if (bounds.Width <= 0 || bounds.Height <= 0) return;
    var texture = GetTexture(batch.GraphicsDevice, asset);
    int sourceEdge = Math.Min(frame, Math.Min(texture.Width, texture.Height) / 2);
    int dx = Math.Min(sourceEdge, bounds.Width / 2), dy = Math.Min(sourceEdge, bounds.Height / 2);
    for (int row = 0; row < 3; row++)
    for (int col = 0; col < 3; col++)
    {
      int sx = col == 0 ? 0 : col == 1 ? sourceEdge : texture.Width - sourceEdge;
      int sy = row == 0 ? 0 : row == 1 ? sourceEdge : texture.Height - sourceEdge;
      int sw = col == 1 ? texture.Width - sourceEdge * 2 : sourceEdge;
      int sh = row == 1 ? texture.Height - sourceEdge * 2 : sourceEdge;
      int x = bounds.X + (col == 0 ? 0 : col == 1 ? dx : bounds.Width - dx);
      int y = bounds.Y + (row == 0 ? 0 : row == 1 ? dy : bounds.Height - dy);
      int w = col == 1 ? bounds.Width - dx * 2 : dx;
      int h = row == 1 ? bounds.Height - dy * 2 : dy;
      if (w > 0 && h > 0)
        batch.Draw(texture, new Rectangle(x, y, w, h), new Rectangle(sx, sy, sw, sh), Color.White * opacity);
    }
  }

  public static void Panel(SpriteBatch batch, Rectangle bounds, bool header = false)
  {
    batch.Draw(Pixel(batch.GraphicsDevice), bounds, PanelBackgroundTint);
    NineSlice(batch, "modal_info_complete", bounds, 8);
    if (header)
      NineSlice(batch, "modal_title_complete", new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Min(PanelHeaderHeight, bounds.Height)), 8);
  }

  public static void Button(SpriteBatch batch, Rectangle bounds, bool active, float pulse = 0,
    bool tab = false, string modalAsset = null, bool confirm = false)
  {
    batch.Draw(Pixel(batch.GraphicsDevice), bounds, PanelColor);
    string asset = modalAsset ?? (tab ? (active ? "tab_active" : "tab_inactive")
      : "button_" + (active ? "active_" : "idle_") + (confirm ? "green" : "blue"));
    NineSlice(batch, asset, bounds, tab ? 8 : 12);
    if (modalAsset != null && active)
      NineSlice(batch, confirm ? "button_active_green" : "button_active_blue", bounds);
    if (pulse > 0) NineSlice(batch, "button_active_blue", bounds, 12, pulse * .5f);
  }

  public static void Progress(SpriteBatch batch, Rectangle track, float fraction)
  {
    batch.Draw(GetTexture(batch.GraphicsDevice, "progress_background"), track, Color.White);
    int width = (int)(track.Width * Math.Clamp(fraction, 0, 1));
    if (width <= 0) return;
    var fill = GetTexture(batch.GraphicsDevice, "slider_foreground");
    batch.Draw(fill, new Rectangle(track.X, track.Center.Y - 10, width, 20),
      new Rectangle(fill.Width / 2, 0, 1, fill.Height), Color.White);
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
    var surface = new NineSliceRuntime
    {
      TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
      CustomFrameTextureCoordinateWidth = 12,
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
