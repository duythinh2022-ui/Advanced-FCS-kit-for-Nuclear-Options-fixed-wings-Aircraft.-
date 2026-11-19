import glob,sys,pandas as pd,numpy as np
def swings(x,thr):
    # count reversals with amplitude > thr (peak-to-peak) using a simple zigzag
    n=0; ext=x[0]; d=0
    for v in x[1:]:
        if d>=0:
            if v>ext: ext=v
            elif ext-v>thr: n+=1 if d!=0 else 0; d=-1; ext=v
        if d<0:
            if v<ext: ext=v
            elif v-ext>thr: n+=1; d=1; ext=v
    return n
for tag in sys.argv[1:]:
    S=[];Q=[];T=[]
    for f in glob.glob(f'runs/{tag}_*_*_*.csv'):
        pd_=float(f.split('_')[-1][:-4]); d=pd.read_csv(f); d['t']=d.t+5; w=d[(d.t>pd_)&(d.t<pd_+3)]
        S.append(swings(w.qCmd.values,10)); Q.append(swings(w.q.values,10)); T.append(swings(w.tvc.values,10))
    print(tag,len(S),'qCmd swings>10: mean %.2f max %d | q swings>10 mean %.2f max %d | tvc swings>10 mean %.2f'%(np.mean(S),max(S),np.mean(Q),max(Q),np.mean(T)))
