import sys,pandas as pd,numpy as np,warnings
warnings.filterwarnings('ignore')
def an(t):
    d=pd.read_csv(f'/home/claude/bench/runs/h_{t}.csv')
    d=d[d.t<-0.02].reset_index(drop=True)
    s=np.sign(d.sr.where(d.sr.abs()>0.5)).ffill()
    ch=d.index[(s!=s.shift())&s.notna()&s.shift().notna()]
    if len(ch)==0: return t+" no reversal"
    i=ch[0]; tr=d.t[i]; w=d[(d.t>=tr)&(d.t<=tr+1.2)]; pre=d[(d.t>tr-0.3)&(d.t<tr)]
    dt=d.t.diff().median()
    ps=w.p*np.cos(np.radians(w.alpha))+w.r*np.sin(np.radians(w.alpha))
    sg=np.sign(d.sr[i])
    mu=(ps*sg).cumsum()*dt
    t_half=(w.t[(ps*sg)>0.5*abs(pre.p.mean())].min()-tr) if ((ps*sg)>0.5*abs(pre.p.mean())).any() else np.nan
    gov=w.rollGov.min() if 'rollGov' in w else 1
    return f"{t:22s} a {pre.alpha.mean():5.1f} cmd {pre.alphaCmd.mean():5.1f} p0 {pre.p.mean():5.0f} | over {(w.alpha-w.alphaCmd).max():4.1f} q-qT {(w.q-w.qT).max():5.1f}/{(w.q-w.qT).min():6.1f} | tvc pre {pre.tvc.mean():5.1f} min {w.tvc.min():6.1f} | roll back 50% in {t_half:4.2f}s, new-dir roll @1.2s {mu.iloc[-1]:5.0f} | gov {gov:.2f}"
for t in sys.argv[1:]: print(an(t))
