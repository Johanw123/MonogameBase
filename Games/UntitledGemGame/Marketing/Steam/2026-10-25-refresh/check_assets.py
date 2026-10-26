#!/usr/bin/env python3
"""Validate both upload packages and build a local visual review page."""
import html
import json
import subprocess
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent
ITCH = ROOT.parents[1] / 'Itch/2026-10-25-refresh'


def probe(path):
    return json.loads(
        subprocess.run(
            ['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)],
            capture_output=True,
            check=True,
        ).stdout
    )


def main():
    assets = json.loads((ROOT / 'assets.json').read_text())
    results = []
    total = 0
    cards = []
    for c in assets['clips']:
        p = ROOT / 'clips' / f"{c['name']}.mp4"
        info = probe(p)
        v = info['streams'][0]
        assert len(info['streams']) == 1 and v['codec_type'] == 'video'
        assert (v['width'], v['height'], v['avg_frame_rate']) == (1170, 658, '30/1')
        assert v['codec_name'] == 'h264' and v['pix_fmt'] == 'yuv420p'
        assert all(v[k] == 'bt709' for k in ['color_space', 'color_transfer', 'color_primaries'])
        duration = float(info['format']['duration'])
        assert duration <= 12 and abs(duration - c['dur']) < 0.1
        assert p.stat().st_size < 5_000_000
        total += p.stat().st_size
        subprocess.run(['ffmpeg', '-v', 'error', '-i', str(p), '-f', 'null', '-'], check=True, capture_output=True)
        gif = ITCH / 'gifs' / f"{c['name']}.gif"
        im = Image.open(gif)
        assert im.format == 'GIF' and im.n_frames > 1 and im.info.get('loop') == 0
        assert gif.stat().st_size < 2_900_000
        # Native source reports prove this is current 4K gameplay at normal speed.
        take = (ROOT / c['take']).resolve()
        report = json.loads(take.with_suffix('.capture.json').read_text())
        assert report['completed'] and report['width'] * report['height'] == 2160 * 3840
        assert report['fps'] == 60 and report['scene']['time_scale'] == 1 and report['scene']['stats']['AutoRefuel'] is True
        results.append({
            'clip': c['name'],
            'size': [1170, 658],
            'fps': 30,
            'duration': duration,
            'bytes': p.stat().st_size,
            'audio': 'none',
            'color': 'bt709',
            'full_decode': 'passed',
            'gif_size': list(im.size),
            'gif_bytes': gif.stat().st_size,
        })
        label = html.escape(c['name'].replace('_', ' '))
        cards.append(
            f'<article><h3>{label}</h3>'
            f'<video controls muted loop playsinline preload="metadata" src="clips/{c["name"]}.mp4"></video>'
            f'<p><a href="clips/{c["name"]}.mp4">Steam MP4</a> · '
            f'<a href="../../Itch/2026-10-25-refresh/gifs/{c["name"]}.gif">itch.io GIF</a></p></article>'
        )
    assert total < 15_000_000
    assert len(assets['screenshots']) >= 5
    for s in assets['screenshots']:
        master = ROOT / 'screenshots' / f"{s['name']}.png"
        upload = ROOT / 'screenshots-upload' / f"{s['name']}.jpg"
        itch = ITCH / 'screenshots' / f"{s['name']}.jpg"
        im = Image.open(master)
        size = im.size
        assert im.format == 'PNG' and size[0] >= 1920 and size[1] >= 1080 and size[0] * 9 == size[1] * 16
        assert Image.open(upload).size == size and upload.stat().st_size < 4_900_000
        assert Image.open(itch).size == (1920, 1080) and itch.stat().st_size < 2_900_000
        take = (ROOT / s['take']).resolve()
        report = json.loads(take.with_suffix('.capture.json').read_text())
        assert report['completed'] and report['width'] * report['height'] == 2160 * 3840
        assert report['fps'] == 60 and report['scene']['time_scale'] == 1 and report['scene']['stats']['AutoRefuel'] is True
        results.append({
            'screenshot': s['name'],
            'master_size': list(size),
            'steam_jpeg_bytes': upload.stat().st_size,
            'itch_jpeg_bytes': itch.stat().st_size,
        })
        label = html.escape(s['name'].replace('_', ' '))
        cards.append(
            f'<article><h3>{label}</h3>'
            f'<a href="screenshots-upload/{s["name"]}.jpg"><img loading="lazy" src="screenshots-upload/{s["name"]}.jpg" alt="{label}"></a>'
            f'<p><a href="screenshots/{s["name"]}.png">PNG master</a> · '
            f'<a href="../../Itch/2026-10-25-refresh/screenshots/{s["name"]}.jpg">itch.io JPEG</a></p></article>'
        )
    report = {
        'steam_clips_bytes': total,
        'clips': len(assets['clips']),
        'screenshots': len(assets['screenshots']),
        'assets': results,
    }
    (ROOT / 'quality.json').write_text(json.dumps(report, indent=2) + '\n')
    (ROOT / 'review.html').write_text(
        '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
        '<title>Beyond the Belt — current store assets</title>'
        '<style>body{font:16px system-ui;background:#10161d;color:#e8f1f7;max-width:1300px;margin:30px auto;padding:20px}'
        'a{color:#6fe3e5}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:24px}'
        'article{background:#18232d;padding:16px;border-radius:12px}video,img{display:block;width:100%;height:auto}h3{font-size:18px}</style>'
        '<h1>Beyond the Belt — 25 October store assets</h1>'
        '<p>Real gameplay with staged progression. Silent loops; no promotional overlays. PNG masters and upload variants are linked below. Nothing is published.</p>'
        '<p><a href="STORE_PAGE.md">Steam copy and requirements</a> · <a href="../../Itch/2026-10-25-refresh/ITCH_PAGE.md">itch.io formats and placement</a></p>'
        '<div class="grid">' + ''.join(cards) + '</div></html>'
    )
    print(f'PASS: {len(assets["clips"])} Steam loops ({total / 1e6:.2f} MB), {len(assets["screenshots"])} screenshots, matching itch.io GIF/JPEG variants')


if __name__ == '__main__':
    main()
