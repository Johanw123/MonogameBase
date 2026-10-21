#!/usr/bin/env python3
"""Prepare purchased Synthwave WAVs for the in-game radio (requires ffmpeg).

Keeps the originals untouched. Only main stems, one transition per stem track,
and the full loops from the other pack are shipped. No intros/short video cuts.
"""
import json
import pathlib
import re
import subprocess
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Content/Music/Synthwave"
OUTPUT = ROOT / "Content/Music/Radio"
TITLES = {"TheDeep": "The Deep", "AMagicalAdventure": "A Magical Adventure",
          "SpaceShip": "Space Ship", "OldStarship": "Old Starship",
          "NeonCityVibe": "Neon City Vibe"}
ROLES = {"Bass": "Bass", "Synth": "Pad", "SynthArp": "Arp",
         "Drums": "Drums", "Perc": "Percussion", "SoloSynth": "Lead"}


def frames(path):
    with wave.open(str(path)) as audio:
        assert (audio.getframerate(), audio.getnchannels(), audio.getsampwidth()) == (44100, 2, 2), path
        return audio.getnframes()


def encode(path, name, count):
    destination = OUTPUT / (name + ".ogg")
    if not destination.exists() or destination.stat().st_mtime < max(path.stat().st_mtime, pathlib.Path(__file__).stat().st_mtime):
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(path),
                        "-af", f"apad=whole_len={count},atrim=end_sample={count}",
                        "-c:a", "libvorbis", "-q:a", "5", str(destination)], check=True)
    return destination.name


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    tracks = []
    for directory in sorted(SOURCE.iterdir()):
        matches = []
        for path in sorted(directory.glob("*_STEMS_Main*.wav")):
            match = re.fullmatch(r"(.+)_STEMS_Main(\d?)_(\w+)_(\d+)bpm", path.stem)
            assert match, path
            song, section, instrument, tempo = match.groups()
            bpm = int(tempo)
            # Some leads/pads span sixteen bars; most stems span eight. The
            # one-sample export differences must not create independent clocks.
            bars = round(frames(path) / (44100 * 240 / bpm))
            assert bars in (8, 16), path
            count = round(44100 * 240 * bars / bpm)
            assert abs(count - frames(path)) <= 1, path
            matches.append((path, song, int(section or 0), ROLES[instrument], bpm, count))
        if matches:
            song, bpm = matches[0][1], matches[0][4]
            sections = sorted({m[2] for m in matches if m[2]})
            stems = []
            for path, _, section, role, _, count in matches:
                active = [section] if section else [s for s in sections if not any(m[2] == s and m[3] == role for m in matches)]
                stems.append(dict(file=encode(path, path.stem, count), frames=count, role=role, sections=active))
            transition = next(directory.glob("*_STEMS_FXTransition1_*.wav"))
            count = frames(transition)
            stems.append(dict(file=encode(transition, transition.stem, count), frames=count, role="Transition", sections=[]))
            tracks.append(dict(name=TITLES[song], bpm=bpm, phraseFrames=round(44100 * 240 * 16 / bpm), sections=sections, stems=stems))
        else:
            path = next(directory.glob("* (loop).wav"))
            count = frames(path)
            tracks.append(dict(name=directory.name[3:], bpm=0, phraseFrames=count, sections=[1],
                               stems=[dict(file=encode(path, path.stem, count), frames=count, role="Mix", sections=[1])]))
    (OUTPUT / "catalog.json").write_text(json.dumps(tracks, indent=2) + "\n")
    print(f"Prepared {len(tracks)} tracks; {sum(p.stat().st_size for p in OUTPUT.glob('*.ogg')) / 1048576:.1f} MB of Ogg audio.")


if __name__ == "__main__":
    main()
