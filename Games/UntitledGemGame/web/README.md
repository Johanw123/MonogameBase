# Web export

The browser build uses KNI/BlazorGL and the current game source. Install the
.NET 10 SDK and a Windows .NET 8 runtime for the KNI content compiler.
The `wasm-tools` workload is optional for this interpreted build.

On Windows, export from the game directory:

```powershell
dotnet publish web/UntitledGemGame.web.csproj -c Release -m:1 -o web/export
```

On Linux, install Wine and Windows .NET 8 in the prefix used for MonoGame
shader compilation, then run:

```sh
MGFXC_WINE_PATH="$HOME/.winemonogame" ./export-web.sh
```

The script also detects an existing `$HOME/.winemonogame` prefix. KNI's
shader and audio tools require Windows native libraries; running MGCB directly
under Linux .NET fails on `d3dcompiler_47.dll`. The build runs the same KNI
compiler through Wine on Linux. The prefix must contain 64-bit Windows .NET 8
and Microsoft's `d3dcompiler_47.dll`.

`./export-web.sh` also creates **`web/beyond-the-belt-itch.zip`**. Upload that
ZIP to itch.io and mark it as an HTML build. It keeps `index.html` at the root
and includes `_framework`, `_content`, and `Content`. It omits duplicate `.br`
and `.gz` compression copies and directory entries to stay within itch.io's
1,000-file limit. The uncompressed originals remain available to the browser.

After publishing on Windows, or to package an existing publish without rebuilding:

```sh
dotnet web/ContentBuilder/bin/Release/net10.0/ContentBuilder.dll --package-itch web/export/wwwroot web/beyond-the-belt-itch.zip
```

For other hosts, upload the **contents of `web/export/wwwroot`**. Paths use a
relative base so the game can also be hosted under a subdirectory.

To test locally, serve the output over HTTP:

```sh
python -m http.server 8080 --directory web/export/wwwroot
```

Open `http://localhost:8080`. Click in the game to allow browser audio.
Check New Game, collecting gems, upgrades and abilities, settings, changing
languages, resizing the browser, and reloading to continue saved progress.
Progress and preferences are stored in localStorage on the hosting origin.

The content build derives the asset list from the desktop content collection
and adds WebGL effects and browser font/translation files. It packs raw files
and compiled XNBs into `Content/assets.zip`. Startup downloads this bundle
asynchronously, reports progress, and unpacks it into the browser's virtual
filesystem in batches. The browser TitleContainer reads those files locally;
it never runs KNI's synchronous HTTP requests during the game loop. Music
remains separate for browser streaming. The itch packager omits the individual
files already in the bundle, so the upload does not duplicate them.

Gameplay textures and shaders initialize at most once per browser frame.
New Game/Continue construct the upgrade UI in small batches and warm the gem
pool between frames, with a four-millisecond work budget and loading status.
The web build skips the forced startup garbage collection and BaseGame's
cosmetic 1.5-second loading delay. Desktop initialization drains the same setup
steps immediately and retains its garbage collection.

Shader and audio compilation must succeed before the export can be used.
`SkipWebContent=true` reuses previously compiled assets and rebuilds the bundle;
it cannot create a playable export when those compiled assets are missing.

Shaders use an explicit `__KNIFX__` branch with the full SM4 profile. KNI uses
that profile for reflection and translates its GL shaders through SM3. Using
the desktop `level_9_1` fallback instead restricts the generated pixel shader
to SM2 limits and prevents the game's more complex effects from compiling.
The browser graphics device uses KNI's `FL10_0` profile to accept the SM4
metadata; `HiDef` accepts only SM3. This requires WebGL2 with support for
textures of at least 8192 pixels. The PC device keeps its existing profile.
KNI's browser blend-state mapper also rejects `Max`, so web glows use additive
blending; overlapping Arc Harpoon segments may appear brighter than on PC.

Browser support uses the same framework source through `Framework/Framework.csproj`
with a browser asset loader and field-font adapters. Web HUD and tooltip text
use the existing combined outline/fill shader pass: KNI caches vertex bindings
per vertex shader, so separate passes with different active color inputs can
reuse the outline color for the fill. Tooltip sizes use actual font metrics
and wrapping, as on desktop. `Generators/UpgradesGenerator.csproj` builds the
shared upgrade generator into a separate output directory. Both avoid sharing
`bin`/`obj` files with the desktop projects. The GumService alias accounts for
the namespace difference between the desktop Gum and KNI Gum releases.

## Verification limits in the restricted agent environment

The managed Release publish and browser startup/persistence unit checks were
verified using cached dependencies. This sandbox prohibits the sockets needed
by Wine and a live browser. A complete shader/audio content compilation and a
live browser playtest still need to be run outside that sandbox.

### Menu rendering performance

The browser framework variant (`Framework/BaseGame.cs`) allocates world and HUD
render targets at 1920×1080. HUD layout and input remain at 3840×2160: a scoped
half-scale camera, sprite transform and field-font adapter scale drawing only.
The camera and screen-layer settings restore after drawing, including exceptions.
Desktop keeps its existing 4K targets and shader effects.

Both builds cull custom borders and link segments against their actual draw
matrix, cache link geometry (invalidating moved endpoints/midpoints), and reuse
glow blend states. Link animations advance even while their segments are culled.
Browser text also caches bounds and skips layout/drawing for offscreen labels.
The browser shader branches use tight static glows, hover highlights and simple
purchase pulses instead of procedural border particles and wobbling link glows.

After changing shaders, run the full `./export-web.sh`: `SkipWebContent=true`
retains old shader XNBs and cannot validate the new visual effects. Test the
resulting itch ZIP in a browser, including panning, zooming, hover/tooltips,
talents, systems, HUD commands and resizing/fullscreen.
