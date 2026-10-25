#!/usr/bin/env python3
"""Check the ten fresh exports and the source intervals used by their edits."""
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent

def run(args):
    return subprocess.run(args, capture_output=True, text=True, check=True).stdout

def check(edit_path):
    edit = json.loads(edit_path.read_text())
    assert edit.get('game_audio') is False, 'Current shorts must use music only'
    video = (edit_path.parent / edit['output']).resolve()
    info = json.loads(run(['ffprobe', '-v', 'error', '-show_format', '-show_streams', '-of', 'json', str(video)]))
    v = next(s for s in info['streams'] if s['codec_type'] == 'video')
    a = next(s for s in info['streams'] if s['codec_type'] == 'audio')
    expected = sum(s['dur'] for s in edit['shots'])
    assert (v['width'], v['height']) == (1080, 1920)
    assert v['codec_name'] == 'h264' and v['pix_fmt'] == 'yuv420p'
    assert v['avg_frame_rate'] == '60/1'
    assert a['codec_name'] == 'aac' and int(a['sample_rate']) == 48000 and a['channels'] == 2
    assert abs(float(info['format']['duration']) - expected) < .1
    subprocess.run(['ffmpeg', '-v', 'error', '-i', str(video), '-f', 'null', '-'], check=True, capture_output=True)
    loudness = subprocess.run(['ffmpeg', '-hide_banner', '-i', str(video), '-af',
        'loudnorm=I=-14:TP=-1.5:LRA=11:print_format=json', '-f', 'null', '-'],
        check=True, capture_output=True, text=True).stderr
    measured = json.loads(re.findall(r'\{[^{}]+\}', loudness, re.S)[-1])
    assert abs(float(measured['input_i']) + 14) <= 1.5, measured
    assert float(measured['input_tp']) <= -1, measured
    source_checks = []
    for shot in edit['shots']:
        take = (edit_path.parent / shot['take']).resolve()
        report = json.loads(take.with_suffix('.capture.json').read_text())
        assert report['completed'] and report['width'] * report['height'] == 2160 * 3840
        assert report['fps'] == 60 and report['scene']['time_scale'] == 1
        assert report['scene']['stats']['AutoRefuel'] is True
        samples = [s for s in report['samples'] if shot['in'] <= s['t'] <= shot['in'] + shot['dur'] * shot.get('speed', 1)]
        assert samples and all('ships_waiting_for_fuel' in s for s in samples)
        waiting = max(s['ships_waiting_for_fuel'] for s in samples)
        # An occasional sample at the transition into automatic refueling is fine;
        # repeated waiting for more than two seconds needs review.
        waiting_since = None
        maximum_seconds = 0
        for sample in samples:
            if sample['ships_waiting_for_fuel']:
                if waiting_since is None:
                    waiting_since = sample['t']
                maximum_seconds = max(maximum_seconds, sample['t'] - waiting_since)
            else:
                waiting_since = None
        # Inclusive interval endpoints can contain nine samples in exactly two
        # seconds. Use their timestamps rather than assuming a sample count.
        assert maximum_seconds <= 2.001, f'{take.name}: sustained ships waiting for fuel'
        source_checks.append({'take': str(take.relative_to(ROOT)), 'max_ships_waiting_for_fuel': waiting,
            'max_waiting_seconds': round(maximum_seconds, 4),
            'max_ships_refueling': max(s['ships_refueling'] for s in samples),
            'max_fleet_ships': max(s['fleet_ships'] for s in samples)})
    return {'file': str(video.relative_to(ROOT)), 'duration': float(info['format']['duration']),
        'size': [v['width'], v['height']], 'fps': v['avg_frame_rate'], 'audio': a['codec_name'],
        'integrated_lufs': float(measured['input_i']), 'true_peak_dbfs': float(measured['input_tp']),
        'full_decode': 'passed', 'sources': source_checks}

if __name__ == '__main__':
    edits = sorted(ROOT.glob('[0-9][0-9]-*/edit.json'))
    assert len(edits) == 16
    results = []
    for edit in edits:
        results.append(check(edit))
        (edit.parent / 'quality.json').write_text(json.dumps(results[-1], indent=2) + '\n')
        print(f"PASS {results[-1]['file']}", flush=True)
    (ROOT / 'export_qc.json').write_text(json.dumps(results, indent=2) + '\n')
