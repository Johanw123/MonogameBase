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
