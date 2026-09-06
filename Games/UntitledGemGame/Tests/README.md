Build the desktop game, then run the persistence checks (no graphics window or extra test packages required):

```sh
dotnet build --no-restore
dotnet run --project Tests/PersistenceChecks.csproj
```

The checks exercise the built game's save store, currency restoration, and upgrade restoration using the real upgrade definitions. They use an isolated temporary directory and never touch the player's save.

Progress is stored at `Environment.SpecialFolder.LocalApplicationData/UntitledGemGame/progress.json` (normally `~/.local/share/UntitledGemGame/progress.json` on Linux and `%LOCALAPPDATA%\UntitledGemGame\progress.json` on Windows). The previous complete save is kept in `progress.json.bak` and loaded if the primary is damaged. Unreadable or unsupported saves are preserved; a HUD error explains when saving is disabled.

Saves include all three upgrade trees, currency balances, run earnings, prestige state, and equipped ability slots. Purchases, ability refunds/equipment changes, and prestige save immediately; currency also autosaves every five seconds. Leaving the game screen, closing the game, and losing focus save as well. Pending delivered income is included once. Saving during prestige records its completed transaction.

World entities, loose gems, harvester cargo, and running ability effects are not serialized. Harvesters are recreated from the restored upgrades; equipped abilities restart their cooldowns. There is no offline income simulation.

Manual checks with the game running:

- Purchase upgrades in each tree, equip abilities, quit, and reopen. Confirm levels, next prices, available branches, currency, harvesters, and equipped slots.
- Force-close after an autosave and reopen; then force-close during prestige. Confirm the reward is granted once and the regular tree is reset.
- Refund ability upgrades and reopen. Confirm the refunded balance and cleared ability tree.
