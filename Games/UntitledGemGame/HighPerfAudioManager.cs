using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

// A bounded mix: rate limits use seconds, and playing voices remain owned until they finish.
public sealed class HighPerfAudioManager
{
  private const int MaxPendingRequests = 64;
  private const int MaxSoundTypes = 32;
  private readonly record struct Request(SoundEffect Effect, int Type, float Volume, float Pitch,
    float Pan, float Priority, SoundEvent? Event, SoundMixRule Rule, long Trigger)
  {
    public int Importance => Rule.Bus switch
    {
      SoundBus.Player => 3, SoundBus.Milestone => 2, SoundBus.Pickup => 1, _ => 0
    };
  }

  private sealed class Bus(int voices, float rate, int burst)
  {
    public readonly int MaxVoices = voices;
    public readonly float Rate = rate;
    public readonly int Burst = burst;
    public float Tokens = burst;
  }

  // Separate capacity means a busy fleet cannot consume the player's voices or tokens.
  private readonly Bus[] buses = [new(8, 30f, 8), new(6, 10f, 3), new(2, 5f, 1), new(4, 8f, 4)];
  private readonly Request[] pending = new Request[MaxPendingRequests];
  private readonly int[,] pendingEventSlot = new int[Enum.GetValues<SoundEvent>().Length, 4];
  private int pendingCount;
  private readonly double[,] lastEvent = new double[Enum.GetValues<SoundEvent>().Length, 4];
  private readonly long[,] lastTrigger = new long[Enum.GetValues<SoundEvent>().Length, 4];
  private readonly int[,] typeCounts = new int[MaxSoundTypes, 4];
  private readonly int[] typeLimits = new int[MaxSoundTypes];
  private readonly float[] pitchSteps = new float[MaxSoundTypes], pitchLimits = new float[MaxSoundTypes];
  private readonly float[] pitchOffsets = new float[MaxSoundTypes];
  private readonly double[] lastTypePlay = new double[MaxSoundTypes];
  private double clock, duckUntil;
  private float activity, automationGain = 1f, duckGain = 1f;

  private sealed class Voice(Request request, SoundEffectInstance sound, double end)
  {
    public readonly Request Request = request;
    public readonly SoundEffectInstance Sound = sound;
    public readonly double End = end;
  }
  private readonly List<Voice> voices = new(20);
  private static readonly IComparer<Request> ByPriority = Comparer<Request>.Create((a, b) =>
    a.Importance != b.Importance ? b.Importance.CompareTo(a.Importance) : b.Priority.CompareTo(a.Priority));

  public int ActiveVoiceCount => voices.Count;
  public float AutomationGain => automationGain;
  public float MasterVolume { get; set; } = 1f;

  public HighPerfAudioManager()
  {
    Array.Fill(typeLimits, 8);
    for (int i = 0; i < lastEvent.GetLength(0); i++)
      for (int bus = 0; bus < 4; bus++) lastEvent[i, bus] = double.NegativeInfinity;
  }

  public void ConfigureSoundType(int soundTypeId, int maxPerFrame, float pitchStep = 0.05f, float maxPitch = 0.6f)
  {
    if ((uint)soundTypeId >= MaxSoundTypes) return;
    typeLimits[soundTypeId] = maxPerFrame;
    pitchSteps[soundTypeId] = pitchStep;
    pitchLimits[soundTypeId] = maxPitch;
  }

  public void RequestSound(SoundEffect effect, int soundTypeId, float volume = 1f, float basePitch = 0f,
    float pan = 0f, float priority = 1f, SoundEvent? soundEvent = null, bool playerInitiated = false, long trigger = 0)
  {
    if (effect == null || effect.IsDisposed || volume <= 0f) return;
    if ((uint)soundTypeId >= MaxSoundTypes) soundTypeId = 0;
    var rule = soundEvent.HasValue ? SoundMix.Rule(soundEvent.Value, playerInitiated) : new(SoundBus.Player, 0f);
    // Count attempted background events, including ones we drop. Saturate to bound dense single frames.
    if (rule.Bus is SoundBus.Automation or SoundBus.Pickup) activity = Math.Min(200f, activity + 1f);
    var request = new Request(effect, soundTypeId, volume, basePitch, pan, priority, soundEvent, rule, trigger);
    if (soundEvent.HasValue)
    {
      int existing = pendingEventSlot[(int)soundEvent.Value, (int)rule.Bus] - 1;
      if (existing >= 0 && (trigger == 0 || pending[existing].Trigger != trigger))
      {
        // Coalesce repeated triggers in constant time, keeping intentional layers together.
        if (priority > pending[existing].Priority) pending[existing] = request;
        return;
      }
    }
    if (pendingCount < pending.Length)
    {
      Remember(pendingCount, request);
      pending[pendingCount++] = request;
      return;
    }
    int weakest = 0;
    for (int i = 1; i < pendingCount; i++) if (ByPriority.Compare(pending[i], pending[weakest]) > 0) weakest = i;
    if (ByPriority.Compare(request, pending[weakest]) < 0)
    {
      var previous = pending[weakest];
      if (previous.Event.HasValue && pendingEventSlot[(int)previous.Event.Value, (int)previous.Rule.Bus] == weakest + 1)
        pendingEventSlot[(int)previous.Event.Value, (int)previous.Rule.Bus] = 0;
      Remember(weakest, request);
      pending[weakest] = request;
    }
  }

  private void Remember(int index, Request request)
  {
    if (request.Event.HasValue) pendingEventSlot[(int)request.Event.Value, (int)request.Rule.Bus] = index + 1;
  }

  // Replacing a sound in the lab must also release its playing instances before disposing the asset.
  public void RemovePending(SoundEffect effect)
  {
    int remaining = 0;
    Array.Clear(pendingEventSlot);
    for (int i = 0; i < pendingCount; i++)
      if (!ReferenceEquals(pending[i].Effect, effect))
      {
        Remember(remaining, pending[i]);
        pending[remaining++] = pending[i];
      }
    Array.Clear(pending, remaining, pendingCount - remaining);
    pendingCount = remaining;
    for (int i = voices.Count - 1; i >= 0; i--)
      if (ReferenceEquals(voices[i].Request.Effect, effect)) Release(i);
  }

  public void StopAll()
  {
    for (int i = voices.Count - 1; i >= 0; i--) Release(i);
    Array.Clear(pending, 0, pendingCount);
    pendingCount = 0;
    Array.Clear(pendingEventSlot);
    Array.Clear(typeCounts);
    Array.Clear(pitchOffsets);
    activity = 0f;
    automationGain = 1f;
    duckGain = 1f;
    duckUntil = 0;
    foreach (var bus in buses) bus.Tokens = bus.Burst;
    for (int i = 0; i < lastEvent.GetLength(0); i++)
      for (int bus = 0; bus < 4; bus++) lastEvent[i, bus] = double.NegativeInfinity;
  }

  private void Release(int index)
  {
    var voice = voices[index];
    if (!voice.Sound.IsDisposed) { voice.Sound.Stop(); voice.Sound.Dispose(); }
    voices.RemoveAt(index);
  }

  public float BackgroundGain => automationGain * duckGain;

  private float Gain(SoundBus bus) => bus switch
  {
    SoundBus.Automation => BackgroundGain,
    SoundBus.Pickup => MathF.Sqrt(BackgroundGain),
    _ => 1f
  };

  public void ProcessQueue(GameTime gameTime)
  {
    float dt = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
    // Follow the game's clock, while staying monotonic if a screen starts a fresh clock.
    double nextClock = Math.Max(clock + dt, gameTime.TotalGameTime.TotalSeconds);
    dt = (float)(nextClock - clock);
    clock = nextClock;
    duckGain = clock < duckUntil ? 0.35f : MathHelper.Lerp(duckGain, 1f, 1f - MathF.Exp(-dt / 0.12f));
    activity *= MathF.Exp(-dt);
    float target = Math.Clamp(MathF.Sqrt(8f / Math.Max(8f, activity)), 0.22f, 1f);
    automationGain = MathHelper.Lerp(automationGain, target, 1f - MathF.Exp(-dt / (target < automationGain ? 0.06f : 0.6f)));
    foreach (var bus in buses) bus.Tokens = Math.Min(bus.Burst, bus.Tokens + dt * bus.Rate);
    Array.Clear(typeCounts);
    for (int i = voices.Count - 1; i >= 0; i--)
      if (voices[i].Sound.IsDisposed || voices[i].Sound.State == SoundState.Stopped || clock >= voices[i].End) Release(i);

    if (pendingCount > 1) Array.Sort(pending, 0, pendingCount, ByPriority);
    for (int i = 0; i < pendingCount; i++)
    {
      var request = pending[i];
      var busId = (int)request.Rule.Bus;
      var bus = buses[busId];
      if (request.Effect.IsDisposed || bus.Tokens < 1f || typeCounts[request.Type, busId] >= typeLimits[request.Type]) continue;
      if (request.Event is SoundEvent e && clock - lastEvent[(int)e, busId] + 0.000001 < request.Rule.Cooldown
        && (request.Trigger == 0 || request.Trigger != lastTrigger[(int)e, busId])) continue;

      int count = 0, victim = -1;
      for (int j = 0; j < voices.Count; j++)
      {
        if (voices[j].Request.Rule.Bus != request.Rule.Bus) continue;
        count++;
        if (voices[j].Request.Priority <= request.Priority
          && (victim < 0 || voices[j].Request.Priority < voices[victim].Request.Priority)) victim = j;
      }
      if (count >= bus.MaxVoices)
      {
        // Player input can replace an older equal-priority tail. Background tails never churn.
        if (victim < 0 || request.Rule.Bus is SoundBus.Automation or SoundBus.Pickup) continue;
        Release(victim);
      }

      float pitch = MathHelper.Clamp(request.Pitch + (clock - lastTypePlay[request.Type] > 0.2 ? 0f : pitchOffsets[request.Type]), -1f, 1f);
      var sound = request.Effect.CreateInstance();
      try
      {
        sound.Pitch = pitch;
        sound.Pan = MathHelper.Clamp(request.Pan, -1f, 1f);
        sound.Volume = MathHelper.Clamp(request.Volume * MasterVolume * Gain(request.Rule.Bus), 0f, 1f);
        sound.Play();
      }
      catch (InstancePlayLimitException) { sound.Dispose(); continue; }
      if (request.Rule.Bus == SoundBus.Player) { duckUntil = clock + 0.18; duckGain = 0.35f; }
      voices.Add(new(request, sound, clock + request.Effect.Duration.TotalSeconds / Math.Pow(2, pitch)));
      AudioManager.NotifyPlayed(request.Effect, sound.Volume, pitch, sound.Pan);
      bus.Tokens -= 1f;
      typeCounts[request.Type, busId]++;
      if (request.Event.HasValue)
      {
        lastEvent[(int)request.Event.Value, busId] = clock;
        lastTrigger[(int)request.Event.Value, busId] = request.Trigger;
      }
      pitchOffsets[request.Type] = Math.Min((clock - lastTypePlay[request.Type] > 0.2 ? 0f : pitchOffsets[request.Type]) + pitchSteps[request.Type], pitchLimits[request.Type]);
      lastTypePlay[request.Type] = clock;
    }
    // Duck existing background tails as well as new sounds. Release smoothly after player input.
    foreach (var voice in voices)
      voice.Sound.Volume = MathHelper.Clamp(voice.Request.Volume * MasterVolume * Gain(voice.Request.Rule.Bus), 0f, 1f);
    Array.Clear(pending, 0, pendingCount);
    pendingCount = 0;
    Array.Clear(pendingEventSlot);
  }
}
