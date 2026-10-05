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
  public string Preset { get; set; } = "Beginning";
  public SceneSave Save { get; set; } = new();
  // Raw stat values by short name or property name, applied after the save loads (like the stat sliders).
  public Dictionary<string, JsonElement> Stats { get; set; } = new();
  // Multiplies the camera zoom after loading; below 1 shows (and plays in) more space.
  public float Zoom { get; set; } = 1;
  public List<SceneAction> Actions { get; set; } = new();
  public string Encoder { get; set; } = "auto";
  public int Quality { get; set; } = 16;
  // Seconds of recorded footage to also save as lossless PNG stills (store screenshots): <output>_<t>s.png.
  public List<double> Stills { get; set; } = new();
}

// Changes to the preset's save before it is loaded. Levels accept a number or "max".
public sealed class SceneSave
{
  public double? Gems { get; set; }
  public double? EarnedThisRun { get; set; }
  public double? AbilityPoints { get; set; }
  public double? PrestigePoints { get; set; }
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
  public int ActiveGems { get; set; }
  // Levels bought across all three upgrade trees.
  public int Upgrades { get; set; }
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
