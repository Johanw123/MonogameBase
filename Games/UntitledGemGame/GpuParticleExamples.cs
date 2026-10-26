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
    bool extreme = count > 20000;
    float sizeScale = Math.Min(scale, 1.55f);
    byte alpha = extreme
      ? (byte)Math.Clamp((int)MathF.Round(255f * MathF.Pow(24000f / count, 0.28f)), 95, 142)
      : (byte)255;
    float radiusScale = extreme ? Math.Max(scale * 2.2f, 10f) : scale;
    static Color WithAlpha(Color c, byte a) => new(c.R, c.G, c.B, a);
    var burst = new GpuParticleBurst(position, main, WithAlpha(new Color(255, 130, 15), alpha))
    {
      Size = new Vector2(0.72f, 1.78f) * sizeScale,
      Speed = (extreme ? new Vector2(28, 255) : new Vector2(20, 140)) * scale,
      Lifetime = extreme ? new(0.75f, 1.65f) : new(0.35f, 1.25f),
      Radius = 3 * radiusScale, Direction = direction
    };
    int accepted;
    switch (example)
    {
      case GpuParticleExample.Shockwave:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(25, 195, 255), alpha), Motion = GpuParticleMotion.Outward,
          Radius = (extreme ? 28 : 8) * scale, InnerRadius = (extreme ? 12 : 6) * scale,
          Speed = (extreme ? new Vector2(35, 215) : new Vector2(85, 135)) * scale,
          Lifetime = extreme ? new(0.75f, 1.55f) : new(0.65f, 1.1f), Spread = extreme ? 0.55f : 0.25f, Direction = 0, Streak = true });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(95, 225, 255), alpha),
          Speed = (extreme ? new Vector2(25, 175) : new Vector2(15, 55)) * scale, Size = new Vector2(0.72f, 1.75f) * sizeScale,
          Lifetime = extreme ? new(0.65f, 1.35f) : new(0.2f, 0.55f), Streak = extreme });
      case GpuParticleExample.Fountain:
        return particles.Emit(burst with { Count = count, Color = WithAlpha(new(190, 65, 255), alpha),
          Direction = direction - MathHelper.PiOver2, Spread = 0.7f,
          Speed = new Vector2(80, 155) * scale, Lifetime = new(0.6f, 1.4f), Streak = extreme });
      case GpuParticleExample.EngineExhaust:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(30, 135, 255), alpha), Spread = 0.45f, Direction = direction + MathHelper.PiOver2,
          Speed = new Vector2(45, 90) * scale, Size = new Vector2(1, 2.5f) * sizeScale,
          Lifetime = new(0.18f, 0.5f), Radius = 1.8f * scale });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(170, 225, 255), alpha), Spread = 0.12f, Direction = direction + MathHelper.PiOver2,
          Speed = new Vector2(85, 140) * scale, Size = new Vector2(0.4f, 0.8f) * sizeScale,
          Lifetime = new(0.08f, 0.2f), Radius = 0.5f * scale, Streak = true });
      case GpuParticleExample.MiningImpact:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(255, 195, 45), alpha), Spread = 1.3f, Direction = direction + MathHelper.Pi,
          Speed = new Vector2(50, 145) * scale, Size = new Vector2(0.65f, 1.55f) * sizeScale,
          Lifetime = new(0.2f, 0.65f), Streak = true });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(255, 95, 15), alpha), Spread = 2.5f, Direction = direction + MathHelper.Pi,
          Speed = new Vector2(30, 165) * scale, Lifetime = new(0.5f, 1.2f), Streak = extreme });
      case GpuParticleExample.ElectricDischarge:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(45, 205, 255), alpha), Streak = true,
          Speed = (extreme ? new Vector2(32, 225) : new Vector2(90, 260)) * scale, Size = new Vector2(0.68f, 1.65f) * sizeScale,
          Lifetime = extreme ? new(0.65f, 1.45f) : new(0.2f, 0.65f) });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(25, 135, 255), alpha),
          Motion = GpuParticleMotion.Outward, Spread = 0.45f, Direction = 0, Radius = (extreme ? 24 : 6) * scale,
          InnerRadius = (extreme ? 10 : 4) * scale, Speed = (extreme ? new Vector2(35, 195) : new Vector2(80, 130)) * scale,
          Lifetime = extreme ? new(0.65f, 1.45f) : new(0.35f, 0.75f), Streak = true });
      case GpuParticleExample.CoreFracture:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(255, 185, 25), alpha), Streak = true,
          Speed = (extreme ? new Vector2(30, 215) : new Vector2(45, 210)) * scale, Size = new Vector2(0.70f, 1.72f) * sizeScale,
          Lifetime = extreme ? new(0.75f, 1.65f) : new(0.45f, 1.35f) });
        accepted += particles.Emit(burst with { Count = small / 2, Color = WithAlpha(new(255, 75, 15), alpha),
          Speed = (extreme ? new Vector2(24, 175) : new Vector2(15, 75)) * scale, Lifetime = new(0.85f, 1.85f), Streak = extreme });
        return accepted + particles.Emit(burst with { Count = small - small / 2, Color = WithAlpha(new(255, 145, 25), alpha),
          Motion = GpuParticleMotion.Outward, Spread = 0.45f, Direction = 0, Radius = (extreme ? 30 : 14) * scale,
          InnerRadius = (extreme ? 12 : 10) * scale, Speed = (extreme ? new Vector2(35, 195) : new Vector2(75, 125)) * scale,
          Lifetime = extreme ? new(0.75f, 1.55f) : new(0.55f, 1.05f), Streak = true });
      case GpuParticleExample.WarpIn:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(175, 55, 255), alpha), Streak = true,
          Motion = GpuParticleMotion.Inward, Spread = 0.25f, Direction = 0,
          Radius = 85 * scale, InnerRadius = 45 * scale, Speed = new Vector2(75, 185) * scale,
          Lifetime = new(0.55f, 1.05f), Size = new Vector2(0.68f, 1.65f) * sizeScale });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(35, 195, 255), alpha),
          Motion = GpuParticleMotion.Outward, Spread = 0.35f, Direction = 0, Radius = (extreme ? 22 : 6) * scale,
          InnerRadius = (extreme ? 10 : 4) * scale, Speed = (extreme ? new Vector2(30, 195) : new Vector2(55, 95)) * scale,
          Lifetime = extreme ? new(0.65f, 1.45f) : new(0.45f, 0.85f), Streak = true });
      case GpuParticleExample.RefuelStream:
        accepted = particles.Emit(burst with { Color = WithAlpha(new(35, 240, 115), alpha), Spread = 0.08f,
          Radius = 1.5f * scale, Speed = new Vector2(90, 105) * scale,
          Size = new Vector2(0.35f, 0.8f) * sizeScale, Lifetime = new(0.65f, 0.85f), Streak = true });
        return accepted + particles.Emit(burst with { Count = small, Color = WithAlpha(new(190, 255, 200), alpha), Spread = 0.04f,
          Radius = 0.6f * scale, Speed = new Vector2(100, 110) * scale,
          Size = new Vector2(0.6f, 1.2f) * sizeScale, Lifetime = new(0.6f, 0.75f) });
      default:
        accepted = particles.Emit(burst with { Streak = true });
        accepted += particles.Emit(burst with { Count = small - small / 4, Color = WithAlpha(new(255, 55, 10), alpha),
          Speed = (extreme ? new Vector2(24, 175) : new Vector2(12, 65)) * scale, Lifetime = new(0.75f, 1.7f), Streak = extreme });
        return accepted + particles.Emit(burst with { Count = small / 4, Color = WithAlpha(new(255, 195, 35), alpha),
          Speed = (extreme ? new Vector2(32, 210) : new Vector2(15, 45)) * scale, Size = new Vector2(0.68f, 1.65f) * sizeScale,
          Lifetime = extreme ? new(0.65f, 1.35f) : new(0.12f, 0.35f), Streak = extreme });
    }
  }
}
