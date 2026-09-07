# Headless progression simulator

Run a fresh automated playthrough in seconds, without opening a window or reading/writing player saves. Produces estimated completion time, a purchase/prestige timeline, outstanding upgrades, and long gaps in progression.

From the game directory:

```sh
dotnet build --no-restore
dotnet run --project Simulation -- --output Simulation/results/baseline
```

The simulator references the built game, just like the existing persistence checks. Rebuild the game after changing C# rules or upgrade definition base values. Button costs/values/prerequisites are loaded from JSON on each build of the simulator. `--data Content/Data` reads that directory directly, useful for comparing edited costs without rebuilding. There is no rendering, content loading pipeline, input polling, or save access during simulation itself.

Outputs:

- `report.md`: completion, longest waits between purchases, longest waits for a **previously unpurchased level**, and remaining upgrades.
- `timeline.csv`: every purchase and prestige, timestamp, run, cost, level, preceding wait, and recent earned red gems per second. A prestige row's cost column is the purple reward.
- `summary.json`: settings, completion milestones, balances, pending levels/requirements, and data warnings.

“Ever purchased” preserves the highest level bought across resets. “Currently maxed” requires all regular, ability, and meta upgrade levels at once. Repeatable prestige and ability refund actions are excluded from completion. Missing-definition editor placeholders are excluded and reported; the current data contains `abilities:NB1`.

## Player policy

The bot buys the cheapest **affordable** unlocked level, comparing the raw listed cost across currencies and breaking ties by tree/node ID. It buys every upgrade, including upgrades with no modeled income effect. It does not reserve money or optimize return on investment. Purchases and menus take zero time by default; `--purchase-seconds 1` adds one second of paused menu time to every purchase. `--step` controls the decision interval while earning money.

It buys Expand Space when affordable, which resets regular upgrades and awards purple based on run earnings plus loose gem value. Space levels, blue balance, ability levels, and meta levels persist. After all five space levels, it uses repeat prestige when the reward reaches `--prestige` and persistent upgrades remain. It stops resetting once those are maxed, then finishes the regular tree. Ability points come from purchasing AP nodes, including repeated purchases after resets; colored world gems still pay red currency.

This is one reproducible policy, **not an optimal completion time**. In particular, buying cheap upgrades can delay saving for a large income upgrade; a different prestige target can change results significantly.

## Accuracy and calibration

This is a deterministic expected-value economy model, **not the game's live ECS with rendering disabled**. Upgrade parsing, costs, increments, base stats, quality probability tables, and prestige reward calculation use the built game's code/data. Dependencies follow the game's purchase-state rules (BlockedBy unlocks after one purchased level); purple purchases respect per-level Expand Space requirements.

Ambient spawns, quality unlocks, lucky gems, clusters/core/motherlode/superclusters, showers/comets, passive income, fleet counts/speed/range/capacity/fuel/refueling, global fleet multipliers, and expected jackpot payouts contribute to modeled income. Gem count is capped; excess spawns are discarded. Loose gems retain their accumulated value across purchases and count toward prestige rewards.

Collection is approximated by average cargo cycles:

- One-way travel distance is `distance * 3.5 / CameraZoomScale`.
- A cycle includes outbound travel, return travel, and `capacity / (efficiency * range multiplier)` seconds gathering gems.
- Fuel uptime uses the live harvester's 2500 base fuel, distance-based consumption, and two-second base refueling. The bot requests refuel immediately.
- Deliveries are smoothed over time; cargo is treated as delivered immediately at that average rate. No individual gems, routes, collisions, density-dependent searching, or delivery animation are simulated.
- Home-base collection is an approximate `efficiency * range multiplier` gems/sec. `--clicks` adds manually collected gems/sec, bounded by available gems.
- Gem Spawner is equipped first when a slot exists and fires on cooldown, including rings and fleet emitters. Its generated gems enter the shared pool.

Other active abilities (magnets, chain magnetizers, drones), multicast, spatial merger behavior, ship specialization milestones (launch thrusters, scanner, quantum cargo, chain collection, warp, return gate), resonance, entanglement, and home-base partial refueling **do not contribute extra income in this version**. Monochrome veins do not change expected value. These nodes still cost money and count toward completion. Click upgrades and targeting strategies are represented only through your chosen collection settings. Random-seed variation is not modeled. Human/menu time is included only through the optional fixed purchase delay; prestige animation time is omitted. Do not treat the result as a measured player completion time or a strict upper/lower bound.

Use observed in-game income to tune distance/efficiency and compare scenarios. Defaults describe an attentive player who requests refuels but does not manually collect gems. A player who leaves the game unattended before Auto Refuel will do worse than this model.

```sh
# Slower encounters / more travel
dotnet run --project Simulation -- --efficiency 0.35 --distance 300 --output Simulation/results/slow
# Faster encounters / some manual collection
dotnet run --project Simulation -- --efficiency 1 --distance 120 --clicks 2 --output Simulation/results/active
# Include one second of paused interaction time per purchase
dotnet run --project Simulation -- --purchase-seconds 1 --output Simulation/results/menu-time
# Compare prestige policy
dotnet run --project Simulation -- --prestige 25 --output Simulation/results/prestige25
# Check integration sensitivity against the default 2-second step
dotnet run --project Simulation -- --step 0.5 --output Simulation/results/fine
# Stop after two simulated hours and inspect what's unfinished
dotnet run --project Simulation -- --hours 2 --grind 120 --output Simulation/results/early
# Transaction, dependency, cap/value conservation, and prestige checks
dotnet run --project Simulation -- --self-test
```

All times are seconds except `--hours`. Default time limit is 100 hours, grind threshold is 300 seconds, repeat prestige target is 10 purple, and output directory is `Simulation/results`. Reports include the unfinished trailing wait if the time limit is reached. An “available” remaining upgrade may simply lack currency; false means a dependency or space requirement is still unmet. Settings must be finite and positive (clicks can be zero; efficiency cannot exceed one).

Changing `--step` can shift purchase ordering and later prestige paths. Use small steps when comparing close balance changes. Reports under `results/` are ignored by Git.
