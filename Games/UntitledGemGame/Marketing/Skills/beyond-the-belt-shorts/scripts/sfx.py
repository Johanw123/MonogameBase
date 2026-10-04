#!/usr/bin/env python3
"""Rebuild a take's game audio from its capture log.

Offscreen captures are silent, but the game logs every sound it plays (file,
volume, pitch, pan, time). This mixes the game's own sound files at those
times into a 48 kHz stereo WAV that lines up with the take's video.

  sfx.py takes/fleet.capture.json [-o takes/fleet.sfx.wav]
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import wave
from functools import lru_cache
from pathlib import Path

import numpy as np

RATE = 48000
GAME_ROOT = Path(__file__).resolve().parents[4]


@lru_cache(maxsize=None)
def load(asset: str) -> np.ndarray:
    """Decode a Content asset (path as the game loads it) to float32 stereo."""
    base = GAME_ROOT / 'Content' / asset
    candidates = [base] + [base.with_suffix(ext) for ext in ('.wav', '.ogg', '.mp3') if not base.suffix]
    path = next((p for p in candidates if p.is_file()), None)
    if path is None:
        raise FileNotFoundError(f'sound asset {asset} not found under {GAME_ROOT / "Content"}')
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-f', 'f32le', '-ac', '2', '-ar', str(RATE), '-'],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)


def pitched(sound: np.ndarray, pitch: float) -> np.ndarray:
    # MonoGame pitch is in octaves (-1..1): playback rate 2^pitch, which also changes length.
    rate = 2.0 ** pitch
    if abs(rate - 1) < 1e-4:
        return sound
    positions = np.arange(0, len(sound) - 1, rate)
    return np.stack([np.interp(positions, np.arange(len(sound)), sound[:, c]) for c in (0, 1)], axis=1).astype(np.float32)


def render(report: dict) -> np.ndarray:
    frames = report['frames'] / report['fps']
    mix = np.zeros((int(frames * RATE) + RATE, 2), dtype=np.float32)
    missing = set()
    for event in report['events']:
        if event['type'] != 'sound':
            continue
        try:
            sound = load(event['file'])
        except FileNotFoundError:
            missing.add(event['file'])
            continue
        sound = pitched(sound, event.get('pitch') or 0.0) * (event.get('volume') or 1.0)
        pan = event.get('pan') or 0.0
        # Constant-power pan.
        angle = (pan + 1) * np.pi / 4
        sound = sound * np.array([np.cos(angle), np.sin(angle)], dtype=np.float32) * np.sqrt(2)
        start = int(round(event['t'] * RATE))
        end = min(len(mix), start + len(sound))
        if end > start:
            mix[start:end] += sound[:end - start]
    for asset in sorted(missing):
        print(f'warning: {asset} missing, skipped', file=sys.stderr)
    return mix[:int(frames * RATE)]


def write(path: Path, audio: np.ndarray) -> None:
    pcm = (np.clip(audio, -1, 1) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('report', type=Path, help='<take>.capture.json')
    parser.add_argument('-o', '--output', type=Path)
    args = parser.parse_args()
    report = json.loads(args.report.read_text())
    out = args.output or args.report.with_name(args.report.name.replace('.capture.json', '.sfx.wav'))
    audio = render(report)
    write(out, audio)
    peak = float(np.abs(audio).max()) if len(audio) else 0.0
    sounds = sum(1 for e in report['events'] if e['type'] == 'sound')
    print(f'{out} ({sounds} sounds, peak {20 * np.log10(max(peak, 1e-9)):.1f} dBFS)')


if __name__ == '__main__':
    main()
