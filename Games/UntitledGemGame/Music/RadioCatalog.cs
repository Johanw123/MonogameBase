using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NVorbis;

namespace UntitledGemGame.Music;

internal enum StemRole { Bass, Pad, Arp, Drums, Percussion, Lead, Mix, Transition }

internal sealed class RadioStem
{
  public string File { get; set; }
  public int Frames { get; set; }
  public StemRole Role { get; set; }
  public int[] Sections { get; set; }
}

internal sealed class RadioTrack
{
  public string Name { get; set; }
  public int Bpm { get; set; }
  public int PhraseFrames { get; set; }
  public int[] Sections { get; set; }
  public RadioStem[] Stems { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(RadioTrack[]))]
internal partial class RadioJsonContext : JsonSerializerContext { }

internal sealed class RadioAudio(RadioTrack track, short[][] samples)
{
  public const int SampleRate = 44100;
  public readonly RadioTrack Track = track;
  public readonly short[][] Samples = samples;

  public static async Task<RadioAudio> LoadAsync(RadioTrack track, Func<string, Stream> open, CancellationToken cancellation)
  {
    var samples = new short[track.Stems.Length][];
    var buffer = new float[16384];
    for (int stem = 0; stem < samples.Length; stem++)
    {
      cancellation.ThrowIfCancellationRequested();
      var definition = track.Stems[stem];
      using var stream = open(definition.File);
      using var reader = new VorbisReader(stream, false);
      if (reader.SampleRate != SampleRate || reader.Channels != 2 || reader.TotalSamples != definition.Frames)
        throw new InvalidDataException($"Music format/length does not match catalog: {definition.File}");
      var pcm = samples[stem] = new short[checked(definition.Frames * 2)];
      int offset = 0, chunks = 0;
      while (offset < pcm.Length)
      {
        cancellation.ThrowIfCancellationRequested();
        int count = reader.ReadSamples(buffer, 0, Math.Min(buffer.Length, pcm.Length - offset));
        if (count == 0) throw new EndOfStreamException(definition.File);
        for (int i = 0; i < count; i++) pcm[offset + i] = (short)Math.Clamp((int)(buffer[i] * 32767f), -32768, 32767);
        offset += count;
        // Also yield to browser paints: WASM has no background decoding thread.
        if (++chunks % 4 == 0) await Task.Delay(1, cancellation);
      }
    }
    return new RadioAudio(track, samples);
  }
}
