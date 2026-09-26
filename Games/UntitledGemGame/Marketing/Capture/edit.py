#!/usr/bin/env python3
"""Edit the captured gameplay into two 1080x1920 social videos. Requires ffmpeg."""
import argparse
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FONT = '/usr/share/fonts/TTF/DejaVuSans-Bold.ttf'
if not Path(FONT).exists():
    FONT = '/usr/share/fonts/TTF/JetBrainsMono-ExtraBold.ttf'

def run(args):
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'warning', '-y', *args], check=True)

def text(label, y, size, color='white', when=None):
    # All labels below are authored constants, without filter escaping characters.
    result = f"drawtext=fontfile='{FONT}':text='{label}':fontsize={size}:fontcolor={color}:x=(w-tw)/2:y={y}"
    if when:
        result += f":enable='gte(t,{when[0]})*lt(t,{when[1]})'"
    return result

def render(raw, output, preview=False):
    output.mkdir(parents=True, exist_ok=True)
    logo = ROOT / 'Content/Textures/logo.png'
    music = ROOT / 'Content/Music/Holizna/Greys.ogg'
    for name, length, captions in [
        ('01-one-ship-to-a-fleet', 18, [
            ('IT STARTED WITH', 'ONE HARVESTER.', 'LATE GAME', 0, 1.5),
            ('A SMALL START.', 'A BIG PLAN.', 'BEGINNING', 1.5, 5.5),
            ('MORE SHIPS.', 'MORE GEMS.', 'EARLY GAME', 5.5, 10.5),
            ('AND THEN...', 'THIS HAPPENS.', 'LATE GAME', 10.5, 18),
        ]),
        ('02-when-abilities-combine', 15, [
            ('WHEN YOUR', 'BUILD CLICKS.', 'ABILITIES IN ACTION', 0, 4),
            ('SPAWN GEMS.', 'PULL THEM IN.', 'GEM SPAWNER + MAGNET', 4, 9),
            ('LET THE FLEET', 'DO ITS THING.', 'AUTOMATED COLLECTION', 9, 15),
        ]),
    ]:
        args = []
        if name.startswith('01'):
            for file in ['beginning.mp4', 'early.mp4', 'late.mp4']:
                args += ['-i', str(raw / file)]
            filters = [
                '[2:v]split=2[lateA][lateB]',
                '[lateA]trim=0:1.5,setpts=PTS-STARTPTS,crop=1080:1080:420:0[a]',
                '[0:v]trim=0:4,setpts=PTS-STARTPTS,crop=1080:1080:200:0[b]',
                '[1:v]trim=0:5,setpts=PTS-STARTPTS,crop=1080:1080:420:0[c]',
                '[lateB]trim=1.5:9,setpts=PTS-STARTPTS,crop=1080:1080:420:0[d]',
                '[a][b][c][d]concat=n=4:v=1:a=0[game]',
            ]
            logo_index, music_index = 3, 4
        else:
            args += ['-i', str(raw / 'late.mp4')]
            filters = ['[0:v]trim=3:18,setpts=PTS-STARTPTS,crop=1080:1080:420:0[game]']
            logo_index, music_index = 1, 2
        args += ['-loop', '1', '-i', str(logo), '-ss', '35', '-i', str(music)]
        filters += [
            '[game]split=2[bg][fg]',
            '[bg]scale=1920:1920,crop=1080:1920,boxblur=30:2,eq=brightness=-0.15:saturation=0.55,drawbox=x=0:y=0:w=iw:h=ih:color=0x070A16@0.65:t=fill[back]',
            '[fg]scale=1000:1000[front]',
            '[back][front]overlay=40:440,drawbox=x=39:y=439:w=1002:h=1002:color=0x61DCD7@0.55:t=2[framed]',
            f'[{logo_index}:v]scale=760:-1[logo]',
            '[framed][logo]overlay=160:1510[branded]',
        ]
        titles = [text('AN INCREMENTAL SPACE GAME', 177, 28, '0x77DDD9')]
        for first, second, label, start, end in captions:
            titles += [text(first, 250, 64, when=(start,end)), text(second, 328, 64, when=(start,end)),
                       text(label, 1462, 23, '0xBDD0DB', (start,end))]
        titles += [text('Follow the development', 1603, 32, '0xDAE4EE'),
                   text('Development footage', 1682, 21, '0x879AAE')]
        filters += ['[branded]' + ','.join(titles) + '[video]',
                    f'[{music_index}:a]atrim=duration={length},asetpts=PTS-STARTPTS,volume=0.45,afade=t=in:d=0.25,afade=t=out:st={length-0.7}:d=0.7[audio]']
        args += ['-filter_complex', ';'.join(filters), '-map', '[video]', '-map', '[audio]',
                 '-t', str(0.1 if preview else length), '-r', '30', '-c:v', 'libx264', '-preset', 'fast',
                 '-crf', '19', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k', '-ar', '48000',
                 '-movflags', '+faststart', str(output / (name + '.mp4'))]
        run(args)
        run(['-ss', '0', '-i', str(output / (name + '.mp4')), '-frames:v', '1',
             '-update', '1', str(output / (name + '-cover.jpg'))])

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('raw', type=Path)
    parser.add_argument('--output', type=Path, default=ROOT / 'Marketing/Exports')
    parser.add_argument('--preview', action='store_true')
    args=parser.parse_args()
    render(args.raw, args.output, args.preview)
