using System;
using System.Collections.Generic;

namespace UntitledGemGame;

// Presentation events for a fresh three-card scan. Saved offers and reopening
// the panel do not begin a new sequence. Rendering never triggers audio.
internal sealed class SignalAudio
{
  public const float CardStagger = 0.22f;
  public const float DecodeDuration = 0.3f;
  public const float SelectableAge = 0.65f;
  private static readonly SoundEvent[] DecodeEvents =
    [SoundEvent.SignalDecodeFirst, SoundEvent.SignalDecodeSecond, SoundEvent.SignalDecodeThird];
  private static readonly SoundEvent[] RevealEvents =
    [SoundEvent.SignalRevealCommon, SoundEvent.SignalRevealUncommon, SoundEvent.SignalRevealRare,
      SoundEvent.SignalRevealEpic, SoundEvent.SignalRevealLegendary];
  private static readonly (float At, int Card, bool Decode)[] Stages =
  [
    (0f, 0, true), (CardStagger, 1, true), (DecodeDuration, 0, false),
    (2 * CardStagger, 2, true), (CardStagger + DecodeDuration, 1, false),
    (2 * CardStagger + DecodeDuration, 2, false)
  ];
  private int nextStage = Stages.Length;
  private int hoveredCard = -1;

  public void BeginReveal()
  {
    nextStage = 0;
    hoveredCard = -1;
  }

  public void Update(float elapsed, IReadOnlyList<SignalChoice> choices, bool visible, int hover,
    Action<SoundEvent> play)
  {
    if (!visible || choices.Count != 3)
    {
      // Closing/pausing during a reveal cancels remaining sounds rather than
      // replaying old decoding cues when the panel becomes visible again.
      nextStage = Stages.Length;
      hoveredCard = -1;
      return;
    }
    while (nextStage < Stages.Length && elapsed >= Stages[nextStage].At)
    {
      var stage = Stages[nextStage++];
      play(stage.Decode ? DecodeEvents[stage.Card] : RevealEvents[choices[stage.Card].Rarity]);
    }
    if (hover < 0 || hover >= 3 || elapsed - hover * CardStagger < SelectableAge) hover = -1;
    if (hover >= 0 && hover != hoveredCard) play(SoundEvent.SignalHover);
    hoveredCard = hover;
  }
}
