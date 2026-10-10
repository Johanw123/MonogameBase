#if !KNI_WEB
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UntitledGemGame.Capture;

// One recorded shot for marketing footage (run with --capture scene.json).
// The world is staged from a debug preset plus overrides, simulated at a fixed
// frame rate and rendered offscreen, so slow encoding never changes the pacing.
// Times in actions are seconds of recorded footage; negative times run during warmup.
public sealed class CaptureScene
{
  public string Output { get; set; }
  public int Width { get; set; } = 2160;
  public int Height { get; set; } = 3840;
  public int Fps { get; set; } = 60;
  public double Warmup { get; set; } = 6;
  public double Duration { get; set; } = 10;
  // Time-lapse: simulation steps per recorded frame (6 = six times real speed). Action times stay in footage seconds.
  public int TimeScale { get; set; } = 1;
  // Compose the real HUD over the world. The HUD is laid out for 16:9, so this needs a landscape size.
  public bool Hud { get; set; }
  // Keep gems and ships out of the HUD strip. Off by default: without a HUD the whole frame is play area.
  public bool HudInset { get; set; }
  // Record the title screen from the moment it appears (the fleet's arrival) instead of a
  // staged session; preset, save and stats are ignored. Use with hud to include the menu itself.
  public bool MainMenu { get; set; }
  public string Preset { get; set; } = "Beginning";
  // A GameLanguage code ("de", "ja", ...); captures default to English whatever the system's language.
  public string Language { get; set; } = "en";
  public SceneSave Save { get; set; } = new();
  // Raw stat values by short name or property name, applied after the save loads (like the stat sliders).
  public Dictionary<string, JsonElement> Stats { get; set; } = new();
  // Multiplies the camera zoom after loading; below 1 shows (and plays in) more space.
  public float Zoom { get; set; } = 1;
  // Whether damage can set off core fractures on its own; the fracture action works either way.
  public bool AutoFractures { get; set; } = true;
  public List<SceneAction> Actions { get; set; } = new();
  public string Encoder { get; set; } = "auto";
  public int Quality { get; set; } = 16;
  // Seconds of recorded footage to also save as lossless PNG stills (store screenshots): <output>_<t>s.png.
  public List<double> Stills { get; set; } = new();
  // Measure instead of record (benchmark.sh): no video is read back or encoded, and the
  // report gains frame timings. Frames that save a still are left out of the timings.
  public bool Benchmark { get; set; }
  // A stand-in player for pacing playthroughs (Autoplay); null leaves the game to the actions.
  public SceneAutoplay Autoplay { get; set; }
}

// Changes to the preset's save before it is loaded. Levels accept a number or "max".
public sealed class SceneSave
{
  public double? Gems { get; set; }
  public double? EarnedThisRun { get; set; }
  public double? AbilityPoints { get; set; }
  public double? PrestigePoints { get; set; }
  // The prestige ladder (PrestigeProgression): points ever earned, this run's unpaid
  // points (shown on the extraction panel) and the echo the bar starts with, 0 to 1.
  public double? PrestigeEarned { get; set; }
  public double? PrestigePending { get; set; }
  public double? PrestigeEcho { get; set; }
  public int? ActiveGems { get; set; }
  public Dictionary<string, JsonElement> Upgrades { get; set; } = new();
  public Dictionary<string, JsonElement> Abilities { get; set; } = new();
  public Dictionary<string, JsonElement> Meta { get; set; } = new();
  public List<string> Equip { get; set; }
  // "all", or a list of module names to own.
  public JsonElement? Modules { get; set; }
  // Module names to queue as sealed discoveries (revealed in the shipyard's Discovery tab).
  public List<string> Reveal { get; set; }
  // Modules fitted per fleet class ("harvester", "advanced", ...): {"harvester": ["Rocket Rack", "Gun Pod"]}.
  public Dictionary<string, List<string>> Fit { get; set; }
  public List<SceneSignal> Signals { get; set; }
  // Fractures already this run: the planet starts cracked and swollen (and without its shell).
  public int? CoreFractures { get; set; }
  // The share of the planet's shell left: 1 untouched, 0.3 badly cracked. Unset, the
  // shell is already gone, so scenes show the planet itself unless they ask for it.
  public double? Shell { get; set; }
}

public sealed class SceneSignal
{
  public string Name { get; set; }
  public string Rarity { get; set; } = "Common";
  public int Count { get; set; } = 1;
}

// do: pointer, hide, click, hold, gravity, click_gems, ability, manual, upgrade, stat, zoom, panel, prestige, fracture, marker, gems
public sealed class SceneAction
{
  public double At { get; set; }
  public string Do { get; set; }
  public double Dur { get; set; }
  // Normalized frame position [x, y] (0..1, top left origin).
  public double[] Pos { get; set; }
  // "gems" aims at a gem cluster, "home" at the homebase; overrides pos.
  public string Target { get; set; }
  public string Id { get; set; }
  public JsonElement? Value { get; set; }
  public double Rate { get; set; } = 3;
  public string Name { get; set; }
  // gems: normalized target points, gem type (Blue, DarkBlue, Gold, LightGreen, Lilac, Purple, Red, Teal),
  // where they fly in from ("" = appear in place, "home", "edges") and the spawn order ("left", "random", "center").
  public List<double[]> Points { get; set; }
  public string Gem { get; set; } = "Red";
  public string From { get; set; } = "";
  public string Order { get; set; } = "left";
  // Named HUD element for pointer/click/hold (e.g. "nav:shipyard", "inspect", "module:Event Horizon", "card:1").
  public string Ui { get; set; }
}

[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
  ReadCommentHandling = JsonCommentHandling.Skip,
  AllowTrailingCommas = true,
  UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
  WriteIndented = true)]
[JsonSerializable(typeof(CaptureScene))]
[JsonSerializable(typeof(CaptureReport))]
[JsonSerializable(typeof(CaptureCatalog))]
internal partial class CaptureJsonContext : JsonSerializerContext { }

// Written next to the video: what happened when, for cutting and for the game-audio track.
public sealed class CaptureReport
{
  public string Video { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
  public int Fps { get; set; }
  public int Frames { get; set; }
  public double RenderSeconds { get; set; }
  public CaptureScene Scene { get; set; }
  public List<CaptureEvent> Events { get; set; } = new();
  public List<CaptureSample> Samples { get; set; } = new();
  public BenchmarkReport Benchmark { get; set; }
}

// Timings of a benchmark scene's recorded frames, in milliseconds of wall time. The
// game runs unthrottled (no vsync or fixed step), so a frame lasts as long as its work.
public sealed class BenchmarkReport
{
  public int Frames { get; set; }
  // From one frame's start to the next: update, draw, presenting and input.
  public FrameStats Frame { get; set; }
  public FrameStats Update { get; set; }
  public FrameStats Draw { get; set; }
  // Frames slower than 60 fps (16.7 ms) and 30 fps (33.3 ms).
  public int FramesOver16 { get; set; }
  public int FramesOver33 { get; set; }
  // Garbage collection while recording.
  public double AllocatedKbPerFrame { get; set; }
  public int Gen0Collections { get; set; }
  public int Gen1Collections { get; set; }
  public int Gen2Collections { get; set; }
  public double GcPauseMs { get; set; }
  // The load at the end of the recording.
  public int ActiveGems { get; set; }
  public int FlyingShips { get; set; }
  // Gem bookkeeping at the end: in play, in the render batch, and parked for reuse.
  public int LiveGems { get; set; }
  public int RenderedGems { get; set; }
  public int ParkedGems { get; set; }
  // Gem work per frame (means): gems in the update list and gem quads rebuilt for drawing.
  public double AwakeGems { get; set; }
  public double RebuiltQuads { get; set; }
  // Graphics work per frame (means): draw calls, render target switches and triangles.
  public double DrawCalls { get; set; }
  public double TargetSwitches { get; set; }
  public double Primitives { get; set; }
  // Every measured frame, in order, and the indices of those during which a GC ran:
  // for telling garbage collection spikes from slow game work.
  public List<double> FrameMs { get; set; }
  public List<int> GcFrames { get; set; }
}

public sealed class FrameStats
{
  public double Mean { get; set; }
  public double P50 { get; set; }
  public double P95 { get; set; }
  public double P99 { get; set; }
  public double Max { get; set; }
}

public sealed class CaptureEvent
{
  public double T { get; set; }
  public string Type { get; set; }
  public string Id { get; set; }
  public string File { get; set; }
  public float? Volume { get; set; }
  public float? Pitch { get; set; }
  public float? Pan { get; set; }
  public double[] Pos { get; set; }
}

public sealed class CaptureSample
{
  public double T { get; set; }
  public double Gems { get; set; }
  public double GemsPerMinute { get; set; }
  // Damage dealt to the planet over the last minute (CoreFracture).
  public double DamagePerMinute { get; set; }
  public int CoreFractures { get; set; }
  public double EarnedThisRun { get; set; }
  // Gem income over the last minute, which earns prestige points, and the extraction
  // panel's bar (echo plus that income's share of the next point) and unpaid points.
  public double SustainedIncome { get; set; }
  public double PrestigeBar { get; set; }
  public double PrestigePending { get; set; }
  // Every prestige point ever earned, extractions so far, and the Gem Lore multiplier on gem value.
  public double PrestigeEarned { get; set; }
  public double Extractions { get; set; }
  public double GemValue { get; set; }
  public int ActiveGems { get; set; }
  // Levels bought across all three upgrade trees.
  public int Upgrades { get; set; }
  // The planet's damage this run by source (the HUD's Damage panel), for checking combos.
  public Dictionary<string, double> DamageBySource { get; set; }
}

// --capture-list output: everything a scene can name.
public sealed class CaptureCatalog
{
  public List<CatalogEntry> Presets { get; set; } = new();
  public List<CatalogEntry> FeaturePresets { get; set; } = new();
  public List<CatalogUpgrade> Upgrades { get; set; } = new();
  public List<CatalogStat> Stats { get; set; } = new();
  public List<CatalogEntry> Abilities { get; set; } = new();
  public List<CatalogEntry> ManualAbilities { get; set; } = new();
  public List<string> Modules { get; set; } = new();
  public List<string> Signals { get; set; } = new();
  public List<string> SignalRarities { get; set; } = new();
  public List<string> Panels { get; set; } = new();
  public List<string> Sounds { get; set; } = new();
}

public sealed class CatalogEntry
{
  public string Id { get; set; }
  public string Name { get; set; }
  public string Description { get; set; }
}

public sealed class CatalogUpgrade
{
  public string Id { get; set; }
  public string Tree { get; set; }
  public string Name { get; set; }
  public string Stat { get; set; }
  public int Levels { get; set; }
  public string BlockedBy { get; set; }
}

public sealed class CatalogStat
{
  public string Id { get; set; }
  public string Property { get; set; }
  public string Name { get; set; }
  public string Type { get; set; }
  public string Base { get; set; }
  public string Source { get; set; }
}
#endif
