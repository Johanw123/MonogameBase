using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// The fleet's module effects: lightning, pulses, beams, exhaust plumes, gravity wells and
// the Time Heist ghost, shaded by ModuleEffects.fx. Effects are queued while the ships are
// drawn and rendered together over them, one draw call per kind. The ship's weapons use a
// batch of their own for their trails, lightning, geysers, molten cracks, lodged slugs, the
// warp jump and the links a click throws from gem to gem.
public sealed class ModuleEffectBatch
{
  public enum RingStyle { Shock, Collapse, Burst }

  // Drawn in this order: what covers (the vortex, a slug's body) under what glows.
  private enum Kind { Vortex, Crack, Slug, Beam, Link, Jet, Lance, Ring, Bolt, Ghost, Glint }
  private static readonly string[] Techniques = ["ModuleVortex", "ModuleCrack", "ModuleSlug", "ModuleBeam", "ModuleLink",
    "ModuleJet", "ModuleLance", "ModuleRing", "ModuleBolt", "ModuleGhost", "ModuleGlint"];

  // The joins are where a piece of a crack is cut to meet its neighbours (zero for everything else).
  private readonly record struct Item(Vector2 A, Vector2 B, float Reach, float Progress, Color Data, Color Tint,
    Vector2 JoinA = default, Vector2 JoinB = default);

  // Light is added and the vortex's event horizon darkens (premultiplied output).
  private static readonly BlendState LightBlend = new()
  {
    ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.InverseSourceAlpha,
    AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.InverseSourceAlpha,
  };

  // Enough for a whole fleet's storms in one frame; more are dropped rather than queued.
  private const int MaxPerKind = 1024;
  // The widest bolt (reach 40) strays a third of its reach.
  private const float BoltStrayRoom = 14f;
  private readonly List<Item>[] items = [new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];
  private readonly GraphicsDevice graphicsDevice;
  private SdfLineRenderer renderer;
  private Effect rendererEffect;

  public ModuleEffectBatch(GraphicsDevice graphicsDevice) => this.graphicsDevice = graphicsDevice;

  private static byte Unit(float value) => (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);

  private void Add(Kind kind, Vector2 a, Vector2 b, float reach, float progress, Color data, Color tint,
    Vector2 joinA = default, Vector2 joinB = default)
  {
    var list = items[(int)kind];
    // An effect between two points needs them apart: the shader takes its direction from them,
    // and without one it would light its whole quad.
    if (list.Count < MaxPerKind && data.A > 0 && Vector2.DistanceSquared(a, b) > 1e-4f)
      list.Add(new Item(a, b, reach, progress, data, tint, joinA, joinB));
  }

  // Lightning from one point to the next. Width is the core's (0 thin, 1 thick). Forks (up to
  // two) leave the channel partway along and die out. Scale shrinks the room the bolt strays
  // and glows in, for small crackles.
  public void Bolt(Vector2 from, Vector2 to, float progress, float seed, Color tint, float width = 0.35f,
    float intensity = 1f, int forks = 0, float scale = 1f)
  {
    float length = Vector2.Distance(from, to);
    // Room for the bolt to stray and for its glow.
    float reach = (Math.Clamp(length * 0.22f, 12f, 30f) + 10f) * scale;
    // The tint's alpha carries the forks: 255 none, 128 one, 0 two.
    var forked = new Color(tint.R, tint.G, tint.B, (byte)(forks <= 0 ? 255 : forks == 1 ? 128 : 0));
    Add(Kind.Bolt, from, to, reach, progress, new Color(Unit(seed - MathF.Floor(seed)), Unit(width), Unit(0.26f), Unit(intensity)), forked);
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

  // Energy flowing from one point to another, fading over its life. A lean beam (0..1) carries
  // less halo in less room: for crowds of beams, which would otherwise add up to a white fog.
  public void Beam(Vector2 from, Vector2 to, float progress, Color tint, float halfWidth = 1.4f, float seed = 0f,
    float intensity = 1f, float lean = 0f)
  {
    lean = Math.Clamp(lean, 0f, 1f);
    float reach = halfWidth * MathHelper.Lerp(9f, 4f, lean) + MathHelper.Lerp(6f, 1.5f, lean);
    Add(Kind.Beam, from, to, reach, progress,
      new Color(Unit(Fraction(seed)), Unit(lean * 0.45f), Unit(halfWidth / reach), Unit(intensity)), tint);
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

  // A geyser bursting out of its mouth toward tip, which it reaches as it dies.
  public void Jet(Vector2 mouth, Vector2 tip, float progress, Color tint, float halfWidth, float seed = 0f,
    float intensity = 1f)
  {
    // The column billows to about twice its mouth and sways.
    float reach = halfWidth * 4.5f + 6f;
    Add(Kind.Jet, mouth, tip, reach, progress,
      new Color(Unit(Fraction(seed)), (byte)0, Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // A molten fracture along a path, from its mouth to its tip, where it narrows to nothing.
  // The pieces are cut at the path's corners, so they meet without overlapping.
  public void Crack(ReadOnlySpan<Vector2> path, Color tint, float halfWidth, float intensity = 1f)
  {
    float total = 0f;
    for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
    if (total < 0.01f) return;
    float reach = halfWidth * 7f + 5f;
    float soFar = 0f;
    Vector2 previousAcross = default;
    bool started = false;
    for (int i = 1; i < path.Length; i++)
    {
      var from = path[i - 1];
      var to = path[i];
      float length = Vector2.Distance(from, to);
      if (length < 0.01f) continue;
      var along = (to - from) / length;
      var across = new Vector2(-along.Y, along.X);
      // The cut at each end: square at the crack's ends, mitred where it bends.
      var joinA = started ? Miter(previousAcross, across, reach) : across * reach;
      var joinB = across * reach;
      for (int next = i + 1; next < path.Length; next++)
      {
        var ahead = path[next] - to;
        if (ahead.LengthSquared() < 0.0001f) continue;
        ahead.Normalize();
        joinB = Miter(across, new Vector2(-ahead.Y, ahead.X), reach);
        break;
      }
      Add(Kind.Crack, from, to, reach, soFar / total,
        new Color(Unit(Fraction(soFar / 64f)), Unit(length / total), Unit(halfWidth / reach), Unit(intensity)), tint,
        joinA, joinB);
      soFar += length;
      previousAcross = across;
      started = true;
    }
  }

  // The cut shared by two pieces that meet at a bend, kept from running off at sharp ones.
  private static Vector2 Miter(Vector2 acrossA, Vector2 acrossB, float reach)
  {
    var sum = acrossA + acrossB;
    if (sum.LengthSquared() < 0.01f) return acrossB * reach;
    sum.Normalize();
    return sum * (reach / Math.Max(0.5f, Vector2.Dot(sum, acrossB)));
  }

  // The streak a warp jump leaves from where the drive fired to where the ship went.
  public void Lance(Vector2 from, Vector2 to, float progress, Color tint, float halfWidth, float seed = 0f,
    float intensity = 1f)
  {
    float reach = halfWidth * 3.2f + 4f;
    Add(Kind.Lance, from, to, reach, progress,
      new Color(Unit(Fraction(seed)), (byte)0, Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // A railgun slug lodged in the ground: a steel rod from where it went in (root) to its tail.
  // Cooled runs from 0, white hot as it lands, to 1. Charge (0..1) is a pulse on its way through
  // it: it enters at the root as 1 and leaves by the tail as it falls to 0.
  public void Slug(Vector2 root, Vector2 tail, float cooled, float charge, Color tint, float halfWidth, float seed = 0f,
    float intensity = 1f)
  {
    // Room for its glow and for the charge crackling around it.
    float reach = halfWidth * 4f + 5f;
    Add(Kind.Slug, root, tail, reach, cooled,
      new Color(Unit(Fraction(seed)), Unit(charge), Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // A spark that jumps from one point to the next and leaves a thread of light behind it,
  // which lets go of where it started first.
  public void Link(Vector2 from, Vector2 to, float progress, Color tint, float halfWidth = 0.8f, float seed = 0f,
    float intensity = 1f)
  {
    float reach = halfWidth * 6f + 4f;
    Add(Kind.Link, from, to, reach, progress,
      new Color(Unit(Fraction(seed)), (byte)0, Unit(halfWidth / reach), Unit(intensity)), tint);
  }

  // A star: four long rays (the first along heading) and four short ones. Size is how far the rays reach.
  public void Glint(Vector2 center, float size, Color tint, float seed = 0f, float intensity = 1f, float twinkle = 0f,
    Vector2? heading = null)
  {
    var along = heading is { } given && given.LengthSquared() > 0.0001f ? Vector2.Normalize(given) : Vector2.UnitX;
    Add(Kind.Glint, center, center + along, size, twinkle,
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
        { BaseGlowPadding = 0f, PulseExtraPadding = 0f, BlendState = LightBlend, Oriented = true };
    }
    for (int kind = 0; kind < items.Length; kind++)
    {
      var list = items[kind];
      if (list.Count == 0) continue;
      effect.CurrentTechnique = effect.Techniques[Techniques[kind]];
      // A bolt strays off its line before its glow falls away, so its quad needs that much more room.
      renderer.BaseGlowPadding = kind == (int)Kind.Bolt ? BoltStrayRoom : 0f;
      renderer.Begin(viewProjection, time);
      foreach (var item in list)
      {
        if (item.JoinA != default)
          renderer.DrawLine(item.A, item.B, item.Reach, item.Data, item.Tint, item.Progress, null, item.JoinA, item.JoinB);
        else
          renderer.DrawLine(item.A, item.B, item.Reach, item.Data, item.Tint, item.Progress);
      }
      renderer.End();
      list.Clear();
    }
  }
}
