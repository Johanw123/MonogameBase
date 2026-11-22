#if !KNI_WEB
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Xna.Framework.Audio;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Capture;

// A logged human playthrough: --record-run <dir>. The run keeps its own save in <dir>, so it
// starts fresh and never touches the player's progress. Music stays off and Steam is skipped.
// <dir>/events.jsonl gets one JSON object per line: milestones (purchases and refunds, prestige
// points, extractions, endings), the player's input (world clicks, Tractor Belt intakes, fleet
// commands), an economy sample every 10 s of play and every sound that plays. Each line carries "us", the
// CLOCK_MONOTONIC time in microseconds (what Stopwatch reads on Linux, and what
// gpu-screen-recorder stamps its first frame with), and "play", the run's playtime in seconds:
// time on the gameplay screen while not paused, summed over every session of the run.
// tools/record_run.py records the screen alongside and turns the log into timings and a sound
// list lined up with the video.
public static class RunRecording
{
  public const string EventsFile = "events.jsonl";

  public static bool Active { get; private set; }
  // SIGTERM or SIGINT arrived (tools/record_run.py stopping the run): quit like the menu does,
  // saving the run, instead of dying mid-frame.
  public static volatile bool QuitRequested;
  public static string Folder { get; private set; }
  public static string SavePath => Path.Combine(Folder, "progress.json");

  // Frames longer than this (a suspended machine) count only this much playtime.
  private const double LongestFrame = 5;
  private const double FlushSeconds = 1;
  private const double SampleSeconds = 10;

  private static FileStream file;
  private static PosixSignalRegistration terminate, interrupt;
  private static readonly ArrayBufferWriter<byte> line = new();
  private static Utf8JsonWriter json;
  private static double playtime, nextSample;
  private static long previousFrame, lastFlush;
  private static bool gameplayThisFrame;
  private static bool known;
  private static ulong points, extractions, cells, scans;
  private static int fractures;
  private static bool shellBroken, demoEnd, escaped, prestiging, talents;

  public static void Configure(string folder)
  {
    Folder = Path.GetFullPath(folder);
    Directory.CreateDirectory(Folder);
    string path = Path.Combine(Folder, EventsFile);
    var (resumedPlaytime, sessions) = ReadPrevious(path);
    playtime = resumedPlaytime;
    file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
    json = new Utf8JsonWriter(line, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    Active = true;
    AudioManager.SoundPlayed += OnSoundPlayed;
    terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestQuit);
    interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestQuit);

    var w = Begin("session");
    w.WriteNumber("session", sessions + 1);
    w.WriteNumber("realtime_us", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000);
    w.WriteString("wall", DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"));
    w.WriteBoolean("fresh_save", !File.Exists(SavePath) && !File.Exists(SavePath + ".bak"));
    End(flush: true);
    previousFrame = Stopwatch.GetTimestamp();
    Console.WriteLine($"[RunRecording] Logging to {path} (session {sessions + 1}, playtime {playtime:0.0} s)");
  }

  public static void Close()
  {
    if (!Active) return;
    Begin("quit");
    End(flush: true);
    AudioManager.SoundPlayed -= OnSoundPlayed;
    terminate.Dispose();
    interrupt.Dispose();
    file.Dispose();
    Active = false;
  }

  private static void RequestQuit(PosixSignalContext context)
  {
    // A second signal while quitting ends the process the usual way.
    context.Cancel = !QuitRequested;
    QuitRequested = true;
  }

  // GameScreen.Update, every frame the gameplay screen runs: watch the run for milestones.
  public static void Update(UntitledGemGameGameScreen screen)
  {
    if (!Active) return;
    gameplayThisFrame = true;
    var state = screen.State;
    if (state == null) return;
    if (!known)
    {
      // The first gameplay frame of a session: start from the save's numbers, so a resumed
      // run does not log old milestones again.
      known = true;
      nextSample = playtime;
      points = state.PrestigePointsEarned;
      extractions = state.CoreExtractions;
      cells = state.AbilityPointsPurchased;
      scans = state.Signals.ScansPurchased;
      fractures = state.CoreFractures;
      shellBroken = state.ShellBroken;
      demoEnd = state.DemoEndShown;
      escaped = state.EscapedThisRun;
      prestiging = screen.m_prestiging;
      talents = screen.m_postPrestige;
      var w = Begin("gameplay");
      w.WriteNumber("points", points);
      w.WriteNumber("extractions", extractions);
      w.WriteNumber("red", state.CurrentRedGemCount);
      End(flush: true);
      return;
    }

    if (state.PrestigePointsEarned > points)
      Milestone("point", state.PrestigePointsEarned);
    points = state.PrestigePointsEarned;
    if (state.CoreExtractions > extractions)
      Milestone("extract", state.CoreExtractions);
    extractions = state.CoreExtractions;
    // Power cells and core fractures reset at extraction; only rises are milestones.
    if (state.AbilityPointsPurchased > cells)
      Milestone("power_cell", state.AbilityPointsPurchased);
    cells = state.AbilityPointsPurchased;
    if (state.CoreFractures > fractures)
      Milestone("fracture", (ulong)state.CoreFractures);
    fractures = state.CoreFractures;
    if (state.Signals.ScansPurchased > scans)
      Milestone("signal_scan", state.Signals.ScansPurchased);
    scans = state.Signals.ScansPurchased;
    if (state.ShellBroken && !shellBroken) Milestone("shell_broken");
    shellBroken = state.ShellBroken;
    if (state.DemoEndShown && !demoEnd) Milestone("demo_end");
    demoEnd = state.DemoEndShown;
    if (state.EscapedThisRun && !escaped) Milestone("escape");
    escaped = state.EscapedThisRun;
    // The extraction (or warp escape) sequence, the talent screen after it, then the next run.
    if (screen.m_prestiging && !prestiging)
      Milestone(screen.WarpEscaping ? "warp_sequence" : "prestige_sequence", state.PendingPrestigePoints);
    prestiging = screen.m_prestiging;
    if (screen.m_postPrestige && !talents) Milestone("talent_screen");
    if (!screen.m_postPrestige && talents) Milestone("run_start", state.CoreExtractions + 1);
    talents = screen.m_postPrestige;
    if (playtime >= nextSample && !screen.m_prestiging && !screen.m_postPrestige)
    {
      // The economy every few seconds of play, as capture samples log it for autoplay.
      nextSample = playtime + SampleSeconds;
      var w = Begin("sample");
      w.WriteNumber("red", state.CurrentRedGemCount);
      w.WriteNumber("earned_this_run", state.RedGemsEarnedThisRun);
      w.WriteNumber("sustained_income", Math.Round(state.Income.PerMinute));
      w.WriteNumber("damage_per_minute", Math.Round(screen.DamagePerMinute));
      w.WriteNumber("prestige_bar", Math.Round(state.PrestigeEcho + state.PrestigeProgress, 4));
      w.WriteNumber("gem_value", Math.Round(UpgradeManager.Instance.GemLoreMultiplier, 4));
      End();
    }
  }

  // GameMain.Update, after the screens: add this frame to the playtime when gameplay ran unpaused.
  public static void EndFrame()
  {
    if (!Active) return;
    long now = Stopwatch.GetTimestamp();
    double seconds = Stopwatch.GetElapsedTime(previousFrame, now).TotalSeconds;
    previousFrame = now;
    if (gameplayThisFrame && !GameMain.IsPaused && !PlaytestFeedback.IsOpen)
      playtime += Math.Min(seconds, LongestFrame);
    gameplayThisFrame = false;
    if (Stopwatch.GetElapsedTime(lastFlush, now).TotalSeconds >= FlushSeconds)
    {
      file.Flush();
      lastFlush = now;
    }
  }

  // UpgradeManager.Upgrade, once the price is paid. Kind is "buy" (upgrade tree), "talent"
  // (prestige talents) or "system" (ship system talents bought with power cells).
  public static void Purchase(string kind, string id, string name, int level, ulong cost, string currency)
  {
    if (!Active) return;
    var w = Begin(kind);
    w.WriteString("id", id);
    w.WriteString("name", name);
    w.WriteNumber("level", level);
    w.WriteNumber("cost", cost);
    w.WriteString("currency", currency);
    End();
  }

  // Player input to compare with autoplay: "click" (a world click; repeats while the button
  // is held carry "held"), "belt" (a Tractor Belt intake placed) and "command" (a fleet
  // command; id is its name).
  public static void Input(string type, string id = null, bool held = false)
  {
    if (!Active) return;
    var w = Begin(type);
    if (id != null) w.WriteString("id", id);
    if (held) w.WriteBoolean("held", true);
    End();
  }

  private static void Milestone(string type, ulong? count = null)
  {
    var w = Begin(type);
    if (count is ulong n) w.WriteNumber("count", n);
    End(flush: true);
  }

  private static void OnSoundPlayed(SoundEffect effect, float volume, float pitch, float pan)
  {
    var w = Begin("sound");
    w.WriteString("file", effect.Name);
    w.WriteNumber("volume", MathF.Round(volume, 4));
    w.WriteNumber("pitch", MathF.Round(pitch, 4));
    w.WriteNumber("pan", MathF.Round(pan, 4));
    End();
  }

  private static Utf8JsonWriter Begin(string type)
  {
    line.ResetWrittenCount();
    json.Reset(line);
    json.WriteStartObject();
    json.WriteNumber("us", MonotonicMicroseconds());
    json.WriteNumber("play", Math.Round(playtime, 3));
    json.WriteString("type", type);
    return json;
  }

  private static void End(bool flush = false)
  {
    json.WriteEndObject();
    json.Flush();
    file.Write(line.WrittenSpan);
    file.WriteByte((byte)'\n');
    if (flush) file.Flush();
  }

  // Stopwatch is clock_gettime(CLOCK_MONOTONIC) on Linux; split to keep nanoseconds from overflowing.
  private static long MonotonicMicroseconds()
  {
    long ticks = Stopwatch.GetTimestamp();
    long frequency = Stopwatch.Frequency;
    return ticks / frequency * 1_000_000 + ticks % frequency * 1_000_000 / frequency;
  }

  // The playtime and session count so far, from an existing log. Playtime only grows, so the
  // last whole line has it; a line cut short by a crash is skipped.
  private static (double Playtime, int Sessions) ReadPrevious(string path)
  {
    if (!File.Exists(path)) return (0, 0);
    string[] lines = File.ReadAllLines(path);
    int sessions = lines.Count(l => l.Contains("\"type\":\"session\"", StringComparison.Ordinal));
    for (int i = lines.Length - 1; i >= 0; i--)
    {
      try
      {
        using var doc = JsonDocument.Parse(lines[i]);
        if (doc.RootElement.TryGetProperty("play", out var play)) return (play.GetDouble(), sessions);
      }
      catch (JsonException) { }
    }
    return (0, sessions);
  }
}
#endif
