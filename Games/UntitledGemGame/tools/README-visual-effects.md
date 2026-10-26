Open the in-game debug menu with **Ctrl+Tab**, then expand **Visual effects / space atmosphere**.

**Recommended** is the default: dimmer, less saturated background layers, subtle blue fog,
slow drift and cloud movement, star shimmer, soft bloom, light color grading and a gentle
vignette. **Deep space** is darker with violet fog. **Original look** restores the old
background and original bloom settings. **Compare original (keep my tweaks)** temporarily
bypasses the new setup without losing values. Preset changes are previews until saved.

Each effect has a checkbox and sliders. Ctrl-click a slider to type an exact value:

- Background: brightness, saturation and separate cloud/star opacity.
- Fog: density, scale, flow speed, coverage and RGB color. Only the far background receives fog.
- Motion: drift, camera parallax, cloud distortion and star shimmer. Parallax responds when
  the world camera moves (for example, during escape); drift also works while stationary.
- Bloom: threshold, intensity, blur and saturation; optional hue-preserving soft knee and
  a wider halo with adjustable radius and horizontal stretch.
- Grading: exposure, contrast, saturation, temperature, tint, shadow lift and highlight compression.
- Vignette: strength, radius and softness.
- Optional chromatic aberration, grain, sharpening and barrel/pincushion distortion.
  These are disabled in Recommended to keep the artwork clear.

**Animate background and grain** freezes/unfreezes the visual animation clock.
The existing per-ship, gem and planet glow controls remain under **Bloom & glow tuning**.
The new post effects operate on the world before the HUD, debug controls and menus are composed.

**Save as default** writes `Content/VisualEffects.json` from a desktop developer launch.
It is separate from player saves and is included by both desktop and web content builders.
**Revert to saved** restores the last loaded/saved settings; **Copy settings JSON** exports the
current controls. A/B comparison itself is not saved. Browser previews can be copied but
source defaults must be saved from desktop.

Implementation notes: fog is three-octave procedural noise, with no new image assets. Bloom
retains the existing half-resolution Gaussian blur, adds a quadratic soft threshold and
optionally a nine-tap tent filter over that blurred buffer for a second glow scale. This is
an inexpensive LDR enhancement, not a full HDR bloom pyramid. The existing pipeline still
runs its blur passes when bloom is disabled. Full-screen filters share the combine pass;
optional sampling branches skip the additional halo, sharpening and color-split reads when
those controls are zero. Pixel-based lens and halo controls use a 1080p reference size.

Soft-knee background reference: [Unity's bloom documentation](https://docs.unity.cn/Packages/com.unity.postprocessing%403.3/manual/Bloom.html).

Validation:

```sh
dotnet build --no-restore -m:1 -p:BuildProjectReferences=false -p:UseSharedCompilation=false
dotnet build Tests/PersistenceChecks.csproj --no-restore -m:1 -p:BuildProjectReferences=false -p:UseSharedCompilation=false
SDL_VIDEODRIVER=offscreen ALSOFT_DRIVERS=null dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --visual-effects-check .
```

The GPU checks compile the three shaders with the game's native ShadowDusk compiler and
exercise neutral composition, vignette, exposure with bloom disabled, optional sampling
branches, soft extraction, moving fog, alpha output and settings serialization/validation.
