# Damage suite: the cases flight logs and the damage tool produce, each next to its undamaged twin.
# usage: dmgsuite.py <tag> [Name=value ...]     (env BENCH_DIR, REG_S, BENCH_DATA as for reg.py / suite.sh)
# Every case: alt 1500, full throttle, FinLossAoACap=65 (Tom's in-game value; the stub carries 40). Times are from the pull
# start (CSV t = time from pull). Damage keys are the bench's dmg / dmgAt / detachAt (sim/Damage.cs); a fin's rudder goes
# with it by connectivity. Read mechanisms against the twin, not degrees: fins-off sideslip is uncalibrated (CALIBRATION.md).
import subprocess, os, sys, math, csv
from concurrent.futures import ThreadPoolExecutor
S = os.environ.get('REG_S'); B = os.environ.get('BENCH_DIR'); DATA = os.environ.get('BENCH_DATA', (S or '') + '/airframe_cal.json')
if not S or not B: sys.exit('set REG_S (output root) and BENCH_DIR (bench folder)')
tag = sys.argv[1]; sets = sys.argv[2:]; out = f'{S}/dmg/{tag}'; os.makedirs(out, exist_ok=True)

FINS = 'detach=Vstab_L+Vstab_R'; FIN1 = 'detach=Vstab_L'
# stick profiles (t from pull): pitch prof, roll rprof; ev = (event time, kind) for the scorecard window
P_HOLD = '0:0,0.05:1,9:1'
LEAF_STOP = ('0:0,2:0,2.05:1,5:1,5.05:0,9:0', 5.0, 'stop')
LEAF_REV = ('0:0,2:0,2.05:1,5:1,5.05:-1,8:-1,8.05:0', 5.0, 'rev')
PUSH_P = '0:0,0.05:1,4:1,4.05:-1,7:-1,7.05:0'
PO_REL = ('0:0,1.5:0,1.55:1,4:1,4.05:0,8:0', 4.0, 'push, roll released')
PO_HELD = ('0:0,1.5:0,1.55:1,7:1,7.05:0', 4.0, 'push, roll held')
CASES = []   # (group, name, ias km/h, prof, rprof, t_event, kind, keys)
for cfg, keys in (('no fins', [FINS]), ('one fin', [FIN1]), ('clean', [])):
    for ias in (296, 370):   # 160 / 200 kt
        CASES.append((cfg, f'leaf stop {ias}', ias, P_HOLD, *LEAF_STOP, keys))
        CASES.append((cfg, f'leaf rev {ias}', ias, P_HOLD, *LEAF_REV, keys))
    for ias in (560, 600):   # entry speed: the push comes at ~215 kt from 65 deg with the corkscrew's yaw on (20:19 log, 120 s)
        CASES.append((cfg, f'pushover released {ias}', ias, PUSH_P, *PO_REL, keys))
        CASES.append((cfg, f'pushover HELD {ias}', ias, PUSH_P, *PO_HELD, keys))
# fins shot to 8 HP during a high-speed rolling pull, then on to 65 (the 18:32 log: fins shot off, then pulled to 66)
for cfg, keys in (('fins 8 HP mid-pull', ['dmgAt=0.5:Vstab_L:8+Vstab_R:8']), ('clean', [])):
    CASES.append((cfg, 'rolling pull 900', 900, '0:0,0.05:1,8:1', '0:0,1:0,1.05:1,3:1,3.05:0,8:0', 3.0, 'stop', keys))
# fins + right flaperon gone at 480 kt (the 18:25 log), roll reversal at moderate AoA
for cfg, keys in (('fins + R flaperon', ['detach=Vstab_L+Vstab_R+Flap_R+Aileron_R']), ('R flaperon', ['detach=Flap_R+Aileron_R']), ('clean', [])):
    CASES.append((cfg, 'roll rev 890', 890, '0:0,0.05:0.4,6:0.4', '0:0,1:0,1.05:1,2.5:1,2.55:-1,4:-1,4.05:0,6:0', 2.5, 'rev', keys))
# wing panels: damaged-but-attached and shot off, 1 g roll and reversal at 600 km/h
for cfg, keys in (('L wing 20 HP', ['dmg=Wingmid_L:20+Wingtip_L:20+WingRear_L:20+WingFore_L:20']), ('L wingmid off', ['detach=Wingmid_L']), ('clean', [])):
    CASES.append((cfg, 'roll rev 600', 600, '0:0,6:0', '0:0,0.5:0,0.55:1,1.5:1,1.55:-1,2.5:-1,2.55:0,6:0', 1.5, 'rev', keys))
# engine: left LERX shot off at 65 deg / low speed (asymmetric thrust where the rudders are faded)
for cfg, keys in (('L LERX off @3s', ['detachAt=3:LERX_L']), ('clean', [])):
    CASES.append((cfg, 'held 65 + LERX 300', 300, P_HOLD, '0:0,6:0', 3.0, 'engine', keys))

def run(i):
    g, name, ias, prof, rprof, tev, kind, keys = CASES[i]
    f = f'{out}/c{i}.csv'; fd = f'{out}/c{i}_d.csv'
    if not os.path.exists(f):
        cmd = ['dotnet', B + '/bin/bench.dll', 'one', f'iasKmh={ias}', 'alt=1500', 'trimT=1', 'pull=0', 'hold=0', 'roll=0', 'post=9',
               'throttle=1', 'FinLossAoACap=65', f'prof={prof}', f'rprof={rprof}', f'csv={f}', f'dmgCsv={fd}'] + keys + sets
        r = subprocess.run(cmd, cwd=B, env=dict(os.environ, BENCH_DATA=DATA), capture_output=True, text=True)
        open(f + '.log', 'w').write(r.stdout + r.stderr)
    return f, fd

def card(i, f, fd):
    g, name, ias, prof, rprof, tev, kind, keys = CASES[i]
    R = list(csv.DictReader(open(f)))
    num = lambda r, k: float(r[k])
    vr = lambda r: num(r, 'p') * math.cos(math.radians(num(r, 'alpha'))) + num(r, 'r') * math.sin(math.radians(num(r, 'alpha')))
    W = [r for r in R if num(r, 't') >= 0.0]
    after = [r for r in W if num(r, 't') >= tev]
    pk = max(W, key=lambda r: abs(num(r, 'beta')))
    pk2 = max(after, key=lambda r: abs(num(r, 'beta'))) if after else pk
    aMinE = min((num(r, 'alpha') for r in after), default=float('nan'))
    dep = any(abs(num(r, 'beta')) > 30 or num(r, 'alpha') > 80 or num(r, 'alpha') < -70 for r in W)
    # stop / reversal: time from the event until |velocity roll| < 3 deg/s (stop) or has changed sign and passed 10 (rev)
    tz = float('nan')
    if kind in ('stop', 'rev'):
        v0 = vr(min(after, key=lambda r: abs(num(r, 't') - tev))) if after else 0.0
        for r in after:
            v = vr(r)
            if (kind == 'stop' and abs(v) < 3.0) or (kind == 'rev' and v0 != 0 and v * math.copysign(1, v0) < -10.0):
                tz = num(r, 't') - tev; break
    vmax = max((abs(vr(r)) for r in W if num(r, 't') < tev), default=0.0)
    ev = ''
    if os.path.exists(fd):
        D = list(csv.DictReader(open(fd)))
        lost = [d['events'] for d in D if 'detached' in d['events'] or 'killed' in d['events']]
        ev = ' | '.join(x.strip('"') for x in lost)[:110]
    return dict(group=g, case=name, aMax=max(num(r, 'alpha') for r in W), aMin=aMinE, vmax=vmax,
                bPk=abs(num(pk, 'beta')), tPk=num(pk, 't'), bAfter=abs(num(pk2, 'beta')), tz=tz, dep=dep, ev=ev)

with ThreadPoolExecutor(4) as ex: files = list(ex.map(run, range(len(CASES))))
rows = [card(i, f, fd) for i, (f, fd) in enumerate(files)]
print(f'== damage suite {tag} {" ".join(sets)}   (|b| deg; vroll = p cos a + r sin a, deg/s; t from pull; aMin after the event; tz = s from stick event to stop / reversal, nan = never)')
print(f'{"case":24s} {"config":18s} {"aMax":>5} {"aMin":>6} {"vrMax":>5} {"|b|pk":>6} {"@t":>5} {"|b|aft":>6} {"tz":>5}  dep  events')
for r in rows:
    print(f'{r["case"]:24s} {r["group"]:18s} {r["aMax"]:5.1f} {r["aMin"]:6.1f} {r["vmax"]:5.0f} {r["bPk"]:6.1f} {r["tPk"]:5.2f} {r["bAfter"]:6.1f} {r["tz"]:5.2f}  {"DEP" if r["dep"] else "   "}  {r["ev"]}')
