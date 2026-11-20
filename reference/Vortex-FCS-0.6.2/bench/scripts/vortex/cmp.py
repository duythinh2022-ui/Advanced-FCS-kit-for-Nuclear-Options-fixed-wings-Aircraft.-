import pandas as pd,numpy as np,sys,os,glob,warnings; warnings.filterwarnings('ignore')
a,b=sys.argv[1],sys.argv[2]
print(f"max |difference| per case, {os.path.basename(a)} vs {os.path.basename(b)}")
for f in sorted(glob.glob(a+'/*.csv')):
    g=b+'/'+os.path.basename(f)
    if not os.path.exists(g): continue
    x=pd.read_csv(f); y=pd.read_csv(g); n=min(len(x),len(y)); x=x.iloc[:n]; y=y.iloc[:n]
    d=lambda c: np.nanmax(np.abs(x[c].values-y[c].values))
    dpsi=np.nanmax(np.abs((x.psi.values-y.psi.values+180)%360-180))
    pos=np.nanmax(np.hypot(x.pos_x-y.pos_x,x.pos_z-y.pos_z))
    print(f"  {os.path.basename(f)[:-4]:5s} bank {d('phi'):6.2f} pitch {d('theta'):5.2f} hdg {dpsi:5.2f} deg | vs {d('vs'):5.2f} side {d('vSide'):5.2f} fwd {d('uFwd'):5.2f} m/s | height {d('alt'):6.2f} m pos {pos:6.2f} m | yaw rate {d('r'):5.2f} deg/s | thrust cmd {d('thrEng'):.2f}")
