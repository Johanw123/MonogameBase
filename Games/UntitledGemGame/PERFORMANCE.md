The large-population changes remove repeated work from stationary gems, targeting, and sprite submission.

- `GemSpatialIndex` maintains exact spatial cells incrementally. Spawning, moving, claiming, chain release and recycling update membership; ordinary frames do not rebuild the grid. Claimed slots remain allocated until their animation finishes. Fleet workers only query and atomically claim; structural removals happen after workers join.
- `UpdateSystem2` tracks awake gems in a dense list. Spawn/pickup animations and destruction wake a gem. Settled gems sleep, and mouse interaction queries only the cursor rectangle. Prestige, play-area changes and active magnets wake the population when needed. Clicked gems deliver on arrival without re-entering spatial queries.
- `GemRenderBatch` retains quads in GPU buffers, in pages of 4,096 gems. Animation and hover changes refresh affected geometry; unchanged pages are neither rebuilt nor uploaded. Geometry updates preserve the existing gem shader and texture. Collected gems are removed without changing the relative drawing order of survivors. Removals are compacted once per draw by copying existing quads; this can also dirty later pages, so collection-heavy scenes retain less of the upload savings. Buffers are released when the game screen unloads.
- Cluster targeting uses cached position/value totals for occupied cells. Weighted random cluster selection uses a cumulative distribution and binary search. Centroids divide coordinate sums by gem count, fixing high-value clusters pointing toward the origin. Value-based target scoring uses wide arithmetic.
- Nearby harvesting reads positions directly from the index, skips returning/departing ships and respects remaining primary cargo capacity. The previous implementation could claim an entire pile regardless of capacity. Queries use the actual pickup radius and have no fixed candidate-buffer limit. Small fleets avoid parallel scheduling overhead.
- Merger buffers are reused and merging runs at most five times per second (up to 0.2 seconds of extra detection latency). Chain-line rendering no longer copies the dictionary every frame.
- The internal live-gem slot limit increased from 50,000 to 500,000. Normal spawning still respects the configured upgrade limit; animation slots also count against internal capacity.

Measurements on 2026-09-08, .NET 10.0.10, eight logical CPUs. Both implementations were compiled with optimization enabled, with tiered compilation disabled for the benchmark process. Each result is the median of seven runs after three warmups. Positions are seeded and distributed over 3,840 × 2,000 world units. The original framework hash is compiled directly from its unchanged source in the benchmark baseline project, with matching capacity for each population.

| Gems | Index maintenance + 1,000 radius-30 queries, original → new | 100 scored cluster targets, original → new | 1,000 weighted cluster targets, original → new |
|---:|---:|---:|---:|
| 40,000 | 1.271 → 1.651 ms | 29.335 → 16.683 ms | 37.596 → 0.122 ms |
| 250,000 | 7.690 → 4.933 ms | 312.258 → 19.767 ms | 29.506 → 0.119 ms |
| 500,000 | 17.416 → 10.450 ms | 753.710 → 19.724 ms | 31.052 → 0.120 ms |

The new query workload allocates zero managed bytes after warmup. At 40,000 gems the isolated query workload is slightly slower; its extra cell lookup/traversal cost is outweighed at larger populations. These measurements exclude the removed per-gem updates and sprite geometry rebuilding, ECS lookup savings, actual collection, GPU shading, UI and audio. They are not whole-game frame rates. Absolute timings vary with host load.

Validation passed:

- 8,000 randomized add/move/claim/release/recycle operations compared against brute-force queries, including negative coordinates, boundaries, growing cell storage, centroid correctness and available-list accounting.
- 120,000 gems in one pile, small output buffers, simultaneous claims by 16 workers, duplicate cleanup and reuse of every slot.
- Actual ECS registration/removal and pooled reuse, verifying that animation and prestige destruction wake sleeping gems exactly once.
- Chain effects track a gem lifetime as well as its entity ID. Tests cover resetting a pooled gem before deferred ECS removal, reusing the same object/ID/spatial slot, normal completion and cancellation, and preserving replacement claims and effect lines.
- All 603 existing persistence/gameplay checks. Two stale fixtures now use the configured first-point price and explicitly set the intended upgrade gate data, rather than relying on old balance values or literal JSON formatting.
- An offscreen 4,100-gem comparison against SpriteBatch using the repository's existing compiled gem shader: no pixels differed by more than 2/255 per RGB channel. Covered scale, tint, rotation, texture regions, flipping, removal across page boundaries, overlapping gems, repeated collection/respawning with pooled sprites and IDs, and buffer reuse. An unrelated collection must not change which gem is visible on top of a pile; the original swap-removal cache failed this case. An unchanged frame uploaded zero pages.

Reproduce the build, checks and CPU benchmark from the game directory:

```sh
dotnet build UntitledGemGame.csproj --no-restore -m:1 -p:BuildProjectReferences=false -p:Optimize=true
dotnet run --project Tests/PersistenceChecks.csproj -p:Optimize=true
DOTNET_TieredCompilation=0 dotnet run --project Tests/PersistenceChecks.csproj --no-build -- --benchmark
```

The standalone spatial/lifecycle checks can also run with `-- --spatial-check`. The build's existing upgrade generator writes diagnostic files outside the game directory, so a restricted sandbox needs build permission.

The optional Linux graphics comparison creates an offscreen MonoGame device and never loads player saves. Pass a compatible compiled gem shader:

```sh
SDL_VIDEODRIVER=offscreen LD_LIBRARY_PATH="$PWD/bin/Debug/net10.0/runtimes/linux-x64/native" \
  dotnet run --project Tests/PersistenceChecks.csproj --no-build -- \
  --render-check bin/Release/net10.0/Content/Shaders/GeneratedShaders/GemShader.mgfx
```

Magnetizers remain enabled and functional. Their current attraction loop still compares each loose gem against active magnets, so disabling them for the demo makes the sleeping path more effective. Very large overlapping gem populations can still be GPU-bound: the existing shader performs multiple texture samples per fragment, and caching geometry does not remove overdraw. Animated harvester sprites and return lines also still cost work per visible ship. No full-game FPS target has been established for 500,000 gems.

The developer overlay displays queryable gems, updating gems and uploaded gem render pages. In a settled scene with no active magnets, the updating count should fall to zero and the upload count should remain zero until a visual changes. Use those counters alongside CPU/GPU profiling when evaluating a particular fleet size and resolution.

Geometry updates (2026-09-26) now queue each dirty render slot once and rebuild it
immediately before drawing. Spawn motion, hover changes, and multiple simulation
ticks share that final rebuild. Removed entries are skipped before stable
compaction. Unrotated gems avoid trigonometry and rotation arithmetic. Ordinary
gem updates only invalidate geometry when position or scale changes; explicit
color/chain changes still invalidate it directly.

Play-area constraints reuse their previous result while position, animation
destination, and camera bounds are unchanged. Movement and developer camera
changes still clamp correctly; pooled reuse invalidates the cache. Spatial
positions are synchronized only when different, and collection radii only when
scale changes (including the first update after spawn/reuse). Magnet behavior is
unchanged. The overlay reports rebuilt quads as well as uploaded pages.

Run `./trace.sh` for a Release CPU trace. Close any Debug game first: the script
rejects an existing process unless its command line identifies a Release build.
It launches Release when no game is running. Use the same save, scene, resolution,
and frame-pacing settings when comparing captures; the previous Debug recording
is not a valid before/after performance baseline for Release.

For GPU execution timing, install your distribution's `apitrace` package and run
`./trace-gpu.sh`. This builds Release and captures OpenGL commands while you play;
close the game normally after a short reproduction. It then replays the capture
with `--pgpu --pcpu`, saving per-frame/draw-call timing to
`traces/gpu_TIMESTAMP.profile.txt` alongside the `.trace`. The game uses its normal
saves. Capture files can grow quickly. Replay an existing capture with
`./trace-gpu.sh --replay traces/gpu_TIMESTAMP.trace`, or open it in `qapitrace`.
These are GPU replay measurements, not live CPU/GPU timeline correlation or GPU
utilization percentages. See the [apitrace profiling documentation](https://github.com/apitrace/apitrace/blob/master/docs/USAGE.markdown#profiling-a-trace).

The GPU script defaults to apitrace's EGL wrapper and forces SDL to use EGL on
X11 as well, so session-variable detection cannot select the wrong tracer.
`GPU_TRACE_API=gl ./trace-gpu.sh` explicitly selects X11/GLX for both SDL and
apitrace. These overrides apply only to the captured process. Capture output
is retained in `.capture.log`, and replay diagnostics in `.replay.log`. Missing
captures and failed/empty timing reports now stop with an error instead of
leaving an apparently successful empty profile.

GPU timing replay uses `--headless` to avoid visible EGL window-resize assertions
on the desktop compositor. The recorded rendering commands are still replayed.

Gem surface shader (2026-09-26): the renderer generates one 26×38 RGBA data texture
from the loaded grayscale sprite on first use, and releases it with the gem
renderer. R stores the tint weight, G the white facet contribution, B a two-texel
inner-outline mask, and A silhouette coverage. Static contour and highlight work
is baked once. The shader now uses one bilinear sample and color arithmetic;
Sobel/neighbor sampling and animated shine are removed. Outline selection still
uses vertex alpha for hovered/lucky/gilded gems; vertex alpha zero is not opacity.
The outline is now a solid, filtered inner border instead of an additive halo.
The source artwork, gem geometry, collision dimensions and shared batch remain
the same. The packed texture is numeric data, uploaded directly without an image
pipeline premultiplication pass.

An isolated Release comparison rendered 10,000 gems at 1024×512, with 20% outlined,
for 40 frames alternating old/new shaders. Headless apitrace GPU replay, excluding
the first five warmup frames, measured median gem draw times of 0.447488 ms old
and 0.052224 ms new (about 8.6× faster in this specific workload). This includes
the removal of shine and the deliberate visual simplification. It is not a
whole-game speedup estimate. No change is made to bloom or other shaders.

Validation: Release build and shader compilation, all 903 persistence checks,
surface coverage/premultiplied-weight checks, and the existing 4,100-gem rendering
comparisons passed. The reference comparison now binds the same packed surface
to SpriteBatch as the cached renderer. It verifies geometry/batching equivalence,
not pixel equality with the intentionally different old shader.

To reproduce the visual comparison and alternating profiling workload, keep a
compiled copy of the old shader before replacing it, compile the new shader, and
run the standalone check (it does not load game state or saves):

```sh
SDL_VIDEODRIVER=offscreen LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/runtimes/linux-x64/native" \
  dotnet Tests/bin/Release/net10.0/PersistenceChecks.dll --gem-shader-check \
  /path/to/old.mgfx /path/to/new.mgfx Content/Textures/Gems/GemGrayStatic.png /tmp/gem-preview
```

It writes before.png and after.png. Rows show scales 0.5, 1, 2 and 4; paired columns
show ordinary/outlined gems in blue, green, purple and gold. Capture this command
with `apitrace trace --api egl` and replay with `--headless --pgpu` for GPU timings.

Manual Homebase Magnetizer uses a separate bounded main-thread pass over stable
spatial slots: at most 8,192 slots per simulation frame. It does not wake sleeping
gems or add a magnet source to the gem-by-magnet search. Movement updates the
existing spatial index and retained render geometry directly, without per-gem
particles, queries, or allocations. Three inward rings provide shared feedback.
Slot generations protect recycled entries; spawned gems start attraction at their
birth, and reset cancels deferred movement. Elapsed-time compensation and a final
bounded pass preserve the total pull for late batches. At very high populations,
each gem moves less frequently; the work cap trades motion smoothness for a
bounded frame cost. Existing automatic magnets yield while the command is active.

The command benchmark at 100,000 and 500,000 gems measured roughly 2.9 ms median
and under 3.8 ms p95 per active batch for CPU movement/index work in this environment.
Spatial-cell growth still allocates; this is not a full-game CPU/GPU or FPS guarantee.
Collector Swarm always spawns eight drones, scaling stats/value rather than entity
count. Crystal Shatter emits 24 shards, scaling their value with current quality
and fleet cargo capacity, and queues them behind the existing population cap.

```sh
DOTNET_TieredCompilation=0 dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --manual-gravity-benchmark
```

## Whole-game benchmark and the October 2026 pass

`./benchmark.sh` measures CPU frame time over the scenes in `tools/benchmark/scenes`:
progression stages, gem and fleet loads (fleets up to the expected maximum of about
100 ships), every menu and the big events. Each scene runs the Release build through
capture mode with `"benchmark": true`: no video, an unthrottled game loop, and frame,
update and draw times, GC counts and pauses, draw calls and gem counts in
`<scene>.capture.json`. Results go to `benchmarks/<timestamp>/` (git-ignored) with
`report.md` from `tools/benchmark/report.py`. `--trace` adds dotnet-trace profiles with
per-phase tables and hotspots, `--allocs` adds allocation and GC summaries
(`tools/benchmark/allocs`), and `--only a,b` picks scenes. Compare two runs with
`python3 tools/benchmark/compare.py benchmarks/<before> benchmarks/<after>`. Compare
only runs from the same machine; identical runs differ by a few percent. Capture
renders the world at 4K pixel area, but the endgame and fleet scenes measured the same
at 1080p, so the numbers are CPU costs. The benchmarks run the JIT Release build;
players get the NativeAOT build.

The first benchmark (2026-10-07) found the endgame at 16.6 ms per frame, 42% of frames
over 16.7 ms, with garbage-collection stutter in every late scene. The fixes, largest
first:

- Apos.Shapes 0.6.8 re-uploaded its whole vertex array on every `ShapeBatch.End()`
  without a discard hint, making the driver wait for the GPU each time. It is now
  vendored in `ThirdParty/Apos.Shapes` with the upstream 0.7.0 upload fix and faster
  line and circle vertex building (see its README).
- Gem entities are recycled. Creating and destroying an ECS entity per gem made
  MonoGame.Extended rescan every entity for every system and box ints in a quadratic
  `Bag.Contains` (92% of all allocations in the endgame). A collected gem now leaves the
  spatial index, render batch and update list and parks its entity
  (`EntityFactory.ParkGem`); the next spawn reuses it with its components.
  `UpdateSystem2` keeps a dense list of live gems for whole-field work, since
  `ActiveEntities` also holds parked ones.
- Gum's `Draw(layer)` overloads never reset the renderer's per-frame render state
  record, which grew every frame. `RenderGuiSystem` resets it once per frame.
- The talent tree's tier lookups and spent points are cached until a level changes;
  the menu recomputed the whole tree for every button every frame.
- The extract panel refreshes its prestige estimate four times a second instead of
  visiting every gem every frame. Extracting still uses the exact value.
- `PlayAreaBounds.ForCamera` and the cameras' view-projection matrix are cached per
  camera state; bursts asked for the bounds once per spawned gem, and every effect
  built a new frustum.
- Bursts larger than 3,000 gems spawn over the following frames, in order
  (`SpawnDebris`); waiting gems count against the field limit and are saved.
- Arc Harpoon layers are drawn once for all harpoons (5 batches instead of 5 per
  harpoon plus 2), talent labels share one stroke and one fill pass
  (`FontManager.BeginFieldFonts`), and HUD text no longer builds expression trees.
- Magnets wake only the gems in their reach (spatial queries), launch glides land on
  their final point once slower than 12 units/s, spatial moves skip the cell lookup
  when a gem stays in its cell, and the gem render batch compacts in runs.

Afterwards the endgame measured about 7 ms per frame with roughly one dropped frame per
15 seconds, and steady play allocates a few MB per second, with a gen0/gen1 collection
every few seconds that takes 2-3 ms. The first one or two collections after loading a
save still take 11-16 ms, because they promote the freshly loaded world and the gem
entities created while the restored field fills. Extraction still takes 25-35 ms for its
one frame. The rest of each frame is mostly gem simulation and drawing, which scale with
the number of gems moving at once (the endgame keeps about 14,000 in flight), and
waiting on the GPU in Present.

## Gem animation on the GPU (October 2026)

Every gem animation that follows a fixed curve is drawn by the gem shader
(`Content/Shaders/GemShader.fx`) instead of being stepped on the CPU each frame:

- Spawning (`Entities/GemFlight.cs`): the launch glide (velocity decaying at e^-8t) and the
  grow-in (e^-5t). The CPU puts the gem on its landing point at full size at once.
- Collection (`Gem.BeginGpuCollect`): the burst away from the collector, the homing (2500/s²
  up to 800/s) and the shrink. Collectors (the home base and ships, `GemCollectors`, 128 slots)
  send their live positions to the shader each frame. UpdateSystem2 delivers clicked gems
  when they reach the base's range and retires each gem once it has vanished.
- Core fracture swallows (`Gem.Swallow`): the reversed launch into the planet.
- Graviton Cascade chain pulls (`Gem.BeginGpuPull`): the quintic ease to the chain's target.
  The chain line follows the same curve on the CPU; the gem is placed on the target when
  the chain completes.

Gem quads carry the extra data (`GemVertex`: a float4 and a float2 beyond position, colour
and texture coordinate), and `Gem.FlightClock` (simulation seconds) is the shared clock.
Anything that moves or claims a gem while the shader animates it first hands the motion
back to the CPU from where the gem appears (`TakeOverGlide`, `TakeOverFlight`,
`TakeOverPull`), so nothing jumps. Movers (magnets, gravity, the play-area clamp) only take
over the glide and the shader keeps growing the gem; a chain that grabs a gem still growing
settles it at full size, hidden by the chain's yank. Without the shader's support (an old
compiled shader, or the tests) the original CPU animations run.

Before this, the endgame woke 10,000-14,000 gems and rebuilt as many quads every frame, and
a big refill stalled while ~27,000 gems flew out. Afterwards the endgame updates almost no
gems per frame and rebuilds a few hundred quads. A rebuilt AOT content pipeline must
compile the new GemShader.fx; prebuilt shaders from before this change fall back to the
CPU animations.

Loading: `EntityFactory.WarmGemPool` creates this run's gem entities (up to the field limit,
at most 120,000) while the screen loads, `OrbitSkin.Preload` loads the menu skin images, and
one full garbage collection runs before play starts. The first seconds after a load then
have no collections. In the JIT build, the end of the crash-landing intro still compiles most
gameplay code on first use (the `05-load` scene); the NativeAOT build does not compile.

Extraction: the prestige collapse pulls the field home 3,000 gems per frame, and upgrade
resets no longer log every button state (the logger writes synchronously).

Measured afterwards on the same machine (Ryzen 7 5700X), every gameplay scene ran its 15
seconds without a frame over 16.7 ms: the endgame at 4.7 ms per frame (16.6 ms in the first
benchmark), 50,000 gems with a 100-ship fleet at 4.6 ms, 100,000 gems at 4.6 ms, late game at
3.1 ms. Remaining one-offs are the first collection after a menu first opens (about 15 ms)
and, in the JIT build only, compiling code on first use.

## 50,000-gem field (October 2026)

The field cap (`MaxGemCount` in `Content/Data/upgrades.json`) went from 20,000 to 50,000.
To keep big fields cheap, gems are drawn instanced: one `GemInstance` per gem (76 bytes:
centre, rotation, extent, texture rectangle, colour and the animation data) with a shared
four-corner quad, instead of four 48-byte vertices. The shader builds the corners
(`GemShader.fx`). Uploads, rebuilds and the ordered compaction after collections move a
quarter of the data. `WarmGemPool` warms the whole cap when the save holds 5,000+ gems or
the player has extracted before, since those fields fill up.

Measured at the 50K cap: late game 3.4 ms per frame (3.1 ms at 20K), the endgame 6.4 ms with
about 39,000 gems churning (4.7 ms with 10,000), 50,000 gems with a 100-ship fleet 3.7 ms,
menus 3.4-4.7 ms. No scene had more than two frames over 16.7 ms in 15 seconds.
