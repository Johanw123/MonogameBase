
using System.Collections.Generic;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Serilog;

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

  private Settings m_settings;

  // public float MusicVolume = 0.3f;
  // public float SFXVolume = 0.5f;

  private Dictionary<string, Song> _songs = new Dictionary<string, Song>();

  private AudioManager()
  {
  }

  public void SetSettings(Settings settings)
  {
    m_settings = settings;
  }

  public void PlaySong(string songName)
  {
    if (_songs.TryGetValue(songName, out Song value))
    {
      MediaPlayer.Play(value);
    }
  }

  public void LoadContent()
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

    MenuHoverButtonSoundEffect = AssetManager.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist7.wav");
    MenuClickButtonSoundEffect = AssetManager.Load<SoundEffect>("SFX/Menu/Soundpack/Minimalist10.wav");

    ShipEngineDyingSoundEffect = AssetManager.Load<SoundEffect>("SFX/Ship.wav");

    GemPickupSoundEffect = AssetManager.Load<SoundEffect>("SFX/gem.wav");

    ImpactSoundEffect = AssetManager.Load<SoundEffect>("SFX/Impact_test.wav");
    BlipSoundEffect = AssetManager.Load<SoundEffect>("SFX/blip.wav");
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

  public void Update(GameTime gameTime)
  {
    if (MediaPlayer.State == MediaState.Stopped && _songs.Count > 0)
    {
      var random = new System.Random();
      var songNames = new List<string>(_songs.Keys);
      var nextSongName = songNames[random.Next(songNames.Count)];
      MediaPlayer.Play(_songs[nextSongName]);
    }
  }

  public void PlaySound(SoundEffect soundEffect, float pitch = 0f, float pan = 0f)
  {
    if (m_disableSound)
      return;

    soundEffect.Play(m_settings.SfxVolume, pitch, pan);
  }

  public void StopSound(string soundName)
  {
  }
}
