Build the desktop game, then run the persistence checks (no graphics window or extra test packages required):

```sh
dotnet build --no-restore
dotnet run --project Tests/PersistenceChecks.csproj
```

The checks exercise the built game's save store, currency restoration, and upgrade restoration using the real upgrade definitions. They use an isolated temporary directory and never touch the player's save.

Progress is stored at `Environment.SpecialFolder.LocalApplicationData/UntitledGemGame/progress.json` (normally `~/.local/share/UntitledGemGame/progress.json` on Linux and `%LOCALAPPDATA%\UntitledGemGame\progress.json` on Windows). The previous complete save is kept in `progress.json.bak` and loaded if the primary is damaged. Unreadable or unsupported saves are preserved; a HUD error explains when saving is disabled.

Saves include all three upgrade trees, currency balances, run earnings, prestige state, and equipped ability slots. Purchases, ability refunds/equipment changes, and prestige save immediately; currency also autosaves every five seconds. Leaving the game screen, closing the game, and losing focus save as well. Pending delivered income is included once. Saving during prestige records its completed transaction.

The bottom-right ability point panel buys one point for red gems. Tune its starting price and curve independently using `RedGemsPerFirstPoint` and `EarningsExponent` in `AbilityPointProgression.cs`; prestige has its own values in `PrestigeProgression.cs`. Ability prices are approximately first-point cost × (purchases + 1)^(1 / exponent), currently 100,000 × (purchases + 1)^5. Increasing the exponent makes prices grow more slowly. The bar shows the current wallet divided by that price. Spending/refunding points and prestige do not reset the saved purchase count. Existing saves start at zero panel purchases; the original ability-point upgrade nodes remain available and do not advance this counter.

The active loose-gem count is saved and restored through the spawn queue at random positions, with gem qualities rolled from the restored upgrades. Pending queued gems are included so saving during restoration preserves the count. Older saves without a count retain their previous startup behavior. Prestige clears the count. Exact gem positions and values, other world entities, harvester cargo, and running ability effects are not serialized. Harvesters are recreated from the restored upgrades; equipped abilities restart their cooldowns. There is no offline income simulation.

Manual checks with the game running:

- Fill the bottom-right ability point bar and click it. Confirm one point is granted, red gems are deducted, and the new price appears. Spend/refund the point, prestige, and reopen; confirm the price remains increased. Clicking with insufficient gems must do nothing.

- Purchase upgrades in each tree, equip abilities, quit, and reopen. Confirm levels, next prices, available branches, currency, harvesters, and equipped slots.
- Force-close after an autosave and reopen; then force-close during prestige. Confirm the reward is granted once and the regular tree is reset.
- Refund ability upgrades and reopen. Confirm the refunded balance and cleared ability tree.

- With no save or backup, confirm Continue is hidden and New Game starts directly.
- With an existing save, click New Game: Cancel and Escape must preserve progress; confirming must start fresh. Continue must still restore progress after cancelling.

The suite also validates the persistent gem index and sleeping-gem lifecycle. See [PERFORMANCE.md](../PERFORMANCE.md) for benchmark results, reproduction commands, and the optional offscreen renderer comparison.
