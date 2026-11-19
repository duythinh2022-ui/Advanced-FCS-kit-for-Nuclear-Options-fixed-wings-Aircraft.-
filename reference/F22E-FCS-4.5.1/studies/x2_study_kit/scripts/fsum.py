import os
import csv,math,glob
S=os.environ.get('OUT_DIR','.')
def L(f): return [{k:float(v) for k,v in x.items() if v!=''} for x in csv.DictReader(open(f))]
V=['cur','N','T','X2']
m=lambda a,k: sum(x[k] for x in a)/max(1,len(a))
print('### Loaded roll at 36 (roll starts 1.5 s into the pull); steady = 1.5-4.5 s after roll start')
print('case        var | roll about vel | stab L (local) | stab R (local) | TVC steady | TVC onset min | beta max')
for c in ['lr_400_1','lr_250_1','lr_250_0.75','lr_300_0','lr_250_0']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); t0=-4.5
        on=[x for x in r if t0<=x['t']<t0+0.6]; ss=[x for x in r if t0+1.5<=x['t']<t0+4.5]
        wv=sum(x['p']*math.cos(math.radians(x['alpha']))+x['r']*math.sin(math.radians(x['alpha'])) for x in ss)/len(ss)
        print(f"{c:11s} {v:3s} | {wv:5.0f} | {m(ss,'stabL'):+6.1f} ({m(ss,'sL_laoa'):4.1f}) | {m(ss,'stabR'):+6.1f} ({m(ss,'sR_laoa'):4.1f}) | {m(ss,'tvc'):+5.1f} | {min(x['tvc'] for x in on):+6.1f} | {max(abs(x['beta']) for x in r if x['t']>=t0):4.1f}")
print('### Held AoA (full pull to the cap, 300 km/h entry), mean over the last 3 s')
print('case        var | AoA | mean stab | TVC | IAS')
for c in ['hold20_1','hold20_0','hold30_1','hold30_0','hold45_1','hold45_0']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); w=[x for x in r if -3<=x['t']<0]
        print(f"{c:11s} {v:3s} | {m(w,'alpha'):4.1f} | {(m(w,'stabL')+m(w,'stabR'))/2:+6.1f} | {m(w,'tvc'):+5.1f} | {m(w,'ias'):4.0f}")
print('### Push from 45 (4 s full pull, then full push)')
print('case        var | AoA at push | peak nose-down q | time to AoA 20 | min AoA | IAS at push')
for c in ['push_idle','push_notvc','push_ab']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); tp=-3.0
        a0=[x for x in r if x['t']>=tp][0]
        after=[x for x in r if x['t']>=tp]
        t20=next((x['t']-tp for x in after if x['alpha']<20),float('nan'))
        print(f"{c:11s} {v:3s} | {a0['alpha']:5.1f} | {min(x['q'] for x in after):6.1f} | {t20:5.2f} s | {min(x['alpha'] for x in after):6.1f} | {a0['ias']:4.0f}")
