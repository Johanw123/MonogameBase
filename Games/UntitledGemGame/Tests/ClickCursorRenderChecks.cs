using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// Runs the real shape shader and virtual-target downscale without loading player saves.
internal sealed class ClickCursorRenderChecks : Game
{
  private readonly string output;
  private ShapeBatch shapes = null!;
  private SpriteBatch sprites = null!;

  public ClickCursorRenderChecks(string content, string output)
  {
    Content.RootDirectory = Path.GetFullPath(content);
    this.output = output;
    _ = new GraphicsDeviceManager(this)
    {
      PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720,
      SynchronizeWithVerticalRetrace = false
    };
    IsFixedTimeStep = false;
  }

  protected override void LoadContent()
  {
    shapes = new ShapeBatch(GraphicsDevice, Content, Content.Load<Effect>("Shaders/Shapes/apos-shapes"));
    sprites = new SpriteBatch(GraphicsDevice);
    Directory.CreateDirectory(output);
  }

  protected override void Draw(GameTime gameTime)
  {
    foreach (var viewport in new[] { new Rectangle(0, 0, 1280, 720), new Rectangle(128, 72, 1024, 576) })
    {
      var pointer = new Vector2(900, 450);
      using var world = new RenderTarget2D(GraphicsDevice, 3840, 2160);
      using var display = new RenderTarget2D(GraphicsDevice, 1280, 720);
      int previousGreen = -1;
      foreach (float progress in new[] { -1f, 0f, 0.5f, 1f })
      {
        // This changes GraphicsDevice.Viewport, just like the real frame.
        GraphicsDevice.SetRenderTarget(world);
        GraphicsDevice.Clear(Color.Black);
        var center = ClickUtility.PointerToTarget(pointer, viewport, new Vector2(world.Width, world.Height));
        float pixel = world.Width / (float)viewport.Width;
        shapes.Begin(Matrix.Identity, Matrix.CreateOrthographicOffCenter(0, world.Width, world.Height, 0, 0, 1));
        ClickCursorVisual.Draw(shapes, center, new Vector2(40 * pixel), pixel, progress >= 0, Math.Max(0, progress), progress == 1 ? 1 : 0);
        shapes.End();
        GraphicsDevice.SetRenderTarget(display);
        GraphicsDevice.Clear(Color.Black);
        sprites.Begin(samplerState: SamplerState.LinearClamp);
        sprites.Draw(world, viewport, Color.White);
        sprites.End();
        var pixels = new Color[display.Width * display.Height];
        display.GetData(pixels);
        int left = display.Width, top = display.Height, right = -1, bottom = -1, green = 0;
        for (int y = 0; y < display.Height; ++y)
        for (int x = 0; x < display.Width; ++x)
        {
          var p = pixels[y * display.Width + x];
          if (p.G < 45 && p.B < 45) continue;
          left = Math.Min(left, x); right = Math.Max(right, x);
          top = Math.Min(top, y); bottom = Math.Max(bottom, y);
          if (p.G > 35 && p.G > p.B * 1.08) ++green;
        }
        if (progress < 0 && (right < 0 || Math.Abs((left + right) / 2f - pointer.X) > 1.5f
          || Math.Abs((top + bottom) / 2f - pointer.Y) > 1.5f))
          throw new Exception($"Rendered pointer ring is offset: bounds {left},{top}..{right},{bottom}, pointer {pointer}");
        if (progress >= 0)
        {
          if (green <= previousGreen) throw new Exception("Cooldown disk must expand and fill more area as progress increases");
          previousGreen = green;
        }
        if (progress == 1)
        {
          var nearBoundary = pixels[(int)pointer.Y * display.Width + (int)pointer.X + 37];
          if (nearBoundary.G < 45 || nearBoundary.G <= nearBoundary.B)
            throw new Exception("Activation fill must reach the targeting boundary, not stop in the middle");
        }
        using var file = File.Create(Path.Combine(output, $"ring-{viewport.X}-{progress:0.0}.png"));
        display.SaveAsPng(file, display.Width, display.Height);
      }
    }
    GraphicsDevice.SetRenderTarget(null);
    Console.WriteLine("Pointer ring render checks passed: off-center pointer, virtual downscale, letterboxing and expanding cooldown fill.");
    Exit();
  }
}
