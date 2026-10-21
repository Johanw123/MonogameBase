using System;
using ImGuiNET;
using JapeFramework;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  // ============================================================================================
  // SIGNAL HOVER OUTLINE & GPU PARTICLE TUNING (Runtime-editable via Tab Debug Menu)
  // ============================================================================================

  // 0 = Mouse only, 1 = Left card, 2 = Middle card, 3 = Right card, 4 = All 3 cards
  public static int PinSignalHoverMode = 0;

  // --- Hover Outline Shader Knobs (passed to SignalCardShader.fx) ---
  public static float OutlinePrimaryRimStrength  = 0.70f; // Inner crisp contour line brightness (0 = off, 1 = full)
  public static float OutlineOuterTraceStrength  = 0.00f; // Outer parallel dashed tech-trace brightness (0 = off, 1 = full)
  public static float OutlineSoftGlowStrength    = 3.00f; // Tight neon glow falloff around the contour (0 = razor line only, >1 = stronger glow)
  public static float OutlinePulseRippleStrength = 2.00f; // Expanding contour ripple between inner & outer trace (0 = off)
  public static float OutlineWaveSpeed           = 4.07f; // Speed of the traveling highlight wave around the contour

  // --- GPU Particle Physics & Emission Timing ---
  public static float HoverParticleDrag          = 3.00f;  // Velocity damping per second (closer to 1.0 = glides farther)
  public static float HoverParticleBuoyancyY     = 100f;   // Y acceleration in virtual px/s^2
  public static float HoverStreamInterval        = 0.033f; // Seconds between continuous hover particle steps (smaller = denser stream)

  // --- Perimeter GPU Sparks & Streaks (spawned around the card contour) ---
  public static float PerimeterSpawnOffsetPx     = 13.1f;  // Distance outside the 768x964 card border where perimeter particles spawn
  public static int   PerimeterBurstsPerStep     = 5;      // Number of perimeter spawn points per HoverStreamInterval
  public static int   PerimeterCountPerBurst     = 3;      // Particles emitted at each perimeter spawn point
  public static Vector2 PerimeterSparkSize       = new(5.4f, 9.2f);   // Pinpoint spark size range (virtual px)
  public static Vector2 PerimeterStreakSize      = new(3.8f, 6.6f);   // Velocity-aligned streak size range (virtual px)
  public static Vector2 PerimeterSparkSpeed      = new(55f, 130f);    // Outward speed range for sparks (virtual px/s)
  public static Vector2 PerimeterStreakSpeed     = new(75f, 155f);    // Outward speed range for streaks (virtual px/s)
  public static Vector2 PerimeterSparkLifetime   = new(0.42f, 0.85f); // Lifetime range for sparks (seconds)
  public static Vector2 PerimeterStreakLifetime  = new(0.30f, 0.62f); // Lifetime range for streaks (seconds)

  // --- Central Hex Reactor Socket GPU Sparks & Streaks (inside the upper bay) ---
  public static int   SocketOutwardSparkCount    = 3;      // Outward sparks emitted per step around the center icon
  public static int   SocketOrbitStreakCount     = 3;      // Tangential orbital streaks emitted per step around the center icon
  public static Vector2 SocketSparkSize          = new(4.6f, 8.2f);
  public static Vector2 SocketStreakSize         = new(3.4f, 5.8f);
  public static Vector2 SocketSparkSpeed         = new(42f, 92f);
  public static Vector2 SocketStreakSpeed        = new(58f, 115f);

  private static Vector4 HoverOutlineShaderParams => new(
    OutlinePrimaryRimStrength,
    OutlineOuterTraceStrength,
    OutlineSoftGlowStrength,
    OutlinePulseRippleStrength);

  public static bool IsCardHoverPinned(int index) =>
    PinSignalHoverMode == 4 || PinSignalHoverMode == index + 1;

  public static void ResetSignalHoverTuning()
  {
    OutlinePrimaryRimStrength  = 0.70f;
    OutlineOuterTraceStrength  = 0.00f;
    OutlineSoftGlowStrength    = 3.00f;
    OutlinePulseRippleStrength = 2.00f;
    OutlineWaveSpeed           = 4.07f;
    HoverParticleDrag          = 3.00f;
    HoverParticleBuoyancyY     = 100f;
    HoverStreamInterval        = 0.033f;
    PerimeterSpawnOffsetPx     = 13.1f;
    PerimeterBurstsPerStep     = 5;
    PerimeterCountPerBurst     = 3;
    PerimeterSparkSize         = new(5.4f, 9.2f);
    PerimeterStreakSize        = new(3.8f, 6.6f);
    PerimeterSparkSpeed        = new(55f, 130f);
    PerimeterStreakSpeed       = new(75f, 155f);
    PerimeterSparkLifetime     = new(0.42f, 0.85f);
    PerimeterStreakLifetime    = new(0.30f, 0.62f);
    SocketOutwardSparkCount    = 3;
    SocketOrbitStreakCount     = 3;
    SocketSparkSize            = new(4.6f, 8.2f);
    SocketStreakSize           = new(3.4f, 5.8f);
    SocketSparkSpeed           = new(42f, 92f);
    SocketStreakSpeed          = new(58f, 115f);
  }

  public static void DrawSignalHoverDebug()
  {
    if (!ImGui.CollapsingHeader("Signal card hover & outline tuning")) return;
    ImGui.TextWrapped("Tune the Signal card hover contour outline and GPU particles in real time. Pin hover on a card so it stays active while dragging sliders.");

    ImGui.Combo("Pin hover preview", ref PinSignalHoverMode, "Mouse only\0Left card\0Middle card\0Right card\0All 3 cards\0");
    if (ImGui.Button("Open sample Signal choice (Common / Rare / Legendary)"))
    {
      if (Instance != null && UpgradeManager.Instance != null)
      {
        UpgradeManager.Instance.UGM.SignalsUnlocked = true;
        var sig = UpgradeManager.Instance.Signals;
        sig.PendingChoices.Clear();
        sig.PendingChoices.Add(new SignalChoice { Signal = 0, Rarity = 0 });
        sig.PendingChoices.Add(new SignalChoice { Signal = 2, Rarity = 2 });
        sig.PendingChoices.Add(new SignalChoice { Signal = 4, Rarity = 4 });
        Instance.SetUpgradeType(UpgradeTypes.Signals);
        Instance.signalRevealStarted = BaseGame.Time.TotalGameTime.TotalSeconds - 5.0;
        if (PinSignalHoverMode == 0) PinSignalHoverMode = 4;
      }
    }

    ImGui.SeparatorText("Hover Outline & Glow (SignalCardShader.fx)");
    ImGui.SliderFloat("Inner contour rim", ref OutlinePrimaryRimStrength, 0f, 2.5f, "%.2f");
    ImGui.SliderFloat("Outer dashed tech-trace", ref OutlineOuterTraceStrength, 0f, 2.5f, "%.2f");
    ImGui.SliderFloat("Soft neon edge glow", ref OutlineSoftGlowStrength, 0f, 6.0f, "%.2f");
    ImGui.SliderFloat("Expanding ripple pulse", ref OutlinePulseRippleStrength, 0f, 4.0f, "%.2f");
    ImGui.SliderFloat("Traveling wave speed", ref OutlineWaveSpeed, 0f, 10.0f, "%.2f");

    ImGui.SeparatorText("GPU Particle Physics & Timing");
    ImGui.SliderFloat("Particle drag", ref HoverParticleDrag, 0.10f, 6.0f, "%.2f");
    ImGui.SliderFloat("Upward buoyancy (Y accel)", ref HoverParticleBuoyancyY, -250f, 250f, "%.0f");
    ImGui.SliderFloat("Stream interval (s)", ref HoverStreamInterval, 0.010f, 0.150f, "%.3f s");

    ImGui.SeparatorText("Perimeter GPU Sparks & Streaks");
    ImGui.SliderFloat("Border spawn offset (px)", ref PerimeterSpawnOffsetPx, -8f, 60f, "%.1f");
    ImGui.SliderInt("Spawn points per step", ref PerimeterBurstsPerStep, 0, 10);
    ImGui.SliderInt("Particles per point", ref PerimeterCountPerBurst, 1, 8);
    ImGui.DragFloatRange2("Perimeter spark size", ref PerimeterSparkSize.X, ref PerimeterSparkSize.Y, 0.2f, 1.0f, 30.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Perimeter streak size", ref PerimeterStreakSize.X, ref PerimeterStreakSize.Y, 0.2f, 1.0f, 30.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Perimeter spark speed", ref PerimeterSparkSpeed.X, ref PerimeterSparkSpeed.Y, 1.0f, 5.0f, 350.0f, "Min: %.0f", "Max: %.0f");
    ImGui.DragFloatRange2("Perimeter streak speed", ref PerimeterStreakSpeed.X, ref PerimeterStreakSpeed.Y, 1.0f, 5.0f, 350.0f, "Min: %.0f", "Max: %.0f");
    ImGui.DragFloatRange2("Perimeter spark life (s)", ref PerimeterSparkLifetime.X, ref PerimeterSparkLifetime.Y, 0.02f, 0.05f, 2.5f, "Min: %.2f", "Max: %.2f");
    ImGui.DragFloatRange2("Perimeter streak life (s)", ref PerimeterStreakLifetime.X, ref PerimeterStreakLifetime.Y, 0.02f, 0.05f, 2.5f, "Min: %.2f", "Max: %.2f");

    ImGui.SeparatorText("Center Hex Socket GPU Particles");
    ImGui.SliderInt("Outward socket sparks", ref SocketOutwardSparkCount, 0, 12);
    ImGui.SliderInt("Orbital socket streaks", ref SocketOrbitStreakCount, 0, 12);
    ImGui.DragFloatRange2("Socket spark size", ref SocketSparkSize.X, ref SocketSparkSize.Y, 0.2f, 1.0f, 25.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Socket streak size", ref SocketStreakSize.X, ref SocketStreakSize.Y, 0.2f, 1.0f, 25.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Socket spark speed", ref SocketSparkSpeed.X, ref SocketSparkSpeed.Y, 1.0f, 5.0f, 300.0f, "Min: %.0f", "Max: %.0f");
    ImGui.DragFloatRange2("Socket streak speed", ref SocketStreakSpeed.X, ref SocketStreakSpeed.Y, 1.0f, 5.0f, 300.0f, "Min: %.0f", "Max: %.0f");

    if (ImGui.Button("Reset Signal hover defaults"))
    {
      ResetSignalHoverTuning();
    }
    ImGui.SameLine();
    if (ImGui.Button("Print Signal hover values to console"))
    {
      Console.WriteLine(
        $"OutlinePrimaryRimStrength={OutlinePrimaryRimStrength:F2}f, OutlineOuterTraceStrength={OutlineOuterTraceStrength:F2}f, " +
        $"OutlineSoftGlowStrength={OutlineSoftGlowStrength:F2}f, OutlinePulseRippleStrength={OutlinePulseRippleStrength:F2}f, OutlineWaveSpeed={OutlineWaveSpeed:F2}f, " +
        $"HoverParticleDrag={HoverParticleDrag:F2}f, HoverParticleBuoyancyY={HoverParticleBuoyancyY:F0}f, HoverStreamInterval={HoverStreamInterval:F3}f, " +
        $"PerimeterSpawnOffsetPx={PerimeterSpawnOffsetPx:F1}f, PerimeterBurstsPerStep={PerimeterBurstsPerStep}, PerimeterCountPerBurst={PerimeterCountPerBurst}, " +
        $"PerimeterSparkSize=({PerimeterSparkSize.X:F1}, {PerimeterSparkSize.Y:F1}), PerimeterStreakSize=({PerimeterStreakSize.X:F1}, {PerimeterStreakSize.Y:F1}), " +
        $"PerimeterSparkSpeed=({PerimeterSparkSpeed.X:F0}, {PerimeterSparkSpeed.Y:F0}), PerimeterStreakSpeed=({PerimeterStreakSpeed.X:F0}, {PerimeterStreakSpeed.Y:F0}), " +
        $"SocketOutwardSparkCount={SocketOutwardSparkCount}, SocketOrbitStreakCount={SocketOrbitStreakCount}, " +
        $"SocketSparkSize=({SocketSparkSize.X:F1}, {SocketSparkSize.Y:F1}), SocketStreakSize=({SocketStreakSize.X:F1}, {SocketStreakSize.Y:F1})");
    }
  }

  // Local coordinates relative to SignalChoiceIconCenter(card) let reveals and hover emitters
  // follow their card seamlessly when lifting on hover, docking, or resizing.
  private readonly GpuParticleSystem[] signalParticlePools = new GpuParticleSystem[3];
  private readonly float[] signalHoverEmitClocks = new float[3];
  private readonly int[] signalHoverCornerStep = new int[3];
  private int signalParticleRevealedMask;
  private int signalParticleHoveredMask;

  private void ClearSignalParticles()
  {
    signalParticleRevealedMask = 0;
    signalParticleHoveredMask = 0;
    Array.Clear(signalHoverEmitClocks);
    foreach (var pool in signalParticlePools) pool?.Clear();
  }

  private GpuParticleSystem GetSignalParticlePool(int index)
  {
#if KNI_WEB
    return null;
#else
    if (EffectCache.GpuParticlesFx?.IsLoaded != true || EffectCache.GpuParticlesFx.IsFailed) return null;
    var pool = signalParticlePools[index] ??= new GpuParticleSystem(GameMain.Instance.GraphicsDevice,
      EffectCache.GpuParticlesFx.Value, 64);
    pool.Drag = HoverParticleDrag;
    pool.Acceleration = new Vector2(0f, HoverParticleBuoyancyY);
    return pool;
#endif
  }

  internal void UpdateSignalParticles(float dt)
  {
    if (!signalChoicesVisible || !drawUpgradesGui || m_upgradeWindowType != UpgradeTypes.Signals)
    {
      signalParticleHoveredMask = 0;
      Array.Clear(signalHoverEmitClocks);
      foreach (var pool in signalParticlePools) pool?.Clear();
      return;
    }

    float time = (float)BaseGame.Time.TotalGameTime.TotalSeconds;
    for (int i = 0; i < 3; i++)
    {
      int rarity = Signals.PendingChoices[i].Rarity;
      float elapsed = SignalRevealAge(i) - 0.3f;
      if (elapsed >= 0 && (signalParticleRevealedMask & (1 << i)) == 0)
      {
        signalParticleRevealedMask |= 1 << i;
        // Opening an already revealed choice never replays the discharge.
        if (rarity > 0 && elapsed < 0.2f) EmitSignalParticles(i, rarity);
      }

      float hover = signalHoverAmounts[i];
      bool isHovered = hover > 0.12f && SignalRevealAge(i) >= 0.45f;
      if (isHovered)
      {
        if ((signalParticleHoveredMask & (1 << i)) == 0)
        {
          signalParticleHoveredMask |= 1 << i;
          signalHoverEmitClocks[i] = 0f;
          EmitSignalHoverIgnition(i, rarity);
        }

        signalHoverEmitClocks[i] += dt;
        for (int step = 0; step < 3 && signalHoverEmitClocks[i] >= HoverStreamInterval; step++)
        {
          signalHoverEmitClocks[i] -= HoverStreamInterval;
          EmitSignalHoverStream(i, rarity, time);
        }
        signalHoverEmitClocks[i] = Math.Min(signalHoverEmitClocks[i], HoverStreamInterval);
      }
      else
      {
        signalParticleHoveredMask &= ~(1 << i);
        signalHoverEmitClocks[i] = 0f;
      }

      signalParticlePools[i]?.Update(dt);
    }
  }

  private void EmitSignalParticles(int index, int rarity)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null) return;
    var card = SignalCard(index);
    float radius = Math.Min(card.Width * 0.32f, card.Height * 0.22f);
    var color = SignalRarity(rarity).Color;
    float duration = 0.7f + rarity * 0.3f;
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 48 + rarity * 52, Color.Lerp(color, Color.White, 0.55f))
    {
      Motion = GpuParticleMotion.Outward, Radius = radius * 0.4f, InnerRadius = radius * 0.32f,
      Speed = new Vector2(0.55f, 1.35f) * radius, Spread = 0.18f,
      Lifetime = new(duration * 0.40f, duration * 0.85f), Size = new(3.5f, 6.5f), Streak = true
    });
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 24 + rarity * 24, color)
    {
      Motion = GpuParticleMotion.Outward, Radius = radius * 0.45f, InnerRadius = radius * 0.3f,
      Speed = new Vector2(0.25f, 0.65f) * radius, Spread = 0.5f,
      Lifetime = new(duration * 0.45f, duration * 0.9f), Size = new(5.5f, 10.5f)
    });
    if (rarity >= 3)
      pool.Emit(new GpuParticleBurst(Vector2.Zero, rarity * 28, Color.Lerp(color, Color.White, 0.35f))
      {
        Motion = GpuParticleMotion.Tangential, Radius = radius * 0.5f, InnerRadius = radius * 0.48f,
        Direction = 0, Spread = 0.15f, Speed = new Vector2(0.30f, 0.65f) * radius,
        Lifetime = new(0.35f, duration * 0.75f), Size = new(3.2f, 6.0f), Streak = true
      });
#endif
  }

  private void EmitSignalHoverIgnition(int index, int rarity)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null) return;
    var baseColor = SignalRarity(rarity).Color;
    if (rarity == 0) baseColor = Color.Lerp(baseColor, new Color(95, 220, 255), 0.55f);
    var hotColor = Color.Lerp(baseColor, Color.White, 0.72f);
    var goldTint = Color.Lerp(baseColor, new Color(255, 232, 145), 0.48f);

    // Crisp outward spark burst + tangential vortex ring around the hex reactor socket
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 48, hotColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = 90f, InnerRadius = 80f,
      Speed = new(65f, 145f), Spread = 0.25f,
      Lifetime = new(0.28f, 0.58f), Size = PerimeterStreakSize, Streak = true
    });
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 32, baseColor)
    {
      Motion = GpuParticleMotion.Tangential, Radius = 88f, InnerRadius = 82f,
      Speed = new(55f, 115f), Spread = 0.20f,
      Lifetime = new(0.32f, 0.68f), Size = SocketSparkSize
    });

    // Perimeter ignition burst around the card contour
    for (int k = 0; k < 10; k++)
    {
      float qAngle = (k * 0.1f + 0.037f) % 1f;
      SampleClamshellPerimeter(qAngle, out var edgePos, out float edgeOutAngle);
      var outDir = new Vector2(MathF.Cos(edgeOutAngle), MathF.Sin(edgeOutAngle) - 0.30f);
      float emitAngle = MathF.Atan2(outDir.Y, outDir.X);
      bool isStreak = (k & 1) == 0;
      pool.Emit(new GpuParticleBurst(edgePos, 4, isStreak ? hotColor : goldTint)
      {
        Direction = emitAngle, Spread = 0.42f,
        Speed = isStreak ? PerimeterStreakSpeed : PerimeterSparkSpeed,
        Lifetime = new(0.32f, 0.72f),
        Size = isStreak ? PerimeterStreakSize : PerimeterSparkSize,
        Radius = 16f, Streak = isStreak
      });
    }
#endif
  }

  private static void SampleClamshellPerimeter(float normAngle, out Vector2 pos, out float outAngle)
  {
    const float CenterY = 238f, Chamfer = 30f;
    float hx = 384f + PerimeterSpawnOffsetPx;
    float hy = 482f + PerimeterSpawnOffsetPx;
    float angle = (normAngle - 0.5f) * MathF.Tau;
    float dx = MathF.Cos(angle);
    float dy = MathF.Sin(angle);
    float tBox = Math.Min(hx / Math.Max(MathF.Abs(dx), 0.0001f), hy / Math.Max(MathF.Abs(dy), 0.0001f));
    float tChamfer = (hx + hy - Chamfer) / Math.Max(MathF.Abs(dx) + MathF.Abs(dy), 0.0001f);
    float t = Math.Min(tBox, tChamfer);
    pos = new Vector2(dx * t, CenterY + dy * t);
    outAngle = angle;
  }

  private void EmitSignalHoverStream(int index, int rarity, float time)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null || pool.PendingBursts >= GpuParticleSystem.MaxPendingBursts - 8) return;

    var baseColor = SignalRarity(rarity).Color;
    if (rarity == 0) baseColor = Color.Lerp(baseColor, new Color(95, 220, 255), 0.55f);
    var hotColor = Color.Lerp(baseColor, Color.White, 0.74f);
    var goldTint = Color.Lerp(baseColor, new Color(255, 232, 145), 0.48f);

    // 1. Well-separated quasirandom perimeter sparks & streaks that travel cleanly outward & upward into space
    for (int k = 0; k < PerimeterBurstsPerStep; k++)
    {
      int step = signalHoverCornerStep[index] = (signalHoverCornerStep[index] + 1) % 1024;
      float qAngle = (step * 0.6180339887f) % 1f;
      SampleClamshellPerimeter(qAngle, out var edgePos, out float edgeOutAngle);

      // Tilt direction slightly upward so side sparks arc gracefully away from the card
      var outDir = new Vector2(MathF.Cos(edgeOutAngle), MathF.Sin(edgeOutAngle) - 0.35f);
      float emitAngle = MathF.Atan2(outDir.Y, outDir.X);
      bool isStreak = k >= (PerimeterBurstsPerStep + 1) / 2;

      pool.Emit(new GpuParticleBurst(edgePos, PerimeterCountPerBurst, isStreak ? baseColor : ((step & 1) == 0 ? hotColor : goldTint))
      {
        Direction = emitAngle, Spread = isStreak ? 0.36f : 0.48f,
        Speed = isStreak ? PerimeterStreakSpeed : PerimeterSparkSpeed,
        Lifetime = isStreak ? PerimeterStreakLifetime : PerimeterSparkLifetime,
        Size = isStreak ? PerimeterStreakSize : PerimeterSparkSize,
        Radius = 14f, Streak = isStreak
      });
    }

    // 2. Central hex reactor socket outward/tangential energy sparks inside upperBay
    if (SocketOutwardSparkCount > 0)
    {
      pool.Emit(new GpuParticleBurst(Vector2.Zero, SocketOutwardSparkCount, hotColor)
      {
        Motion = GpuParticleMotion.Outward, Radius = 86f, InnerRadius = 76f,
        Speed = SocketSparkSpeed, Spread = 0.45f,
        Lifetime = new(0.32f, 0.68f), Size = SocketSparkSize
      });
    }
    if (SocketOrbitStreakCount > 0)
    {
      pool.Emit(new GpuParticleBurst(Vector2.Zero, SocketOrbitStreakCount, goldTint)
      {
        Motion = GpuParticleMotion.Tangential, Radius = 84f, InnerRadius = 78f,
        Speed = SocketStreakSpeed, Spread = 0.25f,
        Lifetime = new(0.28f, 0.56f), Size = SocketStreakSize, Streak = true
      });
    }
#endif
  }

  internal void SimulateSignalParticles()
  {
    foreach (var pool in signalParticlePools) pool?.Simulate();
  }

  private void DisposeSignalParticles()
  {
    foreach (var pool in signalParticlePools) pool?.Dispose();
    Array.Clear(signalParticlePools);
  }
}
