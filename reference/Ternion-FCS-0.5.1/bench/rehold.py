import sys,pandas as pd,numpy as np
def an(t):
    d=pd.read_csv(f'/home/claude/bench/runs/h_{t}.csv')
    w=d[(d.sr.abs()>0.5)]; t0,t1=w.t.iloc[0],w.t.iloc[-1]
    pre=d[(d.t>t0-0.6)&(d.t<t0-0.05)]; ww=d[(d.t>=t0)&(d.t<=min(t1+0.8,-0.02))]
    dt=d.t.diff().median()
    ps=d.p*np.cos(np.radians(d.alpha))+d.r*np.sin(np.radians(d.alpha)); d['ps']=ps
    mu=np.cumsum(ps.where(d.t>=t0,0))*dt; ww=d[(d.t>=t0)&(d.t<=min(t1+0.8,-0.02))]
    mu1=mu[(d.t-(t0+1)).abs().idxmin()]; mu2=mu[(d.t-(t0+2)).abs().idxmin()]
    pinned=(ww.tvc<=-19.7).mean()
    ov=(ww.alpha-ww.alphaCmd).max()
    return f"{t:22s} a0 {pre.alpha.mean():5.1f} cmd {pre.alphaCmd.mean():5.1f}..{ww.alphaCmd.max():5.1f} | over {ov:4.1f} | psMax {ww.ps.abs().max():4.0f} roll@1s {mu1:5.0f} @2s {mu2:5.0f} | tvc mean {ww.tvc.mean():6.1f} pinned {pinned*100:3.0f}% | q-qT rms {np.sqrt(((ww.q-ww.qT)**2).mean()):5.2f}"
if __name__=='__main__':
    for t in sys.argv[1:]: print(an(t))
