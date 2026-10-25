using System;
using System.Buffers.Binary;

namespace UntitledGemGame.Music;

// One PCM clock for every layer, including muted ones. Neither game frame rate
// nor opening a menu changes the musical position. No independent loop timers.
internal sealed class RadioMixer
{
  private readonly RadioAudio audio;
  private readonly Random random;
  private readonly float[] gains, targets;
  private readonly int[] cursors;
  private readonly long endFrame;
  private readonly int fadeFrames;
  private int section, arrangement;
  private bool menu;
  public long Position { get; private set; }
  public bool Finished => Position >= endFrame;
  public string Name => audio.Track.Name;

  public RadioMixer(RadioAudio audio, Random random)
  {
    this.audio = audio;
    this.random = random;
    gains = new float[audio.Samples.Length];
    targets = new float[gains.Length];
    cursors = new int[gains.Length];
    section = audio.Track.Sections[0];
    endFrame = (long)audio.Track.PhraseFrames * (audio.Track.Bpm == 0 ? random.Next(2, 4) : random.Next(8, 13));
    fadeFrames = audio.Track.Bpm == 0 ? 3 * RadioAudio.SampleRate : (int)Math.Round(RadioAudio.SampleRate * 480d / audio.Track.Bpm);
    UpdateTargets();
  }

  public void SetMenu(bool value)
  {
    if (menu == value) return;
    menu = value;
    UpdateTargets();
  }

  private void UpdateTargets()
  {
    for (int i = 0; i < targets.Length; i++)
    {
      var stem = audio.Track.Stems[i];
      bool active = Array.IndexOf(stem.Sections, section) >= 0;
      targets[i] = !active ? 0f : stem.Role switch
      {
        StemRole.Bass => menu ? 0.65f : 0.85f,
        StemRole.Pad => 0.65f,
        StemRole.Arp => menu ? 0.22f : 0.6f,
        StemRole.Drums => menu ? 0f : arrangement switch { 0 => 0.35f, 1 => 0.65f, 2 => 0.65f, _ => 0f },
        StemRole.Percussion => menu || arrangement == 3 ? 0f : 0.4f,
        StemRole.Lead => menu || arrangement != 2 ? 0f : 0.6f,
        StemRole.Mix => menu ? 0.4f : 0.8f,
        _ => 0f
      };
    }
  }

  // Returns bytes written, stopping exactly at the song boundary. A player can
  // put the next song into the remainder of the same output buffer.
  public int Render(Span<byte> destination)
  {
    int frames = (int)Math.Min(destination.Length / 4, endFrame - Position);
    const float gainStep = 1f / (RadioAudio.SampleRate * 1.5f);
    for (int frame = 0; frame < frames; frame++, Position++)
    {
      if (Position > 0 && Position % audio.Track.PhraseFrames == 0)
      {
        // Drift / pulse / occasional melody / breakdown. Do not repeat the
        // same arrangement, and let a section breathe for two whole phrases.
        arrangement = (arrangement + random.Next(1, 4)) % 4;
        if (Position / audio.Track.PhraseFrames % 2 == 0 && audio.Track.Sections.Length > 1)
        {
          int previous = Array.IndexOf(audio.Track.Sections, section);
          section = audio.Track.Sections[(previous + random.Next(1, audio.Track.Sections.Length)) % audio.Track.Sections.Length];
        }
        UpdateTargets();
      }
      float left = 0, right = 0;
      for (int stem = 0; stem < gains.Length; stem++)
      {
        float target = targets[stem];
        var definition = audio.Track.Stems[stem];
        if (definition.Role == StemRole.Transition)
        {
          // A quiet wash leads out of a song. Different tempos never have
          // simultaneous drum beats; the next song fades in after this one.
          long start = endFrame - definition.Frames;
          if (Position >= start)
          {
            cursors[stem] = (int)(Position - start) * 2;
            target = menu ? 0.08f : 0.18f;
          }
        }
        gains[stem] += Math.Clamp(target - gains[stem], -gainStep, gainStep);
        var pcm = audio.Samples[stem];
        int cursor = cursors[stem];
        left += pcm[cursor] * gains[stem];
        right += pcm[cursor + 1] * gains[stem];
        cursors[stem] = cursor + 2 == pcm.Length ? 0 : cursor + 2;
      }
      float envelope = Math.Min(1f, Math.Min(Position / (3f * RadioAudio.SampleRate), (endFrame - Position) / (float)fadeFrames));
      // Keep headroom for summed instruments. The music slider is applied by
      // the output voice, independently of the SFX mixer and its voice limits.
      float gain = 0.7f * envelope;
      BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(frame * 4, 2), (short)Math.Clamp((int)(left * gain), -32768, 32767));
      BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(frame * 4 + 2, 2), (short)Math.Clamp((int)(right * gain), -32768, 32767));
    }
    return frames * 4;
  }
}
