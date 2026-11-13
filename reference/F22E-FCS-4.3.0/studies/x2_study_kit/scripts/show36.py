import os
import csv,sys
r=list(csv.DictReader(open(sys.argv[1])))
cols=['t','ias','alpha','beta','p','r','omegaV','phi','stabL','stabR','sL_laoa','sR_laoa','tvc','thr','rudL','rudR','ailL','ailR','flapL','flapR']
print(' '.join(f'{c:>7}' for c in cols))
last=-9
for x in r:
    t=float(x['t'])
    if t>=float(sys.argv[2]) and t-last>=0.249:
        last=t; print(' '.join(f'{float(x[c]):7.1f}' for c in cols))
