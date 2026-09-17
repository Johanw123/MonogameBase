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

Desktop upgrade popout checks:

- Open an upgrade tree and click **Pop out** at the top right. The game should stay visible, undimmed and running while the second window has focus.
- Move the detached window to another monitor; resize it to both wide and tall sizes. The tree keeps its proportions, and pointer targets must match the letterboxed content. Check text and icons at 720p, 1080p and 4K, and move between monitors with different display scaling: the popout should retain the same sharpness as the docked HUD at equivalent sizes. The upload resolution follows actual drawable pixels, capped at the source HUD resolution.
- Buy an upgrade, hover for its tooltip, pan with right/middle drag, zoom with the wheel, and switch between Upgrades and Abilities. Currency and purchased levels must update immediately in both windows.
- Click in the main game window while the tree is detached. Gameplay controls should work, and clicks in the detached window must not collect gems or refuel ships in the main window.
- Click **Dock**, then repeat **Pop out** and close the native window using its title-bar close button. Both actions return the tree to the game without exiting. Hiding upgrades, leaving gameplay and quitting must also clean up the second window.
- Test a prestige confirmation and pause menu from each window. Browser builds retain the existing in-game tree.

The desktop popout renders and presents once per main-window draw, with no separate frame-rate cap. Check tree panning and zooming with the main game running above 30 FPS; both windows should update together. Native multi-monitor placement is controlled by the desktop window manager.

Run the popout checks on the actual desktop video backend, including Wayland and X11 on Linux. A native window handle alone does not prove that the compositor displays it: confirm that the tree is visible and interactive. SDL2's Wayland backend cannot use the former forced software window-surface path. The popout now creates an independent SDL renderer, presents its first frame before detaching the tree, and restores the in-game tree if a later upload/draw fails. Include a forced presentation failure when testing this fallback.

The shared header slider has independent session values: **Dimming** starts at 50% when docked (range 0–100%); **Background** starts at 100% in the popout (range 0–100%). At 0%, the background should be transparent while nodes, text, tooltips, and controls retain their normal opacity. Change both, dock/reopen, and confirm each value is retained without affecting the other. Slider drags must not buy a node behind the header. Hover upgrade nodes in the popout and verify that both tooltip panels appear only there; main-window ability tooltips must still appear in the main window. On Wayland, background opacity uses premultiplied alpha in an EGL surface; compositor-owned decorations remain controlled by the desktop. Backends without per-pixel transparency show the slider as unavailable instead of fading the entire window.
