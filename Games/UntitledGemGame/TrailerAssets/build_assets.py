from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,subprocess,numpy as np,wave
ROOT=Path(__file__).resolve().parent
FPS=60; BPM=137.35; BEAT=60/BPM
# clip, source seconds, beat count, editorial purpose
spec=[(16,13,8,'Cold open: late-game spectacle'),(1,3,10,'Begin with a click'),(2,1,10,'Expand the upgrade tree'),(3,5,6,'Show the upgraded click'),(4,.8,6,'Gravity well in action'),(5,1,6,'Unlock and upgrade ships'),(6,6,12,'Growing automated fleet'),(7,7.5,6,'Unlock abilities'),(8,.8,10,'Abilities in action'),(9,17,12,'Command the fleet'),(10,11,10,'Reveal a legendary module'),(11,2,10,'Equip modules'),(11,17,6,'Module payoff in gameplay'),(12,1.7,12,'Scan and choose discoveries'),(13,3,4,'Inspect persistent signal bonuses'),(14,.8,8,'Prestige and rebuild'),(14,5.5,4,'Permanent prestige upgrades'),(15,4,10,'Late-game escalation'),(16,10,6,'Larger fleet and chain effects'),(15,22,4,'Gem showers'),(17,4,6,'Endgame payoff'),(16,15,14,'Logo and Steam wishlist')]
shots=[];beats=0
for clip,start,n,label in spec:
 rec=round(beats*BEAT*FPS); end=round((beats+n)*BEAT*FPS);shots.append(dict(clip=clip,source_in=round(start*FPS),source_out=round(start*FPS)+end-rec,record_in=rec,record_out=end,label=label));beats+=n
DURATION=shots[-1]['record_out']/FPS
bold='/usr/share/fonts/noto/NotoSans-Bold.ttf';regular='/usr/share/fonts/noto/NotoSans-Regular.ttf';mono='/usr/share/fonts/TTF/JetBrainsMono-Medium.ttf'
W,H=3840,2160
cyan=(127,229,237,255);white=(238,249,252,255)
def font(size,b=False):return ImageFont.truetype(bold if b else regular,size)
def overlay(name,headline,sub='',center=False,end=False):
 im=Image.new('RGBA',(W,H));d=ImageDraw.Draw(im)
 if center:
  # restrained full-screen tint keeps the spectacle visible behind the hook/endcard
  d.rectangle((0,0,W,H),fill=(3,9,18,150 if end else 85))
  if not end:
   d.text((W//2,870),headline,font=font(126,True),fill=white,anchor='mm',stroke_width=2,stroke_fill=(4,11,20,255))
   d.rectangle((W//2-160,1000,W//2+160,1006),fill=cyan)
   d.text((W//2,1100),sub,font=font(52),fill=cyan,anchor='mm')
  else:
   d.text((W//2,1370),'BUILD YOUR FLEET. GO BEYOND.',font=font(56,True),fill=white,anchor='mm')
   d.rounded_rectangle((1190,1510,2650,1680),radius=18,fill=(9,35,44,245),outline=cyan,width=3)
   d.text((W//2,1590),'WISHLIST ON STEAM',font=font(66,True),fill=white,anchor='mm')
   d.text((W//2,1850),'TEAM JAPE',font=font(34,True),fill=cyan,anchor='mm')
 else:
  tw=d.textbbox((0,0),headline,font=font(86,True))[2];sw=d.textbbox((0,0),sub,font=font(42))[2];pw=max(tw,sw)+132
  # lower third, clear of the bottom HUD and the centre of gameplay
  d.rounded_rectangle((180,1600,180+pw,1880),radius=16,fill=(3,12,22,225))
  d.rectangle((180,1600,190,1880),fill=cyan)
  d.text((238,1634),headline,font=font(86,True),fill=white)
  d.text((242,1760),sub,font=font(42),fill=cyan)
 p=ROOT/'graphics'/f'{name}.png';im.save(p);return str(p)
# First/last shot index inclusive; offsets keep clean picture between captions.
caps=[('hook',0,0,'HOW FAR CAN ONE CLICK GO?','BEYOND THE BELT',True),('click',1,1,'START WITH A CLICK','Collect gems. Fuel your first upgrades.',False),('upgrades',2,4,'MAKE EVERY CLICK COUNT','Unlock upgrades. Pull in bigger hauls.',False),('fleet',5,6,'BUILD YOUR FLEET','Unlock ships and let your collectors get to work.',False),('abilities',7,8,'UNLEASH YOUR ABILITIES','Turn a trickle of gems into a flood.',False),('commands',9,9,'TAKE COMMAND','Direct your fleet. Bring the haul home.',False),('modules',10,12,'FIND YOUR NEXT BIG UPGRADE','Discover rare modules. Customize your ships.',False),('signals',13,14,'DECODE DEEP-SPACE SIGNALS','Choose discoveries that boost your build.',False),('prestige',15,16,'PRESTIGE. GROW STRONGER.','Reset for permanent upgrades. Push further.',False),('escalation',17,18,'FROM ONE CLICK TO ALL THIS','Build a gem-collecting powerhouse.',False),('endcard',21,21,'','',True)]
captions=[]
for name,a,b,h,s,center in caps:
 start=shots[a]['record_in']+(12 if name!='endcard' else 0);end=shots[b]['record_out']-(18 if name!='endcard' else 0)
 # concise copy; long feature groups receive a clean tail without the card
 if name not in ['hook','endcard']:end=min(end,start+round(5.1*FPS))
 path=overlay(name,h,s,center,name=='endcard');captions.append(dict(name=name,record_in=start,record_out=end,path=path,headline=h,sub=s))
# Music is game-owned existing soundtrack, converted to PCM for Resolve on Linux.
music_src=ROOT.parent/'Content/Music/Holizna/Sky Fish.ogg';music_path=ROOT/'audio/Sky Fish - trailer edit.wav'
subprocess.run(['ffmpeg','-v','error','-ss','15.4','-i',str(music_src),'-t',str(DURATION),'-af',f'volume=0.70,afade=t=in:st=0:d=0.25,afade=t=out:st={DURATION-3}:d=3','-ar','48000','-ac','2','-c:a','pcm_s24le','-y',str(music_path)],check=True)
# A separate, removable track of the game's own UI/impact sounds punctuates beats.
sr=48000;mix=np.zeros((round(DURATION*sr),2),np.float32)
sfx=[]
def add(file,t,gain,label):
 data=np.frombuffer(subprocess.check_output(['ffmpeg','-v','error','-i',str(ROOT.parent/file),'-ar',str(sr),'-ac','2','-f','f32le','pipe:1']),np.float32).reshape(-1,2);pos=round(t*sr);n=min(len(data),len(mix)-pos)
 if n>0:mix[pos:pos+n]+=data[:n]*gain
 sfx.append(dict(file=file,time=t,gain=gain,label=label))
add('Content/SFX/Impact_1_Low.wav',0,.32,'Opening impact')
for i in [2,5,7,10,13,15]:add('Content/SFX/Menu/swoosh_2.wav',shots[i]['record_in']/FPS,.22,'Feature transition')
add('Content/SFX/Menu/upgrade_done_2.wav',shots[2]['record_in']/FPS+1.0,.3,'Upgrade accent')
add('Content/SFX/Menu/upgrade_done_3.wav',shots[10]['record_in']/FPS+1.1,.3,'Module reveal accent')
add('Content/SFX/Impact_1_Low.wav',shots[17]['record_in']/FPS,.35,'Escalation impact')
add('Content/SFX/Menu/swoosh_4.wav',shots[21]['record_in']/FPS,.25,'Logo reveal')
add('Content/SFX/Impact_1_Low.wav',shots[21]['record_in']/FPS+.12,.4,'Logo impact')
mix=np.clip(mix,-.95,.95);sfx_path=ROOT/'audio/Game SFX - trailer accents.wav'
with wave.open(str(sfx_path),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(sr);w.writeframes((mix*32767).astype('<i2').tobytes())
plan=dict(name='Beyond the Belt - Gameplay Trailer v01',fps=FPS,bpm=BPM,duration=DURATION,music_source=str(music_src),music_source_in=15.4,music_path=str(music_path),sfx_path=str(sfx_path),shots=shots,captions=captions,sfx=sfx,logo_path=str(ROOT.parent/'Content/Textures/logo_4k.png'))
(ROOT/'edit_plan.json').write_text(json.dumps(plan,indent=2)+'\n')
print(json.dumps(dict(duration=DURATION,frames=shots[-1]['record_out'],shots=len(shots),captions=len(captions))))

# Explicit video durations avoid Resolve's five-second still-image placement rule.
# ProRes 4444 preserves title transparency and is supported by Resolve on Linux.
import concurrent.futures
jobs=[(c['path'],c['record_out']-c['record_in']) for c in captions]
jobs.append((plan['logo_path'],shots[-1]['record_out']-shots[-1]['record_in']))
def encode_graphic(row):
    source,frame_count=row
    dest=ROOT/'graphics'/(Path(source).stem+'.mov')
    subprocess.run(['ffmpeg','-v','error','-loop','1','-framerate',str(FPS),
        '-i',source,'-frames:v',str(frame_count),'-vf','scale=1920:1080',
        '-c:v','prores_ks','-profile:v','4','-pix_fmt','yuva444p10le',
        '-threads','2','-y',str(dest)],check=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
    list(executor.map(encode_graphic,jobs))
