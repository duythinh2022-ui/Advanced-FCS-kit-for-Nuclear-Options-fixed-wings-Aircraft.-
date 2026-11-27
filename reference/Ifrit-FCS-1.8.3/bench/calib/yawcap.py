import sys, numpy as np, pandas as pd
from scipy.signal import savgol_filter
eq=pd.read_csv(sys.argv[1]); lg=pd.read_csv(sys.argv[2], usecols=lambda c: c in ('t','aYaw','yawSat','elevator_L_now','elevator_R_now','splitL','splitR','alpha','r','tas','tvcdNow','rCmd','sr','sy','pSTarget','p'))
m=eq.merge(lg, on='t', suffixes=('','_lg'))
m['yawSplit']=m['rdot']-m['rdotNoYawEff']        # plant yaw from the split decelerons
m['yawTail']=m['rdotNoYawEff']-m['pdotNoYawEff'] # plant yaw from the tails' differential
m['dTail']=(m['elevator_L_now']-m['elevator_R_now']).abs()
t=m['t'].values; r=m['r'].values
rd=np.full(len(r),np.nan); br=np.where(np.diff(t)>0.03)[0]+1
for seg in np.split(np.arange(len(t)),br):
    if len(seg)>12: rd[seg]=savgol_filter(r[seg],9,3,deriv=1,delta=np.median(np.diff(t[seg])))
m['rdotMeas']=rd
print(f"samples {len(m)}; tail differential p95 {m['dTail'].quantile(.95):.1f} deg, max {m['dTail'].max():.1f}; split>5deg {(m[['splitL','splitR']].max(axis=1)>5).mean()*100:.1f}% of samples")
big=m['dTail']>=m['dTail'].quantile(.9)
bands=[(-90,-30),(-30,0),(0,15),(15,30),(30,45),(45,90)]
print(f"{'AoA band':>10} {'n':>5} {'dTail':>6} {'yawTail':>8} {'yawSplit':>9} {'aYaw':>6} {'rdot pred':>10} {'meas':>6} {'tas':>5}")
for lo,hi in bands:
    s=m[big&(m['alpha']>=lo)&(m['alpha']<hi)]
    if len(s)<5: continue
    print(f"{lo:>4}..{hi:<4} {len(s):>5} {s['dTail'].median():>6.1f} {s['yawTail'].abs().median():>8.1f} {s['yawSplit'].abs().median():>9.1f} {s['aYaw'].median():>6.0f} {s['rdot'].abs().median():>10.1f} {s['rdotMeas'].abs().median():>6.1f} {s['tas'].median():>5.0f}")
print("overall plant rdot vs measured: RMS err %.1f, LS scale %.3f" % (np.sqrt(np.nanmean((m['rdot']-m['rdotMeas'])**2)), np.nansum(m['rdot']*m['rdotMeas'])/np.nansum(m['rdot']**2)))
