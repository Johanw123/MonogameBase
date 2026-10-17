using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShadowDusk.Compiler;
using ShadowDusk.Core;
using UntitledGemGame;
using UntitledGemGame.Screens;

// Exercises real floating-point render targets and VTF, without loading a game or touching saves.
internal sealed class GpuParticleChecks : Game
{
  private readonly string shaderPath, output;
  private readonly GraphicsDeviceManager manager;
  private bool ran;

  public GpuParticleChecks(string shaderPath, string output)
  {
    this.shaderPath = shaderPath;
    this.output = output;
    manager = new GraphicsDeviceManager(this)
    {
      GraphicsProfile = GraphicsProfile.HiDef,
      PreferredBackBufferWidth = 512, PreferredBackBufferHeight = 320,
      SynchronizeWithVerticalRetrace = false
    };
    IsFixedTimeStep = false;
  }

  public static byte[] Compile(string source)
  {
    var compiler = new EffectCompiler();
    var result = compiler.Compile(File.ReadAllText(source), new CompilerOptions { Target = PlatformTarget.OpenGL });
    if (result.IsFailure) throw new Exception(string.Join(Environment.NewLine, result.Error.Select(e => e.ToString())));
    Console.WriteLine("GPU particle shader compiled for MonoGame OpenGL using the game's ShadowDusk compiler.");
    return result.Value.Data;
  }

  protected override void Draw(GameTime gameTime)
  {
    if (ran) return;
    ran = true;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    using var effect = new Effect(GraphicsDevice, Compile(shaderPath));
    using var particles = new GpuParticleSystem(GraphicsDevice, effect, 4) { Drag = 0 };
    using var target = new RenderTarget2D(GraphicsDevice, 512, 320, false, SurfaceFormat.Color,
      DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    var projection = Matrix.CreateOrthographicOffCenter(-128, 128, 80, -80, 0, 1);
    int checks = 0;
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
      checks++;
    }
    void Near(float actual, float expected, string message)
      => Check(Math.Abs(actual - expected) < 0.003f, $"{message}: expected {expected}, got {actual}");
    void Reject(Action action, string message)
    {
      try { action(); }
      catch (ArgumentException) { checks++; return; }
      throw new Exception(message);
    }
    Vector4[] State(string name)
    {
      const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
      var textures = (RenderTarget2D[])typeof(GpuParticleSystem).GetField(name, flags)!.GetValue(particles)!;
      int read = (int)typeof(GpuParticleSystem).GetField("readIndex", flags)!.GetValue(particles)!;
      // Test-only readback. Production simulation/rendering never calls GetData.
      var state = new Vector4[particles.Capacity];
      textures[read].GetData(state);
      // Inactive rows are deliberately left untouched on the GPU. They are neither
      // simulated nor drawn, and are cleared lazily before the allocator reuses them.
      var rows = (int[])typeof(GpuParticleSystem).GetField("rowCounts", flags)!.GetValue(particles)!;
      for (int row = 0; row < rows.Length; row++)
        if (rows[row] == 0) Array.Clear(state, row * particles.TextureSize, particles.TextureSize);
      return state;
    }
    GpuParticleBurst Burst(Vector2 position, int count, Color color) => new(position, count, color)
    {
      Speed = Vector2.Zero, Radius = 0, Lifetime = Vector2.One, Size = new(8)
    };
    void Advance(float seconds)
    {
      while (seconds > 0)
      {
        float dt = Math.Min(seconds, 0.1f);
        particles.Update(dt);
        particles.Simulate();
        seconds -= dt;
      }
    }

    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(9, 13, 21));
    var viewport = new Viewport(7, 11, 490, 300);
    GraphicsDevice.Viewport = viewport;
    var scissor = new Rectangle(13, 17, 470, 280);
    GraphicsDevice.ScissorRectangle = scissor;
    var blend = BlendState.AlphaBlend;
    GraphicsDevice.BlendState = blend;
    particles.Simulate();
    Check(!particles.HasParticles, "An empty system must stay idle");
    Check(GraphicsDevice.GetRenderTargets()[0].RenderTarget == target, "Simulation must restore the scene target");
    Check(GraphicsDevice.Viewport.Equals(viewport) && GraphicsDevice.ScissorRectangle == scissor,
      "Simulation must restore viewport and scissor");
    Check(GraphicsDevice.BlendState == blend, "Simulation must restore blending");

    var initial = Burst(new Vector2(-50, 30), 3, new Color(31, 183, 240)) with { Velocity = new(20, -10) };
    Check(particles.Emit(initial) == 3, "Emit must accept an ordinary burst");
    Check(particles.PendingBursts == 1, "Emit must defer work until simulation");
    particles.Simulate();
    GraphicsDevice.SetRenderTarget(null);
    var positions = State("positions");
    var velocities = State("velocities");
    Check(positions.Count(p => p.Z > 0) == 3, "Spawning must affect exactly the requested texels");
    Near(positions[0].X, -50, "Signed world position must survive float target storage");
    Near(velocities[0].X, 20, "Initial velocity must be stored on the GPU");
    Near(positions[0].Z, 1, "Spawn lifetime");
    Check(velocities[0].W == 31 + 183 * 256 + 240 * 65536, "Packed RGB must survive float32 without rounding");

    particles.Acceleration = new Vector2(0, 20);
    Advance(0.1f);
    positions = State("positions");
    velocities = State("velocities");
    Near(positions[0].X, -48, "GPU position integration");
    Near(positions[0].Y, 29.1f, "Closed-form position integration");
    Near(positions[0].Z, 0.9f, "GPU lifetime integration");
    Near(velocities[0].Y, -8, "GPU acceleration");
    particles.Simulate();
    Check(State("positions").SequenceEqual(positions), "No elapsed time must freeze all particles");

    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(Color.Black);
    particles.Draw(projection);
    Check(GraphicsDevice.GetRenderTargets()[0].RenderTarget == target, "Particle drawing must keep the scene target");
    GraphicsDevice.SetRenderTarget(null);
    var pixels = new Color[512 * 320];
    target.GetData(pixels);
    Check(pixels.Any(c => c.B > 80 && c.G > 40 && c.R < c.G), "VTF draw must render the burst's colour");
    Check(!pixels.Take(512 * 10).Any(c => c != Color.Black), "Dead slots must not create phantom quads");
    // A normal MonoGame draw after the native VTF pass must still have correct shader/VAO state.
    using (var spriteBatch = new SpriteBatch(GraphicsDevice))
    using (var pixel = new Texture2D(GraphicsDevice, 1, 1))
    {
      pixel.SetData(new[] { Color.White });
      GraphicsDevice.SetRenderTarget(target);
      spriteBatch.Begin();
      spriteBatch.Draw(pixel, new Rectangle(30, 30, 8, 8), Color.Lime);
      spriteBatch.End();
      GraphicsDevice.SetRenderTarget(null);
      target.GetData(pixels);
      Check(pixels[32 * 512 + 32] == Color.Lime, "SpriteBatch must render correctly after the VTF draw");
    }

    particles.Emit(Burst(new Vector2(60, -25), 2, Color.Orange));
    particles.Simulate();
    positions = State("positions");
    Check(positions.Count(p => p.Z > 0) == 5, "A second burst must preserve existing particles");
    Near(positions[0].Z, 0.9f, "New spawning must not reset older lifetimes");
    Near(positions[3].X, 60, "New burst location");
    particles.Drag = 2;
    Advance(0.1f);
    Near(State("velocities")[0].X, 20 * MathF.Exp(-0.2f), "Frame-rate independent velocity damping");
    Advance(1.2f);
    Check(!particles.HasParticles && State("positions").All(p => p.Z == 0), "Expired particles must become idle");

    particles.Clear();
    particles.Emit(Burst(new Vector2(-40, -20), 12, Color.Red));
    particles.Simulate();
    particles.Emit(Burst(new Vector2(40, 20), 8, Color.Blue));
    particles.Simulate();
    positions = State("positions");
    Check(positions.Count(p => p.X == -40) == 8 && positions.Count(p => p.X == 40) == 8,
      "A wrapped burst must replace the oldest slots and preserve the others");
    Check(particles.Emit(Burst(Vector2.Zero, 500, Color.White)) == 16, "Oversized bursts must clamp to capacity");
    particles.Clear();
    for (int i = 0; i < GpuParticleSystem.MaxPendingBursts; i++)
      Check(particles.Emit(Burst(Vector2.Zero, 1, Color.White)) == 1, "The bounded pending queue must accept its advertised capacity");
    Check(particles.Emit(Burst(Vector2.Zero, 1, Color.White)) == 0, "A full queue must reject further bursts");
    particles.Clear();
    particles.Simulate();
    Check(!particles.HasParticles && State("positions").All(p => p == Vector4.Zero), "Clear must retire GPU rows and queued work");
    Reject(() => particles.Emit(initial with { Lifetime = new(0, 1) }), "Zero life must be rejected");
    Reject(() => particles.Emit(initial with { Position = new(float.NaN, 0) }), "Nonfinite positions must be rejected");
    Reject(() => particles.Update(float.NaN), "Nonfinite time must be rejected");
    Reject(() => particles.Emit(initial with { InnerRadius = 2, Radius = 1 }), "Inverted spawn rings must be rejected");

    // A large configured pool must not incur full-pool GPU work for a small burst.
    using (var sparse = new GpuParticleSystem(GraphicsDevice, effect, 256) { Drag = 0 })
    {
      sparse.Emit(Burst(new(-70, 0), 256, Color.Red) with { Lifetime = new(0.02f) });
      sparse.Emit(Burst(new(0, 0), 256, Color.Green) with { Lifetime = new(0.02f) });
      sparse.Emit(Burst(new(50, 0), 3, Color.Blue) with { Lifetime = new(10) });
      sparse.Update(0.05f);
      sparse.Simulate();
      Check(sparse.ActiveRows == 1 && sparse.ActiveSlotUpperBound == 3, "Expired rows must leave only the long-lived burst");
      sparse.Update(0.01f);
      sparse.Simulate();
      Check(sparse.SimulatedSlotsLastFrame == 256 && sparse.SimulationPassesLastFrame == 1,
        "A sparse 65K pool must update only one occupied row in one MRT pass");
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(Color.Black);
      sparse.Draw(projection);
      Check(sparse.DrawnSlotsLastFrame == 3 && sparse.DrawCallsLastFrame == 1,
        "VTF must submit only the occupied slots, including a nonzero first slot");
      GraphicsDevice.SetRenderTarget(null);
      target.GetData(pixels);
      Check(pixels.Any(c => c.B > 50) && !pixels.Any(c => c.R > 0 || c.G > 0),
        "A nonzero sparse draw range must fetch the blue burst without resurrecting expired rows");
      // Warm all driver/framework caches before checking managed allocation on the hot path.
      for (int i = 0; i < 10; i++) { sparse.Update(0.01f); sparse.Simulate(); sparse.Draw(projection); }
      long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
      for (int i = 0; i < 32; i++) { sparse.Update(0.01f); sparse.Simulate(); sparse.Draw(projection); }
      long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
      Console.WriteLine($"Particle simulation + draw managed allocations: {allocated / 32.0:F0} bytes/frame (65K pool, 3 live slots).");
      Check(allocated < 32 * 256, "The warmed particle hot path must avoid per-frame managed allocations");
      sparse.Clear();
      sparse.Simulate();
      sparse.Draw(projection);
      Check(sparse.SimulatedSlotsLastFrame == 0 && sparse.DrawnSlotsLastFrame == 0,
        "An idle pool must submit no simulation or drawing work");
    }

    foreach (float drag in new[] { 0f, 0.0001f, 2f })
    {
      using var once = new GpuParticleSystem(GraphicsDevice, effect, 4) { Drag = drag, Acceleration = new(13, -27) };
      using var divided = new GpuParticleSystem(GraphicsDevice, effect, 4) { Drag = drag, Acceleration = once.Acceleration };
      once.Emit(initial); divided.Emit(initial);
      once.Update(0.2f); once.Simulate();
      for (int i = 0; i < 20; i++) { divided.Update(0.01f); divided.Simulate(); }
      Vector4 ReadFirst(GpuParticleSystem system)
      {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var textures = (RenderTarget2D[])typeof(GpuParticleSystem).GetField("positions", flags)!.GetValue(system)!;
        int read = (int)typeof(GpuParticleSystem).GetField("readIndex", flags)!.GetValue(system)!;
        var values = new Vector4[system.Capacity];
        textures[read].GetData(values);
        return values[0];
      }
      var a = ReadFirst(once); var b = ReadFirst(divided);
      Near(a.X, b.X, "Drag integration must agree across frame rates (X)");
      Near(a.Y, b.Y, "Drag integration must agree across frame rates (Y)");
    }

    // Exercise every debug recipe, including inward motion and velocity-aligned streaks.
    using var gallery = new RenderTarget2D(GraphicsDevice, 1536, 960, false, SurfaceFormat.Color,
      DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    using var galleryBatch = new SpriteBatch(GraphicsDevice);
    GraphicsDevice.SetRenderTarget(gallery);
    GraphicsDevice.Clear(new Color(9, 13, 21));
    GraphicsDevice.SetRenderTarget(null);
    int tile = 0;
    using (var examples = new GpuParticleSystem(GraphicsDevice, effect))
      foreach (var example in Enum.GetValues<GpuParticleExample>())
      {
        examples.Clear();
        Check(GpuParticleExamples.Emit(examples, example, Vector2.Zero, 129) == 129,
          $"{example} must distribute exactly the requested count across its layers");
        examples.Update(0.08f);
        examples.Simulate();
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Black);
        examples.Draw(projection);
        GraphicsDevice.SetRenderTarget(null);
        target.GetData(pixels);
        Check(pixels.Any(c => c.R > 10 || c.G > 10 || c.B > 10), $"{example} must render visible particles");
        // Save representative ages; continuous recipes need several emissions to form a plume/trail.
        examples.Clear();
        bool continuous = example is GpuParticleExample.EngineExhaust or GpuParticleExample.MiningImpact or GpuParticleExample.RefuelStream;
        if (continuous)
          for (int i = 0; i < 12; i++)
          {
            GpuParticleExamples.Emit(examples, example, Vector2.Zero, 256, scale: 1.5f);
            examples.Update(0.04f);
            examples.Simulate();
          }
        else
        {
          GpuParticleExamples.Emit(examples, example, Vector2.Zero, 1024, scale: 1.5f);
          examples.Update(example == GpuParticleExample.ElectricDischarge ? 0.04f : 0.2f);
          examples.Simulate();
        }
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(new Color(9, 13, 21));
        examples.Draw(projection);
        GraphicsDevice.SetRenderTarget(gallery);
        galleryBatch.Begin();
        galleryBatch.Draw(target, new Rectangle(tile % 3 * 512, tile / 3 * 320, 512, 320), Color.White);
        galleryBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        tile++;
      }
    using (var stream = File.Create(output + "-emitters.png")) gallery.SaveAsPng(stream, gallery.Width, gallery.Height);

    CheckGameplayBudget(effect, Check);
    particles.Emit(initial);
    particles.Simulate();
    manager.PreferredBackBufferWidth = 514;
    manager.ApplyChanges();
    Check(!particles.HasParticles, "A device reset must drop transient particles");
    particles.Emit(initial);
    particles.Simulate();
    Check(State("positions").Count(p => p.Z > 0) == 3, "Spawning must work after a device reset");

    // Larger simultaneous bursts provide a useful visual preview of the actual renderer.
    using var preview = new GpuParticleSystem(GraphicsDevice, effect);
    preview.Emit(new(new Vector2(-75, 0), 900, new Color(255, 140, 35)) { Size = new(1.5f, 3f) });
    preview.Emit(new(new Vector2(0, 0), 900, new Color(65, 200, 255))
      { Speed = new(65, 85), Size = new(1.5f, 3f) });
    preview.Emit(new(new Vector2(75, 0), 900, new Color(190, 95, 255))
      { Direction = -MathHelper.PiOver2, Spread = 0.7f, Size = new(1.5f, 3f) });
    preview.Update(0.2f);
    preview.Simulate();
    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(9, 13, 21));
    preview.Draw(projection);
    GraphicsDevice.SetRenderTarget(null);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    using (var stream = File.Create(output + ".png")) target.SaveAsPng(stream, target.Width, target.Height);
    Console.WriteLine($"GPU particle checks passed ({checks} assertions); preview: {output}.png");
    Exit();
  }

  private void CheckGameplayBudget(Effect effect, Action<bool, string> check)
  {
    // Run the actual screen emission path with a real pool, without loading a world
    // or touching saves. Repeated Updates before Draw must also remain bounded.
    using var pool = new GpuParticleSystem(GraphicsDevice, effect, 128);
    var screen = (UntitledGemGameGameScreen)RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGameGameScreen));
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var type = typeof(UntitledGemGameGameScreen);
    void Set(string name, object value) => type.GetField(name, flags)!.SetValue(screen, value);
    int Count() => (int)type.GetField("gameplayParticleCount", flags)!.GetValue(screen)!;
    var refill = type.GetMethod("RefillGameplayParticleBudget", flags)!;
    var reset = type.GetMethod("ResetGameplayParticleBudget", flags)!;
    var emit = type.GetMethod("EmitGameplayParticles", flags)!;
    void Emit(int count, bool major = false) => emit.Invoke(screen,
      [new GpuParticleBurst(Vector2.Zero, count, Color.Gold), major]);
    Set("gpuParticles", pool);
    Set("gameplayParticlesEnabled", true);
    reset.Invoke(screen, null);
    for (int i = 0; i < 10000; i++) Emit(40);
    check(Count() == 960 && pool.PendingBursts == 24, "Rapid fire must cap particle count and layer work per Update");

    reset.Invoke(screen, null);
    pool.Clear();
    for (int frame = 0; frame < 5; frame++)
    {
      refill.Invoke(screen, [0.1f]);
      for (int i = 0; i < 100; i++) Emit(1);
    }
    check(pool.PendingBursts == 48, "Updates without a Draw must reserve sixteen descriptors for milestone effects");
    Emit(1000, major: true);
    check(Count() == 1000 && pool.PendingBursts == 49, "Milestone bursts must survive exhausted ordinary emission budgets");
    Set("gameplayParticlesEnabled", false);
    Emit(1000, major: true);
    check(pool.PendingBursts == 49, "Disabling gameplay particles must suppress milestone emissions too");
    Set("gameplayParticlesEnabled", true);

    int TotalAt(int fps)
    {
      reset.Invoke(screen, null);
      int total = 0;
      for (int i = 0; i < fps; i++)
      {
        pool.Clear();
        refill.Invoke(screen, [1f / fps]);
        Emit(int.MaxValue);
        total += Count();
      }
      return total;
    }
    int at30 = TotalAt(30), at120 = TotalAt(120);
    check(at30 <= 5760 && at120 <= 5760 && Math.Abs(at30 - at120) <= 160,
      "Emission rate must be bounded by elapsed time at both 30 and 120 fps");
    pool.Clear();
    reset.Invoke(screen, null);
    refill.Invoke(screen, [30f]);
    Emit(int.MaxValue);
    check(Count() == 960, "A long stall must not bank a particle emission storm");
  }
}
