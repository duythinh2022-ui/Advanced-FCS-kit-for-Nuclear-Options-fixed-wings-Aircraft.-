#!/usr/bin/env python3
"""UTILISATION INVARIANT - the bench assertion that catches "a governor is quietly holding the command".

THE INVARIANT, in words:
    With the lateral stick asking full, and the sideslip comfortably inside the margin the governor
    itself is defending, the COMMANDED roll rate must sit at (or near) its own rate cap.

If a pacing device holds the command 30 % under a cap that the airframe is meeting, while the
sideslip sits at a fraction of the margin, that device is spending rate and buying nothing. That is a
defect whatever the airframe, and it needs NO reference build to detect - which is the point: it fires
on a brand-new aircraft's first bench run, before anyone has a "it used to be faster" to compare to.

WHY THIS EXISTS (1 Nov 2026). This failure mode has now cost rates three times - Revoker 0.3.0, Ifrit
0.6.0, Raptor 2.5.0-2.6.7 - and every single time it was found by Tom flying, never by the bench,
because the bench suite asserted SIDESLIP only. Worse, the first two diagnoses this session were
wrong because they compared rate CAPS, which were identical; the cut was upstream of the cap, in the
command. The chain is:

    stick  ->  command (pSTarget)  ->  cap (pSmax)  ->  achieved (p)

A cap diff sees only the third link. This check watches the second.

WHAT IT DOES NOT DO: it does not assert the achieved rate (that is the airframe's business, and a
jet that cannot meet its own cap is a different bug), and it does not fire where the cap itself is
low - a cap that honestly reflects authority is correct. It fires only on the command-to-cap gap.

usage: utilcheck.py <jet>                 # jet = f22 | ifr | rev, runs the sweep itself
       utilcheck.py --log <log.csv> ...   # or check flown logs / existing bench csvs
options: --min 0.85      utilisation floor (default 0.85)
         --slack 0.5     only assert where |beta| < slack x margin (default 0.5)
         --margin 8      the margin in deg when the log has no stopSlipMargin column (default 8)
"""
import csv, os, subprocess, sys

AOA_BANDS = ((0, 15), (15, 25), (25, 40), (40, 55), (55, 70))
IAS_BANDS = ((30, 45), (45, 60), (60, 75), (75, 95), (95, 130))

# (name, ias km/h, pitch profile, roll profile) - a full-lateral hold at each of several AoA
CASES = [
    ('level roll',        f'{v}', '0:0,12:0',        '0:0,1:0,1.05:1,12:1')   for v in (200, 300, 450, 650)
] + [
    ('pull then roll',    f'{v}', '0:0,0.05:0.5,12:0.5', '0:0,2:0,2.05:1,12:1') for v in (300, 450, 650)
] + [
    ('full aft then roll', f'{v}', '0:0,0.05:1,14:1', '0:0,3:0,3.05:1,14:1')   for v in (160, 300, 450)
]

BENCH = {
    'f22': dict(dir=os.environ.get('BENCH_DIR','/root/src274/F22E_FCS_2.7.4_src/bench'),
                data=os.environ.get('BENCH_DATA','/tmp/airframe_cal.json'),
                mode='one', ias='iasKmh', cmd='pSTgt',
                extra=['alt=1500', 'trimT=1', 'pull=0', 'hold=0', 'roll=0', 'post=12', 'throttle=1']),
    'ifr': dict(dir='/home/claude/work/ifr060/bench',
                data='/home/claude/work/ifr060/bench/extract/ifrit_airframe.json',
                mode='ifly', ias='ias', cmd='pSTarget', extra=['alt=1500', 'thr=1']),
    'rev': dict(dir='/home/claude/work/rev030/bench',
                data='/home/claude/work/rev030/bench/extract/rev_airframe.json',
                mode='ifly', ias='ias', cmd='pSTarget', extra=['alt=1500', 'thr=1']),
}


def load(p):
    rows = []
    with open(p) as f:
        for r in csv.DictReader(f):
            try:
                rows.append({k: (float(v) if v not in (None, '', 'NaN') else float('nan'))
                             for k, v in r.items() if k})
            except Exception:
                pass
    # The bench csv writes ias in km/h; the in-game FCS log writes m/s. Normalise to m/s so the
    # IAS bands mean the same thing for both. (This cost an hour on 1 Nov: the invariant silently
    # matched zero cells on bench output because every sample fell outside the m/s bands.)
    v = sorted(r.get('ias', 0.0) for r in rows if r.get('ias', 0.0) == r.get('ias', 0.0))
    if v and v[int(0.95 * (len(v) - 1))] > 170.0:
        for r in rows:
            if r.get('ias', 0.0) == r.get('ias', 0.0):
                r['ias'] = r['ias'] / 3.6
    return rows


def g(r, k, d=float('nan')):
    v = r.get(k, d)
    return v if v == v else d


def cmdkey(rows):
    for k in ('pSTarget', 'pSTgt'):
        if rows and k in rows[0]:
            return k
    return None


def check(rows, label, mn, slack, margin, out):
    ck = cmdkey(rows)
    if ck is None:
        out.append((label, None, 'no command column (pSTarget / pSTgt) - cannot check'))
        return
    for a0, a1 in AOA_BANDS:
        for i0, i1 in IAS_BANDS:
            s = [r for r in rows
                 if a0 <= abs(g(r, 'alpha')) < a1 and i0 <= g(r, 'ias') < i1
                 and abs(g(r, 'sr', 0)) > 0.9 and g(r, 'pSmax', 0) > 1
                 and abs(g(r, 'beta', 99)) < slack * g(r, 'stopSlipMargin', margin)]
            if len(s) < 25:
                continue
            c = sum(abs(g(r, ck)) for r in s) / len(s)
            l = sum(abs(g(r, 'pSmax')) for r in s) / len(s)
            u = c / max(1e-3, l)
            cell = f'a{a0}-{a1} ias{i0}-{i1}'
            bmax = max(abs(g(r, 'beta', 0)) for r in s)
            hold = sum(abs(g(r, 'paceHold', 0)) for r in s) / len(s)
            det = f'cmd {c:5.1f} / cap {l:5.1f} = {100 * u:3.0f}%  (paceHold {hold:4.1f}, |beta|max {bmax:3.1f}, n={len(s)})'
            out.append((f'{label}  {cell}', u >= mn, det))


def main():
    a = sys.argv[1:]
    mn = float(a[a.index('--min') + 1]) if '--min' in a else 0.85
    slack = float(a[a.index('--slack') + 1]) if '--slack' in a else 0.5
    margin = float(a[a.index('--margin') + 1]) if '--margin' in a else 8.0
    rest = [x for i, x in enumerate(a)
            if not x.startswith('--') and (i == 0 or a[i - 1] not in ('--min', '--slack', '--margin'))]
    out = []
    if '--log' in a:
        for p in rest:
            check(load(p), p.split('/')[-1][:26], mn, slack, margin, out)
    else:
        jet = rest[0] if rest else 'f22'
        b = BENCH[jet]
        env = dict(os.environ, BENCH_DATA=b['data'])
        tmp = os.environ.get('UTIL_TMP', '/tmp/claude-0/utilcheck')
        os.makedirs(tmp, exist_ok=True)
        allrows = []
        for i, (nm, ias, prof, rprof) in enumerate(CASES):
            csvp = f'{tmp}/{jet}_{i}.csv'
            cmd = ['dotnet', b['dir'] + '/bin/bench.dll', b['mode'], f"{b['ias']}={ias}"] + b['extra']
            cmd += [x for x in rest[1:] if '=' in x]  # 2.6.9: pass statics through (Name=value)
            cmd += ([f'prof={prof}', f'rprof={rprof}', f'csv={csvp}'] if b['mode'] == 'one'
                    else [f'pitch={prof}', f'roll={rprof}', f'fcslog={csvp}'])
            subprocess.run(cmd, env=env, cwd=b['dir'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if os.path.exists(csvp):
                allrows += load(csvp)
        check(allrows, jet, mn, slack, margin, out)

    bad = [o for o in out if o[1] is False]
    print(f"UTILISATION INVARIANT  (floor {100 * mn:.0f}% of cap, asserted only where |beta| < {slack:g} x margin)")
    for label, ok, det in out:
        tag = 'PASS' if ok else ('FAIL' if ok is False else 'SKIP')
        print(f"  [{tag}] {label:30s} {det}")
    if not out:
        print("  no cell had enough full-stick samples inside the margin - widen the cases")
    print(f"\n{len(bad)} FAIL / {len([o for o in out if o[1] is not None])} checked")
    if bad:
        print("A FAIL means the command is being held under a cap the airframe is meeting, with the")
        print("margin unused. Find the device (bisect the pacing statics) - do not raise the floor.")
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
