using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Tweening;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Prestige is a time loop (DesignDocument.txt): the core extraction fails, the unstable
// planet implodes and goes supernova, and the black hole it leaves swallows everything.
// Time then restarts with the ship crash-landing again.
//  1. Instability (0-1.8 s): the drill beam bores in. The planet throbs with quickening
//     heartbeats as molten fissures spread over its turning surface (PlanetCollapse.fx)
//     and spit sparks.
//  2. Implosion (1.8-2.6 s): gravity wins. The surface is dragged into the core, light
//     bends around it and streams of debris fall in, until it is a white-hot point.
//  3. Supernova (2.6 s): the core rebounds. A refracting shock front races across the
//     screen (BlackHoleWarp.fx) behind a burst of GPU particles, and a black hole is born.
//  4. Swallow (2.6-4.45 s): the hole, a black spot ringed by a shader accretion disk,
//     bends the light around it and feeds:
//     the field streams into it (Gem.Swallow, drawn by the gem shader), then the rest of
//     the view is drawn in and fades into the void while the field and fleet are cleared.
//  5. Void (4.45-5.6 s): the starfield returns around the settled hole. The prestige tree
//     then opens over it.
//  6. Rewind (when the tree closes): the hole shrinks away, the planet re-forms out of an
//     inrush of light, and the ship crash-lands as it did at the very start.
// Nothing here wakes the gem population: the gems fall in on the GPU, a few thousand
// claimed per frame, so a full field costs no more than an empty one.
public partial class UntitledGemGameGameScreen
{
  private const float CollapseBuildupSeconds = 1.8f;
  private const float CollapseImplodeSeconds = 2.6f;
  private const float PrestigeSwallowSeconds = 4.45f;
  private const float PrestigeCollapseSeconds = 5.6f;
  // The field starts falling into the hole, and the last gems found have started by then.
  private const float HoleFeedStart = 2.7f;
  private const float HoleFeedEnd = 3.35f;
  private const float HoleFallSeconds = 0.4f;
  private const int HoleSwallowsPerFrame = 8000;
  // The rest of the view is drawn in, then fades into the void.
  private const float SuctionStart = 3.45f;
  private const float VoidFadeStart = 4.0f;
  private const float SupernovaShockSeconds = 1.35f;
  private const float LoopArrivalSeconds = 2.4f;
  private const float LoopRewindSeconds = 1.3f;
  private static readonly float[] CollapseHeartbeats = [0.25f, 0.7f, 1.05f, 1.3f, 1.48f, 1.62f, 1.72f];
  private static readonly Color VoidGlowColor = new(170, 90, 255);
  private static readonly Color FissureColor = new(255, 96, 28);
  private static readonly Color SupernovaColor = new(255, 232, 196);
  private static readonly Color RewindColor = new(150, 205, 255);

  private bool prestigeSwallowed;
  private bool planetConsumed;
  private bool loopArrival;
  // 1 when time restarts, falling to 0 as the planet re-forms.
  private float loopRewind;
  private int holeFeedCursor;
  private float accretionClock;
  private RenderTarget2D warpTarget;
  private RenderTargetBinding[] warpBindings = new RenderTargetBinding[1];

  private static float Smooth(float from, float to, float t)
    => MathHelper.SmoothStep(0f, 1f, Math.Clamp((t - from) / (to - from), 0f, 1f));

  private static float EaseOutBack(float x)
  {
    const float overshoot = 1.70158f;
    float shifted = x - 1f;
    return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
  }

  private void StartPrestigeCollapse()
  {
    prestigeSwallowed = planetConsumed = false;
    loopRewind = 0f;
    holeFeedCursor = 0;
    accretionClock = 0f;
    PulsePlanet(0.6f, 0.3f);
    EmitFissureSparks(0);
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
      {
        PulsePlanet(0.55f + 0.45f * i / (CollapseHeartbeats.Length - 1f));
        EmitFissureSparks(i + 1);
      }
    if (Crossed(CollapseBuildupSeconds))
    {
      EmitImplosionParticles();
      AudioManager.Instance.PlaySound(SoundEvent.TimeLoopCollapseBuildup);
    }
    if (Crossed(CollapseImplodeSeconds))
    {
      EmitSupernovaParticles();
      AudioManager.Instance.PlaySound(SoundEvent.TimeLoopCollapseImplode);
    }
    if (t >= HoleFeedStart && t < VoidFadeStart + 0.2f)
      EmitAccretionParticles(dt);
    if (t < CollapseBuildupSeconds)
      planetShake = 0.15f + 0.75f * Smooth(0f, CollapseBuildupSeconds, t);
    else if (t < CollapseImplodeSeconds)
      planetShake = 1f;
    else
      planetShake = 0f;
    // An escape keeps the run, so its field stays put.
    if (!_warpEscaping && t >= HoleFeedStart && t < PrestigeSwallowSeconds)
      FeedFieldIntoHole(t);
    if (_warpEscaping || prestigeSwallowed || t < PrestigeSwallowSeconds) return;

    // Everything is gone: clear the field and the fleet, and hide the homebase below
    // the screen, ready to land again when time restarts.
    prestigeSwallowed = planetConsumed = true;
    UpdateSystem2.Instance.FinishPrestigeCollection();
    m_entityFactory.RemoveAllShips();
    ClearPlanetShots();
    m_homeBaseEntity.Get<Transform2>().Position = HomeBaseIntroStart();
    AudioManager.Instance.PlaySound(SoundEvent.TimeLoopPlanetSwallowed);
  }

  // Claims loose gems and drops them into the hole on the GPU, a few thousand per frame,
  // resuming the scan where the last frame stopped. Nearer gems go first, so the field
  // drains in a wave sweeping outward from the hole, and everything has started falling
  // by HoleFeedEnd.
  private void FeedFieldIntoHole(float t)
  {
    var grid = HarvesterCollectionSystem.Instance?.flatSpatialHash;
    if (grid == null || UpdateSystem2.Instance == null) return;
    float window = Math.Max(0f, HoleFeedEnd - t);
    float reach = ViewReach();
    int slots = grid.AllocatedSlotCount;
    int budget = HoleSwallowsPerFrame;
    for (int scanned = 0; scanned < slots && budget > 0; scanned++)
    {
      if (holeFeedCursor >= slots) holeFeedCursor = 0;
      int i = holeFeedCursor++;
      ref var data = ref grid.Gems[i];
      if (!data.IsActive || data.ClaimState != 0) continue;
      var gem = UpdateSystem2.Instance.GetEntityP(data.EntityId)?.Get<Gem>();
      if (gem == null || !gem.IsLive || gem.PickedUp || gem.WasClicked || !grid.TryClaim(i)) continue;
      float distance = Vector2.Distance(gem.BoundingCircle.Center, PlanetPos) / reach;
      float delay = window * Math.Clamp(distance * 0.9f + 0.1f * Random.Shared.NextSingle() - 0.1f, 0f, 1f);
      // They shrink away at the event horizon (about half the planet's radius).
      gem.Swallow(PlanetPos, PlanetRadius, delay, HoleFallSeconds);
      --budget;
    }
  }

  // Runs every update: once the prestige tree has closed, time restarts.
  private void UpdateTimeLoop(float dt)
  {
    if (planetConsumed && !m_prestiging && !m_postPrestige)
      StartLoopRestart();
    if (loopRewind <= 0f) return;
    float before = loopRewind;
    loopRewind = Math.Max(0f, loopRewind - dt / LoopRewindSeconds);
    if (before > 0.5f && loopRewind <= 0.5f)
      EmitReformParticles();
  }

  private void StartLoopRestart()
  {
    // Time rewinds: the hole gives the planet back.
    planetConsumed = false;
    loopRewind = 1f;
    planetHitPulse = planetShake = 0f;
    AudioManager.Instance.PlaySound(SoundEvent.TimeLoopRestart);
    EmitRewindParticles();

    // The ship arrives exactly as it did the first time round.
    SyncPlanetSize(animate: false);
    PlaceHomeBaseAndPlanet();
    var transform = m_homeBaseEntity.Get<Transform2>();
    transform.Position = HomeBaseIntroStart();
    loopArrival = true;
    introAge = 0f;
    preGameTween = _tweenerPreGame.TweenTo(transform, t => t.Position, HomeBasePos, duration: LoopArrivalSeconds)
      .OnEnd(_ =>
      {
        loopArrival = false;
        FinishCrashIntro();
        AudioManager.Instance.PlaySound(SoundEvent.ShipCrashLand);
      })
      .Easing(EasingFunctions.CubicOut);
  }

  // The planet throbs during the buildup, swells, then crushes inward to nothing. When
  // time restarts it re-forms out of the shrinking hole.
  private float PlanetLoopScale()
  {
    if (loopRewind > 0f)
      return EaseOutBack(Smooth(0.4f, 1f, 1f - loopRewind));
    if (!m_prestiging) return 1f;
    float t = m_prestigeTime;
    if (t < CollapseBuildupSeconds)
      return 1f + 0.03f * MathF.Sin(t * 25f) * Smooth(0.5f, CollapseBuildupSeconds, t);
    float swell = 0.08f * Smooth(CollapseBuildupSeconds, 1.95f, t);
    float crush = Smooth(1.95f, CollapseImplodeSeconds, t);
    return (1f + swell) * (1f - crush * crush);
  }

  // How far the planet has cracked, how hot it burns and how far it has been crushed into
  // its core (PlanetCollapse.fx); false while it is just a planet.
  private bool PlanetCollapseState(out float crack, out float heat, out float crush)
  {
    crack = heat = crush = 0f;
    if (loopRewind > 0f)
    {
      // The re-forming planet un-crushes and cools.
      float progress = 1f - loopRewind;
      crush = 1f - Smooth(0.4f, 0.95f, progress);
      heat = 1f - Smooth(0.5f, 1f, progress);
      return true;
    }
    if (!m_prestiging) return FracturePlanetState(out crack, out heat, out crush);
    if (m_prestigeTime >= CollapseImplodeSeconds) return false;
    float t = m_prestigeTime;
    crack = Smooth(0.05f, 1.7f, t);
    heat = Math.Min(1f, 0.1f + 0.45f * Smooth(0.2f, CollapseBuildupSeconds, t)
      + 0.45f * Smooth(CollapseBuildupSeconds, 2.45f, t) + 0.3f * planetHitPulse * planetHitPulse);
    crush = Smooth(1.85f, CollapseImplodeSeconds, t);
    return true;
  }

  // Draws the planet through PlanetCollapse.fx while it destabilises, implodes or
  // re-forms. False when the ordinary sprite should be drawn instead.
  private bool DrawCollapsingPlanet(Texture2D texture, Rectangle source, int frame, Vector2 position,
    Vector2 origin, float scale, Color tint)
  {
    if (!PlanetCollapseState(out float crack, out float heat, out float crush)) return false;
    var effect = EffectCache.PlanetCollapseFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return false;
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(m_camera.ViewProjection());
    parameters["FrameRect"]?.SetValue(new Vector4(source.X / (float)texture.Width, source.Y / (float)texture.Height,
      source.Width / (float)texture.Width, source.Height / (float)texture.Height));
    parameters["Rotation"]?.SetValue(FrameRotation(frame));
    parameters["Crack"]?.SetValue(crack);
    parameters["Heat"]?.SetValue(heat);
    parameters["Crush"]?.SetValue(crush);
    parameters["Time"]?.SetValue(planetAge);
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: effect.Value);
    m_spriteBatch.Draw(texture, position, source, tint, 0f, origin, scale, SpriteEffects.None, 0f);
    m_spriteBatch.End();
    return true;
  }

  // Event horizon radius in world units: born from the crushed planet, swelling as it
  // feeds, then settling in the void until time restarts and it gives the planet back.
  private float LoopHoleHorizon()
  {
    float t = m_prestigeTime;
    if (loopRewind > 0f)
      return PlanetRadius * 0.5f * Smooth(0.5f, 1f, loopRewind);
    if (planetConsumed)
    {
      float settle = m_prestiging ? Smooth(PrestigeSwallowSeconds, PrestigeCollapseSeconds, t) : 1f;
      return PlanetRadius * MathHelper.Lerp(0.72f, 0.5f, settle) * (1f + 0.03f * MathF.Sin(planetAge * 2.2f));
    }
    if (!m_prestiging) return 0f;
    return PlanetRadius * (0.5f * EaseOutBack(Smooth(CollapseImplodeSeconds, 3.1f, t))
      + 0.22f * Smooth(3.1f, 4.2f, t));
  }

  // A newborn hole's disk burns white-hot, then cools.
  private float LoopHoleHeat()
    => m_prestiging && !planetConsumed ? 1f - Smooth(CollapseImplodeSeconds, 3.8f, m_prestigeTime) : 0f;

  // What the full-screen lens (BlackHoleWarp.fx) does this frame. Distances are in screen
  // heights, centred on the planet.
  private struct WorldLens
  {
    public float Horizon, LensRadius, LensCore, Pull, Twist, SwirlReach;
    public float ShockRadius, ShockWidth, ShockStrength, ShockGlow;
    public float Glow, GlowRadius, Fade, Rattle, DiskGlow, DiskHeat;
    public Vector3 GlowColor;
  }

  // Bends the rendered world around the planet and draws the black hole itself (a black
  // spot with an accretion disk) during an extraction, and drives the core fracture's
  // gravity and shock front. It runs on the world before bloom and before the HUD, so
  // captures see it and the HUD stays sharp.
  private void ApplyWorldLens()
  {
    if (!PlanetMiningEnabled) return;
    var effect = EffectCache.BlackHoleWarpFx;
    if (effect?.IsLoaded != true || effect.IsFailed) return;
    var viewProjection = m_camera.ViewProjection();
    float units = MathF.Abs(viewProjection.M22) * 0.5f;
    WorldLens lens;
    if (m_prestiging || planetConsumed || loopRewind > 0f)
      lens = ExtractionLens(units);
    else if (!FractureLens(units, out lens))
      return;

    if (GraphicsDevice.RenderTargetCount == 0) return;
    if (warpBindings.Length != GraphicsDevice.RenderTargetCount)
      warpBindings = new RenderTargetBinding[GraphicsDevice.RenderTargetCount];
    GraphicsDevice.GetRenderTargets(warpBindings);
    if (warpBindings[0].RenderTarget is not RenderTarget2D scene) return;

    var shake = lens.Rattle > 0f
      ? new Vector2((Random.Shared.NextSingle() - 0.5f) * scene.Height / scene.Width, Random.Shared.NextSingle() - 0.5f) * 2f * lens.Rattle
      : Vector2.Zero;

    if (warpTarget == null || warpTarget.Width != scene.Width || warpTarget.Height != scene.Height)
    {
      warpTarget?.Dispose();
      warpTarget = new RenderTarget2D(GraphicsDevice, scene.Width, scene.Height, false, scene.Format, DepthFormat.None);
    }
    GraphicsDevice.SetRenderTarget(warpTarget);
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
    m_spriteBatch.Draw(scene, Vector2.Zero, Color.White);
    m_spriteBatch.End();

    var projected = Vector3.Transform(new Vector3(PlanetPos, 0f), viewProjection);
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, scene.Width, scene.Height, 0, 0, 1));
    parameters["Center"]?.SetValue(new Vector2((projected.X + 1f) * 0.5f, (1f - projected.Y) * 0.5f));
    parameters["Aspect"]?.SetValue(scene.Width / (float)scene.Height);
    parameters["Shake"]?.SetValue(shake);
    parameters["Horizon"]?.SetValue(lens.Horizon);
    parameters["Lens"]?.SetValue(lens.LensRadius * lens.LensRadius);
    parameters["LensCore"]?.SetValue(lens.LensCore);
    parameters["Pull"]?.SetValue(lens.Pull);
    parameters["Twist"]?.SetValue(lens.Twist);
    parameters["SwirlReach"]?.SetValue(Math.Max(lens.SwirlReach, 0.03f));
    parameters["ShockRadius"]?.SetValue(lens.ShockRadius);
    parameters["ShockWidth"]?.SetValue(Math.Max(lens.ShockWidth, 0.01f));
    parameters["ShockStrength"]?.SetValue(lens.ShockStrength);
    parameters["ShockGlow"]?.SetValue(lens.ShockGlow);
    parameters["GlowColor"]?.SetValue(lens.GlowColor);
    parameters["Glow"]?.SetValue(lens.Glow);
    parameters["GlowRadius"]?.SetValue(Math.Max(lens.GlowRadius, 0.001f));
    parameters["Fade"]?.SetValue(lens.Fade);
    parameters["DiskTime"]?.SetValue(planetAge);
    parameters["DiskGlow"]?.SetValue(lens.DiskGlow);
    parameters["DiskHeat"]?.SetValue(lens.DiskHeat);
    parameters["DiskReach"]?.SetValue(2.7f);

    GraphicsDevice.SetRenderTarget(scene);
    m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, effect: effect.Value);
    m_spriteBatch.Draw(warpTarget, new Rectangle(0, 0, scene.Width, scene.Height), Color.White);
    m_spriteBatch.End();
  }

  private WorldLens ExtractionLens(float units)
  {
    float t = m_prestigeTime;
    bool collapsing = m_prestiging && !planetConsumed;
    bool fullSequence = collapsing && !_warpEscaping;
    var lens = new WorldLens { ShockWidth = 0.05f, GlowColor = SupernovaColor.ToVector3() };
    lens.Horizon = LoopHoleHorizon() * units;
    lens.DiskGlow = lens.Horizon > 0.0005f ? 1f : 0f;
    lens.DiskHeat = LoopHoleHeat();
    lens.SwirlReach = lens.Horizon * 2.5f;

    // Light bends around the core as its mass concentrates, then around the hole.
    lens.LensRadius = collapsing && t < CollapseImplodeSeconds
      ? PlanetRadius * 0.85f * units * Smooth(1f, CollapseImplodeSeconds, t)
      : lens.Horizon * 1.55f;

    // The supernova's shock front, or when time restarts, a front rushing back in.
    if (collapsing && t >= CollapseImplodeSeconds && t < CollapseImplodeSeconds + SupernovaShockSeconds)
    {
      float progress = (t - CollapseImplodeSeconds) / SupernovaShockSeconds;
      float remaining = 1f - progress;
      lens.ShockRadius = 0.02f + 1.9f * (1f - MathF.Pow(remaining, 2.2f));
      lens.ShockWidth = 0.025f + 0.06f * progress;
      lens.ShockStrength = 0.03f * MathF.Pow(remaining, 1.5f);
      lens.ShockGlow = 0.3f * remaining * remaining;
    }
    else if (loopRewind > 0f)
    {
      float progress = Smooth(0f, 0.55f, 1f - loopRewind);
      float wave = MathF.Sin(MathF.PI * progress);
      lens.ShockRadius = 1.5f * (1f - progress);
      lens.ShockStrength = 0.02f * wave;
      lens.ShockGlow = 0.15f * wave;
    }

    // The hole draws in the field, then the rest of the view, with a swirl near it.
    float suction = fullSequence ? Smooth(SuctionStart, PrestigeSwallowSeconds, t) : 0f;
    lens.Pull = 6f * suction * suction * suction;
    if (collapsing && t >= CollapseImplodeSeconds)
      lens.Twist = 1.1f * Smooth(2.8f, 3.6f, t) + 2.4f * suction;
    else if (planetConsumed)
      lens.Twist = 0.9f;

    // The core flares as it collapses and as it bursts, then dies away.
    float flare = collapsing ? Smooth(2.25f, 2.58f, t) * (1f - Smooth(CollapseImplodeSeconds, 3.25f, t)) : 0f;
    lens.Glow = 1.3f * flare;
    lens.GlowRadius = PlanetRadius * units * (0.35f + 1.3f * Smooth(CollapseImplodeSeconds, 3f, t));
    if (loopRewind > 0f)
    {
      lens.Glow = 0.7f * MathF.Sin(MathF.PI * Smooth(0.3f, 1f, 1f - loopRewind));
      lens.GlowRadius = PlanetRadius * units * 1.4f;
      lens.GlowColor = RewindColor.ToVector3();
    }

    // The swallowed view fades into the void; the starfield returns around the hole.
    lens.Fade = fullSequence ? Smooth(VoidFadeStart, PrestigeSwallowSeconds, t) : 0f;
    if (m_prestiging && planetConsumed)
      lens.Fade = 1f - Smooth(4.6f, 5.5f, t);

    // The view rattles as the core caves in and as it bursts.
    lens.Rattle = collapsing
      ? 0.0015f * Smooth(CollapseBuildupSeconds, CollapseImplodeSeconds, t) * (t < CollapseImplodeSeconds ? 1f : 0f)
        + (t >= CollapseImplodeSeconds ? 0.006f * (1f - Smooth(CollapseImplodeSeconds, 3.2f, t)) : 0f)
      : 0f;
    return lens;
  }

  // ---- GPU particles for big events (extraction, escape, core fracture): fixed counts at
  // fixed moments, outside the gameplay budget that weapons and drills share (and which
  // is closed while prestiging).

  private void EmitEventParticles(in GpuParticleBurst burst)
  {
    if (gameplayParticlesEnabled) GetParticleSystem()?.Emit(burst);
  }

  // Half the view's diagonal in world units: far enough to start off screen.
  private float ViewReach()
  {
    var view = m_camera.BoundingRectangle;
    return 0.5f * MathF.Sqrt(view.Width * view.Width + view.Height * view.Height);
  }

  // The launch speed that, against the pool's drag, carries a particle `distance` in `lifetime`.
  private float InfallSpeed(float distance, float lifetime)
  {
    float drag = gpuParticles?.Drag ?? 1.5f;
    if (drag < 0.01f) return distance / lifetime;
    return distance * drag / (1f - MathF.Exp(-drag * lifetime));
  }

  // A stream of particles falling from an annulus onto the centre, dying as they reach `end`.
  private void EmitInfall(Vector2 center, int count, Color color, float outer, float inner, float end,
    float lifetime, Vector2 size, float spread = 0.08f)
  {
    float speed = InfallSpeed(0.5f * (outer + inner) - end, lifetime);
    EmitEventParticles(new GpuParticleBurst(center, count, color)
    {
      Motion = GpuParticleMotion.Inward, Radius = outer, InnerRadius = inner, Spread = spread,
      Speed = new Vector2(speed * 0.96f, speed), Lifetime = new Vector2(lifetime * 0.94f, lifetime),
      Size = size, Streak = true
    });
  }

  // Each heartbeat spits more sparks and embers off the cracking surface.
  private void EmitFissureSparks(int beat)
  {
    float r = PlanetRadius;
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 50 + 35 * beat, Color.Lerp(FissureColor, Color.White, 0.35f))
    {
      Motion = GpuParticleMotion.Outward, Radius = r * 0.98f, InnerRadius = r * 0.82f, Spread = 0.7f,
      Speed = new Vector2(40f, 120f + 30f * beat), Lifetime = new(0.25f, 0.75f),
      Size = new(0.4f, 1f), Streak = true
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 16 + 8 * beat, FissureColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = r, InnerRadius = r * 0.85f, Spread = 1.2f,
      Speed = new(8f, 35f), Lifetime = new(0.6f, 1.3f), Size = new(0.7f, 1.6f)
    });
  }

  // Gravity turns inward: debris from across the view streams into the crushing core.
  private void EmitImplosionParticles()
  {
    float reach = ViewReach();
    float r = PlanetRadius;
    float lifetime = CollapseImplodeSeconds - CollapseBuildupSeconds - 0.06f;
    EmitInfall(PlanetPos, 1600, Color.Lerp(VoidGlowColor, Color.White, 0.3f), reach * 1.1f, reach * 0.45f,
      r * 0.15f, lifetime, new(0.5f, 1.3f));
    EmitInfall(PlanetPos, 900, Color.Lerp(FissureColor, Color.White, 0.4f), r * 3.6f, r * 1.6f,
      r * 0.1f, lifetime * 0.85f, new(0.45f, 1.1f));
  }

  // The core rebounds: hot streaks, embers and glowing gas blast out, with a brief
  // flash at the heart (bloom carries it; the screen is never whited out).
  private void EmitSupernovaParticles()
  {
    float reach = ViewReach();
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 3200, SupernovaColor)
    {
      Speed = new Vector2(0.35f, 1.4f) * reach, Lifetime = new(0.6f, 1.6f),
      Size = new(0.6f, 1.7f), Radius = 4f, Streak = true
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 1500, new Color(255, 120, 40))
    {
      Speed = new Vector2(0.08f, 0.5f) * reach, Lifetime = new(1f, 2.2f),
      Size = new(0.8f, 2.2f), Radius = PlanetRadius * 0.2f
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 450, VoidGlowColor * 0.55f)
    {
      Speed = new Vector2(0.04f, 0.25f) * reach, Lifetime = new(0.8f, 1.9f),
      Size = new(3f, 7f), Radius = PlanetRadius * 0.3f
    });
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 60, Color.White)
    {
      Speed = new(5f, 30f), Lifetime = new(0.08f, 0.22f), Size = new(8f, 18f), Radius = 4f
    });
  }

  // The newborn hole feeds: thin streams fall in from off screen and vanish at its horizon.
  private void EmitAccretionParticles(float dt)
  {
    accretionClock += dt;
    const float interval = 0.08f;
    if (accretionClock < interval) return;
    accretionClock %= interval;
    float reach = ViewReach();
    bool hot = Random.Shared.NextSingle() < 0.5f;
    EmitInfall(PlanetPos, 110, hot ? new Color(255, 150, 70) : Color.Lerp(VoidGlowColor, Color.White, 0.25f),
      reach * 1.15f, reach * 0.55f, Math.Max(LoopHoleHorizon(), PlanetRadius * 0.4f), 0.6f, new(0.4f, 1.1f), 0.35f);
  }

  // Time restarts: light rushes back into the point the hole leaves behind.
  private void EmitRewindParticles()
  {
    float reach = ViewReach();
    EmitInfall(PlanetPos, 1400, RewindColor, reach * 0.95f, reach * 0.35f, PlanetRadius * 0.1f,
      LoopRewindSeconds * 0.5f, new(0.5f, 1.2f));
  }

  // ...and the planet re-forms with a soft breath of light.
  private void EmitReformParticles()
  {
    EmitEventParticles(new GpuParticleBurst(PlanetPos, 500, Color.Lerp(RewindColor, Color.White, 0.4f))
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius * 0.6f, InnerRadius = PlanetRadius * 0.2f,
      Spread = 0.5f, Speed = new(40f, 160f), Lifetime = new(0.4f, 1f), Size = new(0.5f, 1.3f), Streak = true
    });
  }
}
