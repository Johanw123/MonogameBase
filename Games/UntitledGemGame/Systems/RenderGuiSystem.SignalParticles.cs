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

  // --- Card Selection Dissolve & GPU Explosion Tuning (Runtime-editable via Tab Debug Menu) ---
  public static float SelectSpeedMultiplier      = 1.50f;  // Overall playback speed (0.1x slow-mo .. 3.0x fast)
  public static float SelectAnimDurationBase     = 1.00f;  // Base selection dissolve duration (seconds)
  public static float SelectRarityDurationStep   = 0.00f;  // Extra duration added per rarity tier (0..4)
  public static float SelectUnchosenDuration     = 0.38f;  // Fade/dissolve duration for the 2 unchosen cards
  public static float SelectCardLiftPx           = 14.0f;  // Upward surge (virtual px) when clicking a card

  // Shader Dissolve & Shockwave Shape
  public static int   SelectDissolveDirection    = 0;      // 0 = Middle -> Outward, 1 = Edges -> Inward
  public static float SelectFocalY               = 0.50f;  // Vertical origin of the dissolve on the card (0..1)
  public static float SelectBurnWidthScale       = 3.00f;  // Width multiplier for the glowing plasma burn frontier
  public static float SelectNoiseWeight          = 0.10f;  // How jagged/crystalline the dissolve edge is (0..0.85)
  public static float SelectFlashIntensity       = 2.50f;  // Initial overcharge flash & shockwave intensity on click
  public static float SelectShockwaveScale       = 1.00f;  // Scale & opacity multiplier for the expanding shockwave rings

  // GPU Explosion Particles
  public static float SelectParticleMultiplier   = 1.43f;  // Overall GPU particle count multiplier on card selection
  public static float SelectParticleDrag         = 2.97f;  // Lower drag during explosion so streaks/sparks blast outward
  public static float SelectParticleBuoyancyY    = -14f;   // Y acceleration (virtual px/s^2) during explosion
  public static float SelectWaveInterval         = 0.049f; // Seconds between particle steps along the expanding burn wave
  public static Vector2 SelectCoreStreakSpeed    = new(165f, 440f);
  public static Vector2 SelectCoreSparkSpeed     = new(115f, 320f);
  public static Vector2 SelectCoreStreakSize     = new(4.6f, 8.8f);
  public static Vector2 SelectCoreSparkSize      = new(6.0f, 11.6f);
  public static Vector2 SelectCoreStreakLifetime = new(0.38f, 0.88f);
  public static Vector2 SelectCoreSparkLifetime  = new(0.46f, 1.02f);

  // Live Debug Preview Controls (Auto-loop & Manual Scrub)
  public static bool  SelectDebugAutoLoop        = false;
  public static int   SelectDebugLoopCardIndex   = 2;      // 0 = Left (Common), 1 = Mid (Rare), 2 = Right (Legendary)
  public static float SelectDebugLoopPause       = 0.45f;  // Pause between auto-loop cycles (seconds)
  public static bool  SelectDebugManualScrub     = false;
  public static float SelectDebugScrubProgress   = 0.35f;  // Manual scrub dissolve progress (0..1)

  private static Vector4 HoverOutlineShaderParams => new(
    OutlinePrimaryRimStrength,
    OutlineOuterTraceStrength,
    OutlineSoftGlowStrength,
    OutlinePulseRippleStrength);

  private static Vector4 DissolveShapeShaderParams => new(
    SelectFocalY,
    SelectNoiseWeight,
    SelectDissolveDirection == 0 ? 1f : 0f,
    0f);

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

  public static void ResetSignalSelectionTuning()
  {
    SelectSpeedMultiplier      = 1.50f;
    SelectAnimDurationBase     = 1.00f;
    SelectRarityDurationStep   = 0.00f;
    SelectUnchosenDuration     = 0.38f;
    SelectCardLiftPx           = 14.0f;
    SelectDissolveDirection    = 0;
    SelectFocalY               = 0.50f;
    SelectBurnWidthScale       = 3.00f;
    SelectNoiseWeight          = 0.10f;
    SelectFlashIntensity       = 2.50f;
    SelectShockwaveScale       = 1.00f;
    SelectParticleMultiplier   = 1.43f;
    SelectParticleDrag         = 2.97f;
    SelectParticleBuoyancyY    = -14f;
    SelectWaveInterval         = 0.049f;
    SelectCoreStreakSpeed      = new(165f, 440f);
    SelectCoreSparkSpeed       = new(115f, 320f);
    SelectCoreStreakSize       = new(4.6f, 8.8f);
    SelectCoreSparkSize        = new(6.0f, 11.6f);
    SelectCoreStreakLifetime   = new(0.38f, 0.88f);
    SelectCoreSparkLifetime    = new(0.46f, 1.02f);
  }

  private static void PopulateDebugSampleChoices()
  {
    if (Instance == null || UpgradeManager.Instance == null) return;
    UpgradeManager.Instance.UGM.SignalsUnlocked = true;
    var sig = UpgradeManager.Instance.Signals;
    sig.PendingChoices.Clear();
    sig.PendingChoices.Add(new SignalChoice { Signal = 0, Rarity = 0 });
    sig.PendingChoices.Add(new SignalChoice { Signal = 2, Rarity = 2 });
    sig.PendingChoices.Add(new SignalChoice { Signal = 4, Rarity = 4 });
    Instance.SetUpgradeType(UpgradeTypes.Signals);
    Instance.signalRevealStarted = BaseGame.Time.TotalGameTime.TotalSeconds - 5.0;
    Instance.signalSelectIndex = -1;
  }

  public static void TriggerDebugSelectPreview(int cardIndex)
  {
    if (Instance == null || UpgradeManager.Instance == null) return;
    SelectDebugManualScrub = false;
    PopulateDebugSampleChoices();
    Instance.BeginSignalSelectionAnimation(Math.Clamp(cardIndex, 0, 2), debugPreview: true);
  }

  public static void DrawSignalHoverDebug()
  {
    if (!ImGui.CollapsingHeader("Signal card hover & outline tuning")) return;
    ImGui.TextWrapped("Tune the Signal card hover contour outline and continuous hover GPU particles in real time.");

    ImGui.Combo("Pin hover preview", ref PinSignalHoverMode, "Mouse only\0Left card\0Middle card\0Right card\0All 3 cards\0");
    if (ImGui.Button("Open sample Signal choice (Common / Rare / Legendary)"))
    {
      PopulateDebugSampleChoices();
      if (PinSignalHoverMode == 0) PinSignalHoverMode = 4;
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

  public static void DrawSignalSelectionDebug()
  {
    if (!ImGui.CollapsingHeader("Signal card selection dissolve & explosion")) return;
    ImGui.TextWrapped("Trigger, loop, scrub, or slow-motion the Signal card selection dissolve and GPU particle explosion.");

    if (ImGui.Button("Select Left (Common)")) TriggerDebugSelectPreview(0);
    ImGui.SameLine();
    if (ImGui.Button("Select Mid (Rare)")) TriggerDebugSelectPreview(1);
    ImGui.SameLine();
    if (ImGui.Button("Select Right (Legendary)")) TriggerDebugSelectPreview(2);

    ImGui.Checkbox("Auto-loop selection preview", ref SelectDebugAutoLoop);
    if (SelectDebugAutoLoop)
    {
      SelectDebugManualScrub = false;
      ImGui.Combo("Auto-loop card", ref SelectDebugLoopCardIndex, "Left (Common)\0Middle (Rare)\0Right (Legendary)\0");
      ImGui.SliderFloat("Pause between loops (s)", ref SelectDebugLoopPause, 0.05f, 2.00f, "%.2f s");
    }

    if (ImGui.Checkbox("Manual scrub dissolve (freeze frame)", ref SelectDebugManualScrub) && SelectDebugManualScrub)
    {
      SelectDebugAutoLoop = false;
      if (Instance != null && !Instance.IsSelectingSignal)
        TriggerDebugSelectPreview(SelectDebugLoopCardIndex);
      SelectDebugManualScrub = true;
    }
    if (SelectDebugManualScrub)
    {
      ImGui.Combo("Scrub target card", ref SelectDebugLoopCardIndex, "Left (Common)\0Middle (Rare)\0Right (Legendary)\0");
      ImGui.SliderFloat("Scrub dissolve progress", ref SelectDebugScrubProgress, 0f, 1f, "%.3f");
    }

    ImGui.SeparatorText("Animation Speed & Timing");
    ImGui.SliderFloat("Animation speed mult", ref SelectSpeedMultiplier, 0.10f, 3.00f, "%.2fx");
    ImGui.SliderFloat("Base dissolve duration (s)", ref SelectAnimDurationBase, 0.30f, 2.50f, "%.2f s");
    ImGui.SliderFloat("Extra duration per rarity (s)", ref SelectRarityDurationStep, 0.00f, 0.40f, "%.2f s");
    ImGui.SliderFloat("Unchosen fade duration (s)", ref SelectUnchosenDuration, 0.10f, 1.50f, "%.2f s");
    ImGui.SliderFloat("Click upward surge (px)", ref SelectCardLiftPx, -20f, 50f, "%.1f px");

    ImGui.SeparatorText("Shader Dissolve & Shockwave (SignalCardShader.fx)");
    ImGui.Combo("Dissolve direction", ref SelectDissolveDirection, "Middle -> Outward\0Edges -> Inward\0");
    ImGui.SliderFloat("Dissolve origin Y (0..1)", ref SelectFocalY, 0.10f, 0.85f, "%.2f");
    ImGui.SliderFloat("Plasma burn edge width", ref SelectBurnWidthScale, 0.15f, 3.00f, "%.2f");
    ImGui.SliderFloat("Crystalline noise weight", ref SelectNoiseWeight, 0.00f, 0.85f, "%.2f");
    ImGui.SliderFloat("Click flash intensity", ref SelectFlashIntensity, 0.00f, 2.50f, "%.2f");
    ImGui.SliderFloat("Shockwave ring scale", ref SelectShockwaveScale, 0.00f, 2.50f, "%.2f");

    ImGui.SeparatorText("GPU Explosion Particles");
    ImGui.SliderFloat("Explosion particle mult", ref SelectParticleMultiplier, 0.00f, 3.00f, "%.2f");
    ImGui.SliderFloat("Explosion particle drag", ref SelectParticleDrag, 0.20f, 6.00f, "%.2f");
    ImGui.SliderFloat("Explosion buoyancy (Y accel)", ref SelectParticleBuoyancyY, -300f, 300f, "%.0f");
    ImGui.SliderFloat("Burn wave emit interval (s)", ref SelectWaveInterval, 0.008f, 0.100f, "%.3f s");
    ImGui.DragFloatRange2("Explosion streak speed", ref SelectCoreStreakSpeed.X, ref SelectCoreStreakSpeed.Y, 2.0f, 20.0f, 950.0f, "Min: %.0f", "Max: %.0f");
    ImGui.DragFloatRange2("Explosion spark speed", ref SelectCoreSparkSpeed.X, ref SelectCoreSparkSpeed.Y, 2.0f, 20.0f, 750.0f, "Min: %.0f", "Max: %.0f");
    ImGui.DragFloatRange2("Explosion streak size", ref SelectCoreStreakSize.X, ref SelectCoreStreakSize.Y, 0.2f, 1.0f, 30.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Explosion spark size", ref SelectCoreSparkSize.X, ref SelectCoreSparkSize.Y, 0.2f, 1.0f, 30.0f, "Min: %.1f", "Max: %.1f");
    ImGui.DragFloatRange2("Explosion streak life (s)", ref SelectCoreStreakLifetime.X, ref SelectCoreStreakLifetime.Y, 0.02f, 0.05f, 3.0f, "Min: %.2f", "Max: %.2f");
    ImGui.DragFloatRange2("Explosion spark life (s)", ref SelectCoreSparkLifetime.X, ref SelectCoreSparkLifetime.Y, 0.02f, 0.05f, 3.0f, "Min: %.2f", "Max: %.2f");

    if (ImGui.Button("Reset Signal selection defaults"))
    {
      ResetSignalSelectionTuning();
    }
    ImGui.SameLine();
    if (ImGui.Button("Print Signal selection values to console"))
    {
      Console.WriteLine(
        $"SelectSpeedMultiplier={SelectSpeedMultiplier:F2}f, SelectAnimDurationBase={SelectAnimDurationBase:F2}f, " +
        $"SelectRarityDurationStep={SelectRarityDurationStep:F2}f, SelectUnchosenDuration={SelectUnchosenDuration:F2}f, SelectCardLiftPx={SelectCardLiftPx:F1}f, " +
        $"SelectDissolveDirection={SelectDissolveDirection}, SelectFocalY={SelectFocalY:F2}f, SelectBurnWidthScale={SelectBurnWidthScale:F2}f, " +
        $"SelectNoiseWeight={SelectNoiseWeight:F2}f, SelectFlashIntensity={SelectFlashIntensity:F2}f, SelectShockwaveScale={SelectShockwaveScale:F2}f, " +
        $"SelectParticleMultiplier={SelectParticleMultiplier:F2}f, SelectParticleDrag={SelectParticleDrag:F2}f, SelectParticleBuoyancyY={SelectParticleBuoyancyY:F0}f, " +
        $"SelectWaveInterval={SelectWaveInterval:F3}f, SelectCoreStreakSpeed=({SelectCoreStreakSpeed.X:F0}, {SelectCoreStreakSpeed.Y:F0}), " +
        $"SelectCoreSparkSpeed=({SelectCoreSparkSpeed.X:F0}, {SelectCoreSparkSpeed.Y:F0}), SelectCoreStreakSize=({SelectCoreStreakSize.X:F1}, {SelectCoreStreakSize.Y:F1}), " +
        $"SelectCoreSparkSize=({SelectCoreSparkSize.X:F1}, {SelectCoreSparkSize.Y:F1}), SelectCoreStreakLifetime=({SelectCoreStreakLifetime.X:F2}, {SelectCoreStreakLifetime.Y:F2}), " +
        $"SelectCoreSparkLifetime=({SelectCoreSparkLifetime.X:F2}, {SelectCoreSparkLifetime.Y:F2})");
    }
  }

  // Local coordinates relative to SignalChoiceIconCenter(card) let reveals and hover emitters
  // follow their card seamlessly when lifting on hover, docking, or resizing.
  private readonly GpuParticleSystem[] signalParticlePools = new GpuParticleSystem[3];
  private readonly float[] signalHoverEmitClocks = new float[3];
  private readonly int[] signalHoverCornerStep = new int[3];
  private int signalParticleRevealedMask;
  private int signalParticleHoveredMask;

  // Selection dissolve & particle explosion state
  private int signalSelectIndex = -1;
  private float signalSelectElapsed;
  private float signalSelectLoopCooldown;
  private bool signalSelectCoreBurstFired;
  private float signalSelectWaveClock;
  private bool signalSelectIsDebugPreview;
  private readonly SignalChoice[] signalSelectChoices = new SignalChoice[3];
  private readonly long[] signalSelectStackCounts = new long[3];
  private readonly double[] signalSelectBonusPercents = new double[3];
  private bool IsSelectingSignal => signalSelectIndex >= 0;

  private static float SignalSelectDuration(int rarity)
    => SelectAnimDurationBase + Math.Clamp(rarity, 0, 4) * SelectRarityDurationStep;

  private float SignalSelectElapsed
  {
    get
    {
      if (signalSelectIndex < 0) return 0f;
      if (SelectDebugManualScrub)
      {
        int r = signalSelectChoices[signalSelectIndex].Rarity;
        return SelectDebugScrubProgress * SignalSelectDuration(r);
      }
      return signalSelectElapsed;
    }
  }

  internal void BeginSignalSelectionAnimation(int index, bool debugPreview = false)
  {
    if (index < 0 || index >= 3) return;
    if (Signals.PendingChoices.Count == 3)
    {
      for (int k = 0; k < 3; k++)
      {
        var ch = Signals.PendingChoices[k];
        signalSelectChoices[k] = ch;
        signalSelectStackCounts[k] = Signals.StackCount(ch.Signal);
        signalSelectBonusPercents[k] = Signals.BonusPercent(ch.Signal);
      }
    }
    if (!debugPreview)
    {
      if (!UntitledGemGameGameScreen.Instance.TryChooseSignal(index))
        return;
    }
    else
    {
      AudioManager.Instance?.PlaySound(SoundEvent.SignalChoose);
      Signals.PendingChoices.Clear();
    }

    signalSelectIndex = index;
    signalSelectElapsed = 0f;
    signalSelectCoreBurstFired = false;
    signalSelectWaveClock = 0f;
    signalSelectIsDebugPreview = debugPreview;
    signalHoverAmounts[index] = 1f;

    int chosenRarity = signalSelectChoices[index].Rarity;
    EmitSignalSelectIgnition(index, chosenRarity);
  }

  private void ClearSignalParticles()
  {
    signalParticleRevealedMask = 0;
    signalParticleHoveredMask = 0;
    signalSelectIndex = -1;
    signalSelectElapsed = 0f;
    signalSelectIsDebugPreview = false;
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
      EffectCache.GpuParticlesFx.Value, 96);
    pool.Drag = signalSelectIndex == index ? SelectParticleDrag : HoverParticleDrag;
    pool.Acceleration = new Vector2(0f, signalSelectIndex == index ? SelectParticleBuoyancyY : HoverParticleBuoyancyY);
    return pool;
#endif
  }

  internal void UpdateSignalParticles(float dt)
  {
    if (!drawUpgradesGui || m_upgradeWindowType != UpgradeTypes.Signals)
    {
      ClearSignalParticles();
      return;
    }

    if (SelectDebugManualScrub)
    {
      int targetCard = Math.Clamp(SelectDebugLoopCardIndex, 0, 2);
      if (!IsSelectingSignal || signalSelectIndex != targetCard)
      {
        PopulateDebugSampleChoices();
        BeginSignalSelectionAnimation(targetCard, debugPreview: true);
        SelectDebugManualScrub = true;
      }
      for (int i = 0; i < 3; i++) signalParticlePools[i]?.Update(dt);
      return;
    }

    if (IsSelectingSignal)
    {
      int selIdx = signalSelectIndex;
      int selRarity = signalSelectChoices[selIdx].Rarity;
      float duration = SignalSelectDuration(selRarity);
      float scaledDt = dt * Math.Max(0.05f, SelectSpeedMultiplier);
      signalSelectElapsed += scaledDt;
      float elapsed = signalSelectElapsed;

      if (elapsed >= duration)
      {
        bool wasDebug = signalSelectIsDebugPreview;
        signalSelectIndex = -1;
        signalSelectElapsed = 0f;
        signalSelectIsDebugPreview = false;
        signalSelectLoopCooldown = SelectDebugLoopPause;
        if ((wasDebug || SelectDebugAutoLoop) && Signals.PendingChoices.Count == 0)
        {
          PopulateDebugSampleChoices();
        }
      }
      else
      {
        float dissolveProg = Math.Clamp((elapsed - 0.08f) / Math.Max(0.15f, duration - 0.16f), 0f, 1f);
        signalSelectWaveClock += scaledDt;
        float waveStep = Math.Max(0.008f, SelectWaveInterval);
        for (int s = 0; s < 3 && signalSelectWaveClock >= waveStep; s++)
        {
          signalSelectWaveClock -= waveStep;
          if (dissolveProg >= 0.02f && dissolveProg <= 0.88f)
            EmitSignalSelectDissolveWave(selIdx, selRarity, dissolveProg);
        }

        float finaleThreshold = SelectDissolveDirection == 0 ? 0.68f : 0.54f;
        if (!signalSelectCoreBurstFired && dissolveProg >= finaleThreshold)
        {
          signalSelectCoreBurstFired = true;
          EmitSignalSelectCoreFinale(selIdx, selRarity);
        }

        for (int i = 0; i < 3; i++)
        {
          if (signalParticlePools[i] != null)
          {
            signalParticlePools[i].Drag = i == selIdx ? SelectParticleDrag : HoverParticleDrag;
            signalParticlePools[i].Acceleration = new Vector2(0f, i == selIdx ? SelectParticleBuoyancyY : HoverParticleBuoyancyY);
            signalParticlePools[i].Update(scaledDt);
          }
        }
        return;
      }
    }

    if (SelectDebugAutoLoop)
    {
      signalSelectLoopCooldown -= dt;
      if (signalSelectLoopCooldown <= 0f)
      {
        TriggerDebugSelectPreview(SelectDebugLoopCardIndex);
        return;
      }
    }

    if (!signalChoicesVisible)
    {
      signalParticleHoveredMask = 0;
      Array.Clear(signalHoverEmitClocks);
      // Let any remaining explosion embers finish drifting and fading naturally on screen
      foreach (var pool in signalParticlePools) pool?.Update(dt);
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

      if (signalParticlePools[i] != null)
      {
        signalParticlePools[i].Drag = HoverParticleDrag;
        signalParticlePools[i].Acceleration = new Vector2(0f, HoverParticleBuoyancyY);
        signalParticlePools[i].Update(dt);
      }
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

  private void EmitSignalSelectIgnition(int index, int rarity)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null) return;
    pool.Drag = SelectParticleDrag;

    var baseColor = SignalRarity(rarity).Color;
    if (rarity == 0) baseColor = Color.Lerp(baseColor, new Color(95, 220, 255), 0.58f);
    var hotColor = Color.Lerp(baseColor, Color.White, 0.84f);
    var goldTint = Color.Lerp(baseColor, new Color(255, 232, 145), 0.58f);

    float mult = Math.Clamp(SelectParticleMultiplier, 0f, 5f);
    if (mult <= 0.01f) return;
    float rarityBoost = 1.05f + rarity * 0.24f;
    float focalOffsetY = (SelectFocalY - 0.253f) * 964f;
    var middleOrigin = new Vector2(0f, focalOffsetY);

    // Immediate supernova detonation erupting from the focal origin
    int coreStreaks = Math.Max(4, (int)((92 + rarity * 48) * mult));
    int coreSparks = Math.Max(4, (int)((76 + rarity * 40) * mult));
    pool.Emit(new GpuParticleBurst(middleOrigin, coreStreaks, hotColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = 68f, InnerRadius = 12f,
      Speed = SelectCoreStreakSpeed * rarityBoost, Spread = 0.26f,
      Lifetime = SelectCoreStreakLifetime * (1f + rarity * 0.06f), Size = SelectCoreStreakSize, Streak = true
    });
    pool.Emit(new GpuParticleBurst(middleOrigin, coreSparks, goldTint)
    {
      Motion = GpuParticleMotion.Outward, Radius = 78f, InnerRadius = 18f,
      Speed = SelectCoreSparkSpeed * rarityBoost, Spread = 0.55f,
      Lifetime = SelectCoreSparkLifetime * (1f + rarity * 0.08f), Size = SelectCoreSparkSize
    });
    pool.Emit(new GpuParticleBurst(Vector2.Zero, Math.Max(4, (int)((52 + rarity * 28) * mult)), baseColor)
    {
      Motion = GpuParticleMotion.Outward, Radius = 88f, InnerRadius = 32f,
      Speed = SelectCoreStreakSpeed * (0.90f + rarity * 0.18f), Spread = 0.32f,
      Lifetime = SelectCoreStreakLifetime, Size = SelectCoreStreakSize, Streak = true
    });
    pool.Emit(new GpuParticleBurst(middleOrigin, Math.Max(4, (int)((44 + rarity * 24) * mult)), hotColor)
    {
      Motion = GpuParticleMotion.Tangential, Radius = 92f, InnerRadius = 74f,
      Speed = new Vector2(115f, 250f) * rarityBoost, Spread = 0.20f,
      Lifetime = SelectCoreStreakLifetime * 0.90f, Size = SelectCoreStreakSize, Streak = true
    });
#endif
  }

  private void EmitSignalSelectDissolveWave(int index, int rarity, float dissolveProgress)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null || pool.PendingBursts >= GpuParticleSystem.MaxPendingBursts - 10) return;

    float mult = Math.Clamp(SelectParticleMultiplier, 0f, 5f);
    if (mult <= 0.01f) return;

    var baseColor = SignalRarity(rarity).Color;
    if (rarity == 0) baseColor = Color.Lerp(baseColor, new Color(95, 220, 255), 0.58f);
    var hotColor = Color.Lerp(baseColor, Color.White, 0.82f);
    var goldTint = Color.Lerp(baseColor, new Color(255, 232, 145), 0.56f);

    // Follow the active plasma burn frontier (outward from middle when SelectDissolveDirection == 0, or inward from perimeter)
    float contourScale = SelectDissolveDirection == 0
      ? MathHelper.Lerp(0.14f, 1.05f, MathF.Pow(dissolveProgress, 0.88f))
      : MathHelper.Lerp(1.02f, 0.10f, MathF.Pow(dissolveProgress, 0.92f));
    var middleOrigin = new Vector2(0f, (SelectFocalY - 0.253f) * 964f);
    int countPerPoint = Math.Max(1, (int)MathF.Round((3 + rarity) * mult));
    float speedScale = 0.72f + rarity * 0.08f + dissolveProgress * 0.25f;

    for (int k = 0; k < 6; k++)
    {
      int step = signalHoverCornerStep[index] = (signalHoverCornerStep[index] + 1) % 1024;
      float qAngle = (step * 0.6180339887f) % 1f;
      SampleClamshellPerimeter(qAngle, out var edgePos, out float edgeOutAngle);
      var burnPos = Vector2.Lerp(middleOrigin, edgePos, contourScale);

      var outDir = new Vector2(MathF.Cos(edgeOutAngle), MathF.Sin(edgeOutAngle) - 0.34f);
      float emitAngle = MathF.Atan2(outDir.Y, outDir.X);
      bool isStreak = (k & 1) == 0;
      var tint = isStreak ? ((step & 1) == 0 ? hotColor : baseColor) : goldTint;

      pool.Emit(new GpuParticleBurst(burnPos, countPerPoint, tint)
      {
        Direction = emitAngle, Spread = isStreak ? 0.44f : 0.68f,
        Speed = (isStreak ? SelectCoreStreakSpeed : SelectCoreSparkSpeed) * speedScale,
        Lifetime = (isStreak ? SelectCoreStreakLifetime : SelectCoreSparkLifetime) * 0.85f,
        Size = isStreak ? SelectCoreStreakSize : SelectCoreSparkSize,
        Radius = 24f, Streak = isStreak
      });
    }
#endif
  }

  private void EmitSignalSelectCoreFinale(int index, int rarity)
  {
#if !KNI_WEB
    var pool = GetSignalParticlePool(index);
    if (pool == null) return;

    float mult = Math.Clamp(SelectParticleMultiplier, 0f, 5f);
    if (mult <= 0.01f) return;

    var baseColor = SignalRarity(rarity).Color;
    if (rarity == 0) baseColor = Color.Lerp(baseColor, new Color(95, 220, 255), 0.58f);
    var hotColor = Color.Lerp(baseColor, Color.White, 0.86f);
    var goldTint = Color.Lerp(baseColor, new Color(255, 234, 155), 0.60f);

    if (SelectDissolveDirection == 0)
    {
      int perBurstCount = Math.Max(1, (int)((6 + rarity * 3) * mult));
      // Final 360-degree Clamshell Perimeter Shatter as the outward plasma wave reaches the card border
      for (int k = 0; k < 16; k++)
      {
        float qAngle = (k / 16f + 0.021f) % 1f;
        SampleClamshellPerimeter(qAngle, out var edgePos, out float edgeOutAngle);
        var outDir = new Vector2(MathF.Cos(edgeOutAngle), MathF.Sin(edgeOutAngle) - 0.30f);
        float emitAngle = MathF.Atan2(outDir.Y, outDir.X);
        bool isStreak = (k & 1) == 0;
        pool.Emit(new GpuParticleBurst(edgePos, perBurstCount, isStreak ? hotColor : goldTint)
        {
          Direction = emitAngle, Spread = isStreak ? 0.40f : 0.58f,
          Speed = (isStreak ? SelectCoreStreakSpeed : SelectCoreSparkSpeed) * (0.85f + rarity * 0.12f),
          Lifetime = (isStreak ? SelectCoreStreakLifetime : SelectCoreSparkLifetime) * (1.0f + rarity * 0.08f),
          Size = (isStreak ? SelectCoreStreakSize : SelectCoreSparkSize) * (1.0f + rarity * 0.04f),
          Radius = 26f, Streak = isStreak
        });
      }
    }
    else
    {
      var middleOrigin = new Vector2(0f, (SelectFocalY - 0.253f) * 964f);
      float rarityBoost = 1.15f + rarity * 0.24f;
      int finaleStreaks = Math.Max(6, (int)((96 + rarity * 54) * mult));
      int finaleSparks = Math.Max(6, (int)((84 + rarity * 46) * mult));
      pool.Emit(new GpuParticleBurst(middleOrigin, finaleStreaks, hotColor)
      {
        Motion = GpuParticleMotion.Outward, Radius = 52f, InnerRadius = 8f,
        Speed = SelectCoreStreakSpeed * rarityBoost, Spread = 0.30f,
        Lifetime = SelectCoreStreakLifetime * (1.05f + rarity * 0.10f),
        Size = SelectCoreStreakSize * (1.05f + rarity * 0.05f), Streak = true
      });
      pool.Emit(new GpuParticleBurst(middleOrigin, finaleSparks, goldTint)
      {
        Motion = GpuParticleMotion.Outward, Radius = 64f, InnerRadius = 12f,
        Speed = SelectCoreSparkSpeed * rarityBoost, Spread = 0.65f,
        Lifetime = SelectCoreSparkLifetime * (1.05f + rarity * 0.12f),
        Size = SelectCoreSparkSize * (1.05f + rarity * 0.06f)
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
    SimulateModuleRevealParticles();
  }

  private void DisposeSignalParticles()
  {
    foreach (var pool in signalParticlePools) pool?.Dispose();
    Array.Clear(signalParticlePools);
    DisposeModuleRevealParticles();
  }
}
