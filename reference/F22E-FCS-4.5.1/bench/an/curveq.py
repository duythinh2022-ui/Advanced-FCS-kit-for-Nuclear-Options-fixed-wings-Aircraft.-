"""Response-curve quality for every stick event (3.1.6; Tom 6 Nov: "instant pitch response from stick input, an actual smooth
pitch rate curve, no spike no hitch no sudden slow down, no chatter, no hesitation ... do that from now on not just for pushovers").

Works on the game's FCS CSV, the bench's 'one' CSV and the bench's replay CSV (columns picked automatically).

Axes: pitch (stick sp, body pitch rate q, command qCmd) and roll (stick sr, velocity-vector roll pS = p cos a + r sin a,
command pCmd). Events:

  GO   the stick crosses 50 % of travel (or from one side to the other: a reversal) -> the response toward the stick
       onset   s        until the response has moved 5 deg/s toward the stick
       t50     s        until half of the window's peak
       rise    deg/s^2  mean build from onset to 90 % of peak
       dip     deg/s    worst drop below the running maximum before the window ends (hitch / sudden slow-down)
       plateau s        longest stall (< 15 deg/s^2 of build) before 90 % of peak (hesitation)
       peaks   n        local maxima with >= 3 deg/s prominence (spike-then-sag, wobble)
       cmdDip  deg/s    worst retreat of the command toward zero while the stick holds (the law changing its mind)
  STOP the stick returns under 10 % -> the response back to zero
       tStop   s        until |response| < 10 % of where it was
       over    deg/s    overshoot past zero the other way
       rebound n        zero crossings after the first (oscillation)
  both: tvcRev / stabRev = nozzle / stab direction reversals of >= 4 deg in the window (chatter)
  GO, over the whole hold (to the stick letting go, 3 s; past the AoA target too):
       spk      n   command ticks off their 5-tick median by > 4 deg/s (single-tick command spikes)
       wob      n   response turn-arounds of >= 2 deg/s beyond the one capture (wobble)
       tvcRevH  n   nozzle reversals >= 4 deg (gate: at most 1, the capture's brake)

Window: GO ends when the stick lets go / changes side, after 3 s, or (pitch) at the AoA target of a push / pull through zero;
STOP ends 2 s after the stick returns or at the next stick input.

Usage: python3 curveq.py <csv> [<csv> ...] [axis=pitch|roll|both]  (prints one row per event)
       import curveq; curveq.events(df, axis)  -> list of dicts
"""
import sys
import numpy as np
import pandas as pd


def _col(d, *names):
    for n in names:
        if n in d:
            return n
    return None


def _rev(x, thr):
    n = 0; d = 0; ext = x[0]
    for v in x[1:]:
        if d == 0:
            if abs(v - ext) >= thr:
                d = 1 if v > ext else -1; ext = v
            continue
        if d == 1:
            if v > ext: ext = v
            elif ext - v >= thr: n += 1; d = -1; ext = v
        else:
            if v < ext: ext = v
            elif v - ext >= thr: n += 1; d = 1; ext = v
    return n


def _series(d, axis):
    a = d[_col(d, 'alpha', 'alpha_b')].values
    if axis == 'pitch':
        st = d['sp'].values; y = d[_col(d, 'q', 'q_b')].values
        c = _col(d, 'qCmd', 'q_cmd'); cmd = d[c].values if c else None
    else:
        st = d['sr'].values
        p = d[_col(d, 'p', 'p_b')].values; r = d[_col(d, 'r', 'r_b')].values
        y = p * np.cos(np.radians(a)) + r * np.sin(np.radians(a))
        c = _col(d, 'pCmd'); cmd = d[c].values if c else None
    tv = _col(d, 'tvcNow', 'tvc', 'tvc_b'); sb = _col(d, 'Elevator_L_now', 'stabL', 'stabL_b')
    return d['t'].values, st, y, cmd, a, (d[tv].values if tv else None), (d[sb].values if sb else None)


def _go(t, st, y, cmd, a, tv, sb, i0, axis):
    s = 1.0 if st[i0] > 0 else -1.0
    end = len(t) - 1
    for i in range(i0 + 1, len(t)):
        if t[i] - t[i0] > 3.0 or abs(st[i]) < 0.3 or np.sign(st[i]) != s:
            end = i; break
        if axis == 'pitch' and ((s < 0 and a[i0] > 15 and a[i] < 10.0) or (s > 0 and a[i0] < -15 and a[i] > 0.0)):
            end = i; break
    seg = slice(i0, end + 1); ts = t[seg] - t[i0]; qq = s * y[seg]
    if len(qq) < 6:
        return None
    q0 = qq[0]
    on = int(np.argmax(qq - q0 >= 5.0)) if (qq - q0 >= 5.0).any() else -1
    pk = qq.max()
    t50 = ts[int(np.argmax(qq >= q0 + 0.5 * (pk - q0)))]
    i90 = int(np.argmax(qq >= q0 + 0.9 * (pk - q0)))
    rise = (qq[i90] - qq[on]) / max(1e-3, ts[i90] - ts[on]) if on >= 0 and i90 > on else np.nan
    dip = float((np.maximum.accumulate(qq) - qq).max())
    dq = np.convolve(np.gradient(qq, ts), np.ones(3) / 3, mode='same')
    best = cur = 0.0
    for j in range(max(on, 0), max(on, 0, i90)):
        if dq[j] < 15.0: cur += ts[j + 1] - ts[j]; best = max(best, cur)
        else: cur = 0.0
    peaks = 0
    for j in range(1, len(qq) - 1):
        if qq[j] >= qq[j - 1] and qq[j] > qq[j + 1]:
            nxt = qq[j + 1:]
            exc = int(np.argmax(nxt > qq[j])) if (nxt > qq[j]).any() else len(nxt)
            if exc > 0 and qq[j] - nxt[:exc].min() >= 3.0:
                peaks += 1
    cd = float((np.maximum.accumulate(s * cmd[seg]) - s * cmd[seg]).max()) if cmd is not None else np.nan
    # the whole hold (to the stick letting go / changing side / 3 s), past the AoA target: spikes and wobble anywhere count
    hend = len(t) - 1
    for i in range(i0 + 1, len(t)):
        if t[i] - t[i0] > 3.0 or abs(st[i]) < 0.3 or np.sign(st[i]) != s:
            hend = i; break
    hs = slice(i0, hend + 1)
    spk = _spikes(cmd[hs], 4.0) if cmd is not None else -1
    wob = max(0, _extrema(s * y[hs], 2.0) - 1)   # one turn-around (the capture) is the curve; more is wobble
    return dict(ev='GO', t0=round(t[i0], 2), a0=round(a[i0], 1), y0=round(q0, 1), dur=round(ts[-1], 2),
                onset=round(ts[on], 2) if on >= 0 else np.nan, t50=round(t50, 2),
                rise=round(rise) if rise == rise else np.nan, peak=round(pk, 1), dip=round(dip, 1),
                plateau=round(best, 2), peaks=peaks, cmdDip=round(cd, 1),
                tvcRev=_rev(tv[seg], 4.0) if tv is not None else -1, stabRev=_rev(sb[seg], 4.0) if sb is not None else -1,
                spk=spk, wob=wob, tvcRevH=_rev(tv[hs], 4.0) if tv is not None else -1)


def _spikes(x, thr):
    """ticks where the command leaves its 5-tick median by more than thr (one- or two-tick spikes: chatter)"""
    n = 0
    for i in range(2, len(x) - 2):
        if abs(x[i] - np.median(x[i - 2:i + 3])) > thr:
            n += 1
    return n


def _extrema(x, thr):
    """turning points of x with at least thr of swing between them"""
    return _rev(x, thr)


def _stop(t, st, y, a, tv, sb, i0):
    y0 = y[i0]
    if abs(y0) < 5.0:
        return None
    end = len(t) - 1
    for i in range(i0 + 1, len(t)):
        if t[i] - t[i0] > 2.0 or abs(st[i]) >= 0.2:
            end = i; break
    seg = slice(i0, end + 1); ts = t[seg] - t[i0]; yy = np.sign(y0) * y[seg]
    k = int(np.argmax(yy < 0.1 * abs(y0))) if (yy < 0.1 * abs(y0)).any() else -1
    over = float(max(0.0, -yy.min()))
    zc = np.where(np.diff(np.sign(yy)) != 0)[0]
    return dict(ev='STOP', t0=round(t[i0], 2), a0=round(a[i0], 1), y0=round(y0, 1), dur=round(ts[-1], 2),
                tStop=round(ts[k], 2) if k >= 0 else np.nan, over=round(over, 1), rebound=max(0, len(zc) - 1),
                tvcRev=_rev(tv[seg], 4.0) if tv is not None else -1, stabRev=_rev(sb[seg], 4.0) if sb is not None else -1)


def events(d, axis='pitch'):
    t, st, y, cmd, a, tv, sb = _series(d, axis)
    out = []
    for i in range(1, len(t)):
        crossed = (abs(st[i]) >= 0.5 and abs(st[i - 1]) < 0.5) or (abs(st[i]) >= 0.5 and np.sign(st[i]) != np.sign(st[i - 1]) and abs(st[i - 1]) >= 0.5)
        if crossed:
            r = _go(t, st, y, cmd, a, tv, sb, i, axis)
            if r: r['axis'] = axis; out.append(r)
        if abs(st[i]) < 0.1 and abs(st[i - 1]) >= 0.1:
            r = _stop(t, st, y, a, tv, sb, i)
            if r: r['axis'] = axis; out.append(r)
    return out


GATE = dict(onset=0.15, dip=3.0, plateau=0.15, peaks=0, cmdDip=3.0, over=None, rebound=1, spk=0, wob=0, tvcRevH=1)


def flags(r):
    """the gate: which smoothness rules an event breaks"""
    f = []
    if r['ev'] == 'GO':
        if r['onset'] == r['onset'] and r['onset'] > GATE['onset']: f.append('onset')
        if r['dip'] > GATE['dip']: f.append('dip')
        if r['plateau'] > GATE['plateau']: f.append('plateau')
        if r['peaks'] > GATE['peaks']: f.append('peaks')
        if r['cmdDip'] == r['cmdDip'] and r['cmdDip'] > GATE['cmdDip']: f.append('cmdDip')
        if r.get('spk', 0) > GATE['spk']: f.append('spk')
        if r.get('wob', 0) > GATE['wob']: f.append('wob')
        if r.get('tvcRevH', 0) > GATE['tvcRevH']: f.append('tvcRev')
    else:
        if r['rebound'] > GATE['rebound']: f.append('rebound')
    return ','.join(f)


if __name__ == '__main__':
    axes = ['pitch', 'roll']
    files = []
    for x in sys.argv[1:]:
        if x.startswith('axis='):
            axes = ['pitch', 'roll'] if x[5:] == 'both' else [x[5:]]
        else:
            files.append(x)
    rows = []
    for f in files:
        d = pd.read_csv(f, low_memory=False)
        for ax in axes:
            for r in events(d, ax):
                r['file'] = f.split('/')[-1]; r['flags'] = flags(r); rows.append(r)
    pd.set_option('display.width', 280); pd.set_option('display.max_rows', 2000)
    print(pd.DataFrame(rows).to_string(index=False))
