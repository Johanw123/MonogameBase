#!/usr/bin/env python3
"""Run a fresh-slate production stage sequentially, with bounded software-render threads.

Usage: python3 Marketing/Shorts/produce.py preview 01 04 06
       python3 Marketing/Shorts/produce.py capture 01
       python3 Marketing/Shorts/produce.py draft 01
       python3 Marketing/Shorts/produce.py render 01
Preview review remains a manual step before capture/render.
"""
import argparse
import json
import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SCRIPTS = ROOT.parent / 'Skills/beyond-the-belt-shorts/scripts'
ORDER = ['01', '04', '06', '02', '03', '05', '07', '08', '09', '10']

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=['preview', 'capture', 'draft', 'render'])
    parser.add_argument('numbers', nargs='*', choices=ORDER)
    parser.add_argument('--threads', type=int, default=4)
    args = parser.parse_args()
    if args.threads < 1: parser.error('--threads must be positive')
    # Software GL and x264 otherwise each create many worker threads and large
    # frame buffers. Bound each job and keep at most two game captures active.
    if hasattr(os, 'sched_getaffinity'):
        cpus = sorted(os.sched_getaffinity(0))
        os.sched_setaffinity(0, cpus[:args.threads])
    env = dict(os.environ, LP_NUM_THREADS=str(args.threads), OMP_NUM_THREADS=str(args.threads))
    for number in args.numbers or ORDER:
        folder = next(ROOT.glob(number + '-*'))
        if args.stage in ('preview', 'capture'):
            for scene in sorted((folder / 'scenes').glob('*.json')):
                spec = json.loads(scene.read_text())
                output = (scene.parent / spec['output']).resolve()
                if args.stage == 'preview':
                    output = output.with_name(output.stem + '.preview.mp4')
                report = output.with_suffix('.capture.json')
                if args.stage == 'capture' and output.exists() and report.exists() and json.loads(report.read_text())['completed']:
                    print('Already captured:', output.name, flush=True)
                    continue
                print(args.stage.upper(), scene.relative_to(ROOT), flush=True)
                command = ['python3', str(SCRIPTS / 'capture.py'), str(scene), '--no-build']
                if args.stage == 'preview': command.append('--preview')
                subprocess.run(command, env=env, check=True)
        else:
            edit = folder / 'edit.json'
            if args.stage == 'draft':
                spec = json.loads(edit.read_text())
                spec['output'] = spec['output'].replace('.mp4', '_review.mp4')
                spec['size'], spec['fps'] = [1080, 1920], 60
                edit = folder / 'edit_draft.json'
                edit.write_text(json.dumps(spec, indent=2) + '\n')
            print(args.stage.upper(), edit.relative_to(ROOT), flush=True)
            subprocess.run(['python3', str(SCRIPTS / 'render_short.py'), str(edit)], env=env, check=True)

if __name__ == '__main__': main()
