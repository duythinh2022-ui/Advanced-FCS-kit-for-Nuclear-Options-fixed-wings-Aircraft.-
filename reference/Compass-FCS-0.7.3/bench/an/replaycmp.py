#!/usr/bin/env python3
# 3 Nov 2026: compare a bench replay (FCS telemetry rows, game columns) with the game log over the same window.
# Usage: replaycmp.py <game log.csv> <replay.csv> [more replays...]
# Per signal: bench / game mean and std, RMS difference; lateral chatter: mean |change per tick| of pCmd / rCmd, roll-rate std,
# sideslip mean / peak; released-follow engagements where the FCS logs relFollow.
import sys, numpy as np, pandas as pd
g = pd.read_csv(sys.argv[1], low_memory=False)
SIG = ['alpha', 'beta', 'p', 'q', 'r', 'ias', 'phi', 'pCmd', 'rCmd', 'qCmd']
for f in sys.argv[2:]:
    b = pd.read_csv(f, low_memory=False)
    t0, t1 = b.t.min(), b.t.max()
    gw = g[(g.t >= t0) & (g.t <= t1)]
    gi = pd.DataFrame({c: np.interp(b.t, gw.t, gw[c]) for c in SIG if c in gw and c in b})
    print(f"== {f.split('/')[-1]}  {t0:.2f}-{t1:.2f} s, {len(b)} ticks")
    for c in gi.columns:
        dd = b[c] - gi[c]
        if c == 'phi': dd = (dd + 180.0) % 360.0 - 180.0   # bank angle wraps at +-180
        print(f"   {c:6s} bench {b[c].mean():7.2f} +-{b[c].std():6.2f} | game {gw[c].mean():7.2f} +-{gw[c].std():6.2f} | rms diff {np.sqrt((dd ** 2).mean()):6.2f}")
    def chat(x, tag):
        s = f"   {tag:5s} rCmd |d|/tick {x.rCmd.diff().abs().mean():5.2f}  pCmd |d|/tick {x.pCmd.diff().abs().mean():5.2f}  p std {x.p.std():5.2f}  beta mean {x.beta.mean():6.2f} peak {x.beta.abs().max():5.2f}"
        if 'relFollow' in x:
            on = x.relFollow != 0
            s += f"  follow engagements {int((on & ~on.shift(1, fill_value=False)).sum())} ({on.mean() * 100:.1f} % of ticks)"
        print(s)
    chat(b, 'bench'); chat(gw, 'game')
