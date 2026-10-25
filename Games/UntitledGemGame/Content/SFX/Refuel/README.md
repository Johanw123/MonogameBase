Manual refueling starts with the curated Shapeform pneumatic charge; completion
uses a soft holographic ping. These are assigned in AudioManager.cs.

For an optional continuous charging hum, add `loop.wav`, rebuild and restart:

- `loop.wav`: seamless charging hum, with no intro or ending baked in.

The missing loop is silent. The content builder includes
WAV files in this folder automatically.

The hum follows actual refueling progress, so speed upgrades need no separate
audio files. Its pitch rises from -0.15 to +0.25 as refueling advances. It fades
in/out over 60 ms, plays at 25% of the SFX setting, and allows at most two
simultaneous hums. Their combined gain is divided by the square
root of the voice count and follows the background mix's density and player
ducking gain. Tune these values in AudioManager.cs. Pausing or leaving
gameplay stops the hums; continuing refueling starts them again.
