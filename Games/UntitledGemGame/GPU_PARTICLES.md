# Texture-based GPU particles

The desktop prototype uses the same technique as the local `VTFParticles` XNA
sample and `UnityTextureLookupGpuParticles`: particle state lives in float
textures, pixel shaders integrate it, and a vertex shader fetches it to place
particle quads. This is a new implementation inspired by those projects.

## Gameplay effects

Desktop gameplay now emits particles for:

- Cannon/manual impacts and critical hits: directional gold chips and orange embers.
  Rockets have wider sprays; railguns have faster cyan streaks. Mining lasers
  emit small contact sparks at a fixed cosmetic rate, independently of damage ticks.
- Core drills: a landing burst, sustained outward molten spray, and a finishing
  burst. Core Tap produces a narrow geyser; Tectonic Rupture throws sparks off the rim.
- Shell breaks: cyan strain sparks followed by violet/cyan debris between the
  existing large plates. Core fractures emit sparks along the growing fissure,
  inward motes during the swallow, then gold streaks and red embers at eruption.
- Secondary explosions: kamikaze drones, harpoon capacitor discharges, weapon
  jackpots, weak points and magma chains use colour-matched streaks, embers and a
  brief hot flash. The Foozle asteroid explosion sheet is no longer loaded,
  rendered or included in content builds. Main weapon impacts and core eruptions
  use their existing dedicated particles without adding duplicate explosions.

Planet combat callouts (critical hits, overloads, drill finishers, weak points,
shock, resonance and detonation counts) are removed. Rewards and damage still
resolve normally; their particles, sounds and shockwaves provide visual feedback.

Ordinary impacts and drill contact share a 4,800-particles/second budget with a
960-particle burst allowance and at most 24 layer descriptors per Update. Sixteen
queue entries are reserved for milestone effects; these have fixed burst counts
and bypass the ordinary budget. Stalls discard continuous-emitter catch-up debt.
No per-particle CPU state, collisions or readbacks are added.

**Tab → GPU particles → Gameplay particle effects** toggles these emissions for
comparison. It is a temporary debug setting and is not saved. Already emitted
particles finish naturally. The pool is created on demand at the selected size.
Browser gameplay retains its existing effects and skips these desktop particles.

## Try it in the game

Press **Tab**, expand **GPU particles**, choose an emitter, then click **Emit once**.
**Run emitter** repeats it at the selected interval; **Move emitter in a circle**
shows the trail left by a moving source. Place previews at the view centre,
homebase engine, planet edge or mouse. Direction and effect scale are adjustable.
The mouse position is sampled during Update, using the correct window viewport.

Recipes: **Explosion**, **Cyan shockwave**, **Violet fountain**, **Engine exhaust**,
**Mining impact**, **Electric discharge**, **Core fracture**, **Warp arrival** and
**Refuel stream**. They combine layers of soft glows, velocity-aligned streaks,
expanding rings or inward-moving particles. The descriptions suggest possible
other uses. These recipes remain debug previews; gameplay uses separately tuned,
budgeted layers in `Screens/GameScreen.ParticleEffects.cs`.

The debug pool defaults to **65,536** slots (4 MiB of state), with choices from
16,384 through **1,048,576**. Changing the pool clears its effects. The logarithmic
particle slider covers the whole selected pool. The panel shows occupied slots
(an upper bound), simulated texels, submitted quads, region counts and accepted
emission size. Drag and gravity can be changed while particles are running.
Emission catch-up is bounded to four recipe invocations per Update.

## Emit from gameplay

```csharp
screen.EmitParticleBurst(new GpuParticleBurst(worldPosition, 512, Color.Orange)
{
    Speed = new Vector2(30, 120),       // min/max world units per second
    Lifetime = new Vector2(0.3f, 1.2f), // min/max seconds
    Size = new Vector2(0.7f, 2f),      // min/max diameter in world units
    Radius = 2                       // initial emission disc radius
});
```

`Velocity` adds inherited velocity. `Direction` is radians in the world XY
plane; `Spread` defaults to a full circle and can be narrowed to make a cone.
`Motion` can align velocity outward, inward or tangentially to the initial
spawn position; use zero spread for a precise radial direction. `InnerRadius`
turns the emission disc into an annulus. `Streak` aligns and stretches a particle
along its velocity. Colour alpha scales brightness. The method returns the accepted particle count.
It returns zero while the shader is unavailable or the pending queue is full.

For a standalone system, construct `GpuParticleSystem` with the graphics device
and the loaded `GpuParticles.fx` effect. Call `Update(dt)` during simulation,
`Simulate()` **before drawing scene pixels**, and `Draw(viewProjection)` in the
world pass, outside any active SpriteBatch/ShapeBatch. The class owns its effect
clone, render targets and draw resources; dispose it when leaving the screen.

## Storage and limits

- Standalone default: **16,384 slots**, using four 128×128 RGBA float32 render
  targets (1 MiB of state). The debug panel creates a larger pool explicitly.
- Position targets hold world XY, remaining lifetime and initial lifetime.
  Velocity targets hold velocity XY, signed diameter (negative for streaks),
  and packed RGB. Colour keeps its full eight bits per channel.
- Slots form a bounded ring. Oversized bursts clamp to capacity; wrapping replaces
  the oldest allocated slots. Up to 64 layer descriptors can wait between draws.
- The CPU tracks one expiry/count per **texture row**, never per particle. Adjacent
  occupied rows form ranges. Expired rows are omitted from simulation and drawing;
  partially occupied rows may retain dead padding until their last burst expires.
- A new row is cleared lazily before use. Clearing the system only retires rows
  and pending work; it performs no full-texture GPU clear. New bursts are clipped
  to their actual texel spans, rather than shading the whole pool per burst.
- **Multiple render targets** write position and velocity together: one spawning
  pass per layer and one update pass over occupied ranges per rendered frame.
  Constant acceleration and linear drag are integrated in closed form, removing
  the old 1/60 s substep loop while keeping motion independent of frame rate.
- Motion and expiry share a clock, capped at 0.25 s of catch-up per rendered frame.
  Pausing freezes both. Prestige/session reset clears them; a device reset also
  rebuilds native drawing resources.
- The VTF draw submits only occupied ranges. Dead particles return before fetching
  appearance data. A fully idle pool submits no simulation or drawing work.
- Effect parameters/techniques and render-target binding arrays are cached. The
  warmed desktop simulation/draw path allocates **zero managed bytes per frame**
  in the graphics check. There are no per-particle CPU updates or GPU readbacks.

A 65,536-slot pool containing three particles uses one 256-texel simulation row
and submits three quads, rather than processing all 65,536 slots. Cost still grows
with occupied rows and visible particle coverage; large overlapping glows can
be expensive on the GPU. This does not claim a hardware frame-rate target.

## DesktopGL drawing

MonoGame 3.8.5's standard OpenGL effect path only assigns pixel-shader sampler
units; it does not wire up vertex texture fetch. The installed ShadowDusk
compiler explicitly rejects vertex texture sampling for that path.

`GpuParticleGlRenderer` handles just the particle draw with a private GLSL 1.50
program and VAO, using OpenGL functions resolved through MonoGame's bundled SDL.
A clipped MonoGame draw binds the two textures first. The native draw reads those
same texture units and restores the previous program/VAO afterwards. Simulation
still uses ordinary MonoGame effects and render targets. No framework fork or
additional rendering package is needed. The two GLSL files are embedded in the
desktop executable so publishing includes them.

This prototype is **desktop-only**. Browser controls explain that limitation;
KNI/WebGL rendering and other MonoGame backends need a verified draw adapter.
Particles are cosmetic: they do not collide, sort by depth or affect game rules.

## Checks

After building the game and test project:

```sh
dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll \
  --gpu-particle-shader-check Content/Shaders/GpuParticles.fx

SDL_VIDEODRIVER=offscreen ALSOFT_DRIVERS=null \
dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll \
  --gpu-particle-check Content/Shaders/GpuParticles.fx /tmp/gpu-particles
```

The graphics check compiles the actual simulation shader, runs the real float
target simulation and VTF renderer, and saves `/tmp/gpu-particles.png`. It covers
signed coordinates, exact integration, gravity, drag, lifetime, overlapping bursts,
wrapping, bounded queues, lazy clearing, sparse ranges, zero warmed managed
allocations, every emitter recipe, state restoration, subsequent SpriteBatch
drawing and device reset. Readback is confined to this check.
The graphics check also exercises the real screen emission budget under rapid
fire, multiple Updates before Draw, frame-rate changes, disabling, and long stalls.

Capture scenes can also trigger a test explosion with
`{"at": 0, "do": "event", "id": "particles", "pos": [0.5, 0.5]}`.
