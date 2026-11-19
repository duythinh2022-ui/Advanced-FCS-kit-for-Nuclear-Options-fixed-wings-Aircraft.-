# Damage suite for the stock-jet benches (ifly mode): flight-log-like damage cases next to their undamaged twins.
# usage: BENCH_DIR=<bench> BENCH_DATA=<airframe json> AN_OUT=<out root> sdmgsuite.py <ifrit|revoker> <tag> [Name=value ...]
# Damage keys as in bench/DAMAGE.md (dmg / dmgAt / detachAt / detach); times from the profile start. Part names are the
# airframe's own. Read mechanisms against the twin, not degrees (sideslip calibration as in CALIBRATION.md).
import subprocess, os, sys, math, csv
from concurrent.futures import ThreadPoolExecutor
B = os.environ['BENCH_DIR']; DATA = os.environ['BENCH_DATA']; S = os.environ.get('AN_OUT', '/tmp/anout')
jet, tag = sys.argv[1], sys.argv[2]; sets = sys.argv[3:]; out = f'{S}/sdmg/{jet}/{tag}'; os.makedirs(out, exist_ok=True)
P_HOLD = '0:0,0.05:1,9:1'
LEAF_STOP = ('0:0,2:0,2.05:1,5:1,5.05:0,9:0', 5.0, 'stop')
LEAF_REV = ('0:0,2:0,2.05:1,5:1,5.05:-1,8:-1,8.05:0', 5.0, 'rev')
PUSH_P = '0:0,0.05:1,4:1,4.05:-1,7:-1,7.05:0'
PO_REL = ('0:0,1.5:0,1.55:1,4:1,4.05:0,8:0', 4.0, 'push, roll released')
PO_HELD = ('0:0,1.5:0,1.55:1,7:1,7.05:0', 4.0, 'push, roll held')
JETS = {
    # Ifrit: yaw from the ruddervators (elevator_L/R) and roll TVC; no rudder
    'ifrit': dict(yaw=[('one ruddervator', ['detach=elevator_L']), ('both ruddervators', ['detach=elevator_L+elevator_R'])],
                  roll=('R flaperon+aileron', 'detach=flap_R+aileron_R'), wing=('L wing 20 HP', 'dmg=wing2_L:20+wingtip_L:20+wing1_L:20'),
                  eng=('L intake off @3s', 'detachAt=3:intake_L'), midrun=('ruddervators 5 HP mid-pull', 'dmgAt=0.5:elevator_L:5+elevator_R:5')),
    # Revoker: one rudder between two fins (the rudder also hinges to the fuselage)
    'revoker': dict(yaw=[('fins+rudder gone', ['detach=vstab1+vstab2+rudder']), ('rudder gone', ['detach=rudder'])],
                    roll=('R aileron', 'detach=aileron_R'), wing=('L wing 20 HP', 'dmg=wing2_L:20+wingtip_L:20+wing1a_L:20+wing1b_L:20'),
                    eng=('R intake off @3s (engine)', 'detachAt=3:intake_R'), midrun=('fins 2 HP mid-pull', 'dmgAt=0.5:vstab1:2+vstab2:2+rudder:2')),
}
J = JETS[jet]
CASES = []   # (config, name, ias, pitch, roll, t_event, kind, keys)
for cfg, keys in J['yaw'] + [('clean', [])]:
    for ias in (296, 370):
        CASES.append((cfg, f'leaf stop {ias}', ias, P_HOLD, *LEAF_STOP, keys))
        CASES.append((cfg, f'leaf rev {ias}', ias, P_HOLD, *LEAF_REV, keys))
    for ias in (560, 600):
        CASES.append((cfg, f'pushover released {ias}', ias, PUSH_P, *PO_REL, keys))
        CASES.append((cfg, f'pushover HELD {ias}', ias, PUSH_P, *PO_HELD, keys))
for cfg, keys in ((J['midrun'][0], [J['midrun'][1]]), ('clean', [])):
    CASES.append((cfg, 'rolling pull 900', 900, '0:0,0.05:1,8:1', '0:0,1:0,1.05:1,3:1,3.05:0,8:0', 3.0, 'stop', keys))
for cfg, keys in ((J['roll'][0], [J['roll'][1]]), ('clean', [])):
    CASES.append((cfg, 'roll rev 890', 890, '0:0,0.05:0.4,6:0.4', '0:0,1:0,1.05:1,2.5:1,2.55:-1,4:-1,4.05:0,6:0', 2.5, 'rev', keys))
for cfg, keys in ((J['wing'][0], [J['wing'][1]]), ('clean', [])):
    CASES.append((cfg, 'roll rev 600', 600, '0:0,6:0', '0:0,0.5:0,0.55:1,1.5:1,1.55:-1,2.5:-1,2.55:0,6:0', 1.5, 'rev', keys))
for cfg, keys in ((J['eng'][0], [J['eng'][1]]), ('clean', [])):
    CASES.append((cfg, 'held max AoA 300 + engine', 300, P_HOLD, '0:0,6:0', 3.0, 'engine', keys))

def run(i):
    g, name, ias, pitch, roll, tev, kind, keys = CASES[i]
    f = f'{out}/c{i}.csv'; fd = f'{out}/c{i}_d.csv'
    if not os.path.exists(f):
        cmd = ['dotnet', B + '/bin/bench.dll', 'ifly', f'ias={ias}', 'alt=1500', 'thr=1', 'T=9', f'pitch={pitch}', f'roll={roll}',
               f'csv={f}', f'dmgCsv={fd}'] + keys + sets
        r = subprocess.run(cmd, cwd=B, env=dict(os.environ, BENCH_DATA=DATA), capture_output=True, text=True)
        open(f + '.log', 'w').write(r.stdout + r.stderr)
    return f, fd

def card(i, f, fd):
    g, name, ias, pitch, roll, tev, kind, keys = CASES[i]
    if not os.path.exists(f): return dict(group=g, case=name, err=open(f + '.log').read().strip().splitlines()[-1][:100] if os.path.exists(f + '.log') else 'no output')
    R = list(csv.DictReader(open(f)))
    num = lambda r, k: float(r[k])
    vr = lambda r: num(r, 'p') * math.cos(math.radians(num(r, 'alpha'))) + num(r, 'r') * math.sin(math.radians(num(r, 'alpha')))
    W = [r for r in R if num(r, 't') >= 0.0]; after = [r for r in W if num(r, 't') >= tev]
    if not W: return dict(group=g, case=name, err='empty log')
    pk = max(W, key=lambda r: abs(num(r, 'beta'))); pk2 = max(after, key=lambda r: abs(num(r, 'beta'))) if after else pk
    dep = any(abs(num(r, 'beta')) > 30 or num(r, 'alpha') > 80 or num(r, 'alpha') < -70 for r in W)
    tz = float('nan')
    if kind in ('stop', 'rev') and after:
        v0 = vr(after[0])
        for r in after:
            v = vr(r)
            if (kind == 'stop' and abs(v) < 3.0) or (kind == 'rev' and v0 != 0 and v * math.copysign(1, v0) < -10.0): tz = num(r, 't') - tev; break
    ev = ''
    if os.path.exists(fd):
        D = list(csv.DictReader(open(fd)))
        ev = ' | '.join(d['events'].strip('"') for d in D if 'detached' in d['events'] or 'killed' in d['events'] or 'broke' in d['events'])[:110]
    return dict(group=g, case=name, aMax=max(num(r, 'alpha') for r in W), aMin=min((num(r, 'alpha') for r in after), default=float('nan')),
                vmax=max((abs(vr(r)) for r in W if num(r, 't') < tev), default=0.0), bPk=abs(num(pk, 'beta')), tPk=num(pk, 't'),
                bAfter=abs(num(pk2, 'beta')), tz=tz, dep=dep, ev=ev)

with ThreadPoolExecutor(4) as ex: files = list(ex.map(run, range(len(CASES))))
rows = [card(i, f, fd) for i, (f, fd) in enumerate(files)]
print(f'== {jet} damage suite {tag} {" ".join(sets)}   (|b| deg; vroll = p cos a + r sin a; t from profile start; aMin after the event; tz = s to stop / reversal, nan = never)')
print(f'{"case":26s} {"config":26s} {"aMax":>5} {"aMin":>6} {"vrMax":>5} {"|b|pk":>6} {"@t":>5} {"|b|aft":>6} {"tz":>5}  dep  events')
for r in rows:
    if 'err' in r: print(f'{r["case"]:26s} {r["group"]:26s}  ERROR {r["err"]}'); continue
    print(f'{r["case"]:26s} {r["group"]:26s} {r["aMax"]:5.1f} {r["aMin"]:6.1f} {r["vmax"]:5.0f} {r["bPk"]:6.1f} {r["tPk"]:5.2f} {r["bAfter"]:6.1f} {r["tz"]:5.2f}  {"DEP" if r["dep"] else "   "}  {r["ev"]}')
