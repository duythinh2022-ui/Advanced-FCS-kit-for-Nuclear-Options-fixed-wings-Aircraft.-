import pandas as pd, numpy as np, sys
f=sys.argv[1]; thr=float(sys.argv[2]) if len(sys.argv)>2 else 5.0
d=pd.read_csv(f)
d=d[d['mode'].isin([3,6])].reset_index(drop=True)
# rudder usage: Rudder_L_now / 35
d['rud']=(d.Rudder_L_now.abs()+d.Rudder_R_now.abs())/2/35
b=d.beta.abs().values
over=b>thr
# segments
segs=[]; i=0; n=len(b)
while i<n:
    if over[i]:
        j=i
        while j<n and over[j]: j+=1
        segs.append((i,j)); i=j
    else: i+=1
print(f"{f}: {len(segs)} excursions > {thr} deg; total time {sum(d.t[j-1]-d.t[i] for i,j in segs):.1f}s of {d.t.iloc[-1]-d.t.iloc[0]:.0f}s")
rows=[]
for i,j in segs:
    k=i+int(np.argmax(b[i:j])); pk=b[k]
    if pk<thr+0.5: continue
    pre=d.iloc[max(0,i-30):i]  # 0.5s before
    w=d.iloc[max(0,i-60):min(n,j+30)]
    rows.append(dict(t0=d.t[i],tpk=d.t[k],dur=d.t[j-1]-d.t[i],pk=pk*np.sign(d.beta[k]),alpha0=d.alpha[i],alphapk=d.alpha[k],ias=d.ias[k]*1.0,
        p0=d.p[i],ppk=d.p[k],r0=d.r[i],rpk=d.r[k],sp=d.sp[k],sr0=d.sr[i],srpk=d.sr[k],sy=d.sy[k],
        rud0=d.rud[i],rudmax=w.rud.max(),lim0=d.stopSlipLim[i],hold=0,ysp=w.yawSatPitch.max(),mrg=d.stopSlipMargin[k],pred=w.stopSlipPred.max(),
        srflip=int(((np.sign(w.sr).diff().abs()>1)&(w.sr.abs()>0.3)).any()), pSmax=d.pSmax[i]))
r=pd.DataFrame(rows)
pd.set_option('display.width',250); pd.set_option('display.max_rows',500)
print(r.round(2).to_string())
