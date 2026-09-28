using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// Isolated GPU check: compares atlas draws with original images, no game state/saves.
internal sealed class IconAtlasChecks : Game
{
  private readonly string root;
  public IconAtlasChecks(string root)
  {
    this.root = Path.GetFullPath(root);
    Content.RootDirectory = Path.Combine(this.root, "bin/Debug/net10.0/Content");
    _ = new GraphicsDeviceManager(this)
    {
      PreferredBackBufferWidth = 1024, PreferredBackBufferHeight = 640,
      SynchronizeWithVerticalRetrace = false
    };
  }

  protected override void LoadContent()
  {
    var atlas = Content.Load<Texture2D>("Atlases/icons");
    using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Content/Atlases/icons.json")));
    var frames = metadata.RootElement.EnumerateObject().ToArray();
    foreach (var path in ModuleCatalog.Icons.Skip(1).Concat(SignalCatalog.Definitions.Select(s => s.Icon)))
      if (!metadata.RootElement.TryGetProperty(path, out _)) throw new Exception($"Missing icon: {path}");
    using var batch = new SpriteBatch(GraphicsDevice);
    using var expected = new RenderTarget2D(GraphicsDevice, 1024, 640);
    using var actual = new RenderTarget2D(GraphicsDevice, 1024, 640);
    var icons = new IconDrawBatch();
    foreach (var sampler in new[] { SamplerState.PointClamp, SamplerState.LinearClamp })
    {
      GraphicsDevice.SetRenderTarget(expected);
      GraphicsDevice.Clear(new Color(12, 16, 24));
      for (int i = 0; i < frames.Length; i++)
      {
        using var stream = File.OpenRead(Path.Combine(root, "Content", frames[i].Name));
        using var source = Texture2D.FromStream(GraphicsDevice, stream);
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (int p = 0; p < pixels.Length; p++) pixels[p] = Color.FromNonPremultiplied(pixels[p].ToVector4());
        source.SetData(pixels);
        var center = new Vector2(i % 12 * 84 + 42, i / 12 * 100 + 50);
        float scale = 61f / Math.Max(source.Width, source.Height);
        var tint = Color.White * (i % 2 == 0 ? 1f : .25f);
        batch.Begin(samplerState: sampler);
        batch.Draw(source, center, null, tint, 0, new Vector2(source.Width, source.Height) / 2, scale, SpriteEffects.None, 0);
        batch.End();
        var r = frames[i].Value;
        icons.Add(new Rectangle(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32()), center, scale, tint);
      }
      GraphicsDevice.SetRenderTarget(actual);
      GraphicsDevice.Clear(new Color(12, 16, 24));
      long before = GraphicsDevice.Metrics.DrawCount;
      icons.Draw(batch, atlas, sampler);
      if (GraphicsDevice.Metrics.DrawCount - before != 1) throw new Exception("Icons did not draw in a single GPU batch");
      before = GraphicsDevice.Metrics.DrawCount;
      icons.Draw(batch, atlas, sampler);
      if (GraphicsDevice.Metrics.DrawCount != before) throw new Exception("Icons leaked into a later pass");
      GraphicsDevice.SetRenderTarget(null);
      var left = new Color[1024 * 640];
      var right = new Color[left.Length];
      expected.GetData(left);
      actual.GetData(right);
      for (int i = 0; i < left.Length; i++)
        if (Math.Abs(left[i].R - right[i].R) > 2 || Math.Abs(left[i].G - right[i].G) > 2 ||
            Math.Abs(left[i].B - right[i].B) > 2 || Math.Abs(left[i].A - right[i].A) > 2)
          throw new Exception($"Atlas differs from original at pixel {i}: {left[i]} vs {right[i]}");
    }
    Console.WriteLine($"Atlas checks passed: {frames.Length} icons match originals with point/linear filtering, each in one GPU draw.");
    Exit();
  }
}
