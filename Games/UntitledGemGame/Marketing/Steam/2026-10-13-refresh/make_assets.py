#!/usr/bin/env python3
"""Reuse current 4K takes for Steam loops/screenshots and itch.io image variants."""
import argparse
import io
import json
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
ITCH = ROOT.parents[1] / 'Itch/2026-10-13-refresh'

def run(args):
    return subprocess.run(args, capture_output=True, check=True)

def probe(path):
    return json.loads(run(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(path)]).stdout)

def landscape(path, width=None, focus_y=.5):
    v=next(s for s in probe(path)['streams'] if s['codec_type']=='video')
    filters=[]
    if v['width'] < v['height']:
        h=int(v['width']*9/16)//2*2
        y=max(0,min(v['height']-h,round(focus_y*v['height']-h/2)))
        filters.append(f"crop={v['width']}:{h}:0:{y}")
    if width:
        filters.append(f'scale={width}:{round(width*9/16)//2*2}:flags=lanczos')
    return ','.join(filters) or 'null'

def clip(c):
    source=(ROOT/c['take']).resolve();out=ROOT/'clips'/f"{c['name']}.mp4"
    if not out.exists():
        loop=.35;d=c['dur'];crop=landscape(source,1170,c.get('focus_y',.5))
        graph=(f'[0:v]{crop},fps=30,setsar=1,format=yuv420p,split=3[m][t][h];'
          f'[m]trim=start={loop}:end={d},setpts=PTS-STARTPTS[main];'
          f'[t]trim=start={d}:end={d+loop},setpts=PTS-STARTPTS[tail];'
          f'[h]trim=end={loop},setpts=PTS-STARTPTS[head];'
          f'[tail][head]xfade=transition=fade:duration={loop}:offset=0[seam];'
          '[main][seam]concat=n=2:v=1:a=0,setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709[out]')
        run(['ffmpeg','-v','error','-n','-threads','1','-ss',str(c['in']),'-t',str(d+loop),'-i',str(source),
          '-filter_complex_threads','2','-filter_complex',graph,'-map','[out]','-an','-c:v','libx264',
          '-preset','slow','-crf','23','-maxrate','3500k','-bufsize','3500k','-pix_fmt','yuv420p',
          '-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-movflags','+faststart',str(out)])
    gif=ITCH/'gifs'/f"{c['name']}.gif"
    if not gif.exists():
        # GIFs have a tighter image-upload budget; preserve the HD MP4 separately.
        for w,fps,colors in [(640,12,128),(640,10,96),(480,10,96),(480,8,64)]:
            palette=f'fps={fps},scale={w}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors={colors}:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle'
            run(['ffmpeg','-v','error','-y','-i',str(out),'-t',str(c.get('gif_duration',min(3.5,c['dur']))),
                 '-filter_complex_threads','1','-filter_complex',palette,'-loop','0',str(gif)])
            if gif.stat().st_size < 2_900_000: break
        assert gif.stat().st_size < 2_900_000, gif
    return out,gif

def screenshot(s):
    source=(ROOT/s['take']).resolve();out=ROOT/'screenshots'/f"{s['name']}.png"
    if not out.exists():
        run(['ffmpeg','-v','error','-n','-ss',str(s['at']),'-i',str(source),'-frames:v','1',
             '-vf',landscape(source,1920,s.get('focus_y',.5)) if probe(source)['streams'][0]['width']<probe(source)['streams'][0]['height'] else 'null',str(out)])
    upload=ROOT/'screenshots-upload'/f"{s['name']}.jpg"
    im=Image.open(out).convert('RGB')
    for quality in [95,93,90,87,85]:
        im.save(upload,quality=quality,subsampling=0,optimize=True)
        if upload.stat().st_size < 4_900_000: break
    assert upload.stat().st_size < 4_900_000
    dest=ITCH/'screenshots'/f"{s['name']}.jpg"
    im.thumbnail((1920,1080),Image.Resampling.LANCZOS)
    im.save(dest,quality=95,subsampling=0,optimize=True)
    assert dest.stat().st_size < 2_900_000
    return out,upload,dest

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--skip-extraction',action='store_true');args=parser.parse_args()
    for folder in [ROOT/'clips',ROOT/'screenshots',ROOT/'screenshots-upload',ITCH/'gifs',ITCH/'screenshots']:
        folder.mkdir(parents=True,exist_ok=True)
    spec=json.loads((ROOT/'assets.json').read_text());results=[]
    for c in spec['clips']:
        if args.skip_extraction and 'extract_v3' in c['take']: continue
        for path in clip(c):
            results.append({'file':str(path.relative_to(ROOT.parents[1])),'bytes':path.stat().st_size})
        print('CLIP',c['name'],flush=True)
    for s in spec['screenshots']:
        if args.skip_extraction and 'extract_v3' in s['take']: continue
        for path in screenshot(s):
            results.append({'file':str(path.relative_to(ROOT.parents[1])),'bytes':path.stat().st_size})
        print('STILL',s['name'],flush=True)
    (ROOT/'manifest.json').write_text(json.dumps(results,indent=2)+'\n')

if __name__=='__main__':main()
