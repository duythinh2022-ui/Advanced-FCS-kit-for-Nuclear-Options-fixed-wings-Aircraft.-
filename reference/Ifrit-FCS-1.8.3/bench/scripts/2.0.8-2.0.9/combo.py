import json,subprocess,sys,statistics as st
B='/home/claude/w/src/bench'
F={'KingRaptor','Intake_L','Intake_R','Fore','Cockpit','LERX_L','LERX_R'}
A={'Engine_L','Engine_R','Tailboom_L','Tailboom_R','Tail'}
def fam(n):
    n=n.replace('Aryx_KingRaptor','KingRaptor'); return 'F' if n in F else ('A' if n in A else 'O')
def fit(k,pk):
    f=f'/tmp/claude-0/fit_{k}_{pk}.json'
    subprocess.run(['dotnet','bin/bench.dll','cmsweep',f'StabDownwash={k}',f'StabStallAoA={pk}','BodyLiftShaping=false','ias=300','a0=-10','a1=42.5','da=2.5',f'dump={f}'],cwd=B,capture_output=True)
    d=json.load(open(f)); mfs=[];mas=[]
    for r in d['rows']:
        if abs(r['a'])<1: continue
        ML={'F':0,'A':0,'O':0}; NL={'F':0,'A':0,'O':0}; MD=0; ND=0
        for p in r['parts']:
            g=fam(p['n']); ML[g]+=p['ml']; NL[g]+=p['nl']; MD+=p['md']; ND+=p['nd']
        N0=sum(p['nl']+p['nd'] for p in r['parts']); Mo=ML['O']+MD; No=NL['O']+ND
        det=ML['F']*NL['A']-ML['A']*NL['F']
        mF=(-Mo*NL['A']-ML['A']*(N0-No))/det; mA=(ML['F']*(N0-No)+NL['F']*Mo)/det
        if r['a']>=0: mfs.append(mF); mas.append(mA)
    return st.mean(mfs),st.mean(mas),min(mfs),max(mfs)
for arg in sys.argv[1:]:
    k,pk=arg.split(':')
    mF,mA,lo,hi=fit(k,pk)
    print(f"k={k} peak={pk}: mF {mF:.3f} ({lo:.3f}..{hi:.3f}) mA {mA:.3f}")
