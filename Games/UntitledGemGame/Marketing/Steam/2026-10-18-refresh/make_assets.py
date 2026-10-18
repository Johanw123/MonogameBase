#!/usr/bin/env python3
"""Build Steam loops/screenshots and itch.io GIF/JPEG variants from current 4K takes."""
import argparse
import json
import math
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
ITCH = ROOT.parents[1] / 'Itch/2026-10-18-refresh'


def run(args):
    return subprocess.run(args, capture_output=True, check=True)


def probe(path):
    return json.loads(run(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]).stdout)


def landscape(path, width=None, focus_x=0.5, focus_y=0.5, zoom=1.0):
    v = next(s for s in probe(path)['streams'] if s['codec_type'] == 'video')
    vw, vh = v['width'], v['height']
    filters = []
    if vw < vh:
        h = int(vw * 9 / 16) // 2 * 2
        y = max(0, min(vh - h, round(focus_y * vh - h / 2)))
        filters.append(f"crop={vw}:{h}:0:{y}")
        vw, vh = vw, h
    if zoom > 1.0:
        cw = int(vw / zoom) // 2 * 2
        ch = int(vh / zoom) // 2 * 2
        cx = max(0, min(vw - cw, round(focus_x * vw - cw / 2)))
        cy = max(0, min(vh - ch, round(focus_y * vh - ch / 2)))
        filters.append(f"crop={cw}:{ch}:{cx}:{cy}")
    if width:
        filters.append(f"scale={width}:{round(width * 9 / 16) // 2 * 2}:flags=lanczos")
    return ','.join(filters) or 'null'


def clip(c):
    source = (ROOT / c['take']).resolve()
    out = ROOT / 'clips' / f"{c['name']}.mp4"
    if not out.exists():
        loop = 0.35
        d = c['dur']
        fx, fy = c.get('focus', [0.5, c.get('focus_y', 0.5)])
        crop = landscape(source, 1170, fx, fy, c.get('zoom', 1.0))
        graph = (
            f"[0:v]{crop},fps=30,setsar=1,format=yuv420p,split=3[m][t][h];"
            f"[m]trim=start={loop}:end={d},setpts=PTS-STARTPTS[main];"
            f"[t]trim=start={d}:end={d + loop},setpts=PTS-STARTPTS[tail];"
            f"[h]trim=end={loop},setpts=PTS-STARTPTS[head];"
            f"[tail][head]xfade=transition=fade:duration={loop}:offset=0[seam];"
            "[main][seam]concat=n=2:v=1:a=0,setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709[out]"
        )
        run([
            'ffmpeg', '-v', 'error', '-n', '-threads', '2',
            '-ss', str(c['in']), '-t', str(d + loop), '-i', str(source),
            '-filter_complex_threads', '2', '-filter_complex', graph,
            '-map', '[out]', '-an', '-c:v', 'libx264',
            '-preset', 'slow', '-crf', str(c.get('crf', 23)),
            '-maxrate', '3500k', '-bufsize', '3500k', '-pix_fmt', 'yuv420p',
            '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
            '-movflags', '+faststart', str(out)
        ])
    gif = ITCH / 'gifs' / f"{c['name']}.gif"
    if not gif.exists():
        for w, fps, colors in [(640, 12, 128), (640, 10, 96), (480, 10, 96), (480, 8, 64)]:
            palette = (
                f"fps={fps},scale={w}:-1:flags=lanczos,split[a][b];"
                f"[a]palettegen=max_colors={colors}:stats_mode=diff[p];"
                f"[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
            )
            run([
                'ffmpeg', '-v', 'error', '-y', '-i', str(out),
                '-t', str(c.get('gif_duration', min(3.5, c['dur']))),
                '-filter_complex_threads', '1', '-filter_complex', palette,
                '-loop', '0', str(gif)
            ])
            if gif.stat().st_size < 2_900_000:
                break
        assert gif.stat().st_size < 2_900_000, gif
    return out, gif


def screenshot(s):
    source = (ROOT / s['take']).resolve()
    out = ROOT / 'screenshots' / f"{s['name']}.png"
    if not out.exists():
        v = probe(source)['streams'][0]
        fx, fy = s.get('focus', [0.5, s.get('focus_y', 0.5)])
        vf = landscape(source, 1920 if v['width'] < v['height'] else None, fx, fy, s.get('zoom', 1.0))
        run(['ffmpeg', '-v', 'error', '-n', '-ss', str(s['at']), '-i', str(source), '-frames:v', '1', '-vf', vf, str(out)])
    upload = ROOT / 'screenshots-upload' / f"{s['name']}.jpg"
    im = Image.open(out).convert('RGB')
    for quality in [95, 93, 90, 87, 85, 82]:
        im.save(upload, quality=quality, subsampling=0, optimize=True)
        if upload.stat().st_size < 4_900_000:
            break
    assert upload.stat().st_size < 4_900_000
    dest = ITCH / 'screenshots' / f"{s['name']}.jpg"
    thumb = im.copy()
    thumb.thumbnail((1920, 1080), Image.Resampling.LANCZOS)
    for quality in [95, 92, 88, 85]:
        thumb.save(dest, quality=quality, subsampling=0, optimize=True)
        if dest.stat().st_size < 2_900_000:
            break
    assert dest.stat().st_size < 2_900_000
    return out, upload, dest


def build_overview(screenshots):
    cols = 4
    rows = math.ceil(len(screenshots) / cols)
    tw, th, pad, label_h = 440, 248, 16, 34
    canvas = Image.new('RGB', (cols * (tw + pad) + pad, rows * (th + label_h + pad) + pad), (15, 20, 26))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype('/usr/share/fonts/TTF/DejaVuSans.ttf', 18)
    except Exception:
        font = ImageFont.load_default()
    for idx, s in enumerate(screenshots):
        r, c = divmod(idx, cols)
        x = pad + c * (tw + pad)
        y = pad + r * (th + label_h + pad)
        im = Image.open(ROOT / 'screenshots-upload' / f"{s['name']}.jpg").convert('RGB')
        im = im.resize((tw, th), Image.Resampling.LANCZOS)
        canvas.paste(im, (x, y))
        draw.text((x, y + th + 6), s['name'], fill=(224, 236, 245), font=font)
    canvas.save(ROOT / 'screenshots-overview.jpg', quality=90, optimize=True)


def main():
    parser = argparse.ArgumentParser()
    parser.parse_args()
    for folder in [ROOT / 'clips', ROOT / 'screenshots', ROOT / 'screenshots-upload', ITCH / 'gifs', ITCH / 'screenshots']:
        folder.mkdir(parents=True, exist_ok=True)
    spec = json.loads((ROOT / 'assets.json').read_text())
    results = []
    for c in spec['clips']:
        for path in clip(c):
            results.append({'file': str(path.relative_to(ROOT.parents[1])), 'bytes': path.stat().st_size})
        print('CLIP', c['name'], flush=True)
    for s in spec['screenshots']:
        for path in screenshot(s):
            results.append({'file': str(path.relative_to(ROOT.parents[1])), 'bytes': path.stat().st_size})
        print('STILL', s['name'], flush=True)
    build_overview(spec['screenshots'])
    (ROOT / 'manifest.json').write_text(json.dumps(results, indent=2) + '\n')


if __name__ == '__main__':
    main()
