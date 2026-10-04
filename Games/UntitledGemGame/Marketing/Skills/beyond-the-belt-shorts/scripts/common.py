"""Shared helpers for the Beyond the Belt shorts scripts."""
from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path

# scripts/ -> skill -> Skills -> Marketing -> game root (symlinked installs resolve to the repo copy)
GAME_ROOT = Path(__file__).resolve().parents[4]
BUILD = GAME_ROOT / 'bin' / 'Debug' / 'net10.0'
FONT_BOLD = '/usr/share/fonts/noto/NotoSans-Bold.ttf'
FONT_REGULAR = '/usr/share/fonts/noto/NotoSans-Regular.ttf'
FONT_BLACK = '/usr/share/fonts/noto/NotoSans-Black.ttf'
LOGO = GAME_ROOT / 'Content' / 'Textures' / 'logo_4k.png'
SUFFIXES = ['', 'K', 'M', 'B', 'T', 'Qa', 'Qi', 'Sx', 'Sp', 'Oc', 'No', 'Dc']


def game_number(value: float, decimals: bool = True) -> str:
    """Format like the game's HUD (NumberFormatter.AbbreviateBigNumber): 12.57T."""
    value = int(value)
    if value < 1000:
        return str(value)
    magnitude = min((len(str(value)) - 1) // 3, len(SUFFIXES) - 1)
    hundredths = value * 100 // 10 ** (magnitude * 3)
    whole, fraction = divmod(hundredths, 100)
    if not decimals or fraction == 0:
        return f'{whole}{SUFFIXES[magnitude]}'
    return f'{whole}.{fraction:02d}{SUFFIXES[magnitude]}'


def word_number(value: float) -> str:
    """12 TRILLION style, for headlines."""
    names = ['', 'THOUSAND', 'MILLION', 'BILLION', 'TRILLION', 'QUADRILLION']
    value = int(value)
    magnitude = min((len(str(value)) - 1) // 3, len(names) - 1) if value >= 1000 else 0
    whole = value // 10 ** (magnitude * 3)
    return f'{whole} {names[magnitude]}'.strip()


def probe(path: Path) -> dict:
    out = subprocess.run(['ffprobe', '-v', 'error', '-print_format', 'json', '-show_format', '-show_streams', str(path)],
                         check=True, capture_output=True, text=True).stdout
    return json.loads(out)


def duration(path: Path) -> float:
    return float(probe(path)['format']['duration'])


def contact_sheet(video: Path, out: Path, times: list[float], labels: list[str] | None = None,
                  tile_height: int = 480, columns: int = 8) -> Path:
    """One labelled frame per time, tiled into a single JPEG for review."""
    tiles = []
    for i, t in enumerate(times):
        label = (labels[i] if labels else f'{t:.2f}s').replace(':', r'\:').replace("'", '')
        tile = out.with_name(f'.{out.stem}_{i:03d}.png')
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-ss', f'{max(0.0, t):.3f}', '-i', str(video), '-frames:v', '1',
                        '-vf', f'scale=-2:{tile_height},drawtext=fontfile={FONT_BOLD}:text=\'{label}\':fontcolor=yellow:'
                               f'fontsize={tile_height // 22}:x=8:y=8:box=1:boxcolor=black@0.6:boxborderw=4',
                        str(tile)], check=True)
        tiles.append(tile)

    inputs = sum((['-i', str(t)] for t in tiles), [])
    layout = f'xstack=inputs={len(tiles)}:fill=black:layout=' + '|'.join(
        f'{"+".join(["w0"] * (i % columns)) or 0}_{"+".join(["h0"] * (i // columns)) or 0}' for i in range(len(tiles)))
    graph = layout if len(tiles) > 1 else 'null'
    subprocess.run(['ffmpeg', '-v', 'error', '-y', *inputs, '-filter_complex', graph, '-frames:v', '1', '-q:v', '3', str(out)],
                   check=True)
    for tile in tiles:
        tile.unlink()
    return out


def load_jsonc(path: Path) -> dict:
    """JSON with // comments and trailing commas, as the game's scene reader accepts."""
    text, out, i, in_string = path.read_text(), [], 0, False
    while i < len(text):
        c = text[i]
        if in_string:
            out.append(c)
            if c == '\\':
                out.append(text[i + 1])
                i += 1
            elif c == '"':
                in_string = False
        elif c == '"':
            in_string = True
            out.append(c)
        elif text.startswith('//', i):
            i = text.find('\n', i)
            if i < 0:
                break
            continue
        else:
            out.append(c)
        i += 1
    return json.loads(re.sub(r',(\s*[}\]])', r'\1', ''.join(out)))
