# Getting started with Steam

The desktop game initializes Steam before constructing its renderer, runs callbacks
on the game thread each update, and shuts Steam down after disposing the game.
`GameServices` exposes stats, achievements, availability and the player name. The browser
uses `LocalPlatformServices` and does not reference Steamworks.NET.

## First connection

The game's Steam App ID is `5084070`, but local runs temporarily use Valve's
Spacewar sample App ID `480` while the game's Steamworks setup is being completed.
Start the Steam desktop client and sign in. From the game project directory, run
this in fish, bash, or PowerShell:

```bash
dotnet run
```

The default `UntitledGemGame` profile in `Properties/launchSettings.json` supplies
`SteamAppId=480` automatically. Select that profile when launching from an IDE.
Launch settings apply to local development and are not included in published builds.
Switch the profile back to `5084070` when the account/app configuration is ready.
Spacewar uses shared test data; do not use it for the game's real achievements or saves.

Look for `[Steam] Connected as ...` in the console and try Shift+Tab in the game.
A successful connection does not by itself guarantee the overlay works on every
graphics/platform configuration. Initialization failures log the Steam error and
allow local play. The environment variable keeps a local development run from
requesting a relaunch through Steam.

This setup accepts `SteamAppId` from the environment; it does not require or generate
`steam_appid.txt`. Startup and diagnostics do not write achievements, stats or cloud
saves. Gameplay can now explicitly write stats and achievements through the API below.

## Calling the API from gameplay

Import `UntitledGemGame.Platform` and use `GameServices.Stats` and
`GameServices.Achievements` from any thread. The examples below use placeholder
API names: define and publish your own names/types in Steamworks for App ID 5084070
before connecting them to gameplay. They are not Spacewar's definitions.

```csharp
using UntitledGemGame.Platform;

var increment = await GameServices.Stats.Increment("GEMS_COLLECTED", 1);
if (!increment.Success)
{
    Console.WriteLine(increment.Message);
    return;
}

var unlock = await GameServices.Achievements.Unlock("FIRST_RUN");
if (!unlock.Success)
    Console.WriteLine(unlock.Message);

// At a milestone such as the end of a run, while the game loop is still running:
var saved = await GameServices.Stats.FlushAsync();
if (!saved.Success)
    Console.WriteLine($"Steam progress was not confirmed saved: {saved.Message}");
```

Reads also run through the game-thread queue:

```csharp
var gems = await GameServices.Stats.GetIntAsync("GEMS_COLLECTED");
if (gems.Success)
    Console.WriteLine(gems.Value);

var unlocked = await GameServices.Achievements.IsUnlockedAsync("FIRST_RUN");
if (unlocked.Success)
    Console.WriteLine(unlocked.Value);
```

`Set(name, int)`, `Set(name, float)` and matching `Increment` overloads support
integer and floating-point stats; use `GetFloatAsync` for floating-point reads.
`GameServices.Stats.IsReady` indicates whether initialization has finished.
Requests queued before readiness wait up to 30 seconds while updates are pumped.
No platform/Steam dependency is needed at call sites beyond this namespace.

### Completion and saving

- Mutation success means Steam accepted the value into local state. It does not
  mean the server saved it. Read values are valid only when `Success` is true.
- Changes accumulate until `FlushAsync`. A clean flush performs no Steam write.
  Stats and achievements share the same flush. New operations wait behind a save
  in progress so its callback cannot incorrectly confirm later changes.
- Flush success confirms Steam's `UserStatsStored_t` success callback, or that
  there were no pending changes. Check mutation results separately: a flush does
  not turn a rejected mutation into a successful one.
- Flush at major milestones, not every frame or every gem. There is no automatic
  timer or durable offline queue. Keep ordinary game progress in the local save.
- After a rejected save, retry `FlushAsync` later; do not repeat increments.
  `Conflict` means Steam supplied corrected server values: re-read and reconcile
  them with the game's saved progress. A save timeout has an unknown outcome and
  disables the progress service for that session to avoid misattributing late
  callbacks; restart the game before retrying.
- Unavailable platforms return `Unavailable`; calls after shutdown return
  `Disposed` (or `Unavailable` through the reset global facade). Nothing is
  silently reported as saved. Queues are bounded at 4096 pending requests.
- Only the application owns initialization, callback pumping and disposal. Do not
  call raw Steam stats/write APIs alongside this layer: it owns save callbacks.

Use `await` in asynchronous event handlers/tasks. Never call `.Wait()` or `.Result`
on the game thread, or await completion before starting the game loop: requests
need that loop to advance. Task completion does not guarantee continuation on the
game thread, so dispatch any subsequent entity/UI changes through the game's normal
threading mechanism. All Steam API operations themselves execute on the game thread.

Await the final milestone flush **before** calling `Exit()`. Disposal cancels pending
requests and logs unconfirmed changes; it does not block shutdown waiting for Steam.

Run isolated queue/save tests without Steam:

```sh
dotnet run --project Tests/PlatformChecks/PlatformChecks.csproj
```

## API diagnostics

Run a read-only check without opening a game window:

```sh
dotnet run -- --steam-check
```

This uses the default local App ID (currently 480) and checks identity, login,
ownership, language, friend count, cloud settings/quota, a stats request, achievement
reads, an asynchronous player-count request, and a read through the queued gameplay
API. It pumps callbacks on the main thread with a 15-second deadline per diagnostic
phase and exits with code 0 on success or 1 on failure.
No achievement unlocks, stats updates, leaderboard scores or cloud files are written.
A zero achievement count is reported as skipped, not verified. Cloud quota reads
do not verify cloud file synchronization. Windows and overlay behavior still need
to be tested on the actual Windows build.

Verified on Linux with App ID 480: the normal game diagnostic mode and an isolated
Native AOT executable using the same platform sources both passed the live checks,
including five achievement reads, both asynchronous results and the queued gameplay
read. Steamworks.NET still emits IL2091 for generic `CallResult<T>` and `Callback<T>`
marshalling during AOT publication. The read callbacks worked in that run. Live save
confirmation callbacks have not been exercised; queue/save semantics are covered by
35 isolated checks using a fake backend. New callback types should also be exercised
in a published build. Supply `SteamAppId` before
process startup (the launch profile does this), so native Steam code receives it.

## Linux overlay testing (optional)

Steam API initialization and overlay injection are separate. When launching outside
Steam on Linux, Valve documents preloading `gameoverlayrenderer.so`. For an x64 game
with Steam in its standard location, this command works in fish and bash:

```sh
env SDL_VIDEODRIVER=x11 LD_PRELOAD="$HOME/.local/share/Steam/ubuntu12_64/gameoverlayrenderer.so" dotnet run --no-build
```

Build once with `dotnet build` if needed. Adjust the library path if Steam is
installed elsewhere. This command selects X11 (XWayland on a Wayland desktop) to
avoid native Wayland overlay compatibility issues. Ensure the overlay is enabled
in Steam's In Game settings, then try Shift+Tab. If you already use `LD_PRELOAD`
for other libraries, preserve those entries when adding the overlay library.

References: [Valve's Linux FAQ](https://partner.steamgames.com/doc/store/application/platforms/linux),
[Wayland overlay tracking issue](https://github.com/ValveSoftware/steam-for-linux/issues/8020).

## Release builds

`SteamPlatformServices.cs` defaults to App ID `5084070`. When run directly without
an environment override, the game
calls `RestartAppIfNecessary` and exits if Steam requests a relaunch.

Publish for an explicit supported runtime, for example:

```bash
dotnet publish UntitledGemGame.csproj -c Release -r linux-x64
```

The matching native Steam library and wrapper license are copied into build and
publish output. Native AOT incorporates the managed wrapper into the executable;
the native Steam library must still ship alongside it. Verify initialization,
callbacks, shutdown, and overlay using the actual published build on each target
OS. The initial targets support `win-x64`, `linux-x64`, and `osx-x64`.

Next, define achievement API names in Steamworks and connect a first gameplay
milestone through the platform service. Cloud save integration can be planned
around the existing save-file location separately.

References: [Steamworks.NET installation](https://steamworks.github.io/installation/),
[Valve's API overview](https://partner.steamgames.com/doc/sdk/api).
