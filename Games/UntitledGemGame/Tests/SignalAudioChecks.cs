using System.Reflection;
using UntitledGemGame;

internal static class SignalAudioChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    var type = typeof(GameMain).Assembly.GetType("UntitledGemGame.SignalAudio")!;
    var audio = Activator.CreateInstance(type, nonPublic: true)!;
    var begin = type.GetMethod("BeginReveal")!;
    var update = type.GetMethod("Update")!;
    var played = new List<SoundEvent>();
    Action<SoundEvent> play = played.Add;
    var choices = new List<SignalChoice>
    {
      new() { Signal = 0, Rarity = 0 }, new() { Signal = 2, Rarity = 2 }, new() { Signal = 4, Rarity = 4 }
    };
    void Begin() { begin.Invoke(audio, null); played.Clear(); }
    void Tick(float elapsed, bool visible = true, int hover = -1) => update.Invoke(audio, [elapsed, choices, visible, hover, play]);

    Tick(5f);
    Check(played.Count == 0, "A restored offer replayed its decode/reveal sounds");
    Begin();
    Tick(0f);
    Check(played.SequenceEqual([SoundEvent.SignalDecodeFirst]), "First decode did not start with the scan");
    Tick(0.219f);
    Check(played.Count == 1, "Second decode started early");
    Tick(0.22f);
    Check(played[^1] == SoundEvent.SignalDecodeSecond, "Second card has no independent decode event");
    Tick(0.299f);
    Check(played.Count == 2, "First reveal played before its visual reveal");
    Tick(0.3f);
    Check(played[^1] == SoundEvent.SignalRevealCommon, "Common reveal event missing");
    Tick(0.44f);
    Check(played[^1] == SoundEvent.SignalDecodeThird, "Third card has no independent decode event");
    Tick(0.52f);
    Check(played[^1] == SoundEvent.SignalRevealRare, "Rare reveal event missing");
    Tick(0.74f);
    Check(played[^1] == SoundEvent.SignalRevealLegendary && played.Count == 6, "Legendary reveal event missing");
    Tick(1f); Tick(10f);
    Check(played.Count == 6, "Reveal audio repeated on later frames");

    SoundEvent[] revealEvents = [SoundEvent.SignalRevealCommon, SoundEvent.SignalRevealUncommon,
      SoundEvent.SignalRevealRare, SoundEvent.SignalRevealEpic, SoundEvent.SignalRevealLegendary];
    foreach (var (soundEvent, rarity) in revealEvents.Select((e, index) => (e, index)))
    {
      foreach (var choice in choices) choice.Rarity = rarity;
      Begin();
      for (int frame = 0; frame < 60; frame++) Tick(frame / 60f);
      Check(played.Count(e => e == soundEvent) == 3, $"Not every card played its {soundEvent} event");
      Check(played.Count == 6, "A card emitted an extra decode or reveal event");
      Check(SoundMix.Rule(soundEvent).Bus == SoundBus.Player, "Reveals can be silenced by automation limits");
    }

    Begin(); Tick(0.8f);
    Check(played.SequenceEqual([SoundEvent.SignalDecodeFirst, SoundEvent.SignalDecodeSecond, SoundEvent.SignalRevealLegendary,
      SoundEvent.SignalDecodeThird, SoundEvent.SignalRevealLegendary, SoundEvent.SignalRevealLegendary]),
      "A slow frame lost or reordered reveal stages");
    Begin(); Tick(0f); Tick(0.1f, visible: false); Tick(5f);
    Check(played.SequenceEqual([SoundEvent.SignalDecodeFirst]), "Reopening or unpausing replayed unfinished reveal sounds");
    Begin(); choices.Clear(); Tick(0f);
    choices.AddRange([new() { Rarity = 0 }, new() { Rarity = 1 }, new() { Rarity = 2 }]);
    Tick(5f);
    Check(played.Count == 0, "Selecting a card did not cancel its old reveal sequence");

    // Existing offers can still be hovered without rearming their reveal audio.
    played.Clear();
    Tick(5f, hover: 0); Tick(5.1f, hover: 0); Tick(5.2f, hover: 1); Tick(5.3f, hover: 2);
    Check(played.SequenceEqual(Enumerable.Repeat(SoundEvent.SignalHover, 3)), "Hover must play once on entering each card");
    Tick(5.4f, hover: -1); Tick(5.5f, hover: 2);
    Check(played.Count == 4, "Leaving and returning to a card did not play hover");
    Tick(5.6f, visible: false); Tick(5.7f, hover: 2);
    Check(played.Count == 5, "Closing/reopening retained a stale hovered card");
    Tick(0.64f, hover: 0);
    Check(played.Count == 5, "A card played hover while still decoding");
    Tick(0.65f, hover: 0);
    Check(played.Count == 6, "Hover did not start when the card became selectable");
    Check(SoundMix.Rule(SoundEvent.SignalHover).Bus == SoundBus.Player, "Signal hover is on the background sound bus");
    Console.WriteLine("Signal audio checks passed: three decodes, five rarities, repeated rarity, reveal timing, slow frames, reopening, pause cancellation and hover entry/exit.");
  }
}
