The game radio uses the purchased music in `../Synthwave/`. These Ogg files are
compressed copies, not AI-generated music. Rebuild them with:

    python3 tools/prepare_music.py

Requires ffmpeg with libvorbis. The script leaves the purchased WAVs untouched,
aligns main stems to their eight/sixteen-bar lengths, and writes `catalog.json`.
Only main stems, FXTransition1 and the other pack's full loops are included.
Full arrangements, intros, alternate loops and short cuts remain source assets
for future uses such as trailers.

All ten pieces play once per shuffled rotation. The first piece has stems.
Stem tracks last 8–12 sixteen-bar phrases (roughly 5–10 minutes), with a new
arrangement every phrase and a section change every other phrase. Lead melodies
are occasional; other arrangements have a light pulse or a beatless breakdown.
Loop-only pieces repeat 2–3 times. Stems from different pieces are never mixed.
A quiet transition wash and fade-out precede the next piece's fade-in.

Esc pauses the music and its sample clock alongside the game. Resuming continues
from the same position, including audio already queued for playback.

Main menu, feedback, the docked upgrade overlay and prestige screens use
the softer mix: bass/pads/quiet arpeggios continue, while drums, percussion and
leads fade away. Complete loops duck in volume. The same sample clock keeps
running through these menus, so returning to gameplay restores the current phrase.

`Music/RadioMixer.cs` owns the PCM clock and arrangements. `RadioPlayer.cs`
decodes the current and next piece only and feeds a single streaming voice.
The music slider controls that voice separately from SFX. Browser builds bundle
these Ogg files locally and wait for the audio-activation gesture before starting.

Verification:

    dotnet run --project Tests/MusicChecks -- Content/Music/Radio

To also render a one-minute Space Ship preview with menu changes at 8, 25 and
35 seconds:

    dotnet run --project Tests/MusicChecks -- Content/Music/Radio /tmp/radio-preview.wav
