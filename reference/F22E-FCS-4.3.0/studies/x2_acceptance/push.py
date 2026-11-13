# free replays of planned push-overs: push.py <tag> <bench dir> [Name=value ...] -> out_push/<tag>/*.csv + summary
import json,os,subprocess,sys
from concurrent.futures import ThreadPoolExecutor
E=json.load(open('/home/claude/w/acc/events.json')); tag=sys.argv[1]; B=sys.argv[2]; kv=sys.argv[3:]
O=f'/home/claude/w/acc/out_push/{tag}'; os.makedirs(O,exist_ok=True)
J=[]
for lg,ts in E.items():
    for t in ts:
        J.append(['dotnet','bin/bench.dll','replay',f'log=/home/claude/w/acc/logs/{lg}',f't0={t-0.5:.3f}',f't1={t+3.0:.3f}',f'out={O}/{lg[9:-4]}_{t:.2f}.csv']+kv)
env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json')
with ThreadPoolExecutor(2) as ex: rc=list(ex.map(lambda c: subprocess.run(c,cwd=B,env=env,capture_output=True).returncode,J))
print(tag,len(J),'failures',sum(1 for x in rc if x))
