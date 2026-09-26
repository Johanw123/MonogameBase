#!/usr/bin/env python3
"""Capture actual gameplay from the existing Linux desktop build, in an isolated session."""
import argparse
import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=Path, default=ROOT / 'bin/Debug/net10.0')
    parser.add_argument('--output', type=Path, default=ROOT / 'Marketing/Source')
    args = parser.parse_args()
    build, output = args.build.resolve(), args.output.resolve()
    if not (build / 'UntitledGemGame.dll').exists():
        parser.error('Build the desktop game first, or select its output with --build.')
    for command in ('dotnet', 'ffmpeg'):
        if not shutil.which(command):
            parser.error(f'{command} is required.')
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='beyond-the-belt-capture-') as directory:
        temp = Path(directory)
        harness, app, work = (temp / name for name in ('harness', 'app', 'work'))
        harness.mkdir(); work.mkdir()
        shutil.copyfile(Path(__file__).with_name('Program.cs.txt'), harness / 'Program.cs')
        (harness / 'Capture.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>'
            f'<Reference Include="{escape(str(build))}/*.dll" />'
            '</ItemGroup></Project>')
        subprocess.run(['dotnet', 'build', str(harness / 'Capture.csproj'), '-o', str(app), '--nologo'], check=True)
        for name in ('Content', 'JFContent'):
            (app / name).symlink_to(build / name, target_is_directory=True)
            (work / name).symlink_to(ROOT / name, target_is_directory=True)
        (app / 'Settings.json').write_text(json.dumps(dict(
            width=1080, height=1080, isFixedTimeStep=False, isVSync=False,
            isFullscreen=False, isBorderless=False, musicVolume=0, sfxVolume=0)))
        for stage, frames, name in [(0,150,'beginning'), (1,180,'early'), (3,540,'late')]:
            data=temp / f'data{stage}'; data.mkdir()
            env=dict(os.environ, SDL_VIDEODRIVER='offscreen', ALSOFT_DRIVERS='null',
                     LD_LIBRARY_PATH=str(build / 'runtimes/linux-x64/native'),
                     XDG_DATA_HOME=str(data), CAPTURE_STAGE=str(stage), CAPTURE_FRAMES=str(frames),
                     CAPTURE_OUTPUT=str(output / f'{name}.mp4'))
            print(f'Capturing {name} ({frames/30:g} seconds)...', flush=True)
            with (output / f'{name}.log').open('w') as log:
                subprocess.run(['dotnet', str(app / 'Capture.dll')], cwd=work, env=env,
                               stdout=log, stderr=subprocess.STDOUT, check=True)
    print(f'Footage saved to {output}')

if __name__ == '__main__':
    main()
