using System;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Cosmetic-only, bounded pools. Own PRNG keeps visual experiments out of gameplay rolls.
// No render targets, textures, event subscriptions or per-frame heap allocations.
public sealed class WorldPolish
{
  public const int MaxImpacts = 8, MaxFragments = 192, MaxDust = 120;
  private struct Impact
  {
    public Vector3 Normal;
    public Color Color;
    public float Age, Duration, Energy;
  }
  private struct Fragment
  {
    public Vector2 Position, Velocity;
    public float Age, Duration, Size, Rotation, Spin;
    public Color Hot;
  }
  private readonly Impact[] impacts = new Impact[MaxImpacts];
  private readonly Fragment[] fragments = new Fragment[MaxFragments];
  private readonly Vector4[] hitPositions = new Vector4[MaxImpacts], hitColors = new Vector4[MaxImpacts];
  private readonly Random random = new(48163);
  private int fragmentCursor;
  private float emissionCredit = 8;
  public int ActiveImpacts { get; private set; }
  public int ActiveFragments { get; private set; }

  public void Clear()
  {
    Array.Clear(impacts);
    Array.Clear(fragments);
    ActiveImpacts = ActiveFragments = fragmentCursor = 0;
    emissionCredit = 8;
  }

  public void Update(float dt)
  {
    if (!float.IsFinite(dt) || dt <= 0) return;
    emissionCredit = Math.Min(8, emissionCredit + dt * 24);
    ActiveImpacts = ActiveFragments = 0;
    for (int i = 0; i < impacts.Length; i++)
    {
      ref var impact = ref impacts[i];
      impact.Age += dt;
      if (impact.Energy > 0 && impact.Age < impact.Duration) ActiveImpacts++;
    }
    // Analytic drag integrates consistently when the simulation step changes.
    float drag = MathF.Exp(-1.8f * dt);
    for (int i = 0; i < fragments.Length; i++)
    {
      ref var fragment = ref fragments[i];
      fragment.Age += dt;
      if (fragment.Age >= fragment.Duration) continue;
      fragment.Position += fragment.Velocity * ((1f - drag) / 1.8f);
      fragment.Velocity *= drag;
      fragment.Rotation += fragment.Spin * dt;
      ActiveFragments++;
    }
  }

  public void AddImpact(Vector2 position, Vector3 surfaceNormal, Vector2 outward, Color color,
    float energy = 1, bool debris = true)
  {
    var s = VisualEffects.Settings;
    if (!VisualEffects.Active || (!s.ImpactLighting && !s.RockDebris)) return;
    if (!float.IsFinite(energy) || energy <= 0 || !float.IsFinite(surfaceNormal.LengthSquared())
      || surfaceNormal.LengthSquared() < .001f || !float.IsFinite(position.X) || !float.IsFinite(position.Y)
      || !float.IsFinite(outward.LengthSquared())) return;
    energy = Math.Clamp(energy, .1f, 2f);
    if (s.ImpactLighting)
    {
      int slot = 0;
      float weakest = float.MaxValue;
      for (int i = 0; i < impacts.Length; i++)
      {
        var old = impacts[i];
        float remaining = old.Duration > 0 ? Math.Max(0, 1 - old.Age / old.Duration) * old.Energy : 0;
        if (remaining < weakest) { weakest = remaining; slot = i; }
      }
      if (energy >= weakest)
        impacts[slot] = new Impact { Normal = Vector3.Normalize(surfaceNormal), Color = color,
          Energy = energy, Duration = s.ImpactDuration };
    }
    // Continuous lasers never consume the fragment budget or suppress a heavy hit light.
    if (!debris || !s.RockDebris || s.DebrisAmount <= 0 || emissionCredit < 1) return;
    emissionCredit--;
    float direction = outward.LengthSquared() > .001f ? MathF.Atan2(outward.Y, outward.X) : 0;
    int count = (int)MathF.Round(7 * energy * s.DebrisAmount);
    for (int i = 0; i < count; i++)
    {
      float angle = direction + (random.NextSingle() - .5f) * 2.1f;
      var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Range(25, 95) * s.DebrisSpeed;
      fragments[fragmentCursor] = new Fragment
      {
        Position = position, Velocity = velocity, Duration = Range(.65f, 1.25f) * s.DebrisLifetime,
        Size = Range(.8f, 2.3f) * s.DebrisSize, Rotation = Range(0, MathHelper.TwoPi),
        Spin = Range(-6, 6), Hot = Color.Lerp(new Color(95, 100, 125), color, .35f)
      };
      fragmentCursor = (fragmentCursor + 1) % fragments.Length;
    }
  }

  private float Range(float min, float max) => min + random.NextSingle() * (max - min);

  public void DrawPlanet(SpriteBatch batch, Effect effect, Matrix projection, Vector2 center,
    float radius, float rotation, float time, float visibility = 1)
  {
    var s = VisualEffects.Settings;
    if (!VisualEffects.Active || effect == null || radius <= .01f || visibility <= 0
      || (!s.Atmosphere && !s.ImpactLighting)) return;
    int count = 0;
    float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
    if (s.ImpactLighting)
      foreach (var hit in impacts)
      {
        if (hit.Energy <= 0 || hit.Age >= hit.Duration) continue;
        var normal = new Vector3(hit.Normal.X * cos + hit.Normal.Z * sin, hit.Normal.Y,
          -hit.Normal.X * sin + hit.Normal.Z * cos);
        hitPositions[count] = new Vector4(normal, hit.Age / hit.Duration);
        hitColors[count] = new Vector4(hit.Color.ToVector3(), hit.Energy);
        count++;
      }
    if (!s.Atmosphere && count == 0) return;
    effect.Parameters["view_projection"].SetValue(projection);
    effect.Parameters["Hits"].SetValue(hitPositions);
    effect.Parameters["HitColors"].SetValue(hitColors);
    effect.Parameters["HitCount"].SetValue((float)count);
    effect.Parameters["AtmosphereColor"].SetValue(new Vector3(s.AtmosphereRed, s.AtmosphereGreen, s.AtmosphereBlue));
    effect.Parameters["AtmosphereStrength"].SetValue(s.Atmosphere ? s.AtmosphereStrength : 0);
    effect.Parameters["AtmosphereWidth"].SetValue(s.AtmosphereWidth);
    effect.Parameters["AuroraStrength"].SetValue(s.AuroraStrength);
    effect.Parameters["Time"].SetValue(time * s.AuroraSpeed);
    effect.Parameters["ImpactLightStrength"].SetValue(s.ImpactLightStrength);
    effect.Parameters["ImpactLightRadius"].SetValue(s.ImpactLightRadius);
    effect.Parameters["ImpactRippleStrength"].SetValue(s.ImpactRippleStrength);
    batch.Begin(blendState: BlendState.Additive, effect: effect);
    batch.Draw(AssetManager.DefaultTexture, center, null, Color.White * Math.Clamp(visibility, 0, 1),
      0, new Vector2(.5f), radius * 3, SpriteEffects.None, 0);
    batch.End();
  }

  public void DrawFragments(SpriteBatch batch, Effect effect, Matrix projection)
  {
    if (!VisualEffects.Active || !VisualEffects.Settings.RockDebris || effect == null) return;
    effect.CurrentTechnique = effect.Techniques["Rock"];
    effect.Parameters["view_projection"].SetValue(projection);
    batch.Begin(blendState: BlendState.AlphaBlend, effect: effect);
    foreach (var fragment in fragments)
    {
      if (fragment.Age >= fragment.Duration) continue;
      float age = fragment.Age / fragment.Duration;
      float alpha = Math.Clamp((1 - age) * 3, 0, 1);
      var tint = Color.Lerp(fragment.Hot, new Color(46, 49, 65), Math.Min(1, age * 2));
      // Rotation and a changing narrow axis suggest tumbling chunks, rather than sparks.
      float tumble = .55f + .45f * MathF.Abs(MathF.Cos(fragment.Rotation));
      batch.Draw(AssetManager.DefaultTexture, fragment.Position, null, tint * alpha,
        fragment.Rotation, new Vector2(.5f), new Vector2(fragment.Size * 2 * tumble, fragment.Size * 2),
        SpriteEffects.None, 0);
    }
    batch.End();
  }

  // Stable low-discrepancy positions, two depth planes. This renders behind all gameplay.
  // Screen-sized wrapping avoids a giant world-sized quad or a full-screen noise pass.
  public static void DrawDust(SpriteBatch batch, Effect effect, int width, int height,
    Vector2 cameraPosition, float zoom, float time)
  {
    var s = VisualEffects.Settings;
    if (!VisualEffects.Active || !s.SpaceDust || s.DustAmount <= 0 || s.DustBrightness <= 0 || effect == null) return;
    effect.CurrentTechnique = effect.Techniques["Dust"];
    effect.Parameters["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1));
    batch.Begin(blendState: BlendState.AlphaBlend, effect: effect);
    int count = (int)(MaxDust * s.DustAmount);
    float resolution = height / 1080f;
    float margin = 24 * resolution;
    for (int i = 0; i < count; i++)
    {
      float depth = i % 3 == 0 ? 1f : .45f;
      float x = Fraction(i * .75487766f + .19f) * (width + 2 * margin)
        + time * 3 * s.DustSpeed * depth * resolution - cameraPosition.X * zoom * depth * .25f;
      float y = Fraction(i * .56984029f + .67f) * (height + 2 * margin)
        + time * 7 * s.DustSpeed * depth * resolution - cameraPosition.Y * zoom * depth * .25f;
      var position = new Vector2(Wrap(x, width + 2 * margin) - margin, Wrap(y, height + 2 * margin) - margin);
      float pulse = .65f + .35f * MathF.Sin(time * .4f + i * 2.4f);
      float size = (3 + depth * 4) * resolution * s.DustSize;
      batch.Draw(AssetManager.DefaultTexture, position, null,
        new Color(120, 170, 205) * (s.DustBrightness * pulse * depth), 0,
        new Vector2(.5f), size, SpriteEffects.None, 0);
    }
    batch.End();
  }
  private static float Fraction(float value) => value - MathF.Floor(value);
  private static float Wrap(float value, float size) => value - MathF.Floor(value / size) * size;
}
