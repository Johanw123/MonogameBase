#nullable enable
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using UntitledGemGame;

internal static class SoundEffectsChecks
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  private static object Call(object instance, string method, params object[] args)
  {
    try { return instance.GetType().GetMethod(method, Private)!.Invoke(instance, args)!; }
    catch (TargetInvocationException error) { throw error.InnerException!; }
  }

  private static void Check(bool value, string message)
  {
    if (!value) throw new Exception(message);
  }

  public static void Run()
  {
    string originalDirectory = Directory.GetCurrentDirectory();
    string directory = Path.Combine(Path.GetTempPath(), "btb-sound-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(directory, "Content", "SFX"));
    File.WriteAllText(Path.Combine(directory, "UntitledGemGame.csproj"), "<Project />");
    string content = Path.Combine(directory, "Content");
    SoundEffectDefaults.Write(content, new());
    var singleton = typeof(AudioManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
    object? previousManager = singleton.GetValue(null);
    var audio = (AudioManager)Activator.CreateInstance(typeof(AudioManager), nonPublic: true)!;
    singleton.SetValue(null, audio);
    audio.SetSettings(new Settings { SfxVolume = 1f });
    object? lab = null;
    AudioManager? nextLaunch = null;
    using var builtIn = new SoundEffect(new byte[4410 * 2], 44100, AudioChannels.Mono);
    try
    {
      Directory.SetCurrentDirectory(directory);
      Call(audio, "LoadSoundDefaults");
      var sounds = (Dictionary<string, SoundEffect>)typeof(AudioManager).GetField("_sounds", Private)!.GetValue(audio)!;
      var cues = (Dictionary<SoundEvent, SoundCue[]>)typeof(AudioManager).GetField("_eventCues", Private)!.GetValue(audio)!;
      Check(Enum.GetValues<SoundEvent>().All(cues.ContainsKey), "Every event must have a sound slot");
      foreach (var cue in cues.Values.SelectMany(c => c))
        if (cue.Path != null) sounds[cue.Path] = builtIn;
      var layeredStartup = cues[SoundEvent.PlanetShellBurst];
      using var game = new Game();
      Type labType = typeof(GameMain).Assembly.GetType("UntitledGemGame.DebugSoundEffects")!;
      lab = Activator.CreateInstance(labType, Private, null, [game.Window], null)!;
      var slots = (IDictionary)labType.GetField("slots", Private)!.GetValue(lab)!;
      SoundEvent[] signalEvents = [SoundEvent.SignalDecodeFirst, SoundEvent.SignalDecodeSecond, SoundEvent.SignalDecodeThird,
        SoundEvent.SignalRevealCommon, SoundEvent.SignalRevealUncommon, SoundEvent.SignalRevealRare,
        SoundEvent.SignalRevealEpic, SoundEvent.SignalRevealLegendary, SoundEvent.SignalHover];
      foreach (var signalEvent in signalEvents)
      {
        Check(slots.Contains(signalEvent), $"F8 has no slot for {signalEvent}");
        Check((string)labType.GetMethod("GroupName", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [signalEvent])! == "Signals",
          $"F8 did not group {signalEvent} under Signals");
      }
      object Slot(SoundEvent e) => slots[e]!;
      object? Field(SoundEvent e, string name) => Slot(e).GetType().GetField(name)!.GetValue(Slot(e));

      byte[] wave = Wave();
      string source = Path.Combine(directory, "sound with spaces.wav");
      File.WriteAllBytes(source, wave);
      void Import(SoundEvent e, string path)
      {
        Call(lab, "BeginImport", e, path);
        var timer = Stopwatch.StartNew();
        while (Field(e, "Loading") != null && timer.Elapsed < TimeSpan.FromSeconds(35))
        {
          Call(lab, "Update");
          Thread.Sleep(5);
        }
        Check(Field(e, "Loading") == null, "Import did not finish");
      }
      var soundEvent = SoundEvent.PlanetShellBurst;
      Import(soundEvent, source);
      Check(Field(soundEvent, "Error") == null && cues[soundEvent].Length == 1, "Import must replace all event layers");
      var replacement = sounds[cues[soundEvent][0].Path];
      Check(!ReferenceEquals(replacement, builtIn), "Import kept the built-in sound");
      Check(replacement.Duration.TotalSeconds > 0, "Imported audio has no samples");

      var played = new List<SoundEffect>();
      var playedParameters = new List<(float Volume, float Pitch, float Pan)>();
      void Played(SoundEffect effect, float volume, float pitch, float pan)
      {
        played.Add(effect);
        playedParameters.Add((volume, pitch, pan));
      }
      AudioManager.SoundPlayed += Played;
      var queue = (HighPerfAudioManager)typeof(AudioManager).GetField("m_soundManager", Private)!.GetValue(audio)!;
      try
      {
        audio.PlaySound(soundEvent);
        queue.ProcessQueue(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.016)));
        Check(played.Count == 1 && ReferenceEquals(played[0], replacement), "Gameplay did not use the replacement");
        var tuned = cues[soundEvent][0] with
        { Volume = 0.42f, Pitch = -0.25f, PitchJitter = 0f, Pan = 0.65f, Priority = 2f, Type = AudioManager.SoundType.UI };
        Call(audio, "DebugSetCue", soundEvent, 0, tuned);
        audio.PlaySound(soundEvent);
        queue.ProcessQueue(new GameTime(TimeSpan.FromSeconds(1.2), TimeSpan.FromSeconds(0.2)));
        Check(playedParameters[^1] == (0.42f, -0.25f, 0.65f), "Gameplay ignored the live parameters");
        Call(audio, "DebugPreview", soundEvent);
        var previews = (List<SoundEffectInstance>)typeof(AudioManager).GetField("_debugPreviews", Private)!.GetValue(audio)!;
        Check(previews.Count == 1, "Preview did not start");
        Check(previews[0].Volume == tuned.Volume && previews[0].Pitch == tuned.Pitch && previews[0].Pan == tuned.Pan,
          "Preview ignored the live parameters");
        Call(audio, "DebugSetCue", soundEvent, 0, tuned with { PitchJitter = 0.15f });
        var previewPitches = new HashSet<float>();
        for (int i = 0; i < 8; i++)
        {
          Call(audio, "DebugPreview", soundEvent);
          float pitch = previews[0].Pitch;
          Check(pitch >= -0.4f && pitch <= -0.1f, "Preview jitter exceeded its configured range");
          previewPitches.Add(pitch);
        }
        Check(previewPitches.Count > 1, "Preview did not apply pitch jitter");
        tuned = cues[soundEvent][0];
        Call(audio, "DebugStopPreviews");
        Check(previews.Count == 0, "Stop preview leaked its instance");

        Call(lab, "Save", soundEvent);
        Check(File.Exists(source), "Save must preserve the source file");
        Dictionary<string, SoundCue[]> defaults;
        using (var stream = File.OpenRead(Path.Combine(content, SoundEffectDefaults.ManifestPath))) defaults = SoundEffectDefaults.Read(stream);
        string savedPath = defaults[soundEvent.ToString()][0].Path;
        Check(File.ReadAllBytes(Path.Combine(content, savedPath)).SequenceEqual(wave), "Save changed PCM audio");

        nextLaunch = (AudioManager)Activator.CreateInstance(typeof(AudioManager), nonPublic: true)!;
        Call(nextLaunch, "LoadSoundDefaults");
        var nextCues = (IReadOnlyList<SoundCue>)Call(nextLaunch, "DebugCues", soundEvent);
        Check(nextCues.Count == 1 && nextCues[0].Path == savedPath, "Fresh launch did not use the saved default");
        Check(nextCues[0] == tuned with { Path = savedPath }, "Fresh launch lost the saved parameters");

        // A second replacement must discard requests for its disposed predecessor.
        audio.PlaySound(soundEvent);
        Import(soundEvent, source);
        Check(cues[soundEvent][0] == tuned, "Dropping another file reset the tuned parameters");
        Check(replacement.IsDisposed, "Replaced runtime effect was not released");
        played.Clear();
        queue.ProcessQueue(new GameTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.016)));
        Check(played.Count == 0, "Queued audio still references the disposed effect");

        // Exercise the real Revert and Save operations while preserving startup layers.
        Call(lab, "Revert", soundEvent);
        Check(ReferenceEquals(cues[soundEvent], layeredStartup), "Revert lost the launch's layered assignment");
        Call(lab, "Save", soundEvent);
        using (var stream = File.OpenRead(Path.Combine(content, SoundEffectDefaults.ManifestPath))) defaults = SoundEffectDefaults.Read(stream);
        Check(!defaults.ContainsKey(soundEvent.ToString()), "Save after Revert must restore the built-in default");

        // Tune an original layered sound without importing a replacement.
        Call(audio, "DebugSetCue", soundEvent, 1, layeredStartup[1] with { Pitch = 0.3f, Volume = 0.25f, Pan = -0.5f });
        Check(cues[soundEvent][0] == layeredStartup[0] && layeredStartup[1].Pitch != 0.3f,
          "Layer editing changed another layer or mutated the startup assignment");
        Call(lab, "Save", soundEvent);
        using (var stream = File.OpenRead(Path.Combine(content, SoundEffectDefaults.ManifestPath))) defaults = SoundEffectDefaults.Read(stream);
        Check(defaults[soundEvent.ToString()].SequenceEqual(cues[soundEvent]), "Parameter-only save lost the original layers");
        var parameterLaunch = (AudioManager)Activator.CreateInstance(typeof(AudioManager), nonPublic: true)!;
        Call(parameterLaunch, "LoadSoundDefaults");
        Check(((IReadOnlyList<SoundCue>)Call(parameterLaunch, "DebugCues", soundEvent)).SequenceEqual(cues[soundEvent]),
          "Fresh launch lost parameter-only edits to the original layers");
        Call(parameterLaunch, "DebugDisposeSounds");
        Call(lab, "SetSilent", soundEvent);
        Check(cues[soundEvent].Length == 1 && cues[soundEvent][0].Path == null, "Set silent did not mute every layer");
        Call(lab, "Revert", soundEvent);
        Call(lab, "Save", soundEvent);

        Import(SoundEvent.CannonFireManual, source);
        string activePath = cues[SoundEvent.CannonFireManual][0].Path;
        var active = sounds[activePath];
        Import(SoundEvent.CannonFireManual, Path.Combine(directory, "missing.wav"));
        Check(Field(SoundEvent.CannonFireManual, "Error") != null && ReferenceEquals(active, sounds[activePath]) && !active.IsDisposed,
          "A failed import must leave the current sound usable");

        Call(lab, "SetSilent", SoundEvent.CannonFireManual);
        Check(active.IsDisposed && cues[SoundEvent.CannonFireManual][0].Path == null
          && cues[SoundEvent.CannonFireManual][0].FallbackPath == null, "Set silent must release the override and remove all sound paths");
        played.Clear();
        audio.PlaySound(SoundEvent.CannonFireManual);
        queue.ProcessQueue(new GameTime(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(0.016)));
        Call(audio, "DebugPreview", SoundEvent.CannonFireManual);
        Check(played.Count == 0 && previews.Count == 0, "A silent event still played audio");
        // Use an originally audible event to verify saved silence across launches.
        Call(lab, "SetSilent", SoundEvent.MenuClick);
        Call(lab, "Save", SoundEvent.MenuClick);
        var silentLaunch = (AudioManager)Activator.CreateInstance(typeof(AudioManager), nonPublic: true)!;
        Call(silentLaunch, "LoadSoundDefaults");
        var silentCues = (IReadOnlyList<SoundCue>)Call(silentLaunch, "DebugCues", SoundEvent.MenuClick);
        Check(silentCues.Count == 1 && silentCues[0].Path == null && silentCues[0].FallbackPath == null,
          "Fresh launch lost the saved silent assignment");
        Call(silentLaunch, "DebugRevert", SoundEvent.MenuClick);
        Check(((IReadOnlyList<SoundCue>)Call(silentLaunch, "DebugCues", SoundEvent.MenuClick))[0].Path == null,
          "Revert must retain silence when it was the startup default");
        Call(silentLaunch, "DebugDisposeSounds");
        Call(lab, "Revert", SoundEvent.MenuClick);
        Check(cues[SoundEvent.MenuClick][0].Path != null, "Revert did not restore the audible startup assignment");
        Call(lab, "Save", SoundEvent.MenuClick);

        // Refueling owns long-lived voices: replacing its slot must stop the old loop.
        var harvester = new UntitledGemGame.Entities.Harvester();
        audio.UpdateRefuelSound(harvester);
        var voices = (IDictionary)typeof(AudioManager).GetField("_refuelVoices", Private)!.GetValue(audio)!;
        Check(voices.Count == 1, "Refuel fixture did not start a loop");
        Import(SoundEvent.RefuelLoop, source);
        Check(voices.Count == 0, "Replacing RefuelLoop must stop active old loops");
        audio.UpdateRefuelSound(harvester);
        Check(voices.Count == 1, "Refueling did not restart with the replacement");
        Call(audio, "DebugSetCue", SoundEvent.RefuelLoop, 0, cues[SoundEvent.RefuelLoop][0] with
          { Volume = 0.2f, Pitch = 0.3f, Pan = -0.4f });
        audio.UpdateRefuelSound(harvester);
        var voice = voices[harvester]!;
        var loop = (SoundEffectInstance)voice.GetType().GetField("Sound")!.GetValue(voice)!;
        Check(Math.Abs(loop.Pitch - 0.15f) < 0.001f && loop.Pan == -0.4f, "Live loop pitch/pan did not update");
        Call(lab, "SetSilent", SoundEvent.RefuelLoop);
        Check(voices.Count == 0, "Set silent did not stop the active refuel loop");

        // Distinct volumes identify the request ordering even when effects share an asset.
        playedParameters.Clear();
        queue.RequestSound(builtIn, 0, volume: 0.1f, priority: 0.1f);
        queue.RequestSound(builtIn, 0, volume: 0.9f, priority: 3f);
        queue.ProcessQueue(new GameTime(TimeSpan.FromSeconds(4.1), TimeSpan.FromSeconds(0.016)));
        Check(playedParameters.Select(p => p.Volume).SequenceEqual(new[] { 0.9f, 0.1f }), "Playback queue ignored priority");
      }
      finally { AudioManager.SoundPlayed -= Played; }

      // Newly added signal slots use the same live import, tuning, preview and
      // persisted-default path as the existing events.
      Import(SoundEvent.SignalRevealRare, source);
      var signalCue = cues[SoundEvent.SignalRevealRare][0] with { Volume = 0.37f, Pitch = 0.12f, Pan = -0.2f };
      Call(audio, "DebugSetCue", SoundEvent.SignalRevealRare, 0, signalCue);
      Call(audio, "DebugPreview", SoundEvent.SignalRevealRare);
      var signalPreviews = (List<SoundEffectInstance>)typeof(AudioManager).GetField("_debugPreviews", Private)!.GetValue(audio)!;
      Check(signalPreviews.Count == 1 && signalPreviews[0].Volume == 0.37f && signalPreviews[0].Pitch == 0.12f
        && signalPreviews[0].Pan == -0.2f, "Signal reveal ignored its F8 live tuning");
      Call(lab, "Save", SoundEvent.SignalRevealRare);
      var signalLaunch = (AudioManager)Activator.CreateInstance(typeof(AudioManager), nonPublic: true)!;
      try
      {
        Call(signalLaunch, "LoadSoundDefaults");
        var savedSignal = ((IReadOnlyList<SoundCue>)Call(signalLaunch, "DebugCues", SoundEvent.SignalRevealRare))[0];
        Check(savedSignal.Path.StartsWith("SFX/EventOverrides/SignalRevealRare-") && savedSignal.Volume == 0.37f
          && savedSignal.Pitch == 0.12f && savedSignal.Pan == -0.2f, "Signal reveal lost its saved F8 assignment");
      }
      finally { Call(signalLaunch, "DebugDisposeSounds"); }
      Call(lab, "Revert", SoundEvent.SignalRevealRare);
      Check(cues[SoundEvent.SignalRevealRare][0].Path == "SFX/Shapeform/equip.wav", "Signal reveal revert lost its startup cue");

      // Decode a compressed file through the same import path as native file drops.
      string mp3 = Path.Combine(directory, "compressed sound.mp3");
      var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true };
      foreach (string arg in new[] { "-nostdin", "-loglevel", "error", "-i", source, mp3 }) start.ArgumentList.Add(arg);
      using (var process = Process.Start(start)!) { string errors = process.StandardError.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0, errors); }
      Import(SoundEvent.MenuClick, mp3);
      Check(Field(SoundEvent.MenuClick, "Error") == null && cues[SoundEvent.MenuClick][0].Path.StartsWith("debug-sfx:"), "Compressed sound import failed");

      string[] invalid = ["{\"NoSuchEvent\":[{\"Path\":null}]}", "{\"MenuClick\":[{\"Path\":\"SFX/../../escape.wav\"}]}",
        "{\"MenuClick\":[{\"Path\":null,\"Pitch\":2}]}"];
      foreach (string json in invalid)
      {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        bool rejected = false;
        try { SoundEffectDefaults.Read(stream); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid manifest assignment was accepted");
      }
      Console.WriteLine("Sound effects checks passed: live parameters, preview jitter, saved tuning and silence, layered revert, priority, refuel loops, failed imports, WAV and MP3.");
    }
    finally
    {
      (lab as IDisposable)?.Dispose();
      if (nextLaunch != null) Call(nextLaunch, "DebugDisposeSounds");
      singleton.SetValue(null, previousManager);
      Directory.SetCurrentDirectory(originalDirectory);
      Directory.Delete(directory, recursive: true);
    }
  }

  private static byte[] Wave()
  {
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    const int samples = 4410;
    writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100);
    writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
    writer.Write("data"u8); writer.Write(samples * 2);
    for (int i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * Math.PI * 2 * 440 / 44100) * 1000));
    return stream.ToArray();
  }
}
