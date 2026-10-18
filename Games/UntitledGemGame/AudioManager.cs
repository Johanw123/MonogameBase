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
  private const string SfxRefuelLoop      = "SFX/Refuel/loop";

  // Curated, level-balanced stock recordings. Source paths and processing: Content/SFX/Shapeform/SOURCES.md.
  private const string SfxWindowOpen = "SFX/Shapeform/window-open.wav";
  private const string SfxWindowClose = "SFX/Shapeform/window-close.wav";
  private const string SfxError = "SFX/Shapeform/error.wav";
  private const string SfxRefund = "SFX/Shapeform/refund.wav";
  private const string SfxScan = "SFX/Shapeform/scan.wav";
  private const string SfxConfirm = "SFX/Shapeform/confirm.wav";
  private const string SfxEquip = "SFX/Shapeform/equip.wav";
  private const string SfxUnequip = "SFX/Shapeform/unequip.wav";
  private const string SfxCommand = "SFX/Shapeform/command.wav";
  private const string SfxMerge = "SFX/Shapeform/merge.wav";
  private const string SfxCargo = "SFX/Shapeform/cargo.wav";
  private const string SfxJackpot = "SFX/Shapeform/jackpot.wav";
  private const string SfxWarp = "SFX/Shapeform/warp.wav";
  private const string SfxSpeedboost = "SFX/Shapeform/speedboost.wav";
  private const string SfxMagnet = "SFX/Shapeform/magnet.wav";
  private const string SfxElectric = "SFX/Shapeform/electric.wav";
  private const string SfxServoRise = "SFX/Shapeform/servo-rise.wav";
  private const string SfxMaterialize = "SFX/Shapeform/materialize.wav";
  private const string SfxDrillLaunch = "SFX/Shapeform/drill-launch.wav";
  private const string SfxHeavyImpact = "SFX/Shapeform/heavy-impact.wav";
  private const string SfxDebris = "SFX/Shapeform/debris.wav";
  private const string SfxCoreTap = "SFX/Shapeform/core-tap.wav";
  private const string SfxWingLaunch = "SFX/Shapeform/wing-launch.wav";
  private const string SfxCannon = "SFX/Shapeform/cannon.wav";
  private const string SfxRocket = "SFX/Shapeform/rocket.wav";
  private const string SfxCluster = "SFX/Shapeform/cluster.wav";
  private const string SfxSurge = "SFX/Shapeform/surge.wav";
  private const string SfxCharge = "SFX/Shapeform/charge.wav";
  private const string SfxRailgun = "SFX/Shapeform/railgun.wav";
  private const string SfxServoFall = "SFX/Shapeform/servo-fall.wav";
  private const string SfxCannonImpact = "SFX/Shapeform/impact.wav";
  private const string SfxRicochet = "SFX/Shapeform/ricochet.wav";
  private const string SfxExplosion = "SFX/Shapeform/explosion.wav";
  private const string SfxEnergyRelease = "SFX/Shapeform/energy-release.wav";

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
    [SoundEvent.TooltipShow]           = [new(SfxTooltipShow, Volume: 0.5f)],
    [SoundEvent.WindowOpen]            = [new(SfxWindowOpen, Volume: 0.7f, Preload: false)],
    [SoundEvent.WindowClose]           = [new(SfxWindowClose, Volume: 0.6f, Preload: false)],
    [SoundEvent.UpgradeStart]          = [new(SfxUpgradeStart, Volume: 0.8f)],
    [SoundEvent.UpgradeComplete]       = [new(SfxUpgradeDone, PitchJitter: 0.08f, Volume: 0.6f)],
    [SoundEvent.UpgradeError]          = [new(SfxError, Volume: 0.65f)],
    [SoundEvent.UpgradeRefund]         = [new(SfxRefund, Volume: 0.75f)],
    [SoundEvent.ManualAbilityActivate] = [new(SfxCommand, Volume: 0.85f)],
    [SoundEvent.SignalScan]            = [new(SfxScan, Volume: 0.75f)],
    [SoundEvent.SignalChoose]          = [new(SfxConfirm, Volume: 0.9f)],

    // Gems & Cursor
    [SoundEvent.GemPickup]             = [new(SfxGemPickup, PitchJitter: 0.2f, Priority: 0.5f, Type: SoundType.GemCollect)],
    [SoundEvent.GemClick]              = [new(SfxGemClick, PitchJitter: 0.15f)],
    [SoundEvent.GemMerge]              = [new(SfxMerge, Volume: 0.28f, PitchJitter: 0.06f)],
    [SoundEvent.CursorGravityCollapse] = [new(SfxGemClick)],
    [SoundEvent.CursorGravityReady]    = [new(SfxBlip, Pitch: 0.25f)],

    // Harvesters & Refueling
    [SoundEvent.RefuelRequest]         = [new(SfxBlip)],
    [SoundEvent.RefuelStart]           = [new(SfxCharge, Volume: 0.7f)],
    [SoundEvent.RefuelLoop]            = [new(SfxRefuelLoop, Volume: 0.25f, Optional: true)],
    [SoundEvent.RefuelComplete]        = [new(SfxCargo, Volume: 0.3f, Pitch: 0.15f)],
    [SoundEvent.HarvesterDeliverCargo] = [new(SfxCargo, Volume: 0.25f, PitchJitter: 0.08f)],
    [SoundEvent.HarvesterWarpDrive]    = [new(SfxWarp, Volume: 0.25f, PitchJitter: 0.06f)],
    [SoundEvent.JackpotHaul]           = [new(SfxJackpot, Volume: 0.55f)],

    // Shipyard & Module Salvage
    [SoundEvent.ModuleSalvageReady]    = [new(SfxBlip)],
    [SoundEvent.ModuleRevealStart]     = [new(SfxUpgradeStart, Volume: 0.8f)],
    [SoundEvent.ModuleRevealCommon]    = [new(SfxConfirm, Volume: 0.9f)],
    [SoundEvent.ModuleRevealEpic]      = [new(SfxJackpot, Volume: 0.85f, Pitch: 0.15f)],
    [SoundEvent.ModuleRevealLegendary] = [new(SfxJackpot, Volume: 1f, Pitch: -0.15f), new(SfxHeavyImpact, Volume: 0.4f)],
    [SoundEvent.ModuleEquip]           = [new(SfxEquip, Volume: 0.9f)],
    [SoundEvent.ModuleUnequip]         = [new(SfxUnequip, Volume: 0.7f)],

    // Ship & Intro
    [SoundEvent.ShipEngineDying]       = [new(SfxShipEngineDying)],
    [SoundEvent.ShipCrashLand]         = [new(SfxImpact)],

    // Ship Systems & Abilities
    [SoundEvent.AbilitySpeedboostActivate]     = [new(SfxSpeedboost, Volume: 0.45f)],
    [SoundEvent.AbilityMagnetActivate]         = [new(SfxMagnet, Volume: 0.4f)],
    [SoundEvent.AbilityChainLightningActivate] = [new(SfxElectric, Volume: 0.35f)],
    [SoundEvent.AbilityDroneSwarmActivate]     = [new(SfxServoRise, Volume: 0.4f)],
    [SoundEvent.AbilityGemSpawnerActivate]     = [new(SfxMaterialize, Volume: 0.35f)],
    [SoundEvent.CoreDrillLaunch]               = [new(SfxDrillLaunch, Volume: 0.4f)],
    [SoundEvent.CoreDrillImpact]               = [new(SfxHeavyImpact, Volume: 0.4f)],
    [SoundEvent.CoreDrillRupture]              = [new(SfxDebris, Volume: 0.35f)],
    [SoundEvent.CoreDrillCoreTap]              = [new(SfxCoreTap, Volume: 0.3f, PitchJitter: 0.06f)],
    [SoundEvent.KamikazeWingLaunch]            = [new(SfxWingLaunch, Volume: 0.4f)],

    // Weapons — Firing & Launching
    [SoundEvent.CannonFireManual]   = [new(SfxCannon, Volume: 0.9f, Pitch: -0.1f, PitchJitter: 0.04f, Priority: 2f)],
    [SoundEvent.CannonFireAuto]     = [new(SfxCannon, Volume: 0.35f, PitchJitter: 0.06f, Priority: 0.4f)],
    [SoundEvent.RocketLaunch]       = [new(SfxRocket, Volume: 0.45f, PitchJitter: 0.08f, Priority: 0.6f)],
    [SoundEvent.RocketClusterSplit] = [new(SfxCluster, Volume: 0.25f, PitchJitter: 0.1f, Priority: 0.3f)],
    [SoundEvent.LaserOverheatSurge] = [new(SfxSurge, Volume: 0.3f)],
    [SoundEvent.RailgunWindUp]      = [new(SfxCharge, Volume: 0.45f, Pitch: -0.15f)],
    [SoundEvent.RailgunFire]        = [new(SfxRailgun, Volume: 0.55f, Priority: 1.2f)],
    [SoundEvent.ArcHarpoonLaunch]   = [new(SfxEquip, Volume: 0.5f, Pitch: -0.2f)],
    [SoundEvent.ArcHarpoonPulse]    = [new(SfxElectric, Volume: 0.22f, PitchJitter: 0.06f, Priority: 0.3f)],
    [SoundEvent.ArcHarpoonRelease]  = [new(SfxServoFall, Volume: 0.3f)],

    // Weapons — Planet Impacts & Combos
    [SoundEvent.CannonImpact]           = [new(SfxCannonImpact, Volume: 0.4f, PitchJitter: 0.08f, Priority: 0.4f)],
    [SoundEvent.CannonCriticalImpact]   = [new(SfxHeavyImpact, Volume: 0.5f, PitchJitter: 0.06f, Priority: 0.7f)],
    [SoundEvent.CannonRicochet]         = [new(SfxRicochet, Volume: 0.25f, PitchJitter: 0.1f, Priority: 0.3f)],
    [SoundEvent.RocketImpact]           = [new(SfxExplosion, Volume: 0.5f, PitchJitter: 0.08f, Priority: 0.6f)],
    [SoundEvent.RocketMiniImpact]       = [new(SfxExplosion, Volume: 0.22f, Pitch: 0.3f, PitchJitter: 0.08f, Priority: 0.3f)],
    [SoundEvent.ArcHarpoonImpact]       = [new(SfxCoreTap, Volume: 0.35f)],
    [SoundEvent.RailgunImpact]          = [new(SfxImpact)],
    [SoundEvent.PlanetCrackerStart]     = [new(SfxImpact)],
    [SoundEvent.KamikazeImpact]         = [new(SfxImpact, Pitch: 0.3f, Priority: 0.5f)],
    [SoundEvent.KamikazeBombletImpact]  = [new(SfxImpact, Pitch: 0.6f, Priority: 0.3f)],
    [SoundEvent.KamikazeDoomsdayImpact] = [new(SfxImpact, Pitch: -0.6f, Priority: 0.5f)],
    [SoundEvent.MagmaDetonation]        = [new(SfxExplosion, Volume: 0.45f, Pitch: -0.15f)],
    [SoundEvent.PlanetaryOverloadErupt] = [new(SfxEnergyRelease, Volume: 0.4f, Priority: 1.2f)],
    [SoundEvent.WeakPointBurst]         = [new(SfxDebris, Volume: 0.35f, Pitch: 0.2f)],
    [SoundEvent.WeaponJackpot]          = [new(SfxJackpot, Volume: 0.5f, Pitch: 0.15f)],

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
  private const int MaxRefuelVoices = 2;
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
  private long _soundTrigger;

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
      // A larger fleet must not multiply the loudness of its charging hum.
      voice.Sound.Volume = MathHelper.Clamp(voice.Volume * _eventCues[SoundEvent.RefuelLoop][0].Volume
        * (m_settings?.SfxVolume ?? 0f) * m_soundManager.BackgroundGain / MathF.Sqrt(Math.Max(1, _refuelVoices.Count)), 0f, 1f);
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

  public void StopGameplaySounds()
  {
    StopRefuelSounds();
    m_soundManager.StopAll();
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
    m_soundManager.MasterVolume = m_settings?.SfxVolume ?? 0f;
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

  public void PlaySound(SoundEvent soundEvent, float pitchOffset = 0f, float pan = 0f, float volumeScale = 1f, bool playerInitiated = false)
  {
    if (m_disableSound || !_eventCues.TryGetValue(soundEvent, out var cues))
      return;

    long trigger = ++_soundTrigger;
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

      PlaySound(effect, cue.Type, pitch, MathHelper.Clamp(pan + cue.Pan, -1f, 1f), cue.Priority, cueVolume * volumeScale, soundEvent, playerInitiated, trigger);
    }
  }

  public void PlaySound(SoundEffect soundEffect, SoundType type = SoundType.Default, float pitch = 0f, float pan = 0f, float priority = 1f, float volume = 1f,
    SoundEvent? soundEvent = null, bool playerInitiated = false, long trigger = 0)
  {
    if (m_disableSound || soundEffect == null)
      return;

    m_soundManager.MasterVolume = m_settings?.SfxVolume ?? 0f;
    if (m_soundManager.MasterVolume <= 0f) return;
    m_soundManager.RequestSound(soundEffect, (int)type, volume, pitch, pan, priority, soundEvent, playerInitiated, trigger);
  }
}
