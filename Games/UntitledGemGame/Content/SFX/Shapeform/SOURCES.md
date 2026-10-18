# Shapeform sound selections

Stock recordings from the local Shapeform library, imported 2026-10-18.
No audio was synthesized. Original recordings stay outside the repository.

Rebuild with `python3 tools/audio/import_shapeform.py /path/to/Shapeform`.
Requires FFmpeg. Selections and source paths live in
`tools/audio/shapeform-selections.json`.

Processing: mono 44.1 kHz / 16-bit PCM; remove leading silence (keep 3 ms),
45 Hz high-pass, gentle low-pass, limit long tails, 20 ms fade-out,
balance to at most -23 dBFS RMS and -6 dBFS sample peak.
The gain is linear; quiet transients retain their dynamics.

| Game asset | Source recording (relative to library root) | Source / game duration | Game RMS |
| --- | --- | --- | --- |
| window-open.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Open Close/FUI Open Simple Soft.wav | 0.29 / 0.29 s | -23.0 dBFS |
| window-close.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Open Close/FUI Close Simple Soft.wav | 0.31 / 0.31 s | -23.0 dBFS |
| error.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Update 2/FUI Error Popup Subtle 01.wav | 0.34 / 0.34 s | -23.2 dBFS |
| refund.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Update 2/FUI Shop Currency 01.wav | 0.42 / 0.40 s | -25.8 dBFS |
| scan.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Update 2/FUI Radar Ping Echo 01.wav | 0.87 / 0.87 s | -23.0 dBFS |
| confirm.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Mechanisms/Target Lock.wav | 0.36 / 0.36 s | -23.0 dBFS |
| equip.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Mechanisms/Micro Mechanism Extend Short.wav | 0.17 / 0.15 s | -23.0 dBFS |
| unequip.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Mechanisms/Micro Mechanism Release.wav | 0.78 / 0.55 s | -25.9 dBFS |
| command.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Activate Deactivate/Activate Hologram Warm Short.wav | 1.00 / 0.80 s | -23.0 dBFS |
| merge.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Holographic Chimes/Holographic Chime Ping Short.wav | 0.96 / 0.60 s | -23.0 dBFS |
| cargo.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Holographic Chimes/Holographic Soft Ping.wav | 0.93 / 0.55 s | -23.0 dBFS |
| jackpot.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Update 2/FUI Reward Unlock 01.wav | 1.44 / 1.44 s | -25.3 dBFS |
| warp.wav | Shapeforms Sci Fi Warp Speed 24 48/Shapeforms Sci Fi Warp Speed 24 48/Audio/Wormhole Open Close/Warp Enter Exit Combined Short_01.wav | 1.37 / 1.10 s | -23.8 dBFS |
| speedboost.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Boost/One Shot/Boost Whoosh Layer Short_01.wav | 0.27 / 0.27 s | -23.0 dBFS |
| magnet.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Energy and Motion/FUI Phased Motion Short.wav | 0.45 / 0.45 s | -26.7 dBFS |
| electric.wav | Shapeforms Aeon Bonus 24 48/Shapeforms Aeon Bonus 24 48/AUDIO/ELECTRIC ARCS/Electric Arc Spark_01.wav | 0.24 / 0.24 s | -24.5 dBFS |
| servo-rise.wav | Shapeforms Aeon Bonus 24 48/Shapeforms Aeon Bonus 24 48/AUDIO/SERVOS/Sci-Fi Small Servo Motion Rise_01.wav | 1.26 / 0.60 s | -23.0 dBFS |
| materialize.wav | Shapeforms Audio Future UI 24 48/Shapeforms Audio Future UI 24 48/Audio/Energy and Motion/Holographic Particles Materialize.wav | 2.67 / 1.20 s | -23.0 dBFS |
| drill-launch.wav | Shapeforms Aeon Bonus 24 48/Shapeforms Aeon Bonus 24 48/AUDIO/MECHANISMS/Charge Mechanism_01.wav | 1.23 / 0.80 s | -25.2 dBFS |
| heavy-impact.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Impacts/Asteroid Impact Thud_01.wav | 0.23 / 0.23 s | -23.0 dBFS |
| debris.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Impacts/Impact Asteroid Debris Tail_01.wav | 0.65 / 0.65 s | -23.0 dBFS |
| core-tap.wav | Shapeforms Arcane Activations 24 48/Shapeforms Arcane Activations 24 48/Audio/Stone Interaction/Place Stone 01.wav | 0.27 / 0.27 s | -23.0 dBFS |
| wing-launch.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Boost/One Shot/Boost Short Noise Burst_01.wav | 0.42 / 0.42 s | -23.0 dBFS |
| cannon.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Boost/One Shot/Boost Percussive Transient_01.wav | 0.10 / 0.10 s | -23.0 dBFS |
| rocket.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Thrusters/One Shot/Thruster Ignite Short_01.wav | 0.21 / 0.21 s | -23.0 dBFS |
| cluster.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Thrusters/One Shot/Thruster Short Puff_01.wav | 0.35 / 0.35 s | -23.0 dBFS |
| surge.wav | Shapeforms Aeon Bonus 24 48/Shapeforms Aeon Bonus 24 48/AUDIO/ELECTRIC ARCS/Electric Arc Spark_03.wav | 0.40 / 0.40 s | -25.0 dBFS |
| charge.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Turbine/Spin Up and Recharge/Power Up Charge Pneumatic Short_01.wav | 0.43 / 0.43 s | -23.0 dBFS |
| railgun.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Boost/One Shot/Explosive Propulsion Layer_01.wav | 0.63 / 0.63 s | -23.0 dBFS |
| servo-fall.wav | Shapeforms Aeon Bonus 24 48/Shapeforms Aeon Bonus 24 48/AUDIO/SERVOS/Sci-Fi Small Servo Motion Fall_01.wav | 1.23 / 0.60 s | -23.0 dBFS |
| impact.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Impacts/Impact Clean Kick Thud_01.wav | 0.13 / 0.13 s | -23.0 dBFS |
| ricochet.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Impacts/Metallic Transient_01.wav | 0.12 / 0.12 s | -23.0 dBFS |
| explosion.wav | Shapeforms Sci Fi Spacecraft 16 44/Shapeforms Sci Fi Spacecraft 16 44/Audio/Boost/One Shot/Explosive Boost Impact Soft_01.wav | 0.61 / 0.61 s | -23.0 dBFS |
| energy-release.wav | Shapeforms Arcane Activations 24 48/Shapeforms Arcane Activations 24 48/Audio/Arcane Magic/Pure Energy Release Dark.wav | 6.74 / 1.80 s | -23.0 dBFS |
