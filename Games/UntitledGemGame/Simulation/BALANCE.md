# Regular upgrade progression

## Fleet consolidation (2026-10-05)

The harvester side now mirrors the weapons side: fewer, stronger nodes on a clean layout. It supersedes the fleet prices in "Live calibration and fleet tiers" below.

- **Layout.** One spine runs right from Home Base: Drifter → Seeker → Prospector → Trove Hunter, each as unlock then Count. Each class's Engines, Cargo and Delivery Value sit in a row beside its spine section, with its specials beyond. Rimrunners mirror Seekers below the spine. Fleet-wide Logistics sits above Home Base.
- **One node per stat per class** (69 fleet nodes → 39, the same as the weapons). Drifters, the starter class like the cannon, get deep 10-rank nodes. Every other class gets 5-rank nodes; Count adds two ships per rank.
- **Fuel and range are fleet-wide.** Max fuel and fuel efficiency only ever multiplied each other, so both became Fuel Tanks; per-class range became Tractor Scoops. Refuel speed is no longer a tree upgrade: refuelling takes 2 s, and the Refuel signal and Quick Coupler module still speed it up. Fleet Refuel, Dockside Reclamation and Emergency Autopilot (Core Shard) cover refuelling quality of life.
- **Prices.** Ranks grow ×2.7–2.8, like the weapons' fire power and sharper than the old ×1.6–2.2. Each class's nodes start near its unlock price, where Prospector and Trove Hunter nodes used to be far cheaper than their unlocks. Unlocks: Seeker/Rimrunner 50K, Prospector 750K, Trove Hunter 10M, alongside the Harpoon/Rockets/Railgun. Maxing a class costs a few hundred times its unlock, as maxing a weapon does. Fully upgrading the fleet costs about 5.9B (Trove Hunters 5.4B) instead of about 1.2 trillion.

**Why the late classes moved down.** Live-game measurements from the capture tool (progression presets, no clicking, preset meta replaced by the 23 real tier talents) found income levelling off at roughly 12–35M gems/min once the weapons are maxed (~25M). A fully maxed regular tree earned about the same with or without Prospectors and Trove Hunters; removing the Railgun cost about 20%. In the mid game the gem field keeps filling, so the fleet, not the weapons, is the bottleneck there. Late classes therefore pay off as mid-run collection capacity. At the old prices (Prospector 400M, Trove Hunter 12B with ranks to 240B) they were out of reach before income levelled off. The old presets' late-game incomes (100B+/min) came from legacy meta nodes that are no longer purchasable.

The single-run simulator now buys in this order: Laser → Harpoon → Rockets → Seeker/Rimrunner → Railgun → Prospector, with Trove Hunters in later, talent-boosted runs.

Balance pass: 2026-09-16. Prices remain economic gates: prestige makes late upgrades practical, but there is no mandatory prestige-count lock on regular purchases. Existing purchases are retained when loading saves; the quality-node migration is described below.

## Color and quality progression

Color unlocks now alternate with single-rank quality nodes on one branch:

Light Green → Quality → Blue → Quality → Teal → Quality → Lilac → Quality → Purple → Quality → Gold → Quality → Dark Blue → four final quality ranks.

There are ten purchasable quality ranks in total, plus the starting quality. At the minimum prerequisites, each new color has an immediate spawn chance: Light Green 8%, Blue 5%, Teal 5%, Lilac 2%, Purple 1%, Gold 1%, Dark Blue 0.5%. Rolls for still-locked colors pay red gems. Every intervening quality rank also increases the expected value of already unlocked colors, so it has an immediate benefit before the next unlock. The final four ranks improve the mix of all eight colors.

The six intervening quality purchases cost 100 / 800 / 4,000 / 16,000 / 60,000 / 300,000 red gems. Final ranks cost 2M / 8M / 32M / 128M. Color prices are retained. Spawn cooldown, capacity and gem value remain side branches; they no longer let colors bypass the quality path.

The old five-rank `GSQ1` is split across the intervening nodes when loading saves. Any ranks missing beneath an already unlocked color are supplied without a charge, ensuring that saved color unlocks work immediately. Subsequent loads do not grant extra ranks. Automated checks cover minimum-prerequisite spawn chances, probability totals, useful quality purchases, all combinations of old paid ranks and color progress, and the fully upgraded new branch.

## Live calibration and fleet tiers

The user completed all upgrades from a fresh save in about **50 minutes with roughly 10 prestiges**. This supersedes the earlier simulation estimates as the actual playtime benchmark. The calibration runs below used no clicking or a constant manual collection rate. The simulator now defaults to heavier early manual collection that tapers with fleet size (see README). It omits click area, cluster targeting and most active abilities; those differences have not yet been measured well enough to predict a new completion time.

This pass separates fleet price ranges so finishing a type is generally cheaper than entering the next one. It changes only harvester prices; effects, reveal rules, purchase prerequisites and prestige scaling are retained.

| Transition | Most expensive individual rank in previous type | First ship of next type | Entry / previous peak |
|---|---:|---:|---:|
| Standard → Advanced | 450,000 | 2,000,000 | 4.44x |
| Advanced → Expert | 32,000,000 | 400,000,000 | 12.5x |
| Expert → Ultimate | 2,500,000,000 | 12,000,000,000 | 4.8x |

The previous entry prices were 3,000, 500,000 and 50 million, allowing types to overlap heavily. All ranks, reinforcement nodes and specialization milestones are included in the tier comparison. Standard prices stay intact. Advanced and Expert entries now have their own rising count curves, and Ultimate reinforcement ends at 240 billion. Smaller support upgrades after an expensive entry provide a period of more frequent purchases.

This remains a price incentive: a player can choose to save for the next fleet early. No max-level prerequisite or prestige requirement was added. Existing purchases are retained without retroactive charges.

Validation: every first purchase of Advanced, Expert and Ultimate in the zero-click run and a flat 500-gems/sec manual-collection stress run occurred only after every rank and specialization for the preceding fleet was bought **in that same run**. The stress rate is deliberately an arbitrary high-throughput check, not an estimate of the player's click rate. Because this bot buys cheapest-first, these checks establish the intended price ordering rather than proving that every human will follow it. JSON validation also confirmed that this pass changed only prices, retained all ranks and prerequisites, and kept every node's costs increasing.

```sh
dotnet run --project Simulation -- --data Content/Data --hours 24 --clicks 0 --output Simulation/results/fleet-tiers-idle
dotnet run --project Simulation -- --data Content/Data --hours 24 --clicks 500 --output Simulation/results/fleet-tiers-click-stress
```

## Intended rhythm

- Keep the starter fleet, basic spawning and quality affordable. A player saving for the next fleet should still have useful smaller purchases available in the current type.
- Show a few distant goals early, with their real effects and prices. Previewing an upgrade does not satisfy its purchase prerequisite.
- Stretch Expert, Ultimate and the final passive-income branch across later runs. Higher ranks grow more expensive within each tier.
- Permanent value multipliers should turn previously daunting prices into achievable purchases. Deeper prestige runs should fund multiple permanent upgrades.

| Milestone | Revealed by first purchase of | Purchase prerequisite | Red cost |
|---|---|---|---:|
| Launch Thrusters | Harvester Count | Harvester Count | 25,000 |
| Quantum Cargo Hold | Harvester Count | Treasure Scanner | 25,000,000 |
| Cosmic Clusters | Cluster Gems | Gem Comet Frequency | 2,000,000,000 |
| Warp Drive | Advanced Count | Ultimate Speed | 4,000,000,000 |
| Return Gate | Warp Drive | Warp Drive | 32,000,000,000 |

Milestone buttons are enlarged. Their extra tooltips describe the progression role; revealed but blocked nodes also name the missing prerequisite. Reveals update on purchase as well as save restoration. Connections from still-invisible prerequisites stay hidden.

Advanced Count starts at 2 million and reaches 16 million on its first node; its reinforcement node ends at 32 million. Expert Count starts at 400 million and reaches 2.2 billion on its first node; its reinforcement node ends at 2.5 billion. Ultimate Count starts at 12 billion and reaches 80 billion, with final reinforcement costing 240 billion. Chain Collection costs 100 million. All stat increments remain unchanged.

## Supporting prestige scaling

The first purple gem still requires 100,000 run earnings. Rewards are now `floor((earnings / 100000)^0.3)`, up from exponent 0.2. For example, 100 million run earnings pay 7 purple instead of 3, and 10 billion pay 31 instead of 10. The reward preview uses the same function as the payout.

Gem Value Multiplier now reaches 3x / 6x / 12x / 24x / 48x across its five ranks (previously 2x through 6x). Fleet Refinery reaches 1.5x / 2.25x / 3.5x / 5.5x / 8.5x (previously 1.4x through 3x). Their purple prices and space requirements are unchanged. These are independent multipliers, combined with collection improvements and regular gem value.

Purchases and tooltip affordability now retain 64-bit prices. Fleet cargo also retains 64-bit value so stronger permanent scaling cannot wrap a loaded ship's cargo at 4.29 billion.

## Historical simulation results (2026-09-14, superseded prices)

The following results predate the fleet-tier changes above and were contradicted as live playtime predictions by the 50-minute playtest. They document model behavior only, not the expected length of the game.

These are deterministic economy estimates, not measured playthrough times. The bot buys the cheapest affordable upgrade or HUD ability point, expands immediately when affordable, then repeats prestige at a selected reward while meta upgrades remain. It does not optimize investments, reserve milestone costs or model most active abilities. See [README.md](README.md) for the full model and limitations.

The simulator was updated to use the current HUD ability-point shop and to stop resetting when all meta upgrades are bought. Earlier results from its obsolete AP-node policy are not a valid baseline.

| Scenario | Regular tree completion, excluding prestige actions |
|---|---:|
| Previous balance, prestige at 10 purple | 6.43 hours |
| Previous balance, no prestige | 4.71 hours |
| Revised balance, prestige at 10 purple | 4.63 hours |
| Revised balance, prestige at 3 purple | 4.64 hours |
| Revised balance, prestige at 25 purple | 7.64 hours |
| Revised balance, slower collection and longer travel | 8.04 hours |
| Revised balance, 0.5-second simulation steps | 4.60 hours |
| Revised balance, no prestige | Still missing the last two Ultimate Count ranks after 200 hours |

In that old model run, the first fleet preview appeared at about 1.6 minutes and Cosmic Clusters at 3.8 minutes. Thrusters were bought at about 58 minutes, the first expansion at 62 minutes, and Quantum Cargo at 178 minutes. The real player finished the game before the model's first expansion, demonstrating that these timestamps are not suitable pacing targets.

The bot's default run uses 34 resets, many very short near the end. This is a policy artifact worth checking in playtests: human purchase priorities and reset animation/menu time can change that result substantially. The fine-step check changes reset ordering/count but keeps regular completion close. The estimates intentionally do not claim full ability-tree completion; late ability prices remain a separate balance concern.

Run the same scenarios against the current prices from the game directory after building the game (these will not reproduce the historical table):

```sh
dotnet run --project Simulation -- --data Content/Data --hours 24 --output Simulation/results/final
dotnet run --project Simulation -- --data Content/Data --hours 24 --prestige 3 --output Simulation/results/final-prestige3
dotnet run --project Simulation -- --data Content/Data --hours 12 --prestige 25 --output Simulation/results/final-prestige25
dotnet run --project Simulation -- --data Content/Data --hours 24 --efficiency 0.35 --distance 300 --output Simulation/results/final-slow
dotnet run --project Simulation -- --data Content/Data --hours 8 --step 0.5 --output Simulation/results/final-fine
dotnet run --project Simulation -- --data Content/Data --hours 200 --no-prestige --output Simulation/results/final-no-prestige
```

In-game follow-up: check early preview visibility and tooltip space, the feel of saving for Thrusters, the acceleration after buying permanent value, and whether late fleet abilities remain satisfying once unlocked. The simulator omits their spatial effects, including warp, return gates and chain collection.

## Normal Gem Value prices

The three normal Gem Value nodes retain their entry prices and now multiply cost by 10 per rank to slow repeat purchases and income growth. Bonuses and prerequisites are unchanged.

| Node | Rank 1 | Rank 2 | Rank 3 | Rank 4 | Rank 5 |
|---|---:|---:|---:|---:|---:|
| GV1 | 150 | 1,500 | 15,000 | 150,000 | 1,500,000 |
| GV2 | 5,000 | 50,000 | 500,000 | 5,000,000 | 50,000,000 |
| GV3 | 500 | 5,000 | 50,000 | 500,000 | 5,000,000 |

Prices are red gems. These curves have been validated structurally; their effect on completion time still needs live play calibration.

## Ship Systems (2026-10-05)

Abilities are now **Ship Systems**, bought with **power cells** (the HUD point shop, formerly ability points) and unlocked by the tier-1 prestige talent **Auxiliary Power** (`SSU1`). Cells, their price curve and every system talent now reset when the core is extracted, so each run is a fresh build.

The three sprawling trees (148 nodes, 158 levels) were compressed into one fixed tab per system (46 nodes, 96 levels), laid out in `ShipSystems.cs`: a core talent that brings the system online, three columns that each chain down through a mechanic to a five-cell capstone, and tiers that need 1/4/7/10/14/19 cells spent above them in the same system. Repeated single-level nodes were merged into three-rank nodes whose ranks add up to the stat's old maximum, so a fully learned system matches the old fully upgraded one; only Arc Reach and Rapid Discharge (Storm Drones range and interval) were dropped. Ability Slot nodes are gone: online systems fill up to three HUD slots automatically.

**Not balanced yet.** Rank values, the 2/5-cell mechanic and capstone prices, the tier requirements and the cell price curve (`AbilityPointProgression`) are first guesses for the planned balance pass. A full system costs 46–49 cells, so the per-run cell budget decides how many capstones a run can reach.

### Core Drill (2026-10-05)

A fourth system, unbalanced like the rest. Its pod curves from the homebase around the planet and drills on the far side, away from the weapon lane. Base: 1.5 gems/s for 4 s every 8 s, two quality layers deeper than the cannon (Genesis Pulse: 5 gems every 5 s at cannon quality). `Tests/CoreDrillChecks.cs` keeps the trade-off: the drill must knock loose fewer gems per second than Genesis Pulse, both unlearned and fully learned (about 0.75 vs 1, and 35 vs 125 gems/s), while every drilled gem comes from deeper layers. Paths: Fault Lines → Tectonic Rupture (cracks leak gems, then burst all around the planet), Pressure Build → Core Tap (depth ramps while drilling, then a geyser of core gems four layers deeper at 3× value), Seismic Resonance → Hollow World (weapon hits mine deeper while it drills; every finished drill adds 3% to all planet hits for the run, up to 45%). With four systems the HUD now equips up to four.

## Prestige talent overhaul (2026-10-05)

The prestige tree was rebuilt around weapons and systems that set each other off, inspired by The Gnorp Apologue's talents (cross-unit triggers such as "gatling bullets can spawn rockets", talents that do several things at once). Every talent still costs one point, the tier gates (0/3/5/10/16 earlier points) and the free Expand Space per tier are unchanged. 27 talents:

| Tier | Talents |
|---|---|
| 1 Directives | Command Center, Auxiliary Power, Shipyard, Deep-Core Munitions, Overloaded Holds, **Thermite Rounds** |
| 2 Infrastructure | Deep Space Signals, Cargo Catapult, Matter Compression, Quantum Touch, **Lightning Rod** |
| 3 Reactions | **Beam Riders**, **Magma Detonation**, **Echo Protocol**, **Armed Escorts**, **Gravity Mastery**, Combo Supernova |
| 4 Convergence | Project Constellation, **Main Battery Relay**, **Drone Gunships**, **Kamikaze Drones**, **Command Nexus**, Phase Logistics |
| 5 Transcendence | **Shard Reactor**, **Signal Resonance**, **Planetary Overload**, Singularity Collapse, Weaponized Compression |

New or reworked (bold): Thermite Rounds (cannon hits leave molten craters; laser scars burn twice as long), Lightning Rod (cannon hits add pulses to the anchored harpoon; faster harpoon reload), Beam Riders (laser beams launch rockets that use every rocket upgrade), Magma Detonation (rockets and the Railgun burst scars and craters), Echo Protocol (replaces both multicast talents: 25% double cast, every system activation fires a cannon volley), Gravity Mastery (Mobile Singularity + Event Horizon), Main Battery Relay (the Railgun makes every weapon fire), Drone Gunships (drones fire cannon shells), Armed Escorts (fleet deliveries fire one cannon shell per fitted module: the Shipyard feeds the arsenal), Command Nexus (now includes Command Chain, no global cooldown), Shard Reactor (+20% weapon damage per unspent Core Shard; collecting a shard fires every weapon), Signal Resonance (+1% weapon gems per signal, deeper every 25), Planetary Overload (every 500 weapon gems: a quake ring and every weapon fires, at most every 8 s). Shipyard and Deep Space Signals are back in the tree; they had dropped out of the tiers and could not be bought.

Retired from the tree (definitions kept as legacy, unbuyable): Target Painter, Jackpot Haul, Fleet Requisition, Combined Arms, Resonance Cascade, Quantum Entanglement, Multicast Protocol, Multicast Mastery, Command Chain, Event Horizon. Presets and the simulator now only buy talents in the tree.

**Kamikaze Drones** (added later, Convergence) swaps the Drone Swarm ship system for the **Kamikaze Wing**, in the same tab slot: bomber drones (3, 12 s base cooldown) dive into the planet, each detonating for 12× the strongest weapon's fire power, one quality layer deeper. Blasts count as rocket hits (Incendiary Warheads, Magma Detonation), Drone Gunships fires a shell per launch, and drone count and drone cooldown signals apply. Its tree mirrors Core Drill's shape and 49-cell cost: Wing Size, Shaped Warheads and Rapid Rearm; Cluster Bombs (bomblets hop across the surface at 30% each), Volatile Payload (20% critical blasts at 3×) and Second Sortie (25% chance a blast launches another bomber); ranks for bomblet count, critical chance and power, sortie chance, depth and dive speed; and the capstones Firestorm (every blast leaves a molten crater), Doomsday Drone (the wing's last bomber hits 8× and quakes the planet) and Hive Mind (each blast takes 1 s off the other systems' cooldowns). Values are first guesses; a maxed wing is very strong.

Two new Core Shard upgrades build on the talents: **Incendiary Warheads** (rockets leave molten craters) and **Tesla Coil** (every harpoon pulse arcs to every molten scar and crater). Example builds: *Barrage* (Quad Lasers, Rocket Swarm, Beam Riders, Project Constellation, Main Battery Relay); *Firestorm* (Thermite Rounds, Gatling Cannon, Incendiary Warheads, Magma Detonation); *Storm* (Lightning Rod, Gatling Cannon or Drone Gunships, Tesla Coil, Thermite Rounds); *Systems* (Auxiliary Power, Echo Protocol, Drone Gunships, Core Drill Seismic Resonance); *Hoarder* (Shard Reactor holding Core Shards instead of spending them). All values are first guesses for the balance pass; the simulator does not model these effects.


## Core fractures replace run objectives (2026-10-05)

Weapons now deal **damage**: a hit's damage is what its fire power, shot count and yield bonuses used to make in gems, and each point knocks one gem loose while the field has room. Weapons keep firing at a full field (the Railgun and harpoon no longer hold their shots, the laser no longer stalls); the damage still counts, it just spills no gems. Run objectives are gone. Core Shards come from **core fractures** instead: when the damage dealt over the last minute reaches `CoreFracture.Threshold(n) = 1K × 4^n`, the planet shakes, swallows every loose gem, erupts with twice as many (at least five seconds of the damage that caused it) two layers below the strongest weapon, keeps a crack for the rest of the run, and releases a Core Shard to click (it flies home on its own after 12 s). Weapons, ships and ship systems freeze from the first tremor until the shard is out, and the eruption's shockwave blows the fleet out to the screen edges. The player is never shown the damage or the thresholds, so each fracture is a surprise. Each fracture also swells the planet by 7% (up to eight fractures, +56%): ships, weapons, debris and clicks use the new size at the eruption, the sprite balloons out to it, and gems left inside are pushed to the edge of the debris ring. Fractures and shards reset at extraction.

Measured live (capture presets, damage over a full minute): First upgrades 645/min, First prestige 1.2K, Developing fleet 12K, Mid game 90K–155K, Late game: fleet 128K, Late game: production 1.0M, Uber endgame 7.6M. The ×4 curve therefore pays about 2 shards by the developing fleet, 4 in mid game, 5 at the late-game snapshot and 6 once a run passes 1.02M (what the six objectives paid), and 7 at the extreme endgame, so no run affords all nine Core Shard upgrades. Builds that favor the fleet over weapons earn fewer shards; Shard Reactor (holding shards) and weapon talents earn more.

## Prestige loops (2026-10-09)

Prestige points come from sustained gem income on a lifetime ladder (`PrestigeProgression`). Pacing is now tuned with **autoplay playthroughs**: the capture tool's stand-in player (`Capture/Autoplay.cs`, scene key `autoplay`) plays the real game from a fresh save. It clicks about 3 times a second (Core Shards, the planet while the cannon is manual, gem clusters otherwise), fires fleet commands, shops every 10 s (Core Shard upgrades, then the cheapest affordable upgrade or power cell, then system talents), extracts after 5 minutes without a new point (runs of at least 15 minutes), and learns talents in the highest open tier. Five game hours take about 20 minutes at `time_scale` 60 with `benchmark: true`. Price-only models and the simulator missed clicking, Midas Touch and commands, and underestimated a real first run about 20×.

Before this tuning a first run earned 10 points in 23 minutes: income went from 4K/min at 5 minutes to 350K at 15 minutes, and talents barely raised it, so later loops added nothing. Changes:

- **Passive income removed for now**: the Gem Synthesizer, Synth Overclock and Tap Dynamo nodes are gone from the tree (their definitions stay; passive signals drop out at zero passive income).
- **Price inflation** (`PriceInflation`): every price above 20K is multiplied by price / 20K, at most ×300. It is baked into `upgrades_buttons.json` and applied to power cells and signal scans, so a run levels off (about 300K/min after 15 minutes in a first run).
- **Gem Lore**: a free reward in every talent tier. Each talent learned in the tier multiplies gem value by ×1.25, ×1.3, ×1.35, ×2, ×2 and ×2.2 (tiers 1–6), from the first extraction on. Gem value grows slowly at first and steeply later, and every point spent makes the next loop a little richer. Values are whole numbers, so the roll rounds the multiplied value at random in proportion.
- **Ladder** 100K × 1.6^n gems/min, with each step 2.5% steeper than the last (`GrowthSteepening`).
- **Core fractures** also steepen: ×4 per fracture, each step 25% more (1K, 4K, 20K, 125K, 977K, 9.5M).
- **Midas Touch** ×2 instead of ×3.

Final playthrough (5 hours, autoplay as above):

| Run | Start | Points | Total | Peak income | Gem Lore |
|---:|---:|---|---:|---:|---:|
| 1 | 0:00 | 2 (14, 17 min) | 2 | 257K | ×1 |
| 2 | 0:22 | 2 | 4 | 531K | ×2 |
| 3–6 | 0:37 | 1, 1, 1, 2 | 9 | 12.7M | ×6 |
| 7–11 | 1:37 | 1 each | 14 | 405M | ×118 |
| 12 | 2:53 | 2 | 16 | 1.5B | ×237 |
| 13–20 | 3:10 | 1 each (later in each run) | 24 | 3.3T | ×161K |

Tier 6 (20 points) arrives at about 4 hours 10 minutes. Core Shards per run go 2 → 3 → 4. Earlier tries: tier-sized multipliers (×2 per tier and up) made each new tier a burst and the gaps between them walls; per-talent factors of ×2.5–3 in tiers 5–6 sped the late game up to 2–3 points a loop. Retune with `FirstThreshold`/`ThresholdGrowth`/`GrowthSteepening`, `CoreExtraction.GemLore` and `CoreFracture.GrowthSteepening`, and check with a playthrough.
