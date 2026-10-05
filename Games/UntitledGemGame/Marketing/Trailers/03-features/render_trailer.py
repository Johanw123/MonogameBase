#!/usr/bin/env python3
"""Render this trailer one shot at a time to bound decoder/filter memory.

Uses the shorts skill's framing, overlays, game-audio mix and output checks.
Run from any directory: python3 render_trailer.py [--preview]
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parents[1] / 'Skills/beyond-the-belt-shorts/scripts'))
from render_short import Edit, build_overlays, check, frame_filter, render_audio, review_sheet


def run(edit_path):
    edit = Edit(edit_path)
    if edit.output.exists():
        raise FileExistsError(f'{edit.output} exists; bump the output version')
    edit.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.render-', dir=edit.output.parent) as work:
        work = Path(work)
        overlays = build_overlays(edit, work)
        parts = []
        for i, shot in enumerate(edit.shots):
            print(f'Shot {i + 1}/{len(edit.shots)}: {shot["at"]:.2f}s {shot["take"].name}', flush=True)
            inputs = ['-threads', '2', '-ss', str(shot['in']), '-t', str(shot['span']), '-i', str(shot['take'])]
            graph = [f'[0:v]{frame_filter(shot, edit.w, edit.h, edit.fps)},trim=duration={shot["dur"]:.6f},setpts=PTS-STARTPTS,format=yuv420p[base0]']
            last = 'base0'
            count = 0
            for overlay in overlays:
                begin = max(shot['at'], overlay['at'])
                end = min(shot['at'] + shot['dur'], overlay['at'] + overlay['dur'])
                if end - begin <= 1e-6:
                    continue
                count += 1
                if overlay.get('video'):
                    inputs += ['-ss', str(begin - overlay['at']), '-t', str(end - begin), '-i', str(overlay['file'])]
                else:
                    inputs += ['-loop', '1', '-framerate', str(edit.fps), '-t', str(end - begin), '-i', str(overlay['file'])]
                offset = begin - shot['at']
                fade = min(overlay.get('fade', 0), (end - begin) / 2)
                fades = f',fade=t=in:st=0:d={fade}:alpha=1,fade=t=out:st={end-begin-fade}:d={fade}:alpha=1' if fade else ''
                graph += [f'[{count}:v]format=rgba,fps={edit.fps}{fades},setpts=PTS-STARTPTS+{offset:.6f}/TB[o{count}]', f'[{last}][o{count}]overlay=0:0:eof_action=pass:format=auto[base{count}]']
                last = f'base{count}'
            part = work / f'shot_{i:02d}.mp4'
            subprocess.run(['ffmpeg', '-v', 'error', '-y', *inputs, '-filter_complex_threads', '2', '-filter_complex', ';'.join(graph), '-map', f'[{last}]', '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '17', '-threads', '4', '-pix_fmt', 'yuv420p', '-r', str(edit.fps), '-frames:v', str(round(shot['dur'] * edit.fps)), str(part)], check=True)
            parts.append(part)
        audio = render_audio(edit, work)
        playlist = work / 'concat.txt'
        playlist.write_text(''.join(f"file '{p.as_posix()}'\n" for p in parts))
        inputs = ['-f', 'concat', '-safe', '0', '-i', str(playlist)]
        if audio:
            inputs += ['-i', str(audio)]
        command = ['ffmpeg', '-v', 'error', '-y', *inputs, '-map', '0:v', '-c:v', 'copy']
        if audio:
            command += ['-map', '1:a', '-c:a', 'aac', '-b:a', '192k', '-ar', '48000']
        subprocess.run(command + ['-t', str(edit.total), '-movflags', '+faststart', str(edit.output)], check=True)
    problems = check(edit)
    if problems:
        raise RuntimeError('; '.join(problems))
    sheet = review_sheet(edit)
    print(f'Complete: {edit.output}, {edit.total:.2f}s, {edit.w}x{edit.h}, {edit.fps} fps\nReview: {sheet}', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--preview', action='store_true')
    args = parser.parse_args()
    path = ROOT / 'edit.json'
    if args.preview:
        spec = json.loads(path.read_text())
        output = Path(spec['output'])
        spec.update(size=[960, 540], fps=30, output=str(output.with_name(output.stem + '_preview.mp4')))
        for shot in spec['shots']:
            shot['take'] = shot['take'].replace('.mp4', '.preview.mp4')
            if not (ROOT / shot['take']).is_file():
                take = ROOT / shot['take']
                stem = re.sub(r'_v\d+$', '', take.stem.removesuffix('.preview'))
                previews = list(take.parent.glob(stem + '*.preview.mp4'))
                if previews:
                    shot['take'] = str(max(previews, key=lambda p: p.stat().st_mtime).relative_to(ROOT))
        path = ROOT / 'edit_preview.json'
        path.write_text(json.dumps(spec, indent=2) + '\n')
    run(path)
