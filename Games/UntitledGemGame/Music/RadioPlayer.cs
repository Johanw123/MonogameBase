using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Serilog;

namespace UntitledGemGame.Music;

internal sealed class RadioPlayer : IDisposable
{
  private const string Root = "Content/Music/Radio/";
  private readonly Random random = new();
  private readonly Func<string, Stream> open;
  private readonly Queue<RadioTrack> rotation = new();
  private readonly CancellationTokenSource cancellation = new();
  private readonly byte[] buffer = new byte[2048 * 4];
  private RadioTrack[] catalog;
  private RadioTrack lastTrack;
  private Task<RadioAudio> loading;
  private RadioMixer mixer;
  private DynamicSoundEffectInstance output;
  private bool menu, disposed, failed;

  public RadioPlayer(Func<string, Stream> open = null)
    => this.open = open ?? (name => TitleContainer.OpenStream(Root + name));

  public void Update(bool inMenu, float volume, bool paused = false)
  {
    if (disposed || failed) return;
    menu = inMenu;
    try
    {
      if (paused)
      {
        if (output != null)
        {
          output.Volume = Math.Clamp(volume, 0f, 1f);
          if (output.State == SoundState.Playing) output.Pause();
        }
        return;
      }
      if (catalog == null)
      {
        using var stream = open("catalog.json");
        catalog = JsonSerializer.Deserialize(stream, RadioJsonContext.Default.RadioTrackArray);
        if (catalog == null || catalog.Length == 0) throw new InvalidDataException("Empty music catalog.");
        BeginLoad();
      }
      if (mixer == null && !UseLoadedTrack()) return;
      mixer.SetMenu(menu);
      output ??= new DynamicSoundEffectInstance(RadioAudio.SampleRate, AudioChannels.Stereo);
      output.Volume = Math.Clamp(volume, 0f, 1f);
      FillBuffers();
      if (output.State == SoundState.Paused) output.Resume();
      else if (output.State != SoundState.Playing) output.Play();
    }
    catch (Exception exception)
    {
      failed = true;
      Log.Error(exception, "Music radio stopped");
      Dispose();
    }
  }

  private void BeginLoad()
  {
    if (rotation.Count == 0)
    {
      var shuffled = (RadioTrack[])catalog.Clone();
      random.Shuffle(shuffled);
      // Let the first menu/game session demonstrate the adaptive soundtrack.
      if (lastTrack == null)
      {
        int firstStemTrack = Array.FindIndex(shuffled, track => track.Bpm > 0);
        if (firstStemTrack > 0) (shuffled[0], shuffled[firstStemTrack]) = (shuffled[firstStemTrack], shuffled[0]);
      }
      // Each track plays once per shuffled pass, including the loop-only pack.
      if (shuffled.Length > 1 && shuffled[0] == lastTrack)
        (shuffled[0], shuffled[1]) = (shuffled[1], shuffled[0]);
      foreach (var track in shuffled) rotation.Enqueue(track);
    }
    lastTrack = rotation.Dequeue();
    var next = lastTrack;
    var token = cancellation.Token;
#if KNI_WEB
    loading = RadioAudio.LoadAsync(next, open, token);
#else
    loading = Task.Run(() => RadioAudio.LoadAsync(next, open, token), token);
#endif
  }

  private bool UseLoadedTrack()
  {
    if (!loading.IsCompleted) return false;
    mixer = new RadioMixer(loading.GetAwaiter().GetResult(), random);
    mixer.SetMenu(menu);
    Log.Information("Music radio: {Track}", mixer.Name);
    BeginLoad(); // Decode the next track well before the current one ends.
    return true;
  }

  private void FillBuffers()
  {
    while (output.PendingBufferCount < 4)
    {
      int offset = 0;
      while (offset < buffer.Length)
      {
        if (mixer.Finished && !UseLoadedTrack())
        {
          Array.Clear(buffer, offset, buffer.Length - offset);
          break;
        }
        offset += mixer.Render(buffer.AsSpan(offset));
      }
      output.SubmitBuffer(buffer);
    }
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    cancellation.Cancel();
    output?.Dispose();
    // Observe a decode fault even if the game closes before the next update.
    if (loading != null) _ = loading.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    loading = null;
    mixer = null;
    catalog = null;
    rotation.Clear();
    cancellation.Dispose();
  }
}
