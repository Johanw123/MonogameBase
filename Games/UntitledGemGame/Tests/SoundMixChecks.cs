using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

internal static class SoundMixChecks
{
  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  public static void Run()
  {
    using var shortSound = new SoundEffect(new byte[4410 * 2], 44100, AudioChannels.Mono);
    using var longSound = new SoundEffect(new byte[44100 * 2 * 3], 44100, AudioChannels.Mono);
    var played = new List<(SoundEffect Effect, float Volume, float Pitch)>();
    void Played(SoundEffect effect, float volume, float pitch, float pan) => played.Add((effect, volume, pitch));
    AudioManager.SoundPlayed += Played;
    HighPerfAudioManager mixer = null;
    try
    {
      // Fill the entire pending queue with higher numeric priorities, then send a player action.
      // Event importance must protect that action before admission, as well as during playback.
      mixer = new();
      for (int i = 0; i < 1000; i++)
        mixer.RequestSound(shortSound, 0, 0.1f, priority: 4000f, soundEvent: SoundEvent.CannonFireAuto, trigger: 1);
      mixer.RequestSound(shortSound, 0, 0.9f, priority: 0.1f, soundEvent: SoundEvent.CannonFireManual);
      mixer.ProcessQueue(Tick(1, 0.016));
      Check(played.Count(p => p.Volume == 0.9f) == 1 && played[0].Volume == 0.9f,
        "A late player action was lost behind automatic requests");
      Check(played.Count <= 4, "A same-frame burst escaped bus limits");
      mixer.StopAll();

      // Early-game activity keeps its full level; a brief action ducks it, then releases.
      mixer = new();
      played.Clear();
      mixer.RequestSound(longSound, 0, 0.4f, soundEvent: SoundEvent.CannonFireAuto);
      mixer.ProcessQueue(Tick(0.01, 0.01));
      Check(played.Single().Volume == 0.4f && mixer.AutomationGain == 1f,
        "Sparse early-game automation lost its full level");
      mixer.RequestSound(shortSound, 0, 0.9f, soundEvent: SoundEvent.CannonFireManual);
      mixer.ProcessQueue(Tick(0.03, 0.02));
      Check(mixer.BackgroundGain <= 0.35f, "Player action did not duck sparse automation");
      mixer.ProcessQueue(Tick(1, 0.97));
      Check(mixer.BackgroundGain > 0.95f, "Background level did not recover after player input");
      mixer.StopAll();

      // The same event may have multiple intentional layers, but duplicate triggers coalesce.
      played.Clear();
      mixer.RequestSound(shortSound, 0, soundEvent: SoundEvent.PlanetShellBurst, trigger: 1);
      mixer.RequestSound(shortSound, 0, soundEvent: SoundEvent.PlanetShellBurst, trigger: 1);
      mixer.RequestSound(shortSound, 0, soundEvent: SoundEvent.PlanetShellBurst, trigger: 2);
      mixer.ProcessQueue(Tick(2, 1));
      Check(played.Count == 2, "Event throttling lost a layer or admitted a duplicate trigger");
      mixer.StopAll();

      // No special immediate path for pickups: they share the bounded background mix.
      played.Clear();
      for (int frame = 1; frame <= 120; frame++)
      {
        for (int j = 0; j < 100; j++)
          mixer.RequestSound(shortSound, 1, soundEvent: SoundEvent.GemPickup);
        mixer.ProcessQueue(Tick(2 + frame / 60d, 1 / 60d));
      }
      Check(played.Count is >= 8 and <= 11, "Pickup rate is not limited across frames");
      Check(mixer.AutomationGain <= 0.3f, "Dense pickup requests did not quiet the background mix");
      mixer.StopAll();

      var autoEvents = new[] { SoundEvent.CannonFireAuto, SoundEvent.CannonImpact, SoundEvent.RocketLaunch,
        SoundEvent.RocketImpact, SoundEvent.ArcHarpoonPulse, SoundEvent.RailgunFire, SoundEvent.CoreDrillImpact };
      var counts = new List<int>();
      foreach (int fps in new[] { 30, 60, 144 })
      {
        mixer = new();
        played.Clear();
        for (int frame = 1; frame <= fps * 5; frame++)
        {
          foreach (var soundEvent in autoEvents)
            mixer.RequestSound(shortSound, 0, soundEvent: soundEvent);
          mixer.ProcessQueue(Tick(frame / (double)fps, 1d / fps));
          Check(mixer.ActiveVoiceCount <= 6, "Automation exceeded its live voice budget");
        }
        counts.Add(played.Count);
        Check(played.Count is >= 40 and <= 53, $"Unexpected background playback at {fps} FPS: {played.Count}");
        mixer.StopAll();
      }
      Check(counts.Max() - counts.Min() <= 2, "Background playback rate depends on frame rate");

      // Long samples cannot bypass concurrency caps; player and milestone capacity stays available.
      mixer = new();
      played.Clear();
      for (int frame = 1; frame <= 120; frame++)
      {
        foreach (var soundEvent in autoEvents)
          mixer.RequestSound(longSound, 0, 0.4f, soundEvent: soundEvent);
        mixer.ProcessQueue(Tick(frame / 60d, 1 / 60d));
        Check(mixer.ActiveVoiceCount <= 6, "Long automation tails accumulated too many voices");
      }
      int before = played.Count;
      mixer.RequestSound(longSound, 0, 0.9f, soundEvent: SoundEvent.CannonFireManual);
      mixer.RequestSound(longSound, 0, 0.8f, soundEvent: SoundEvent.PlanetShellBurst);
      mixer.ProcessQueue(Tick(2.02, 0.02));
      Check(played.Skip(before).Any(p => p.Volume == 0.9f) && played.Skip(before).Any(p => p.Volume == 0.8f),
        "Active automation prevented player or milestone playback");
      Check(mixer.BackgroundGain < mixer.AutomationGain * 0.5f, "Player input did not duck background voices");
      mixer.RemovePending(longSound);
      Check(mixer.ActiveVoiceCount == 0, "Replacing an asset leaked its active instances");

      // Muting updates already playing voices; screen teardown clears both queued and active audio.
      mixer.RequestSound(longSound, 0, soundEvent: SoundEvent.GemClick);
      mixer.ProcessQueue(Tick(2.1, 0.08));
      mixer.MasterVolume = 0;
      mixer.ProcessQueue(Tick(2.12, 0.02));
      var voices = (System.Collections.IList)typeof(HighPerfAudioManager)
        .GetField("voices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mixer);
      foreach (var voice in voices)
      {
        var instance = (SoundEffectInstance)voice.GetType().GetField("Sound").GetValue(voice);
        Check(instance.Volume == 0f, "An active sound ignored the SFX mute setting");
      }
      mixer.RequestSound(longSound, 0, soundEvent: SoundEvent.GemClick);
      mixer.StopAll();
      played.Clear();
      mixer.ProcessQueue(Tick(3, 0.88));
      Check(played.Count == 0 && mixer.ActiveVoiceCount == 0, "Screen teardown retained sound");
      Check(SoundMix.Rule(SoundEvent.CannonImpact, playerInitiated: true).Bus == SoundBus.Player,
        "Manual impacts lost their player context");
      Check(SoundMix.Rule(SoundEvent.RefuelStart).Bus == SoundBus.Player,
        "Manual refueling must have protected feedback");
      Console.WriteLine($"Sound mix checks passed: saturated queues, layers, pickup bursts, reserved voices, ducking, mute and teardown. Automation counts over 5 seconds at 30/60/144 FPS: {string.Join('/', counts)}.");
    }
    finally
    {
      mixer?.StopAll();
      AudioManager.SoundPlayed -= Played;
    }
  }

  private static GameTime Tick(double total, double delta) => new(TimeSpan.FromSeconds(total), TimeSpan.FromSeconds(delta));
}
