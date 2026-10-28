"""Check the rendered trailer and its source evidence; write a reviewable JSON report."""
import json,subprocess,re,sys
from pathlib import Path
WORK=Path(__file__).resolve().parent
edit=json.loads((WORK/'edit.json').read_text())
video=WORK/edit['output']
def run(args):return subprocess.run(args,capture_output=True,text=True,check=True)
info=json.loads(run(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(video)]).stdout)
v=next(s for s in info['streams'] if s['codec_type']=='video')
a=next(s for s in info['streams'] if s['codec_type']=='audio')
assert [v['width'],v['height']]==edit['size']
assert v['avg_frame_rate']=='60/1'
assert abs(float(info['format']['duration'])-60)<.05
assert a['sample_rate']=='48000' and a['channels']==2
run(['ffmpeg','-v','error','-i',str(video),'-f','null','-'])
measure=run(['ffmpeg','-hide_banner','-nostats','-i',str(video),'-af','loudnorm=I=-14:TP=-1.5:LRA=11:print_format=json','-f','null','-']).stderr
loud=json.loads(re.findall(r'\{[^{}]*"input_i"[^{}]*\}',measure,re.S)[-1])
assert abs(float(loud['input_i'])+14)<1.0,loud
assert float(loud['input_tp'])<=-1.0,loud
sources={}
for shot in edit['shots']:
 name=shot['take'];take=WORK/name
 report=json.loads(take.with_suffix('.capture.json').read_text())
 assert report['completed']
 assert report['fps']==60 and report['width']==3840 and report['height']==2160
 assert shot['in']+shot['dur']<=report['frames']/report['fps']+.02
 assert shot.get('speed',1)==1
 assert not any(a['do']=='zoom' for a in report['scene']['actions'])
 assert take.with_suffix('.sfx.wav').exists()
 samples=[s for s in report['samples'] if shot['in']<=s['t']<shot['in']+shot['dur']]
 sources[name]=dict(frames=report['frames'],fps=report['fps'],sounds=sum(e['type']=='sound' for e in report['events']),
  min_field=min(s['active_gems'] for s in report['samples']),max_field=max(s['active_gems'] for s in report['samples']),
  max_ships_waiting_for_fuel=max(s.get('ships_waiting_for_fuel',0) for s in report['samples']))
result=dict(video=edit['output'],duration=float(info['format']['duration']),size=edit['size'],fps=60,
 full_decode='passed',audio_loudness_lufs=float(loud['input_i']),audio_true_peak_dbtp=float(loud['input_tp']),
 shots=len(edit['shots']),unique_takes=len(sources),sources=sources)
(WORK/'quality.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k!='sources'},indent=2))
