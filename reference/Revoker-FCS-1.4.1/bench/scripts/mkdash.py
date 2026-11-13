import json,re,csv,math,os
import numpy as np
from scipy.optimize import linprog
O=os.path.expanduser("~/w/out"); D=os.path.expanduser("~/w/dash")
# --- pitch authority
rows=[]
for thr,lab in (("1","MAX AB"),("0.88","MIL"),("0","Idle")):
    ias=None
    for line in open(f"{O}/auth_thr{thr}.txt"):
        m=re.match(r"# IAS (\d+) km/h",line)
        if m: ias=int(m.group(1)); continue
        if line.startswith("#") or line.startswith("alpha") or not line.strip(): continue
        f=line.split("|")[0].split()
        a,nz,q0,dnA,dnT,dn45,upT,upA,up45=map(float,f[:9])
        rows.append(dict(throttle=lab,ias_kmh=ias,alpha_deg=a,neutral=q0,noseUp_tails=upA,noseUp_tails_tvc=upT,noseDown_tails=dnA,noseDown_tails_tvc=dnT,nz_neutral=nz))
with open(f"{D}/pitch_authority.csv","w",newline="") as fh:
    w=csv.DictWriter(fh,fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
# --- lateral LP ceilings
SETS={"Surfaces only":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R"],
      "+ split decelerons":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","Split_L","Split_R"],
      "+ roll TVC":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","TVC_L","TVC_R"],
      "+ split + roll TVC":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","Split_L","Split_R","TVC_L","TVC_R"]}
lat=[]
for l in open(f"{O}/eff_mil.jsonl"):
    r=json.loads(l)
    a=math.radians(r["alpha"]); ca,sa=math.cos(a),math.sin(a)
    for sn,names in SETS.items():
        D3=np.array([d for n in names for d in r["eff"][n]]).T
        pS=D3[0]*ca+D3[2]*sa; rS=D3[2]*ca-D3[0]*sa
        res=linprog(-pS,A_eq=np.vstack([rS,D3[1]]),b_eq=[0,0],bounds=[(0,1)]*D3.shape[1],method="highs")
        lat.append(dict(ias_kmh=int(r["ias"]),alpha_deg=r["alpha"],effectors=sn,max_vv_roll_accel=round(-res.fun,1)))
with open(f"{D}/lateral_ceiling.csv","w",newline="") as fh:
    w=csv.DictWriter(fh,fieldnames=list(lat[0])); w.writeheader(); w.writerows(lat)
# --- loaded roll time histories
VN={"stock":"Stock FBW","V1":"2.2.5 law, pitch-only TVC","V2":"+ split & roll TVC","V4":"+ 15° yaw TVC"}
ts=[]
for k,lab in VN.items():
    with open(f"{O}/ts/D350_{k}.csv") as fh:
        for r in csv.DictReader(fh):
            t=float(r["t"])
            if t<2.0-1e-6 or abs(round(t/0.1)*0.1-t)>0.011: continue
            ts.append(dict(variant=lab,t_s=round(t,2),alpha_deg=round(float(r["alpha"]),2),beta_deg=round(float(r["beta"]),2),vv_roll_rate=round(float(r["pV"]),2),ias_kmh=round(float(r["ias"]),1)))
with open(f"{D}/loaded_roll_350.csv","w",newline="") as fh:
    w=csv.DictWriter(fh,fieldnames=list(ts[0])); w.writeheader(); w.writerows(ts)
# --- variant matrix
LAB={"V0_stock":"Stock FBW","V1_port_pitchTVC":"2.2.5 law, pitch-only TVC (AoA 65)","V2_native65":"+ split & roll TVC (AoA 65)","V3_native90":"+ split & roll TVC (AoA 90)","V4_yaw65":"+ 15° yaw TVC (AoA 65)","V5_yaw90":"+ 15° yaw TVC (AoA 90)","V6_yaw90_q60":"+ yaw TVC, AoA 90, 60°/s pitch"}
M={}
for line in open(f"{O}/matrix.csv"):
    p=line.strip().split(",")
    if len(p)<5: continue
    v,test,cond,st=p[:4]; met={}
    for kv in p[4:]:
        if "=" in kv:
            k,val=kv.split("=",1)
            try: met[k]=float(val.split("..")[0]) if ".." not in val else val
            except: met[k]=val
    M.setdefault(v,{})[(test,cond)]=(st,met)
def g(v,t,c,k):
    s=M[v].get((t,c)); 
    if not s: return ""
    x=s[1].get(k,""); return x
summ=[]
order=["V0_stock","V1_port_pitchTVC","V2_native65","V3_native90","V4_yaw65","V5_yaw90","V6_yaw90_q60"]
for i,v in enumerate(order):
    dep=sum(1 for s in M[v].values() if s[0]=="DEP")
    summ.append(dict(order=i,variant=LAB[v],
        peak_g_600=g(v,"A_pull","600","nzMax"),pitch_rate_450=g(v,"A_pull","450","qMax"),
        max_aoa_hold_350=g(v,"B_hold","350","alphaHoldMin"),bank_1s_400=g(v,"C_roll1g","400","bank1"),
        loaded_roll_rate_350=g(v,"D_loadedRoll","350","pVmax"),loaded_roll_beta_350=g(v,"D_loadedRoll","350","betaMax"),
        loaded_roll_aoa_350=g(v,"D_loadedRoll","350","alphaMinRoll"),
        pedal_yaw_rate_300=g(v,"H_pedal","300","rMax"),reversal_beta_300=g(v,"E_reversal","300","betaMax"),
        min_aoa_push_220=g(v,"I_pushLow","220","alphaMin"),runs_over_15_beta=dep))
with open(f"{D}/variant_matrix.csv","w",newline="") as fh:
    w=csv.DictWriter(fh,fieldnames=list(summ[0])); w.writeheader(); w.writerows(summ)
print(len(rows),len(lat),len(ts),len(summ))
for s in summ: print(s)
