# loadrev.py <dir> : tables for loadrev.sh's runs (roll reversals with an asymmetric store).
# (4.1.2 added: the new roll's peak acceleration, when after the crossing, against the brake; the ailerons' and flaps' differential
# while braking and at its largest in the new roll. 4.1.1's table stopped 0.1 s after the crossing and missed a rise that peaked at 0.2 s.)
# Single reversals: the trajectory's acceleration while braking, in the 0.1 s after the zero crossing, the largest rise in 3 ticks
# at the crossing and anywhere up to 95 % of the new rate, the stabs' differential travel in the 0.1 s after the crossing,
# sideslip, the roll behind its command, stick flip -> p = 0 / 60 % / 90 % of the new rate, a fall-back of the roll rate on the way.
# Roll spam: means and worst cases over the runs.
import csv, sys, glob, os
import numpy as np
P = sys.argv[1]
def load(f):
    r = csv.reader(open(f)); h = next(r); rows = [x for x in r if len(x) == len(h)]
    return {c: np.array([float(x[i]) if x[i] not in ("", "NaN") else np.nan for x in rows]) for i, c in enumerate(h)}
def single(f):
    d = load(f); t = d["t"]; sr = d["sr"]; pS = d["pS"]; p = d["p"]; dt = np.median(np.diff(t))
    cand = [i for i in range(1, len(sr)) if sr[i-1]*sr[i] <= 0 and abs(sr[i-1]) > 0.1 and abs(p[i]) > 10 and abs(sr[min(i+20, len(sr)-1)]) > 0.8]
    if not cand: return None
    i0 = cand[0]; s = np.sign(sr[i0+20]); z = i0
    while z < len(pS)-2 and s*pS[z] < 0: z += 1
    if z >= len(pS)-80: return None
    acc = s*np.gradient(pS, dt); brake = np.median(acc[min(i0+10, z-6):z-1]); tgt = np.median(s*pS[z+60:z+85]); e = z
    while e < len(pS)-2 and s*pS[e] < 0.95*tgt: e += 1
    dst = 0.5*(d["stabLcmd"]-d["stabRcmd"]); zz = z
    while zz < len(p)-2 and s*p[zz] < 0: zz += 1
    def tto(fr):
        k = np.argmax((s*p > fr*tgt) & (np.arange(len(p)) > zz)); return (t[k]-t[i0]) if k > 0 else np.nan
    k60 = np.argmax((s*p > 0.6*tgt) & (np.arange(len(p)) > zz)); k60 = k60 if k60 > 0 else zz
    sp3 = np.convolve(s*p, np.ones(3)/3, mode="same"); hb = 0.0; run = sp3[zz]
    for k in range(zz, k60+1):
        run = max(run, sp3[k]); hb = max(hb, run-sp3[k])
    w = slice(i0, min(e+30, len(t)))
    # 4.1.2: the new roll's peak acceleration (zero crossing -> 95 % of the new rate), when, and against the brake; the roll
    # surfaces' differential (ailerons, flaps: positions) while braking (median) and at its largest in the new roll
    kp = z+int(np.argmax(acc[z:max(e, z+2)])); ad = 0.5*(d["ailR"]-d["ailL"]); fd = 0.5*(d["flapR"]-d["flapL"]); b0 = min(i0+10, z-6)
    sg = np.sign(ad[z:max(e, z+2)][np.argmax(np.abs(ad[z:max(e, z+2)]))]) or 1.0
    X = (acc[kp], t[kp]-t[z], acc[kp]/max(brake, 1.0), np.median(sg*ad[b0:z]), np.max(sg*ad[z:max(e, z+2)]), np.median(sg*fd[b0:z]), np.max(sg*fd[z:max(e, z+2)]))
    return X + (d["ias"][z]/3.6, d["alpha"][z], np.nanmax(np.abs(d["asymDy"])), brake, acc[z+1:z+7].max(), max(acc[k+3]-acc[k] for k in range(z-6, z+6)),
            max(acc[k+3]-acc[k] for k in range(z-6, max(e-3, z+7))), abs(dst[z+6]-dst[z-1]), np.nanmax(np.abs(d["beta"][w])), np.max(np.abs(pS[z:e]-p[z:e])) if e > z else np.nan,
            t[zz]-t[i0], tto(0.6), tto(0.9), hb)
print("SINGLE REVERSALS (store on WingRear_L)")
print("%-16s %4s %5s %5s | %5s %6s %5s %6s %6s | %5s | %5s %5s | %5s %5s %5s | %5s | %5s %5s %5s | %11s | %11s" % ("case", "ias", "AoA", "dy", "brake", "a0.1s", "ratio", "step0", "stepUp", "dStab", "beta", "lag", "t p=0", "t60%", "t90%", "hitch", "peak", "at", "pk/br", "ail br->pk", "flap br->pk"))
for f in sorted(glob.glob(P+"/s[1234]_*.csv")):
    m = single(f); n = os.path.basename(f)[:-4]
    if m is None: print("%-16s (no reversal found)" % n); continue
    X = m[:7]; m = m[7:]
    print("%-16s %4.0f %5.1f %5.3f | %5.0f %6.0f %5.2f %6.0f %6.0f | %5.1f | %5.1f %5.1f | %5.2f %5.2f %5.2f | %5.1f | %5.0f %5.2f %5.2f | %5.1f %5.1f | %5.1f %5.1f" % ((n, m[0], m[1], m[2], m[3], m[4], m[4]/max(m[3], 1), m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13]) + X))
def spam(f):
    d = load(f); t = d["t"]; pS = d["pS"]; p = d["p"]; sr = d["sr"]; tg = d["pSTgt"]; dt = np.median(np.diff(t)); acc = np.gradient(pS, dt)
    i0 = np.argmax(np.abs(sr) > 0.5); w = np.zeros(len(t), bool); w[i0:] = True; b = np.abs(d["beta"])
    zs = [i for i in range(i0+8, len(pS)-12) if pS[i-1]*pS[i] < 0 and abs(pS[i+8]) > 8 and abs(pS[i-8]) > 8]
    steps = []
    for z in zs:
        a = np.sign(pS[z+8])*acc; steps.append(max(a[k+3]-a[k] for k in range(z-6, z+6)))
    held = np.ones(len(t), bool)
    for k in range(1, 16): held[k:] &= (np.abs(sr[k:]) > 0.95) & (np.sign(sr[k:]) == np.sign(sr[:-k]))
    ret = 0; i = i0+16
    while i < len(t)-13:
        if held[i] and held[i-12] and abs(tg[i-12]) > 25 and np.sign(tg[i-12]) == np.sign(sr[i]) and sr[i]*tg[i] < 0.6*abs(tg[i-12]): ret += 1; i += 40
        else: i += 1
    return (max(steps) if steps else 0.0, np.nanmax(b[w]), np.nanpercentile(b[w], 95), float((b[w] > 5).sum()*dt), np.nanmax(np.abs(pS-p)[w]), float(np.sqrt(np.nanmean(((pS-p)[w])**2))), ret,
            float(np.nansum(np.abs(p[w]))*dt), np.nanmax((d["alpha"]-d["asymCapPos"])[w]), int(np.isnan(p).sum()))
R = [spam(f) for f in sorted(glob.glob(P+"/spam_*.csv"))]
if R:
    A = np.array(R)
    print("\nROLL SPAM, %d runs (1100 / 1500 / 2000 kg, 480 / 520 / 600 km/h, three flip timings, both directions), full aft held: mean / worst" % len(R))
    for j, lab in enumerate(("largest acceleration step at a zero crossing (deg/s2 in 3 ticks)", "sideslip max (deg)", "sideslip 95th percentile (deg)", "seconds above 5 deg sideslip", "roll behind its command, max (deg/s)", "roll behind its command, rms (deg/s)", "governor retreats of the rate target, stick held (total)", "bank angle travelled (deg)", "AoA above the asymmetric cap, max (deg)")):
        if j == 6: print("  %-66s %7.0f" % (lab, A[:, j].sum()))
        elif j == 7: print("  %-66s %7.1f / %.1f" % (lab, A[:, j].mean(), A[:, j].min()))
        else: print("  %-66s %7.2f / %.2f" % (lab, np.nanmean(A[:, j]), np.nanmax(A[:, j])))
    print("  NaN ticks: %d" % int(A[:, 9].sum()))
