using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// The fleet's module effects and the weapons' effects through the real shader
// (ModuleEffects.fx) and batch: every kind lights the pixels it should, stays inside its
// quad and only the vortex darkens.
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
    foreach (string technique in new[] { "ModuleBolt", "ModuleRing", "ModuleBeam", "ModuleVortex", "ModuleGhost",
      "ModuleJet", "ModuleCrack", "ModuleLance", "ModuleGlint", "ModuleSlug", "ModuleLink" })
      Check(effect.Techniques[technique] != null, $"ModuleEffects must offer {technique}");

    if (sheetPath != null) SaveSheet(effect, sheetPath);
    const int width = 256, height = 256;
    using var target = new RenderTarget2D(GraphicsDevice, width, height);
    var batch = new ModuleEffectBatch(GraphicsDevice);
    var view = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
    var grey = new Color(60, 60, 60);
    Color[] Render(Action queue, float time = 1f, Color? backdrop = null)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(backdrop ?? grey);
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

    // The weapons' effects. Forks, asked for through the bolt's tint, add light off the channel.
    int Total(Color[] pixels) => pixels.Sum(c => c.R + c.G + c.B - ground);
    var from = new Vector2(40, 128);
    var to = new Vector2(216, 128);
    Check(Total(Render(() => batch.Bolt(from, to, 0.3f, 0.2f, tint, forks: 2)))
      > Total(Render(() => batch.Bolt(from, to, 0.3f, 0.2f, tint))) + 2000, "Forks must add light to a bolt");
    // Without a direction the shader would light the whole quad.
    var nowhere = Render(() =>
    {
      batch.Plume(middle, middle, tint, 8f);
      batch.Bolt(middle, middle, 0.3f, 0.2f, tint);
      batch.Beam(middle, middle, 0f, tint);
      batch.Jet(middle, middle, 0.3f, tint, 8f);
      batch.Lance(middle, middle, 0.3f, tint, 8f);
    });
    Check(MaxLight(nowhere, (x, y) => true) == ground, "An effect with nowhere to go must draw nothing");

    // A crowd of lean beams meeting at one point stays coloured where regular beams wash out.
    Color[] Crowd(float lean) => Render(() =>
    {
      for (int i = 0; i < 40; i++)
        batch.Beam(middle + new Vector2(MathF.Cos(i * 0.157f), MathF.Sin(i * 0.157f)) * 110f, middle, 0f, tint, 0.7f, i * 0.13f, 0.9f, lean);
    });
    int White(Color[] pixels) => pixels.Count(c => c.R >= 250 && c.G >= 250 && c.B >= 250);
    Check(White(Crowd(1f)) * 4 < White(Crowd(0f)), "A crowd of lean beams must not wash out where it meets");

    // A geyser is white hot at its mouth, climbs as it ages and is gone when spent.
    var mouth = new Vector2(128, 236);
    var tip = new Vector2(128, 30);
    var young = Render(() => batch.Jet(mouth, tip, 0.04f, tint, 8f));
    var grown = Render(() => batch.Jet(mouth, tip, 0.5f, tint, 8f));
    Check(MaxLight(young, (x, y) => Vector2.Distance(new Vector2(x, y), mouth) < 10f) > ground + 400, "A geyser must be white hot at its mouth");
    Check(MaxLight(young, (x, y) => y < 130) <= ground + 6 && MaxLight(grown, (x, y) => y is > 70 and < 130) > ground + 40,
      "A geyser must climb as it ages");
    Check(MaxLight(grown, (x, y) => x is < 80 or > 176) == ground, "A geyser must stay inside its quad");
    Check(MaxLight(Render(() => batch.Jet(mouth, tip, 1f, tint, 8f)), (x, y) => true) <= ground + 6, "A spent geyser is gone");

    // A molten crack is white hot along its path, joins at a bend without doubling its glow
    // there and narrows to nothing at its tip.
    var magma = new Color(255, 110, 30);
    Vector2[] bent = [new(40, 150), new(128, 150), new(190, 88)];
    var crack = Render(() => batch.Crack(bent, magma, 2f));
    Check(MaxLight(crack, (x, y) => x == 80 && y is > 146 and < 154) > ground + 400
      && MaxLight(crack, (x, y) => x == 160 && y is > 112 and < 124) > ground + 300, "A crack must be white hot along both legs");
    // Its glow at the same distance from the path: beside the straight leg, and outside the bend.
    Check(Light(crack, 131, 159) < Light(crack, 80, 159) + 60, "A crack's pieces must not overlap at a bend");
    Check(Light(crack, 131, 159) > ground + 30, "A crack's glow must carry round a bend");
    Check(MaxLight(crack, (x, y) => Vector2.Distance(new Vector2(x, y), bent[2]) < 2.5f) < MaxLight(crack, (x, y) => x == 80) - 200,
      "A crack must narrow to nothing at its tip");
    Check(MaxLight(crack, (x, y) => y > 175 || x < 20) == ground, "A crack's light must stay inside its pieces");

    // The warp streak joins where the drive fired to where the ship went, then lets go of its start.
    var fired = new Vector2(128, 240);
    var gone = new Vector2(128, 16);
    var streak = Render(() => batch.Lance(fired, gone, 0.05f, tint, 6f));
    Check(Enumerable.Range(24, 208).All(y => MaxLight(streak, (px, py) => py == y && px is > 124 and < 132) > ground + 150),
      "A fresh warp streak must join its ends without gaps");
    var fading = Render(() => batch.Lance(fired, gone, 0.6f, tint, 6f));
    Check(MaxLight(fading, (x, y) => y > 225) <= ground + 6 && MaxLight(fading, (x, y) => y is > 30 and < 60) > ground + 40,
      "An ageing warp streak must let go of where it started first");
    Check(MaxLight(streak, (x, y) => x is < 100 or > 156) == ground, "A warp streak must stay inside its quad");

    // A glint is brightest at its heart, with long rays along its heading and short ones between.
    var glint = Render(() => batch.Glint(middle, 60f, tint));
    Check(Light(glint, 128, 128) > ground + 500, "A glint must be bright at its heart");
    Check(Light(glint, 158, 128) > Light(glint, 149, 149) + 30 && Light(glint, 128, 98) > Light(glint, 149, 149) + 30,
      "A glint's long rays must run along and across its heading");
    Check(MaxLight(glint, (x, y) => Away(x, y) > 61f) == ground, "A glint must stay inside its size");

    // A lodged slug is a solid rod: it covers what is behind it, lands white hot, cools to
    // steel with a bright tail, and lights up as a pulse runs through it.
    var slugRoot = new Vector2(128, 150);
    var slugTail = new Vector2(128, 131);
    var conductor = new Color(150, 205, 255);
    int MinLight(Color[] pixels, Func<int, int, bool> where)
    {
      int min = int.MaxValue;
      for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
          if (where(x, y)) min = Math.Min(min, Light(pixels, x, y));
      return min;
    }
    bool OnBody(int x, int y) => x is >= 126 and <= 130 && y is >= 143 and <= 147;
    var cold = Render(() => batch.Slug(slugRoot, slugTail, 1f, 0f, conductor, 2.3f));
    // On a bright ground its steel shows dark.
    var onLight = Render(() => batch.Slug(slugRoot, slugTail, 1f, 0f, conductor, 2.3f), backdrop: new Color(230, 230, 230));
    Check(MinLight(onLight, OnBody) < 690 - 300, "A slug's body must cover what is behind it");
    Check(MaxLight(cold, (x, y) => Vector2.Distance(new Vector2(x, y), slugTail) < 2f) > ground + 300,
      "A slug's tail must glow");
    var landed = Render(() => batch.Slug(slugRoot, slugTail, 0.02f, 0f, conductor, 2.3f));
    Check(MinLight(landed, OnBody) > ground + 300, "A slug must land white hot");
    Check(Total(Render(() => batch.Slug(slugRoot, slugTail, 1f, 0.5f, conductor, 2.3f))) > Total(cold) + 3000,
      "A pulse must light the slug it runs through");
    foreach (var slug in new[] { cold, landed })
      Check(MaxLight(slug, (x, y) => x is < 112 or > 144 || y is < 115 or > 166) == ground
        && MinLight(slug, (x, y) => x is < 112 or > 144 || y is < 115 or > 166) == ground, "A slug must stay inside its quad");

    // A Gem Link: the spark starts where it jumps from, crosses, and the thread it leaves lets
    // go of its start first.
    Check(MaxLight(Render(() => batch.Link(from, to, 0.04f, tint, 0.9f)), (x, y) => x > 150) == ground
      && MaxLight(Render(() => batch.Link(from, to, 0.04f, tint, 0.9f)), (x, y) => x < 110) > ground + 200,
      "A link's spark starts where it jumps from");
    var linked = Render(() => batch.Link(from, to, 0.3f, tint, 0.9f));
    Check(Enumerable.Range(45, 165).All(x => MaxLight(linked, (px, py) => px == x && py is > 124 and < 132) > ground + 60),
      "A link must join its ends once the spark has crossed");
    var letting = Render(() => batch.Link(from, to, 0.8f, tint, 0.9f));
    Check(MaxLight(letting, (x, y) => x < 70) <= ground + 6 && MaxLight(letting, (x, y) => x is > 185 and < 212) > ground + 20,
      "An ageing link must let go of where it started first");
    Check(MaxLight(linked, (x, y) => y is < 116 or > 140) == ground, "A link's light must stay inside its quad");
    Check(MaxLight(Render(() => batch.Link(from, to, 1f, tint, 0.9f)), (x, y) => true) <= ground + 6, "A spent link is gone");
    Check(ClickUtility.HopDelay(0) == 0f && ClickUtility.HopDelay(2) > ClickUtility.HopDelay(1) && ClickUtility.HopDelay(96) < 0.33f,
      "The spark must reach each hop of a Gem Link in turn, and the last within a third of a second");

    Console.WriteLine($"Module effects passed {count} checks: bolts and their forks, three ring styles, beams, plumes, the vortex, "
      + "the ghost, geysers, molten cracks, the warp streak, glints, lodged slugs and Gem Links.");
    Exit();
  }

  // Every effect at several moments of its life, at twice world size on a starry backdrop.
  private void SaveSheet(Effect effect, string path)
  {
    const int width = 1920, height = 2040;
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

    // Row 4: the ship's weapons. A rocket's exhaust, flak tracers, a kamikaze drone's and a
    // doomsday drone's trail; then forked harpoon lightning at three ages and a small crackle.
    effects.Plume(new Vector2(90, 640), new Vector2(30, 640), new Color(35, 200, 255), 2.9f, 0.3f);
    for (int pellet = 0; pellet < 4; pellet++)
      effects.Plume(new Vector2(190 + pellet * 9, 628 + pellet * 9), new Vector2(150 + pellet * 9, 624 + pellet * 7),
        new Color(255, 160, 40), 1.1f, pellet * 0.27f);
    effects.Plume(new Vector2(300, 640), new Vector2(240, 650), new Color(255, 80, 50), 3.2f, 0.7f, 0.8f);
    effects.Plume(new Vector2(400, 640), new Vector2(330, 630), new Color(255, 40, 30), 5.5f, 0.1f, 0.9f);
    var arc = new Color(70, 220, 255);
    for (int column = 0; column < 3; column++)
      effects.Bolt(new Vector2(470 + column * 150, 600), new Vector2(560 + column * 150, 690), ages[column],
        0.17f + column * 0.31f, arc, 0.2f, 1f, forks: 2);
    effects.Bolt(new Vector2(905, 640), new Vector2(912, 646), 0.3f, 0.4f, arc, 0.1f, 0.8f, scale: 0.3f);

    // Row 5: a geyser through its life, a molten crack with a branch, the warp streak
    // through its life and glints (steady and twinkling).
    float[] jetAges = [0.08f, 0.35f, 0.7f];
    for (int column = 0; column < 3; column++)
      effects.Jet(new Vector2(50 + column * 80, 835), new Vector2(50 + column * 80, 715), jetAges[column],
        new Color(255, 150, 70), 6f, column * 0.37f);
    var kinks = new Random(5);
    var fault = new Vector2[15];
    for (int step = 0; step < fault.Length; step++)
      fault[step] = new Vector2(300 + step * 15, 790 - step * 4 + (step == 0 ? 0 : (float)(kinks.NextDouble() - 0.5) * 16f));
    effects.Crack(fault, new Color(255, 110, 30), 1.6f);
    effects.Crack([fault[5], fault[5] + new Vector2(8, -14), fault[5] + new Vector2(22, -22), fault[5] + new Vector2(27, -38)],
      new Color(255, 110, 30), 1f, 0.9f);
    for (int column = 0; column < 3; column++)
      effects.Lance(new Vector2(580 + column * 60, 835), new Vector2(580 + column * 60, 560), jetAges[column],
        new Color(110, 200, 255), 7f, column * 0.3f);
    effects.Glint(new Vector2(800, 760), 14f, Color.Cyan, 0.2f);
    effects.Glint(new Vector2(850, 760), 30f, new Color(110, 200, 255), 0.5f);
    effects.Glint(new Vector2(905, 800), 12f, Color.Cyan, 0.9f, 0.8f, 1f, new Vector2(1, 1));
    // A crowd of lean beams meeting at one point, as the Graviton Cascade's links do.
    var links = new Random(9);
    for (int link = 0; link < 60; link++)
      effects.Beam(new Vector2(780 + (float)links.NextDouble() * 170, 620 + (float)links.NextDouble() * 100),
        new Vector2(940, 720), 0f, new Color(60, 230, 255), 0.5f, link * 0.13f, 0.7f, lean: 1f);
    // Row 6: lodged slugs on a lit ground and on a dark one (just landed, cooling, cold, and with
    // a pulse entering, half way and leaving), then a Gem Link through its life and a click's pulse.
    batch.Begin();
    batch.Draw(pixel, new Rectangle(0, 1880, 760, 160), new Color(70, 220, 240));
    batch.Draw(pixel, new Rectangle(380, 1880, 380, 160), new Color(60, 50, 95));
    batch.End();
    var conductor = new Color(150, 205, 255);
    (float Cooled, float Charge)[] slugStates = [(0.02f, 0f), (0.35f, 0f), (1f, 0f), (1f, 0.9f), (1f, 0.5f), (1f, 0.12f)];
    for (int slug = 0; slug < slugStates.Length; slug++)
      for (int ground = 0; ground < 2; ground++)
      {
        var root = new Vector2(30 + slug * 28 + ground * 190, 950);
        effects.Slug(root, root + new Vector2(0.25f * (slug - 2.5f), -19f), slugStates[slug].Cooled, slugStates[slug].Charge,
          conductor, 2.3f, slug * 0.29f);
      }
    float[] linkAges = [0.12f, 0.3f, 0.55f, 0.8f];
    Vector2[] chainPath = [new(22, -14), new(40, 6), new(28, 30), new(52, 44), new(74, 26), new(66, -4), new(88, -18)];
    for (int column = 0; column < linkAges.Length; column++)
    {
      var point = new Vector2(410 + column * 105, 905);
      for (int hop = 0; hop < chainPath.Length; hop++)
      {
        var next = new Vector2(410 + column * 105, 905) + chainPath[hop];
        float age = linkAges[column] - ClickUtility.HopDelay(hop) / 0.5f;
        if (age >= 0f)
        {
          effects.Link(point, next, age, column == 3 ? Color.Gold : Color.Aquamarine, 0.9f, hop * 0.137f);
          float landed = (age - 0.22f) / 0.78f;
          if (landed > 0f)
            effects.Glint(next, MathHelper.Lerp(9f, 15f, 1f - landed), column == 3 ? Color.Gold : Color.Aquamarine, hop * 0.137f,
              (1f - landed) * (1f - landed), heading: next - point);
        }
        point = next;
      }
    }
    // A click's pulse, and the Click Shockwave's wider one, as ClickUtility draws them.
    effects.Ring(new Vector2(880, 900), 18f, 0.3f, Color.Aquamarine, ModuleEffectBatch.RingStyle.Shock, 0.3f,
      ClickUtility.PulseIntensity, ClickUtility.PulseWidth);
    effects.Ring(new Vector2(880, 965), 90f, 0.4f, Color.Aquamarine, ModuleEffectBatch.RingStyle.Shock, 0.3f,
      ClickUtility.PulseIntensity, ClickUtility.PulseWidth);
    effects.Draw(effect, view, 3.37f);
    GraphicsDevice.SetRenderTarget(null);
    using var file = File.Create(path);
    target.SaveAsPng(file, width, height);
  }
}
