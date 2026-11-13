"""Measure a finished render for faults a contact sheet cannot show.

Every frame is decoded and compared with the next at low resolution, so this
finds stutter, freezes, black frames and lost frames, then measures loudness on
the delivered file. check() returns (problems, notes): problems fail the render,
notes are things that may be intended, such as a menu held still under a caption.
"""
from __future__ import annotations

import re
import subprocess
from pathlib import Path
from typing import Iterator

import numpy as np

WIDTH, HEIGHT = 180, 320
HELD = 0.1           # mean change (of 255) between two frames that show the same picture
MOVING = 0.6         # clearly moving footage; x264 noise on near-still footage reaches about 0.45
CONTEXT = 15         # frames each side of a held frame that must be moving
BLACK = 3.0          # mean brightness (of 255) of a black frame
STILL_NOTE = 1.0     # seconds without change worth a note
TARGET_LUFS, LUFS_TOLERANCE = -14.0, 1.5
PEAK_LIMIT = -1.0    # dBTP


def measure_frames(video: Path) -> tuple[np.ndarray, np.ndarray]:
    """Mean brightness of each frame, and mean change from each frame to the next."""
    size = WIDTH * HEIGHT
    process = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', str(video), '-vf',
                                f'scale={WIDTH}:{HEIGHT}:flags=area,format=gray', '-f', 'rawvideo', '-'],
                               stdout=subprocess.PIPE)
    brightness, change, previous = [], [], None
    while len(data := process.stdout.read(size)) == size:
        frame = np.frombuffer(data, np.uint8).astype(np.int16)
        brightness.append(frame.mean())
        if previous is not None:
            change.append(np.abs(frame - previous).mean())
        previous = frame
    if process.wait():
        raise RuntimeError(f'{video} does not decode cleanly')
    return np.array(brightness), np.array(change)


def spans(mask: np.ndarray) -> Iterator[tuple[int, int]]:
    """Start and stop of each run of True values."""
    edges = np.flatnonzero(np.diff(np.concatenate(([0], mask.astype(np.int8), [0]))))
    yield from zip(edges[::2].tolist(), edges[1::2].tolist())


def repeated_frames(change: np.ndarray) -> list[int]:
    """Frames of one shot that stopped while the footage around them kept moving."""
    repeated = []
    for start, stop in spans(change < HELD):
        before, after = change[max(0, start - CONTEXT):start], change[stop:stop + CONTEXT]
        sides = [side for side in (before, after) if len(side) >= 5]
        if sides and all(np.median(side) >= MOVING for side in sides):
            repeated += range(start, stop)
    return repeated


def loudness(video: Path) -> tuple[float, float]:
    """Integrated loudness (LUFS) and true peak (dBTP) of the delivered audio."""
    out = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', str(video), '-af', 'ebur128=peak=true',
                          '-f', 'null', '-'], capture_output=True, text=True).stderr
    return (float(re.findall(r'I:\s+(-?[\d.]+) LUFS', out)[-1]),
            float(re.findall(r'Peak:\s+(-?inf|-?[\d.]+) dBFS', out)[-1]))


def check(edit, has_audio: bool) -> tuple[list[str], list[str]]:
    brightness, change = measure_frames(edit.output)
    fps = edit.fps
    problems, notes = [], []
    expected = round(edit.total * fps)
    if abs(len(brightness) - expected) > 1:
        problems.append(f'{len(brightness)} frames, expected {expected}')

    for number, shot in enumerate(edit.shots, 1):
        first = round(shot['at'] * fps)
        last = min(round((shot['at'] + shot['dur']) * fps), len(brightness))
        repeated = repeated_frames(change[first:last - 1])
        if repeated:
            problems.append(f"shot {number}: {len(repeated)} repeated frame(s) between "
                            f"{(first + repeated[0]) / fps:.2f}s and {(first + repeated[-1] + 1) / fps:.2f}s")
        # Fades to black are deliberate, so only the rest of the shot has to show a picture.
        lit_from = first + round(shot.get('fade_in', 0) * fps)
        lit_to = last - round(shot.get('fade_out', 0) * fps)
        black = np.flatnonzero(brightness[lit_from:lit_to] < BLACK)
        if len(black):
            problems.append(f'shot {number}: {len(black)} black frame(s) from {(lit_from + black[0]) / fps:.2f}s')

    card = (edit.spec.get('end_card') or {}).get('at', edit.total)
    for start, stop in spans(change[:round(card * fps)] < HELD):
        if (stop - start) / fps >= STILL_NOTE:
            notes.append(f'nothing moves from {start / fps:.2f}s to {stop / fps:.2f}s')

    if has_audio:
        lufs, peak = loudness(edit.output)
        if abs(lufs - TARGET_LUFS) > LUFS_TOLERANCE:
            problems.append(f'loudness {lufs:.1f} LUFS, expected {TARGET_LUFS:.0f}')
        if peak > PEAK_LIMIT:
            problems.append(f'true peak {peak:.1f} dBTP, limit {PEAK_LIMIT:.0f}')
    return problems, notes
