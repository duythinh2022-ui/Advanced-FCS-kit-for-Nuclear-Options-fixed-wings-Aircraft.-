# runseq.py <tag> <cg> [Name=value ...]: windows in flight order, the learned-braking stores carried from each window to the next
# (faded by the flight time between them, KAchHalfLife), as one continuous flight would. -> out/<tag>_<cg>/wNN.csv, kach.json
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import json, subprocess, sys, os, math, re
B=BENCH; tag, cg = sys.argv[1], sys.argv[2]; extra = sys.argv[3:]
HL = float(next((x.split('=')[1] for x in extra if x.startswith('KAchHalfLife=')), 120))
base = open(TZF).read().split() + ["MassTarget=29200","FuelFrac=1",f"CgAftShift={cg}","CapturePlanLearned=true","CaptureRollShare=false"]
W = json.load(open(OV+"/windows.json")); od=f"{OV}/out/{tag}_{cg}"; os.makedirs(od, exist_ok=True)
env = dict(os.environ)
st = [0.0,0.0,0.0,0.0]; tPrev=None; log=[]
for w in W:
    if tPrev is not None:
        f = 0.5 ** (max(0.0, w['t0'] - tPrev) / HL); st[2]*=f; st[3]*=f
    seed=[f"KAchSeedHeld={st[0]}",f"KAchSeedFree={st[1]}",f"KAchSeedWHeld={st[2]}",f"KAchSeedWFree={st[3]}"]
    r = subprocess.run(["dotnet", B+"/bin/bench.dll", "replay", f"log={OV}/win/w{w['id']:02d}.csv", f"t0={w['t0']}", f"t1={w['t1']}", f"out={od}/w{w['id']:02d}.csv"] + base + seed + extra, cwd=B, env=env, capture_output=True, text=True)
    m = re.search(r"#kach (\S+) (\S+) (\S+) (\S+)", r.stderr)
    if r.returncode or not m: print("FAIL", w['id'], r.stderr[-300:]); break
    st=[float(x) for x in m.groups()]; tPrev=w['t1']; log.append(dict(id=w['id'],seedIn=seed,out=st))
json.dump(log, open(od+"/kach.json","w"), indent=0)
print(tag, cg, "final stores held/free", [round(x,2) for x in st])
