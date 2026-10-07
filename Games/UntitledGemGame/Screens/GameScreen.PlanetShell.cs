using System;
using System.Collections.Generic;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// The planet's shell (tuning and plate layout in PlanetShell.cs): a dark sphere with a
// violet glow at its edge, drawn by PlanetShell.fx on the planet's texel grid and
// turning with it. Every hit wears it down; cracks spread along the seams between its
// plates, lit by the planet inside. When its health is gone it shatters:
//  1. Strain (0-0.9 s): heartbeats shake it as every seam cracks open and floods with light.
//  2. Burst (0.9 s): its plates fly apart, tumbling, and the planet beneath shows.
// The shell releases no Core Shard, and nothing holds still for it. Damage dealt after
// the burst starts counting toward core fractures (GameScreen.CoreFracture.cs).
public partial class UntitledGemGameGameScreen
{
  private const float ShellDiscRadius = 31f; // RADIUS in PlanetShell.fx
  private const float ShellBurst = 0.9f;
  private const float ShellCrackFloodSeconds = 0.7f;
  private const float ShellFragmentSeconds = 1.6f;
  private const float ShellFragmentDrag = 1.4f;
  private static readonly float[] ShellHeartbeats = [0.05f, 0.4f, 0.62f, 0.78f];
  private static readonly Color ShellRimColor = new(140, 82, 255);
  private static readonly Color ShellLightColor = new(173, 255, 238);

  private sealed class ShellFragment
  {
    public int Plate;
    // Where the plate sat on the disc, in texels from the planet's centre.
    public Vector2 Centroid, Direction;
    public float Speed, Spin;
    // 0 at the edge of the disc, 1 facing the viewer: those fly toward the camera.
    public float Facing;
  }

  private readonly List<ShellFragment> shellFragments = new();
  private PlanetShell.Layout shellLayout;
  private ulong shellLayoutRun = ulong.MaxValue;
  private bool shellShattering;
  private float shellTime;
  // The planet's turn and texel size as the shell burst; its fragments keep them.
  private float shellBurstRotation;
  private float shellBurstTexel;

  // The shell takes every hit until it bursts.
  public bool PlanetShelled => PlanetMiningEnabled
    && (!m_gameState.ShellBroken || shellShattering && shellTime < ShellBurst);

  public double ShellDamage => m_gameState.ShellDamage;

  // Each run's shell breaks along its own seams.
  private PlanetShell.Layout ShellLayout()
  {
    if (shellLayout == null || shellLayoutRun != m_gameState.CoreExtractions)
    {
      shellLayoutRun = m_gameState.CoreExtractions;
      shellLayout = PlanetShell.CreateLayout(unchecked(1009 + (int)shellLayoutRun * 7919));
    }
    return shellLayout;
  }

  private void DamageShell(double damage)
  {
    if (!(damage > 0) || m_gameState.ShellBroken) return;
    m_gameState.ShellDamage = PlanetShell.Sanitize(m_gameState.ShellDamage + damage);
    if (m_gameState.ShellBroken) StartShellShatter();
  }

  // Breaks the shell now, whatever its health left (debug tools and capture scenes).
  public void ShatterPlanetShell()
  {
    if (!PlanetMiningEnabled || m_gameState.ShellBroken || m_prestiging || m_postPrestige) return;
    m_gameState.ShellDamage = PlanetShell.Health;
    StartShellShatter();
  }

  private void StartShellShatter()
  {
    shellShattering = true;
    shellTime = 0f;
    shellFragments.Clear();
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.3f);
    // The shell counts as gone at once, so a save mid-strain does not bring it back.
    SaveProgress();
  }

  private void UpdatePlanetShell(float dt)
  {
    if (!shellShattering) return;
    float previous = shellTime;
    shellTime += dt;
    bool Crossed(float moment) => previous < moment && shellTime >= moment;

    for (int i = 0; i < ShellHeartbeats.Length; i++)
      if (Crossed(ShellHeartbeats[i]))
        PulsePlanet(0.35f + 0.4f * i / (ShellHeartbeats.Length - 1f));
    if (shellTime < ShellBurst)
      planetShake = Math.Max(planetShake, 0.1f + 0.6f * Smooth(0f, ShellBurst, shellTime));
    if (Crossed(ShellBurst)) BurstShell();
    if (shellTime >= ShellBurst + ShellFragmentSeconds)
    {
      shellShattering = false;
      shellFragments.Clear();
    }
  }

  private void BurstShell()
  {
    shellBurstRotation = FrameRotation(PlanetFrame(planetAge));
    shellBurstTexel = PlanetSpriteScale();
    // Each plate on the side facing the viewer becomes a fragment, flying out from
    // where it sat on the disc.
    var layout = ShellLayout();
    float cos = MathF.Cos(shellBurstRotation), sin = MathF.Sin(shellBurstRotation);
    var sums = new Vector3[PlanetShell.PlateCount];
    for (int y = 0; y < PlanetFrameSize; y++)
      for (int x = 0; x < PlanetFrameSize; x++)
      {
        var texel = new Vector2(x + 0.5f - PlanetFrameSize / 2f, y + 0.5f - PlanetFrameSize / 2f);
        if (texel.LengthSquared() > ShellDiscRadius * ShellDiscRadius) continue;
        float nx = texel.X / ShellDiscRadius, ny = -texel.Y / ShellDiscRadius;
        float nz = MathF.Sqrt(Math.Max(0f, 1f - nx * nx - ny * ny));
        int plate = PlanetShell.PlateAt(layout, new Vector3(nx * cos - nz * sin, ny, nx * sin + nz * cos));
        sums[plate] += new Vector3(texel, 1f);
      }
    shellFragments.Clear();
    for (int plate = 0; plate < sums.Length; plate++)
    {
      if (sums[plate].Z < 3f) continue;
      var centroid = new Vector2(sums[plate].X, sums[plate].Y) / sums[plate].Z;
      float reach = Math.Clamp(centroid.Length() / ShellDiscRadius, 0f, 1f);
      shellFragments.Add(new ShellFragment
      {
        Plate = plate,
        Centroid = centroid,
        Direction = centroid.LengthSquared() > 4f ? Vector2.Normalize(centroid)
          : PlanetDirection(Random.Shared.NextSingle() * MathHelper.TwoPi),
        Speed = (260f + 320f * reach) * (0.85f + 0.3f * Random.Shared.NextSingle()),
        Spin = (Random.Shared.Next(2) == 0 ? -1f : 1f) * (0.6f + 1.8f * Random.Shared.NextSingle()),
        Facing = 1f - reach,
      });
    }

    // Damage from here on counts toward the first core fracture, after a breather.
    fractureCooldown = Math.Max(fractureCooldown, FractureCooldownSeconds);
    PulsePlanet(1f, 0.6f);
    SpawnerEffects.Add(null, PlanetPos, Color.White, PlanetRadius, PlanetRadius * 2.4f, 0.45f);
    SpawnerEffects.Add(null, PlanetPos, ShellLightColor, PlanetRadius, PlanetRadius * 4.5f, 0.8f);
    SpawnerEffects.Add(null, PlanetPos, ShellRimColor, PlanetRadius * 1.1f, PlanetRadius * 7f, 1.2f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: 0.3f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect, pitch: -0.4f);
  }

  private void ClearPlanetShell()
  {
    shellShattering = false;
    shellTime = 0f;
    shellFragments.Clear();
  }

  // ---- Drawing ----

  private bool BeginShellEffect(float rotation, float wear, float glow, float flash)
  {
    var effect = EffectCache.PlanetShellFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return false;
    var layout = ShellLayout();
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
    parameters["Rotation"]?.SetValue(rotation);
    parameters["Wear"]?.SetValue(wear);
    parameters["Glow"]?.SetValue(glow);
    parameters["Flash"]?.SetValue(flash);
    parameters["Time"]?.SetValue(planetAge);
    parameters["Plates"]?.SetValue(layout.Plates);
    parameters["CrackOrigins"]?.SetValue(layout.CrackOrigins);
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, effect: effect.Value);
    return true;
  }

  // The whole shell over the planet. Returns false while its shader is unavailable,
  // and the bare planet is drawn instead.
  private bool DrawPlanetShell()
  {
    float wear = PlanetShell.Wear(m_gameState.ShellDamage), glow = 0f;
    if (shellShattering)
    {
      wear = MathHelper.Lerp(1f, PlanetShell.FullWear, Smooth(0f, ShellCrackFloodSeconds, shellTime));
      glow = Smooth(0f, ShellBurst, shellTime);
    }
    if (!BeginShellEffect(FrameRotation(PlanetFrame(planetAge)), wear, glow, planetHitPulse * planetHitPulse))
      return false;
    // r = 255 draws every plate (see PlanetShell.fx).
    m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, PlanetPos + PlanetShakeOffset(), null,
      new Color(255, 0, 0, 255), 0f, new Vector2(0.5f), PlanetFrameSize * PlanetSpriteScale(), SpriteEffects.None, 0f);
    m_spriteBatch.End();
    return true;
  }

  private void DrawShellFragments()
  {
    if (shellFragments.Count == 0 || shellTime < ShellBurst) return;
    float age = shellTime - ShellBurst;
    // The seams' light dies away as the plates fly.
    if (!BeginShellEffect(shellBurstRotation, PlanetShell.FullWear, 1f - Smooth(0f, 0.6f, age), 0f)) return;
    float travelled = (1f - MathF.Exp(-ShellFragmentDrag * age)) / ShellFragmentDrag;
    float alpha = 1f - Smooth(ShellFragmentSeconds * 0.45f, ShellFragmentSeconds, age);
    float eased = Smooth(0f, ShellFragmentSeconds, age);
    foreach (var fragment in shellFragments)
    {
      // Plates facing the viewer fly toward the camera, swelling and fading sooner.
      float grow = 1f + 0.6f * fragment.Facing * eased;
      float fade = alpha * (1f - 0.5f * fragment.Facing * eased);
      var position = PlanetPos + fragment.Centroid * shellBurstTexel + fragment.Direction * fragment.Speed * travelled;
      // Turn about the plate's own centre: the quad's origin is in texture units of the 1x1 texture.
      var origin = new Vector2(0.5f) + fragment.Centroid / PlanetFrameSize;
      m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, position, null,
        new Color(fragment.Plate, 0, 0, (int)(255 * Math.Clamp(fade, 0f, 1f))), fragment.Spin * age, origin,
        PlanetFrameSize * shellBurstTexel * grow, SpriteEffects.None, 0f);
    }
    m_spriteBatch.End();
  }
}
