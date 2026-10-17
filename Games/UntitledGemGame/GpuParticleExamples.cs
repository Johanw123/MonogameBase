using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

public enum GpuParticleExample
{
  Explosion, Shockwave, Fountain, EngineExhaust, MiningImpact, ElectricDischarge, CoreFracture, WarpIn, RefuelStream
}

// Debug recipes only. Each layer is one small CPU descriptor, regardless of particle count.
public static class GpuParticleExamples
{
  public const string Labels = "Explosion\0Cyan shockwave\0Violet fountain\0Engine exhaust\0Mining impact\0Electric discharge\0Core fracture\0Warp arrival\0Refuel stream\0";
  public static string Description(GpuParticleExample example) => example switch
  {
    GpuParticleExample.Explosion => "Hot flash, fast orange debris and slow red embers. A weapon impact or ship explosion.",
    GpuParticleExample.Shockwave => "A thin expanding cyan ring with a short central flash. An EMP or ability pulse.",
    GpuParticleExample.Fountain => "A violet cone of glowing particles. Rotate the direction or add gravity to shape the fountain.",
    GpuParticleExample.EngineExhaust => "A narrow white-hot core inside a wider blue plume. Run continuously and move the source to leave exhaust.",
    GpuParticleExample.MiningImpact => "Directional gold streaks mixed with slower ore sparks. Try this near the planet's edge.",
    GpuParticleExample.ElectricDischarge => "Short white-blue streaks and a cyan energy ring. An electrical hit or chain-lightning endpoint.",
    GpuParticleExample.CoreFracture => "Gold rays, a spreading halo and lingering red embers. A core fracture or rare reward reveal.",
    GpuParticleExample.WarpIn => "Violet streaks converge from a ring while a blue pulse expands out. A warp arrival or gravity collapse.",
    _ => "A narrow green stream with bright droplets. Run continuously to preview refueling or energy transfer."
  };
  public static float Interval(GpuParticleExample example) => example switch
  {
    GpuParticleExample.EngineExhaust or GpuParticleExample.RefuelStream => 0.04f,
    GpuParticleExample.MiningImpact => 0.12f,
    GpuParticleExample.ElectricDischarge => 0.25f,
    _ => 0.8f
  };

  public static int Emit(GpuParticleSystem particles, GpuParticleExample example, Vector2 position,
    int count, float direction = 0, float scale = 1)
  {
    if (count <= 0) return 0;
    int small = count / 4, main = count - small;
    var burst = new GpuParticleBurst(position, main, new Color(255, 140, 35))
    {
      Size = new Vector2(0.6f, 1.8f) * scale, Speed = new Vector2(25, 125) * scale,
      Lifetime = new(0.3f, 1.1f), Radius = 2 * scale, Direction = direction
    };
    int accepted;
    switch (example)
    {
      case GpuParticleExample.Shockwave:
        accepted = particles.Emit(burst with { Color = new(45, 185, 255), Motion = GpuParticleMotion.Outward,
          Radius = 8 * scale, InnerRadius = 8 * scale, Speed = new Vector2(100, 108) * scale,
          Lifetime = new(0.65f, 0.85f), Spread = 0, Direction = 0 });
        return accepted + particles.Emit(burst with { Count = small, Color = new(150, 235, 255),
          Speed = new Vector2(8, 25) * scale, Size = new Vector2(2, 5) * scale, Lifetime = new(0.1f, 0.25f) });
      case GpuParticleExample.Fountain:
        return particles.Emit(burst with { Count = count, Color = new(190, 95, 255),
          Direction = direction - MathHelper.PiOver2, Spread = 0.7f,
          Speed = new Vector2(80, 155) * scale, Lifetime = new(0.6f, 1.4f) });
      case GpuParticleExample.EngineExhaust:
        accepted = particles.Emit(burst with { Color = new(30, 135, 255), Spread = 0.45f, Direction = direction + MathHelper.PiOver2,
          Speed = new Vector2(45, 90) * scale, Size = new Vector2(1, 3) * scale,
          Lifetime = new(0.18f, 0.5f), Radius = 1.8f * scale });
        return accepted + particles.Emit(burst with { Count = small, Color = new(170, 225, 255), Spread = 0.12f, Direction = direction + MathHelper.PiOver2,
          Speed = new Vector2(85, 140) * scale, Size = new Vector2(0.4f, 0.8f) * scale,
          Lifetime = new(0.08f, 0.2f), Radius = 0.5f * scale, Streak = true });
      case GpuParticleExample.MiningImpact:
        accepted = particles.Emit(burst with { Color = new(255, 195, 65), Spread = 1.3f, Direction = direction + MathHelper.Pi,
          Speed = new Vector2(50, 145) * scale, Size = new Vector2(0.35f, 0.75f) * scale,
          Lifetime = new(0.2f, 0.65f), Streak = true });
        return accepted + particles.Emit(burst with { Count = small, Color = new(255, 100, 20), Spread = 2.5f, Direction = direction + MathHelper.Pi,
          Speed = new Vector2(10, 40) * scale, Lifetime = new(0.5f, 1.2f) });
      case GpuParticleExample.ElectricDischarge:
        accepted = particles.Emit(burst with { Color = new(170, 225, 255), Streak = true,
          Speed = new Vector2(130, 240) * scale, Size = new Vector2(0.25f, 0.6f) * scale,
          Lifetime = new(0.08f, 0.25f) });
        return accepted + particles.Emit(burst with { Count = small, Color = new(30, 150, 255),
          Motion = GpuParticleMotion.Outward, Spread = 0, Direction = 0, Radius = 3 * scale,
          InnerRadius = 3 * scale, Speed = new Vector2(100, 105) * scale, Lifetime = new(0.2f, 0.35f) });
      case GpuParticleExample.CoreFracture:
        accepted = particles.Emit(burst with { Color = new(255, 195, 55), Streak = true,
          Speed = new Vector2(50, 170) * scale, Size = new Vector2(0.4f, 1.2f) * scale,
          Lifetime = new(0.4f, 1.1f) });
        accepted += particles.Emit(burst with { Count = small / 2, Color = new(255, 115, 15),
          Speed = new Vector2(8, 30) * scale, Lifetime = new(1.2f, 2.4f) });
        return accepted + particles.Emit(burst with { Count = small - small / 2, Color = new(255, 215, 100),
          Motion = GpuParticleMotion.Outward, Spread = 0, Direction = 0, Radius = 12 * scale,
          InnerRadius = 12 * scale, Speed = new Vector2(90, 95) * scale, Lifetime = new(0.6f, 0.85f) });
      case GpuParticleExample.WarpIn:
        accepted = particles.Emit(burst with { Color = new(160, 75, 255), Streak = true,
          Motion = GpuParticleMotion.Inward, Spread = 0, Direction = 0,
          Radius = 80 * scale, InnerRadius = 76 * scale, Speed = new Vector2(130, 140) * scale,
          Lifetime = new(0.5f, 0.6f), Size = new Vector2(0.4f, 0.9f) * scale });
        return accepted + particles.Emit(burst with { Count = small, Color = new(65, 185, 255),
          Motion = GpuParticleMotion.Outward, Spread = 0, Direction = 0, Radius = 3 * scale,
          InnerRadius = 3 * scale, Speed = new Vector2(65, 72) * scale, Lifetime = new(0.35f, 0.6f) });
      case GpuParticleExample.RefuelStream:
        accepted = particles.Emit(burst with { Color = new(35, 240, 115), Spread = 0.08f,
          Radius = 1.5f * scale, Speed = new Vector2(90, 105) * scale,
          Size = new Vector2(0.35f, 0.8f) * scale, Lifetime = new(0.65f, 0.85f), Streak = true });
        return accepted + particles.Emit(burst with { Count = small, Color = new(190, 255, 200), Spread = 0.04f,
          Radius = 0.6f * scale, Speed = new Vector2(100, 110) * scale,
          Size = new Vector2(0.6f, 1.2f) * scale, Lifetime = new(0.6f, 0.75f) });
      default:
        accepted = particles.Emit(burst with { Streak = true });
        accepted += particles.Emit(burst with { Count = small - small / 4, Color = new(255, 65, 12),
          Speed = new Vector2(5, 35) * scale, Lifetime = new(0.7f, 1.7f) });
        return accepted + particles.Emit(burst with { Count = small / 4, Color = new(255, 230, 160),
          Speed = new Vector2(5, 12) * scale, Size = new Vector2(2, 5) * scale, Lifetime = new(0.06f, 0.2f) });
    }
  }
}
