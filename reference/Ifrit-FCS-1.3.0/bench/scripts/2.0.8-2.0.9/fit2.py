import json,subprocess,statistics as st,sys
B='/home/claude/w/src/bench'
F={'KingRaptor','Intake_L','Intake_R','Fore','Cockpit','LERX_L','LERX_R'}
A={'Engine_L','Engine_R','Tailboom_L','Tailboom_R','Tail'}
def fam(n):
    n=n.replace('Aryx_KingRaptor','KingRaptor'); return 'F' if n in F else ('A' if n in A else 'O')
g=float(sys.argv[1])
f='/tmp/claude-0/fit2.json'
subprocess.run(['dotnet','bin/bench.dll','cmsweep','BodyLiftShaping=false','ias=300','a0=5','a1=40','da=2.5',f'dump={f}'],cwd=B,capture_output=True)
ref=subprocess.run(['dotnet','bin/bench.dll','cmsweep','ias=300','a0=5','a1=40','da=2.5','dump=/tmp/claude-0/fit2r.json'],cwd=B,capture_output=True)
d=json.load(open(f)); r0=json.load(open('/tmp/claude-0/fit2r.json'))
mfs=[];mas=[]
for r,rr in zip(d['rows'],r0['rows']):
    ML={'F':0,'A':0,'O':0}; NL={'F':0,'A':0,'O':0}; MD=0; ND=0
    for p in r['parts']:
        k=fam(p['n']); ML[k]+=p['ml']; NL[k]+=p['nl']; MD+=p['md']; ND+=p['nd']
    Mt=sum(p['ml']+p['md'] for p in rr['parts']); Nt=sum(p['nl']+p['nd'] for p in rr['parts'])*(1+g)
    Mo=ML['O']+MD; No=NL['O']+ND
    det=ML['F']*NL['A']-ML['A']*NL['F']
    b1=Mt-Mo; b2=Nt-No
    mF=(b1*NL['A']-ML['A']*b2)/det; mA=(ML['F']*b2-NL['F']*b1)/det
    mfs.append(mF); mas.append(mA)
    print(f"{r['a']:5.1f} mF {mF:.3f} mA {mA:.3f}")
print("mean",round(st.mean(mfs),3),round(st.mean(mas),3))
