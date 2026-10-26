using System.Text;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

internal sealed class WorldPolishChecks : Game
{
  private readonly string root;
  private bool ran;
  public WorldPolishChecks(string root)
  {
    this.root = Path.GetFullPath(root);
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 256,
      PreferredBackBufferHeight = 256, GraphicsProfile = GraphicsProfile.HiDef };
  }
  protected override void Draw(GameTime gameTime)
  {
    if (ran) return;
    ran = true;
    int checks = 0;
    void Check(bool pass, string message)
    {
      if (!pass) throw new Exception(message);
      checks++;
    }
    Effect Shader(string name) => new(GraphicsDevice,
      GpuParticleChecks.Compile(Path.Combine(root, "Content/Shaders", name + ".fx")));
    using var atmosphere = Shader("PlanetAtmosphere");
    using var particles = Shader("WorldParticles");
    using var pixel = new Texture2D(GraphicsDevice, 1, 1);
    pixel.SetData(new[] { Color.White });
    var oldPixel = AssetManager.DefaultTexture;
    AssetManager.DefaultTexture = pixel;
    using var target = new RenderTarget2D(GraphicsDevice, 256, 256);
    using var batch = new SpriteBatch(GraphicsDevice);
    var projection = Matrix.CreateOrthographicOffCenter(0, 256, 256, 0, 0, 1);
    var center = new Vector2(128);
    var polish = new WorldPolish();
    VisualEffects.Recommended();
    VisualEffects.Bypass = false;
    var s = VisualEffects.Settings;
    Color[] Render(Action draw)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(Color.Black);
      draw();
      GraphicsDevice.SetRenderTarget(null);
      var data = new Color[256 * 256];
      target.GetData(data);
      return data;
    }
    Color[] Planet(float rotation = 0, float time = 0, float radius = 64)
      => Render(() => polish.DrawPlanet(batch, atmosphere, projection, center, radius, rotation, time));
    int Light(Color c) => c.R + c.G + c.B;
    bool Dark(Color[] data) => data.All(c => Light(c) == 0);
    try
    {
      var rim = Planet();
      Check(Light(rim[128 * 256 + 128]) == 0, "Atmosphere must leave the center's surface details alone");
      Check(Light(rim[63 * 256 + 128]) > 15, "Atmosphere must illuminate the silhouette");
      Check(Light(rim[15 * 256 + 128]) == 0, "Atmosphere must fade before its quad edge");
      Check(!rim.SequenceEqual(Planet(time: 10)), "Aurora wisps must animate");
      Check(Dark(Planet(radius: 0)), "Consumed or collapsed planets must not leave an atmosphere");
      s.Atmosphere = false;
      Check(Dark(Planet()), "Disabled atmosphere and no hits must draw nothing");
      polish.AddImpact(center, Vector3.UnitZ, Vector2.UnitX, new Color(255, 140, 40));
      polish.Update(.02f);
      var hit = Planet();
      Check(Light(hit[128 * 256 + 128]) > 50, "A hit must locally light its surface point");
      Check(Light(hit[70 * 256 + 128]) < Light(hit[128 * 256 + 128]) / 3,
        "Hit light must not wash over the whole planet");
      Check(Dark(Planet(rotation: MathHelper.Pi)), "Hits rotating behind the planet must be hidden");
      s.ImpactLightStrength = 0;
      s.ImpactRippleStrength = 1;
      polish.Update(.1f);
      var early = Planet();
      polish.Update(.15f);
      var later = Planet();
      int PeakRadius(Color[] data) => Enumerable.Range(1, 60).OrderByDescending(x => Light(data[128 * 256 + 128 + x])).First();
      Check(PeakRadius(later) > PeakRadius(early) + 8, "The impact ripple must expand across the curved surface");
      Check(!Dark(Render(() => polish.DrawFragments(batch, particles, projection))), "Rock debris must render through its own shader technique");
      for (int frame = 0; frame < 100; frame++)
      {
        polish.Update(.01f);
        for (int i = 0; i < 100; i++)
          polish.AddImpact(center, Vector3.UnitZ, Vector2.UnitX, Color.Orange, 2);
      }
      polish.Update(.001f);
      Check(polish.ActiveImpacts is > 0 and <= WorldPolish.MaxImpacts
        && polish.ActiveFragments is > 0 and <= WorldPolish.MaxFragments,
        "Dense late-game hits must respect the fixed light and fragment pools");
      polish.Update(10);
      Check(polish.ActiveImpacts == 0 && polish.ActiveFragments == 0 && Dark(Planet()), "All transient effects must expire");
      VisualEffects.Bypass = true;
      polish.AddImpact(center, Vector3.UnitZ, Vector2.UnitX, Color.Orange);
      polish.Update(.01f);
      Check(polish.ActiveImpacts == 0 && polish.ActiveFragments == 0, "Comparison bypass must disable new emissions");
      VisualEffects.Bypass = false;
      s.ImpactLightStrength = .65f;
      polish.AddImpact(center, Vector3.UnitZ, Vector2.UnitX, Color.Orange);
      polish.Clear();
      Check(polish.ActiveImpacts == 0 && polish.ActiveFragments == 0 && Dark(Planet()), "Reset must clear all transient visuals");
      for (int i = 0; i < 100; i++)
        polish.AddImpact(center, Vector3.UnitX, Vector2.UnitX, Color.Orange, .3f, debris: false);
      polish.AddImpact(center, Vector3.UnitZ, Vector2.UnitX, Color.CornflowerBlue, 2);
      polish.Update(.01f);
      var priority = Planet();
      Check(polish.ActiveFragments > 0 && priority[128 * 256 + 128].B > priority[128 * 256 + 128].R,
        "Continuous laser contacts must not suppress the fragments or local light of a heavy hit");
      Color[] Dust(float time) => Render(() => WorldPolish.DrawDust(batch, particles, 256, 256, Vector2.Zero, 1, time));
      s.DustSize = 3;
      s.DustBrightness = 1;
      var dust = Dust(0);
      Check(!Dark(dust) && !dust.SequenceEqual(Dust(10)), "Ambient dust must render and drift");
      s.SpaceDust = false;
      Check(Dark(Dust(0)), "Dust toggle must remove the layer immediately");
      s.AtmosphereWidth = .21f;
      using var stream = new MemoryStream(Encoding.UTF8.GetBytes(VisualEffects.ToJson()));
      var loaded = VisualEffects.Read(stream);
      Check(loaded.AtmosphereWidth == .21f && !loaded.SpaceDust && loaded.DebrisAmount == 1,
        "New controls must round-trip in the existing default settings file");
      loaded.ImpactDuration = 0;
      bool rejected = false;
      try { loaded.Validate(); } catch (InvalidDataException) { rejected = true; }
      Check(rejected, "Invalid effect lifetimes must be rejected before reaching shaders");
      Console.WriteLine($"World polish: {checks} checks passed.");
    }
    finally { AssetManager.DefaultTexture = oldPixel; }
    Exit();
  }
}
