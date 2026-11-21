"""Weapon effects baked to transparent PNGs, as parts for composing store art by hand.

The game draws these with shaders at runtime, so there is no sprite to copy. This
is a NumPy port of those shaders, frozen at one moment each:
  LaserBeam.fx       mining laser, Overheat Surge and Planet Cracker beams
  RailgunBolt.fx     the round in flight, its ion trail, charge-up and muzzle flash
  IncendiaryRound.fx cannon rounds
  ArcAnchor.fx       the Arc Harpoon's energy lance and surface ring
  ModuleEffects.fx   lightning (ModuleBolt)
Colours and sizes come from GameScreen.Weapons.cs / Railgun.cs / ArcHarpoon.cs.

Usage: python3 tools/build_capsule_fx.py <out_dir> [scale]
Scale 1 suits a 1232x706 capsule with the home base about 850 px long.

Every effect fires to the right. Line effects (beams, trails, bolts, rounds) lie on
the image's horizontal centre line; anchors.json and README.txt list where they
start and end. Radial effects (flashes, flares, rings) are centred in the image.
The game adds this light to the scene: the layers also work in Normal blend mode,
and Addition gives the game's look.
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(sys.argv[1])
SCALE = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
PX = 8.0 * SCALE        # pixels per world unit
RAIL_PX = 6.5 * SCALE   # pixels per railgun effect unit
ANCHORS = {}

WHITE = np.array([1.0, 1.0, 1.0])


def rgb(r, g, b):
    return np.array([r, g, b]) / 255.0


def frac(x):
    return x - np.floor(x)


def sat(x):
    return np.clip(x, 0.0, 1.0)


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(a, b, x):
    t = sat((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def hash1(n):
    return frac(np.sin(n * 127.1) * 43758.5453)


def hash2(x, y):
    return frac(np.sin(x * 12.9898 + y * 78.233) * 43758.5453)


def noise(x, y):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = hash2(ix, iy), hash2(ix + 1, iy)
    c, d = hash2(ix, iy + 1), hash2(ix + 1, iy + 1)
    return lerp(lerp(a, b, fx), lerp(c, d, fx), fy)


def gauss(d, w):
    return np.exp(-(d * d) / np.maximum(w * w, 0.0001))


def reach_fade(d, reach, start=0.45):
    t = 1.0 - smoothstep(reach * start, reach, d)
    return t * t


def blur(img, sigma):
    """Gaussian blur of an (h, w, 3) image through the FFT; light outside the image is zero."""
    pad = int(sigma * 3) + 1
    padded = np.pad(img, ((pad, pad), (pad, pad), (0, 0)))
    fy = np.fft.fftfreq(padded.shape[0])[:, None]
    fx = np.fft.rfftfreq(padded.shape[1])[None, :]
    response = np.exp(-2.0 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    out = np.fft.irfft2(np.fft.rfft2(padded, axes=(0, 1)) * response[:, :, None], s=padded.shape[:2], axes=(0, 1))
    return out[pad:-pad, pad:-pad].astype(np.float32)


def mix(*terms):
    """Sum of colour * scalar-field terms into an (h, w, 3) image."""
    return sum(np.asarray(c)[None, None, :] * f[:, :, None] for c, f in terms)


# ---- LaserBeam.fx ------------------------------------------------------------------

def laser(x, y, length, w, t, beam_c, flare_c, intensity=1.0, parts='all'):
    along, across = x, y
    h = along / length
    inside = (h >= 0) & (h <= 1)
    flicker = 1.0 + 0.08 * math.sin(t * 71.0) + 0.06 * np.sin(t * 17.0 + along * 0.04)
    ww = np.maximum(w * flicker, 0.5)
    shimmer = np.sin(along * 0.11 - t * 26.0) * 0.18 * ww
    beam_dist = np.abs(across + shimmer)
    end_dist = np.hypot(x - np.clip(along, 0, length), y)
    dist = beam_dist if parts == 'body' else np.where(inside, beam_dist, end_dist)
    flow_a = 0.5 + 0.5 * np.sin(along * 0.16 - t * 40.0)
    flow_b = 0.5 + 0.5 * np.sin(along * 0.05 - t * 19.0 + 1.7)
    energy = 0.55 + 0.45 * flow_a * flow_b
    core = np.exp(-(dist * dist) / (ww * ww * 0.25))
    inner = np.exp(-(dist * dist) / (ww * ww * 2.2)) * energy
    # The game cuts the halo at the quad's edge (14 widths); here it eases out earlier.
    halo = np.exp(-dist / (ww * 4.0)) * 0.3 * reach_fade(dist, w * 9.0)
    if parts == 'body':
        # A plain run of beam with soft ends, to stretch or mask to any length.
        beam = smoothstep(0.0, w * 5.0, along) * smoothstep(0.0, w * 5.0, length - along)
    else:
        # The shader dims the beam with a hard step past the impact point, which the
        # planet hides in game; here it eases off under the impact flare instead.
        beam = smoothstep(-ww, ww * 3.0, along) * (1.0 - 0.6 * smoothstep(length - w, length + w * 2.0, along))
    emitter = np.exp(-(x * x + y * y) / (w * w * 6.0)) * (0.8 + 0.2 * math.sin(t * 33.0))
    to_impact = (x - length) ** 2 + y * y
    impact = np.exp(-to_impact / (w * w * 14.0)) * (1.1 + 0.35 * math.sin(t * 47.0))
    if parts in ('body', 'emitter'):
        impact = impact * 0
    if parts in ('body', 'impact'):
        emitter = emitter * 0
    if parts in ('emitter', 'impact'):
        beam = beam * 0
    return mix((WHITE * 1.4, core * beam), (beam_c, (inner * 1.2 + halo) * beam),
               (WHITE * 0.7 + flare_c, impact), (WHITE * 0.4 + flare_c, emitter)) * intensity


# ---- RailgunBolt.fx ----------------------------------------------------------------

RAIL_TINT = rgb(110, 180, 255)
RAIL_DEEP = np.array([0.2, 0.35, 0.95])


def rail_trail(x, y, length, t, age, seed, flight, progress, intensity=1.0):
    """Coordinates in effect units (s = 1)."""
    along, across = x, y
    seed = seed * 6.2832
    head = length * min(max(progress, 0.0), 1.0)
    in_flight = 1.0 if age <= flight else 0.0
    since = max(age - flight, 0.0)
    life = min(since / 0.9, 1.0)
    fade = (1.0 - life) ** 1.4
    spread = 1.0 - (1.0 - life) ** 2
    channel = (1.0 - smoothstep(head - 4.0, head + 2.0, along)) * smoothstep(-1.0, 3.0, along)
    # In game the glow and haze end as abruptly as the channel (the muzzle flash and the
    # planet cover it); a still needs them to taper.
    soft = (1.0 - smoothstep(head - 26.0, head + 2.0, along)) * smoothstep(-1.0, 30.0, along)
    past_muzzle = smoothstep(-2.0, 8.0, along)
    ad = across

    flash = max(1.0 - age / 0.28, 0.0) ** 1.3
    core_w = 1.1 + 2.0 * flash
    core = gauss(ad, core_w)
    flicker = 0.75 + 0.25 * np.sin(along * 0.37 + t * 90.0 + seed)
    glow = np.exp(-np.abs(ad) / (6.0 + 14.0 * flash)) * (0.75 * flash + 0.08 * fade)
    filament_noise = noise(along * 0.06 - t * 3.0, seed * 3.0 + along * 0)
    filament = core * fade * flicker * (0.5 + 0.5 * filament_noise)

    radius = 3.0 + 19.0 * spread
    reach = np.minimum(1.0, 0.25 + along / 90.0)
    phase = along * (6.2832 / 30.0) + seed - since * 5.0
    sin_p, cos_p = np.sin(phase), np.cos(phase)
    strand_w = 1.0 + 1.8 * spread
    strand_a = gauss(across - radius * reach * sin_p, strand_w) * (0.55 + 0.45 * cos_p)
    strand_b = gauss(across + radius * reach * sin_p, strand_w) * (0.55 - 0.45 * cos_p)
    breakup = noise(along * 0.045 + seed * 2.0, since * 2.5 + along * 0)
    dissolve = smoothstep(life * 0.8 - 0.45, life * 0.8 + 0.35, breakup + 0.2)
    strands = (strand_a + strand_b) * fade * dissolve * (0.5 + 0.5 * (1.0 - flash)) * 1.5

    haze_w = 3.0 + 14.0 * spread
    turbulence = noise(along * 0.035 - since * 1.5 + seed, across * 0.09 + since * 0.8)
    haze = gauss(across, haze_w) * (0.25 + 0.75 * turbulence) * fade * (0.22 + 0.2 * flash)

    color = mix((WHITE * 1.6 * flash, core * channel),
                (RAIL_TINT, (core * 0.5 * flash + filament * 0.9) * channel + glow * soft),
                (lerp(RAIL_TINT, RAIL_DEEP, life * 0.7), strands * 0.9 * channel + haze * soft))

    fx, fy = along - head, across
    sabot = gauss(fx / 5.0, 1.0) * gauss(fy / 1.3, 1.0)
    bow = gauss(np.maximum(fx, 0.0) / 12.0, 1.0) * gauss(fy / 5.0, 1.0) * 0.45 * past_muzzle
    back = np.maximum(-fx, 0.0)
    cone = gauss(np.abs(fy) - back * 0.22, 1.1) * np.exp(-back / 36.0) * sat(back * 2.0) * 0.7 * past_muzzle
    color = color + mix((WHITE, (sabot * 1.8 + bow * 0.5) * in_flight), (RAIL_TINT, (bow * 0.6 + cone) * in_flight))

    from_end = np.hypot(x - length, y)
    impact = gauss(from_end / (6.0 + 24.0 * min(since / 0.3, 1.0)), 1.0) \
        * max(1.0 - since / 0.45, 0.0) ** 1.5 * (1.0 if age >= flight else 0.0)
    color = color + mix((WHITE * 0.8 + RAIL_TINT * 0.6, impact))

    from_line = np.hypot(along - np.clip(along, 0.0, length), across)
    return color * (intensity * reach_fade(from_line, 56.0))[:, :, None]


def rail_muzzle(x, y, t, wind_up=0.0, shot=None, intensity=1.0):
    """Coordinates in effect units. shot = seconds since firing, or None."""
    along, across = x, y
    r = np.maximum(np.hypot(x, y), 0.001)
    theta = np.arctan2(across, along)
    ahead = smoothstep(-12.0, 6.0, along)
    tint = RAIL_TINT
    color = np.zeros(x.shape + (3,))

    if wind_up > 0.0:
        build = float(smoothstep(0.08, 0.72, wind_up))
        climax = float(smoothstep(0.55, 0.78, wind_up))
        hold = float(smoothstep(0.78, 1.0, wind_up))
        active = float(smoothstep(0.0, 0.1, wind_up))
        ring_r = lerp(42.0, 7.0, build ** 0.7) * (1.0 - 0.75 * hold)
        arms = 0.5 + 0.5 * np.sin(theta * 3.0 - np.log(r + 1.0) * 5.0 + t * 11.0)
        arms2 = 0.5 + 0.5 * np.sin(theta * 5.0 + np.log(r + 1.0) * 7.0 - t * 16.0)
        swirl = noise(theta * 1.6 + t * 2.0, r * 0.08 - t * 1.5)
        ring = gauss(r - ring_r, 5.0 + 7.0 * (1.0 - build)) * (0.15 + 0.85 * arms * arms) * (0.4 + 0.6 * swirl) * ahead
        inner = gauss(r - ring_r * 0.55, 3.0 + 3.0 * (1.0 - build)) * (0.2 + 0.8 * arms2 * arms2) * ahead * build
        vortex = (ring * 1.1 + inner * 0.9) * active * (1.0 - 0.6 * hold)
        spokes = (0.5 + 0.5 * np.cos(theta * 9.0 + t * 1.3)) ** 14.0 \
            + 0.6 * (0.5 + 0.5 * np.cos(theta * 5.0 - t * 0.9 + 1.0)) ** 20.0
        flow = frac(r / 24.0 + t * 2.6) ** 2.5
        streaks = spokes * flow * np.exp(-r / 48.0) * smoothstep(ring_r * 0.8, ring_r * 1.6, r) \
            * build * ahead * (1.0 - hold) * 1.3
        glow_r = (3.0 + 9.0 * build) * (1.0 - 0.55 * hold)
        gather = gauss(r, glow_r) * (0.25 + 0.75 * build) * active
        core_r = (0.8 + 2.2 * climax) * (1.0 - 0.4 * hold) + 0.6 * hold
        star = np.exp(-r / core_r) * (0.3 + 1.2 * climax)
        flare_l = 14.0 + 26.0 * hold
        flare = (gauss(along, 0.7) * np.exp(-np.abs(across) / flare_l)
                 + gauss(across, 0.7) * np.exp(-np.abs(along) / (flare_l * 0.45)) * ahead) * (0.6 * climax + 0.9 * hold)
        hum = gauss(r, 20.0 + 12.0 * build) * 0.12 * build * (0.8 + 0.2 * math.sin(t * 40.0)) * ahead
        color = color + mix((tint, vortex * 1.2 + streaks * 1.1 + gather * 0.9 + hum),
                            (WHITE, gather * 0.3 * climax + star + flare))

    if shot is not None:
        flash = max(1.0 - shot / 0.16, 0.0) ** 1.2
        forward = 0.55 + 0.45 * smoothstep(-0.6, 1.0, np.cos(theta))
        rays = (0.5 + 0.5 * np.cos(theta * 7.0 + 0.4)) ** 24.0 + (0.5 + 0.5 * np.cos(theta * 11.0 - 1.7)) ** 40.0 * 0.7
        burst = rays * np.exp(-r / (26.0 + 60.0 * flash)) * forward * flash
        bloom = gauss(r, 10.0 + 22.0 * flash) * flash * 0.9
        hot = np.exp(-r / (1.5 + 3.0 * flash)) * (1.6 * flash)
        spike = gauss(across, 0.8 + 1.2 * flash) * np.exp(-np.maximum(along, 0.0) / (60.0 + 80.0 * flash)) \
            * (along >= 0) * flash
        ring_t = min(shot / 0.5, 1.0)
        ease = 1.0 - (1.0 - ring_t) ** 2
        q = np.hypot(along * np.where(along > 0.0, 0.7, 1.25), across)
        ring_r = 3.0 + 78.0 * ease
        ring = gauss(q - ring_r, 1.8 + 3.0 * ring_t) * (1.0 - ring_t) * (0.9 + 0.3 * np.sin(theta * 6.0 + t * 20.0))
        disc = gauss(q - ring_r * 0.6, 1.2 + 1.5 * ring_t) * (1.0 - ring_t) * 0.5 * (1.0 if shot <= 0.3 else 0.0)
        jet_t = min(shot / 0.24, 1.0)
        jet_reach = 5.0 + 26.0 * jet_t
        side = np.abs(across)
        jet = gauss(along + 1.5 + 2.0 * jet_t, 1.2 + 0.22 * side) * (side <= jet_reach) \
            * (1.0 - side / jet_reach) * (1.0 - jet_t)
        backflash = np.exp(-r / 14.0) * (along <= 0.0) * flash * 0.35
        color = color + mix((WHITE, hot + bloom * 0.8 + burst * 0.9 + spike * 0.9 + disc * 0.6),
                            (tint, bloom * 0.7 + burst * 0.6 + spike * 0.6 + ring * 1.1 + jet * 0.9 + backflash))

    # The game's light reaches 180 units; ease out sooner to keep the image handy.
    return color * (intensity * reach_fade(r, 105.0))[:, :, None]


# ---- IncendiaryRound.fx ------------------------------------------------------------

def fire_color(heat):
    red, orange = np.array([0.6, 0.07, 0.03]), np.array([1.0, 0.38, 0.07])
    yellow, white = np.array([1.0, 0.8, 0.35]), np.array([1.0, 0.97, 0.9])
    heat = sat(heat)[:, :, None]
    low = lerp(red, orange, heat / 0.4)
    mid = lerp(orange, yellow, (heat - 0.4) / 0.35)
    high = lerp(yellow, white, (heat - 0.75) / 0.25)
    return np.where(heat < 0.4, low, np.where(heat < 0.75, mid, high))


def cannon_round(u, v, t, hot, seed):
    """u, v = the quad's texture coordinates (0..1); the slug sits at u = 0.75."""
    px, py = (u - 0.75) * 4.0, v - 0.5
    slug = np.exp(-(px * px / 0.09 + py * py / 0.012))
    glow = 0.3 * np.exp(-(px * px / 0.3 + py * py / 0.05))
    behind = sat(-px / 2.9)
    flicker = noise(px * 3.0 + t * 26.0 + seed, py * 7.0 + seed * 0.37)
    width = (0.17 * (1.0 - behind) + 0.03) * (0.7 + 0.6 * flicker)
    # The shader stops the flame dead at the slug; at this size that shows as a flat face,
    # so the flame closes around the slug's nose instead.
    nose = np.exp(-(np.maximum(px, 0.0) / 0.14) ** 2)
    tail = sat(1.0 - np.abs(py) / width) * (1.0 - behind) ** 1.6 * (0.55 + 0.45 * flicker) * nose
    embers = 0.0
    for i in range(3):
        phase = float(frac(t * 2.3 + hash2(seed, i)))
        ax, ay = -phase * 2.9 * 0.9, (float(hash2(i, seed)) - 0.5) * 0.7 * phase
        embers = embers + np.exp(-((px - ax) ** 2 + (py - ay) ** 2) / 0.0025) * (1.0 - phase)
    heat = slug + glow + 0.8 * tail + 0.6 * embers
    edges = smoothstep(0.0, 0.06, u) * smoothstep(0.0, 0.06, 1.0 - u) * smoothstep(0.0, 0.2, 0.5 - np.abs(v - 0.5))
    strength = sat(heat) * edges
    return fire_color(heat * (0.7 + 0.45 * hot)) * strength[:, :, None]


# ---- ArcAnchor.fx ------------------------------------------------------------------

ARC_GLOW, ARC_HOT = np.array([0.27, 0.86, 1.0]), np.array([0.85, 0.98, 1.0])


def sd_rhombus(px, py, bx, by):
    px, py = np.abs(px), np.abs(py)
    wx, wy = bx - 2.0 * px, by - 2.0 * py
    h = np.clip((wx * bx - wy * by) / (bx * bx + by * by), -1.0, 1.0)
    d = np.hypot(px - 0.5 * bx * (1.0 - h), py - 0.5 * by * (1.0 + h))
    return d * np.sign(px * by + py * bx - bx * by)


def edge_fade(px, py):
    return sat((1.0 - np.maximum(np.abs(px), np.abs(py))) * 6.0)


def arc_lance(px, py, t, charge, flash):
    tip, tail = 0.95, -0.5
    centre = (tip + tail) * 0.5
    qx = px - centre
    stretch = np.where(qx > 0.0, tip - centre, centre - tail)
    d = sd_rhombus(qx / stretch, py, 1.0, 0.24) * stretch
    inside = smoothstep(0.015, -0.015, d)
    flow = noise(px * 7.0 + t * 6.0, py * 14.0 + t * 2.0)
    spine = np.exp(-np.abs(py) * 18.0) * smoothstep(tail, tip, px)
    rim = np.exp(-np.abs(d) * 24.0)
    halo = np.exp(-np.maximum(d, 0.0) * 10.0) * 0.25
    energy = (inside * (0.38 + 0.22 * flow) + rim * 0.25 + halo + spine * inside * 0.25) * (0.6 + 0.3 * charge + 0.7 * flash)
    color = lerp(ARC_GLOW, ARC_HOT, sat(spine * 0.35 + 0.4 * flash)[:, :, None])
    return color * (energy * edge_fade(px, py))[:, :, None]


def arc_ring(px, py, t, squash, charge, flash):
    rx, ry = px / squash, py
    radius = np.hypot(rx, ry)
    angle = np.arctan2(ry, rx)
    ring_radius = 0.5 + 0.38 * flash
    band = np.exp(-((radius - ring_radius) * 16.0) ** 2)
    slot = frac((angle + t * 1.7) / 6.2831853 * 3.0)
    arc_length = 0.3 + 0.55 * charge
    arc = smoothstep(0.0, 0.05, slot) * smoothstep(0.0, 0.05, arc_length - slot)
    faint = 0.12 + 0.2 * charge
    glow_core = np.exp(-radius * 8.0) * (0.3 + 0.7 * max(charge * 0.5, flash))
    energy = band * lerp(faint, 1.0, arc) * (0.6 + 0.4 * charge + flash) + glow_core
    color = lerp(ARC_GLOW, ARC_HOT, sat(flash + glow_core)[:, :, None])
    return color * (energy * edge_fade(px, py))[:, :, None]


# ---- ModuleEffects.fx: ModuleBolt --------------------------------------------------

def zigzag(x, seed):
    i = np.floor(x)
    a, b = hash1(i + seed), hash1(i + 1.0 + seed)
    return lerp(a, b, x - i) * 2.0 - 1.0, (b - a) * 2.0


def strand(along, length, amp, seed, coarse):
    u = sat(along / length)
    pin = np.maximum(np.sin(u * 3.14159), 0.0) ** 0.55
    big = zigzag(along * coarse, seed)
    mid = zigzag(along * coarse * 2.9, seed * 1.7 + 13.0)
    fine = zigzag(along * coarse * 7.3, seed * 2.3 + 71.0)
    offset = (big[0] + mid[0] * 0.42 + fine[0] * 0.16) * amp * pin
    slope = (big[1] + mid[1] * 0.42 * 2.9 + fine[1] * 0.16 * 7.3) * coarse * amp * pin
    return offset, slope


def strand_distance(along, across, length, st):
    beyond = np.maximum(np.maximum(-along, along - length), 0.0)
    off = np.abs(across - st[0])
    near = off / np.sqrt(1.0 + st[1] * st[1])
    return np.hypot(beyond, lerp(near, off, sat((off - 1.5) / 5.0)))


def fork(along, across, length, amp, width, channel, seed, coarse):
    split = length * (0.18 + 0.42 * hash1(seed * 0.731 + 5.0))
    side = -1.0 if hash1(seed * 1.37 + 9.0) < 0.5 else 1.0
    run = along - split
    reach_along = max(length * (0.22 + 0.2 * hash1(seed * 2.11 + 3.0)), 10.0)
    veer = side * run * (0.45 + 0.3 * hash1(seed * 0.37 + 1.0)) \
        + zigzag(run * coarse * 3.1, seed + 23.0)[0] * amp * 0.5 * sat(run / 5.0)
    d = np.abs(across - channel[0] - veer) * 0.85
    taper = sat(1.0 - run / reach_along) * (run >= 0.0)
    return (gauss(d, width * 0.55) * 0.85 + gauss(d, width * 2.4) * 0.14) * taper


def bolt(x, y, length, t, age, seed, tint, width=0.12, forks=0, intensity=1.0, reach=None):
    reach = reach or min(max(length * 0.22, 12.0), 30.0) + 10.0
    along, across = x, y
    seed = seed * 97.0
    w = 0.55 + width * 2.2
    amp = 0.26 * reach
    frame = math.floor(t * 24.0 + seed)
    shape = seed + frame * 17.3
    coarse = max(1.0 / 26.0, 3.0 / length)
    channel = strand(along, length, amp, shape, coarse)
    d1 = strand_distance(along, across, length, channel)
    d2 = strand_distance(along, across, length, strand(along, length, amp * 1.3, shape + 41.0, coarse * 0.8))
    core = gauss(d1, w)
    halo = (w * 3.2 / (d1 + w * 3.2)) ** 2 * reach_fade(d1, reach, 0.5)
    second = gauss(d2, w * 0.5) * 0.35 + gauss(d2, w * 2.5) * 0.06
    forked = {0: 0.0, 1: 0.5, 2: 1.0}[forks]
    fk = fork(along, across, length, amp, w, channel, shape, coarse) * min(forked * 4.0, 1.0) \
        + fork(along, across, length, amp, w, channel, shape + 57.0, coarse) * min(max(forked * 4.0 - 2.5, 0.0), 1.0)
    fk = fk * reach_fade(np.abs(across), reach, 0.5)
    front = min(age * 9.0, 1.0) * (length + reach)
    struck = smoothstep(front + 6.0, front - 6.0, along)
    flicker = 0.7 + 0.3 * float(hash1(frame * 3.7 + seed))
    fade = 1.0 - age
    life = (fade * fade * flicker + math.exp(-age * 16.0) * 0.9) * struck
    node = gauss(np.hypot(x - length, y), 2.0 + w * 2.0) * float(smoothstep(0.8, 1.0, age * 9.0)) * fade
    light = mix((tint, halo * 0.6 + second + node * 0.9 + fk * 0.6), (WHITE, core + node * 0.5 + fk * 0.7))
    return light * ((life + node) * intensity)[:, :, None]


# ---- Baking ------------------------------------------------------------------------

def bake(fn, x0, x1, y0, y1, px):
    """Evaluates fn(x, y) over the box (in the effect's units) at px pixels per unit."""
    w, h = int(round((x1 - x0) * px)), int(round((y1 - y0) * px))
    xs = (np.arange(w) + 0.5) / px + x0
    ys = (np.arange(h) + 0.5) / px + y0
    out = np.empty((h, w, 3), np.float32)
    for r in range(0, h, 192):
        x, y = np.meshgrid(xs, ys[r:r + 192])
        out[r:r + 192] = fn(x, y)
    return out


def save(name, light, anchor_px, kind, end_px=None, bloom=0.6):
    """Light (added to the scene in game) becomes colour + alpha, with a little of the
    game's bloom. Line effects are cropped symmetrically about their axis, radial ones
    about their centre, so the axis or centre stays in the middle of the image."""
    if bloom:
        hot = np.clip(light, 0.0, 1.5)
        for sigma, amount in ((1.6 * SCALE, 0.30 * bloom), (5.0 * SCALE, 0.16 * bloom)):
            light = light + amount * blur(hot, sigma)
    alpha = np.clip(light.max(axis=2), 0.0, 1.0)
    colour = np.clip(light / np.maximum(alpha, 1e-4)[:, :, None], 0.0, 1.0)
    visible = alpha > 2.5 / 255.0
    ys, xs = np.nonzero(visible)
    ax, ay = anchor_px
    pad = 2
    if kind == 'line':
        half = int(min(max(ay - ys.min(), ys.max() - ay) + pad, ay, light.shape[0] - ay))
        top, bottom = int(round(ay - half)), int(round(ay + half))
        left, right = max(xs.min() - pad, 0), min(xs.max() + pad + 1, light.shape[1])
    else:
        half = int(min(max(ay - ys.min(), ys.max() - ay, ax - xs.min(), xs.max() - ax) + pad,
                       ay, light.shape[0] - ay, ax, light.shape[1] - ax))
        top, bottom, left, right = (int(round(v)) for v in (ay - half, ay + half, ax - half, ax + half))
    rgba = np.dstack([colour, alpha])[top:bottom, left:right]
    rgba[rgba[:, :, 3] == 0] = 0
    img = Image.fromarray(np.round(rgba * 255).astype(np.uint8), 'RGBA')
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, compress_level=6)
    entry = dict(size=list(img.size), kind=kind)
    if kind == 'line':
        entry['start'] = [int(round(ax - left)), img.height // 2]
        entry['end'] = [int(round(end_px - left)), img.height // 2]
    ANCHORS[name] = entry
    print(name, img.size)


def line(name, fn, length, pad, px, pad_end=None, **kw):
    """A line effect from (0, 0) to (length, 0), padded by pad units around it."""
    pad_end = pad if pad_end is None else pad_end
    light = bake(fn, -pad, length + pad_end, -pad, pad, px)
    save(name, light, (pad * px, pad * px), 'line', end_px=(pad + length) * px, **kw)


def radial(name, fn, radius, px, **kw):
    light = bake(fn, -radius, radius, -radius, radius, px)
    save(name, light, (radius * px, radius * px), 'radial', **kw)


def main():
    # -- Mining laser: three looks, complete beams at a few lengths plus loose pieces. --
    looks = dict(
        laser=(3.0, rgb(255, 95, 35), rgb(255, 190, 110), 1.0),
        laser_thin=(1.8, rgb(255, 95, 35), rgb(255, 190, 110), 1.0),
        laser_surge=(3.0 * 1.35, rgb(230, 145, 80), rgb(245, 190, 135), 0.85),
        planet_cracker=(6.0, rgb(110, 215, 255), rgb(215, 245, 255), 0.6),
    )
    for look, (w, beam_c, flare_c, intensity) in looks.items():
        lengths = (400, 700, 1000) if look in ('laser', 'laser_thin') else (700,)
        for n in lengths:
            length = n / 8.0
            line('laser/%s_beam_%d.png' % (look, n),
                 lambda x, y: laser(x, y, length, w, 0.37, beam_c, flare_c, intensity), length, w * 10.0, PX)
        line('laser/%s_body_only.png' % look,
             lambda x, y: laser(x, y, 75.0, w, 0.37, beam_c, flare_c, intensity, parts='body'), 75.0, w * 10.0, PX)
        radial('laser/%s_emitter_flare.png' % look,
               lambda x, y: laser(x, y, 400.0, w, 0.37, beam_c, flare_c, intensity, parts='emitter'), w * 8.0, PX)
        radial('laser/%s_impact_flare.png' % look,
               lambda x, y: laser(x + 400.0, y, 400.0, w, 0.37, beam_c, flare_c, intensity, parts='impact'), w * 12.0, PX)

    # -- Cannon: incendiary rounds (automatic and critical) and a muzzle flash. --
    for hot, tag, height in ((0.5, 'round', 8.0), (1.0, 'round_critical', 14.4)):
        for i, seed in enumerate((23, 101, 187, 64)[:4 if hot < 1 else 2]):
            h_px = int(round(height * PX))
            us = (np.arange(h_px * 4) + 0.5) / (h_px * 4)
            vs = (np.arange(h_px) + 0.5) / h_px
            u, v = np.meshgrid(us, vs)
            light = cannon_round(u, v, 0.21 + i * 0.13, hot, float(seed)).astype(np.float32)
            save('cannon/%s_%d.png' % (tag, i + 1), light, (h_px * 3.0, h_px / 2.0), 'line', end_px=h_px * 3.0)

    def muzzle_flash(x, y):
        r = np.hypot(x, y)
        theta = np.arctan2(y, x)
        petals = (0.5 + 0.5 * np.cos(theta * 5.0 + 0.6)) ** 6.0
        hot = np.exp(-(r / 1.6) ** 2) * 1.6
        puff = gauss(r, 5.0) * 0.8 + petals * np.exp(-r / 5.5) * 0.7 * smoothstep(-3.0, 2.0, x)
        spike = gauss(y, 1.1) * np.exp(-np.maximum(x, 0.0) / 9.0) * (x >= 0) * 0.9
        return mix((WHITE, hot + spike * 0.6), (rgb(255, 120, 45), puff + spike),
                   (np.array([1.0, 0.8, 0.35]), gauss(r, 2.8) * 0.8))
    radial('cannon/muzzle_flash.png', muzzle_flash, 30.0, PX)

    # -- Railgun: charge-up, muzzle flash, the round in flight and the trail it leaves. --
    radial('railgun/charge_vortex.png', lambda x, y: rail_muzzle(x, y, 0.4, wind_up=0.62), 105.0, RAIL_PX)
    radial('railgun/charge_star.png', lambda x, y: rail_muzzle(x, y, 0.4, wind_up=0.93), 105.0, RAIL_PX)
    radial('railgun/muzzle_flash.png', lambda x, y: rail_muzzle(x, y, 0.4, shot=0.03), 105.0, RAIL_PX)
    radial('railgun/muzzle_shock_ring.png', lambda x, y: rail_muzzle(x, y, 0.4, shot=0.14), 105.0, RAIL_PX)
    for n in (500, 800):
        length = n / 6.5
        line('railgun/round_in_flight_%d.png' % n,
             lambda x, y: rail_trail(x, y, length * 1.25, 0.4, 0.10, 0.31, 0.125, 0.8), length, 56.0, RAIL_PX,
             pad_end=30.0)
        line('railgun/shot_impact_%d.png' % n,
             lambda x, y: rail_trail(x, y, length, 0.4, 0.15, 0.31, 0.12, 1.0), length, 56.0, RAIL_PX)
        line('railgun/ion_trail_%d.png' % n,
             lambda x, y: rail_trail(x, y, length, 0.4, 0.60, 0.31, 0.12, 1.0, intensity=2.0), length, 56.0, RAIL_PX)

    # -- Arc Harpoon: energy lance, surface rings and lightning. --
    grid = int(round(240 * SCALE))
    c = (np.arange(grid) + 0.5) / grid * 2.0 - 1.0
    qx, qy = np.meshgrid(c, c)
    lance = arc_lance(qx, qy, 0.3, 0.8, 0.25).astype(np.float32)
    save('harpoon/energy_lance.png', lance, (grid / 2.0, grid / 2.0), 'radial')
    for tag, squash, flash in (('ring', 1.0, 0.0), ('ring_pulse', 1.0, 0.7), ('ring_on_surface', 0.45, 0.0)):
        ring = arc_ring(qx, qy, 0.3, squash, 0.9, flash).astype(np.float32)
        save('harpoon/%s.png' % tag, ring, (grid / 2.0, grid / 2.0), 'radial')
    cyan = rgb(70, 220, 255)
    for n in (300, 600, 900):
        length = n / 8.0
        for i, (seed, forks) in enumerate(((0.13, 0), (0.47, 1), (0.71, 2))):
            line('harpoon/lightning_%d_%d.png' % (n, i + 1),
                 lambda x, y: bolt(x, y, length, 0.5 + i * 0.21, 0.10, seed + n * 0.001, cyan, forks=forks),
                 length, 42.0, PX)

    OUT.mkdir(parents=True, exist_ok=True)
    with open(OUT / 'anchors.json', 'w') as f:
        json.dump(ANCHORS, f, indent=1, sort_keys=True)
        f.write('\n')


if __name__ == '__main__':
    main()
