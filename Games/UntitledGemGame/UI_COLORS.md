# Interface colors

The Synty `InterfaceSciFiSoldierHUD` kit and the existing tree/tooltip shaders share the palette in
`GuiPalette.cs`, exposed through `GuiSkin.cs`. Keep the shader animation and artwork; use color to establish
hierarchy rather than adding more decoration.

| Role | Color | Use |
| --- | --- | --- |
| Main text | `#E2EEF4` | Names, descriptions, ordinary buttons |
| Secondary text | `#A2B9C6` | Instructions, units, unavailable tier requirements |
| Disabled text | `#849AA6` | Disabled menu controls |
| Interaction | `#52E4F2` | Tactical cyan focus and purchased-rank indicators |
| Panels / value | `#FFBE52` | Shared panel headings, tabs, decorations, numbers and buyable-node outlines |
| Prestige | `#C6A2F6` | Prestige currency and epic rarity |
| Power cells | `#76CCF8` | Power cell readouts, cooldowns and rare rarity |
| Success | `#68E698` | Completed ranks, active effects, confirmation |
| Restriction | `#F09286` | Ruled-out choices and blocked actions |

Deep slate-navy (`#09111A`) backgrounds anchor the palette. Rows use `#101F2C`,
locked rows `#0A141E`; avoid large purple or gold fills behind small text.
Cyan and blue sit next to each other on the color wheel; amber supplies a warm
counterpoint for value. All Upgrades, Talents, Systems, Shipyard and Signals panels share the yellow
Upgrades accent. Rarity colors remain on module names and indicators.
Keep labels such as MAX, LOCKED, CLAIMED and RULED OUT: color supplements them.

Contrast checks against the panel, elevated row and hover backgrounds give
main text at least 9.78:1 and secondary text at least 5.72:1. Disabled text is
at least 4.73:1; semantic text accents are at least 5.86:1. These are palette
pair measurements, not a claim that every animated pixel meets those ratios.

## Runtime editor

Press **Tab** during gameplay and expand **HUD / GUI colors**. Hue shift,
saturation and brightness recolor the base blue surfaces, borders and baked blue
panel artwork. **All panels / upgrades / values** controls the shared panel accent.
The other pickers control resources, rarity, status and text. Changes preview
immediately in open UI.

- **Save as default** writes `Content/GuiPalette.json` in the source project.
  Developer launches read it directly; desktop and web builds bundle it.
- **Revert to saved** restores the last successfully saved palette.
- **Original palette** previews the original colors without overwriting the saved
  default. Save afterward to make the original palette the default again.
- Each color's **Revert**, and **Revert base colors**, restore only those saved values.

The editor marks unsaved changes. Leaving it or closing the game does not save a
preview. Palette defaults are separate from player saves. The table and contrast
measurements above describe the original palette.

## Upgrade node states

Purchase progress and affordability are separate cues:

- Untouched nodes have an empty rank rail and a neutral outline. The upgrade
  artwork is inset above a reserved footer so it cannot overlap the rail.
- Partially purchased nodes have a teal rank rail; even the first rank stays visible.
- Maxed nodes have a full sage rail and a check badge, with a quiet outline.
  Check and plus glyphs reuse `icons_set/icons_128/tick.png` and `add.png`,
  preloaded with the other textures and drawn with linear filtering.
- A node you can buy now gets an amber outline and a plus badge, whether new or partial.
- Locked, maxed and read-only nodes never get the buyable badge.

Idle connections have a narrow colored glow, without a travelling pulse. A purchase briefly
animates the connection. Shader borders remain, with their glow reduced; completed
nodes no longer have bright white double outlines. Connections stop outside each
node with a small gap so neither the line core nor its glow cuts into the border.

## HUD surfaces and spacing

- Resource balances share one continuous background, with a label row, a value
  row and a separate icon gutter. Avoid inset backgrounds for each number.
- Manual commands use flat surfaces with one uniform border. The progress line
  and status text carry state; the title stays neutral.
- Ability slots have one inset state border, without a second textured frame.
- Buying a power cell is a compact action next to its price, like Refund all.
- Module and signal tooltips use the same opaque elevated surface, solid outline
  and shadow so the inventory behind them does not show through.
- The upgrade-state legend is omitted from the tree header.

## Quiet panel detail

New Game uses a faint ship crest; Settings uses concentric calibration rings and
instrument ticks behind the controls. These decorations are static, ignore input,
and follow the runtime GUI palette. Headers and active HUD buttons use flat fills
and crisp outlines, without the broad radial glow behind their labels.

The HUD retains its semi-transparent glass background with a low-opacity, inset
Synty hex pattern. Tile the pattern separately from the nine-slice panel so its
cells keep their proportions and never stretch with the bar.

The additional `synty_map_ship`, `synty_hud_ring_calibration` and
`synty_hud_calibration_ticks` PNGs are unmodified Synty pack assets. Their source
paths are recorded in `tools/build_synty_ui.py`; the existing hex texture comes
from the same pack.

## Preview on Linux

Use `SDL_VIDEODRIVER=offscreen` and `ALSOFT_DRIVERS=null` for capture and menu
render checks. Use `"encoder": "x264"` in capture scenes when NVENC is unavailable.
Allow the intro to finish (10 seconds of warmup) before reviewing HUD screens.
