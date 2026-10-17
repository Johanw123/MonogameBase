using AsyncContent;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using Serilog;
using System;
using System.Collections.Generic;
using UntitledGemGame;
using UntitledGemGame.Entities;

public enum SoundEvent
{
  // UI & Menu
  MenuHover,
  MenuClick,
  TooltipShow,
  WindowOpen,
  WindowClose,
  UpgradeStart,
  UpgradeComplete,
  UpgradeError,
  UpgradeRefund,
  ManualAbilityActivate,
  SignalScan,
  SignalChoose,

  // Gems & Cursor
  GemPickup,
  GemClick,
  GemMerge,
  CursorGravityCollapse,
  CursorGravityReady,

  // Harvesters & Refueling
  RefuelRequest,
  RefuelStart,
  RefuelLoop,
  RefuelComplete,
  HarvesterDeliverCargo,
  HarvesterWarpDrive,
  JackpotHaul,

  // Shipyard & Module Salvage
  ModuleSalvageReady,
  ModuleRevealStart,
  ModuleRevealCommon,
  ModuleRevealEpic,
  ModuleRevealLegendary,
  ModuleEquip,
  ModuleUnequip,

  // Ship & Intro
  ShipEngineDying,
  ShipCrashLand,

  // Ship Systems & Abilities
  AbilitySpeedboostActivate,
  AbilityMagnetActivate,
  AbilityChainLightningActivate,
  AbilityDroneSwarmActivate,
  AbilityGemSpawnerActivate,
  CoreDrillLaunch,
  CoreDrillImpact,
  CoreDrillRupture,
  CoreDrillCoreTap,
  KamikazeWingLaunch,

  // Weapons — Firing & Launching
  CannonFireManual,
  CannonFireAuto,
  RocketLaunch,
  RocketClusterSplit,
  LaserOverheatSurge,
  RailgunWindUp,
  RailgunFire,
  ArcHarpoonLaunch,
  ArcHarpoonPulse,
  ArcHarpoonRelease,

  // Weapons — Planet Impacts & Combos
  CannonImpact,
  CannonCriticalImpact,
  CannonRicochet,
  RocketImpact,
  RocketMiniImpact,
  ArcHarpoonImpact,
  RailgunImpact,
  PlanetCrackerStart,
  KamikazeImpact,
  KamikazeBombletImpact,
  KamikazeDoomsdayImpact,
  MagmaDetonation,
  PlanetaryOverloadErupt,
  WeakPointBurst,
  WeaponJackpot,

  // Planet Shell
  PlanetShellShatterStart,
  PlanetShellBurst,

  // Core Fracture & Core Shards
  CoreFractureStart,
  CoreFractureHeartbeat,
  CoreFractureSwallow,
  CoreFractureEruption,
  CoreShardRelease,
  CoreShardClick,
  CoreShardCollect,

  // Prestige & Time Loop
  PrestigePointEarned,
  PrestigePointLanded,
  CoreExtractionStart,
  TimeLoopCollapseBuildup,
  TimeLoopCollapseImplode,
  TimeLoopPlanetSwallowed,
  TimeLoopRestart,
}

public readonly record struct SoundCue(
  string Path,
  float Volume = 1f,
  float Pitch = 0f,
  float PitchJitter = 0f,
  float Priority = 1f,
  AudioManager.SoundType Type = AudioManager.SoundType.Default,
  bool Preload = true,
  bool Optional = false,
  string FallbackPath = null,
  float FallbackVolume = 1f,
  float Pan = 0f
);

public partial class AudioManager
{
  // ============================================================================
  // CENTRAL AUDIO CONFIGURATION
  // Change sound paths, volumes, pitches, or random pitch jitter for any game
  // event here. Any new "SFX/..." path referenced below is loaded automatically.
  // Events set to SfxNone are silent placeholders ready to be assigned a sound.
  // ============================================================================

  private const string SfxNone            = null;
  private const string SfxMenuHover       = "SFX/Menu/Soundpack/Minimalist7.wav";
  private const string SfxMenuClick       = "SFX/Menu/Soundpack/Minimalist10.wav";
  private const string SfxTooltipShow     = "SFX/Menu/hover_tooltip.wav";
  private const string SfxUpgradeStart    = "SFX/Menu/swoosh_4.wav";
  private const string SfxUpgradeDone     = "SFX/Menu/test3.wav";
  private const string SfxGemPickup       = "SFX/gem.wav";
  private const string SfxGemClick        = "SFX/Menu/Abstract1.wav";
  private const string SfxImpact          = "SFX/Impact_test2.wav";
  private const string SfxBlip            = "SFX/blip.wav";
  private const string SfxShipEngineDying = "SFX/Ship.wav";
  private const string SfxRefuelStart     = "SFX/Refuel/start";
  private const string SfxRefuelLoop      = "SFX/Refuel/loop";
  private const string SfxRefuelComplete  = "SFX/Refuel/complete";

  private const string SfxTest = "SFX/Next-Gen-UI-Designed-Ocular-Sounds/Next-Gen UI - Designed/Select/UIMisc_SELECT-Close All Windows_Ocular_NEXT-GEN UI.wav";

  private static readonly string[] SongNames =
  [
    "Greys",
    "Hopkinsville Goblins",
    "Pleiadeans",
    "Sky Fish",
  ];

  private static readonly Dictionary<SoundEvent, SoundCue[]> EventCues = new()
  {
    // UI & Menu
    [SoundEvent.MenuHover]             = [new(SfxMenuHover, Preload: false)],
    [SoundEvent.MenuClick]             = [new(SfxMenuClick, Preload: false)],
    [SoundEvent.TooltipShow]           = [new(SfxTooltipShow, Volume: 2.0f)],
    [SoundEvent.WindowOpen]            = [new(SfxNone)],
    [SoundEvent.WindowClose]           = [new(SfxNone)],
    [SoundEvent.UpgradeStart]          = [new(SfxUpgradeStart, Volume: 2.0f)],
    [SoundEvent.UpgradeComplete]       = [new(SfxUpgradeDone, PitchJitter: 0.2f, Volume: 2.0f)],
    [SoundEvent.UpgradeError]          = [new(SfxNone)],
    [SoundEvent.UpgradeRefund]         = [new(SfxNone)],
    [SoundEvent.ManualAbilityActivate] = [new(SfxMenuHover)],
    [SoundEvent.SignalScan]            = [new(SfxNone)],
    [SoundEvent.SignalChoose]          = [new(SfxNone)],

    // Gems & Cursor
    [SoundEvent.GemPickup]             = [new(SfxGemPickup, PitchJitter: 0.2f, Priority: 0.5f, Type: SoundType.GemCollect)],
    [SoundEvent.GemClick]              = [new(SfxGemClick, PitchJitter: 0.15f)],
    [SoundEvent.GemMerge]              = [new(SfxNone)],
    [SoundEvent.CursorGravityCollapse] = [new(SfxGemClick)],
    [SoundEvent.CursorGravityReady]    = [new(SfxBlip, Pitch: 0.25f)],

    // Harvesters & Refueling
    [SoundEvent.RefuelRequest]         = [new(SfxBlip)],
    [SoundEvent.RefuelStart]           = [new(SfxRefuelStart, Optional: true)],
    [SoundEvent.RefuelLoop]            = [new(SfxRefuelLoop, Volume: 0.25f, Optional: true)],
    [SoundEvent.RefuelComplete]        = [new(SfxRefuelComplete, Optional: true)],
    [SoundEvent.HarvesterDeliverCargo] = [new(SfxNone)],
    [SoundEvent.HarvesterWarpDrive]    = [new(SfxNone)],
    [SoundEvent.JackpotHaul]           = [new(SfxNone)],

    // Shipyard & Module Salvage
    [SoundEvent.ModuleSalvageReady]    = [new(SfxBlip)],
    [SoundEvent.ModuleRevealStart]     = [new(SfxUpgradeStart, Volume: 2.0f)],
    [SoundEvent.ModuleRevealCommon]    = [new(SfxUpgradeDone, Volume: 2.0f)],
    [SoundEvent.ModuleRevealEpic]      = [new(SfxRefuelComplete, Pitch: 0.2f, Optional: true, FallbackPath: SfxUpgradeDone, FallbackVolume: 2.0f)],
    [SoundEvent.ModuleRevealLegendary] = [new(SfxImpact, Pitch: -0.35f)],
    [SoundEvent.ModuleEquip]           = [new(SfxNone)],
    [SoundEvent.ModuleUnequip]         = [new(SfxNone)],

    // Ship & Intro
    [SoundEvent.ShipEngineDying]       = [new(SfxShipEngineDying)],
    [SoundEvent.ShipCrashLand]         = [new(SfxImpact)],

    // Ship Systems & Abilities
    [SoundEvent.AbilitySpeedboostActivate]     = [new(SfxNone)],
    [SoundEvent.AbilityMagnetActivate]         = [new(SfxNone)],
    [SoundEvent.AbilityChainLightningActivate] = [new(SfxNone)],
    [SoundEvent.AbilityDroneSwarmActivate]     = [new(SfxNone)],
    [SoundEvent.AbilityGemSpawnerActivate]     = [new(SfxNone)],
    [SoundEvent.CoreDrillLaunch]               = [new(SfxNone)],
    [SoundEvent.CoreDrillImpact]               = [new(SfxNone)],
    [SoundEvent.CoreDrillRupture]              = [new(SfxNone)],
    [SoundEvent.CoreDrillCoreTap]              = [new(SfxNone)],
    [SoundEvent.KamikazeWingLaunch]            = [new(SfxNone)],

    // Weapons — Firing & Launching
    [SoundEvent.CannonFireManual]   = [new(SfxNone, PitchJitter: 0.1f, Priority: 0.5f)],
    [SoundEvent.CannonFireAuto]     = [new(SfxNone, PitchJitter: 0.1f, Priority: 0.4f)],
    [SoundEvent.RocketLaunch]       = [new(SfxNone, PitchJitter: 0.1f, Priority: 0.6f)],
    [SoundEvent.RocketClusterSplit] = [new(SfxNone, PitchJitter: 0.1f, Priority: 0.5f)],
    [SoundEvent.LaserOverheatSurge] = [new(SfxNone)],
    [SoundEvent.RailgunWindUp]      = [new(SfxNone)],
    [SoundEvent.RailgunFire]        = [new(SfxNone)],
    [SoundEvent.ArcHarpoonLaunch]   = [new(SfxNone)],
    [SoundEvent.ArcHarpoonPulse]    = [new(SfxNone)],
    [SoundEvent.ArcHarpoonRelease]  = [new(SfxNone)],

    // Weapons — Planet Impacts & Combos
    [SoundEvent.CannonImpact]           = [new(SfxNone, PitchJitter: 0.15f, Priority: 0.4f)],
    [SoundEvent.CannonCriticalImpact]   = [new(SfxNone, PitchJitter: 0.1f, Priority: 0.7f)],
    [SoundEvent.CannonRicochet]         = [new(SfxNone, PitchJitter: 0.15f, Priority: 0.5f)],
    [SoundEvent.RocketImpact]           = [new(SfxNone, PitchJitter: 0.12f, Priority: 0.6f)],
    [SoundEvent.RocketMiniImpact]       = [new(SfxNone, PitchJitter: 0.15f, Priority: 0.4f)],
    [SoundEvent.ArcHarpoonImpact]       = [new(SfxNone)],
    [SoundEvent.RailgunImpact]          = [new(SfxImpact)],
    [SoundEvent.PlanetCrackerStart]     = [new(SfxImpact)],
    [SoundEvent.KamikazeImpact]         = [new(SfxImpact, Pitch: 0.3f, Priority: 0.5f)],
    [SoundEvent.KamikazeBombletImpact]  = [new(SfxImpact, Pitch: 0.6f, Priority: 0.3f)],
    [SoundEvent.KamikazeDoomsdayImpact] = [new(SfxImpact, Pitch: -0.6f, Priority: 0.5f)],
    [SoundEvent.MagmaDetonation]        = [new(SfxNone)],
    [SoundEvent.PlanetaryOverloadErupt] = [new(SfxNone)],
    [SoundEvent.WeakPointBurst]         = [new(SfxNone)],
    [SoundEvent.WeaponJackpot]          = [new(SfxNone)],

    // Planet Shell
    [SoundEvent.PlanetShellShatterStart] = [new(SfxImpact, Pitch: -0.3f)],
    [SoundEvent.PlanetShellBurst]        = [new(SfxImpact, Pitch: 0.3f), new(SfxGemClick, Pitch: -0.4f)],

    // Core Fracture & Core Shards
    [SoundEvent.CoreFractureStart]     = [new(SfxImpact, Pitch: -0.6f)],
    [SoundEvent.CoreFractureHeartbeat] = [new(SfxImpact, Pitch: -0.8f)],
    [SoundEvent.CoreFractureSwallow]   = [new(SfxImpact, Pitch: -0.4f)],
    [SoundEvent.CoreFractureEruption]  = [new(SfxImpact), new(SfxImpact, Pitch: -0.5f)],
    [SoundEvent.CoreShardRelease]      = [new(SfxUpgradeDone, Volume: 2.0f, Pitch: 0.3f)],
    [SoundEvent.CoreShardClick]        = [new(SfxGemClick, Pitch: 0.4f)],
    [SoundEvent.CoreShardCollect]      = [new(SfxUpgradeDone, Volume: 2.0f)],

    // Prestige & Time Loop
    [SoundEvent.PrestigePointEarned]     = [new(SfxUpgradeDone, Volume: 2.0f, Pitch: 0.5f)],
    [SoundEvent.PrestigePointLanded]     = [new(SfxGemClick, Pitch: 0.5f)],
    [SoundEvent.CoreExtractionStart]     = [new(SfxImpact)],
    [SoundEvent.TimeLoopCollapseBuildup] = [new(SfxImpact)],
    [SoundEvent.TimeLoopCollapseImplode] = [new(SfxImpact)],
    [SoundEvent.TimeLoopPlanetSwallowed] = [new(SfxImpact)],
    [SoundEvent.TimeLoopRestart]         = [new(SfxImpact)],
  };

  // Refuel loop tuning
  private const int MaxRefuelVoices = 4;
  private const float RefuelFadeSeconds = 0.06f;
  private const float RefuelPitchMin = -0.15f;
  private const float RefuelPitchMax = 0.25f;

  // ============================================================================

  private static AudioManager _instance;
  public static AudioManager Instance => _instance ??= new AudioManager();

  private bool m_initialized = false;
  private bool m_disableSound = false;
  private Settings m_settings;

  private readonly HighPerfAudioManager m_soundManager = new();
  private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, Song> _songs = new(StringComparer.Ordinal);
  private readonly Dictionary<SoundEvent, SoundCue[]> _eventCues = new(EventCues);

  // Every sound that actually plays; capture sessions log these to rebuild the game audio offline.
  public static event Action<SoundEffect, float, float, float> SoundPlayed;
  public static readonly List<string> LoadedSoundNames = new();

  internal static void NotifyPlayed(SoundEffect effect, float volume, float pitch, float pan)
    => SoundPlayed?.Invoke(effect, volume, pitch, pan);

  private static SoundEffect Named(SoundEffect effect, string assetPath)
  {
    if (effect == null) return null;
    effect.Name = assetPath;
    LoadedSoundNames.Add(assetPath);
    return effect;
  }

  private SoundEffect ResolveSound(string path)
    => !string.IsNullOrEmpty(path) && _sounds.TryGetValue(path, out var effect) ? effect : null;

  private sealed class RefuelVoice
  {
    public SoundEffectInstance Sound;
    public bool Updated;
    public float Volume;
    public float PitchVariation;
  }

  private readonly Dictionary<Harvester, RefuelVoice> _refuelVoices = new();
  private readonly List<Harvester> _finishedRefuelVoices = new();

  public void UpdateRefuelSound(Harvester harvester)
  {
    var cue = _eventCues[SoundEvent.RefuelLoop][0];
    var loopEffect = ResolveSound(cue.Path);
    if (m_disableSound || loopEffect == null || harvester.MarkedForDestroy)
      return;

    if (!_refuelVoices.TryGetValue(harvester, out var voice))
    {
      if (_refuelVoices.Count >= MaxRefuelVoices) return;
      var sound = loopEffect.CreateInstance();
      sound.IsLooped = true;
      sound.Volume = 0f;
      try { sound.Play(); }
      catch (InstancePlayLimitException) { sound.Dispose(); return; }
      voice = new RefuelVoice { Sound = sound, PitchVariation = RandomHelper.Float(-1f, 1f) };
      _refuelVoices.Add(harvester, voice);
    }

    voice.Updated = true;
    voice.Sound.Pitch = MathHelper.Clamp(cue.Pitch + voice.PitchVariation * cue.PitchJitter
      + MathHelper.Lerp(RefuelPitchMin, RefuelPitchMax,
        MathHelper.Clamp((float)harvester.refuelProgressPercent / 100f, 0f, 1f)), -1f, 1f);
    voice.Sound.Pan = cue.Pan;
  }

  // Called once per screen frame; voices must be refreshed by active harvesters.
  public void UpdateRefuelSounds(GameTime gameTime, bool suspended)
  {
    if (suspended) { StopRefuelSounds(); return; }
    _finishedRefuelVoices.Clear();
    float fadeStep = (float)gameTime.ElapsedGameTime.TotalSeconds / RefuelFadeSeconds;
    foreach (var pair in _refuelVoices)
    {
      var voice = pair.Value;
      bool active = voice.Updated && !pair.Key.MarkedForDestroy
        && pair.Key.CurrentState == Harvester.HarvesterState.Refueling;
      voice.Volume = MathHelper.Clamp(voice.Volume + (active ? fadeStep : -fadeStep), 0f, 1f);
      voice.Sound.Volume = MathHelper.Clamp(voice.Volume * _eventCues[SoundEvent.RefuelLoop][0].Volume * (m_settings?.SfxVolume ?? 0f), 0f, 1f);
      voice.Updated = false;
      if (!active && voice.Volume <= 0f)
      {
        voice.Sound.Stop();
        voice.Sound.Dispose();
        _finishedRefuelVoices.Add(pair.Key);
      }
    }
    foreach (var harvester in _finishedRefuelVoices) _refuelVoices.Remove(harvester);
  }

  public void StopRefuelSounds()
  {
    foreach (var voice in _refuelVoices.Values)
    {
      voice.Sound.Stop();
      voice.Sound.Dispose();
    }
    _refuelVoices.Clear();
    _finishedRefuelVoices.Clear();
  }

  public enum SoundType
  {
    Default = 0,
    GemCollect = 1,
    UI = 3
  }

  private AudioManager()
  {
    m_soundManager.ConfigureSoundType(
      soundTypeId: (int)SoundType.GemCollect,
      maxPerFrame: 1,
      pitchStep: 0.04f,
      maxPitch: 0.5f
    );
  }

  public void SetSettings(Settings settings)
  {
    m_settings = settings;
  }

  public void PlaySong(string songName)
  {
    if (_songs.TryGetValue(songName, out Song value))
    {
      if (value == null)
      {
        Log.Error($"Song '{songName}' not found in AudioManager.");
        return;
      }

      MediaPlayer.Play(value);
    }
  }

  // 0 to 1 over musicFadeSeconds, scaling the music volume setting (see Update).
  private float musicFade = 1f;
  private float musicFadeSeconds;

  // Starts a song silent and swells it to the music volume setting.
  public void FadeInSong(string songName, float seconds)
  {
    musicFade = 0f;
    musicFadeSeconds = seconds;
    ApplyMusicVolume();
    PlaySong(songName);
  }

  private void ApplyMusicVolume() => MediaPlayer.Volume = (m_settings?.MusicVolume ?? 0f) * musicFade;

  public void LoadContent(ContentManager content)
  {
    if (m_initialized)
      return;

    m_initialized = true;

    if (m_disableSound)
      return;

    foreach (var name in SongNames)
    {
      if (name != "Greys")
      {
        GameplayPreloader.Queue<Song>($"Music/Holizna/{name}", asset => _songs[name] = asset.Value);
        continue;
      }
      _songs[name] = AssetManager.Load<Song>($"Music/Holizna/{name}");
    }

    LoadSoundDefaults();

    var immediatePaths = new HashSet<string>(StringComparer.Ordinal);
    var preloadPaths = new Dictionary<string, bool>(StringComparer.Ordinal);

    void RegisterPath(string path, bool preload, bool optional)
    {
      if (string.IsNullOrEmpty(path) || _sounds.ContainsKey(path)) return;
      if (!preload)
      {
        immediatePaths.Add(path);
        preloadPaths.Remove(path);
      }
      else if (!immediatePaths.Contains(path))
      {
        preloadPaths[path] = preloadPaths.TryGetValue(path, out bool prevOptional) ? (prevOptional && optional) : optional;
      }
    }

    foreach (var cues in _eventCues.Values)
    {
      foreach (var cue in cues)
      {
        RegisterPath(cue.Path, cue.Preload, cue.Optional);
        RegisterPath(cue.FallbackPath, cue.Preload, false);
      }
    }

    foreach (var path in immediatePaths)
    {
      _sounds[path] = Named(AssetManager.Load<SoundEffect>(path), path);
    }

    foreach (var (path, optional) in preloadPaths)
    {
      GameplayPreloader.Queue<SoundEffect>(path, asset => _sounds[path] = Named(asset.Value, path), optional: optional);
    }
  }

  public void SfxVolumeUpdated()
  {
    PlaySound(SoundEvent.MenuClick);
  }

  public void MusicVolumeUpdated()
  {
    Log.Information($"Music volume updated to {m_settings.MusicVolume}");
    ApplyMusicVolume();
  }

  public void Update(GameTime gameTime, bool playNextSong)
  {
    if (musicFade < 1f)
    {
      musicFade = Math.Min(1f, musicFade + (float)gameTime.ElapsedGameTime.TotalSeconds / musicFadeSeconds);
      ApplyMusicVolume();
    }
#if KNI_WEB
    if (!UntitledGemGame.Web.WebBootstrap.AudioActivated) return;
#endif
    if (playNextSong && MediaPlayer.State == MediaState.Stopped && _songs.Count > 0)
    {
      var songNames = new List<string>(_songs.Keys);
      var nextSongName = songNames[Random.Shared.Next(songNames.Count)];
      var song = _songs[nextSongName];

      if (song == null)
      {
        Log.Error($"Song '{nextSongName}' is null in AudioManager.");
        return;
      }

      MediaPlayer.Play(song);
    }

    m_soundManager.ProcessQueue(gameTime);
  }

  public void PlaySound(SoundEvent soundEvent, float pitchOffset = 0f, float pan = 0f, float volumeScale = 1f)
  {
    if (m_disableSound || !_eventCues.TryGetValue(soundEvent, out var cues))
      return;

    foreach (var cue in cues)
    {
      var effect = ResolveSound(cue.Path);
      float cueVolume = cue.Volume;
      if (effect == null && !string.IsNullOrEmpty(cue.FallbackPath))
      {
        effect = ResolveSound(cue.FallbackPath);
        cueVolume = cue.FallbackVolume;
      }
      if (effect == null)
        continue;

      float pitch = cue.Pitch + pitchOffset;
      if (cue.PitchJitter > 0f)
        pitch += RandomHelper.Float(-cue.PitchJitter, cue.PitchJitter);

      PlaySound(effect, cue.Type, pitch, MathHelper.Clamp(pan + cue.Pan, -1f, 1f), cue.Priority, cueVolume * volumeScale);
    }
  }

  public void PlaySound(SoundEffect soundEffect, SoundType type = SoundType.Default, float pitch = 0f, float pan = 0f, float priority = 1f, float volume = 1f)
  {
    if (m_disableSound || soundEffect == null)
      return;

    float sfxVolume = (m_settings?.SfxVolume ?? 0f) * volume;

    if (type == SoundType.GemCollect)
    {
      m_soundManager.PlayGemCollect(soundEffect, GameMain.Time, sfxVolume, pitch, pan);
    }
    else
    {
      m_soundManager.RequestSound(soundEffect, (int)type, sfxVolume, pitch, pan, priority);
    }
  }
}

public class HighPerfAudioManager
{
  private const int MaxPendingRequests = 64;
  private const int MaxSoundTypes = 32;

  public struct SoundRequest
  {
    public SoundEffect Effect;
    public int SoundTypeId;    // Enum or ID representing the sound type
    public float Volume;
    public float Pitch;
    public float Pan;
    public float Priority;
  }

  // Configurable limits per sound type
  public struct SoundProfile
  {
    public int MaxSimultaneousPerFrame; // Max allowed instances in a single tick
    public float PitchStep;             // Pitch increase for rapid consecutive triggers
    public float MaxPitch;              // Cap on pitch escalation (+1.0f is max in MonoGame)

    // Runtime tracking (reset each frame)
    internal int FrameCount;
    internal float CurrentPitchOffset;
  }

  private readonly SoundRequest[] _requestBuffer = new SoundRequest[MaxPendingRequests];
  private int _requestCount = 0;
  private static readonly IComparer<SoundRequest> PriorityComparer =
    Comparer<SoundRequest>.Create((a, b) => b.Priority.CompareTo(a.Priority));

  // Runtime sound replacement must not leave queued requests pointing at a disposed effect.
  public void RemovePending(SoundEffect effect)
  {
    int remaining = 0;
    for (int i = 0; i < _requestCount; i++)
      if (!ReferenceEquals(_requestBuffer[i].Effect, effect))
        _requestBuffer[remaining++] = _requestBuffer[i];
    Array.Clear(_requestBuffer, remaining, _requestCount - remaining);
    _requestCount = remaining;
  }

  // Profiles indexed by SoundTypeId
  private readonly SoundProfile[] _profiles = new SoundProfile[MaxSoundTypes];

  public HighPerfAudioManager()
  {
    // Default fallbacks for unconfigured sound types
    for (int i = 0; i < MaxSoundTypes; i++)
    {
      _profiles[i] = new SoundProfile
      {
        MaxSimultaneousPerFrame = 4, // Default cap
        PitchStep = 0.0f,
        MaxPitch = 1.0f
      };
    }
  }

  /// <summary>
  /// Register throttling rules for a specific sound category.
  /// </summary>
  public void ConfigureSoundType(int soundTypeId, int maxPerFrame, float pitchStep = 0.05f, float maxPitch = 0.6f)
  {
    if (soundTypeId < 0 || soundTypeId >= MaxSoundTypes) return;

    _profiles[soundTypeId] = new SoundProfile
    {
      MaxSimultaneousPerFrame = maxPerFrame,
      PitchStep = pitchStep,
      MaxPitch = maxPitch,
      FrameCount = 0,
      CurrentPitchOffset = 0f
    };
  }

  /// <summary>
  /// Queue a sound request from ECS systems.
  /// </summary>
  public void RequestSound(SoundEffect effect, int soundTypeId, float volume = 1f, float basePitch = 0f, float pan = 0f, float priority = 1f)
  {
    if (effect == null || _requestCount >= MaxPendingRequests) return;
    if (soundTypeId < 0 || soundTypeId >= MaxSoundTypes) soundTypeId = 0;

    ref var profile = ref _profiles[soundTypeId];

    // 1. Cap maximum instances allowed on this frame
    if (profile.FrameCount >= profile.MaxSimultaneousPerFrame)
    {
      return; // Throttle excessive sounds gracefully
    }

    profile.FrameCount++;

    // 2. Apply pitch escalation if configured
    float finalPitch = MathHelper.Clamp(basePitch + profile.CurrentPitchOffset, -1f, 1f);
    profile.CurrentPitchOffset = Math.Min(profile.CurrentPitchOffset + profile.PitchStep, profile.MaxPitch);

    // 3. Store request
    _requestBuffer[_requestCount++] = new SoundRequest
    {
      Effect = effect,
      SoundTypeId = soundTypeId,
      Volume = volume,
      Pitch = finalPitch,
      Pan = pan,
      Priority = priority
    };
  }

  /// <summary>
  /// Process queued sound requests and decay pitch escalation.
  /// </summary>
  public void ProcessQueue(GameTime gameTime)
  {
    float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

    // Give higher-priority effects the first available voices if the device is full.
    if (_requestCount > 1) Array.Sort(_requestBuffer, 0, _requestCount, PriorityComparer);

    // 1. Play queued sounds
    for (int i = 0; i < _requestCount; i++)
    {
      ref var req = ref _requestBuffer[i];
      try
      {
        // Some sounds play at 2x the SFX setting; MonoGame throws for volumes outside 0..1 (crash at 100% SFX).
        float volume = MathHelper.Clamp(req.Volume, 0f, 1f), pitch = MathHelper.Clamp(req.Pitch, -1f, 1f);
        req.Effect.Play(volume, pitch, MathHelper.Clamp(req.Pan, -1f, 1f));
        AudioManager.NotifyPlayed(req.Effect, volume, pitch, req.Pan);
      }
      catch (InstancePlayLimitException)
      {
        break; // Device full; safely abandon remainder
      }
    }

    // 2. Reset frame limits & slowly decay pitch escalation for next frame
    for (int i = 0; i < MaxSoundTypes; i++)
    {
      ref var profile = ref _profiles[i];
      profile.FrameCount = 0;

      // Decay pitch back to baseline over time (e.g., resets after ~0.3 seconds of silence)
      if (profile.CurrentPitchOffset > 0)
      {
        profile.CurrentPitchOffset = Math.Max(0f, profile.CurrentPitchOffset - (deltaSeconds * 2.0f));
      }
    }

    _requestCount = 0;
  }

  // Max 16 gem sounds per second, pitch steps by +0.04 up to +0.5
  private readonly SoundThrottleGroup _gemThrottle = new SoundThrottleGroup(
      maxSoundsPerSecond: 16f,
      pitchStep: 0.04f,
      maxPitch: 0.5f,
      pitchResetWindow: 0.2f
  );

  public void PlayGemCollect(SoundEffect effect, GameTime gameTime, float baseVolume = 0.6f, float basePitch = 0f, float pan = 0f)
  {
    if (effect == null) return;

    double currentTime = gameTime.TotalGameTime.TotalSeconds;
    float finalVolume = baseVolume;
    float finalPitch = basePitch;

    if (_gemThrottle.TryProcessRequest(currentTime, ref finalVolume, ref finalPitch))
    {
      try
      {
        finalVolume = MathHelper.Clamp(finalVolume, 0f, 1f);
        finalPitch = MathHelper.Clamp(finalPitch, -1f, 1f);
        pan = MathHelper.Clamp(pan, -1f, 1f);
        effect.Play(finalVolume, finalPitch, pan);
        AudioManager.NotifyPlayed(effect, finalVolume, finalPitch, pan);
      }
      catch (InstancePlayLimitException)
      {
        // Safe fallthrough if driver reaches absolute hardware cap
      }
    }
  }
}

public class SoundThrottleGroup
{
  private readonly float _minIntervalSeconds; // E.g., 0.05f = max 20 sounds per second
  private readonly float _pitchStep;          // How much pitch increases per rapid collect
  private readonly float _maxPitch;           // Pitch ceiling
  private readonly float _pitchResetWindow;   // Time without pickups before pitch resets

  private double _lastPlayTime;
  private double _lastRequestTime;
  private float _currentPitchOffset;

  public SoundThrottleGroup(float maxSoundsPerSecond = 18f, float pitchStep = 0.03f, float maxPitch = 0.5f, float pitchResetWindow = 0.25f)
  {
    _minIntervalSeconds = 1.0f / maxSoundsPerSecond;
    _pitchStep = pitchStep;
    _maxPitch = maxPitch;
    _pitchResetWindow = pitchResetWindow;
  }

  /// <summary>
  /// Attempts to process a sound request. Returns true if sound should play.
  /// </summary>
  public bool TryProcessRequest(double currentTimeSeconds, ref float volume, ref float pitch)
  {
    // 1. Reset pitch streak if player stopped collecting for a short moment
    if (currentTimeSeconds - _lastRequestTime > _pitchResetWindow)
    {
      _currentPitchOffset = 0f;
    }

    _lastRequestTime = currentTimeSeconds;

    // 2. Time-Based Cooldown Check (Enforces maximum playback rate across frames)
    if (currentTimeSeconds - _lastPlayTime < _minIntervalSeconds)
    {
      return false; // Drop sound to keep mix clean
    }

    // 3. Apply Pitch Escalation
    pitch = MathHelper.Clamp(pitch + _currentPitchOffset, -1.0f, 1.0f);
    _currentPitchOffset = Math.Min(_currentPitchOffset + _pitchStep, _maxPitch);

    // 4. Slightly dampen volume during rapid bursts to prevent clipping
    if (_currentPitchOffset > 0.1f)
    {
      volume *= 0.85f;
    }

    _lastPlayTime = currentTimeSeconds;
    return true;
  }
}
