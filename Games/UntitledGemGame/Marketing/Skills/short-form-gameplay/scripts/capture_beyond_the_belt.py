#!/usr/bin/env python3
"""Capture a selected Beyond the Belt preset without touching the player's save."""
import argparse
import json
import math
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from xml.sax.saxutils import quoteattr

STAGES = ['beginning', 'early', 'mid', 'late', 'endgame']
ABILITIES = {'spawner': 'GS1', 'speed': 'Speed1', 'magnet': 'HBM1',
             'drones': 'Drones1', 'chain': 'CM1'}

def positive(value):
    number = float(value)
    if not math.isfinite(number) or number <= 0:
        raise argparse.ArgumentTypeError('Must be a positive finite number')
    return number

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', type=Path, required=True)
    parser.add_argument('--build', type=Path, help='Defaults to GAME_ROOT/bin/Debug/net10.0')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--stage', choices=STAGES, default='mid')
    parser.add_argument('--seconds', type=positive, default=15)
    parser.add_argument('--warmup', type=positive, default=8)
    parser.add_argument('--zoom', type=positive, default=1, help='Camera zoom multiplier; below 1 shows more world')
    parser.add_argument('--abilities', default='all', help='all, none, or comma-separated spawner,speed,magnet,drones,chain')
    parser.add_argument('--view', choices=['world', 'hud', 'shipyard', 'signals', 'upgrades', 'abilities', 'meta'], default='world', help='Real HUD/menu capture; shipyard equips a module and signals scans then chooses')
    args = parser.parse_args()
    root, output = args.game_root.resolve(), args.output.resolve()
    build = args.build.resolve() if args.build else root / 'bin/Debug/net10.0'
    if output.exists():
        parser.error(f'Output already exists: {output}; choose a new take filename')
    if output.suffix.lower() != '.mp4':
        parser.error('Output must be an .mp4 file')
    selected = None
    if args.abilities != 'all':
        names = [] if args.abilities == 'none' else args.abilities.split(',')
        if any(name not in ABILITIES for name in names) or len(set(names)) != len(names):
            parser.error('Choose unique ability names from: ' + ','.join(ABILITIES))
        selected = ','.join(ABILITIES[name] for name in names)
    if not (build / 'UntitledGemGame.dll').is_file():
        parser.error('Build the game first, or select its existing build with --build')
    for command in ('dotnet', 'ffmpeg'):
        if not shutil.which(command):
            parser.error(f'{command} is required')
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='gameplay-capture-') as directory:
        temp = Path(directory)
        harness, app, work, data = (temp / name for name in ('harness', 'app', 'work', 'data'))
        for path in (harness, work, data):
            path.mkdir()
        shutil.copyfile(Path(__file__).resolve().parents[1] / 'assets/CaptureGame.cs.txt', harness / 'Program.cs')
        (harness / 'Capture.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>'
            f'<Reference Include={quoteattr(str(build / "*.dll"))} />'
            '</ItemGroup></Project>')
        subprocess.run(['dotnet', 'build', str(harness / 'Capture.csproj'), '-o', str(app), '--nologo'], check=True)
        for name in ('Content', 'JFContent'):
            (app / name).symlink_to(build / name, target_is_directory=True)
            (work / name).symlink_to(root / name, target_is_directory=True)
        (app / 'Settings.json').write_text(json.dumps(dict(
            width=1080, height=1080, isFixedTimeStep=False, isVSync=False,
            isFullscreen=False, isBorderless=False, musicVolume=0, sfxVolume=0)))
        env = dict(os.environ, SDL_VIDEODRIVER='offscreen', ALSOFT_DRIVERS='null',
                   LD_LIBRARY_PATH=str(build / 'runtimes/linux-x64/native'),
                   XDG_DATA_HOME=str(data), CAPTURE_STAGE=str(STAGES.index(args.stage)),
                   CAPTURE_FRAMES=str(max(1, round(args.seconds * 30))),
                   CAPTURE_WARMUP_FRAMES=str(round(args.warmup * 30)),
                   CAPTURE_ZOOM=str(args.zoom), CAPTURE_OUTPUT=str(output), CAPTURE_VIEW=args.view)
        env.pop('CAPTURE_ABILITIES', None)
        if selected is not None:
            env['CAPTURE_ABILITIES'] = selected
        with output.with_suffix('.log').open('w') as log:
            subprocess.run(['dotnet', str(app / 'Capture.dll')], cwd=work, env=env,
                           stdout=log, stderr=subprocess.STDOUT, check=True)
    output.with_suffix('.capture.json').write_text(json.dumps({
        'build': str(build), 'build_mtime': (build / 'UntitledGemGame.dll').stat().st_mtime,
        'stage': args.stage, 'seconds': args.seconds, 'warmup': args.warmup,
        'abilities': args.abilities, 'zoom': args.zoom, 'fps': 30,
        'view': args.view,
        'audio': 'none; offline renderer records no live sound',
    }, indent=2) + '\n')
    print(output)

if __name__ == '__main__':
    main()
