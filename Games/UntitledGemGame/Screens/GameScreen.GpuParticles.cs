using System;
using ImGuiNET;
using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private GpuParticleSystem gpuParticles;
  private int particleTestCount = 2048;
  private int particlePoolChoice = 1;
  private int particleTestOrigin = 0;
  private bool repeatParticleTests, moveParticleEmitter = false;
  private float particleTestTimer, particleTestAge;
  private float particleTestInterval = 0.8f, particleTestDirection = 0, particleTestScale = 1;
  private int particleTestStyle = 0, particleLastAccepted;
  private Vector2 particleTestPosition;
  private static readonly int[] ParticlePoolSizes = [128, 256, 512, 1024];

  private GpuParticleSystem DebugParticleSystem()
  {
#if KNI_WEB
    return null;
#else
    if (EffectCache.GpuParticlesFx?.IsLoaded != true || EffectCache.GpuParticlesFx.IsFailed) return null;
    return gpuParticles ??= new GpuParticleSystem(GraphicsDevice, EffectCache.GpuParticlesFx.Value, ParticlePoolSizes[particlePoolChoice]);
#endif
  }

  public int EmitParticleBurst(in GpuParticleBurst burst) => DebugParticleSystem()?.Emit(burst) ?? 0;

  // Recipes are exposed to capture/tests; gameplay does not trigger them.
  public int EmitParticleExample(GpuParticleExample example, Vector2 position, int count = 2048)
  {
    var particles = DebugParticleSystem();
    return particles == null ? 0 : GpuParticleExamples.Emit(particles, example, position, count,
      particleTestDirection * MathHelper.Pi / 180, particleTestScale);
  }

  private void EmitParticleTest() => particleLastAccepted = EmitParticleExample(
    (GpuParticleExample)particleTestStyle, particleTestPosition, particleTestCount);

  private void UpdateGpuParticles(float dt)
  {
    if (!repeatParticleTests && !showDebugGUI)
    {
      gpuParticles?.Update(dt);
      return;
    }
    // Capture world placement during Update, while the camera still has the window viewport.
    particleTestAge += dt;
    particleTestPosition = particleTestOrigin switch
    {
      1 => HomeBasePos + new Vector2(0, 42),
      2 => PlanetPos + new Vector2(-PlanetRadius, 0),
      3 => m_camera.ScreenToWorld(GameInput.Mouse.Position.ToVector2()),
      _ => m_camera.ScreenToWorld(BaseGame.ViewportCenter)
    };
    if (moveParticleEmitter)
      particleTestPosition += new Vector2(MathF.Cos(particleTestAge * 1.7f), MathF.Sin(particleTestAge * 1.7f)) * 24;
    if (repeatParticleTests)
    {
      particleTestTimer -= dt;
      // A bounded catch-up keeps the CPU cost independent of particle count and avoids emission storms after a stall.
      for (int i = 0; i < 4 && particleTestTimer <= 0; i++)
      {
        EmitParticleTest();
        particleTestTimer += particleTestInterval;
      }
      particleTestTimer = Math.Max(0, particleTestTimer);
    }
    gpuParticles?.Update(dt);
  }

  private void ClearGpuParticles()
  {
    repeatParticleTests = false;
    particleTestTimer = particleTestAge = 0;
    particleLastAccepted = 0;
    gpuParticles?.Clear();
  }

  private void DrawGpuParticleDebug()
  {
    if (!ImGui.CollapsingHeader("GPU particles")) return;
#if KNI_WEB
    ImGui.TextWrapped("The texture-based GPU particle prototype is currently available in the desktop build.");
#else
    ImGui.TextWrapped("Preview effects only. These emitters do not change gameplay or saves.");
    if (ImGui.Combo("Particle pool", ref particlePoolChoice, "16,384\0" + "65,536\0" + "262,144\0" + "1,048,576\0"))
    {
      ClearGpuParticles();
      gpuParticles?.Dispose();
      gpuParticles = null;
      particleTestCount = Math.Min(particleTestCount, ParticlePoolSizes[particlePoolChoice] * ParticlePoolSizes[particlePoolChoice]);
    }
    int capacity = ParticlePoolSizes[particlePoolChoice] * ParticlePoolSizes[particlePoolChoice];
    ImGui.SliderInt("Particles per emission", ref particleTestCount, 1, capacity, "%d", ImGuiSliderFlags.Logarithmic);
    if (ImGui.Combo("Emitter", ref particleTestStyle, GpuParticleExamples.Labels))
    {
      particleTestTimer = 0;
      particleTestInterval = GpuParticleExamples.Interval((GpuParticleExample)particleTestStyle);
    }
    ImGui.TextWrapped(GpuParticleExamples.Description((GpuParticleExample)particleTestStyle));
    ImGui.Combo("Emitter position", ref particleTestOrigin, "View centre\0Homebase engine\0Planet edge\0Mouse\0");
    ImGui.SliderFloat("Direction offset (degrees)", ref particleTestDirection, -180, 180);
    ImGui.SliderFloat("Effect scale", ref particleTestScale, 0.25f, 4);
    ImGui.SliderFloat("Emission interval", ref particleTestInterval, 1f / 60, 2f, "%.3f s", ImGuiSliderFlags.Logarithmic);
    ImGui.Checkbox("Move emitter in a circle", ref moveParticleEmitter);
    ImGui.BeginDisabled(GameMain.IsPaused || !GameStarted || m_prestiging || m_postPrestige);
    if (ImGui.Button("Emit once")) EmitParticleTest();
    if (ImGui.Checkbox("Run emitter", ref repeatParticleTests)) particleTestTimer = 0;
    ImGui.EndDisabled();
    ImGui.SameLine();
    if (ImGui.Button("Clear particles")) ClearGpuParticles();
    ImGui.Text($"Pool state: {capacity * 64 / 1048576f:0.##} MiB; last emission accepted: {particleLastAccepted:N0}");
    if (gpuParticles == null) return;
    ImGui.Text($"Occupied slots (upper bound): {gpuParticles.ActiveSlotUpperBound:N0} / {capacity:N0}");
    ImGui.Text($"Simulated texels: {gpuParticles.SimulatedSlotsLastFrame:N0}; submitted quads: {gpuParticles.DrawnSlotsLastFrame:N0}");
    ImGui.Text($"Simulation regions: {gpuParticles.SimulationPassesLastFrame}; draw regions: {gpuParticles.DrawCallsLastFrame}; queued bursts: {gpuParticles.PendingBursts}");
    float drag = gpuParticles.Drag;
    if (ImGui.SliderFloat("Particle drag", ref drag, 0, 5)) gpuParticles.Drag = drag;
    float gravity = gpuParticles.Acceleration.Y;
    if (ImGui.SliderFloat("Particle gravity", ref gravity, -150, 150)) gpuParticles.Acceleration = new Vector2(0, gravity);
#endif
  }
}
