#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using UntitledGemGame;

public partial class AudioManager
{
  private readonly Dictionary<SoundEvent, SoundCue[]> _startupCues = new();
  private readonly Dictionary<SoundEvent, SoundEffect> _debugSounds = new();
  private readonly List<SoundEffect> _debugStartupSounds = new();
  private readonly List<SoundEffectInstance> _debugPreviews = new();

  internal IReadOnlyList<SoundCue> DebugCues(SoundEvent soundEvent) => _eventCues[soundEvent];

  internal bool DebugIsBuiltIn(SoundEvent soundEvent, SoundCue[] cues) => cues.SequenceEqual(EventCues[soundEvent]);

  internal void DebugSetCue(SoundEvent soundEvent, int layer, SoundCue cue)
  {
    // Startup arrays stay immutable so parameter edits can always be reverted.
    var cues = (SoundCue[])_eventCues[soundEvent].Clone();
    cues[layer] = cue;
    _eventCues[soundEvent] = cues;
  }

  internal void DebugSetSilent(SoundEvent soundEvent)
  {
    var cue = _eventCues[soundEvent][0];
    DebugReleaseOverride(soundEvent);
    _eventCues[soundEvent] = [cue with { Path = null, FallbackPath = null }];
  }

  internal void DebugReplace(SoundEvent soundEvent, byte[] wave, string displayName)
  {
    // Construct before changing anything: an invalid file leaves the current sound intact.
    using var stream = new MemoryStream(wave, writable: false);
    var effect = SoundEffect.FromStream(stream);
    effect.Name = displayName;
    var cue = _eventCues[soundEvent][0];
    DebugReleaseOverride(soundEvent);
    string key = "debug-sfx:" + soundEvent;
    _sounds[key] = effect;
    _debugSounds[soundEvent] = effect;
    _eventCues[soundEvent] = [cue with { Path = key, FallbackPath = null, Optional = false }];
  }

  internal void DebugRevert(SoundEvent soundEvent)
  {
    DebugReleaseOverride(soundEvent);
    _eventCues[soundEvent] = _startupCues[soundEvent];
  }

  private void DebugReleaseOverride(SoundEvent soundEvent)
  {
    DebugStopPreviews();
    if (soundEvent == SoundEvent.RefuelLoop) StopRefuelSounds();
    if (!_debugSounds.Remove(soundEvent, out var previous)) return;
    m_soundManager.RemovePending(previous);
    _sounds.Remove("debug-sfx:" + soundEvent);
    previous.Dispose();
  }

  internal void DebugPreview(SoundEvent soundEvent)
  {
    DebugStopPreviews();
    foreach (var cue in _eventCues[soundEvent])
    {
      var effect = ResolveSound(cue.Path);
      float volume = cue.Volume;
      if (effect == null && cue.FallbackPath != null)
      {
        effect = ResolveSound(cue.FallbackPath);
        volume = cue.FallbackVolume;
      }
      if (effect == null) continue;
      var instance = effect.CreateInstance();
      try
      {
        instance.Volume = MathHelper.Clamp(volume * (m_settings?.SfxVolume ?? 0f), 0f, 1f);
        instance.Pitch = MathHelper.Clamp(cue.Pitch + (cue.PitchJitter > 0f
          ? RandomHelper.Float(-cue.PitchJitter, cue.PitchJitter) : 0f), -1f, 1f);
        instance.Pan = cue.Pan;
        instance.Play();
        _debugPreviews.Add(instance);
      }
      catch { instance.Dispose(); throw; }
    }
  }

  internal void DebugStopPreviews()
  {
    foreach (var instance in _debugPreviews) { instance.Stop(); instance.Dispose(); }
    _debugPreviews.Clear();
  }

  internal void DebugDisposeSounds()
  {
    DebugStopPreviews();
    StopRefuelSounds();
    foreach (var effect in _debugSounds.Values) { m_soundManager.RemovePending(effect); effect.Dispose(); }
    foreach (var effect in _debugStartupSounds) { m_soundManager.RemovePending(effect); effect.Dispose(); }
    _debugSounds.Clear();
    _debugStartupSounds.Clear();
  }
}
#endif
