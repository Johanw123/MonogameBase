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
  public Vector2 IntroShakeOffset { get; private set; }
  public float IntroBankAngle { get; private set; }

  private sealed class CrashSpark
  {
    public Vector2 Position, Velocity;
    public float Age, Lifetime;
  }

  public bool IntroEngineOn => (!GameStarted || loopArrival)
    && MathF.Sin(introAge * 23f) + MathF.Sin(introAge * 41f) > -0.25f;

  private Vector2 IntroExhaustPosition(float side)
  {
    var transform = m_homeBaseEntity.Get<Transform2>();
    float size = BaseStats.GetHarvesterCollectionRangeMultiplier(m_homeBaseEntity.Get<Harvester>());
    var offset = new Vector2(side * TextureCache.HomeBase.Width * 0.12f,
      TextureCache.HomeBase.Height * 0.25f) * transform.Scale * size;
    return transform.Position + IntroShakeOffset
      + Vector2.Transform(offset, Matrix.CreateRotationZ(transform.Rotation + IntroBankAngle));
  }

  private void EmitCrashSparks(int count, bool impact)
  {
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
    IntroBankAngle = MathF.Sin(introAge * 27f) * strength * 0.006f;
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
    introSettleRemaining = 0.45f;
    EmitCrashSparks(40, true);
  }

  private void DrawCrashIntro()
  {
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
