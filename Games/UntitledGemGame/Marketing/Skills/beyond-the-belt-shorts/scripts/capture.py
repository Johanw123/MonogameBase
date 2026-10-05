#!/usr/bin/env python3
"""Record Beyond the Belt scenes with the game's capture mode.

Each scene JSON (format: references/scenes.md) becomes a take: <output>.mp4,
<output>.capture.json (events and stats), <output>.sfx.wav (game audio rebuilt
from the log) and <output>.sheet.jpg (contact sheet for review).

  capture.py shots/*.json              # final takes
  capture.py shots/hook.json --preview # quick 540x960 30 fps look first
  capture.py --list catalog.json       # presets, upgrades, stats, abilities...

Rebuilds the game first when sources are newer than the build. Runs offscreen
(SDL offscreen, OpenAL null): no window, no sound, no Steam, never the player's
save or Settings.json.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from common import BUILD, FONT_BLACK, GAME_ROOT, contact_sheet, game_number, load_jsonc
import sfx

DLL = BUILD / 'UntitledGemGame.dll'
ENV = dict(os.environ, SDL_VIDEODRIVER='offscreen', ALSOFT_DRIVERS='null')


def ensure_build(skip: bool) -> None:
    sources = [p for pattern in ('**/*.cs', 'Content/Data/*.json', '*.csproj') for p in GAME_ROOT.glob(pattern)
               if '/obj/' not in str(p) and '/bin/' not in str(p) and '/Tests/' not in str(p)]
    newest = max(p.stat().st_mtime for p in sources)
    if DLL.is_file() and DLL.stat().st_mtime >= newest:
        return
    if skip:
        print('warning: the build is older than the sources (--no-build given)', file=sys.stderr)
        return
    print('building the game (sources changed)...', flush=True)
    result = subprocess.run(['dotnet', 'build', str(GAME_ROOT / 'UntitledGemGame.csproj'), '-c', 'Debug', '--nologo', '-v', 'q'],
                            capture_output=True, text=True)
    if result.returncode != 0:
        print('\n'.join(line for line in result.stdout.splitlines() if ' error ' in line) or result.stdout[-3000:])
        sys.exit('build failed')


def run_game(args: list[str], log: Path, limit: float = 900) -> int:
    """Run the game; stop it if it hangs on shutdown after finishing (seen rarely) or overall."""
    with log.open('w') as handle:
        process = subprocess.Popen(['dotnet', str(DLL), *args], cwd=GAME_ROOT, env=ENV, stdout=handle,
                                   stderr=subprocess.STDOUT)
        started, finished = time.time(), None
        while process.poll() is None:
            time.sleep(0.5)
            if finished is None and any(m in log.read_text(errors='replace') for m in ('CAPTURE DONE', 'CAPTURE LIST')):
                finished = time.time()
            if (finished and time.time() - finished > 15) or time.time() - started > limit:
                process.kill()
                process.wait()
                print(f'  note: the game did not exit by itself ({"after finishing" if finished else "timeout"}); stopped it',
                      file=sys.stderr)
                return 0 if finished else 124
        return process.returncode


def gem_text(action: dict) -> list[dict]:
    """Expand {"do": "gem_text", "text": "WISHLIST\nNOW", ...} into one "gems" action per line.

    The text is drawn with a heavy font and sampled on a dot grid; every filled cell becomes a gem.
    pos: centre of the block (normalized), width: block width (fraction of the frame width),
    rows: dots per line height, gap: space between lines (in line heights), gem: type or one per line.
    """
    from PIL import Image, ImageDraw, ImageFont
    lines = action['text'].split('\n')
    rows = action.get('rows', 11)
    cell = 40
    font = ImageFont.truetype(FONT_BLACK, int(rows * cell * 1.3))
    grids = []
    for line in lines:
        left, top, right, bottom = font.getbbox(line)
        image = Image.new('L', (right - left + cell, bottom - top + cell))
        ImageDraw.Draw(image).text((cell // 2 - left, cell // 2 - top), line, font=font, fill=255)
        cols, line_rows = image.width // cell, image.height // cell
        dots = [(c, r) for r in range(line_rows) for c in range(cols)
                if sum(image.crop((c * cell, r * cell, (c + 1) * cell, (r + 1) * cell)).getdata()) / (cell * cell * 255) >= 0.45]
        grids.append((dots, cols, line_rows))
    widest = max(cols for _, cols, _ in grids)
    aspect = action.get('aspect', 9 / 16)
    spacing_x = action.get('width', 0.86) / widest           # in frame widths
    spacing_y = spacing_x * aspect                           # same physical spacing vertically
    gap = action.get('gap', 1.0)
    heights = [line_rows * spacing_y for _, _, line_rows in grids]
    total = sum(heights) + gap * max(heights) * (len(lines) - 1)
    cx, cy = action.get('pos', [0.5, 0.5])
    y = cy - total / 2
    gems = action.get('gem', 'Red')
    out = []
    for i, ((dots, cols, line_rows), height) in enumerate(zip(grids, heights)):
        x0 = cx - cols * spacing_x / 2
        points = [[round(x0 + (c + 0.5) * spacing_x, 5), round(y + (r + 0.5) * spacing_y, 5)] for c, r in dots]
        out.append({'at': action.get('at', 0), 'do': 'gems', 'dur': action.get('dur', 0), 'points': points,
                    'gem': gems[i % len(gems)] if isinstance(gems, list) else gems,
                    'from': action.get('from', ''), 'order': action.get('order', 'left')})
        y += height + gap * max(heights)
    return out


# How each gem type looks on screen (the game's tints are shader inputs, not display colours).
GEM_DISPLAY = {'Red': (255, 90, 110), 'LightGreen': (120, 230, 140), 'Blue': (120, 150, 255), 'Teal': (90, 220, 220),
               'Lilac': (200, 150, 255), 'Purple': (160, 90, 240), 'Gold': (255, 200, 90), 'DarkBlue': (70, 80, 200)}


def gem_image(action: dict, scene_dir: Path) -> list[dict]:
    """Expand {"do": "gem_image", "image": "logo.png", ...} into "gems" actions, one per gem type.

    The image's opaque pixels are sampled on a grid of `cols` columns; each filled cell becomes a gem.
    gem: list of types; colour "nearest" (by display colour) or "x" (bands left to right, like a gradient).
    pos: centre (normalized), width: fraction of the frame width.
    """
    from PIL import Image
    image = Image.open((scene_dir / action['image']).expanduser()).convert('RGBA')
    image = image.crop(image.getbbox())
    cols = action.get('cols', 70)
    cell = image.width / cols
    rows = max(1, round(image.height / cell))
    small = image.resize((cols, rows), Image.Resampling.BOX)
    aspect = action.get('aspect', 9 / 16)
    spacing_x = action.get('width', 0.86) / cols
    spacing_y = spacing_x * aspect
    cx, cy = action.get('pos', [0.5, 0.5])
    x0, y0 = cx - cols * spacing_x / 2, cy - rows * spacing_y / 2
    types = action.get('gem', ['Teal', 'Blue'])
    types = types if isinstance(types, list) else [types]
    mode = action.get('colour', 'x')
    points = {t: [] for t in types}
    for r in range(rows):
        for c in range(cols):
            red, green, blue, alpha = small.getpixel((c, r))
            if alpha < action.get('alpha', 140):
                continue
            if mode == 'x':
                kind = types[min(len(types) - 1, c * len(types) // cols)]
            else:
                kind = min(types, key=lambda t: sum((a - b) ** 2 for a, b in zip(GEM_DISPLAY[t], (red, green, blue))))
            points[kind].append([round(x0 + (c + 0.5) * spacing_x, 5), round(y0 + (r + 0.5) * spacing_y, 5)])
    return [{'at': action.get('at', 0), 'do': 'gems', 'dur': action.get('dur', 0), 'points': pts, 'gem': kind,
             'from': action.get('from', ''), 'order': action.get('order', 'left')} for kind, pts in points.items() if pts]


def prepare_scene(scene_path: Path, scene: dict, preview: bool, workdir: Path) -> Path:
    """Absolute output, gem_text expanded, and for previews the same shot at 540 px wide and 30 fps
    (framing is identical: the game renders at a 4K pixel area)."""
    scene = dict(scene)
    output = Path(scene['output'])
    if not output.is_absolute():
        output = (scene_path.parent / output).resolve()
    width, height = scene.get('width', 2160), scene.get('height', 3840)
    actions = []
    for action in scene.get('actions', []):
        if action.get('do') == 'gem_text':
            actions += gem_text(dict(action, aspect=width / height))
        elif action.get('do') == 'gem_image':
            actions += gem_image(dict(action, aspect=width / height), scene_path.parent)
        else:
            actions.append(action)
    scene['actions'] = actions
    if preview:
        scale = 540 / min(width, height)
        scene.update(width=int(width * scale) // 2 * 2, height=int(height * scale) // 2 * 2, fps=30, encoder='x264',
                     quality=23)
        output = output.with_name(output.stem + '.preview.mp4')
    scene['output'] = str(output)
    path = workdir / scene_path.name
    path.write_text(json.dumps(scene))
    return path


def summarize(take: Path, report: dict) -> str:
    events = report['events']
    abilities = [f"{e['id']}@{e['t']:.1f}" for e in events if e['type'] == 'ability']
    actions = [f"{e['id']}@{e['t']:.1f}" for e in events if e['type'] == 'action']
    samples = report['samples']
    gems = f"{game_number(samples[0]['gems'])} -> {game_number(samples[-1]['gems'])}" if samples else '?'
    rate = game_number(samples[-1]['gems_per_minute']) + '/min' if samples else '?'
    sounds = sum(1 for e in events if e['type'] == 'sound')
    clicks = sum(1 for e in events if e['type'] == 'click')
    return (f"  {take.name}: {report['frames'] / report['fps']:.1f}s, rendered in {report['render_seconds']}s\n"
            f"    gems {gems}, {rate}, {samples[-1]['active_gems'] if samples else '?'} gems on screen\n"
            f"    actions: {', '.join(actions) or '-'}\n"
            f"    abilities fired: {', '.join(abilities) or '-'}\n"
            f"    {clicks} clicks, {sounds} game sounds")


def capture(scene_path: Path, preview: bool, overwrite: bool, workdir: Path) -> bool:
    run_path = prepare_scene(scene_path, load_jsonc(scene_path), preview, workdir)
    output = Path(load_jsonc(run_path)['output'])
    if output.exists() and not (overwrite or preview):
        print(f'FAILED {scene_path.name}: {output} exists; choose a new take name or pass --overwrite',
              file=sys.stderr)
        return False
    log = output.with_suffix('.log')
    output.parent.mkdir(parents=True, exist_ok=True)
    started = time.time()
    code = run_game(['--capture', str(run_path)] + (['--overwrite'] if overwrite or preview else []), log)
    if code != 0 or not output.is_file():
        lines = log.read_text(errors='replace').splitlines()
        failure = [l for l in lines if 'CAPTURE FAILED' in l] or lines[-15:]
        print(f'FAILED {scene_path.name} (exit {code}), log {log}:\n  ' + '\n  '.join(failure), file=sys.stderr)
        if output.is_file() and code != 0:
            output.unlink()
        return False
    report_path = output.with_suffix('.capture.json')
    report = json.loads(report_path.read_text())
    sfx.write(output.with_suffix('.sfx.wav'), sfx.render(report))
    length = report['frames'] / report['fps']
    count = 8 if length <= 8 else 12
    times = [length * (i + 0.5) / count for i in range(count)]
    contact_sheet(output, output.with_suffix('.sheet.jpg'), times, tile_height=480 if not preview else 320,
                  columns=count if count <= 8 else 6)
    log.unlink()
    print(f'{output} ({time.time() - started:.0f}s)\n{summarize(output, report)}\n  sheet: {output.with_suffix(".sheet.jpg")}',
          flush=True)
    return True


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('scenes', nargs='*', type=Path)
    parser.add_argument('--preview', action='store_true', help='quick low-res take (<output>.preview.mp4)')
    parser.add_argument('--overwrite', action='store_true', help='replace existing takes')
    parser.add_argument('--no-build', action='store_true', help='never rebuild, even if sources changed')
    parser.add_argument('--list', type=Path, metavar='CATALOG', help='write the capture catalog JSON and exit')
    args = parser.parse_args()
    if not args.scenes and not args.list:
        parser.error('give scene files or --list')
    ensure_build(args.no_build)
    if args.list:
        log = args.list.with_suffix('.log')
        if run_game(['--capture-list', str(args.list.resolve())], log) != 0:
            sys.exit(f'listing failed, see {log}')
        log.unlink()
        catalog = json.loads(args.list.read_text())
        print(f'{args.list}: ' + ', '.join(f'{len(v)} {k}' for k, v in catalog.items()))
        return
    failed = 0
    with tempfile.TemporaryDirectory(prefix='btb-scenes-') as workdir:
        for scene in args.scenes:
            failed += not capture(scene.resolve(), args.preview, args.overwrite, Path(workdir))
    if failed:
        sys.exit(f'{failed} of {len(args.scenes)} captures failed')


if __name__ == '__main__':
    main()
