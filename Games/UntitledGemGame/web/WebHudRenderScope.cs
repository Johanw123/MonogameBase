using MonoGameGum.Input;
using System;
using System.Collections.Generic;
using Gum.Wireframe;
using RenderingLibrary;
using RenderingLibrary.Graphics;

namespace UntitledGemGame.Web;

// Scale only drawing. Layout, tooltip placement and cursor transforms use the
// original camera between frames. Gum resets client dimensions from the target.
internal readonly struct WebHudRenderScope : IDisposable
{
  private static readonly List<(LayerCameraSettings Settings, float Zoom)> layerZooms = new();
  internal static bool IsDrawing { get; private set; }
  private readonly float zoom;

  private WebHudRenderScope(float previousZoom) => zoom = previousZoom;

  public static WebHudRenderScope Begin()
  {
    var renderer = SystemManagers.Default.Renderer;
    var scope = new WebHudRenderScope(renderer.Camera.Zoom);
    renderer.Camera.Zoom *= 0.5f;
    renderer.Camera.ClientWidth = 1920;
    renderer.Camera.ClientHeight = 1080;
    layerZooms.Clear();
    foreach (var layer in renderer.Layers)
    {
      if (layer.LayerCameraSettings is { Zoom: { } zoom } settings)
      {
        layerZooms.Add((settings, zoom));
        settings.Zoom = zoom * 0.5f;
      }
    }
    IsDrawing = true;
    return scope;
  }

  public static bool HasCursorOver(InteractiveGue visual, Layer layer)
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    var zoom = camera.Zoom;
    camera.Zoom *= 2f;
    camera.ClientWidth = HudLayout.Width;
    camera.ClientHeight = HudLayout.Bottom;
    try { return visual.HasCursorOver(GumService.Default.Cursor, layer); }
    finally
    {
      camera.Zoom = zoom;
      camera.ClientWidth = 1920;
      camera.ClientHeight = 1080;
    }
  }

  public void Dispose()
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    camera.Zoom = zoom;
    camera.ClientWidth = HudLayout.Width;
    camera.ClientHeight = HudLayout.Bottom;
    foreach (var item in layerZooms) item.Settings.Zoom = item.Zoom;
    layerZooms.Clear();
    IsDrawing = false;
  }
}
