#!/usr/bin/env python3
# score.py <tagA> <tagB> [...]: push-over metrics per replay set (pinned: the command; free: the flown response)
import sys, csv, numpy as np, pandas as pd
sys.path.insert(0, '/home/claude/w'); from hitch2 import dip; from hitch3 import rev
rows = list(csv.DictReader(open('/home/claude/w/hitch_all.csv')))
def pinm(d, t0):
    a = d.alpha_b.values; s = np.sign(a[np.argmax(np.abs(a))]) if len(a) else 1
    push = (d.t >= t0) & (np.abs(d.alpha_b) > 12) & (np.sign(d.alpha_b) * d.sp < -0.3)
    qd = -np.sign(d.alpha_b) * d.q_cmd
    def mdip(lo, hi):
        m = (d.t >= t0) & (np.abs(d.alpha_b) > lo) & (np.abs(d.alpha_b) <= hi) & (np.sign(d.alpha_b) * d.sp < -0.3)
        if m.sum() < 5: return 0.0
        x = (-np.sign(d.alpha_b) * d.q_cmd)[m].values
        pre = np.maximum.accumulate(x); post = np.maximum.accumulate(x[::-1])[::-1]
        return float(np.max(np.minimum(pre - x, post - x)))
    return dict(cdip=dip(d.t, d.q_cmd, d.alpha_b, d.sp, t0), cdE=mdip(30, 90), cdL=mdip(8, 30),
                cmean=float(qd[push].mean()) if push.any() else np.nan,
                pbeta=float(d.beta_b.abs().max()))
def freem(d, t0):
    w = d[(d.t >= t0)]; a = w.alpha_b.values; q = w.q_b.values * -np.sign(a[0] if len(a) else 1)
    # nose-down rate positive; rev() wants nose-down negative
    qn = -q
    early = rev(qn, np.abs(a) > 30); late = rev(qn, (np.abs(a) <= 30) & (np.abs(a) > 8))
    tv = w.tvc_b.values; m = np.abs(a) > 20; noz = 0.0
    if m.any():
        tvm = tv[m] * np.sign(a[0]); mn = np.minimum.accumulate(tvm); noz = float(np.max(np.where(mn <= -8, tvm - mn, 0)))
    tz = w.t[np.abs(w.alpha_b) < 10]
    def mdipf(col, lo, hi):
        m = (w.t >= t0) & (np.abs(w.alpha_b) > lo) & (np.abs(w.alpha_b) <= hi) & (np.sign(w.alpha_b) * w.sp < -0.3)
        if m.sum() < 5: return 0.0
        x = (-np.sign(w.alpha_b) * w[col])[m].values
        pre = np.maximum.accumulate(x); post = np.maximum.accumulate(x[::-1])[::-1]
        return float(np.max(np.minimum(pre - x, post - x)))
    return dict(fcd=mdipf('q_cmd', 8, 90), fqd=mdipf('q_b', 8, 90), early=early, late=late, noz=noz, fbeta=float(w.beta_b.abs().max()), t10=float(tz.iloc[0] - t0) if len(tz) else np.nan,
                pmax=float(w.p_b.abs().max()))
tags = sys.argv[1:]; res = {}
for tg in tags:
    out = []
    for r in rows:
        t0 = float(r['t']); k = f"{r['log']}_{r['t']}"
        try:
            dp = pd.read_csv(f'/home/claude/w/score/{tg}/{k}_pin.csv'); df = pd.read_csv(f'/home/claude/w/score/{tg}/{k}_free.csv')
        except Exception as e:
            continue
        x = dict(key=k, build=r['build']); x.update(pinm(dp, t0)); x.update(freem(df, t0)); out.append(x)
    res[tg] = pd.DataFrame(out).set_index('key')
cols = ['cdip', 'cdE', 'cdL', 'cmean', 'fcd', 'fqd', 'pbeta', 'early', 'late', 'noz', 'fbeta', 't10', 'pmax']
print('per-set (median / p90 / max).  pinned: cdip = two-sided dip of the nose-down COMMAND (deg/s), cmean = mean nose-down command while pushed, pbeta = |beta| peak.')
print('free: early/late = nose-down rate back-off above / below 30 deg AoA, noz = nozzle swing back, fbeta = |beta| peak, t10 = s to AoA<10, pmax = |p| peak')
for tg in tags:
    d = res[tg]
    print(f"{tg:10s} n={len(d)} " + "  ".join(f"{c} {d[c].median():5.1f}/{d[c].quantile(.9):5.1f}/{d[c].max():5.1f}" for c in cols))
if len(tags) > 1:
    a, b = res[tags[0]], res[tags[1]]; j = a.join(b, lsuffix='_a', rsuffix='_b', how='inner')
    print(f"\n{tags[1]} vs {tags[0]}: count better / worse (by > 1 unit), per metric")
    for c, lowbetter in [('cdip', 1), ('cdE', 1), ('cdL', 1), ('fcd', 1), ('fqd', 1), ('cmean', 0), ('pbeta', 1), ('early', 1), ('late', 1), ('noz', 1), ('fbeta', 1), ('t10', 1), ('pmax', 1)]:
        dd = (j[c + '_b'] - j[c + '_a']) * (1 if lowbetter else -1); th = 0.1 if c == 't10' else 1.0
        print(f"  {c:6s} better {int((dd < -th).sum()):3d}  worse {int((dd > th).sum()):3d}  median change {np.nanmedian(j[c + '_b'] - j[c + '_a']):+6.2f}")
    if '-v' in sys.argv[0:0] or True:
        pd.set_option('display.width', 250); pd.set_option('display.max_rows', 200)
        show = j[[f'{c}_{s}' for c in ['cdip', 'cmean', 'pbeta', 'early', 'late', 'noz', 'fbeta', 't10'] for s in 'ab']].round(1)
        show.to_csv(f'/home/claude/w/score/cmp_{tags[0]}_{tags[1]}.csv')
