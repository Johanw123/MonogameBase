"""Reproduce the 100 BPM music assembly and 60-second trailer edit."""
from pathlib import Path
import json,subprocess
WORK=Path(__file__).resolve().parent
ROOT=WORK.parents[2]
MUSIC=ROOT/'Content/Music/Synthwave/04_OldStarship_100bpm/OldStarship_FULL_100bpm.wav'
# Source music beats are at 0.6-second intervals. Reorder existing performance only.
sections=[(78.6,5.4),(13.2,7.8),(21,27),(13.8,7.2),(78.6,7.8),(115.8,4.8)]
filters=[]
for i,(start,dur) in enumerate(sections):
 filters.append(f'[0:a]atrim=start={start}:duration={dur},asetpts=PTS-STARTPTS,afade=t=in:d=0.008,afade=t=out:st={dur-.008}:d=0.008[a{i}]')
filters.append(''.join(f'[a{i}]' for i in range(len(sections)))+f'concat=n={len(sections)}:v=0:a=1[out]')
path=WORK/'takes/old_starship_trailer_v1.wav'
if not path.exists():
 subprocess.run(['ffmpeg','-v','error','-i',str(MUSIC),'-filter_complex',';'.join(filters),'-map','[out]','-ar','48000','-c:a','pcm_s24le',str(path)],check=True)
shots=[]
def shot(name,start,duration,**kwargs):
 shots.append(dict(take=f'takes/{name}_v1.mp4',**{'in':start},dur=duration,**kwargs))
shot('t_fleet',1,1.8,zoom=1.05)
shot('t_cascade',.6,.6,zoom=1.15)
shot('t_fracture',2.1,3,zoom=1.05)
shot('shell_break',.3,2.4,zoom=1.08)
shot('planet_clicks',1.3,3.6,zoom=1.1)
shot('upgrade_ui',.1,1.8,zoom=2.8,focus=[.51,.55])
for name,start,duration in [('arsenal',.2,1.2),('arsenal',1.4,1.8),('arsenal',3.2,1.2),('arsenal',4.4,1.8),('arsenal',6.2,1.2)]:shot(name,start,duration,zoom=1.15)
shot('prospectors',1,2.4,zoom=1.12)
shot('trove_hunters',1,2.4,zoom=1.12)
shot('s_genesis',.5,2.4,zoom=1.05)
shot('s_cascade',.5,2.4,zoom=1.12)
shot('world_eater',.1,1.8,zoom=1.5,focus=[.5,.43])
shot('world_eater',3.2,2.4,zoom=1.5,focus=[.58,.46])
shot('signals_ui',4.3,1.8,zoom=1.5,focus=[.5,.43])
shot('shard',1.5,4.2,zoom=1.15)
shot('loop',3.1,3.6,zoom=1.02,focus=[.5,.49])
shot('loop',7.9,1.8,zoom=1.12,focus=[.5,.43])
shot('loop',13.2,1.8,zoom=1.8,focus=[.5,.48])
shot('climax',.4,2.4,zoom=1.35,zoom_end=1.2)
shot('golden_spirals',.3,2.4,zoom=3.2,zoom_end=2.5,focus=[.69,.5])
shot('golden_spirals',3.6,3,zoom=2.5,zoom_end=1.8,focus=[.69,.5])
shot('climax',3,4.8)
texts=[]
def title(at,dur,text,sub=None):
 t=dict(at=at,dur=dur,text=text,pos=.07,size=40,fade=.08)
 if sub:t['sub']=sub
 texts.append(t)
title(5.5,2.2,'START WITH ONE SHIP')
title(7.9,3.3,'MINE EVERY GEM')
title(13.3,6.9,'ARM YOUR ORBITAL ARSENAL')
title(20.5,4.5,'BUILD AN AUTOMATED FLEET')
title(25.3,4.5,'TRIGGER SHIP SYSTEMS')
title(30.1,4,'FIT WORLD EATER')
title(34.3,1.6,'CHOOSE A SIGNAL')
title(36.1,4,'SHATTER THE CORE')
title(40.3,3.4,'COLLAPSE THE PLANET')
title(44,3.2,'LOOP BACK STRONGER')
edit=dict(output='renders/beyond_the_belt_trailer_v1.mp4',layout='fullscreen',size=[1920,1080],fps=60,
 music=dict(file='takes/old_starship_trailer_v1.wav',gain_db=-3,fade_out=.65),game_audio=dict(gain_db=2),shots=shots,texts=texts,
 end_card=dict(style='slam',at=55.2,dur=4.8,title='WISHLIST NOW',sub='Beyond the Belt on Steam',size=64,dim=.55))
assert abs(sum(s['dur'] for s in shots)-60)<1e-6
for s in edit['shots']:
 name=Path(s['take']).stem.rsplit('_v',1)[0]
 scene=json.loads((WORK/'scenes'/f'{name}.json').read_text())
 s['take']='takes/'+Path(scene['output']).name
(WORK/'edit.json').write_text(json.dumps(edit,indent=2)+'\n')
preview=json.loads(json.dumps(edit));preview.update(output='renders/beyond_the_belt_trailer_preview_v2.mp4',size=[960,540],fps=30)
for s in preview['shots']:s['take']=s['take'].replace('.mp4','.preview.mp4')
(WORK/'edit_preview.json').write_text(json.dumps(preview,indent=2)+'\n')
(WORK/'music_cuts.json').write_text(json.dumps(dict(source=str(MUSIC.relative_to(ROOT)),bpm=100,sections=[dict(source_in=s,duration=d) for s,d in sections]),indent=2)+'\n')
print('26 shots, exactly 60 seconds; edits and assembled soundtrack ready.')
