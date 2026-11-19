import json
F={'KingRaptor','Intake_L','Intake_R','Fore','Cockpit','LERX_L','LERX_R'}
A={'Engine_L','Engine_R','Tailboom_L','Tailboom_R','Tail'}
def fam(n):
    n=n.replace('Aryx_KingRaptor','KingRaptor'); return 'F' if n in F else ('A' if n in A else 'O')
ref=json.load(open('/tmp/claude-0/ref208.json')); raw=json.load(open('/tmp/claude-0/raw209.json')); I=raw['I']
out=[]
for r,rr in zip(raw['rows'],ref['rows']):
    ML={'F':0,'A':0,'O':0}; NL={'F':0,'A':0,'O':0}; MD=0; ND=0
    for p in r['parts']:
        k=fam(p['n']); ML[k]+=p['ml']; NL[k]+=p['nl']; MD+=p['md']; ND+=p['nd']
    Mt=sum(p['ml']+p['md'] for p in rr['parts']); Nt=sum(p['nl']+p['nd'] for p in rr['parts'])
    det=ML['F']*NL['A']-ML['A']*NL['F']
    if abs(det)<1e3: out.append((r['a'],None,None)); continue
    b1=Mt-(ML['O']+MD); b2=Nt-(NL['O']+ND)
    mF=(b1*NL['A']-ML['A']*b2)/det; mA=(ML['F']*b2-NL['F']*b1)/det
    out.append((r['a'],mF,mA))
for a,f,g in out:
    print(f"{a:6.1f} " + ("singular" if f is None else f"{f:6.3f} {g:6.3f}"))
