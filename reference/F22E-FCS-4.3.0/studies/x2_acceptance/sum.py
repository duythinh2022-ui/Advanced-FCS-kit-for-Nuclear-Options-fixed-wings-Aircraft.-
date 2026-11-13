import csv,math,sys
S='/home/claude/w/acc/out'
V=sys.argv[1:] or ['cur','P']
def L(f): return [{k:float(v) for k,v in x.items() if v!=''} for x in csv.DictReader(open(f))]
m=lambda a,k: sum(x[k] for x in a)/max(1,len(a))
print('### lr case var | roll vel | stabL(loc) stabR(loc) | TVC ss | TVC onset min | beta')
for c in ['lr_400_1','lr_250_1','lr_250_0.75','lr_300_0','lr_250_0']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); t0=-4.5
        on=[x for x in r if t0<=x['t']<t0+0.6]; ss=[x for x in r if t0+1.5<=x['t']<t0+4.5]
        wv=sum(x['p']*math.cos(math.radians(x['alpha']))+x['r']*math.sin(math.radians(x['alpha'])) for x in ss)/len(ss)
        print(f"{c:11s} {v:3s} | {wv:5.1f} | {m(ss,'stabL'):+6.1f} | {m(ss,'stabR'):+6.1f} | {m(ss,'tvc'):+5.1f} | {min(x['tvc'] for x in on):+6.1f} | {max(abs(x['beta']) for x in r if x['t']>=t0):4.1f}")
print('### hold var | AoA | stab | TVC | IAS')
for c in ['hold20_1','hold20_0','hold30_1','hold30_0','hold45_1','hold45_0']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); w=[x for x in r if -3<=x['t']<0]
        print(f"{c:9s} {v:3s} | {m(w,'alpha'):4.1f} | {(m(w,'stabL')+m(w,'stabR'))/2:+6.1f} | {m(w,'tvc'):+5.1f} | {m(w,'ias'):4.0f} | max AoA {max(x['alpha'] for x in r):.1f}")
print('### push var | AoA0 | qmin | t20 | minAoA')
for c in ['push_idle','push_notvc','push_ab']:
    for v in V:
        r=L(f'{S}/F_{v}_{c}.csv'); tp=-3.0
        after=[x for x in r if x['t']>=tp]; a0=after[0]
        t20=next((x['t']-tp for x in after if x['alpha']<20),float('nan'))
        print(f"{c:10s} {v:3s} | {a0['alpha']:5.1f} | {min(x['q'] for x in after):6.1f} | {t20:5.2f} | {min(x['alpha'] for x in after):6.1f}")
