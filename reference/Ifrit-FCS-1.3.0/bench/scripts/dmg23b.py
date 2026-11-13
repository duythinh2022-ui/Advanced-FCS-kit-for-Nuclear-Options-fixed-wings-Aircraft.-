import csv,subprocess,itertools,os,sys
S=os.path.dirname(os.path.abspath(__file__))
D="WingFore_L+WingRear_L+Wingmid_L+Wingtip_L+Aileron_L+Flap_L"
V={"off":"OvershootPitchPriority=false CouplingRollLimit=0 BranchCoolTime=1 BranchCoolTimeStop=1".split(),"on":"OvershootPitchPriority=true CouplingRollLimit=0.5 BranchCoolTime=1 BranchCoolTimeStop=0.25".split()}
def run(args):
    f=f"{S}/dm.csv"
    subprocess.run(["dotnet","bin/bench.dll","one",f"detach={D}","trimT=3","throttle=1","post=0",f"csv={f}"]+args,cwd="/tmp/claude-0/bench",capture_output=True)
    return [{k:float(v) for k,v in x.items()} for x in csv.DictReader(open(f))]
for vn,extra in V.items():
    dep=0;n=0;res=[]
    for ias,r,pull,rf,env in itertools.product([350,450,550,650],[1,-1],[1,0.6],[1.0,2.0],[[],["alt=1000"],["alt=7000"],["gyro=None"],["gyro=Full"]]):
        hold=rf+2.5
        R=run([f"iasKmh={ias}",f"roll={r}",f"pull={pull}",f"hold={hold}",f"rollFromPull={rf}","rollT=2.5"]+env+extra)
        t0=rf-hold
        roll=[x for x in R if x["t"]>=t0]; pre=[x for x in R if x["t"]<t0]
        if not roll: continue
        am=max(x["alpha"] for x in roll); bm=max(abs(x["beta"]) for x in roll)
        d = am>20 or bm>15 or min(x["p"]*r for x in roll if x["t"]>t0+0.3)< -20
        p1=[x for x in roll if x["t"]>=t0+1.0][0]
        bank=(p1["phi"]-pre[-1]["phi"])*r
        if bank<-180: bank+=360
        drift=max(abs(x["p"]) for x in pre if x["t"]>-hold)  # roll rate during pull before roll input
        res.append((ias,r,d,bank,drift,am,bm)); n+=1; dep+=d
    print(f"{vn}: departures {dep}/{n}")
    for ias in [350,450,550,650]:
        for r in [1,-1]:
            sub=[x for x in res if x[0]==ias and x[1]==r]
            print(f"   {ias} {'right' if r>0 else 'left '}: departed {sum(x[2] for x in sub)}/{len(sub)}  bank@1s mean {sum(x[3] for x in sub)/len(sub):5.0f} min {min(x[3] for x in sub):5.0f}  |p| during pull before roll max {max(x[4] for x in sub):4.1f}  alpha max {max(x[5] for x in sub):4.0f}  |beta| max {max(x[6] for x in sub):4.1f}")
