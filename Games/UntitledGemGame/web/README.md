# Browser build bring-up

Status: browser startup and bitmap text have been verified by the developer.
Upgrade-menu rendering, input, and performance are under investigation.

From the game directory, desktop development still uses:

```sh
dotnet run
```

Select the browser project explicitly (also from the game directory):

```sh
dotnet run --project web/UntitledGemGame.web.csproj
```

From the repository root, use:

```sh
dotnet run --project Games/UntitledGemGame/web/UntitledGemGame.web.csproj
```

Open the development server's printed URL. The browser project uses .NET 8,
KNI/BlazorGL, and `KNI_WEB`. Progress saving/loading is disabled and Continue is
hidden. Settings start at defaults and are not saved. Desktop persistence is
unchanged. Browser logging uses the console; desktop-only blur is disabled.

The build stages shared assets into `web/wwwroot` (generated and ignored by Git).
Edit the original `wwwroot`, Gum project, or other source content, not this staged
copy. It reuses the existing KNI XNB assets, with raw PNG fallbacks for newer ship and engine
textures, the logo, upgrade panel pixels, and the lilac gem spritesheet. It does not run the obsolete desktop `Content.mgcb`.
The developer has rebuilt the five gameplay shaders successfully using the script below.
Browser HUD/custom text uses the existing Arial bitmap atlas for measurement,
wrapping, and drawing instead of the desktop distance-field font. Audio and text
rendering still need browser validation.

JapeFramework's web project uses `obj/web` and `bin/web`, keeping its restore and
build outputs separate from the desktop project.

## Remaining verification

Package restore succeeded in the developer terminal. Source compilation passes,
but WebAssembly packaging fails in the restricted execution environment when
MSBuild starts its task host (`MSB4216`). Local socket creation is also denied
there, preventing a local server/browser test. Run the command above in a normal
terminal to verify packaging and startup.

Then test menu rendering, New Game, gem collection, upgrades, resize, and audio.
Reloading should start without saved progress. Missing assets, old shader
parameters, or runtime KNI differences may still require fixes.

## Browser rendering and shader rebuild

The web world renders at 1920x1080. The HUD stays at 3840x2160, matching its
layout and mouse coordinates and the desktop GUI rendering path. Presentation
scales both targets to the window. The browser skips world drawing while upgrades
are open, independently of dimming; simulation continues.

Temporary `WEB MENU` console lines report draw FPS and average CPU time for input,
HUD drawing, borders, and lines every two seconds. Border/line times are included
in draw time; these are CPU submission timings, not GPU measurements. Compare a
few lines with the menu closed and open when reporting performance issues.

Font PNGs contain straight alpha. The browser text blend state multiplies RGB by
source alpha and composites premultiplied output into the HUD target. Custom text
uses the same state. Text outlines and HUD mipmap generation remain disabled.

The desktop-style gem and SDF effect paths are restored. Rebuild the old browser
shader binaries from current shared sources before checking their appearance:

```sh
bash Games/UntitledGemGame/web/rebuild-shaders.sh
```

Run that from the repository root in a normal terminal. It requires Windows .NET 8
in the Wine prefix and the restored KNI 4.2.9001 compiler package. It builds five
shaders and only replaces browser XNBs after all succeed. The native Linux build
of this KNI compiler cannot load d3dcompiler_47.dll; it must run through Wine.
The current environment cannot run Wine services, so the shader rebuild and these
latest visual/performance changes still require developer verification.
