Add any of these optional WAV files, then rebuild and restart the game:

- `start.wav`: short activation sound when refueling begins.
- `loop.wav`: seamless charging hum, with no intro or ending baked in.
- `complete.wav`: short ready sound when fuel is restored.

Missing files are silent; each sound is optional. The content builder includes
WAV files in this folder automatically.

The hum follows actual refueling progress, so speed upgrades need no separate
audio files. Its pitch rises from -0.15 to +0.25 as refueling advances. It fades
in/out over 60 ms, plays at 25% of the SFX setting, and allows at most four
simultaneous hums. Tune these values in AudioManager.cs. Pausing or leaving
gameplay stops the hums; continuing refueling starts them again.
