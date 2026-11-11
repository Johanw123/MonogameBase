using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// The fleet's module effects through the real shader (ModuleEffects.fx) and batch: every
// kind lights the pixels it should, stays inside its quad and only the vortex darkens.
// With an output path it also saves a contact sheet of every effect for a look by eye.
internal sealed class ModuleEffectsChecks : Game
{
  private readonly string root, sheetPath;
  private bool ran;

  public ModuleEffectsChecks(string root, string sheetPath)
  {
    this.root = Path.GetFullPath(root);
    this.sheetPath = sheetPath;
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 128,
      PreferredBackBufferHeight = 96, GraphicsProfile = GraphicsProfile.HiDef };
  }

  protected override void Draw(GameTime gameTime)
  {
    if (ran) return;
    ran = true;
    int count = 0;
    void Check(bool pass, string message)
    {
      if (!pass) throw new Exception(message);
      count++;
    }
    using var effect = new Effect(GraphicsDevice,
      GpuParticleChecks.Compile(Path.Combine(root, "Content/Shaders/ModuleEffects.fx")));
    foreach (string technique in new[] { "ModuleBolt", "ModuleRing", "ModuleBeam", "ModuleVortex", "ModuleGhost" })
      Check(effect.Techniques[technique] != null, $"ModuleEffects must offer {technique}");

    if (sheetPath != null) SaveSheet(effect, sheetPath);
    const int width = 256, height = 256;
    using var target = new RenderTarget2D(GraphicsDevice, width, height);
    var batch = new ModuleEffectBatch(GraphicsDevice);
    var view = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
    var grey = new Color(60, 60, 60);
    Color[] Render(Action queue, float time = 1f)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(grey);
      queue();
      batch.Draw(effect, view, time);
      GraphicsDevice.SetRenderTarget(null);
      var pixels = new Color[width * height];
      target.GetData(pixels);
      return pixels;
    }
    int Light(Color[] pixels, int x, int y) => pixels[y * width + x].R + pixels[y * width + x].G + pixels[y * width + x].B;
    int MaxLight(Color[] pixels, Func<int, int, bool> where)
    {
      int max = 0;
      for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
          if (where(x, y)) max = Math.Max(max, Light(pixels, x, y));
      return max;
    }
    const int ground = 180;
    var tint = new Color(90, 165, 255);
    var middle = new Vector2(128, 128);
    float Away(int x, int y) => Vector2.Distance(new Vector2(x, y), middle);

    // A bolt halfway through its life lights its whole path, wanders off the straight
    // line and leaves the far corners alone. Later frames draw another shape.
    var bolt = Render(() => batch.Bolt(new Vector2(40, 128), new Vector2(216, 128), 0.3f, 0.2f, tint));
    Check(MaxLight(bolt, (x, y) => x is > 60 and < 70) > ground + 250 && MaxLight(bolt, (x, y) => x is > 180 and < 190) > ground + 250,
      "A bolt must be white hot along its whole path");
    Check(Enumerable.Range(50, 150).Any(x => Light(bolt, x, 128) < ground + 200),
      "A bolt must wander off the straight line");
    Check(MaxLight(bolt, (x, y) => y < 70 || y > 186) == ground, "A bolt's light must stay inside its quad");
    Check(!bolt.SequenceEqual(Render(() => batch.Bolt(new Vector2(40, 128), new Vector2(216, 128), 0.3f, 0.2f, tint), 1.5f)),
      "A bolt must jump to a new shape over time");
    var early = Render(() => batch.Bolt(new Vector2(40, 128), new Vector2(216, 128), 0.02f, 0.2f, tint));
    Check(MaxLight(early, (x, y) => x > 190) == ground, "A bolt strikes outward from its source");
    var spent = Render(() => batch.Bolt(new Vector2(40, 128), new Vector2(216, 128), 1f, 0.2f, tint));
    Check(MaxLight(spent, (x, y) => true) == ground, "A spent bolt draws nothing");

    // Rings: the shock front runs outward, the collapse runs inward, the burst has rays.
    foreach (var style in Enum.GetValues<ModuleEffectBatch.RingStyle>())
    {
      var ring = Render(() => batch.Ring(middle, 80f, 0.5f, tint, style));
      Check(MaxLight(ring, (x, y) => Away(x, y) < 100f) > ground + 150, $"A {style} ring must be bright");
      Check(MaxLight(ring, (x, y) => Away(x, y) > 111f) == ground, $"A {style} ring must fade out inside its quad");
      Check(MaxLight(Render(() => batch.Ring(middle, 80f, 1f, tint, style)), (x, y) => true) <= ground + 6,
        $"A finished {style} ring is gone");
    }
    float Front(ModuleEffectBatch.RingStyle style, float progress)
    {
      var ring = Render(() => batch.Ring(middle, 80f, progress, tint, style));
      int best = 0, bestLight = 0;
      for (int x = 133; x < 240; x++)
        if (Light(ring, x, 128) > bestLight) (best, bestLight) = (x - 128, Light(ring, x, 128));
      return best;
    }
    Check(Front(ModuleEffectBatch.RingStyle.Shock, 0.6f) > Front(ModuleEffectBatch.RingStyle.Shock, 0.2f) + 10,
      "A shock front must run outward");
    // The collapsing field's outer edge: the farthest pixel it still lights.
    int Outer(float progress)
    {
      var ring = Render(() => batch.Ring(middle, 80f, progress, tint, ModuleEffectBatch.RingStyle.Collapse));
      int outer = 0;
      for (int x = 128; x < 250; x++)
        if (Light(ring, x, 128) > ground + 30) outer = x - 128;
      return outer;
    }
    Check(Outer(0.7f) < Outer(0.2f) - 10, "A collapse must run inward");

    // A beam joins its two ends; a plume is bright at its head and gone at its tail.
    var beam = Render(() => batch.Beam(new Vector2(40, 128), new Vector2(216, 128), 0.2f, tint));
    Check(Enumerable.Range(45, 165).All(x => MaxLight(beam, (px, py) => px == x && py is > 122 and < 134) > ground + 100),
      "A beam must join its ends without gaps");
    Check(MaxLight(beam, (x, y) => y < 100 || y > 156) == ground, "A beam's light must stay inside its quad");
    var plume = Render(() => batch.Plume(new Vector2(60, 128), new Vector2(200, 128), tint, 8f));
    Check(MaxLight(plume, (x, y) => x is > 62 and < 80) > MaxLight(plume, (x, y) => x is > 170 and < 190) + 100,
      "A plume must thin out toward its tail");
    Check(MaxLight(plume, (x, y) => x > 204 || x < 40) <= ground + 6, "A plume ends at its tail and starts at its head");

    // The vortex: a black centre inside a bright disc, nothing past its radius.
    var vortex = Render(() => batch.Vortex(middle, 100f, 25f, new Color(160, 110, 255)));
    Check(Light(vortex, 128, 128) < 20, "The event horizon must black out what is behind it");
    Check(MaxLight(vortex, (x, y) => Away(x, y) is > 28f and < 60f) > ground + 150, "The disc around the horizon must glow");
    // Wells on top of each other settle into one disc: no brighter than a single one by much.
    var crowd = Render(() => { for (int i = 0; i < 8; i++) batch.Vortex(middle + new Vector2(i * 3, 0), 100f, 0f, new Color(160, 110, 255), i * 0.13f); });
    Check(crowd.Count(c => c.R >= 250 && c.G >= 250) < 600, "A crowd of wells must not wash out to white");
    Check(MaxLight(vortex, (x, y) => Away(x, y) > 101f) == ground, "The vortex must end at its radius");
    var well = Render(() => batch.Vortex(middle, 50f, 0f, new Color(160, 110, 255)));
    Check(Light(well, 128, 128) > ground + 100, "A well without a horizon glows at its centre");

    // The ghost points where it is heading.
    var ghost = Render(() => batch.Ghost(middle, Vector2.UnitX, 30f, new Color(70, 230, 255)));
    Check(MaxLight(ghost, (x, y) => x > 133 && Away(x, y) < 20f) > ground + 100, "The ghost must show its nose");
    Check(MaxLight(ghost, (x, y) => Away(x, y) > 31f) == ground, "The ghost must stay inside its quad");

    Console.WriteLine($"Module effects passed {count} checks: bolts, three ring styles, beams, plumes, the vortex and the ghost.");
    Exit();
  }

  // Every effect at several moments of its life, at twice world size on a starry backdrop.
  private void SaveSheet(Effect effect, string path)
  {
    const int width = 1920, height = 1200;
    using var target = new RenderTarget2D(GraphicsDevice, width, height);
    using var batch = new SpriteBatch(GraphicsDevice);
    using var pixel = new Texture2D(GraphicsDevice, 1, 1);
    pixel.SetData(new[] { Color.White });
    var effects = new ModuleEffectBatch(GraphicsDevice);
    var view = Matrix.CreateOrthographicOffCenter(0, width / 2f, height / 2f, 0, 0, 1);
    var random = new Random(7);
    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(8, 9, 20));
    batch.Begin();
    for (int star = 0; star < 900; star++)
      batch.Draw(pixel, new Rectangle(random.Next(width), random.Next(height), 2, 2),
        new Color(random.Next(60, 200), random.Next(60, 200), random.Next(120, 255)));
    batch.End();

    var storm = new Color(90, 165, 255);
    var thunder = new Color(190, 150, 255);
    // Row 1: a storm chain at three ages, and a Thunder God tree.
    float[] ages = [0.06f, 0.3f, 0.65f];
    for (int column = 0; column < 3; column++)
    {
      var origin = new Vector2(60 + column * 200, 60);
      var point = origin;
      var chain = new Random(3);
      for (int hop = 0; hop < 6; hop++)
      {
        float turn = (float)chain.NextDouble() * MathHelper.TwoPi;
        var next = point + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * (20f + (float)chain.NextDouble() * 30f);
        next = Vector2.Clamp(next, origin - new Vector2(40, 40), origin + new Vector2(120, 80));
        effects.Strike(point, next, ages[column], 0.31f + hop * 0.193f, storm);
        point = next;
      }
    }
    var tree = new Random(11);
    var frontier = new Queue<(Vector2 Position, int Depth)>();
    frontier.Enqueue((new Vector2(790, 90), 0));
    int arcs = 0;
    while (frontier.Count > 0 && arcs < 40)
    {
      var (position, depth) = frontier.Dequeue();
      for (int fork = 0; fork < 3 && arcs < 40; fork++)
      {
        float turn = (float)tree.NextDouble() * MathHelper.TwoPi;
        var next = position + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * (25f + (float)tree.NextDouble() * 70f);
        effects.Strike(position, next, 0.25f, ++arcs * 0.193f, thunder, 0.5f, 0.85f, forks: 1);
        if (depth < 3) frontier.Enqueue((next, depth + 1));
      }
    }

    // Row 2: the three ring styles through their lives.
    var styles = Enum.GetValues<ModuleEffectBatch.RingStyle>();
    Color[] ringTints = [Color.DodgerBlue, Color.MediumPurple, Color.Gold];
    float[] ringAges = [0.12f, 0.4f, 0.75f];
    for (int style = 0; style < styles.Length; style++)
      for (int age = 0; age < 3; age++)
        effects.Ring(new Vector2(60 + (style * 3 + age) * 104, 280), 44f, ringAges[age], ringTints[style], styles[style], 0.3f);

    // Row 3: beams, plumes, the World Eater, a buoy and the ghost.
    effects.Beam(new Vector2(30, 420), new Vector2(150, 390), 0.2f, new Color(60, 230, 255), 1.2f);
    effects.Beam(new Vector2(30, 470), new Vector2(230, 450), 0.3f, new Color(255, 200, 70), 1.8f);
    effects.Plume(new Vector2(60, 520), new Vector2(130, 520), new Color(120, 220, 255), 10f, 0.2f, 0.5f);
    effects.Plume(new Vector2(170, 520), new Vector2(226, 520), new Color(255, 140, 40), 5.5f, 0.6f);
    effects.Vortex(new Vector2(420, 460), 120f, 32f, new Color(160, 110, 255), 0.3f, 0.6f, 0.6f);
    effects.Vortex(new Vector2(640, 440), 240f, 42f, new Color(160, 110, 255), 0.7f, 0.6f, 0.6f);
    effects.Vortex(new Vector2(880, 400), 54f, 0f, new Color(160, 110, 255), 0.1f, 1f, 1f);
    effects.Vortex(new Vector2(880, 500), 34f, 0f, new Color(160, 110, 255), 0.1f, 1f, 0.5f, 0.6f);
    effects.Plume(new Vector2(262, 420), new Vector2(175, 420), new Color(70, 230, 255), 9f, 0.4f, 0.55f);
    effects.Ghost(new Vector2(270, 420), Vector2.UnitX, 30f, new Color(70, 230, 255));
    effects.Draw(effect, view, 3.37f);
    GraphicsDevice.SetRenderTarget(null);
    using var file = File.Create(path);
    target.SaveAsPng(file, width, height);
  }
}
