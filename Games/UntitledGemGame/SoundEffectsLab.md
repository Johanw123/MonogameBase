Press **F8** to open the development sound effects lab from the main menu or during
gameplay. It is also available from the Tab debug panel. The lab is disabled when
`Demo.IsDev` is false and is excluded from the browser build and capture sessions.

- Filter by event name, then drop one audio file from your file manager onto the
  named blue target in its event card. Cards have opaque backgrounds, borders,
  extra spacing, and a highlighted target under the pointer. Alternatively,
  paste a full path and press Load or Enter.
- Events are grouped into Menu & UI, Gems & Cursor, Harvesters & Refueling,
  Shipyard & Modules, Signals, Ship & Intro, Abilities & Ship Systems, Weapons,
  Planet & Core, and Prestige & Time Loop. Sections start collapsed; click a
  heading to open it, or use Expand all / Collapse all. The filter matches both
  event names and section names and temporarily opens matching sections. Clearing
  it restores your previous section choices.
- The replacement is used immediately by gameplay, including events that are
  normally silent. For layered events, one imported sound replaces all layers.
- Tune volume (0–2), pitch (-1–1), pitch jitter (0–1), stereo pan (-1 left to +1
  right), playback priority (0–4), and sound category in each card. Edits apply
  immediately to gameplay. Existing layered events have separate controls for
  each layer. A new dropped sound retains the current first layer's parameters.
- Preview plays the current assignment with its event pitch, randomized pitch
  jitter, pan, volume, and your SFX volume setting, bypassing gameplay throttling. RefuelLoop previews once;
  Stop preview stops playback. Gameplay loops switch on the next refueling update.
- Set silent assigns `SfxNone` to the entire event, including all its layers and
  fallback sounds. Active refueling loops and previews stop. Press Save to keep
  silence as the default; Revert restores the startup sound and parameters.
- Revert restores the exact assignment from this launch, even after Save. It
  restores all original layers. Revert changes the live slot; press Save afterward
  if you also want to restore that default on disk.
- Save keeps the current parameters and assignment, including silence and edits
  to original sounds without importing a file. New WAVs go into `Content/SFX/EventOverrides/`; assignments go into
  `Content/SFX/EventDefaults.json`. The original external file is preserved.
  Commit the imported WAV and JSON together. Both desktop and web content builders
  include them, so saved defaults also ship in builds. Developer launches read
  the source files directly, so saved defaults work without rebuilding content.

Mono/stereo 16-bit PCM WAV files at 8–48 kHz load directly. Other audio formats
(including MP3, OGG, FLAC, and other WAV encodings) are converted with **FFmpeg**,
which must be on PATH. Conversion runs in the background; failed imports leave
the current sound intact and show the error in that row. Files and decoded WAVs
are limited to 64 MB. Save requires launching from the project or its build folder
so the source Content directory can be found.

Every `SoundEvent` appears automatically. Add gameplay calls through
`AudioManager.PlaySound(SoundEvent...)` and configure the built-in cue in
`AudioManager.cs`. Event volume, pitch jitter, category, and throttling are retained
when swapping a sound. Every event also has a gameplay mix rule in `SoundMix.cs`,
shown in its card. Player feedback has reserved capacity ahead of milestones,
pickups and automation. Priority orders requests within that importance level.
GemCollect adds escalating pitch; every category uses the bounded queue.
Preview bypasses the mix rules so you can audition the asset clearly.
RefuelLoop uses dedicated loop voices, so its
priority and category controls are disabled. Its pitch and pan update on active
voices, and jitter picks a variation per voice. Music is outside this tool's scope.

The Shapeform first pass assigns 34 edited stock clips to 48 events. Source files,
durations and processing are documented in `Content/SFX/Shapeform/SOURCES.md`.
See `SoundDesign.md` for the selection rationale and late-game mixing budgets.

Focused validation (requires FFmpeg and a working desktop audio backend; OpenAL's
null backend can be used for automated checks):

```sh
dotnet build --no-restore -m:1
dotnet build Tests/PersistenceChecks.csproj --no-restore -m:1
ALSOFT_DRIVERS=null dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --sound-effects-check
ALSOFT_DRIVERS=null dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --sound-mix-check
```

To render the panel and check drop targeting without starting gameplay:

```sh
SDL_VIDEODRIVER=offscreen ALSOFT_DRIVERS=null \
LD_LIBRARY_PATH="$PWD/bin/Debug/net10.0/runtimes/linux-x64/native" \
dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --sound-effects-panel-check /tmp/sound-effects-panel.png
```
