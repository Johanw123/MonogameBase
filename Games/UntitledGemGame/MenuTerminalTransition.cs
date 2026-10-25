using System;
using System.Collections.Generic;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary;

namespace UntitledGemGame;

// Menu pages change like pages of a ship terminal: the old page powers down, then the next
// one is scanned in (MenuTransition.fx). On the title screen the pages share the left
// column, whose backdrop stays lit underneath; starting a game powers the whole title
// screen down. The pause menu scans in when opened and changes page the same way.
//
// The page is drawn into a target of its own for the shader. Binding that target clears
// the HUD target, so Prepare runs before anything reaches the HUD and Composite later
// draws the result wherever the page belongs.
internal static class MenuTerminalTransition
{
  private const float OutSeconds = 0.17f;
  private const float InSeconds = 0.4f;
  private const float ExitSeconds = 0.6f;

  private static GraphicalUiElement outgoing, incoming;
  private static Action showIncoming;
  private static float elapsed, outSeconds;
  private static bool exiting, prepared;
  private static Vector4 region;
  private static RenderTarget2D target;
  private static SpriteBatch batch;
  private static readonly List<GraphicalUiElement> hidden = new();

  internal static bool Active => outgoing != null || incoming != null;
  internal static bool Exiting => exiting;
  internal static bool ExitFinished => exiting && elapsed >= outSeconds;

  // `from` must be showing; it powers down, then `show` replaces it with `to`. Without a
  // `from`, `to` must be showing already and is only scanned in.
  internal static void Begin(GraphicalUiElement from, GraphicalUiElement to, Action show)
  {
    outgoing = from;
    incoming = to;
    showIncoming = show;
    outSeconds = OutSeconds;
    elapsed = from == null ? OutSeconds : 0;
    exiting = false;
  }

  // Powers `page` down for good; it stays dark until the caller replaces it and cancels.
  internal static void BeginExit(GraphicalUiElement page)
  {
    outgoing = page;
    incoming = null;
    showIncoming = null;
    outSeconds = ExitSeconds;
    elapsed = 0;
    exiting = true;
  }

  internal static void Cancel()
  {
    outgoing = incoming = null;
    showIncoming = null;
    exiting = prepared = false;
  }

  internal static void Update(float dt)
  {
    if (!Active) return;
    elapsed += Math.Min(dt, 1f / 30f);
    if (exiting) return;
    if (outgoing != null && elapsed >= outSeconds)
    {
      // Unless something else has replaced the page meanwhile.
      if (!GumService.Default.Root.Children.Contains(outgoing))
      {
        Cancel();
        return;
      }
      outgoing = null;
      showIncoming?.Invoke();
    }
    if (outgoing == null && elapsed >= OutSeconds + InSeconds) Cancel();
  }

  // Draws the current page into the transition's target with drawPage, or returns false to
  // let the caller draw it plainly. With keepBackdrop the page's backdrop is left out of the
  // effect and drawn into the HUD straight away. Call before anything else reaches the HUD.
  internal static bool Prepare(GraphicsDevice device, Action drawPage, bool keepBackdrop)
  {
    prepared = false;
    if (!Active) return false;
    var effect = EffectCache.MenuTransitionFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return false;
    var bindings = device.GetRenderTargets();
    if (bindings.Length == 0 || bindings[0].RenderTarget is not RenderTarget2D hud) return false;
    var page = outgoing ?? incoming;
    if (!GumService.Default.Root.Children.Contains(page)) return false;

    if (target == null || target.IsDisposed || target.Width != hud.Width || target.Height != hud.Height)
    {
      target?.Dispose();
      target = new RenderTarget2D(device, hud.Width, hud.Height, false, SurfaceFormat.Color, DepthFormat.None);
      batch ??= new SpriteBatch(device);
    }
    var viewport = device.Viewport;
    var backdrop = keepBackdrop ? Backdrop(page) : null;
    bool backdropShown = backdrop?.Visible == true;

    if (backdropShown) backdrop.Visible = false;
    device.SetRenderTarget(target);
    device.Clear(Color.Transparent);
    drawPage();
    device.SetRenderTargets(bindings);
    device.Viewport = viewport;
    device.Clear(Color.Transparent);

    // Then only the backdrop, straight into the HUD.
    if (backdropShown)
    {
      backdrop.Visible = true;
      foreach (var child in page.Children)
        if (child is GraphicalUiElement element && element != backdrop && element.Visible)
        {
          element.Visible = false;
          hidden.Add(element);
        }
      drawPage();
      foreach (var element in hidden) element.Visible = true;
      hidden.Clear();
    }

    region = ContentBounds(page, backdrop, GumService.Default.CanvasWidth, GumService.Default.CanvasHeight);
    prepared = true;
    return true;
  }

  // Draws the prepared page through the shader into the bound target.
  internal static bool Composite(GraphicsDevice device)
  {
    if (!prepared || target == null || target.IsDisposed) return false;
    prepared = false;
    float scale = target.Height / (float)GumService.Default.CanvasHeight;
    bool scanning = outgoing == null;
    float progress = scanning ? (elapsed - OutSeconds) / InSeconds : elapsed / outSeconds;
    var fx = EffectCache.MenuTransitionFx.Value;
    var p = fx.Parameters;
    p["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1));
    p["Resolution"].SetValue(new Vector2(target.Width, target.Height));
    p["Scale"].SetValue(scale);
    p["Region"].SetValue(region * scale);
    p["Progress"].SetValue(Math.Clamp(progress, 0f, 1f));
    p["Incoming"].SetValue(scanning ? 1f : 0f);
    p["Time"].SetValue(MenuHoverShimmer.Time);
    p["Accent"].SetValue(GuiSkin.HoverAccent.ToVector3());
    batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
      DepthStencilState.None, RasterizerState.CullNone, fx);
    batch.Draw(target, new Rectangle(0, 0, target.Width, target.Height), Color.White);
    batch.End();
    return true;
  }

  // Each title page has the same full-height backdrop on the left.
  private static GraphicalUiElement Backdrop(GraphicalUiElement page) =>
    page.GetGraphicalUiElementByName("LeftBackdrop") ?? page.GetGraphicalUiElementByName("NineSliceInstance");

  // Where the page has content, in canvas units, so the scan beam spans just that.
  private static Vector4 ContentBounds(GraphicalUiElement page, GraphicalUiElement backdrop, float width, float height)
  {
    var bounds = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
    void Visit(GraphicalUiElement element)
    {
      if (!element.Visible || element == backdrop) return;
      float w = element.GetAbsoluteWidth(), h = element.GetAbsoluteHeight();
      if (w > 0 && h > 0 && w < width * 0.6f && h < height * 0.8f)
      {
        float x = element.GetAbsoluteLeft(), y = element.GetAbsoluteTop();
        bounds = new Vector4(Math.Min(bounds.X, x), Math.Min(bounds.Y, y),
          Math.Max(bounds.Z, x + w), Math.Max(bounds.W, y + h));
      }
      if (element.Children != null)
        foreach (var child in element.Children)
          if (child is GraphicalUiElement gue) Visit(gue);
    }
    if (page.Children != null)
      foreach (var child in page.Children)
        if (child is GraphicalUiElement gue) Visit(gue);
    return bounds.X <= bounds.Z ? bounds : new Vector4(120, 400, 1200, 2100);
  }
}
