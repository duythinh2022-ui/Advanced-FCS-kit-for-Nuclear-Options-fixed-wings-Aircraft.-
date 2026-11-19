# negload.py <dir> : table for negload.sh. Per run, over the time the stick is forward: lowest AoA, the negative limit's range,
# AoA past the limit, sideslip max / seconds over 5 deg, flight-path roll rate reached each way against its target (median of the
# last 0.5 s of each held roll input, sign = with the stick), roll the wrong way (deg/s, worst), largest fall-back of the AoA
# toward zero while the push is held (a limit that moves in on the pilot), departure.
import csv, sys, glob, os
import numpy as np
P = sys.argv[1]
def load(f):
    r = csv.reader(open(f)); h = next(r); rows = [x for x in r if len(x) == len(h)]
    return {c: np.array([float(x[i]) if x[i] not in ("", "NaN") else np.nan for x in rows]) for i, c in enumerate(h)}
print("%-22s %4s %5s | %6s %13s %5s %6s | %5s %5s | %11s %11s %6s | %s" % ("case", "ias", "dy", "aMin", "negLim", "past", "aBack", "beta", "s>5", "roll1 / tgt", "roll2 / tgt", "wrong", ""))
tot = []
for f in sorted(glob.glob(P + "/*.csv")):
    d = load(f); n = os.path.basename(f)[:-4]; t = d["t"]; dt = np.median(np.diff(t))
    w = d["sp"] < -0.9
    if w.sum() < 30: print("%-22s (no push)" % n); continue
    a = d["alpha"]; b = np.abs(d["beta"]); lim = d["asymCapNeg"]; lim = np.where(lim < -900, np.nan, lim); sr = d["sr"]
    ca, sa = np.cos(np.radians(a)), np.sin(np.radians(a)); pv = d["p"] * ca + d["r"] * sa; tg = d["pSTarget"]
    past = np.nanmax(np.where(w, lim - a, np.nan))
    # fall-back: after the AoA first comes within 2 deg of its lowest, how far it comes back toward zero with the push held
    k0 = np.argmax(w); am = np.minimum.accumulate(np.where(w, a, 99)); back = 0.0
    for k in range(k0, len(a)):
        if w[k]: back = max(back, a[k] - am[k])
    segs = []; k = 1
    while k < len(sr):
        if abs(sr[k]) > 0.9 and w[k]:
            j = k
            while j < len(sr) - 1 and abs(sr[j + 1]) > 0.9 and np.sign(sr[j + 1]) == np.sign(sr[k]): j += 1
            if j - k > 60: segs.append((k, j))
            k = j + 1
        else: k += 1
    rr = []; wrong = 0.0
    for (k, j) in segs[:2]:
        s = np.sign(sr[k]); e = slice(max(k, j - 30), j + 1); rr.append((np.median(s * pv[e]), np.median(s * tg[e])))
        wrong = max(wrong, float(np.max(-s * pv[k + 45:j + 1]))) if j - k > 45 else wrong
    while len(rr) < 2: rr.append((np.nan, np.nan))
    dep = "DEP" if (np.nanmax(b) > 25 or np.nanmin(a) < -65 or np.nanmax(a[w]) > 70) else ""
    print("%-22s %4.0f %5.3f | %6.1f %6.1f/%6.1f %5.1f %6.1f | %5.1f %5.2f | %5.0f /%4.0f %5.0f /%4.0f %6.0f | %s" % (n, d["ias"][k0] , np.nanmax(np.abs(d["asymDy"])), np.nanmin(a[w]), np.nanmin(lim[w]), np.nanmax(lim[w]), past, back, np.nanmax(b[w]), float((b[w] > 5).sum() * dt), rr[0][0], rr[0][1], rr[1][0], rr[1][1], wrong, dep))
    tot.append((np.nanmin(a[w]), past, back, np.nanmax(b[w]), float((b[w] > 5).sum() * dt), rr[0][0], rr[1][0], wrong, 1.0 if dep else 0.0))
T = np.array(tot)
print("\nALL %d runs: mean / worst" % len(T))
for i, (nm, wf) in enumerate([("lowest AoA (deg)", np.nanmin), ("AoA past the negative limit (deg)", np.nanmax), ("AoA fall-back with the push held (deg)", np.nanmax), ("sideslip max (deg)", np.nanmax), ("seconds above 5 deg sideslip", np.nanmax), ("roll rate reached, first input (deg/s)", np.nanmin), ("roll rate reached, reversal (deg/s)", np.nanmin), ("roll the wrong way, worst (deg/s)", np.nanmax)]):
    print("  %-44s %8.2f / %8.2f" % (nm, np.nanmean(T[:, i]), wf(T[:, i])))
print("  departures: %d" % int(T[:, 8].sum()))
