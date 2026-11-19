import pandas as pd, numpy as np, sys
f=sys.argv[1]; t0=float(sys.argv[2]); t1=float(sys.argv[3]); step=int(sys.argv[4]) if len(sys.argv)>4 else 3
d=pd.read_csv(f)
w=d[(d.t>=t0)&(d.t<=t1)].iloc[::step]
cols=['t','ias','alpha','beta','p','q','r','sp','sr','sy','qCmd','pSTarget','pSmax','pullTrade','paceHold','yawSatPitch','stopSlipU','stopSlipAB','aYaw','stopSlipLim','stopSlipPred','stopSlipMargin','yawSatPitch','yawSat','rudderAuth','aYaw','aRoll','qCmd','alphaCmd','Rudder_L_cmd','Rudder_L_now','Rudder_R_now','Elevator_L_now','Elevator_R_now','tvcNow','pullTrade','coupRollLim','latReserve','yawLead','stopSlipAB','stopSlipU','guard','omegaV','rS','asymCapEff']
cols=[c for c in cols if c in d.columns]
pd.set_option('display.width',400); pd.set_option('display.max_rows',1000); pd.set_option('display.max_columns',60)
print(w[cols].round(1).to_string(index=False))
