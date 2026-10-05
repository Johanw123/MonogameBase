#!/usr/bin/env python3
"""Render the last 50 seconds and join the original v03 sections with one continuous audio mix."""
import json
import subprocess
import tempfile
from pathlib import Path
from render_trailer import ROOT, run
from render_short import Edit, check, render_audio, review_sheet
from common import probe

spec = json.loads((ROOT / 'edit.json').read_text())
master = Edit(ROOT / 'edit.json')
if master.output.exists():
    raise FileExistsError('Final output exists; choose a new output version before rendering')
opening = ROOT / 'renders/trailer_opening_video.mp4'
if not opening.is_file():
    raise FileNotFoundError('Render edit_opening.json first')
start = sum(shot['dur'] for shot in spec['shots'][:9])
body = dict(spec, output='renders/trailer_features_video.mp4', music=None, game_audio=None,
            shots=spec['shots'][9:], texts=[dict(t, at=t['at'] - start) for t in spec['texts'] if t['at'] >= start])
body['end_card'] = dict(spec['end_card'], at=spec['end_card']['at'] - start)
body_path = ROOT / 'edit_features.json'
body_path.write_text(json.dumps(body, indent=2) + '\n')
run(body_path)
features = ROOT / body['output']
with tempfile.TemporaryDirectory(prefix='.assemble-', dir=master.output.parent) as folder:
    folder = Path(folder)
    audio = render_audio(master, folder)
    playlist = folder / 'sections.txt'
    playlist.write_text(f"file '{opening.as_posix()}'\nfile '{features.as_posix()}'\n")
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', str(playlist),
                    '-i', str(audio), '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac',
                    '-b:a', '192k', '-ar', '48000', '-af', f'afade=t=out:st={master.total - 1.25}:d=1.25',
                    '-t', str(master.total), '-movflags', '+faststart',
                    str(master.output)], check=True)
problems = check(master)
video = next(s for s in probe(master.output)['streams'] if s['codec_type'] == 'video')
if int(video['nb_frames']) != round(master.total * master.fps):
    problems.append('Frame count does not match the 90-second timeline')
if problems:
    raise RuntimeError('; '.join(problems))
print(f'Complete: {master.output}\nReview: {review_sheet(master)}', flush=True)
