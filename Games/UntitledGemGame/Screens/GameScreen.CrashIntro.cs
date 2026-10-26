using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  internal bool IntroTransitionPending { get; set; }
  private bool introFrameDrawn;
  private float introAge;
  private float introSettleRemaining;
  private float introSparkTimer;
  private readonly Random introRandom = new();
  private readonly List<CrashSpark> introSparks = new();
  private MainMenu.MenuFlightHandoff menuFlightHandoff;
  private Vector2[] menuPassingPlanetStartPositions;
  private float introPlanetTravelDistance = 265f;
  private float introPassingPlanetTravelDistance = 355f;
  private const float IntroBackgroundScrollTravel = 55f;
  public Vector2 IntroShakeOffset { get; private set; }
  public float IntroBankAngle { get; private set; }

  private sealed class CrashSpark
  {
    public Vector2 Position, Velocity;
    public float Age, Lifetime;
  }

  public void SetMenuFlightHandoff(MainMenu.MenuFlightHandoff handoff)
  {
    menuFlightHandoff = handoff;
    if (handoff == null) return;
    introFrameDrawn = true;
    menuPassingPlanetStartPositions = new Vector2[handoff.PassingPlanets.Count];
    for (int i = 0; i < handoff.PassingPlanets.Count; i++)
      menuPassingPlanetStartPositions[i] = handoff.PassingPlanets[i].Position;
  }

  // What the title screen flies, so Continue (or a fresh game) picks up without a jump:
  // the hull at the size it is drawn, its turrets and the camera's zoom.
  internal TitleShip CaptureTitleShip()
  {
    var upgrades = m_upgradeManager.UG;
    float hullScale = 1f;
    if (m_homeBaseEntity != null)
    {
      var home = m_homeBaseEntity.Get<Harvester>();
      float baseRange = BaseStats.GetHarvesterBaseCollectionRange(home);
      hullScale = m_homeBaseEntity.Get<Transform2>().Scale.X
        * (baseRange > 0f ? BaseStats.GetHarvesterCollectionRange(home) / baseRange : 1f);
    }
    return new TitleShip
    {
      CameraZoomScale = upgrades.CameraZoomScale,
      HullScale = hullScale,
      MiningLaser = upgrades.MiningLaser,
      ArcHarpoon = upgrades.ArcHarpoon,
      RocketPods = upgrades.RocketPods,
      Railgun = upgrades.Railgun,
    };
  }

  public void AdoptMenuParticles(GpuParticleSystem particles)
  {
    if (particles == null) return;
    gpuParticles?.Dispose();
    gpuParticles = particles;
  }

  private void ConfigureMenuFlightPlanetTravel()
  {
    if (menuFlightHandoff == null) return;
    var center = m_camera.ScreenToWorld(JapeFramework.BaseGame.ViewportCenter);
    var vp = JapeFramework.BaseGame.BoxingViewportAdapter.Viewport;
    var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
    float targetPlanetRadius = 31f * BasePlanetScale * planetVisualSize;
    float highestTopEdge = p0.Y - 12f;
    for (int i = 0; i < menuFlightHandoff.PassingPlanets.Count; i++)
    {
      var p = menuFlightHandoff.PassingPlanets[i];
      float topEdge = p.Position.Y - p.Radius;
      if (topEdge < highestTopEdge) highestTopEdge = topEdge;
    }
    float startY = Math.Min(p0.Y - targetPlanetRadius - 18f, highestTopEdge - targetPlanetRadius - 42f);
    introPlanetTravelDistance = Math.Max(250f, center.Y - startY);
    introPassingPlanetTravelDistance = introPlanetTravelDistance + 95f;
  }

  private float IntroDecelerationProgress()
  {
    if (GameStarted || menuFlightHandoff == null) return 1f;
    float u = Math.Clamp(introAge / 3.0f, 0f, 1f);
    return 1f - MathF.Pow(1f - u, 3f);
  }

  public float CurrentBackgroundScroll
  {
    get
    {
      if (menuFlightHandoff == null) return EscapeBackgroundScroll;
      if (GameStarted) return menuFlightHandoff.BackgroundScroll + IntroBackgroundScrollTravel + EscapeBackgroundScroll;
      float u = Math.Clamp(introAge / 3.0f, 0f, 1f);
      float ease = 1f - (1f - u) * (1f - u);
      return menuFlightHandoff.BackgroundScroll + ease * IntroBackgroundScrollTravel;
    }
  }

  public Vector2 IntroPlanetOffset
    => (!GameStarted && menuFlightHandoff != null)
      ? new Vector2(0f, -(1f - IntroDecelerationProgress()) * introPlanetTravelDistance)
      : Vector2.Zero;

  public bool IntroEngineOn => (!GameStarted || loopArrival)
    && MathF.Sin(introAge * 23f) + MathF.Sin(introAge * 41f) > -0.25f;

  private Vector2 IntroExhaustPosition(float side)
  {
    var transform = m_homeBaseEntity.Get<Transform2>();
    float size = BaseStats.GetHarvesterCollectionRangeMultiplier(m_homeBaseEntity.Get<Harvester>());
    if (!PrerenderedShips.TryGetExhaustOffset(Harvester.HarvesterType.HomeBase, side, out var baseOffset))
      baseOffset = new Vector2(side * TextureCache.HomeBase.Width * 0.12f, TextureCache.HomeBase.Height * 0.25f);
    var offset = baseOffset * transform.Scale * size;
    return transform.Position + IntroShakeOffset
      + Vector2.Transform(offset, Matrix.CreateRotationZ(transform.Rotation + IntroBankAngle));
  }

  private void EmitCrashSparks(int count, bool impact)
  {
    if (GetParticleSystem() != null)
    {
      for (int side = -1; side <= 1; side += 2)
      {
        var position = IntroExhaustPosition(side);
        float direction = m_homeBaseEntity.Get<Transform2>().Rotation + IntroBankAngle + MathHelper.PiOver2;
        EmitGameplayParticles(new GpuParticleBurst(position, count * 4, new Color(255, 215, 110))
        {
          Direction = direction, Spread = impact ? 2.8f : 1.5f,
          Speed = impact ? new(90, 280) : new(50, 150),
          Lifetime = new(0.18f, impact ? 0.85f : 0.5f),
          Size = new(0.55f, 1.25f), Radius = 2f, Streak = true
        }, majorEvent: impact);
        EmitGameplayParticles(new GpuParticleBurst(position, impact ? 48 : 8, new Color(255, 75, 15))
        {
          Direction = direction, Spread = 2.4f, Speed = new(12, impact ? 70 : 40),
          Lifetime = new(0.3f, impact ? 1.2f : 0.65f), Size = new(0.8f, 2.2f), Radius = 3
        }, majorEvent: impact);
      }
      return;
    }
    // Browser fallback while the GPU renderer targets DesktopGL.
    for (int i = 0; i < count && introSparks.Count < 96; i++)
    {
      float angle = MathHelper.PiOver2 + (introRandom.NextSingle() - 0.5f) * 1.8f;
      float speed = (impact ? 100f : 45f) + introRandom.NextSingle() * (impact ? 190f : 90f);
      introSparks.Add(new CrashSpark
      {
        Position = IntroExhaustPosition(i % 2 == 0 ? -1 : 1),
        Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
        Lifetime = 0.25f + introRandom.NextSingle() * (impact ? 0.7f : 0.35f)
      });
    }
  }

  private void UpdateCrashIntro(float dt)
  {
    bool landing = !GameStarted || loopArrival;
    if (!landing && introSettleRemaining <= 0f && introSparks.Count == 0)
    {
      IntroShakeOffset = Vector2.Zero;
      IntroBankAngle = 0f;
      return;
    }
    introAge += dt;
    introSettleRemaining = Math.Max(0f, introSettleRemaining - dt);
    float strength = landing ? 2.5f + 2f * Math.Clamp(introAge / 3f, 0, 1)
      : 7f * introSettleRemaining / 0.45f;
    IntroShakeOffset = new Vector2(MathF.Sin(introAge * 79f), MathF.Sin(introAge * 103f)) * strength;
    float driftBank = (!GameStarted && menuFlightHandoff != null)
      ? -0.09f * MathF.Sin(Math.Clamp(introAge / 3f, 0f, 1f) * MathF.PI)
      : 0f;
    IntroBankAngle = driftBank + MathF.Sin(introAge * 27f) * strength * 0.006f;

    if (!GameStarted)
    {
      planetAge += dt;
      if (menuFlightHandoff != null)
      {
        PlaceHomeBaseAndPlanet();
        float decel = IntroDecelerationProgress();
        for (int i = 0; i < menuFlightHandoff.PassingPlanets.Count; i++)
        {
          var p = menuFlightHandoff.PassingPlanets[i];
          p.Position = menuPassingPlanetStartPositions[i] + new Vector2(0f, decel * introPassingPlanetTravelDistance);
          menuFlightHandoff.PassingPlanets[i] = p;
        }
      }
    }

    for (int i = introSparks.Count - 1; i >= 0; i--)
    {
      var spark = introSparks[i];
      spark.Age += dt;
      if (spark.Age >= spark.Lifetime) { introSparks.RemoveAt(i); continue; }
      spark.Position += spark.Velocity * dt;
      spark.Velocity *= MathF.Exp(-2f * dt);
    }
    if (landing)
    {
      introSparkTimer += dt;
      if (introSparkTimer >= 0.07f)
      {
        introSparkTimer %= 0.07f;
        if (!IntroEngineOn) EmitCrashSparks(3, false);
        else
        {
          var transform = m_homeBaseEntity.Get<Transform2>();
          float size = BaseStats.GetHarvesterCollectionRangeMultiplier(m_homeBaseEntity.Get<Harvester>());
          EmitThrusterParticles(Harvester.HarvesterType.HomeBase, transform.Position + IntroShakeOffset,
            transform.Rotation + IntroBankAngle, transform.Scale * size, 1f, boosted: true);
        }
      }
    }
  }

  // Just below the viewport, where the homebase starts its crash landing.
  private Vector2 HomeBaseIntroStart()
  {
    var transform = m_homeBaseEntity.Get<Transform2>();
    // Invert the exact world-to-clip matrix used by the ship shader. Clip Y=-1
    // is the bottom edge; -1.16 adds an 8% screen-height margin at every zoom.
    var inverseProjection = Matrix.Invert(m_camera.ViewProjection());
    var bottom = Vector3.Transform(new Vector3(0f, -1.16f, 0f), inverseProjection);
    float size = BaseStats.GetHarvesterCollectionRangeMultiplier(m_homeBaseEntity.Get<Harvester>());
    float radius = (new Vector2(TextureCache.HomeBase.Width, TextureCache.HomeBase.Height)
      * transform.Scale * size).Length() * 0.5f;
    return new Vector2(HomeBasePos.X, bottom.Y + radius + 8f);
  }

  private void FinishCrashIntro()
  {
    PlaceHomeBaseAndPlanet();
    introSettleRemaining = 0.45f;
    EmitCrashSparks(40, true);
  }

  private void DrawCrashIntro()
  {
    if (!GameStarted && menuFlightHandoff?.PassingPlanets is { Count: > 0 } passingPlanets)
      MainMenu.DrawPassingPlanets(m_spriteBatch, m_camera, passingPlanets, menuFlightHandoff.FlightTime + introAge);

    if (GameStarted && !loopArrival && introSparks.Count == 0) return;
    m_shapeBatch.Begin(m_camera.GetViewMatrix());
    foreach (var spark in introSparks)
    {
      float fade = 1f - spark.Age / spark.Lifetime;
      var color = Color.Lerp(Color.OrangeRed, Color.LightYellow, fade) * fade;
      m_shapeBatch.FillLine(spark.Position, spark.Position - spark.Velocity * 0.035f, 1.2f, color, 2.5f);
    }
    m_shapeBatch.End();
  }
}
