#!/usr/bin/env python3
"""Cut the store page's "About This Game" clips from the capture takes (clips.json).

Steam's rules (partner.steamgames.com/doc/store/page/assets, checked 2026-10-04): PNG, JPG, GIF,
WEBP, MP4 or WEBM; 1170 px wide recommended (780 px column at 150% DPI); animations at most 12 s;
tag the color space as BT.709. The description guide asks for images under 5 MB each and
about 15 MB in total. Clips autoplay muted and loop, so they carry no audio, and the last
`loop` seconds cross-fade into the first ones so the loop has no jump.

  make_clips.py              # every clip in clips.json
  make_clips.py abilities    # just these
"""
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
WIDTH, HEIGHT = 1170, 658


def make(clip: dict, defaults: dict) -> Path:
    spec = {**defaults, **clip}
    take = (HERE / spec['take']).resolve()
    out = HERE / 'clips' / f"{spec['name']}.mp4"
    start, dur, loop, fps = spec['in'], spec['dur'], spec['loop'], spec['fps']
    if dur > 12:
        sys.exit(f"{spec['name']}: {dur} s is over Steam's 12 s limit")
    zoom = spec.get('zoom', 1.0)
    fx, fy = spec.get('focus', [0.5, 0.5])
    # Static crop around the focus point (kept inside the frame), then scale to the store width.
    crop = (f"crop=w=iw/{zoom}:h=ih/{zoom}:x='min(max({fx}*iw-ow/2,0),iw-ow)':y='min(max({fy}*ih-oh/2,0),ih-oh)',"
            f"scale={WIDTH}:{HEIGHT}:flags=lanczos,fps={fps},setsar=1,format=yuv420p")
    graph = (f"[0:v]{crop},split=3[m][t][h];"
             f"[m]trim=start={loop}:end={dur},setpts=PTS-STARTPTS[main];"
             f"[t]trim=start={dur}:end={dur + loop},setpts=PTS-STARTPTS[tail];"
             f"[h]trim=start=0:end={loop},setpts=PTS-STARTPTS[head];"
             f"[tail][head]xfade=transition=fade:duration={loop}:offset=0[seam];"
             f"[main][seam]concat=n=2:v=1:a=0,setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709[out]")
    out.parent.mkdir(exist_ok=True)
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-ss', str(start), '-t', str(dur + loop), '-i', str(take),
                    '-filter_complex', graph, '-map', '[out]', '-an',
                    '-c:v', 'libx264', '-preset', 'veryslow', '-crf', str(spec['crf']), '-profile:v', 'high',
                    '-pix_fmt', 'yuv420p', '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709',
                    '-color_range', 'tv', '-movflags', '+faststart', str(out)], check=True)
    return out


def main() -> None:
    config = json.loads((HERE / 'clips.json').read_text())
    wanted = set(sys.argv[1:])
    total = 0
    for clip in config['clips']:
        if wanted and clip['name'] not in wanted:
            continue
        out = make(clip, config['defaults'])
        size = out.stat().st_size / 1e6
        total += size
        dur = float(subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(out)],
                                   capture_output=True, text=True).stdout)
        print(f"{out.name:28} {dur:5.2f} s {size:5.2f} MB{'  OVER 5 MB' if size > 5 else ''}")
    print(f"total {total:.2f} MB")


if __name__ == '__main__':
    main()
