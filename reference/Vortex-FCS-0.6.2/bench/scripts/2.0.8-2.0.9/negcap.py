import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cases=[("AB",["throttle=1"]),("idle",["throttle=0"]),("TVC off AB",["throttle=1","DisableTVC=true"]),("TVC off AB, cap -30",["throttle=1","DisableTVC=true","NoTvcAoACapNeg=30"]),("idle Full65",["throttle=0","AoAFull65=true"])]
res=runmany([["iasKmh=220","pull=-1","hold=5","trimT=1","post=3","roll=0","pullRamp=0.2"]+c for _,c in cases])
for (nm,_),D in zip(cases,res):
    H=[x for x in D if -5<=x['t']<=0]
    print(f"{nm:22s} alphaMin {min(x['alpha'] for x in H):6.1f}  held {H[-1]['alpha']:6.1f}  ias {H[-1]['ias']:4.0f}")
