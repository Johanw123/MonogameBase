using System;
using System.Runtime.CompilerServices;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary;
using RenderingLibrary.Graphics;

namespace UntitledGemGame;

// Draws the main menu buttons' highlight between the (now transparent) nine-slice and its
// text, so it can scan in, lock brackets on, fade out and answer clicks (MenuHover.fx).
// Forms still owns visibility, focus, pressed state and hit testing: its states only toggle
// the empty HoverSignal and PressSignal containers that MenuTheme adds to the button.
internal sealed class MenuHoverShimmer : RenderableBase
{
  private sealed class DeviceResources
  {
    public readonly SpriteBatch Batch;
    public DeviceResources(GraphicsDevice device)
    {
      Batch = new SpriteBatch(device);
      device.Disposing += (_, _) => Batch.Dispose();
    }
  }

  private static readonly ConditionalWeakTable<GraphicsDevice, DeviceResources> devices = new();
  private static readonly ConditionalWeakTable<GraphicalUiElement, MenuHoverShimmer> byButton = new();
  internal static float Time { get; set; }

  // Canvas units drawn around the button for the brackets and the click ring.
  private const float Pad = 64f;
  private const float FadeOut = 0.16f;
  private const float TextSlide = 14f;
  private const float TextSlideSeconds = 0.26f;
  private const float ClickSeconds = 0.5f;

  private readonly NineSliceRuntime background;
  private readonly GraphicalUiElement hoverSignal, pressSignal, text;
  private readonly float textX;
  private DeviceResources resources;
  private Texture2D art;
  private bool batchStarted, active;
  private float activeSince, hover, press, textOffset, lastStep = float.NaN;
  private float clickedAt = float.NegativeInfinity;

  private MenuHoverShimmer(GraphicalUiElement button, NineSliceRuntime background)
  {
    this.background = background;
    hoverSignal = button.GetChildByNameRecursively("HoverSignal");
    pressSignal = button.GetChildByNameRecursively("PressSignal");
    text = button.GetChildByNameRecursively("TextInstance");
    textX = text?.X ?? 0;
  }

  internal static void Attach(GraphicalUiElement root, ScreenSave screen)
  {
    foreach (var instance in screen.Instances)
      if (instance.BaseType == "Controls/ButtonMainMenu")
        Attach(root.GetChildByNameRecursively(instance.Name));
  }

  internal static void Attach(GraphicalUiElement button)
  {
    if (button?.GetChildByNameRecursively("Background") is not NineSliceRuntime bg
      || bg.GetChildByNameRecursively("HoverShimmer") != null) return;
    var shimmer = new MenuHoverShimmer(button, bg);
    var visual = new GraphicalUiElement();
    visual.SetContainedObject(shimmer);
    visual.Name = "HoverShimmer";
    visual.WidthUnits = DimensionUnitType.RelativeToParent;
    visual.HeightUnits = DimensionUnitType.RelativeToParent;
    visual.Width = visual.Height = 0;
    visual.Parent = bg;
    byButton.AddOrUpdate(button, shimmer);
    // Forms raises Click for the mouse, keyboard and gamepad alike.
    if (button is Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime forms)
      forms.FormsControl.Click += (_, _) => shimmer.clickedAt = Time;
    if (bg.Texture != null)
      devices.GetValue(bg.Texture.GraphicsDevice, d => new DeviceResources(d));
  }

  // Captures press buttons without going through Forms.
  internal static void NotifyClick(GraphicalUiElement button)
  {
    if (button != null && byButton.TryGetValue(button, out var shimmer)) shimmer.clickedAt = Time;
  }

  // Forms only clears a highlight when the cursor rolls off, which never happens while a
  // page is hidden. A page shown again starts unhighlighted; whatever is under the cursor
  // rolls on with Gum's next update.
  internal static void ResetPage(GraphicalUiElement page)
  {
    if (page == null) return;
    if (page is Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime button
      && byButton.TryGetValue(button, out var shimmer))
    {
      var forms = button.FormsControl;
      if (forms.IsFocused) forms.IsFocused = false;
      button.SetProperty("ButtonCategoryState", forms.IsEnabled ? "Enabled" : "Disabled");
      shimmer.active = false;
      shimmer.hover = shimmer.press = 0;
      shimmer.clickedAt = float.NegativeInfinity;
    }
    if (page.Children == null) return;
    foreach (var child in page.Children)
      if (child is GraphicalUiElement element) ResetPage(element);
  }

  private float ClickAge => Time - clickedAt;

  // Called from BatchKey and Render, whichever runs first in a frame.
  private void Step()
  {
    if (lastStep == Time) return;
    // After a long gap (the menu was hidden) jump straight to the current state.
    bool resumed = float.IsNaN(lastStep) || Time - lastStep > 0.25f;
    float dt = resumed ? 0 : Math.Max(0, Time - lastStep);
    lastStep = Time;

    bool nowActive = hoverSignal?.Visible == true;
    // A page that reappears already highlighted skips the entry animation.
    if (nowActive && !active) activeSince = resumed ? Time - 1f : Time;
    active = nowActive;
    if (active)
    {
      hover = 1;
      // The state swaps in the highlight artwork; keep it for the fade-out too.
      if (background.Texture != null) art = background.Texture;
    }
    else hover = resumed ? 0 : Math.Max(0, hover - dt / FadeOut);

    float pressTarget = pressSignal?.Visible == true ? 1 : 0;
    press = resumed ? pressTarget : MathHelper.Lerp(press, pressTarget, 1 - MathF.Exp(-dt * 18f));
    if (press < 0.001f) press = 0;
  }

  private bool NeedsDraw => hover > 0 || active || ClickAge < ClickSeconds;

  // Idle buttons remain in Gum's normal batch, with no extra draw or state switch.
  // The extra frame after fading out lets Render put the text back.
  public override string BatchKey
  {
    get
    {
      Step();
      return NeedsDraw || textOffset != 0 ? "MenuHoverShimmer" : "";
    }
  }

  public override void StartBatch(ISystemManagers managers)
  {
    batchStarted = false;
    Step();
    if (!NeedsDraw || art == null || art.IsDisposed || !background.Visible) return;
    var effect = EffectCache.MenuHoverFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    var device = effect.Value.GraphicsDevice;
    resources = devices.GetValue(device, d => new DeviceResources(d));
    effect.Value.Parameters["view_projection"].SetValue(
      Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1));
    resources.Batch.Begin(SpriteSortMode.Immediate, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
      SamplerState.LinearClamp, DepthStencilState.None, device.RasterizerState, effect.Value);
    batchStarted = true;
  }

  public override void Render(ISystemManagers managers)
  {
    Step();
    SlideText();
    if (!batchStarted) return;
    var camera = SystemManagers.Default.Renderer.Camera;
    float x = background.GetAbsoluteLeft(), y = background.GetAbsoluteTop();
    float width = background.GetAbsoluteWidth(), height = background.GetAbsoluteHeight();
    camera.WorldToScreen(x, y, out float left, out float top);
    camera.WorldToScreen(x + width, y + height, out float right, out float bottom);
    var size = new Vector2(right - left, bottom - top);
    if (size.X <= 0 || size.Y <= 0 || width <= 0) return;
    float scale = size.X / width;
    var pad = new Vector2(Pad * scale);

    var effect = EffectCache.MenuHoverFx.Value;
    var p = effect.Parameters;
    p["Size"].SetValue(size);
    p["Pad"].SetValue(pad);
    p["Scale"].SetValue(scale);
    p["Intro"].SetValue(Time - activeSince);
    p["Hover"].SetValue(hover);
    p["Press"].SetValue(press);
    p["Click"].SetValue(Math.Min(ClickAge, 10f));
    p["Time"].SetValue(Time);
    p["Accent"].SetValue(GuiSkin.HoverAccent.ToVector4());
    p["TexSize"].SetValue(new Vector2(art.Width, art.Height));
    p["Frame"].SetValue(background.CustomFrameTextureCoordinateWidth ?? 12f);
    // The quad's texture coordinates only locate pixels; the shader slices the artwork itself.
    var quad = size + pad * 2;
    resources.Batch.Draw(art, new Vector2(left, top) - pad, null, Color.White, 0, Vector2.Zero,
      quad / new Vector2(art.Width, art.Height), SpriteEffects.None, 0);
  }

  // The label steps right as the bar scans in. Text renders after the background and this
  // child, so moving it here takes effect in the same frame.
  private void SlideText()
  {
    if (text == null) return;
    float t = Math.Clamp((Time - activeSince) / TextSlideSeconds, 0, 1);
    float eased = 1 - MathF.Pow(1 - t, 3);
    float offset = TextSlide * eased * hover * hover * (3 - 2 * hover);
    if (offset < 0.05f) offset = 0;
    if (MathF.Abs(offset - textOffset) < 0.01f) return;
    textOffset = offset;
    text.X = textX + offset;
  }

  public override void EndBatch(ISystemManagers managers)
  {
    if (batchStarted) resources.Batch.End();
    batchStarted = false;
  }
}
