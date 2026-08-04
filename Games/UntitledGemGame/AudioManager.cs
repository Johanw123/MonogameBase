
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UntitledGemGame;

public class AudioManager
{
  private static AudioManager _instance;
  public static AudioManager Instance
  {
    get
    {
      if (_instance == null)
      {
        _instance = new AudioManager();
      }
      return _instance;
    }
  }

  private bool m_initialized = false;

  public SoundEffect MenuHoverButtonSoundEffect;
  public SoundEffect MenuClickButtonSoundEffect;

  public SoundEffect ShipEngineDyingSoundEffect;

  public SoundEffect GemPickupSoundEffect;

  public SoundEffect ImpactSoundEffect;
  public SoundEffect BlipSoundEffect;

  private bool m_disableSound = false;
  private HighPerfAudioManager m_soundManager = new();

  private Settings m_settings;

  // public float MusicVolume = 0.3f;
  // public float SFXVolume = 0.5f;

  private Dictionary<string, Song> _songs = new Dictionary<string, Song>();

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
      maxPerFrame: 1,         // Maximum 3 gem sounds per tick, no matter how many are collected
      pitchStep: 0.04f,       // Each rapid collect raises pitch slightly
      maxPitch: 0.5f          // Upper limit on pitch multiplier
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

  public void LoadContent(ContentManager content)
  {
    if (m_initialized)
      return;

    m_initialized = true;

    if (m_disableSound)
      return;

    string[] songNames = { "Greys", "Hopkinsville Goblins", "Pleiadeans", "Sky Fish" };

    foreach (var name in songNames)
    {
      // var song = Song.FromUri(name, new System.Uri($"Music/Holizna/{name}.ogg", System.UriKind.RelativeOrAbsolute));
      var song = AssetManager.Load<Song>($"Music/Holizna/{name}");
      _songs[name] = song;
    }

    bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    if (isLinux)
    {
      MenuHoverButtonSoundEffect = AssetManager.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist7.wav");
      MenuClickButtonSoundEffect = AssetManager.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist10.wav");

      ShipEngineDyingSoundEffect = AssetManager.Load<SoundEffect>("SFX/Ship.wav");

      GemPickupSoundEffect = AssetManager.Load<SoundEffect>("SFX/gem.wav");

      ImpactSoundEffect = AssetManager.Load<SoundEffect>("SFX/Impact_test.wav");
      BlipSoundEffect = AssetManager.Load<SoundEffect>("SFX/blip.wav");
    }
    else
    {
      MenuHoverButtonSoundEffect = content.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist7");
      MenuClickButtonSoundEffect = content.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist10");

      ShipEngineDyingSoundEffect = content.Load<SoundEffect>("SFX/Ship");

      GemPickupSoundEffect = content.Load<SoundEffect>("SFX/gem");

      ImpactSoundEffect = content.Load<SoundEffect>("SFX/Impact_test");
      BlipSoundEffect = content.Load<SoundEffect>("SFX/blip");
    }
  }

  public void SfxVolumeUpdated()
  {
    PlaySound(MenuClickButtonSoundEffect);
  }

  public void MusicVolumeUpdated()
  {
    Log.Information($"Music volume updated to {m_settings.MusicVolume}");
    MediaPlayer.Volume = m_settings.MusicVolume;
  }

  public void Update(GameTime gameTime, bool playNextSong)
  {
#if KNI_WEB
    return;
#endif
    if (playNextSong && MediaPlayer.State == MediaState.Stopped && _songs.Count > 0)
    {
      var random = new System.Random();
      var songNames = new List<string>(_songs.Keys);
      var nextSongName = songNames[random.Next(songNames.Count)];
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

  // public void PlaySound(SoundEffect soundEffect, float pitch = 0f, float pan = 0f)
  // {
  //   if (m_disableSound)
  //     return;
  //
  //   if (soundEffect == null)
  //   {
  //     Log.Error("Attempted to play a null SoundEffect.");
  //     return;
  //   }
  //
  //   soundEffect.Play(m_settings.SfxVolume, pitch, pan);
  // }

  private const int MaxSimultaneousSounds = 16;
  private readonly List<SoundEffectInstance> _activeInstances = new();

  public void PlaySound(SoundEffect soundEffect, SoundType type = SoundType.Default, float pitch = 0f, float pan = 0f, float priority = 1f)
  {
    if (m_disableSound)
      return;
    if (soundEffect == null) return;

    if (type == SoundType.GemCollect)
    {
      m_soundManager.PlayGemCollect(soundEffect, GameMain.Time, m_settings.SfxVolume, pitch);
    }
    else
    {
      m_soundManager.RequestSound(soundEffect, (int)type, m_settings.SfxVolume, pitch, pan, priority);
    }

    //
    // // Clean up stopped instances to free active count
    // _activeInstances.RemoveAll(inst => inst.State == SoundState.Stopped);
    //
    // // Cap concurrent sound instances to stay well under OpenAL's limit
    // if (_activeInstances.Count >= MaxSimultaneousSounds)
    // {
    //   // Option A: Silently drop the sound if audio channels are saturated
    //   return;
    // }
    //
    // try
    // {
    //   SoundEffectInstance instance = soundEffect.CreateInstance();
    //   instance.Pitch = pitch;
    //   instance.Pan = pan;
    //   instance.Volume = m_settings.SfxVolume;
    //   instance.Play();
    //
    //   _activeInstances.Add(instance);
    // }
    // catch (InstancePlayLimitException)
    // {
    //   // Safety catch to keep the game from crashing if OpenAL refuses allocation
    // }
  }

  public void StopSound(string soundName)
  {
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

    // 1. Play queued sounds
    for (int i = 0; i < _requestCount; i++)
    {
      ref var req = ref _requestBuffer[i];
      try
      {
        req.Effect.Play(req.Volume, req.Pitch, req.Pan);
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

  public void PlayGemCollect(SoundEffect effect, GameTime gameTime, float baseVolume = 0.6f, float basePitch = 0f)
  {
    if (effect == null) return;

    double currentTime = gameTime.TotalGameTime.TotalSeconds;
    float finalVolume = baseVolume;
    float finalPitch = basePitch;

    if (_gemThrottle.TryProcessRequest(currentTime, ref finalVolume, ref finalPitch))
    {
      try
      {
        effect.Play(finalVolume, finalPitch, 0f);
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
