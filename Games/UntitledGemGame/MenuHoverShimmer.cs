using System.Runtime.CompilerServices;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary;
using RenderingLibrary.Graphics;

namespace UntitledGemGame;

// A separate Gum renderable keeps the animated light between the existing nine-slice
// and its text. Forms still controls visibility, focus, pressed state and hit testing.
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
  internal static float Time { get; set; }
  private readonly NineSliceRuntime background;
  private DeviceResources resources;
  private bool batchStarted, hovering;
  private float hoverStarted;

  private MenuHoverShimmer(NineSliceRuntime background) => this.background = background;

  internal static void Attach(GraphicalUiElement root, ScreenSave screen)
  {
    foreach (var instance in screen.Instances)
    {
      if (instance.BaseType != "Controls/ButtonMainMenu") continue;
      var button = root.GetChildByNameRecursively(instance.Name);
      if (button?.GetChildByNameRecursively("Background") is not NineSliceRuntime bg
        || bg.GetChildByNameRecursively("HoverShimmer") != null) continue;
      var visual = new GraphicalUiElement();
      visual.SetContainedObject(new MenuHoverShimmer(bg));
      visual.Name = "HoverShimmer";
      visual.WidthUnits = DimensionUnitType.RelativeToParent;
      visual.HeightUnits = DimensionUnitType.RelativeToParent;
      visual.Width = visual.Height = 0;
      visual.Parent = bg;
      if (bg.Texture != null)
        devices.GetValue(bg.Texture.GraphicsDevice, d => new DeviceResources(d));
    }
  }

  // Idle buttons remain in Gum's normal batch, with no extra draw or state switch.
  public override string BatchKey => background.Alpha > 0 ? "MenuHoverShimmer" : "";

  public override void StartBatch(ISystemManagers managers)
  {
    batchStarted = false;
    if (background.Alpha == 0 || !background.Visible)
    {
      hovering = false;
      return;
    }
    var effect = EffectCache.MenuHoverFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    if (!hovering) hoverStarted = Time;
    hovering = true;
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
    if (background.Alpha == 0) hovering = false;
    if (!batchStarted) return;
    var camera = SystemManagers.Default.Renderer.Camera;
    float x = background.GetAbsoluteLeft(), y = background.GetAbsoluteTop();
    camera.WorldToScreen(x, y, out float left, out float top);
    camera.WorldToScreen(x + background.GetAbsoluteWidth(), y + background.GetAbsoluteHeight(),
      out float right, out float bottom);
    var size = new Vector2(right - left, bottom - top);
    if (size.X <= 0 || size.Y <= 0) return;
    var effect = EffectCache.MenuHoverFx.Value;
    effect.Parameters["Size"].SetValue(size);
    effect.Parameters["Time"].SetValue(Time - hoverStarted);
    var accent = GuiSkin.HoverAccent.ToVector4();
    accent.W *= background.Alpha / 255f;
    effect.Parameters["Accent"].SetValue(accent);
    resources.Batch.Draw(GuiSkin.Pixel(effect.GraphicsDevice), new Vector2(left, top), null,
      Color.White, 0, Vector2.Zero, size, SpriteEffects.None, 0);
  }

  public override void EndBatch(ISystemManagers managers)
  {
    if (batchStarted) resources.Batch.End();
    batchStarted = false;
  }
}
