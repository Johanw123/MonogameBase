#!/usr/bin/env python3
"""Replace the eight unchanged cuts' audio with music; preserve encoded video."""
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SCRIPT = ROOT.parent / 'Skills/beyond-the-belt-shorts/scripts/render_short.py'
sys.path.insert(0, str(SCRIPT.parent))
spec = importlib.util.spec_from_file_location('short_renderer', SCRIPT)
renderer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(renderer)

def video_hash(path):
    return subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-map', '0:v:0',
        '-c', 'copy', '-f', 'hash', '-hash', 'sha256', '-'], capture_output=True,
        text=True, check=True).stdout.strip()

def main():
    results = []
    for change in json.loads((ROOT / 'music_remix.json').read_text()):
        edit = renderer.Edit(ROOT / change['edit'])
        assert edit.spec['game_audio'] is False
        prior = edit.root / change['prior']
        if not edit.output.exists():
            with tempfile.TemporaryDirectory(prefix='btb-music-') as folder:
                music = renderer.render_audio(edit, Path(folder))
                subprocess.run(['ffmpeg', '-v', 'error', '-n', '-i', str(prior),
                    '-i', str(music), '-map', '0:v:0', '-map', '1:a:0', '-c:v', 'copy',
                    '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-ac', '2',
                    '-t', str(edit.total), '-movflags', '+faststart', str(edit.output)], check=True)
                shutil.copyfile(prior.with_suffix('.sheet.jpg'), edit.output.with_suffix('.sheet.jpg'))
        assert video_hash(prior) == video_hash(edit.output), 'Encoded video changed'
        results.append({'file': str(edit.output.relative_to(ROOT)), 'video': 'identical encoded packets',
                        'audio': 'music only', 'prior': str(prior.relative_to(ROOT))})
        print('PASS music only:', edit.output.name, flush=True)
    (ROOT / 'music_remix_qc.json').write_text(json.dumps(results, indent=2) + '\n')

if __name__ == '__main__': main()
