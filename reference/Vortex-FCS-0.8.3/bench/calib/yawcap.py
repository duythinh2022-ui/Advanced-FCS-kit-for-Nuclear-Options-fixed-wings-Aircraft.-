# Compare, per AoA band, the plant's yaw from rudder + yaw nozzle (eqerr yawsplit) with the FCS's logged aYaw (all effectors),
# and the measured yaw acceleration, at samples where both are near their stops.
import sys, numpy as np, pandas as pd
from scipy.signal import savgol_filter
eq=pd.read_csv(sys.argv[1]); lg=pd.read_csv(sys.argv[2], usecols=lambda c: c in ('t','aYaw','yawSat','rudderAuth','rudder_now','tvcyNow','rudder_cmd','tvcyCmd','alpha','r','tas','ias','yawLead','rCmd'))
m=eq.merge(lg, on='t', suffixes=('','_lg'))
m['effYaw']=m['rdot']-m['rdotNoYawEff']            # plant yaw accel from rudder + nozzle at their logged angles
# measured yaw accel
t=m['t'].values; r=m['r'].values
rd=np.full(len(r),np.nan); br=np.where(np.diff(t)>0.03)[0]+1
for seg in np.split(np.arange(len(t)),br):
    if len(seg)>12: rd[seg]=savgol_filter(r[seg],9,3,deriv=1,delta=np.median(np.diff(t[seg])))
m['rdotMeas']=rd
pin=(m['tvcyNow'].abs()>=13.5)&(m['rudder_now'].abs()>=23)
print(f"samples {len(m)}, both effectors near stops: {pin.sum()}")
bands=[(-90,-40),(-40,-20),(-20,0),(0,20),(20,40),(40,90)]
print(f"{'AoA band':>10} {'n':>5} {'effYaw(plant)':>14} {'aYaw(FCS all)':>14} {'rdot pred':>10} {'rdot meas':>10} {'tas':>6} {'|tvcy|':>6} {'|rud|':>6}")
for lo,hi in bands:
    s=m[pin&(m['alpha']>=lo)&(m['alpha']<hi)]
    if len(s)<5: continue
    print(f"{lo:>4}..{hi:<4} {len(s):>5} {s['effYaw'].abs().median():>14.1f} {s['aYaw'].median():>14.1f} {s['rdot'].abs().median():>10.1f} {s['rdotMeas'].abs().median():>10.1f} {s['tas'].median():>6.0f} {s['tvcyNow'].abs().median():>6.1f} {s['rudder_now'].abs().median():>6.1f}")
# unpinned reference too: the ratio of plant yaw accel to measured in general (plant honesty)
ok=m['rdotMeas'].notna()
print("overall plant rdot vs measured: RMS err %.1f, LS scale %.3f" % (np.sqrt(np.nanmean((m['rdot']-m['rdotMeas'])**2)), np.nansum(m['rdot']*m['rdotMeas'])/np.nansum(m['rdot']**2)))
