import os
import re,sys
def load(f):
    r=[]
    for l in open(f):
        m=re.search(r'alpha\s+(\S+).*Cm\*Sc\s+(\S+).*CN\*S\s+(\S+)',l)
        if m: r.append(tuple(map(float,m.groups())))
    return r
for ias in (400,800):
    a=load(f'cm_{ias}.txt'); b=load(f'cm0_{ias}.txt')
    print(f'--- {ias} km/h   alpha | NP aft of CG (cur, orig CG) | CoP aft of CG (cur)')
    for i in range(1,len(a)-1):
        al=a[i][0]
        np_=-(a[i+1][1]-a[i-1][1])/(a[i+1][2]-a[i-1][2])
        np0=-(b[i+1][1]-b[i-1][1])/(b[i+1][2]-b[i-1][2])
        cop=-a[i][1]/a[i][2] if abs(a[i][2])>3 else float('nan')
        print(f'{al:6.0f}  {np_:7.3f} {np0:7.3f}   {cop:7.3f}')
