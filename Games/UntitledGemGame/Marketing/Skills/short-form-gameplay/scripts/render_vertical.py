#!/usr/bin/env python3
"""Render one gameplay edit as vertical and square videos, with optional unboxed text."""
import argparse
import json
import math
import shutil
import subprocess
import tempfile
from pathlib import Path


def run(args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


def probe(path):
    return json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]))


def number(value, name, low=0, high=float('inf')):
    value = float(value)
    if not math.isfinite(value) or not low <= value <= high:
        raise ValueError(f'{name} must be between {low} and {high}')
    return value


FORMATS = {"vertical": (1080, 1920), "square": (1080, 1080)}


def render(manifest, output, format_name="vertical"):
    out_w, out_h = FORMATS[format_name]
    aspect = out_w / out_h
    config = json.loads(manifest.read_text())
    base = manifest.parent
    resolve = lambda value: (base / value).resolve()
    if output.exists():
        raise ValueError(f'Output already exists: {output}; choose a new filename')
    if output.suffix.lower() != '.mp4':
        raise ValueError('Output must end in .mp4')
    shots = config['shots']
    if not shots:
        raise ValueError('At least one shot is required')
    # Validate all ranges before starting a potentially long render.
    prepared = []
    total = 0
    for shot in shots:
        source = resolve(shot['source'])
        info = probe(source)
        video = next(s for s in info['streams'] if s['codec_type'] == 'video')
        start = number(shot.get('start', 0), 'start')
        duration = number(shot['duration'], 'duration', 1/30)
        available = float(video.get('duration', info['format']['duration']))
        if start + duration > available + 0.02:
            raise ValueError(f'Shot exceeds source duration: {source}')
        framing = shot.get('framing', {}).get(format_name, {})
        fx = number(framing.get('focus_x', shot.get('focus_x', 0.5)), 'focus_x', 0, 1)
        fy = number(framing.get('focus_y', shot.get('focus_y', 0.5)), 'focus_y', 0, 1)
        # Normalize the source display aspect before the selected aspect-ratio crop. Never stretch to fill.
        sar = video.get('sample_aspect_ratio', '1:1')
        n, d = sar.split(':') if sar not in ('N/A', '0:1') else ('1', '1')
        width, height = int(video['width']) * float(n) / float(d), int(video['height'])
        crop_w, crop_h = min(width, height * aspect), min(height, width / aspect)
        crop_w, crop_h = int(crop_w)//2*2, int(crop_h)//2*2
        x = int(max(0, min(width-crop_w, fx*width-crop_w/2)))//2*2
        y = int(max(0, min(height-crop_h, fy*height-crop_h/2)))//2*2
        vf = (f'scale=trunc(iw*sar/2)*2:ih,setsar=1,crop={crop_w}:{crop_h}:{x}:{y},'
              f'scale={out_w}:{out_h}:flags=lanczos,setsar=1,fps=30,setpts=PTS-STARTPTS')
        prepared.append((source, start, duration, vf, any(s['codec_type']=='audio' for s in info['streams'])))
        total += duration
    captions = []
    for caption in config.get('captions', []):
        placement = caption.get('placement', {}).get(format_name, {})
        if set(placement) - {'y', 'size'}:
            raise ValueError('Caption placement overrides support only y and size')
        captions.append({**caption, **placement})
    for caption in captions:
        start = number(caption['start'], 'caption start', 0, total)
        end = number(caption['end'], 'caption end', start, total)
        if end <= start or not caption['text'].strip() or len(caption['text'].splitlines()) > 2:
            raise ValueError('Captions need text, positive duration, and at most two lines')
        number(caption.get('y', 0.18), 'caption y', 0.1, 0.75)
        number(caption.get('size', 52), 'caption size', 24, 72)
    music = config.get('music')
    if music:
        music_path = resolve(music['source'])
        music_info = probe(music_path)
        number(music.get('volume', 0.3), 'music volume', 0, 1)
        music_start = number(music.get('start', 0), 'music start')
        if music_start + total > float(music_info['format']['duration']) + 0.02:
            raise ValueError('Music is shorter than the selected range')
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='gameplay-edit-') as directory:
        temp = Path(directory)
        for index, (source, start, duration, vf, audio) in enumerate(prepared):
            args = ['ffmpeg', '-v', 'error', '-nostdin', '-noautorotate', '-ss', str(start), '-i', str(source)]
            if not audio:
                args += ['-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo']
            args += ['-map', '0:v:0', '-map', '0:a:0' if audio else '1:a:0',
                     '-t', str(duration), '-vf', vf, '-af', 'aresample=48000,asetpts=PTS-STARTPTS,apad',
                     '-c:v', 'libx264', '-preset', 'fast', '-crf', '17', '-pix_fmt', 'yuv420p',
                     '-c:a', 'pcm_s16le', '-ar', '48000', '-ac', '2', str(temp / f'shot{index}.mkv')]
            run(args)
        (temp / 'concat.txt').write_text(''.join(f"file 'shot{i}.mkv'\n" for i in range(len(prepared))))
        filters = []
        if captions:
            if 'font' in config:
                font = resolve(config['font'])
            else:
                font = Path(subprocess.check_output(['fc-match', '-f', '%{file}', 'sans:bold'], text=True))
            shutil.copyfile(font, temp / 'font.ttf')
            for index, caption in enumerate(captions):
                (temp / f'text{index}.txt').write_text(caption['text'])
                filters.append(
                    f"drawtext=fontfile=font.ttf:textfile=text{index}.txt:expansion=none:"
                    f"fontsize={caption.get('size',52)}:fontcolor=white:borderw=2:bordercolor=black@0.8:"
                    f"x=(w-tw)/2:y=h*{caption.get('y',0.18)}:"
                    f"enable='gte(t,{caption['start']})*lt(t,{caption['end']})'")
        args = ['ffmpeg', '-v', 'error', '-nostdin', '-f', 'concat', '-safe', '0', '-i', 'concat.txt']
        if music:
            args += ['-ss', str(music.get('start', 0)), '-i', str(music_path), '-filter_complex',
                     f"[1:a]volume={music.get('volume',0.3)},afade=t=in:d=0.2,"
                     f"afade=t=out:st={max(0,total-0.5)}:d=0.5[m];"
                     '[0:a][m]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[a]',
                     '-map', '0:v:0', '-map', '[a]']
        else:
            args += ['-map', '0:v:0', '-map', '0:a:0']
        args += ['-vf', ','.join(filters) if filters else 'null', '-t', str(total),
                 '-c:v', 'libx264', '-preset', 'fast', '-crf', '19', '-pix_fmt', 'yuv420p', '-r', '30',
                 '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-movflags', '+faststart', str(output)]
        run(args, cwd=temp)
    result = probe(output)
    video = next(s for s in result['streams'] if s['codec_type']=='video')
    if (video['width'], video['height'], video['r_frame_rate']) != (out_w,out_h,'30/1'):
        raise ValueError('Unexpected output dimensions or frame rate')
    print(f'{output} ({total:g} seconds, {out_w}x{out_h}, 30 fps)')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--format', choices=['vertical', 'square', 'both'], default='both',
                        help='Default: both. Adds -vertical and -square to the output stem for paired exports.')
    args = parser.parse_args()
    output = args.output.resolve()
    formats = list(FORMATS) if args.format == 'both' else [args.format]
    outputs = [(name, output.with_name(f'{output.stem}-{name}{output.suffix}')
                if args.format == 'both' else output) for name in formats]
    # Refuse conflicting paired outputs before rendering either version.
    for _, path in outputs:
        if path.exists():
            parser.error(f'Output already exists: {path}; choose a new filename')
    for name, path in outputs:
        render(args.manifest.resolve(), path, name)

if __name__ == '__main__':
    main()
