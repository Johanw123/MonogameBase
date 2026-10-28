"""Capture trailer scenes with a light preview pass and bounded software-renderer threads."""
import argparse,json,os,sys,tempfile
from pathlib import Path
WORK=Path(__file__).resolve().parent
ROOT=WORK.parents[2]
os.environ.setdefault('LP_NUM_THREADS','4')
sys.path.insert(0,str(ROOT/'Marketing/Skills/beyond-the-belt-shorts/scripts'))
import capture
parser=argparse.ArgumentParser()
parser.add_argument('names',nargs='+')
parser.add_argument('--preview',action='store_true')
args=parser.parse_args()
original_prepare=capture.prepare_scene
original_run=capture.run_game
def prepare(*a,**kw):
 p=original_prepare(*a,**kw)
 if args.preview:
  s=json.loads(p.read_text());s['fps']=15;p.write_text(json.dumps(s))
 return p
capture.prepare_scene=prepare
capture.run_game=lambda a,log,limit=3600:original_run(a,log,limit)
failed=[]
with tempfile.TemporaryDirectory(prefix='btb-trailer-') as temp:
 for name in args.names:
  path=WORK/'scenes'/f'{name}.json'
  if not capture.capture(path,args.preview,False,Path(temp)):failed.append(name)
if failed:sys.exit('Failed: '+', '.join(failed))
