using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

// The core fracture's extra spectacle (GameScreen.CoreFracture.cs has the event itself):
//  - Tremor (0-1.8 s): the planet heats up under its spreading crack (PlanetCollapse.fx),
//    magma veins glow through the rock and the view starts to tremble.
//  - Swallow (1.8-3.0 s): gravity turns inward. Space pinches around the planet and swirls
//    as it inhales, a ripple runs inward, and streams of light fall in from off screen.
//  - Build (3.0-3.4 s): the core glows through, sparks crackle out of the crack and a ring
//    of heat closes in.
//  - Eruption (3.4 s): the pinch snaps back into a refracting shock front (BlackHoleWarp.fx)
//    with a flash at the core; a magma geyser jets out of the crack with globs, hot gas, a
//    ring of sparks and shard-coloured glitter, and the crack keeps spitting for a second.
//  - The shard leaves a glittering trail as it flies out.
// Particles go straight to the GPU pool at fixed counts (EmitEventParticles).
public partial class UntitledGemGameGameScreen
{
  private const float FractureEffectsEnd = FractureEruption + 1.3f;
  private float fractureParticleClock;
  private float shardTrailClock;

  // The planet's heat, glowing veins and inhale during a fracture (PlanetCollapse.fx).
  private bool FracturePlanetState(out float crack, out float heat, out float crush)
  {
    crack = heat = crush = 0f;
    if (!fractureActive || fractureTime >= FractureEffectsEnd) return false;
    float t = fractureTime;
    float cool = 1f - Smooth(FractureEruption + 0.1f, FractureEffectsEnd, t);
    float pulse = planetHitPulse * planetHitPulse;
    heat = Math.Min(1f, 0.45f * Smooth(0.1f, FractureSwallowStart, t)
      + 0.35f * Smooth(FractureSwallowStart, FractureEruption, t) + 0.25f * pulse);
    crack = 0.25f * Smooth(0.5f, FractureSwallowStart, t) + 0.3f * Smooth(FractureSwallowStart, FractureEruption, t);
    if (t >= FractureEruption)
    {
      // Burst open white-hot, then the rock cools back down.
      heat = Math.Max(heat, 1f - Smooth(FractureEruption, FractureEruption + 0.35f, t) * 0.4f) * cool;
      crack *= cool;
    }
    // The planet draws in a breath as it swallows, and lets it out in the eruption.
    crush = (0.16f * Smooth(FractureSwallowStart, FractureSwallowEnd, t)
      + 0.07f * Smooth(FractureSwallowEnd, FractureEruption, t))
      * (1f - Smooth(FractureEruption, FractureEruption + 0.15f, t));
    return true;
  }

  // Gravity, the inward ripple, the eruption's shock front, its flash and the trembling.
  private bool FractureLens(float units, out WorldLens lens)
  {
    lens = default;
    if (!fractureActive || fractureTime >= FractureEffectsEnd) return false;
    float t = fractureTime;
    float r = PlanetRadius * units;
    float release = 1f - Smooth(FractureEruption, FractureEruption + 0.12f, t);
    lens.LensRadius = r * (0.2f * Smooth(0.3f, FractureSwallowStart, t)
      + 0.3f * Smooth(FractureSwallowStart, FractureEruption, t)) * release;
    // The planet itself stays unbent; its shader does the inhale.
    lens.LensCore = r * planetVisualSize / PlanetSizeMultiplier;
    lens.Twist = 0.25f * Smooth(FractureSwallowStart, FractureSwallowEnd, t) * release;
    lens.SwirlReach = r * 3f;
    lens.ShockWidth = 0.05f;
    if (t >= FractureSwallowStart && t < FractureSwallowStart + 0.9f)
    {
      // A ripple running inward as gravity turns.
      float progress = (t - FractureSwallowStart) / 0.9f;
      float wave = MathF.Sin(MathF.PI * progress);
      lens.ShockRadius = r + 1.3f * (1f - progress);
      lens.ShockStrength = 0.012f * wave;
      lens.ShockGlow = 0.1f * wave;
    }
    else if (t >= FractureEruption && t < FractureEruption + 1f)
    {
      float progress = t - FractureEruption;
      float remaining = 1f - progress;
      lens.ShockRadius = r + 1.8f * (1f - MathF.Pow(remaining, 2.2f));
      lens.ShockWidth = 0.02f + 0.05f * progress;
      lens.ShockStrength = 0.035f * MathF.Pow(remaining, 1.5f);
      lens.ShockGlow = 0.35f * remaining * remaining;
    }
    float build = 0.35f * Smooth(FractureSwallowEnd, FractureEruption, t) * release;
    float flash = t >= FractureEruption ? 0.55f * (1f - Smooth(FractureEruption, FractureEruption + 0.5f, t)) : 0f;
    lens.Glow = build + flash;
    lens.GlowRadius = r * (1.1f + 0.8f * Smooth(FractureEruption, FractureEruption + 0.4f, t));
    lens.GlowColor = Color.Lerp(FractureColor, FractureHotColor, 0.3f + 0.6f * Smooth(FractureSwallowEnd, FractureEruption + 0.1f, t)).ToVector3();
    lens.Rattle = t < FractureEruption
      ? 0.0008f * Smooth(0f, FractureSwallowStart, t) + 0.002f * Smooth(FractureSwallowStart, FractureEruption, t)
      : 0.006f * (1f - Smooth(FractureEruption, FractureEruption + 0.5f, t));
    return true;
  }

  // Runs each update of the fracture event, after its own beats.
  private void UpdateFractureEffects(float previous)
  {
    float t = fractureTime;
    bool Crossed(float moment) => previous < moment && t >= moment;
    if (Crossed(FractureSwallowEnd))
      EmitInfall(PlanetPos, 600, FractureHotColor, PlanetRadius * 2.6f, PlanetRadius * 2.2f,
        PlanetRadius * 0.9f, FractureEruption - FractureSwallowEnd - 0.02f, new(0.45f, 1f));

    fractureParticleClock += t - previous;
    const float interval = 0.07f;
    if (fractureParticleClock < interval) return;
    fractureParticleClock %= interval;
    if (t >= FractureSwallowStart && t < FractureSwallowEnd)
    {
      // Light from across the field streams into the planet as it inhales.
      bool hot = Random.Shared.NextSingle() < 0.5f;
      float reach = ViewReach();
      EmitInfall(PlanetPos, 90, hot ? new Color(255, 160, 70) : Color.Lerp(FractureHotColor, ShellLightColor, 0.4f),
        reach * 1.1f, reach * 0.5f, PlanetRadius, 0.6f, new(0.4f, 1f), 0.3f);
    }
    else if (t >= FractureSwallowEnd && t < FractureEruption)
      EmitCrackSparks(30 + (int)(60 * Smooth(FractureSwallowEnd, FractureEruption, t)), 1f);
    else if (t >= FractureEruption && t < FractureEruption + 1f)
      // The crack keeps spitting for a moment after the blast.
      EmitCrackSparks((int)(70 * (1f - (t - FractureEruption))), 1.6f);
  }

  // Hot sparks crackling out of the newest crack.
  private void EmitCrackSparks(int count, float strength)
  {
    if (count <= 0) return;
    var focus = CrackFocusWorld();
    EmitEventParticles(new GpuParticleBurst(focus, count, Color.Lerp(FractureColor, FractureHotColor, 0.5f))
    {
      Direction = ParticleOutwardAngle(focus), Spread = 1.3f, Radius = PlanetRadius * 0.08f,
      Speed = new Vector2(60f, 200f) * strength, Lifetime = new(0.2f, 0.6f), Size = new(0.4f, 1f), Streak = true
    });
  }

  // On top of the eruption's own particles: a geyser out of the crack, globs of magma,
  // hot gas, a ring of sparks riding the shock front and shard-coloured glitter.
  private void EmitFractureEruptionExtras()
  {
    var focus = CrackFocusWorld();
    float outward = ParticleOutwardAngle(focus);
    float reach = ViewReach();
    EmitEventParticles(new GpuParticleBurst(focus, 1000, Color.Lerp(FractureHotColor, Color.White, 0.3f))
    {
      Direction = outward, Spread = 0.45f, Radius = PlanetRadius * 0.1f,
      Speed = new Vector2(0.4f, 1.3f) * reach, Lifetime = new(0.5f, 1.3f), Size = new(0.7f, 1.8f), Streak = true
    });
    EmitEventParticles(new GpuParticleBurst(focus, 220, new Color(255, 80, 20))
    {
      Direction = outward, Spread = 1.6f, Radius = PlanetRadius * 0.15f,
      Speed = new Vector2(0.05f, 0.3f) * reach, Lifetime = new(1f, 2.2f), Size = new(2.5f, 5f)
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 110, FractureColor * 0.18f)
    {
      Radius = PlanetRadius * 0.5f, Speed = new Vector2(0.03f, 0.2f) * reach,
      Lifetime = new(0.8f, 1.8f), Size = new(6f, 12f)
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 1400, Color.Lerp(FractureHotColor, Color.White, 0.5f))
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius, InnerRadius = PlanetRadius * 0.9f, Spread = 0.05f,
      Speed = new Vector2(1.4f, 1.7f) * reach, Lifetime = new(0.5f, 0.8f), Size = new(0.5f, 1.1f), Streak = true
    });
    EmitEventParticles(new GpuParticleBurst(focus, 140, CoreShards.Color)
    {
      Direction = outward, Spread = 2f, Radius = PlanetRadius * 0.1f,
      Speed = new(40f, 160f), Lifetime = new(0.6f, 1.4f), Size = new(0.5f, 1.2f), Streak = true
    });
  }

  // A glittering trail behind each shard flying out of the crack.
  private void EmitShardTrails(float dt)
  {
    shardTrailClock += dt;
    const float interval = 1f / 30f;
    if (shardTrailClock < interval) return;
    shardTrailClock %= interval;
    foreach (var shard in shardPickups)
    {
      if (shard.Collecting || shard.Age >= ShardFlightSeconds) continue;
      EmitEventParticles(new GpuParticleBurst(shard.Position, 6, CoreShards.Color)
      {
        Radius = 6f, Speed = new(10f, 40f), Lifetime = new(0.3f, 0.7f), Size = new(0.4f, 0.9f)
      });
    }
  }
}
