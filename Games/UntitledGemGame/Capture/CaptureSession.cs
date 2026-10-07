#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Capture;

// Drives the game for one recorded shot: boots offscreen, stages a save, plays the
// scene's actions on a fixed timestep and pipes rendered frames to FFmpeg.
// Uses an isolated save path and never reads or writes the player's Settings.json.
public static class CaptureSession
{
  private enum Phase { Boot, Run, Done }

  public static bool Active { get; private set; }
  public static CaptureScene Scene { get; private set; }
  public static ScriptedPointer Pointer { get; } = new();
  public static int ExitCode { get; private set; }
  public static string SavePath => Path.Combine(workDirectory, "progress.json");
  // Leave the HUD strip free of gems only when the HUD is part of the shot (or asked for).
  public static bool ReserveHudSpace => !Active || Scene.Hud || Scene.HudInset;
  // The world renders at the pixel area of a 4K screen in the shot's aspect (2160x3840 for 9:16),
  // so framing never depends on the output size; smaller outputs are scaled down on the GPU.
  // The window matches, keeping the HUD, its text and bloom in the same pixel space.
  public static int RenderWidth { get; private set; }
  public static int RenderHeight { get; private set; }
  public static int WindowWidth => RenderWidth;
  public static int WindowHeight => RenderHeight;

  private static string listPath;
  private static bool overwrite;
  private static string workDirectory;
  private static Phase phase;
  private static int frame;
  private static int warmupFrames;
  private static int recordFrames;
  private static int recorded;
  private static bool drawnThisFrame = true;
  private static byte[] pixels;
  private static Process encoder;
  private static RenderTarget2D composed;
  private static SpriteBatch composeBatch;
  private static readonly Stopwatch clock = new();
  // Benchmark timings (scene.benchmark): stopwatch ticks at the start of the frame and
  // of its draw; a measured frame stays open until the next frame starts.
  private static readonly List<double> frameMs = new(), updateMs = new(), drawMs = new();
  private static readonly List<int> gcFrames = new();
  private static GraphicsMetrics metricsAtDraw;
  private static long drawCalls, targetSwitches, primitives, awakeGems, rebuiltQuads;
  private static int collectionsAtFrameStart;
  private static long frameStart, drawStart;
  private static bool frameOpen;
  // Frames left out after a still: its readback drains the GPU and slows the next frame too.
  private static int settleFrames;
  private static long allocatedAtStart;
  private static readonly int[] collectionsAtStart = new int[3];
  private static TimeSpan pauseAtStart;
  private static CaptureReport report;
  private static List<SceneAction> pending;
  private static readonly List<Func<double, bool>> running = new();
  private static readonly Random random = new();
  private static readonly HashSet<IHomeBaseAbility> activeAbilities = new();
  private static readonly Dictionary<IHomeBaseAbility, int> cooldowns = new();

  // Seconds of recorded footage. With time_scale N the game runs N simulation steps per recorded frame.
  public static double Time => (frame - warmupFrames) / (double)(Scene.Fps * Scene.TimeScale);
  private static bool RecordsFrame(int f) => f >= warmupFrames && (f - warmupFrames) % Scene.TimeScale == 0;

  // Returns false when the arguments are not a capture request.
  public static bool Configure(string[] args)
  {
    int scene = Array.IndexOf(args, "--capture");
    int list = Array.IndexOf(args, "--capture-list");
    if (scene < 0 && list < 0) return false;
    overwrite = Array.IndexOf(args, "--overwrite") >= 0;
    string Argument(int index) => index + 1 < args.Length ? args[index + 1]
      : throw new ArgumentException($"{args[index]} needs a file path");
    if (scene >= 0)
    {
      string path = Path.GetFullPath(Argument(scene));
      Scene = JsonSerializer.Deserialize(File.ReadAllText(path), CaptureJsonContext.Default.CaptureScene)
        ?? throw new ArgumentException($"Empty scene file {path}");
      if (string.IsNullOrEmpty(Scene.Output)) throw new ArgumentException("Scene needs an output path");
      // Relative outputs are relative to the scene file, so scene folders can be moved.
      Scene.Output = Path.GetFullPath(Scene.Output, Path.GetDirectoryName(path));
      if (File.Exists(Scene.Output) && !overwrite)
        throw new ArgumentException($"{Scene.Output} exists; choose a new take name or pass --overwrite");
    }
    else
    {
      listPath = Path.GetFullPath(Argument(list));
      Scene = new CaptureScene { Width = 1280, Height = 720, Fps = 30, Duration = 0 };
    }
    if (Scene.Width < 16 || Scene.Height < 16 || Scene.Width % 2 != 0 || Scene.Height % 2 != 0)
      throw new ArgumentException("Width and height must be even and at least 16");
    if (Scene.Fps <= 0 || Scene.Duration < 0 || Scene.Warmup < 0) throw new ArgumentException("Invalid fps, duration or warmup");
    if (Scene.TimeScale < 1) throw new ArgumentException("time_scale must be 1 or more");
    if (Scene.Hud && Scene.Width * 9 != Scene.Height * 16) throw new ArgumentException("The HUD is laid out for 16:9; use a 16:9 size with hud");
    double aspect = Scene.Width / (double)Scene.Height;
    RenderHeight = (int)Math.Round(Math.Sqrt(3840.0 * 2160 / aspect) / 2) * 2;
    RenderWidth = (int)Math.Round(aspect * RenderHeight / 2) * 2;
    workDirectory = Directory.CreateTempSubdirectory("btb-capture-").FullName;
    warmupFrames = (int)Math.Round(Scene.Warmup * Scene.Fps);
    recordFrames = Math.Max(listPath != null ? 0 : 1, (int)Math.Round(Scene.Duration * Scene.Fps));
    if (Scene.Stills.Any(t => t < 0 || t * Scene.Fps >= recordFrames))
      throw new ArgumentException($"stills must be within the recorded {Scene.Duration} s");
    pending = Scene.Actions.OrderBy(a => a.At).ToList();
    foreach (var action in pending) Actions.Validate(action);
    Active = true;
    AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
    return true;
  }

  public static Settings CreateSettings() => new()
  {
    Width = WindowWidth, Height = WindowHeight, IsFullscreen = false, IsBorderless = false,
    IsFixedTimeStep = false, IsVSync = false, MusicVolume = 0,
    // The game default. Some sounds play at 2x the setting, and MonoGame rejects volumes above 1.
    SfxVolume = 0.5f
  };

  // Called at the start of GameMain.Update; returns the fixed-step time for this frame.
  public static GameTime BeginUpdate(GameMain game)
  {
    var step = TimeSpan.FromSeconds(1.0 / Scene.Fps);
    try
    {
      if (phase == Phase.Boot)
      {
        if (GameplayPreloader.Error != null) throw new InvalidOperationException($"Asset failed to load: {GameplayPreloader.Error}");
        if (!AsyncContent.AssetManager.IsLoadingContent() && GameplayPreloader.Ready) Setup(game);
        return new GameTime(TimeSpan.Zero, step);
      }
      if (phase == Phase.Run)
      {
        if (!drawnThisFrame && RecordsFrame(frame)) throw new InvalidOperationException($"Frame {frame} was not drawn");
        long now = Stopwatch.GetTimestamp();
        if (frameOpen)
        {
          if (GC.CollectionCount(0) != collectionsAtFrameStart) gcFrames.Add(frameMs.Count);
          frameMs.Add(Stopwatch.GetElapsedTime(frameStart, now).TotalMilliseconds);
        }
        frameOpen = false;
        frameStart = now;
        collectionsAtFrameStart = GC.CollectionCount(0);
        frame++;
        drawnThisFrame = false;
        if (frame == warmupFrames) StartRecording();
        if (frame % (Scene.Fps * 2) == 0)
          Console.WriteLine($"CAPTURE PROGRESS: {Time:F2}s, {recorded}/{recordFrames} frames recorded");
        // Time-lapse: steps between recorded frames only simulate.
        if (frame >= warmupFrames && !RecordsFrame(frame)) game.SuppressDraw();
        RunActions();
        TrackAbilities();
        Pointer.Commit();
      }
    }
    catch (Exception e) { Fail(game, e); }
    return new GameTime(TimeSpan.FromTicks(step.Ticks * Math.Max(0, frame)), step);
  }

  private static void ReadBackFrame(GameMain game)
  {
    var world = BaseGame.renderTarget2;
    if (world.Width != RenderWidth || world.Height != RenderHeight)
      throw new InvalidOperationException($"World target is {world.Width}x{world.Height}, expected {RenderWidth}x{RenderHeight}");
    var source = Scene.Hud || world.Width != Scene.Width || world.Height != Scene.Height ? Compose(game.GraphicsDevice) : world;
    if (source.Width != Scene.Width || source.Height != Scene.Height)
      throw new InvalidOperationException($"Render target is {source.Width}x{source.Height}, expected {Scene.Width}x{Scene.Height}");
    pixels ??= new byte[source.Width * source.Height * 4];
    source.GetData(pixels);
    if (!Scene.Benchmark)
    {
      encoder ??= StartEncoder(source.Width, source.Height);
      encoder.StandardInput.BaseStream.Write(pixels);
    }
    foreach (double still in Scene.Stills.Where(t => (int)Math.Round(t * Scene.Fps) == recorded))
      SaveStill(still, source.Width, source.Height);
  }

  // The first recorded frame: benchmark.sh attaches its trace here.
  private static void StartRecording()
  {
    Console.WriteLine("CAPTURE RECORDING");
    allocatedAtStart = GC.GetTotalAllocatedBytes();
    for (int generation = 0; generation < collectionsAtStart.Length; generation++)
      collectionsAtStart[generation] = GC.CollectionCount(generation);
    pauseAtStart = GC.GetTotalPauseDuration();
  }

  // Called at the start of GameMain.Draw.
  public static void BeginDraw(GraphicsDevice device)
  {
    if (phase != Phase.Run) return;
    drawStart = Stopwatch.GetTimestamp();
    metricsAtDraw = device.Metrics;
  }

  // Called after GameMain.Draw: reads the finished world (and HUD) image back.
  public static void EndDraw(GameMain game)
  {
    if (phase != Phase.Run) return;
    drawnThisFrame = true;
    if (!RecordsFrame(frame)) return;
    try
    {
      bool stillDue = Scene.Stills.Any(t => (int)Math.Round(t * Scene.Fps) == recorded);
      if (Scene.Benchmark && !stillDue)
      {
        if (settleFrames > 0)
          settleFrames--;
        else
        {
          long now = Stopwatch.GetTimestamp();
          updateMs.Add(Stopwatch.GetElapsedTime(frameStart, drawStart).TotalMilliseconds);
          drawMs.Add(Stopwatch.GetElapsedTime(drawStart, now).TotalMilliseconds);
          var metrics = game.GraphicsDevice.Metrics;
          drawCalls += metrics.DrawCount - metricsAtDraw.DrawCount;
          targetSwitches += metrics.TargetCount - metricsAtDraw.TargetCount;
          primitives += metrics.PrimitiveCount - metricsAtDraw.PrimitiveCount;
          awakeGems += UpdateSystem2.Instance?.UpdatingGemCount ?? 0;
          rebuiltQuads += RenderGemSystem.Instance?.RebuiltQuadsLastFrame ?? 0;
          frameOpen = true;
        }
      }
      else
      {
        ReadBackFrame(game);
        if (Scene.Benchmark) settleFrames = 2;
      }
      if (recorded % Math.Max(1, Scene.Fps / 4) == 0) Sample();
      recorded++;
      if (recorded >= recordFrames) Finish(game);
    }
    catch (Exception e) { Fail(game, e); }
  }

  private static void Setup(GameMain game)
  {
    Gum.GumService.Default.Root.Children.Clear();
    Gum.GumService.Default.ModalRoot.Children.Clear();
    GameMain.CurrentMenu = "";
    GameMain.IsPaused = false;

    // A fresh session first, so the upgrade trees exist for building the preset.
    game.CaptureScreens.ReplaceScreen(new UntitledGemGameGameScreen(game, true));
    var first = UntitledGemGameGameScreen.Instance;
    if (first == null || UpgradeManager.CurrentUpgrades.UpgradeButtons.Count == 0)
      throw new InvalidOperationException("Game screen did not initialise");
    if (listPath != null)
    {
      File.WriteAllText(listPath, JsonSerializer.Serialize(Catalog.Build(), CaptureJsonContext.Default.CaptureCatalog));
      Console.WriteLine($"CAPTURE LIST: {listPath}");
      phase = Phase.Done;
      game.Exit();
      return;
    }

    var save = Staging.BuildSave(Scene, UpgradeManager.CurrentUpgrades);
    if (!new GameSaveStore(SavePath).Save(save)) throw new InvalidOperationException("Could not write the staged save");
    first.DiscardProgressOnUnload();
    game.CaptureScreens.ReplaceScreen(new UntitledGemGameGameScreen(game));
    var screen = UntitledGemGameGameScreen.Instance;
    if (screen == first) throw new InvalidOperationException("Staged session did not load");
    Staging.ApplyStats(Scene.Stats);
    // Cooldowns were set while loading; restart them so cooldown stats count from the first frame.
    foreach (var ability in HomeBase.Instance.ActiveAbilities.Where(a => a is not EmptyAbility))
      ability.CooldownTime = ability.MaxCooldownTime;
    var upgrades = UpgradeManager.Instance.UG;
    upgrades.CameraZoomScale *= Scene.Zoom;
    screen.m_camera.Zoom = upgrades.CameraZoomScale;
    foreach (var id in Scene.Save.Equip ?? new())
      if (!HomeBase.Instance.GetEquippedAbilities().Contains(Actions.AbilityId(id)))
        throw new InvalidOperationException($"Ability {id} could not be equipped (not enough ability slots?)");

    AudioManager.SoundPlayed += OnSoundPlayed;
    report = new CaptureReport { Video = Scene.Output, Width = Scene.Width, Height = Scene.Height, Fps = Scene.Fps, Scene = Scene };
    clock.Start();
    phase = Phase.Run;
    frame = -1;
    Console.WriteLine($"CAPTURE: {Scene.Preset}, {Scene.Width}x{Scene.Height} at {Scene.Fps} fps, " +
      $"{Scene.Warmup}s warmup + {Scene.Duration}s -> {Scene.Output}");
  }

  private static void RunActions()
  {
    double t = Time;
    double halfFrame = 0.5 / Scene.Fps;
    while (pending.Count > 0 && pending[0].At <= t + halfFrame)
    {
      var action = pending[0];
      pending.RemoveAt(0);
      Log("action", action.Do + (action.Id != null ? ":" + action.Id : action.Name != null ? ":" + action.Name : ""));
      var behaviour = Actions.Start(action);
      if (behaviour != null) running.Add(behaviour);
    }
    running.RemoveAll(behaviour => behaviour(t));
  }

  // Automatic abilities fire on cooldown; log each activation as a cut point. Timed
  // abilities become active; instant ones (no duration) only restart their cooldown.
  private static void TrackAbilities()
  {
    var home = HomeBase.Instance;
    if (home == null) return;
    foreach (var ability in home.ActiveAbilities)
    {
      if (ability is EmptyAbility) continue;
      bool wasActive = activeAbilities.Contains(ability);
      int previousCooldown = cooldowns.GetValueOrDefault(ability, ability.CooldownTime);
      bool restarted = !ability.IsActive && !wasActive && ability.CooldownTime > previousCooldown;
      if ((ability.IsActive && !wasActive) || restarted) Log("ability", HomeBase.GetAbilityUpgradeId(ability));
      if (ability.IsActive) activeAbilities.Add(ability); else activeAbilities.Remove(ability);
      cooldowns[ability] = ability.CooldownTime;
    }
  }

  internal static void Log(string type, string id, double[] position = null)
  {
    if (report == null || frame < warmupFrames) return;
    report.Events.Add(new CaptureEvent { T = Math.Round(Time, 4), Type = type, Id = id, Pos = position });
  }

  private static void OnSoundPlayed(SoundEffect effect, float volume, float pitch, float pan)
  {
    if (phase != Phase.Run || frame < warmupFrames) return;
    report.Events.Add(new CaptureEvent { T = Math.Round(Time, 4), Type = "sound", File = effect.Name,
      Volume = volume, Pitch = pitch, Pan = pan });
  }

  private static void Sample()
  {
    var screen = UntitledGemGameGameScreen.Instance;
    var state = screen.State;
    report.Samples.Add(new CaptureSample
    {
      T = Math.Round(Time, 4),
      Gems = PrestigeProgression.AddSaturating(state.CurrentRedGemCount, UntitledGemGameGameScreen.DeliveredUncounted),
      GemsPerMinute = screen.CaptureGemsPerMinute,
      DamagePerMinute = Math.Round(screen.DamagePerMinute),
      CoreFractures = state.CoreFractures,
      EarnedThisRun = state.RedGemsEarnedThisRun,
      ActiveGems = HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems,
      Upgrades = UpgradeManager.CurrentUpgrades.UpgradeButtons.Values
        .Concat(UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities.Values)
        .Concat(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta.Values).Sum(b => b.CurrentLevel),
      DamageBySource = state.Damage.RunTotals(),
    });
  }

  private static RenderTarget2D Compose(GraphicsDevice device)
  {
    composed ??= new RenderTarget2D(device, Scene.Width, Scene.Height);
    composeBatch ??= new SpriteBatch(device);
    var bounds = new Rectangle(0, 0, Scene.Width, Scene.Height);
    device.SetRenderTarget(composed);
    device.Clear(Color.Black);
    composeBatch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.LinearClamp);
    composeBatch.Draw(BaseGame.renderTarget2, bounds, Color.White * (Scene.Hud ? 1 - BaseGame.DimmingFactor : 1));
    composeBatch.End();
    if (Scene.Hud)
    {
      composeBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp);
      composeBatch.Draw(BaseGame._renderTargetHud, bounds, Color.White);
      // The player's mouse over the UI (the real OS cursor is not part of an offscreen render).
      if (Pointer.Visible)
      {
        float scale = Scene.Width / (float)RenderWidth * (Pointer.Left ? 0.88f : 1f);
        var tip = Pointer.State.Position.ToVector2() * (Scene.Width / (float)RenderWidth);
        composeBatch.Draw(CursorTexture(device), tip, null, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
      }
      composeBatch.End();
    }
    device.SetRenderTarget(null);
    return composed;
  }

  private static Texture2D cursorTexture;

  // A classic arrow pointer sized for a 4K HUD: white fill with a dark outline, tip at (0, 0).
  private static Texture2D CursorTexture(GraphicsDevice device)
  {
    if (cursorTexture != null) return cursorTexture;
    const int width = 50, height = 74, outline = 4;
    Vector2[] shape = [new(3, 3), new(3, 58), new(16, 46), new(26, 69), new(36, 64), new(26, 42), new(43, 42)];
    var pixels = new Color[width * height];
    for (int y = 0; y < height; y++)
      for (int x = 0; x < width; x++)
      {
        var point = new Vector2(x + 0.5f, y + 0.5f);
        bool inside = false;
        float distance = float.MaxValue;
        for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
        {
          var a = shape[i]; var b = shape[j];
          if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
          var edge = b - a;
          float t = Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
          distance = Math.Min(distance, Vector2.Distance(point, a + edge * t));
        }
        pixels[y * width + x] = inside && distance > outline ? Color.White
          : inside || distance <= outline ? new Color(10, 16, 24) : Color.Transparent;
      }
    cursorTexture = new Texture2D(device, width, height);
    cursorTexture.SetData(pixels);
    return cursorTexture;
  }

  private static Process StartEncoder(int width, int height)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(Scene.Output));
    string codec = Scene.Encoder switch
    {
      "nvenc" => "nvenc",
      "x264" => "x264",
      "auto" => HasEncoder("h264_nvenc") ? "nvenc" : "x264",
      _ => throw new ArgumentException($"Unknown encoder {Scene.Encoder}; use auto, nvenc or x264")
    };
    var start = new ProcessStartInfo("ffmpeg") { RedirectStandardInput = true, UseShellExecute = false };
    string[] arguments =
    [
      "-y", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "rgba",
      "-video_size", $"{width}x{height}", "-framerate", Scene.Fps.ToString(), "-i", "pipe:0", "-an",
      .. codec == "nvenc"
        ? new[] { "-c:v", "h264_nvenc", "-preset", "p6", "-tune", "hq", "-rc", "vbr", "-cq", Scene.Quality.ToString(), "-b:v", "0", "-profile:v", "high" }
        : new[] { "-c:v", "libx264", "-preset", "medium", "-crf", Scene.Quality.ToString() },
      "-pix_fmt", "yuv420p", "-movflags", "+faststart", Scene.Output
    ];
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    return Process.Start(start) ?? throw new InvalidOperationException("Could not start ffmpeg");
  }

  // The same RGBA frame the video gets, written losslessly (alpha dropped as in the video).
  private static void SaveStill(double time, int width, int height)
  {
    string path = $"{Path.ChangeExtension(Scene.Output, null)}_{time:0.00}s.png";
    var start = new ProcessStartInfo("ffmpeg") { RedirectStandardInput = true, UseShellExecute = false };
    foreach (var argument in new[] { "-y", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "rgba",
      "-video_size", $"{width}x{height}", "-i", "pipe:0", "-frames:v", "1", "-pix_fmt", "rgb24", path })
      start.ArgumentList.Add(argument);
    using var still = Process.Start(start) ?? throw new InvalidOperationException("Could not start ffmpeg");
    still.StandardInput.BaseStream.Write(pixels);
    still.StandardInput.Close();
    still.WaitForExit();
    if (still.ExitCode != 0) throw new InvalidOperationException($"ffmpeg could not write {path}");
    Log("still", path);
  }

  private static bool HasEncoder(string name)
  {
    try
    {
      var info = new ProcessStartInfo("ffmpeg", "-hide_banner -encoders") { RedirectStandardOutput = true, UseShellExecute = false };
      using var process = Process.Start(info);
      string output = process.StandardOutput.ReadToEnd();
      process.WaitForExit();
      return output.Contains(" " + name + " ");
    }
    catch (Exception) { return false; }
  }

  private static void Finish(GameMain game)
  {
    if (encoder != null)
    {
      encoder.StandardInput.Close();
      encoder.WaitForExit();
      if (encoder.ExitCode != 0) throw new InvalidOperationException($"ffmpeg exited with {encoder.ExitCode}");
      encoder.Dispose();
      encoder = null;
    }
    report.Frames = recorded;
    report.RenderSeconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
    if (Scene.Benchmark)
    {
      report.Benchmark = MeasuredBenchmark();
      Console.WriteLine($"BENCHMARK: {report.Benchmark.Frames} frames, mean {report.Benchmark.Frame.Mean} ms, " +
        $"p99 {report.Benchmark.Frame.P99} ms, max {report.Benchmark.Frame.Max} ms");
    }
    string reportPath = Path.ChangeExtension(Scene.Output, ".capture.json");
    File.WriteAllText(reportPath, JsonSerializer.Serialize(report, CaptureJsonContext.Default.CaptureReport));
    Console.WriteLine($"CAPTURE DONE: {recorded} frames in {report.RenderSeconds}s -> {Scene.Output}");
    phase = Phase.Done;
    game.Exit();
  }

  private static BenchmarkReport MeasuredBenchmark()
  {
    static FrameStats Stats(List<double> values)
    {
      if (values.Count == 0) return new FrameStats();
      var sorted = values.OrderBy(v => v).ToArray();
      double At(double share) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(share * sorted.Length) - 1)];
      return new FrameStats
      {
        Mean = Math.Round(sorted.Average(), 3), P50 = Math.Round(At(0.5), 3), P95 = Math.Round(At(0.95), 3),
        P99 = Math.Round(At(0.99), 3), Max = Math.Round(sorted[^1], 3),
      };
    }
    var ships = new List<(Harvester Ship, MonoGame.Extended.Transform2 Transform)>();
    HarvesterCollectionSystem.Instance?.CollectFlyingShips(ships);
    return new BenchmarkReport
    {
      Frames = frameMs.Count,
      Frame = Stats(frameMs),
      Update = Stats(updateMs),
      Draw = Stats(drawMs),
      FramesOver16 = frameMs.Count(ms => ms > 1000.0 / 60),
      FramesOver33 = frameMs.Count(ms => ms > 1000.0 / 30),
      AllocatedKbPerFrame = Math.Round((GC.GetTotalAllocatedBytes() - allocatedAtStart) / 1024.0 / Math.Max(1, recorded), 1),
      Gen0Collections = GC.CollectionCount(0) - collectionsAtStart[0],
      Gen1Collections = GC.CollectionCount(1) - collectionsAtStart[1],
      Gen2Collections = GC.CollectionCount(2) - collectionsAtStart[2],
      GcPauseMs = Math.Round((GC.GetTotalPauseDuration() - pauseAtStart).TotalMilliseconds, 1),
      ActiveGems = HarvesterCollectionSystem.Instance?.flatSpatialHash.NumActiveGems ?? 0,
      FlyingShips = ships.Count,
      LiveGems = UpdateSystem2.Instance?.LiveGemCount ?? 0,
      RenderedGems = RenderGemSystem.Instance?.GemCount ?? 0,
      ParkedGems = EntityFactory.Instance?.ParkedGemCount ?? 0,
      FrameMs = frameMs.Select(ms => Math.Round(ms, 2)).ToList(),
      GcFrames = gcFrames.ToList(),
      DrawCalls = Math.Round((double)drawCalls / Math.Max(1, drawMs.Count), 1),
      TargetSwitches = Math.Round((double)targetSwitches / Math.Max(1, drawMs.Count), 1),
      Primitives = Math.Round((double)primitives / Math.Max(1, drawMs.Count)),
      AwakeGems = Math.Round((double)awakeGems / Math.Max(1, drawMs.Count)),
      RebuiltQuads = Math.Round((double)rebuiltQuads / Math.Max(1, drawMs.Count)),
    };
  }

  private static void Fail(GameMain game, Exception error)
  {
    if (phase == Phase.Done) return;
    phase = Phase.Done;
    ExitCode = 1;
    Console.Error.WriteLine($"CAPTURE FAILED: {error.Message}");
    Console.Error.WriteLine(error.StackTrace);
    try { encoder?.Kill(); encoder?.WaitForExit(); } catch (Exception) { }
    encoder = null;
    if (Scene.Output != null && File.Exists(Scene.Output) && recorded < recordFrames) File.Delete(Scene.Output);
    game.Exit();
  }

  private static void Cleanup()
  {
    try { encoder?.Kill(); } catch (Exception) { }
    try { if (workDirectory != null) Directory.Delete(workDirectory, true); } catch (Exception) { }
  }
}
#endif
