
using System.Collections.Generic;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

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

  private Dictionary<string, Song> _songs = new Dictionary<string, Song>();

  private AudioManager()
  {
  }

  public void PlaySong(string songName)
  {
    if (_songs.ContainsKey(songName))
    {
      MediaPlayer.Play(_songs[songName]);
    }
  }

  public void LoadContent()
  {
    if (m_initialized)
      return;

    m_initialized = true;

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

  public void Update(GameTime gameTime)
  {
    if (MediaPlayer.State == MediaState.Stopped)
    {
      var random = new System.Random();
      var songNames = new List<string>(_songs.Keys);
      var nextSongName = songNames[random.Next(songNames.Count)];
      MediaPlayer.Play(_songs[nextSongName]);
    }
  }

  public void PlaySound(string soundName)
  {
  }

  public void StopSound(string soundName)
  {
  }

  public void SetVolume(float volume)
  {
  }
}
