from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import subprocess, json, concurrent.futures, numpy as np, wave, sys

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent.parent
FPS = 60
BEAT = 60 / 137.35
FILES = {int(p.name.split('_')[0]): p for p in Path('/home/johan/Videos/2').glob('*.mkv')}
for folder in ['plates', 'titles_v02', 'audio', 'exports', 'review']:
    (ROOT / folder).mkdir(exist_ok=True)

# Source-second trims, musical beat lengths, targeted source crop, and caption.
GAME_CROP = (960, 0, 1920, 1800)
TREE = (750, 120, 2350, 1780)
specs = [
    ('01_Click_to_Fleet', 'One click becomes a fleet', 15.4, [
        (17, 2.5, 4, GAME_CROP, 'ONE CLICK.\nA WHOLE FLEET.', 'How far will you take it?'),
        (1, 3, 6, GAME_CROP, 'START WITH\nA CLICK', 'Collect gems. Start growing.'),
        (2, 1, 6, TREE, 'MAKE EVERY\nCLICK COUNT', 'Choose your next upgrade.'),
        (3, 5, 6, GAME_CROP, 'BIGGER\nGEM HAULS', 'Keep the upgrades coming.'),
        (5, 1, 6, TREE, 'UNLOCK\nYOUR FIRST SHIPS', 'Build your collecting crew.'),
        (6, 6, 12, GAME_CROP, 'LET YOUR\nFLEET WORK', 'Collectors bring the haul home.'),
        (6, 10, 8, GAME_CROP, 'BUILD YOUR FLEET.\nGO BEYOND.', 'WISHLIST ON STEAM'),
    ]),
    ('02_Abilities', 'Turn gems into a flood', 29.38, [
        (8, .8, 6, GAME_CROP, 'TURN GEMS\nINTO A FLOOD', 'Unleash your abilities.'),
        (7, 7.5, 6, (650, 150, 2500, 1740), 'UNLOCK YOUR\nNEXT ABILITY', 'Spend ability points. Power up.'),
        (4, .8, 6, GAME_CROP, 'PULL THEM\nALL IN', 'Gravity well in action.'),
        (8, 1.2, 8, GAME_CROP, 'KEEP THE\nGEMS COMING', 'Push your collecting power.'),
        (9, 1, 6, GAME_CROP, 'COMMAND\nYOUR FLEET', 'Direct your collectors.'),
        (17, 3, 4, GAME_CROP, 'GO\nBIGGER', 'Build a collecting powerhouse.'),
        (6, 9, 6, GAME_CROP, 'YOUR NEXT\nGEM OBSESSION', 'WISHLIST ON STEAM'),
    ]),
    ('03_Build_Your_Upgrade', 'Build your next upgrade', 43.36, [
        (10, 13, 6, (1300, 120, 1900, 1750), 'FOUND A\nLEGENDARY MODULE', 'Meet the Astral Relay.'),
        (11, 2, 8, (1050, 180, 2650, 1630), 'CUSTOMIZE\nYOUR SHIPS', 'Equip modules. Shape your build.'),
        (11, 10, 6, GAME_CROP, 'PUT YOUR\nBUILD TO WORK', 'Send your collectors out.'),
        (12, 1.7, 8, (250, 370, 3350, 1450), 'CHOOSE YOUR\nDISCOVERY', 'Decode deep-space signals.'),
        (13, 3, 6, (100, 250, 2180, 1690), 'STACK YOUR\nBONUSES', 'Explore your discovered signals.'),
        (14, .8, 6, (650, 120, 2500, 1780), 'PRESTIGE.\nGROW STRONGER.', 'Reset for permanent upgrades.'),
        (14, 5.5, 6, (650, 120, 2500, 1780), 'PUSH\nFURTHER', 'Choose your permanent upgrades.'),
        (6, 7, 8, GAME_CROP, 'WHAT WILL\nYOU BUILD?', 'WISHLIST ON STEAM'),
    ]),
]

bold = '/usr/share/fonts/noto/NotoSans-Bold.ttf'
regular = '/usr/share/fonts/noto/NotoSans-Regular.ttf'
def font(n, weight=True):
    return ImageFont.truetype(bold if weight else regular, n)

logo = Image.open(GAME / 'Content/Textures/logo_4k.png').convert('RGBA')
logo = logo.crop(logo.getbbox())
logo.thumbnail((420, 134), Image.Resampling.LANCZOS)

def title(path, headline, caption, closing, sequence, total):
    im = Image.new('RGBA', (1080, 1920))
    d = ImageDraw.Draw(im)
    # The gameplay stays clear; the solid bands hold phone-sized typography.
    d.rectangle((0, 0, 1080, 379), fill=(4, 15, 25, 255))
    d.rectangle((0, 1394, 1080, 1920), fill=(4, 15, 25, 255))
    im.alpha_composite(logo, (84, 72))
    d.text((84, 203), headline, font=font(62), fill=(244, 251, 255), spacing=6)
    d.rectangle((84, 355, 248, 359), fill=(115, 232, 235))
    d.text((84, 1440), 'BEYOND THE BELT', font=font(26), fill=(115, 232, 235))
    if closing:
        d.text((84, 1496), 'Ready to go beyond?', font=font(43), fill='white')
        d.rounded_rectangle((84, 1580, 916, 1700), radius=18, fill=(13, 51, 63), outline=(115, 232, 235), width=3)
        d.text((500, 1639), caption, font=font(43), fill='white', anchor='mm')
    else:
        words = caption.split(); lines = ['']
        for word in words:
            candidate = (lines[-1] + ' ' + word).strip()
            if d.textlength(candidate, font=font(44)) > 830:
                lines.append(word)
            else:
                lines[-1] = candidate
        d.text((84, 1496), '\n'.join(lines), font=font(44), fill='white', spacing=12)
        d.text((84, 1660), 'BUILD YOUR FLEET. GO BEYOND.', font=font(27), fill=(115, 232, 235))
    # Progress line is part of each editable caption asset.
    d.rectangle((84, 1770, 916, 1773), fill=(29, 52, 64))
    d.rectangle((84, 1770, 84 + round(832 * sequence / total), 1773), fill=(115, 232, 235))
    im.save(path)

plans = []
jobs = []
for slug, name, music_in, shotspec in specs:
    shots = []; beat = 0
    for i, (clip, source_in, beats, crop, headline, caption) in enumerate(shotspec):
        start = round(beat * BEAT * FPS); beat += beats; end = round(beat * BEAT * FPS)
        plate = ROOT / 'plates' / f'{slug}_{i+1:02d}_{clip}.mp4'
        png = ROOT / 'titles_v02' / f'{slug}_{i+1:02d}.png'
        mov = png.with_suffix('.mov')
        title(png, headline, caption, i == len(shotspec)-1, i+1, len(shotspec))
        row = dict(clip=clip, source=str(FILES[clip]), source_in=round(source_in*FPS), source_out=round(source_in*FPS)+end-start,
                   record_in=start, record_out=end, crop=list(crop), headline=headline, caption=caption, plate=str(plate), title=str(mov))
        shots.append(row); jobs.append((row, png))
    plans.append(dict(slug=slug, name='Beyond the Belt - Short ' + name, fps=FPS, width=1080, height=1920,
                      duration_frames=shots[-1]['record_out'], duration=shots[-1]['record_out']/FPS,
                      music_in=music_in, shots=shots, music=str(ROOT/'audio'/f'{slug}_Music.wav'), sfx=str(ROOT/'audio'/f'{slug}_SFX.wav')))

def build(row_png):
    row, png = row_png
    frames = row['record_out']-row['record_in']; x,y,w,h=row['crop']
    # Target the central action or the specific feature panel, retaining aspect ratio.
    scaled_h = round(h*1080/w/2)*2
    top = 380 + round((1014-scaled_h)/2/2)*2
    vf=f'crop={w}:{h}:{x}:{y},scale=1080:{scaled_h}:flags=lanczos,pad=1080:1920:0:{top}:color=0x040f19,setsar=1'
    if '--titles-only' not in sys.argv:
        subprocess.run(['ffmpeg','-v','error','-threads','2','-ss',str(row['source_in']/FPS),'-i',row['source'],
                        '-an','-frames:v',str(frames),'-vf',vf,'-r',str(FPS),'-c:v','libx264','-preset','veryfast','-crf','17',
                        '-pix_fmt','yuv420p','-threads','2','-y',row['plate']],check=True)
    one = png.with_name(png.stem+'_one.mov')
    subprocess.run(['ffmpeg','-v','error','-i',str(png),'-frames:v','1','-r',str(FPS),'-c:v','prores_ks',
                    '-profile:v','4','-pix_fmt','yuva444p10le','-threads','2','-y',str(one)],check=True)
    subprocess.run(['ffmpeg','-v','error','-stream_loop','-1','-i',str(one),'-frames:v',str(frames),'-c:v','copy','-y',row['title']],check=True)
    one.unlink()
    print(f"Built {Path(row['plate']).name}",flush=True)

with concurrent.futures.ThreadPoolExecutor(max_workers=2) as ex:
    list(ex.map(build, jobs))

(ROOT/'shorts_plan.json').write_text(json.dumps(plans,indent=2)+'\n')
if '--titles-only' in sys.argv:
    print('All title assets rebuilt.',flush=True)
    sys.exit(0)

for p in plans:
    duration=p['duration']
    subprocess.run(['ffmpeg','-v','error','-ss',str(p['music_in']),'-i',str(GAME/'Content/Music/Holizna/Sky Fish.ogg'),
                    '-t',str(duration),'-af',f'volume=0.70,afade=t=in:st=0:d=0.06,afade=t=out:st={duration-1.2}:d=1.2',
                    '-ar','48000','-ac','2','-c:a','pcm_s24le','-y',p['music']],check=True)
    sr=48000;mix=np.zeros((round(duration*sr),2),np.float32)
    for i, shot in enumerate(p['shots']):
        sound='Content/SFX/Impact_1_Low.wav' if i==0 else 'Content/SFX/Menu/swoosh_2.wav'
        if i==len(p['shots'])-1:sound='Content/SFX/Menu/swoosh_4.wav'
        a=np.frombuffer(subprocess.check_output(['ffmpeg','-v','error','-i',str(GAME/sound),'-ar',str(sr),'-ac','2','-f','f32le','pipe:1']),np.float32).reshape(-1,2)
        at=round(shot['record_in']/FPS*sr);n=min(len(a),len(mix)-at);mix[at:at+n]+=a[:n]*(.28 if i==0 else .20)
    with wave.open(p['sfx'],'wb') as f:
        f.setnchannels(2);f.setsampwidth(2);f.setframerate(sr);f.writeframes((np.clip(mix,-.95,.95)*32767).astype('<i2').tobytes())
(ROOT/'shorts_plan.json').write_text(json.dumps(plans,indent=2)+'\n')
print('All three Shorts assets built.',flush=True)
