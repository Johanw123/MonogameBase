# Interface colors

The Orbit kit and the existing tree/tooltip shaders share the palette in
`OrbitSkin.cs`. Keep the shader animation and artwork; use color to establish
hierarchy rather than adding more decoration.

| Role | Color | Use |
| --- | --- | --- |
| Main text | `#DFE8E7` | Names, descriptions, ordinary buttons |
| Secondary text | `#A2B5B8` | Instructions, units, unavailable tier requirements |
| Disabled text | `#90A5A8` | Disabled menu controls |
| Interaction | `#5ADACE` | Teal focus and purchased-rank indicators |
| Value | `#F2C470` | Amber numbers, free rewards and buyable-node outlines |
| Prestige | `#C9ACEB` | Lavender prestige headings and costs |
| Systems | `#87C1E5` | Blue system headings and cooldowns |
| Shipyard | `#DAB28E` | Warm shipyard headings |
| Success | `#89D4A4` | Completed ranks, active effects, confirmation |
| Restriction | `#E4A49C` | Ruled-out choices and blocked actions |

Blue-green backgrounds anchor the palette. Rows use neutral `#12252D`,
locked rows `#09171E`; avoid large purple or gold fills behind small text.
Teal and blue sit next to each other on the color wheel; amber supplies a warm
counterpoint for value. Lavender is a limited feature identifier rather than
an all-purpose highlight. Rarity colors remain on module names and indicators.
Keep labels such as MAX, LOCKED, CLAIMED and RULED OUT: color supplements them.

Contrast checks against the panel, elevated row and hover backgrounds give
main text at least 9.78:1 and secondary text at least 5.72:1. Disabled text is
at least 4.73:1; semantic text accents are at least 5.86:1. These are palette
pair measurements, not a claim that every animated pixel meets those ratios.

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

## Preview on Linux

Use `SDL_VIDEODRIVER=offscreen` and `ALSOFT_DRIVERS=null` for capture and menu
render checks. Use `"encoder": "x264"` in capture scenes when NVENC is unavailable.
Allow the intro to finish (10 seconds of warmup) before reviewing HUD screens.
