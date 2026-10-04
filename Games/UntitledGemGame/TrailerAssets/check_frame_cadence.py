from pathlib import Path
import subprocess,json,concurrent.futures,numpy as np
ROOT=Path(__file__).resolve().parent
FILES=sorted(Path('/home/johan/Videos/2').glob('*.mkv'),key=lambda p:int(p.name.split('_')[0]))
W,H=320,180

def runs(values,threshold,fps=60):
 out=[];start=None
 for i,x in enumerate(values):
  if x<=threshold and start is None:start=i
  if start is not None and (x>threshold or i==len(values)-1):
   end=i if x>threshold else i+1
   if end-start>=3:out.append({'start':round(start/fps,3),'end':round((end+1)/fps,3),'held_frames':end-start+1,'duration_ms':round((end-start+1)*1000/fps)})
   start=None
 return sorted(out,key=lambda x:-x['held_frames'])

def check(p):
 probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-select_streams','v:0','-show_entries','stream=r_frame_rate,avg_frame_rate,width,height:packet=pts_time,duration_time','-of','json',str(p)]))
 times=sorted(float(x['pts_time']) for x in probe['packets'] if 'pts_time' in x);gaps=np.diff(times);fps=60
 cmd=['ffmpeg','-v','error','-threads','2','-i',str(p),'-map','0:v:0','-vf',f'scale={W}:{H}:flags=area,format=gray','-fps_mode','passthrough','-f','rawvideo','pipe:1']
 proc=subprocess.Popen(cmd,stdout=subprocess.PIPE);prev=None;diffs=[];world=[];exact=0;count=0
 while True:
  b=proc.stdout.read(W*H)
  if not b:break
  if len(b)!=W*H:raise RuntimeError('partial frame')
  f=np.frombuffer(b,np.uint8).reshape(H,W).astype(np.int16)
  if prev is not None:
   delta=np.abs(f-prev);diffs.append(float(delta.mean()));world.append(float(delta[:160].mean()));exact+=int(not delta.any())
  prev=f;count+=1
 if proc.wait()!=0:raise RuntimeError('decode failed')
 a=np.array(diffs);res={'file':p.name,'declared_fps':probe['streams'][0]['r_frame_rate'],'frames_decoded':count,'packets':len(times),'timing_gaps_over_25ms':[{'at':round(times[i],3),'gap_ms':round(float(x)*1000,2)} for i,x in enumerate(gaps) if x>.025],'timestamp_gap_max_ms':round(float(gaps.max())*1000,3),'exact_duplicate_frames':exact,'exact_duplicate_percent':round(100*exact/max(1,len(a)),2),'mean_diff_percentiles':np.percentile(a,[0,10,25,50,75,90,100]).round(4).tolist(),'almost_identical_percent':round(100*float((a<=.01).mean()),2),'holds':runs(diffs,.01),'world_holds':runs(world,.01),'diff_per_frame':diffs}
 (ROOT/'review'/('cadence_'+p.stem+'.json')).write_text(json.dumps(res,indent=2));print(json.dumps({k:v for k,v in res.items() if k not in ['diff_per_frame','world_holds','holds']}),flush=True);return res
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as ex:results=list(ex.map(check,FILES))
(ROOT/'frame_cadence_report.json').write_text(json.dumps(results,indent=2)+'\n')
