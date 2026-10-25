# Sound effects first pass

34 Shapeform stock clips now cover 48 events. Selection used the recording names,
duration, onset, RMS/peak level and spectral energy. This is a measured first pass;
the final subjective balance still needs a listening playtest against the music.
F8 previews each assignment in the sound effects lab; use gameplay to assess
throttling and ducking. The existing four saved silent impact assignments in
`Content/SFX/EventDefaults.json` remain in effect.

| Events | Selection and reason |
| --- | --- |
| Windows, errors, refunds | Future UI soft open/close, subtle error popup and currency tick; short, distinct responses without long notification tails. |
| Signals and manual commands | Radar Ping, Target Lock and Activate Hologram Warm Short; scan/confirmation contrast and a warmer command sound. |
| Module equip/unequip and reveal | Micro Mechanism extend/release; short latch-like motion. Target Lock for common salvage, Reward Unlock for rarer reveals. |
| Manual and automatic cannon | Spacecraft Boost Percussive Transient: about 0.1 seconds, immediate onset. Manual fire is louder and lower; automatic fire is quiet background punctuation. |
| Cannon impacts, criticals and ricochets | Clean Kick Thud, Asteroid Impact Thud and Metallic Transient; low thuds distinguish damage from the brighter shot/ricochet transient. |
| Rockets and clusters | Thruster Ignite Short and Short Puff; brief propulsion cues, with Explosive Boost Impact Soft for explosions. Small explosions use less gain and a higher pitch. |
| Railgun | Pneumatic charge followed by Explosive Propulsion Layer; separates anticipation from the heavier discharge. |
| Harpoon, lightning and laser surge | Mechanism movement and Aeon Electric Arc Sparks; electrical texture is filtered and quiet because it repeats. |
| Speedboost, magnet, drones and gem spawner | Short boost whoosh, phased motion, servo rise and particle materialization; each activation gets a different texture. |
| Core drill and wing launch | Aeon charge mechanism, stone/debris impacts and a short propulsion burst. |
| Cargo, merge and jackpots | Soft holographic ping, short chime and Reward Unlock; small income sounds stay subtle, with a richer cue for a jackpot. |
| Warp and overload | Short combined warp and dark energy release; longer transitions are trimmed to avoid persistent wash. |

Imports total about 1.5 MiB as mono 44.1 kHz 16-bit WAV. Leading silence and long
tails are shortened, sub-bass and harsh highs filtered, and gain bounded by both
-23 dBFS RMS and -6 dBFS sample peak. Source paths and per-file measurements are
in [SOURCES.md](Content/SFX/Shapeform/SOURCES.md); the deterministic import script
and selection manifest are under `tools/audio/`. These are processed asset-pack
recordings, with no synthesized sound.

## Mix budgets

`SoundMix.cs` classifies events independently of their editable asset category.
Manual cannon hits carry their player origin through to the impact. Automatic
ship abilities use the automation budget even when triggered frequently by
progression upgrades. Manual refueling keeps protected click feedback; a fleet
refuel click produces one start sound rather than a sound per ship.

| Bus | Live voices | Sustained starts/second | Initial burst |
| --- | ---: | ---: | ---: |
| Player controls and direct actions | 8 | 30 | 8 |
| Automatic weapons, abilities and notifications | 6 | 10 | 3 |
| Pickups, merges and cargo | 2 | 5 | 1 |
| Planet/core/intro/ending milestones | 4 | 8 | 4 |

These limits are shared by all events on a bus. Event cooldowns also coalesce
repetition across frames: roughly 0.18–0.3 seconds for automatic weapons,
0.65–0.8 seconds for frequent notifications and refueling, and 0.4 seconds for
deliveries/merges. Intentional layers of one event remain intact.

The bounded pending queue admits player actions ahead of background requests,
even if automation filled it first. Repeated same-frame events coalesce without
building a backlog. Quiet scenes retain full background gain. Dense activity
lowers background gain smoothly, down to 22%; player sounds temporarily duck it
to 35% of its current gain for 180 ms, then it recovers over about 120 ms.
Pickup ducking is gentler. Limits and ducking apply to already playing tails,
and the SFX setting updates active voices too. The optional refuel hum has a
separate two-voice cap and count-normalized gain that follows the background mix.

Audio updates continue during pause, intro and ending states. Leaving gameplay
stops and disposes its active voices and discards pending requests.

## Validation

`--sound-mix-check` exercises a saturated queue, manual feedback, intentional
layers, dense pickups, long tails, reserved milestone capacity, ducking, live
mute, asset replacement and teardown. Over five seconds of saturated automation,
30, 60 and 144 FPS each admitted 52 starts. The existing `--sound-effects-check`
also covers lab imports, tuning, save/reload, silent overrides and refuel loops.

For listening, compare a fresh run with a late progression debug preset. Try
clicking the planet, collecting gems, refueling, buying upgrades and triggering
commands while weapons and the fleet are busy. Player responses should remain
clear while the automated mix settles into quiet background texture.
