using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

// Gameplay recipes use small, fixed layer counts. Simulation and individual lifetimes
// stay on the GPU; weapons, drills and ship exhaust share an emission budget.
public partial class UntitledGemGameGameScreen
{
  private const float GameplayParticlesPerSecond = 4800f;
  private const float GameplayParticleBurstAllowance = 960f;
  private const int GameplayParticleLayersPerUpdate = 24;
  private bool gameplayParticlesEnabled = true;
  private float gameplayParticleMultiplier = 1f;
  private float gameplayParticleCredit = GameplayParticleBurstAllowance;
  private int gameplayParticleCount, gameplayParticleLayers, ordinaryParticleLayers;
  private float laserParticleClock;
  private int laserParticleBeam;

  private void RefillGameplayParticleBudget(float dt)
  {
    float mult = gameplayParticleMultiplier > 1.001f ? 4f : 1f;
    float maxAllowance = GameplayParticleBurstAllowance * (gameplayParticleMultiplier > 1.001f ? 6f : 1f);
    gameplayParticleCredit = Math.Min(maxAllowance,
      gameplayParticleCredit + Math.Min(dt, 0.1f) * GameplayParticlesPerSecond * mult);
    gameplayParticleCount = gameplayParticleLayers = ordinaryParticleLayers = 0;
  }

  private void ResetGameplayParticleBudget()
  {
    gameplayParticleCredit = GameplayParticleBurstAllowance;
    gameplayParticleCount = gameplayParticleLayers = ordinaryParticleLayers = 0;
    laserParticleClock = 0;
    laserParticleBeam = 0;
    thrusterParticleClock = 0;
    thrusterParticleShip = thrusterParticleBeat = 0;
  }

  private void EmitGameplayParticles(in GpuParticleBurst burst, bool majorEvent = false)
  {
    if (!gameplayParticlesEnabled || gameplayParticleMultiplier <= 0f || m_prestiging || m_postPrestige) return;
    bool overdrive = gameplayParticleMultiplier > 1.001f;
    // Ordinary hits leave sixteen pending descriptors for milestone effects, even
    // when MonoGame performs multiple Updates before the next Draw.
    if (!majorEvent && (ordinaryParticleLayers >= GameplayParticleLayersPerUpdate
      || gameplayParticleCredit < 1f || gpuParticles?.PendingBursts >= GpuParticleSystem.MaxPendingBursts - 16))
      return;
    if (overdrive && gpuParticles?.PendingBursts >= GpuParticleSystem.MaxPendingBursts - 4)
      return;
    var particles = GetParticleSystem();
    if (particles == null) return;
    if (overdrive && !burst.Streak && burst.Size.Y >= 1.8f && burst.Speed.Y <= 30f)
      return; // Skip stationary big-quad flash layers in overdrive so impact centers don't clip to white balls.
    bool isInward = burst.Motion == GpuParticleMotion.Inward;
    bool isTremorOrPod = majorEvent && (burst.Count < 180 || (burst.Radius <= 4f && burst.Spread < 0.5f));
    float effectiveMult = overdrive
      ? (isInward ? Math.Min(gameplayParticleMultiplier, 80f)
        : isTremorOrPod ? Math.Min(gameplayParticleMultiplier, 35f)
        : majorEvent ? Math.Min(gameplayParticleMultiplier, 500f)
        : Math.Min(gameplayParticleMultiplier, 6f))
      : gameplayParticleMultiplier;
    int maxPerLayer = majorEvent ? (isTremorOrPod ? 12000 : particles.Capacity / 4) : 360;
    int scaledCount = Math.Clamp((int)MathF.Round(burst.Count * effectiveMult), 1, maxPerLayer);
    int count = majorEvent ? scaledCount : Math.Min(scaledCount, (int)gameplayParticleCredit);
    if (count <= 0) return;
    var actualBurst = burst with { Count = count };
    if (overdrive)
    {
      float logMult = MathF.Log10(gameplayParticleMultiplier);
      byte targetAlpha = majorEvent
        ? (byte)Math.Clamp((int)MathF.Round(255f * MathF.Pow(18000f / Math.Max(count, 18000), 0.28f)), 88, 134)
        : (byte)72;
      var c = burst.Color;
      int minC = Math.Min((int)c.R, Math.Min((int)c.G, (int)c.B));
      int maxC = Math.Max(1, Math.Max((int)c.R, Math.Max((int)c.G, (int)c.B)));
      float boost = 255f / Math.Max(1, maxC - (int)(minC * 0.82f));
      var scaledColor = new Color(
        (int)Math.Clamp((c.R - minC * 0.82f) * boost, 0f, 255f),
        (int)Math.Clamp((c.G - minC * 0.82f) * boost, 0f, 255f),
        (int)Math.Clamp((c.B - minC * 0.82f) * boost, 0f, 255f),
        targetAlpha);
      if (isInward)
      {
        actualBurst = actualBurst with
        {
          Color = scaledColor,
          Size = new Vector2(0.65f, 1.55f),
          Streak = true
        };
      }
      else
      {
        float minSpeed = majorEvent ? 140f : 180f;
        float maxSpeed = majorEvent ? Math.Max(1150f, burst.Speed.Y * (1.6f + 1.0f * logMult)) : 680f;
        float newRadius = majorEvent ? Math.Max(burst.Radius * 1.35f, 36f) : 18f;
        actualBurst = actualBurst with
        {
          Color = scaledColor,
          Speed = new Vector2(minSpeed, maxSpeed),
          Lifetime = majorEvent ? new Vector2(0.80f, 1.65f) : new Vector2(0.45f, 0.95f),
          Size = majorEvent ? new Vector2(0.78f, 1.85f) : new Vector2(0.58f, 1.35f),
          Radius = Math.Max(newRadius, burst.InnerRadius + 4f),
          Spread = burst.Motion == default ? MathHelper.TwoPi : burst.Spread,
          Streak = true
        };
      }
    }
    int accepted = particles.Emit(actualBurst);
    if (accepted == 0) return;
    gameplayParticleCount += accepted;
    gameplayParticleLayers++;
    if (!majorEvent)
    {
      gameplayParticleCredit -= accepted;
      ordinaryParticleLayers++;
    }
  }

  private float ParticleOutwardAngle(Vector2 position)
  {
    var outward = position - PlanetPos;
    return outward.LengthSquared() > 1f ? MathF.Atan2(outward.Y, outward.X) : PlanetFacingAngle();
  }

  // Replaces the shared pixel-art explosion sheet for secondary blasts. Main
  // weapon impacts and core eruptions already have dedicated particle recipes.
  private void EmitBlastParticles(Vector2 position, float strength, Color color, bool majorEvent = false)
  {
    strength = Math.Clamp(strength, 0.4f, 4f);
    float reach = MathF.Sqrt(strength);
    int count = (int)(96 * strength);
    EmitGameplayParticles(new GpuParticleBurst(position, count, Color.Lerp(color, Color.White, 0.4f))
    {
      Speed = new Vector2(60, 170) * reach, Lifetime = new(0.18f, 0.55f),
      Size = new Vector2(0.55f, 1.2f) * Math.Min(reach, 1.5f), Radius = 3 * reach, Streak = true
    }, majorEvent);
    EmitGameplayParticles(new GpuParticleBurst(position, count / 3, color)
    {
      Speed = new Vector2(12, 50) * reach, Lifetime = new(0.35f, 0.9f),
      Size = new(0.65f, 1.5f), Radius = 4 * reach
    }, majorEvent);
    EmitGameplayParticles(new GpuParticleBurst(position, Math.Max(4, count / 24), Color.Lerp(color, Color.White, 0.7f))
    {
      Speed = new(5, 18), Lifetime = new(0.06f, 0.16f),
      Size = new Vector2(2, 5) * reach, Radius = 2 * reach
    }, majorEvent);
  }

  private void EmitWeaponImpactParticles(PlanetShot shot)
  {
    float direction = ParticleOutwardAngle(shot.End);
    bool rail = shot.Kind == PlanetShotKind.Rail;
    bool rocket = shot.Kind == PlanetShotKind.Rocket;
    bool major = rail || (rocket && !shot.Mini);
    int count = rail ? 220 : rocket ? (shot.Mini ? 48 : 128) : shot.Critical ? 80
      : shot.Kind == PlanetShotKind.Manual ? 40 : 24;
    Color hot = rail ? new Color(160, 230, 255) : shot.Critical ? new Color(255, 235, 140) : new Color(255, 185, 65);
    AddMiningImpact(shot.End, hot, rail ? 2f : rocket && !shot.Mini ? 1.5f : shot.Critical ? 1.3f : .85f);
    EmitGameplayParticles(new GpuParticleBurst(shot.End, count, hot)
    {
      Direction = direction, Spread = rail ? 0.65f : rocket ? 2.5f : 1.9f,
      Speed = rail ? new(150, 330) : rocket ? new(65, 190) : new(45, shot.Critical ? 170 : 115),
      Size = rail ? new(0.45f, 1f) : new(0.4f, 0.95f),
      Lifetime = new(0.16f, rail ? 0.6f : 0.45f), Radius = rocket ? 5f : 2f, Streak = true
    }, majorEvent: major);
    EmitGameplayParticles(new GpuParticleBurst(shot.End, Math.Max(6, count / 3), rail ? RailGlow : new Color(255, 90, 20))
    {
      Direction = direction, Spread = 2.8f, Speed = new(12, 48),
      Lifetime = new(0.35f, 0.85f), Size = new(0.55f, 1.4f), Radius = 3f
    }, majorEvent: major);
    if (rocket || rail || shot.Critical)
      EmitGameplayParticles(new GpuParticleBurst(shot.End, rail ? 22 : 10, hot)
      {
        Speed = new(8, 24), Lifetime = new(0.05f, 0.15f), Size = new(2, rail ? 6 : 4), Radius = 3
      });
  }

  private void UpdateLaserImpactParticles(float dt, int beams, float heat)
  {
    // Contact light is independent of the GPU spark toggle.
    if (heat <= 0f)
    {
      laserParticleClock = 0;
      return;
    }
    laserParticleClock += dt;
    if (laserParticleClock < 0.06f) return;
    // Sample at most four contact points, independently of damage ticks or upgrades.
    laserParticleClock %= 0.06f;
    for (int i = 0; i < Math.Min(4, beams); i++)
    {
      int beam = laserParticleBeam++ % beams;
      var position = LaserContact(beam);
      AddMiningImpact(position, new Color(255, 160, 65), .3f, debris: false);
      EmitGameplayParticles(new GpuParticleBurst(position, 7, new Color(255, 205, 105))
      {
        Direction = LaserContactAngle(beam), Spread = 1.9f, Speed = new(35, 100),
        Lifetime = new(0.12f, 0.32f), Size = new(0.3f, 0.65f), Radius = 1.5f, Streak = true
      });
    }
    laserParticleBeam %= beams;
  }

  private void EmitDrillContactParticles(DrillPod pod, int ticks = 1, bool landing = false)
  {
    float pressure = 1f + 0.06f * Math.Min(pod.Layers, 6);
    var position = pod.Path.End + PlanetDirection(pod.BoreAngle) * 3f;
    AddMiningImpact(position, DrillGlow, landing ? 1.2f : .45f, debris: landing);
    EmitGameplayParticles(new GpuParticleBurst(position, landing ? 100 : 24 * ticks, new Color(255, 205, 105))
    {
      Direction = pod.BoreAngle, Spread = landing ? 2f : 1.1f,
      Speed = new Vector2(75, 190) * pressure, Lifetime = new(0.18f, 0.55f),
      Size = new(0.55f, 1.15f), Radius = 2, Streak = true
    });
    EmitGameplayParticles(new GpuParticleBurst(position, landing ? 24 : 8 * ticks, DrillGlow)
    {
      Direction = pod.BoreAngle, Spread = 2f, Speed = new(12, 45),
      Lifetime = new(0.4f, 0.9f), Size = new(0.6f, 1.3f), Radius = 3
    });
  }

  private void EmitDrillFinishParticles(DrillPod pod, bool coreTap)
  {
    EmitGameplayParticles(new GpuParticleBurst(pod.Path.End, coreTap ? 420 : 160, new Color(255, 220, 130))
    {
      Direction = pod.BoreAngle, Spread = coreTap ? 0.35f : 1.6f,
      Speed = coreTap ? new(220, 420) : new(75, 200), Lifetime = new(0.4f, 1f),
      Size = new(0.65f, 1.45f), Radius = 3, Streak = true
    }, majorEvent: true);
    EmitGameplayParticles(new GpuParticleBurst(pod.Path.End, coreTap ? 100 : 48, new Color(255, 95, 20))
    {
      Direction = pod.BoreAngle, Spread = 2f, Speed = new(15, 65),
      Lifetime = new(0.6f, 1.2f), Size = new(0.6f, 1.5f), Radius = 4
    }, majorEvent: true);
  }

  private void EmitDrillRuptureParticles()
  {
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 360, DrillGlow)
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius * 0.95f, InnerRadius = PlanetRadius * 0.8f,
      Spread = 0.35f, Speed = new(65, 175), Lifetime = new(0.25f, 0.65f),
      Size = new(0.4f, 0.85f), Streak = true
    }, majorEvent: true);
  }

  private void EmitShellStrainParticles(int beat)
  {
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 32 + beat * 12, ShellLightColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius,
      InnerRadius = PlanetRadius * 0.94f, Spread = 0.4f, Speed = new(15, 55),
      Lifetime = new(0.15f, 0.4f), Size = new(0.4f, 0.85f), Streak = true
    }, majorEvent: true);
  }

  private void EmitShellBreakParticles()
  {
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 900, new Color(125, 210, 255))
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius, InnerRadius = PlanetRadius * 0.55f,
      Spread = 0.3f, Speed = new(110, 310), Lifetime = new(0.4f, 1.05f), Size = new(0.5f, 1.15f), Streak = true
    }, majorEvent: true);
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 500, ShellRimColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius, InnerRadius = PlanetRadius * 0.8f,
      Spread = 0.6f, Speed = new(40, 125), Lifetime = new(0.6f, 1.4f), Size = new(0.65f, 1.7f)
    }, majorEvent: true);
  }

  private void EmitFractureTremorParticles(int beat)
  {
    if (crackNetworks.Count == 0) return;
    var points = crackNetworks[^1][0].Points;
    // Sparks follow the growing fissure on the rotating surface, rather than
    // appearing randomly over the planet or on its hidden hemisphere.
    float growth = CrackGrowth * 0.9f;
    float rotation = FrameRotation(PlanetFrame(planetAge));
    for (int side = -1; side <= 1; side++)
    {
      int index = (int)((0.5f + side * growth * 0.5f) * (points.Length - 1));
      var view = ToView(points[index], rotation);
      if (view.Z <= 0f) continue;
      var position = PlanetPos + new Vector2(view.X, -view.Y) * CrackDiscRadius * BasePlanetScale * planetVisualSize;
      EmitGameplayParticles(new GpuParticleBurst(position, 12 + beat * 4, FractureHotColor)
      {
        Direction = ParticleOutwardAngle(position), Spread = 2.6f, Speed = new(18, 65),
        Lifetime = new(0.15f, 0.4f), Size = new(0.3f, 0.7f), Radius = 2, Streak = true
      }, majorEvent: true);
    }
  }

  private void EmitFractureCollapseParticles()
  {
    // With drag, this travel ends just above the surface; motes die before
    // crossing the centre and flying out the other side of the planet.
    float travel = PlanetRadius * 1.9f;
    float speed = travel * 2.3f;
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 720, new Color(255, 160, 60))
    {
      Motion = GpuParticleMotion.Inward, Radius = PlanetRadius + travel,
      InnerRadius = PlanetRadius + travel * 0.9f, Spread = 0.12f,
      Speed = new(speed * 0.95f, speed), Lifetime = new(0.58f, 0.7f),
      Size = new(0.35f, 0.85f), Streak = true
    }, majorEvent: true);
  }

  private void EmitCoreEruptionParticles()
  {
    var focus = CrackFocusWorld();
    EmitGameplayParticles(new GpuParticleBurst(focus, 1100, new Color(255, 215, 105))
    {
      Speed = new(180, 420), Lifetime = new(0.45f, 1.1f), Size = new(0.6f, 1.4f),
      Radius = PlanetRadius * 0.18f, Streak = true
    }, majorEvent: true);
    EmitGameplayParticles(new GpuParticleBurst(PlanetPos, 650, FractureColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = PlanetRadius * 0.9f, InnerRadius = PlanetRadius * 0.65f,
      Speed = new(75, 220), Spread = 0.3f, Lifetime = new(0.45f, 1.1f),
      Size = new(0.45f, 1f), Streak = true
    }, majorEvent: true);
    EmitGameplayParticles(new GpuParticleBurst(focus, 260, new Color(255, 85, 15))
    {
      Speed = new(18, 75), Lifetime = new(0.7f, 1.5f), Size = new(0.6f, 1.6f), Radius = PlanetRadius * 0.3f
    }, majorEvent: true);
  }
}
