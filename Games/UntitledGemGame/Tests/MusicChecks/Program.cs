using System.Buffers.Binary;
using System.Text.Json;
using UntitledGemGame.Music;

int checks = 0;
void Check(bool condition, string message)
{
  if (!condition) throw new Exception(message);
  checks++;
}

// Synthetic PCM makes timing and gain behavior independently measurable.
RadioStem Stem(StemRole role, int frames) => new() { Role = role, Frames = frames, Sections = [1], File = role.ToString() };
var track = new RadioTrack { Name = "Clock check", Bpm = 90, PhraseFrames = 200000, Sections = [1],
  Stems = [Stem(StemRole.Bass, 997), Stem(StemRole.Pad, 1994), Stem(StemRole.Drums, 997)] };
var pcm = new[] { new short[1994], new short[3988], new short[1994] };
for (int i = 0; i < pcm.Length; i++)
  for (int sample = 0; sample < pcm[i].Length; sample += 2)
  {
    pcm[i][sample] = i == 2 ? (short)5000 : (short)0;
    pcm[i][sample + 1] = i == 2 ? (short)0 : (short)(1000 + sample / 2);
  }
var audio = new RadioAudio(track, pcm);
var a = new RadioMixer(audio, new Random(41));
var b = new RadioMixer(audio, new Random(41));
byte[] all = new byte[400000 * 4];
int written = a.Render(all);
var differentlyChunked = new byte[all.Length];
int offset = 0;
while (offset < written)
{
  int length = Math.Min(1028, written - offset);
  offset += b.Render(differentlyChunked.AsSpan(offset, length));
}
Check(all.AsSpan(0, written).SequenceEqual(differentlyChunked.AsSpan(0, written)), "PCM depends on game frame/buffer size");
Check(a.Position == 400000, "Audio clock lost frames");
int expected = (int)(((1000 + 399999 % 997) * 0.85f + (1000 + 399999 % 1994) * 0.65f) * 0.7f);
Check(BinaryPrimitives.ReadInt16LittleEndian(all.AsSpan(399999 * 4 + 2, 2)) == expected, "Double-length stems drifted");
// Keep the following fade check within one arrangement, so a deliberately
// beatless breakdown cannot be mistaken for a failed gameplay fade-in.
track.PhraseFrames = 1000000;
var menuMixer = new RadioMixer(audio, new Random(41));
menuMixer.Render(new byte[44100 * 4 * 4]);
menuMixer.SetMenu(true);
var fadeBuffer = new byte[44100 * 4 * 2];
menuMixer.Render(fadeBuffer);
Check(BinaryPrimitives.ReadInt16LittleEndian(fadeBuffer.AsSpan(fadeBuffer.Length - 4, 2)) == 0
  && BinaryPrimitives.ReadInt16LittleEndian(fadeBuffer.AsSpan(fadeBuffer.Length - 2, 2)) > 0,
  "Menu did not remove drums while retaining the bed");
Check(menuMixer.Position == 44100 * 6, "Opening menu restarted music");
menuMixer.SetMenu(false);
menuMixer.Render(fadeBuffer);
Check(BinaryPrimitives.ReadInt16LittleEndian(fadeBuffer.AsSpan(fadeBuffer.Length - 4, 2)) > 0,
  "Returning to gameplay did not restore drums");
Check(menuMixer.Position == 44100 * 8, "Closing menu restarted music");

var loopTrack = new RadioTrack { Name = "Loop", Bpm = 0, PhraseFrames = 997, Sections = [1], Stems = [Stem(StemRole.Mix, 997)] };
var loop = new RadioMixer(new RadioAudio(loopTrack, [pcm[0]]), new Random(5));
loop.SetMenu(true);
loop.Render(new byte[16000]);
Check(loop.Finished && loop.Position % 997 == 0, "Complete loop did not end on its loop boundary");
Check(loop.Render(new byte[1024]) == 0, "Finished mixer kept writing samples");

string root = args.Length > 0 ? args[0] : "Content/Music/Radio";
using var catalogStream = File.OpenRead(Path.Combine(root, "catalog.json"));
var catalog = JsonSerializer.Deserialize(catalogStream, RadioJsonContext.Default.RadioTrackArray)!;
Check(catalog.Length == 10 && catalog.Count(t => t.Bpm > 0) == 5, "Expected five stem tracks and five full loops");
RadioAudio previewAudio = null;
foreach (var definition in catalog)
{
  var loaded = await RadioAudio.LoadAsync(definition, name => File.OpenRead(Path.Combine(root, name)), CancellationToken.None);
  Check(loaded.Samples.Length == definition.Stems.Length, "Missing decoded layer");
  for (int i = 0; i < definition.Stems.Length; i++)
  {
    var stem = definition.Stems[i];
    Check(loaded.Samples[i].Length == stem.Frames * 2, "Decoded length differs from authored loop length");
    if (definition.Bpm > 0 && stem.Role != StemRole.Transition)
      Check(definition.PhraseFrames % stem.Frames == 0, "A stem does not divide a phrase exactly");
  }
  Console.WriteLine($"Decoded {definition.Name}: {definition.Stems.Length} layers, {loaded.Samples.Sum(s => (long)s.Length * 2) / 1048576d:0.0} MB PCM.");
  if (definition.Name == "Space Ship") previewAudio = loaded;
}

if (args.Length > 1)
{
  var preview = new RadioMixer(previewAudio!, new Random(4));
  byte[] data = new byte[60 * RadioAudio.SampleRate * 4];
  int position = 0;
  foreach (var (seconds, menu) in new[] { (8, true), (17, false), (10, true), (25, false) })
  {
    preview.SetMenu(menu);
    int count = seconds * RadioAudio.SampleRate * 4;
    Check(preview.Render(data.AsSpan(position, count)) == count, "Preview ended early");
    position += count;
  }
  int clipped = 0, peak = 0;
  for (int i = 0; i < data.Length; i += 2)
  {
    int magnitude = Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i, 2)));
    peak = Math.Max(peak, magnitude);
    if (magnitude >= 32767) clipped++;
  }
  Check(clipped == 0, "Preview mix clips");
  using var file = File.Create(args[1]);
  using var writer = new BinaryWriter(file);
  writer.Write("RIFF"u8); writer.Write(data.Length + 36); writer.Write("WAVEfmt "u8);
  writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(RadioAudio.SampleRate);
  writer.Write(RadioAudio.SampleRate * 4); writer.Write((short)4); writer.Write((short)16);
  writer.Write("data"u8); writer.Write(data.Length); writer.Write(data);
  Console.WriteLine($"Preview: {args[1]}; peak {20 * Math.Log10(peak / 32768d):0.0} dBFS, no clipped samples.");
}
Console.WriteLine($"Passed {checks} music checks: decode lengths, double-length stems, shared clock, buffer independence, menu fades, resume and loop endings.");
