using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using UntitledGemGame;

// Offscreen fleet comparison: original strips/hulls versus packed animation regions.
internal sealed class FleetAtlasChecks : Game
{
  private readonly string root, shaderPath;
  private readonly string[] hullPaths = [FleetAtlas.ScoutHull, FleetAtlas.FighterHull, FleetAtlas.TorpedoHull,
    FleetAtlas.BomberHull, FleetAtlas.FrigateHull, FleetAtlas.DroneHull];
  private readonly string[] enginePaths = [FleetAtlas.ScoutEngine, FleetAtlas.FighterEngine, FleetAtlas.TorpedoEngine,
    FleetAtlas.BomberEngine, FleetAtlas.FrigateEngine, FleetAtlas.DroneEngine];

  public FleetAtlasChecks(string root, string shaderPath)
  {
    this.root = Path.GetFullPath(root);
    this.shaderPath = shaderPath;
    Content.RootDirectory = Path.Combine(this.root, "bin/Debug/net10.0/Content");
    _ = new GraphicsDeviceManager(this)
    {
      PreferredBackBufferWidth = 1024, PreferredBackBufferHeight = 768,
      SynchronizeWithVerticalRetrace = false
    };
  }

  protected override void LoadContent()
  {
    var texture = Content.Load<Texture2D>("Atlases/fleet");
    string metadata = File.ReadAllText(Path.Combine(root, "Content/Atlases/fleet.json"));
    var fleet = new FleetAtlas(texture, metadata);
    using var document = JsonDocument.Parse(metadata);
    var originals = new Dictionary<string, Texture2D>();
    foreach (var entry in document.RootElement.EnumerateObject())
    {
      string path = entry.Name.Split('#')[0];
      if (originals.ContainsKey(path)) continue;
      using var stream = File.OpenRead(Path.Combine(root, "Content", path));
      var source = Texture2D.FromStream(GraphicsDevice, stream);
      var pixels = new Color[source.Width * source.Height];
      source.GetData(pixels);
      for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.FromNonPremultiplied(pixels[i].ToVector4());
      source.SetData(pixels);
      originals.Add(path, source);
    }
    using var shader = new Effect(GraphicsDevice, File.ReadAllBytes(shaderPath));
    shader.Parameters["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, 1024, 768, 0, 0, 1));
    shader.Parameters["_OutlineColor"].SetValue(new Vector4(.1f, .85f, .84f, 1));
    shader.Parameters["_TotalTime"].SetValue(1.23f);
    using var batch = new SpriteBatch(GraphicsDevice);
    using var expected = new RenderTarget2D(GraphicsDevice, 1024, 768);
    using var actual = new RenderTarget2D(GraphicsDevice, 1024, 768);
    var engines = enginePaths.Select(fleet.CreateEngine).ToArray();
    var secondScout = fleet.CreateEngine(FleetAtlas.ScoutEngine);
    var alphaStates = new byte[] { 255, 200, 110, 65, 40, 0 };
    long oldCalls = 0, newCalls = 0;
    int maxPointDifferences = 0;
    for (int frame = 0; frame < FleetAtlas.EngineFrameCount; frame++)
    {
      for (int kind = 0; kind < engines.Length; kind++)
      {
        if (engines[kind].TextureRegion.Bounds != fleet.Region(enginePaths[kind] + "#" + frame).Bounds)
          throw new Exception($"Engine {kind} frame {frame} has incorrect atlas coordinates");
        if (engines[kind].Origin != new Vector2(32) || fleet.Region(hullPaths[kind]).Width != 64)
          throw new Exception("Atlas changed frame origins or hull dimensions");
      }
      foreach (var (useShader, sampler) in new[]
      { (false, SamplerState.PointClamp), (false, SamplerState.LinearClamp), (true, SamplerState.PointClamp) })
      {
        foreach (bool packed in new[] { false, true })
        {
          GraphicsDevice.SetRenderTarget(packed ? actual : expected);
          GraphicsDevice.Clear(new Color(12, 16, 24));
          shader.Parameters["TexelSize"].SetValue(packed
            ? new Vector2(1f / texture.Width, 1f / texture.Height) : new Vector2(1f / 64));
          long before = GraphicsDevice.Metrics.DrawCount;
          batch.Begin(samplerState: sampler, effect: useShader ? shader : null);
          for (int i = 0; i < 100; i++)
          {
            int kind = i % engines.Length;
            // Overlap, fractional scales and rotation exercise painter order and UV precision.
            var transform = new Transform2(new Vector2(90 + i % 12 * 65, 80 + i / 12 * 70),
              i * .17f, new Vector2(.6f + i % 4 * .3f));
            Color hullColor = new Color(210, 235, 255, useShader ? (int)alphaStates[i / 6 % alphaStates.Length] : 255);
            Color engineColor = new Color(Color.White * (i % 3 == 0 ? .25f : 1f), 1f);
            Sprite engine, hull;
            if (packed)
            {
              engine = engines[kind];
              hull = new Sprite(fleet.Region(hullPaths[kind]));
            }
            else
            {
              engine = new Sprite(new Texture2DRegion(originals[enginePaths[kind]], frame * 64, 0, 64, 64));
              hull = new Sprite(originals[hullPaths[kind]]);
            }
            engine.Origin = hull.Origin = new Vector2(32);
            engine.Color = engineColor;
            hull.Color = hullColor;
            batch.Draw(engine, transform);
            batch.Draw(hull, transform);
          }
          // Home base shares the same atlas, without splitting the fleet batch.
          var home = new Sprite(packed ? fleet.Region(FleetAtlas.HomeBaseHull)
            : new Texture2DRegion(originals[FleetAtlas.HomeBaseHull])) { Origin = new Vector2(64) };
          batch.Draw(home, new Transform2(new Vector2(920, 650), .3f, new Vector2(.8f)));
          batch.End();
          long calls = GraphicsDevice.Metrics.DrawCount - before;
          if (packed) newCalls = calls; else oldCalls = calls;
        }
        GraphicsDevice.SetRenderTarget(null);
        var left = new Color[1024 * 768];
        var right = new Color[left.Length];
        expected.GetData(left);
        actual.GetData(right);
        int differing = 0;
        for (int i = 0; i < left.Length; i++)
          if (Math.Abs(left[i].R - right[i].R) > 2 || Math.Abs(left[i].G - right[i].G) > 2 ||
              Math.Abs(left[i].B - right[i].B) > 2 || Math.Abs(left[i].A - right[i].A) > 2) differing++;
        // Rotated point sampling can land on an exact texel boundary; atlas UV
        // rounding may select the adjacent texel for a handful of pixels.
        // Linear filtering (menu) and the gameplay shader must match throughout.
        bool pointOnly = !useShader && sampler == SamplerState.PointClamp;
        if (pointOnly) maxPointDifferences = Math.Max(maxPointDifferences, differing);
        if (differing > (pointOnly ? 4 : 0)) throw new Exception($"Fleet pixels differ: frame {frame}, shader {useShader}, {differing} pixels");
        if (oldCalls != 201 || newCalls != 1) throw new Exception($"Unexpected GPU calls: {oldCalls} -> {newCalls}");
      }
      if (secondScout.TextureRegion.Bounds != fleet.Region(FleetAtlas.ScoutEngine + "#0").Bounds)
        throw new Exception("Engine instances share animation playback state");
      foreach (var engine in engines)
        engine.Update(new GameTime(TimeSpan.FromMilliseconds((frame + 1) * 150), TimeSpan.FromMilliseconds(150)));
    }
    if (engines[0].TextureRegion.Bounds != secondScout.TextureRegion.Bounds)
      throw new Exception("Engine animation failed to loop after eight frames");
    foreach (var source in originals.Values) source.Dispose();
    Console.WriteLine($"Fleet atlas passed: all 8 frames, 6 ship types, home base, overlap/rotation/scale and shader states match. GPU draws: {oldCalls} -> {newCalls} for 100 ships + home base.");
    Console.WriteLine($"Maximum rotated point-sampling boundary differences: {maxPointDifferences} / {1024 * 768} pixels. Linear/shader comparisons match within 2/255 throughout.");
    Exit();
  }
}
