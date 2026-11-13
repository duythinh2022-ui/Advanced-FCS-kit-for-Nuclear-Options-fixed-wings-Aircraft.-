#!/usr/bin/env python3
# score.py <tag> [--logs a,b]: equation-error map per AoA band
import sys,glob,os,numpy as np,pandas as pd
from scipy.signal import savgol_filter
B='/home/claude/su30/src/bench/out/eq/'
def load(tag,logs=None):
    fr=[]
    for f in sorted(glob.glob(B+tag+'/*.csv')):
        n=os.path.basename(f)[:-4]
        if logs and n not in logs: continue
        d=pd.read_csv(f); d['log']=n
        t=d.t.values
        br=np.r_[0,np.where(np.diff(t)>0.05)[0]+1,len(t)]
        for ax in 'pqr':
            o=np.full(len(d),np.nan)
            for a,b in zip(br[:-1],br[1:]):
                if b-a>=11: o[a:b]=savgol_filter(d[ax].values[a:b],9,3,deriv=1,delta=1/60)
            d[ax+'m']=o
        o=np.full(len(d),np.nan)
        for a,b in zip(br[:-1],br[1:]):
            if b-a>=11: o[a:b]=savgol_filter(d.nz.values[a:b],9,3)
        d['nzm']=o
        fr.append(d)
    D=pd.concat(fr,ignore_index=True)
    bad=(D.pm.abs()>1500)|(D.qm.abs()>800)|(D.rm.abs()>400)|(D.nz.diff().abs()>1.5)
    for c in ['pm','qm','rm','nzm']: D.loc[bad,c]=np.nan
    return D
def tab(D,label=''):
    D=D.copy(); D['ab']=pd.cut(D.alpha,[-40,0,10,20,30,40,50,55,60,65,70,75,90])
    def f(x):
        m=x.qm.notna()&(x.tas>40)
        if m.sum()<30: return pd.Series(dtype=float)
        o={}
        o['n']=m.sum()
        for a,pm,pd_ in [('p','pm','pdot'),('q','qm','qdot'),('r','rm','rdot')]:
            e=x[pd_][m]-x[pm][m]; o[a+'_bias']=e.mean(); o[a+'_rms']=np.sqrt((e**2).mean()); o[a+'_sd']=x[pm][m].std()
        e=x.nzp[m]-x.nzm[m]; o['nz_bias']=e.mean(); o['nz_rms']=np.sqrt((e**2).mean())
        return pd.Series(o)
    print(label); print(D.groupby('ab',observed=True).apply(f,include_groups=False).round(1).to_string())
if __name__=='__main__':
    tag=sys.argv[1]; D=load(tag)
    tab(D,tag+' all logs')
    print('per log (alpha>40, tas>40): rms p q r / bias q')
    for n,x in D[(D.alpha>40)&(D.tas>40)].groupby('log'):
        print(n,*[round(float(np.sqrt(np.nanmean((x[a+'dot']-x[a+'m'])**2))),1) for a in 'pqr'], round(float(np.nanmean(x.qdot-x.qm)),1), 'sd q',round(float(x.qm.std()),1))
