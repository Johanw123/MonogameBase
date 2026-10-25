using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using UntitledGemGame;

internal static class MusicPlaybackChecks
{
  public static void Run()
  {
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    // Two pieces sharing a real encoded bass stem keep this device check small.
    const string track = """
      {"name":"NAME","bpm":90,"phraseFrames":1881600,"sections":[1],"stems":[
        {"file":"SpaceShip_STEMS_Main1_Bass_90bpm.ogg","frames":940800,"role":"Bass","sections":[1]}]}
      """;
    string catalog = "[" + track.Replace("NAME", "A") + "," + track.Replace("NAME", "B") + "]";
    Func<string, Stream> open = name => name == "catalog.json"
      ? new MemoryStream(Encoding.UTF8.GetBytes(catalog)) : File.OpenRead(Path.Combine("Content/Music/Radio", name));
    Type type = typeof(GameMain).Assembly.GetType("UntitledGemGame.Music.RadioPlayer")!;
    var player = (IDisposable)Activator.CreateInstance(type, [open])!;
    var update = type.GetMethod("Update")!;
    void Tick(bool menu, float volume, bool paused = false)
    {
      FrameworkDispatcher.Update();
      update.Invoke(player, [menu, volume, paused]);
    }
    void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    void Until(Func<bool> condition)
    {
      var timeout = Stopwatch.StartNew();
      while (!condition() && timeout.Elapsed.TotalSeconds < 15) { Tick(true, 0.4f); Thread.Sleep(5); }
      Check(condition(), "Radio did not finish loading");
    }
    try
    {
      Tick(true, 0.4f, paused: true);
      Check(type.GetField("output", fields)!.GetValue(player) == null, "Radio started while the game was paused");
      Until(() => type.GetField("output", fields)!.GetValue(player) != null);
      var output = (DynamicSoundEffectInstance)type.GetField("output", fields)!.GetValue(player)!;
      Check(output.State == SoundState.Playing && output.PendingBufferCount > 0, "Radio did not submit/play PCM");
      Tick(false, 0f);
      Check(output.Volume == 0f, "Music mute did not affect the playing output");
      Tick(true, 0.75f);
      Check(output.Volume == 0.75f, "Music slider did not restore the playing output");

      Until(() => ((Task)type.GetField("loading", fields)!.GetValue(player)!).IsCompleted);
      object mixer = type.GetField("mixer", fields)!.GetValue(player)!;
      Type mixerType = mixer.GetType();
      Tick(false, 0.25f, paused: true);
      Check(output.State == SoundState.Paused, "Game pause did not pause the streaming voice");
      Check(output.Volume == 0.25f, "Music setting did not update while paused");
      Tick(false, 0.25f, paused: true);
      long pausedPosition = (long)mixerType.GetProperty("Position")!.GetValue(mixer)!;
      int pausedBuffers = output.PendingBufferCount;
      Thread.Sleep(150);
      Tick(false, 0.25f, paused: true);
      Check(output.State == SoundState.Paused && output.PendingBufferCount == pausedBuffers,
        "Paused playback consumed its queued audio");
      Check((long)mixerType.GetProperty("Position")!.GetValue(mixer)! == pausedPosition,
        "Paused radio advanced its music clock");
      Tick(false, 0.75f);
      Check(output.State == SoundState.Playing && output.Volume == 0.75f, "Radio did not resume playback");
      Check(ReferenceEquals(type.GetField("mixer", fields)!.GetValue(player), mixer)
        && (long)mixerType.GetProperty("Position")!.GetValue(mixer)! == pausedPosition
          + (output.PendingBufferCount - pausedBuffers) * 2048L,
        "Resuming restarted the song or discarded queued audio");
      string before = (string)mixerType.GetProperty("Name")!.GetValue(mixer)!;
      long end = (long)mixerType.GetField("endFrame", fields)!.GetValue(mixer)!;
      // Force a boundary eight frames into the next buffer to exercise switching
      // songs inside a single submitted PCM block, rather than on a game tick.
      mixerType.GetField("<Position>k__BackingField", fields)!.SetValue(mixer, end - 8);
      Thread.Sleep(120);
      Tick(false, 0.5f);
      mixer = type.GetField("mixer", fields)!.GetValue(player)!;
      Check((string)mixerType.GetProperty("Name")!.GetValue(mixer)! != before, "Finished track did not advance the radio");
      Check(ReferenceEquals(type.GetField("output", fields)!.GetValue(player), output), "Track change restarted the output voice");
      Check(output.Volume == 0.5f, "Track change lost the music setting");
      player.Dispose();
      Check(output.IsDisposed, "Closing radio leaked its streaming voice");
      player.Dispose();
      Tick(true, 1f);
      Console.WriteLine("Music playback checks passed: real streaming voice, PCM queues, live mute/volume, pause/resume without advancing the clock, song boundary inside a buffer, rotation and disposal.");
    }
    finally { player.Dispose(); }
  }
}
