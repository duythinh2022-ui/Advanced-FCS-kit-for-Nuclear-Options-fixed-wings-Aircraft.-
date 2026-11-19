# Vortex 0.5.0 vane box: search for coplanar overlapping faces that face the same way (z-fighting) inside the box's own meshes
# and against the game's fan part and doors, vanes at several angles.
import sys; sys.path.insert(0, '/home/claude/work/geo')
import numpy as np, gen
from scene import world, rot_about, M
def tri_data(P, T):
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]; n = np.cross(b - a, c - a); ar = np.linalg.norm(n, axis=1); n = n / np.maximum(ar, 1e-12)[:, None]
    return np.stack([a, b, c], 1), n, ar / 2
def overlap2d(t1, t2, n):
    # project to the plane, SAT with a small shrink so shared edges do not count
    u = np.cross(n, [1, 0, 0]);  u = u if np.linalg.norm(u) > 0.3 else np.cross(n, [0, 1, 0]); u /= np.linalg.norm(u); v = np.cross(n, u)
    A = np.stack([t1 @ u, t1 @ v], 1); B = np.stack([t2 @ u, t2 @ v], 1)
    for tri in (A, B):
        for i in range(3):
            e = tri[(i + 1) % 3] - tri[i]; ax = np.array([-e[1], e[0]]); l = np.linalg.norm(ax)
            if l < 1e-12: continue
            ax /= l; pa = A @ ax; pb = B @ ax
            if pa.max() <= pb.min() + 2e-4 or pb.max() <= pa.min() + 2e-4: return False
    return True
def check(nameA, PA, TA, nameB, PB, TB, same=False):
    tA, nA, aA = tri_data(PA, TA); tB, nB, aB = tri_data(PB, TB); hits = []
    for i in range(len(tA)):
        if aA[i] < 1e-9: continue
        m = (nB @ nA[i] > 0.9995) & (np.abs((tB[:, 0] - tA[i, 0]) @ nA[i]) < 6e-4) & (aB > 1e-9)
        if same: m[: i + 1] = False
        for j in np.nonzero(m)[0]:
            if overlap2d(tA[i], tB[j], nA[i]): hits.append((i, j, tA[i].mean(0).round(3), nA[i].round(2)))
    print(f"  {nameA:22s} vs {nameB:22s}: {len(hits)} coplanar same-facing overlaps" + (''.join(f"\n      tri {i}/{j} at {c} n {n}" for i, j, c, n in hits[:6])))
    return len(hits)
o = gen.build(); FP, FN, FUV, FC, FT = o['frame']
fanP, _, _, fanS, _ = world('liftFan'); fanT = np.vstack(fanS)
tot = check('frame', FP, FT, 'frame', FP, FT, same=True)
tot += check('frame', FP, FT, 'fan part', fanP, fanT)
for other in ('intake', 'wingRoot_L', 'wingRoot_R'):
    P, _, _, S, _ = world(other); tot += check('frame', FP, FT, other, P, np.vstack(S))
for name, hp, sg in (('fanDoor_LL', np.array([-0.712, -1.197, 1.630]), -1.0), ('fanDoor_LR', np.array([0.712, -1.197, 1.630]), 1.0)):
    for ang in (0, 90):
        Mo = rot_about(hp, [0, 0, 1], sg * ang) @ M(name); P, _, _, S, _ = world(name, Mo)
        tot += check('frame', FP, FT, f'{name} {ang}', P, np.vstack(S))
for va in (-11.1, 0.0, 20.0, 46.7):
    Ws = [gen.vane_world(o, i, va) for i in range(len(o['pairs']))]; off = np.cumsum([0] + [len(w[0]) for w in Ws])
    allP = np.vstack([w[0] for w in Ws]); allT = np.vstack([w[4] + off[i] for i, w in enumerate(Ws)])
    tot += check(f'vanes {va:+.1f}', allP, allT, 'frame', FP, FT)
    tot += check(f'vanes {va:+.1f}', allP, allT, 'fan part', fanP, fanT)
    tot += check(f'vanes {va:+.1f}', allP, allT, 'vanes', allP, allT, same=True)
print('TOTAL', tot)
