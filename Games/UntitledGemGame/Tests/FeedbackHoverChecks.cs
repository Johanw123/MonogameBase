using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using UntitledGemGame;

internal static class FeedbackHoverChecks
{
  // Render the real form into its virtual HUD target after sampling physical input.
  // Recomputing input while that target is bound moves the hover to another position.
  public static void Run(GraphicsDevice device, SpriteBatch batch)
  {
    var originalViewport = device.Viewport;
    var originalMouse = Mouse.GetState();
    var palette = GuiPalette.Snapshot();
    using var target = new RenderTarget2D(device, HudLayout.Width, HudLayout.Bottom);
    var idle = new Color[target.Width * target.Height];
    var hovered = new Color[idle.Length];
    try
    {
      GuiPalette.ResetOriginal();
      PlaytestFeedback.OpenMainMenuSurvey();
      var button = PlaytestFeedback.RatingButtonBounds(1, 4);
      foreach (var viewport in new[] { new Viewport(0, 0, 1280, 720), new Viewport(160, 90, 960, 540) })
      {
        device.Viewport = viewport;
        Mouse.SetPosition(0, 0);
        PlaytestFeedback.Update(new GameTime());
        Render(idle);

        device.Viewport = viewport;
        var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
        var physical = Vector2.Transform(button.Center.ToVector2(), scale)
          + new Vector2(viewport.X, viewport.Y);
        // SDL's offscreen backend can offset a warp by the window decoration height.
        // Correct against the reported client position before checking hit targets.
        var desired = physical.ToPoint();
        var warp = desired;
        for (int attempt = 0; attempt < 3; attempt++)
        {
          Mouse.SetPosition(warp.X, warp.Y);
          var actual = Mouse.GetState().Position;
          if (actual == desired) break;
          warp += desired - actual;
        }
        PlaytestFeedback.Update(new GameTime());
        var clickPosition = PlaytestFeedback.VirtualCursor();
        if (!button.Contains(clickPosition.ToPoint()))
          throw new Exception($"Feedback click position {clickPosition} does not match {button}; physical {physical}, mouse {Mouse.GetState().Position}, requested viewport {viewport}, adapter {BaseGame.BoxingViewportAdapterGui.Viewport}, scale {scale.M11}.");
        Render(hovered);

        int changed = 0;
        for (int y = button.Top; y < button.Bottom; y++)
          for (int x = button.Left; x < button.Right; x++)
            if (idle[y * target.Width + x] != hovered[y * target.Width + x]) changed++;
        if (changed < 10)
          throw new Exception("Feedback hover moved away from the clicked button while rendering the HUD.");
      }
      Console.WriteLine("Feedback hover checks passed: input and rendered hover agree at scaled and letterboxed viewports.");
    }
    finally
    {
      device.SetRenderTarget(null);
      device.Viewport = originalViewport;
      Mouse.SetPosition(originalMouse.X, originalMouse.Y);
      PlaytestFeedback.Close();
      GuiPalette.Apply(palette);
    }

    void Render(Color[] pixels)
    {
      device.SetRenderTarget(target);
      device.Clear(Color.Transparent);
      PlaytestFeedback.Draw(batch);
      device.SetRenderTarget(null);
      target.GetData(pixels);
    }
  }
}
