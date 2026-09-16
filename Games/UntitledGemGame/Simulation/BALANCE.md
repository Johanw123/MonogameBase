# Regular upgrade progression

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
