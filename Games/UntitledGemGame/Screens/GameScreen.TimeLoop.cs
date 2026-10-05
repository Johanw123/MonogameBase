using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Tweening;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Prestige is a time loop (DesignDocument.txt): the planet implodes into a black
// hole that swallows everything, and time restarts with the ship crash-landing again.
//  1. Buildup (0-1.8 s): the planet throbs with quickening heartbeats, glowing
//     fissures spread across it and gravity starts to bend the space around it.
//  2. Implosion (1.8-2.6 s): it swells, then crushes inward to a white-hot point
//     while debris streaks in and a ring collapses onto it.
//  3. Black hole (2.6-4.45 s): a hole (BlackHole.fx) bursts out of the point; the
//     whole screen twists and is pulled into it (BlackHoleWarp.fx over the world).
//  4. Flash (4.45 s): under a white-out the field and the fleet are gone.
//  5. Unwind (4.45-5.6 s): the swirl unwinds onto an empty void with the hole where
//     the planet was. The prestige tree then opens over it.
//  6. Restart (when the tree closes): a white flash fades to the intact planet and
//     the ship crash-lands as it did at the very start.
public partial class UntitledGemGameGameScreen
{
  private const float CollapseBuildupSeconds = 1.8f;
  private const float CollapseImplodeSeconds = 2.6f;
  private const float PrestigeSwallowSeconds = 4.45f;
  private const float PrestigeCollapseSeconds = 5.6f;
  private const float LoopArrivalSeconds = 2.4f;
  private const float LoopFlashSeconds = 0.8f;
  private static readonly float[] CollapseHeartbeats = [0.25f, 0.7f, 1.05f, 1.3f, 1.48f, 1.62f, 1.72f];
  private const float BlackHoleHorizonFraction = 0.3f; // HORIZON in BlackHole.fx
  private const float LoopHoleRotation = -0.22f;
  private static readonly Color VoidGlowColor = new(170, 90, 255);
  private static readonly Color FissureColor = new(255, 96, 28);

  private bool prestigeSwallowed;
  private bool planetConsumed;
  private bool loopArrival;
  private float loopFlash;
  private RenderTarget2D warpTarget;

  private static float Smooth(float from, float to, float t)
    => MathHelper.SmoothStep(0f, 1f, Math.Clamp((t - from) / (to - from), 0f, 1f));

  private static float EaseOutBack(float x)
  {
    const float overshoot = 1.70158f;
    float shifted = x - 1f;
    return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
  }

  // A cheap, stable pseudo-random value per index, for fissures and debris.
  private static float Hash(int index) => MathF.Abs(MathF.Sin(index * 12.9898f) * 43758.5453f) % 1f;

  private void StartPrestigeCollapse()
  {
    prestigeSwallowed = planetConsumed = false;
    loopFlash = 0f;
    PulsePlanet(0.6f, 0.3f);
  }

  private void UpdatePrestigeCollapse(float dt)
  {
    float t = m_prestigeTime;
    float previous = t - dt;
    bool Crossed(float moment) => previous < moment && t >= moment;
    planetAge += dt;
    planetHitPulse = Math.Max(0f, planetHitPulse - dt * 2.5f);

    for (int i = 0; i < CollapseHeartbeats.Length; i++)
      if (Crossed(CollapseHeartbeats[i]))
        PulsePlanet(0.55f + 0.45f * i / (CollapseHeartbeats.Length - 1f));
    if (Crossed(CollapseBuildupSeconds))
    {
      // The implosion: a ring of light collapses onto the planet as it crushes inward.
      SpawnerEffects.Add(null, PlanetPos, Color.White, PlanetRadius * 3.2f, PlanetRadius * 0.2f, 0.8f);
      AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
    }
    if (Crossed(CollapseImplodeSeconds))
    {
      SpawnerEffects.Add(null, PlanetPos, VoidGlowColor, 4f, PlanetRadius * 2.6f, 0.7f);
      SpawnerEffects.Add(null, PlanetPos, Color.White, 2f, PlanetRadius * 1.4f, 0.4f);
      AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
    }
    if (t < CollapseBuildupSeconds)
      planetShake = 0.15f + 0.75f * Smooth(0f, CollapseBuildupSeconds, t);
    else if (t < CollapseImplodeSeconds)
    {
      planetShake = 1f;
      planetHitPulse = Math.Max(planetHitPulse, Smooth(CollapseBuildupSeconds, 2.5f, t));
    }
    else
      planetShake = t < PrestigeSwallowSeconds ? 0.3f : 0f;
    if (prestigeSwallowed || t < PrestigeSwallowSeconds) return;

    // Everything is gone: clear the field and the fleet, and hide the homebase below
    // the screen, ready to land again when time restarts.
    prestigeSwallowed = planetConsumed = true;
    UpdateSystem2.Instance.FinishPrestigeCollection();
    m_entityFactory.RemoveAllShips();
    ClearPlanetShots();
    m_homeBaseEntity.Get<Transform2>().Position = HomeBaseIntroStart();
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
  }

  // Runs every update: once the prestige tree has closed, time restarts.
  private void UpdateTimeLoop(float dt)
  {
    if (planetConsumed && !m_prestiging && !m_postPrestige)
      StartLoopRestart();
    loopFlash = Math.Max(0f, loopFlash - dt / LoopFlashSeconds);
    UpdateLoopCaption(dt);
  }

  private void StartLoopRestart()
  {
    // Time rewinds: the hole is gone and the planet is whole again under a white flash.
    planetConsumed = false;
    loopFlash = 1f;
    planetHitPulse = planetShake = 0f;
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
    StartLoopCaption();

    // The ship arrives exactly as it did the first time round.
    var transform = m_homeBaseEntity.Get<Transform2>();
    transform.Position = HomeBaseIntroStart();
    loopArrival = true;
    introAge = 0f;
    preGameTween = _tweenerPreGame.TweenTo(transform, t => t.Position, HomeBasePos, duration: LoopArrivalSeconds)
      .OnEnd(_ =>
      {
        loopArrival = false;
        FinishCrashIntro();
        AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
      })
      .Easing(EasingFunctions.CubicOut);
  }

  // The planet throbs during the buildup, swells, then crushes inward to nothing.
  private float PlanetLoopScale()
  {
    if (!m_prestiging) return 1f;
    float t = m_prestigeTime;
    if (t < CollapseBuildupSeconds)
      return 1f + 0.03f * MathF.Sin(t * 25f) * Smooth(0.5f, CollapseBuildupSeconds, t);
    float swell = 0.1f * Smooth(CollapseBuildupSeconds, 1.95f, t);
    float crush = Smooth(1.95f, CollapseImplodeSeconds, t);
    return (1f + swell) * (1f - crush * crush);
  }

  // Event horizon radius in world units: born from the crushed planet, swelling as it
  // swallows everything, then settling in the void until time restarts.
  private float LoopHoleHorizon()
  {
    float t = m_prestigeTime;
    if (planetConsumed)
    {
      float settle = m_prestiging ? Smooth(PrestigeSwallowSeconds, PrestigeCollapseSeconds, t) : 1f;
      return PlanetRadius * MathHelper.Lerp(0.72f, 0.5f, settle) * (1f + 0.03f * MathF.Sin(planetAge * 2.2f));
    }
    if (!m_prestiging) return 0f;
    return PlanetRadius * (0.5f * EaseOutBack(Smooth(CollapseImplodeSeconds, 3.1f, t))
      + 0.22f * Smooth(3.1f, 4.2f, t));
  }

  // Fissures, infalling debris and the white-hot core as the planet implodes.
  private void DrawTimeLoopEffects()
  {
    float t = m_prestigeTime;
    if (m_prestiging && !planetConsumed && t < CollapseImplodeSeconds + 0.2f)
    {
      float feather = 1.25f / Math.Max(0.1f, m_camera.Zoom);
      float planetScale = PlanetLoopScale();
      // Molten fissures spread across the planet and burn whiter as it implodes.
      float spread = Smooth(0.3f, CollapseBuildupSeconds, t);
      float heat = Smooth(CollapseBuildupSeconds, CollapseImplodeSeconds, t);
      var fissure = Color.Lerp(FissureColor, new Color(255, 230, 190), heat) * (0.45f + 0.55f * spread);
      int fissures = (int)(3 + 11 * spread);
      m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.NonPremultiplied);
      for (int i = 0; i < fissures && planetScale > 0.02f; i++)
      {
        float angle = Hash(i) * MathHelper.TwoPi;
        var previousPoint = PlanetPos;
        for (int segment = 1; segment <= 3; segment++)
        {
          float reach = segment / 3f * (0.45f + 0.5f * Hash(i + 50)) * PlanetRadius * planetScale;
          float bend = angle + (Hash(i * 7 + segment) - 0.5f) * 0.8f;
          var point = PlanetPos + PlanetDirection(bend) * reach;
          m_shapeBatch.FillLine(previousPoint, point, 1.2f + 0.8f * heat, fissure, feather);
          previousPoint = point;
        }
      }
      m_shapeBatch.End();
      m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
      // Debris streaks inward as the planet crushes.
      if (t >= CollapseBuildupSeconds)
      {
        float fall = Smooth(CollapseBuildupSeconds, CollapseImplodeSeconds, t);
        for (int i = 0; i < 28; i++)
        {
          float angle = Hash(i + 100) * MathHelper.TwoPi + fall * 1.5f;
          float radius = PlanetRadius * (3.2f - 3.1f * MathF.Pow(Math.Clamp(fall + Hash(i + 200) * 0.25f, 0f, 1f), 1.5f));
          var head = PlanetPos + PlanetDirection(angle) * radius;
          var tail = PlanetPos + PlanetDirection(angle - 0.15f) * (radius + 26f);
          m_shapeBatch.FillLine(tail, head, 1.4f, Color.Lerp(FissureColor, Color.White, fall) * 0.8f, feather);
        }
      }
      // The core flares white-hot as the planet vanishes into a point.
      float core = Smooth(2.3f, 2.55f, t) * (1f - Smooth(CollapseImplodeSeconds, 2.8f, t));
      if (core > 0f)
        m_shapeBatch.FillCircle(PlanetPos, 6f + 22f * core, Color.White * core, Math.Max(feather, 10f));
      m_shapeBatch.End();
    }
  }

  // The hole is drawn over the warped world, so the warp pulls everything into it
  // without shrinking the hole itself. It fades out under the white flash.
  private void DrawTimeLoopHole()
  {
    if (!PlanetMiningEnabled) return;
    float flash = m_prestiging ? PrestigeWarpFlash(m_prestigeTime) : 0f;
    DrawBlackHole(PlanetPos, LoopHoleHorizon(), planetAge, LoopHoleRotation, 1f - flash);
  }

  // A procedural black hole (BlackHole.fx) with its event horizon at horizon world units.
  private void DrawBlackHole(Vector2 center, float horizon, float time, float rotation, float opacity)
  {
    var effect = EffectCache.BlackHoleFx;
    if (horizon <= 0.5f || opacity <= 0.01f || effect?.IsLoaded != true || effect.IsFailed) return;
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
    parameters["Time"]?.SetValue(time);
    parameters["Rotation"]?.SetValue(rotation);
    // The default texture is one texel, so the scale is the quad's size in world units.
    float size = 2f * horizon / BlackHoleHorizonFraction;
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: effect.Value);
    m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, center, null, Color.White * opacity, 0f,
      new Vector2(0.5f), size, SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }

  // The white-out as the hole swallows everything, and a dimmer one as it is born.
  private static float PrestigeWarpFlash(float t)
  {
    float birth = Smooth(2.5f, CollapseImplodeSeconds, t) * (1f - Smooth(CollapseImplodeSeconds, 2.8f, t));
    return Math.Max(0.35f * birth, Smooth(4.25f, PrestigeSwallowSeconds, t) * (1f - Smooth(4.7f, 5.4f, t)));
  }

  // The time-restart flash over the world (the HUD draws separately).
  private void DrawLoopFlash()
  {
    if (loopFlash <= 0f) return;
    var viewport = GraphicsDevice.Viewport;
    m_spriteBatch.Begin(blendState: BlendState.AlphaBlend);
    m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, new Rectangle(0, 0, viewport.Width, viewport.Height),
      Color.White * (loopFlash * loopFlash));
    m_spriteBatch.End();
  }

  // Warps the rendered world into the hole. It runs on the world before bloom and
  // before the HUD, so captures see it and the HUD stays sharp.
  private void ApplyPrestigeWarp()
  {
    if (!m_prestiging || !PlanetMiningEnabled) return;
    var effect = EffectCache.BlackHoleWarpFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    var bindings = GraphicsDevice.GetRenderTargets();
    if (bindings.Length == 0 || bindings[0].RenderTarget is not RenderTarget2D scene) return;

    float t = m_prestigeTime;
    float unwind = t >= PrestigeSwallowSeconds
      ? 1f - Smooth(PrestigeSwallowSeconds + 0.05f, PrestigeCollapseSeconds - 0.1f, t)
      : 1f;
    float swallow = Smooth(CollapseImplodeSeconds, 4.4f, t);
    // Gravity starts bending the space around the planet during the buildup.
    float twist = (0.6f * Smooth(0.6f, CollapseImplodeSeconds, t) + 12f * swallow * swallow) * unwind;
    float pull = (0.15f * Smooth(CollapseBuildupSeconds, CollapseImplodeSeconds, t) + 7f * swallow * swallow * swallow)
      * unwind;
    float flash = PrestigeWarpFlash(t);
    if (twist + pull + flash < 0.001f) return;

    if (warpTarget == null || warpTarget.Width != scene.Width || warpTarget.Height != scene.Height)
    {
      warpTarget?.Dispose();
      warpTarget = new RenderTarget2D(GraphicsDevice, scene.Width, scene.Height, false, scene.Format, DepthFormat.None);
    }
    GraphicsDevice.SetRenderTarget(warpTarget);
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
    m_spriteBatch.Draw(scene, Vector2.Zero, Color.White);
    m_spriteBatch.End();

    var projected = Vector3.Transform(new Vector3(PlanetPos, 0f), m_camera.GetBoundingFrustum().Matrix);
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, scene.Width, scene.Height, 0, 0, 1));
    parameters["Center"]?.SetValue(new Vector2((projected.X + 1f) * 0.5f, (1f - projected.Y) * 0.5f));
    parameters["Aspect"]?.SetValue(scene.Width / (float)scene.Height);
    parameters["Radius"]?.SetValue(2.2f);
    parameters["Twist"]?.SetValue(twist);
    parameters["Pull"]?.SetValue(pull);
    parameters["Flash"]?.SetValue(flash);

    GraphicsDevice.SetRenderTarget(scene);
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, effect: effect.Value);
    m_spriteBatch.Draw(warpTarget, new Rectangle(0, 0, scene.Width, scene.Height), Color.White);
    m_spriteBatch.End();
  }
}
