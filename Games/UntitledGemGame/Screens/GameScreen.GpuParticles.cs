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

  internal void SetParticlePoolChoice(int choice)
  {
    choice = Math.Clamp(choice, 0, ParticlePoolSizes.Length - 1);
    if (particlePoolChoice == choice) return;
    ClearGpuParticles();
    gpuParticles?.Dispose();
    gpuParticles = null;
    particlePoolChoice = choice;
    particleTestCount = Math.Min(particleTestCount, ParticlePoolSizes[particlePoolChoice] * ParticlePoolSizes[particlePoolChoice]);
  }

  internal void SetGameplayParticleMultiplier(float multiplier)
  {
    gameplayParticleMultiplier = Math.Clamp(multiplier, 0f, 5000f);
    if (gameplayParticleMultiplier > 1f && particlePoolChoice < 3)
      SetParticlePoolChoice(3);
  }

  private GpuParticleSystem GetParticleSystem()
  {
#if KNI_WEB
    return null;
#else
    if (gpuParticles != null) return gpuParticles;
    if (EffectCache.GpuParticlesFx?.IsLoaded != true || EffectCache.GpuParticlesFx.IsFailed) return null;
    return gpuParticles = new GpuParticleSystem(GraphicsDevice, EffectCache.GpuParticlesFx.Value, ParticlePoolSizes[particlePoolChoice]);
#endif
  }

  public int EmitParticleBurst(in GpuParticleBurst burst) => GetParticleSystem()?.Emit(burst) ?? 0;

  // Preview recipes are exposed to capture/tests. Gameplay has its own tuned effects.
  public int EmitParticleExample(GpuParticleExample example, Vector2 position, int count = 2048, float? scale = null)
  {
    var particles = GetParticleSystem();
    if (particles == null) return 0;
    if (count > 20000) particles.Drag = 0.15f;
    return GpuParticleExamples.Emit(particles, example, position, count,
      particleTestDirection * MathHelper.Pi / 180, scale ?? particleTestScale);
  }

  private void EmitParticleTest() => particleLastAccepted = EmitParticleExample(
    (GpuParticleExample)particleTestStyle, particleTestPosition, particleTestCount);

  private void UpdateGpuParticles(float dt)
  {
    if (gpuParticles != null && !showDebugGUI)
      gpuParticles.Drag = (gameplayParticleMultiplier > 1.001f || thrusterParticleMultiplier > 1.001f) ? 0.15f : 1.5f;
    RefillGameplayParticleBudget(dt);
    UpdateShipThrusterParticles(dt);
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
    ResetGameplayParticleBudget();
    gpuParticles?.Clear();
  }

  private void DrawGpuParticleDebug()
  {
    if (!ImGui.CollapsingHeader("GPU particles")) return;
#if KNI_WEB
    ImGui.TextWrapped("The texture-based GPU particle prototype is currently available in the desktop build.");
#else
    ImGui.Checkbox("Gameplay particle effects", ref gameplayParticlesEnabled);
    float mult = gameplayParticleMultiplier;
    if (ImGui.SliderFloat("Gameplay particle multiplier", ref mult, 1f, 2000f, "%.1fx", ImGuiSliderFlags.Logarithmic))
      SetGameplayParticleMultiplier(mult);
    ImGui.TextWrapped("Weapon impacts, core drills, shell/core breaks, crash sparks and ship exhaust use GPU particles. Signal reveals have a separate UI pool. The emitters below are extra previews.");
    ImGui.Text($"Gameplay this update: {gameplayParticleCount:N0} particles, {gameplayParticleLayers} layers");
    int poolChoice = particlePoolChoice;
    if (ImGui.Combo("Particle pool", ref poolChoice, "16,384\0" + "65,536\0" + "262,144\0" + "1,048,576\0"))
      SetParticlePoolChoice(poolChoice);
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
