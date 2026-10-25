#!/usr/bin/env python3
"""Rebuild the curated stock SFX using Python 3 and FFmpeg (no synthesis)."""
import argparse
import array
import json
import math
from pathlib import Path
import subprocess
import sys
import wave

PROJECT = Path(__file__).resolve().parents[2]
SELECTIONS = Path(__file__).with_name("shapeform-selections.json")
RATE = 44100


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("library", type=Path, help="Shapeform library root")
    args = parser.parse_args()
    output = PROJECT / "Content/SFX/Shapeform"
    output.mkdir(parents=True, exist_ok=True)
    report = ["# Shapeform sound selections", "",
              "Stock recordings from the local Shapeform library, imported 2026-10-18.",
              "No audio was synthesized. Original recordings stay outside the repository.", "",
              "Rebuild with `python3 tools/audio/import_shapeform.py /path/to/Shapeform`.",
              "Requires FFmpeg. Selections and source paths live in",
              "`tools/audio/shapeform-selections.json`.", "",
              "Processing: mono 44.1 kHz / 16-bit PCM; remove leading silence (keep 3 ms),",
              "45 Hz high-pass, gentle low-pass, limit long tails, 20 ms fade-out,",
              "balance to at most -23 dBFS RMS and -6 dBFS sample peak.",
              "The gain is linear; quiet transients retain their dynamics.", "",
              "| Game asset | Source recording (relative to library root) | Source / game duration | Game RMS |",
              "| --- | --- | --- | --- |"]
    selections = json.loads(SELECTIONS.read_text())
    for selection in selections:
        source = args.library / selection["source"]
        raw = subprocess.check_output([
            "ffmpeg", "-v", "error", "-i", str(source), "-ac", "1", "-ar", str(RATE),
            "-af", f"highpass=f=45,lowpass=f={selection.get('lowpass', 7000)}",
            "-f", "f32le", "-",
        ])
        samples = array.array("f", raw)
        if sys.byteorder != "little":
            samples.byteswap()
        original_duration = len(samples) / RATE
        peak = max(abs(v) for v in samples)
        onset = next(i for i, v in enumerate(samples) if abs(v) > peak * 0.003)
        samples = samples[max(0, onset - int(RATE * 0.003)):]
        samples = samples[:int(RATE * selection["maxSeconds"])]
        fade = min(int(RATE * 0.02), len(samples))
        for i in range(fade):
            samples[len(samples) - fade + i] *= (fade - 1 - i) / fade
        peak = max(abs(v) for v in samples)
        rms = math.sqrt(sum(v * v for v in samples) / len(samples))
        gain = min(10 ** (-6 / 20) / peak, 10 ** (-23 / 20) / rms)
        pcm = array.array("h", (round(v * gain * 32767) for v in samples))
        if sys.byteorder != "little":
            pcm.byteswap()
        with wave.open(str(output / (selection["name"] + ".wav")), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(pcm.tobytes())
        report.append(f"| {selection['name']}.wav | {selection['source']} | "
                      f"{original_duration:.2f} / {len(samples) / RATE:.2f} s | {20 * math.log10(rms * gain):.1f} dBFS |")
    (output / "SOURCES.md").write_text("\n".join(report) + "\n")
    print(f"Imported {len(selections)} selections; see {output / 'SOURCES.md'}")


if __name__ == "__main__":
    main()
