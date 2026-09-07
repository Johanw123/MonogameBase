# Initial balance findings — 2026-09-07

These are estimates from the current expected-value simulator and its cheapest-affordable purchasing policy. They are not measured player playtimes. See [model assumptions](README.md), particularly the omitted spatial/ability effects and the refueling assumption.

| Scenario | Hours to all levels currently maxed | Prestige resets |
|---|---:|---:|
| Default: efficiency 0.65, distance 200, no clicks | 7.61 | 50 |
| Slow: efficiency 0.35, distance 300, no clicks | 12.91 | 50 |
| Active: efficiency 1, distance 120, 2 gems/sec manual collection | 4.35 | 50 |
| Default with finer 0.5-second timestep | 7.54 | 50 |
| Default plus 1 second paused menu time per purchase | 11.77 | 50 |

All scenarios reach 494 upgrade levels across 170 upgrade nodes. Repeatable prestige and ability-refund actions are excluded, as is the undefined `abilities:NB1` placeholder. There are no unreachable defined upgrade nodes under this policy with the current data.

## Rebuilding conceals the biggest progression gaps

The longest default waits for a **previously unpurchased level** are:

| Next new level | Wait | Reached at |
|---|---:|---:|
| Expert collection range (`regular:EHCR1`) | 68.23 minutes | 3.88 hours |
| Expert count (`regular:EHC2`) | 64.07 minutes | 5.10 hours |
| Expert count (`regular:EHC1`) | 61.93 minutes | 2.49 hours |

These gaps include buying old upgrades again after prestige. The longest gap between *any* two purchases is only 52 seconds. A purchase-frequency metric alone would miss the repetition.

The five space expansions occur at approximately 1.31, 2.60, 3.92, 5.31, and 5.74 hours. The bot subsequently performs 45 additional prestige resets over about 110 minutes before finishing the remaining regular levels. Those reset counts depend on the configured 10-purple prestige target and buying policy.

The baseline makes **14,989 purchases**, including rebuilding and prestige actions. Even one second of paused interaction per purchase adds about 4.16 hours. Actual UI usage, bulk buying, and player's upgrade choices will matter substantially.

## What to investigate first

1. Measure early and post-prestige income in the real game, then calibrate collection efficiency/travel distance. The 4.35–12.91-hour scenario spread is a sensitivity range, not a statistical confidence interval.
2. Examine whether the hour-long early rebuilds feel rewarding. Retaining some regular progress, strengthening early prestige rewards, or reducing rebuild costs are possible balance experiments; no balance data was changed here.
3. Compare repeat-prestige targets with `--prestige` before changing purple prices. Fifty resets is a result of this policy, not a proven minimum.
4. Compare manual purchase effort with actual UI behavior. Automation or bulk purchasing could reduce repetition even if earning rates stay the same.
5. Model/calibrate omitted abilities and spatial milestone effects before using these figures as a release playtime promise.

The finer timestep changes baseline completion by about 0.83%, so collection assumptions and purchasing policy dominate numerical timestep error in these runs. Full timelines and reports are generated under `Simulation/results/` (ignored by Git).

Validation: desktop build passed; 17 simulator checks passed, including dependency/space gates, initial income, gem cap/value conservation, prestige resets/rewards, retained ability levels, input validation, and time-limit handling.
