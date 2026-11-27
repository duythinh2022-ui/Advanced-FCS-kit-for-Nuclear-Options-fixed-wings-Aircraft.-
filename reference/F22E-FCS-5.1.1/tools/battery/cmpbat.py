import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import json,sys
A,B,tag=sys.argv[1],sys.argv[2],sys.argv[3]
for cg in ('-0.05','0.0','0.05'):
    b=json.load(open(f'{RUNS}/{A}_{tag}_{cg}/summary.json')); n=json.load(open(f'{RUNS}/{B}_{tag}_{cg}/summary.json'))
    print('CG',cg)
    for k in b:
        diffs={m:(round(b[k][m],2),round(n[k][m],2)) for m in b[k] if abs(b[k][m]-n[k][m])>0.05}
        if diffs: print(' ',k,diffs)
