#!/usr/bin/env python3
"""Rate card: achieved and limit rates binned by (AoA, IAS) cell, for comparing a flown log
against the previous build's log CELL BY CELL instead of against a remembered peak.

Why this exists: the rate ceilings are proportional to the MEASURED authority, which scales with
dynamic pressure. On the Raptor rMax = Lerp(MaxYawRate, clamp(boost(ias) * YawStopFactor * aYaw,
20*boost, MaxYawRate), clamp01((|AoA|-15)/10)) - so the same unchanged law gives rMax 27 deg/s at
50 m/s IAS and 44 at 85. A peak rate remembered without its flight condition cannot distinguish a
regression from a slower pass: on 1 Nov 2026 "Raptor yaw was 43, now it's 30" was entirely the
difference between flying the high-AoA segment at 75-95 m/s and at 45-60 m/s, with aYaw and rMax
at matched cells identical or better. Always compare cells, never peaks.

usage: ratecard.py <log.csv> [<log2.csv> ...]
       ratecard.py --roll <log.csv> ...      # roll card instead of yaw
Columns per cell: mean achieved |rate| / mean limit [n samples]. Cells with < 8 samples are blank.
"""
import csv, sys, math

AOA_BANDS = ((0, 15), (15, 25), (25, 40), (40, 55), (55, 70))
IAS_BANDS = ((30, 45), (45, 60), (60, 75), (75, 95), (95, 130))


def load(p):
    rows = []
    with open(p) as f:
        for r in csv.DictReader(f):
            try:
                rows.append({k: (float(v) if v not in (None, '', 'NaN') else float('nan'))
                             for k, v in r.items() if k})
            except Exception:
                pass
    return rows


def g(r, k):
    return r.get(k, float('nan'))


def main():
    args = sys.argv[1:]
    roll = '--roll' in args
    allstick = '--allstick' in args   # include samples with the lateral stick NOT asking
    paths = [a for a in args if not a.startswith('--')]
    if not paths:
        print(__doc__)
        return
    # achieved, limit, authority
    ach, lim, auth = (('p', 'pSmax', 'aRoll') if roll else ('r', 'rMax', 'aYaw'))
    logs = [(p.split('/')[-1][:24], load(p)) for p in paths]
    gate = "ALL samples" if allstick else "lateral stick |sr| > 0.9 only"
    print(f"{'ROLL' if roll else 'YAW'} rate card - mean |{ach}| | commanded |pSTarget| / cap {lim} = UTIL  [n]")
    print(f"  samples: {gate}.  UTIL is only meaningful where the stick is ASKING - with the stick")
    print("  centred pSTarget is ~0 and UTIL reads 0 % for no reason. --allstick removes the gate.")
    print("  UTIL is command / cap. A low UTIL with an unchanged cap means a device is holding the")
    print("  COMMAND below the cap - diffing caps alone will not see it. See the note at the end.")
    print(f"{'cell':22s}" + "".join(f"{n:>34s}" for n, _ in logs))
    for a0, a1 in AOA_BANDS:
        for i0, i1 in IAS_BANDS:
            line = f"a{a0:>2}-{a1:<2} ias{i0:>3}-{i1:<3}  "
            any_ = False
            for _, rows in logs:
                s = [r for r in rows if a0 <= abs(g(r, 'alpha')) < a1 and i0 <= g(r, 'ias') < i1
                     and (allstick or abs(g(r, 'sr')) > 0.9)]
                if len(s) >= 8:
                    f = lambda k: sum(abs(g(r, k)) for r in s) / len(s)
                    a, l, q, c = f(ach), f(lim), f(auth), f('pSTarget')
                    line += f"{a:6.1f} |{c:6.1f}/{l:6.1f}={100 * c / max(1e-3, l):3.0f}% ({q:4.0f})[{len(s):4d}]"
                    any_ = True
                else:
                    line += f"{'-':>34s}"
            if any_:
                print(line)
    print("""
A cell that is lower in the new log is a candidate regression ONLY if the same cell is populated
in both. An empty cell in the old log means that condition was never flown then.

READ THE WHOLE CHAIN, NOT JUST THE CAP. 1 Nov 2026: the Raptor's falling-leaf yaw rate dropped
29.9 -> 40.8 deg/s between 2.6.7 and 2.4.16 with rMax and pSmax IDENTICAL in both (47.5 / 46.0).
Diffing the caps said "no regression"; the truth was that the pace window was holding the
COMMAND to 70 % of the cap (|pSTarget| 33.4 against pSmax 47.5, paceHold 13.9 - the whole gap),
for 0.5 deg of sideslip against 0.5 deg. The chain is stick -> command (pSTarget) -> cap (pSmax /
rMax) -> achieved (p / r); a new governor hides in the command-to-cap gap. Always check UTIL."""
          )


if __name__ == '__main__':
    main()
