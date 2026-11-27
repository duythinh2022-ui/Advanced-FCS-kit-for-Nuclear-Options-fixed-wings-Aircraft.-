# Vortex 0.5.1 vane box: clearance checks against the game's own meshes (exact mesh-mesh distances, FCL).
import sys; sys.path.insert(0, '/home/claude/work/geo')
import numpy as np, trimesh, fcl
from scene import world, M, rot_about
import gen

def bvh(P, T):
    m = fcl.BVHModel(); m.beginModel(len(P), len(T)); m.addSubModel(np.ascontiguousarray(P, dtype=np.float64), np.ascontiguousarray(T, dtype=np.int32)); m.endModel()
    return fcl.CollisionObject(m, fcl.Transform())
def dist(a, b):
    req = fcl.DistanceRequest(enable_nearest_points=True); res = fcl.DistanceResult(); d = fcl.distance(a, b, req, res)
    hit = fcl.collide(a, b, fcl.CollisionRequest(), fcl.CollisionResult()) > 0
    return (0.0 if hit else d), res.nearest_points
def game(name, Mo=None):
    P, N, UV, subs, mn = world(name, Mo); return P, np.vstack(subs)

o = gen.build(); FP, FN, FUV, FC, FT = o['frame']; ZC = gen.ZC
npair = len(o['pairs'])
# frame pieces by position
tc = FP[FT].mean(1); rr = np.hypot(FP[FT][:, :, 0], FP[FT][:, :, 2] - ZC)
is_keel = (np.abs(FP[FT][:, :, 0]).max(1) <= gen.KW + 1e-6)
is_shelf = (np.abs(FP[FT][:, :, 1] - gen.YSH_T).max(1) < 1e-6) | (np.abs(FP[FT][:, :, 1] - gen.YSH_B).max(1) < 1e-6)
is_outer = ~is_keel & ~is_shelf & ((rr.min(1) > gen.RO - 1e-3) | ((np.abs(FP[FT][:, :, 0]).min(1) > gen.XF + gen.TF - 1e-6)))
is_inner = ~is_keel & ~is_shelf & ~is_outer
print(f"frame: {len(FT)} tris = keel {is_keel.sum()}, shelves {is_shelf.sum()}, outer wall {is_outer.sum()}, inner wall + rims {is_inner.sum()}")
fanP, fanT = game('liftFan'); bladeP, bladeT = game('liftFan_blades'); fan_o = bvh(fanP, fanT)
others = [n for n in ('intake', 'wingRoot_L', 'wingRoot_R', 'weaponBay_L', 'weaponBay_R', 'gearDoor_FL', 'gearDoor_FR', 'weaponDoor_L', 'weaponDoor_R', 'weaponDoor_LL', 'weaponDoor_RR')]
oth = [(n,) + game(n) for n in others]

print("\n== static parts against the lift fan part")
hubT = fanT[(np.hypot(fanP[fanT][:, :, 0], fanP[fanT][:, :, 2] - ZC).max(1) < 0.3)]; hub_o = bvh(fanP, hubT)
d, p = dist(bvh(FP, FT[is_keel]), hub_o); print(f"  keel to the hub fairing : {d * 1000:6.1f} mm (nearest hub point {np.round(p[1], 3)})")
# inner wall and rims below/inside the duct: the wall stands 20.8 mm inside the duct wall; its rims reach the buried outer radius on purpose
inner_only = is_inner & (rr.max(1) < gen.RI + 1e-3) & (np.abs(FP[FT][:, :, 0]).max(1) < gen.XF + 1e-6)
d, p = dist(bvh(FP, FT[inner_only]), fan_o); print(f"  inner wall faces    : {d * 1000:6.1f} mm to the duct wall / exit edge (nearest {np.round(p[1], 3)})")
for n, P, T in oth:
    d, p = dist(bvh(FP, FT[~is_shelf]), bvh(P, T)); d2, _ = dist(bvh(FP, FT[is_shelf]), bvh(P, T))
    if min(d, d2) < 0.12: print(f"  to {n:13s}: frame and keel {d * 1000:6.1f} mm, shelves (their rims are inside the airframe) {d2 * 1000:6.1f} mm")
rb = np.hypot(bladeP[:, 0], bladeP[:, 2] - ZC)
print(f"  blades (rotating)   : lowest blade point y {bladeP[:, 1].min():.3f}; frame top y {FP[:, 1].max():.3f} -> {1000 * (bladeP[:, 1].min() - FP[:, 1].max()):.0f} mm below the blades")
print(f"  hub fairing tip y -1.126; keel top y {gen.KYT}; vane tops at 0 deg y {gen.YH + gen.CHORD / 2:.3f}")

print("\n== parts that end inside the airframe on purpose: are their ends hidden?")
static = trimesh.util.concatenate([trimesh.Trimesh(fanP, fanT, process=False)] + [trimesh.Trimesh(P, T, process=False) for n, P, T in oth])
def covered_from_below(pts):
    o_ = np.asarray(pts, float); d_ = np.tile([0, -1.0, 0], (len(o_), 1))
    loc, ray, tri = static.ray.intersects_location(o_ + [0, -1e-4, 0], d_, multiple_hits=False)
    return len(set(ray.tolist())), len(o_)
# frame's outer wall above the exit: radius RO behind the duct's facets
print(f"  frame outer wall above the exit: radius {gen.RO:.4f} against the duct's {gen.R_DUCT:.4f} -> {1000 * (gen.RO - gen.R_DUCT):.1f} mm behind the duct wall, up to y {gen.Y_EXIT + 0.007:.3f} (exit at {gen.Y_EXIT})")
arc = []
for sx in (1, -1):
    for a in np.linspace(-35, 35, 141): arc.append((sx * gen.RSH * np.cos(np.radians(a)), gen.YSH_B, ZC + gen.RSH * np.sin(np.radians(a))))
arc = [p for p in arc if abs(p[0]) > gen.XF + gen.TF]
c, n = covered_from_below(arc); print(f"  shelf outer edge (r {gen.RSH}): {c} of {n} points along it have airframe surface under them (hidden)")
# where does the shelf come out from under the bevel: scan radially
for sx, lab in ((1, 'right'),):
    rows = []
    for a in (0, 15, 30):
        for r in np.arange(0.60, gen.RSH + 1e-6, 0.005):
            c, n = covered_from_below([(sx * r * np.cos(np.radians(a)), gen.YSH_B, ZC + r * np.sin(np.radians(a)))])
            if c: rows.append((a, round(float(r), 3))); break
    print(f"  shelf underside covered by the bay's bevel from r = {rows} (angle from the side, r); visible inside that, down to the flat side at x {gen.XF + gen.TF}")

print("\n== lower doors through their travel (0 = closed, 90 = open)")
H = {'fanDoor_LL': (np.array([-0.712, -1.197, 1.630]), 1.0), 'fanDoor_LR': (np.array([0.712, -1.197, 1.630]), -1.0)}
frame_o = bvh(FP, FT)
for name, (hp, _) in H.items():
    P0, T = game(name)
    for sgn in (1.0, -1.0):
        R = rot_about(hp, [0, 0, 1], sgn * 90.0); Pq = P0 @ R[:3, :3].T + R[:3, 3]
        if Pq[:, 1].min() < P0[:, 1].min() - 0.3: opens = sgn
    rows = []
    for a in list(range(0, 31, 2)) + list(range(35, 91, 5)):
        R = rot_about(hp, [0, 0, 1], opens * a); Pq = P0 @ R[:3, :3].T + R[:3, 3]; do = bvh(Pq, T)
        df, _ = dist(bvh(FP, FT[~is_shelf & ~is_keel]), do); dk, _ = dist(bvh(FP, FT[is_keel]), do); dv = 9; dsh, _ = dist(bvh(FP, FT[is_shelf]), do)
        for i in range(npair):
            for va in (-11.1, 0.0, 46.7):
                Pw, _, _, _, VT = gen.vane_world(o, i, va); dd, _ = dist(bvh(Pw, VT), do); dv = min(dv, dd)
        rows.append((a, df, dk, dv, dsh))
    r = np.array(rows)
    print(f"  {name}: frame min {r[:, 1].min() * 1000:.0f} mm (closed {r[0, 1] * 1000:.0f}, open {r[-1, 1] * 1000:.0f}); keel min {r[:, 2].min() * 1000:.0f} (closed {r[0, 2] * 1000:.0f}); vanes min {r[:, 3].min() * 1000:.0f} (closed {r[0, 3] * 1000:.0f}); shelves min {r[:, 4].min() * 1000:.0f} (closed {r[0, 4] * 1000:.0f}; their outer part is above the bay's bevel, the doors below it); smallest at door angle {int(r[r[:, 1:].min(1).argmin(), 0])} deg")

print("\n== vanes through their travel (drawn -11.1 .. +46.7 deg; checked -12 .. +47.5)")
angs = np.arange(-12.0, 47.51, 0.5); res = {k: [] for k in ('frame', 'keel', 'fan', 'hub', 'next', 'other')}; near_fan = {}
keel_o = bvh(FP, FT[is_keel]); wall_o = bvh(FP, FT[~is_keel])
for va in angs:
    m = dict(frame=9, keel=9, fan=9, hub=9, next=9, other=9)
    W = [gen.vane_world(o, i, va) for i in range(npair)]
    for i in range(npair):
        Pw, _, _, _, VT = W[i]; kind = o['pairs'][i][1]; VP = o['vanes'][kind][0]
        # the pins end inside the keel and the frame on purpose: test the vane bodies (everything but the pin faces)
        r_ = np.hypot(VP[VT][:, :, 1], VP[VT][:, :, 2]).max(1); body = r_ > gen.PIN_R + 1e-6
        bo = bvh(Pw, VT[body]); so = bvh(Pw, VT)
        m['frame'] = min(m['frame'], dist(bo, wall_o)[0]); m['keel'] = min(m['keel'], dist(bo, keel_o)[0])
        df_, pf_ = dist(so, fan_o)
        if df_ < m['fan']: m['fan'] = df_; near_fan[round(float(va), 1)] = (i, np.round(pf_[1], 3))
        dh_ = dist(so, hub_o)[0]; m['hub'] = min(m['hub'], dh_)
        if i + 1 < npair: m['next'] = min(m['next'], dist(so, bvh(W[i + 1][0], W[i + 1][4]))[0])
        for n, P, T in oth[:1]: m['other'] = min(m['other'], dist(so, bvh(P, T))[0])
    for k in res: res[k].append(m[k])
for k, lab in (('keel', 'vane bodies to the keel'), ('frame', 'vane bodies to the frame'), ('next', 'vane to the neighbouring vane'), ('hub', 'vanes to the hub fairing'), ('fan', 'vanes to the whole fan part'), ('other', 'vanes to the intake part')):
    a = np.array(res[k]); i = a.argmin()
    print(f"  {lab:46s}: min {a.min() * 1000:6.1f} mm at {angs[i]:+.1f} deg  (rest {a[np.abs(angs + 11).argmin()] * 1000:.1f}, 0: {a[np.abs(angs).argmin()] * 1000:.1f}, aft stop {a[np.abs(angs - 46.5).argmin()] * 1000:.1f})")
ext = np.array([[(gen.vane_world(o, i, va)[0][:, 1].min(), gen.vane_world(o, i, va)[0][:, 1].max()) for i in (0, 3)] for va in angs])
print(f"  vane envelope y {ext[:, :, 0].min():.3f} .. {ext[:, :, 1].max():.3f}; frame y {gen.YB} .. {gen.YT}; keel y {gen.KYB} .. {gen.KYT}")
i = int(np.array(res['fan']).argmin()); print('  nearest fan-part point at the minimum:', near_fan.get(round(float(angs[i]), 1)))
