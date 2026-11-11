using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// The fleet's module effects: lightning, pulses, beams, exhaust plumes, gravity wells and
// the Time Heist ghost, shaded by ModuleEffects.fx. Effects are queued while the ships are
// drawn and rendered together over them, one draw call per kind.
public sealed class ModuleEffectBatch
{
  public enum RingStyle { Shock, Collapse, Burst }

  private enum Kind { Vortex, Beam, Ring, Bolt, Ghost }
  private static readonly string[] Techniques = ["ModuleVortex", "ModuleBeam", "ModuleRing", "ModuleBolt", "ModuleGhost"];

  private readonly record struct Item(Vector2 A, Vector2 B, float Reach, float Progress, Color Data, Color Tint);

  // Light is added and the vortex's event horizon darkens (premultiplied output).
  private static readonly BlendState LightBlend = new()
  {
    ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.InverseSourceAlpha,
    AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.InverseSourceAlpha,
  };

  // Enough for a whole fleet's storms in one frame; more are dropped rather than queued.
  private const int MaxPerKind = 1024;
  private readonly List<Item>[] items = [new(), new(), new(), new(), new()];
  private readonly GraphicsDevice graphicsDevice;
  private SdfLineRenderer renderer;
  private Effect rendererEffect;

  public ModuleEffectBatch(GraphicsDevice graphicsDevice) => this.graphicsDevice = graphicsDevice;

  private static byte Unit(float value) => (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);

  private void Add(Kind kind, Vector2 a, Vector2 b, float reach, float progress, Color data, Color tint)
  {
    var list = items[(int)kind];
    if (list.Count < MaxPerKind && data.A > 0) list.Add(new Item(a, b, reach, progress, data, tint));
  }

  // Lightning from one point to the next. Width is the core's (0 thin, 1 thick).
  public void Bolt(Vector2 from, Vector2 to, float progress, float seed, Color tint, float width = 0.35f,
    float intensity = 1f)
  {
    float length = Vector2.Distance(from, to);
    // Room for the bolt to stray and for its glow.
    float reach = Math.Clamp(length * 0.22f, 12f, 30f) + 10f;
    Add(Kind.Bolt, from, to, reach, progress, new Color(Unit(seed - MathF.Floor(seed)), Unit(width), Unit(0.26f), Unit(intensity)), tint);
  }

  // A bolt with short forks crackling off the point it strikes.
  public void Strike(Vector2 from, Vector2 to, float progress, float seed, Color tint, float width = 0.35f,
    float intensity = 1f, int forks = 2)
  {
    Bolt(from, to, progress, seed, tint, width, intensity);
    for (int fork = 0; fork < forks; fork++)
    {
      float turn = Fraction(seed * 7.31f + fork * 0.619f) * MathHelper.TwoPi;
      float length = 9f + 14f * Fraction(seed * 3.17f + fork * 0.271f);
      var tip = to + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * length;
      Bolt(to, tip, progress, seed + 0.37f * (fork + 1), tint, width * 0.4f, intensity * 0.7f);
    }
  }

  private static float Fraction(float value) => value - MathF.Floor(value);

  public void Ring(Vector2 center, float radius, float progress, Color tint, RingStyle style, float seed = 0f,
    float intensity = 1f)
  {
    // The front's glow and the burst's rays run a little past the radius.
    float reach = radius * 1.2f + 14f;
    Add(Kind.Ring, center, center + Vector2.UnitX, reach, progress,
      new Color(Unit((int)style * 0.5f), Unit(Fraction(seed)), Unit(radius / reach), Unit(intensity)), tint);
  }

  // Energy flowing from one point to another, fading over its life.
  public void Beam(Vector2 from, Vector2 to, float progress, Color tint, float halfWidth = 1.4f, float seed = 0f,
    float intensity = 1f)
  {
    float reach = halfWidth * 9f + 6f;
    Add(Kind.Beam, from, to, reach, progress,
      new Color(Unit(Fraction(seed)), (byte)0, Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // Exhaust streaming out of head and thinning to nothing at tail.
  public void Plume(Vector2 head, Vector2 tail, Color tint, float halfWidth, float seed = 0f, float intensity = 1f)
  {
    float reach = halfWidth * 2.6f + 4f;
    Add(Kind.Beam, head, tail, reach, 0f,
      new Color(Unit(Fraction(seed)), (byte)255, Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // A gravity well. A horizon radius above zero gives it a black centre.
  public void Vortex(Vector2 center, float radius, float horizonRadius, Color tint, float seed = 0f,
    float spin = 0.5f, float brightness = 0.5f, float intensity = 1f)
    => Add(Kind.Vortex, center, center + Vector2.UnitX, radius, Math.Clamp(horizonRadius / radius, 0f, 0.9f),
      new Color(Unit(Fraction(seed)), Unit(spin), Unit(brightness), Unit(intensity)), tint);

  public void Ghost(Vector2 position, Vector2 heading, float size, Color tint, float seed = 0f, float intensity = 1f)
  {
    if (heading.LengthSquared() < 0.0001f) heading = Vector2.UnitX;
    Add(Kind.Ghost, position, position + Vector2.Normalize(heading), size, 0f,
      new Color(Unit(Fraction(seed)), (byte)0, (byte)0, Unit(intensity)), tint);
  }

  // Draws and empties the queue. Without the shader the effects are skipped.
  public void Draw(Matrix viewProjection, float time)
  {
    var asset = EffectCache.ModuleEffectsFx;
    if (asset?.IsLoaded == true && !asset.IsFailed) Draw(asset.Value, viewProjection, time);
    else foreach (var list in items) list.Clear();
  }

  public void Draw(Effect effect, Matrix viewProjection, float time)
  {
    if (renderer == null || rendererEffect != effect)
    {
      rendererEffect = effect;
      // The reach travels in each line's thickness, so the renderer adds no padding of its own.
      renderer = new SdfLineRenderer(graphicsDevice, effect, MaxPerKind)
        { BaseGlowPadding = 0f, PulseExtraPadding = 0f, BlendState = LightBlend };
    }
    for (int kind = 0; kind < items.Length; kind++)
    {
      var list = items[kind];
      if (list.Count == 0) continue;
      effect.CurrentTechnique = effect.Techniques[Techniques[kind]];
      renderer.Begin(viewProjection, time);
      foreach (var item in list)
        renderer.DrawLine(item.A, item.B, item.Reach, item.Data, item.Tint, item.Progress);
      renderer.End();
      list.Clear();
    }
  }
}
