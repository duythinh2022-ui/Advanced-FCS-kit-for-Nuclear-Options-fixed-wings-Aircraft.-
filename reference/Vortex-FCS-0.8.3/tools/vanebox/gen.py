# Vortex 0.5.1 vane box: mesh generator (frame = round skirt with two flat sides + shelves + thin keel; vane pairs).
# Second design. 0.5.0's box sat inside the round duct (a rectangle inscribed in the circle under a shroud) with a keel channel
# wrapped round the fan's hub fairing; Tom, 18 Nov: "Thats ugly, why the middle is so thick, why is it just a square in a circle".
# Here everything hangs at the duct exit and below it, at the level of the hub fairing's tip and under it: the vanes run from a
# thin keel to the frame over the whole round exit.
# Root-frame coordinates (x right, y up, z forward, metres), converted to the lift fan part's local frame by the exporter.
# Winding as the game's meshes: cross(v1-v0, v2-v0) . normal > 0.
import numpy as np, pickle, sys
sys.path.insert(0, '/home/claude/work/geo')

ORG = np.array([0.0, -0.2264, 2.2087])      # liftFan part origin in the root frame (overwritten by build() from the hierarchy)
ZC = 2.316                                   # fan axis z (root frame); x = 0
R_DUCT = 0.6188                              # duct wall: 32-gon, vertices at k * 11.25 deg, down to its exit at y -1.139
Y_EXIT = -1.139
RI, RO = 0.598, R_DUCT + 0.004               # frame: inner radius, outer radius (4 mm behind the duct wall above the exit)
XF, TF = 0.480, 0.018                        # flat sides: inner face |x|, thickness (the closed doors' inner skin rises outboard of here)
YT, YB = -1.100, -1.214                      # frame top (inside the duct) / bottom at its inner lip
YBO = -1.202                                 # bottom at the outer edge: the rim is bevelled, the closed doors' skin rises outboard
YSH_T, YSH_B, RSH = -1.141, -1.147, 0.655    # shelves closing the duct exit outboard of the flat sides: top, bottom, outer radius (under the bay's bevel)
SH_IN = 0.005                                # shelf's straight edge this far inside the flat wall
KW = 0.009                                   # keel half width
KYT, KYB = -1.140, -1.240                    # keel top (14 mm under the hub fairing's tip at -1.126) / bottom
KZ1, KZE, KYE = 0.480, 0.608, -1.203         # keel: half length of the deep part, ends (inside the frame's wall), bottom at the ends
NPAIR, PITCH = 8, 0.140                      # vane pairs, fore-aft pitch
YH = -1.167                                  # hinge line height
CHORD = 0.090
RELIEF_S, RELIEF_X = 0.016, 0.030             # hub relief: above this chord station the vane's inner end is cut back, by this much at the top edge
GAP = 0.010                                  # vane ends to keel / frame
PIN_R, PIN_IN = 0.0045, 0.008                # pivot pins: radius, depth into the keel / the frame
VANE_MAX = 46.7                              # drawn travel (deg): -11.1 .. +46.7
# vane section: (s along the chord, + = up at 0 deg; half thickness)
SECT = [(0.045, 0.0), (0.038, 0.0036), (0.016, 0.0050), (0.0, 0.0050), (-0.016, 0.0046), (-0.038, 0.0025), (-0.045, 0.0)]

WHITE = (255, 255, 255, 255)                 # as the bay doors' inner structure
DARK = (99, 99, 99, 255)                     # as the fan duct wall and the fan's own jet_mechanical parts
MU, MV = 3.0, 1.5                            # structure texture scale: metres per u / per v

# structure texture regions (u0, u1, v0, v1), v up
R_FRAMES = (0.680, 0.997, 0.640, 0.755)      # stiffener frames: round walls
R_HOLES = (0.030, 0.257, 0.122, 0.237)       # rail + round lightening holes: flat sides
R_KEEL = (0.165, 0.490, 0.865, 0.945)        # row of six framed lightening holes: keel flanks
R_PLAIN = (0.525, 0.615, 0.525, 0.635)       # plain sheet: rims, shelves
MECH_C, MECH_M = (0.790, 0.372), 23.0        # jet_mechanical: the dark metal patch the fan's stators use
# 0.5.5: the frame's outward faces (bottom rim, outer walls, shelf undersides) are drawn with the fan part's skin material,
# sampled where the bay's own bevel round the exit samples it (Tom, 18 Nov: "the rim color still doesnt match the yellow-ish
# white around it"). The bevel (the funnel from the duct exit at y -1.139 down to the bay edges) is SmallFighter1_skin on the
# atlas row v 0.94-0.99; this point is the most uniform spot of it (default livery: 0.625 grey, AO 0.81, smoothness 0.23,
# local std 0.011 over 13x13 texels). The skin faces map onto a few texels round it, so they take the bevel's paint in any
# livery that paints the bay the same way, without its panel detail. Not zero-size: the tangents need a real mapping.
SKIN_UV0, SKIN_K = (0.85851, 0.97037), 0.004    # uv at the frame's centre, uv per metre (+-0.0026 over the frame: within the +-6 texels measured)
SKIN = (255, 255, 255, 255)                  # vertex colour of the skin faces (the game's skin faces carry 1.0)


class Mesh:
    def __init__(self):
        self.P, self.N, self.UV, self.C, self.T, self.G = [], [], [], [], [], []

    def poly(self, pts, n, uvf, col, grp=0):
        """convex planar polygon, flat normal n, uv = uvf(p)"""
        pts = [np.asarray(p, float) for p in pts]
        n = np.asarray(n, float); n = n / np.linalg.norm(n)
        if np.dot(np.cross(pts[1] - pts[0], pts[2] - pts[0]), n) < 0:
            pts = pts[::-1]
        b = len(self.P)
        for p in pts:
            self.P.append(p); self.N.append(n); self.UV.append(np.asarray(uvf(p), float)); self.C.append(col)
        for i in range(1, len(pts) - 1):
            self.T.append((b, b + i, b + i + 1)); self.G.append(grp)

    def quad_n(self, pts, nrms, uvs, col, grp=0):
        """quad with per-vertex normals and uvs; wound to face the mean normal"""
        pts = [np.asarray(p, float) for p in pts]; nrms = [np.asarray(n, float) / np.linalg.norm(n) for n in nrms]
        b = len(self.P)
        for p, n, uv in zip(pts, nrms, uvs):
            self.P.append(p); self.N.append(n); self.UV.append(np.asarray(uv, float)); self.C.append(col)
        f = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        if np.dot(f, sum(nrms)) > 0: self.T += [(b, b + 1, b + 2), (b, b + 2, b + 3)]
        else: self.T += [(b, b + 2, b + 1), (b, b + 3, b + 2)]
        self.G += [grp, grp]

    def arrays(self):
        """triangles ordered by group (stable): group 0 (structure) first, then group 1 (skin); see split()"""
        order = sorted(range(len(self.T)), key=lambda i: self.G[i])
        return (np.array(self.P), np.array(self.N), np.array(self.UV), np.array(self.C, dtype=np.uint8), np.array([self.T[i] for i in order], dtype=np.int32))

    def split(self):
        """index count of group 0 (the first submesh) in arrays()' triangle list"""
        return 3 * sum(1 for g in self.G if g == 0)


def skin_uv(p):
    p = np.asarray(p, float); dy = p[1] - YB
    return (SKIN_UV0[0] + SKIN_K * (p[0] + 0.7 * dy), SKIN_UV0[1] + SKIN_K * ((p[2] - ZC) + 0.7 * dy))


def planar(o, eu, ev, uv0, mu=MU, mv=MV):
    o = np.asarray(o, float); eu = np.asarray(eu, float); ev = np.asarray(ev, float)
    return lambda p: (uv0[0] + np.dot(p - o, eu) / mu, uv0[1] + np.dot(p - o, ev) / mv)


def fit(o, eu, ev, lu, lv, rect):
    u0, u1, v0, v1 = rect
    return planar(o, eu, ev, (u0, v0), lu / (u1 - u0), lv / (v1 - v0))


def arc_angles(r, xlim):
    """angles (deg, from +x towards +z) of the fore arc's vertices: the junction with the flat side, the duct's own vertex
    angles between, the other junction. The aft arc is its mirror."""
    t0 = np.degrees(np.arccos(xlim / r))
    mid = [a for a in np.arange(0, 180.01, 11.25) if t0 + 3.0 < a < 180 - t0 - 3.0]
    return [t0] + mid + [180 - t0]


def outline(r, xlim):
    """closed outline, counter-clockwise seen from above (+y): [(x, dz, kind)], kind = 'a' on an arc, 'f' where a flat starts"""
    A = arc_angles(r, xlim); pts = []
    for a in A: pts.append((r * np.cos(np.radians(a)), r * np.sin(np.radians(a)), a))          # fore arc, +x to -x
    for a in A: pts.append((-r * np.cos(np.radians(a)), -r * np.sin(np.radians(a)), a + 180))    # aft arc, -x to +x
    return pts, len(A)


def build_frame():
    m = Mesh()
    X, Y, Z = np.eye(3)
    pin, na = outline(RI, XF); pout, _ = outline(RO, XF + TF)
    n = len(pin)
    arc_len = RI * np.radians(180 - 2 * np.degrees(np.arccos(XF / RI)))
    h = YT - YB
    plain = planar((0, 0, ZC), X, Z, ((R_PLAIN[0] + R_PLAIN[1]) / 2, (R_PLAIN[2] + R_PLAIN[3]) / 2), 14.0, 7.0)
    for k in range(n):
        k1 = (k + 1) % n
        (x0, z0, a0), (x1, z1, a1) = pin[k], pin[k1]
        (X0, Z0, _), (X1, Z1, _) = pout[k], pout[k1]
        flat = (k % na) == na - 1                      # last point of an arc -> the segment to the next arc is a flat side
        if flat:
            sx = np.sign(x0); L = abs(z1 - z0); Lo = abs(Z1 - Z0)
            zlo, Zlo = min(z0, z1), min(Z0, Z1)
            # inner face (faces the vanes), outer face (faces the pocket under the shelf)
            m.poly([(x0, YB, ZC + z0), (x1, YB, ZC + z1), (x1, YT, ZC + z1), (x0, YT, ZC + z0)], -sx * X, fit((x0, YB, ZC + zlo), Z, Y, L, h, (R_HOLES[0], R_HOLES[1], R_HOLES[2], R_HOLES[2] + h / MV)), WHITE)
            m.poly([(X0, YBO, ZC + Z0), (X1, YBO, ZC + Z1), (X1, YT, ZC + Z1), (X0, YT, ZC + Z0)], sx * X, skin_uv, SKIN, 1)
        else:
            base = (a0 // 180) * 180; t0a = np.degrees(np.arccos(XF / RI))
            def uu(a): return R_FRAMES[0] + (R_FRAMES[1] - R_FRAMES[0]) * ((a - base) - t0a) / (180 - 2 * t0a)
            v0, v1 = R_FRAMES[2], R_FRAMES[2] + h / MV
            n0 = -np.array([np.cos(np.radians(a0)), 0, np.sin(np.radians(a0))]); n1 = -np.array([np.cos(np.radians(a1)), 0, np.sin(np.radians(a1))])
            # inner wall, full height
            m.quad_n([(x0, YB, ZC + z0), (x1, YB, ZC + z1), (x1, YT, ZC + z1), (x0, YT, ZC + z0)], [n0, n1, n1, n0], [(uu(a0), v0), (uu(a1), v0), (uu(a1), v1), (uu(a0), v1)], WHITE)
            # outer wall, from the bottom to 7 mm above the duct's exit (behind the duct wall from there up)
            yo = Y_EXIT + 0.007; vo = v0 + (yo - YB) / MV; vb = v0 + (YBO - YB) / MV
            pw = [(X0, YBO, ZC + Z0), (X1, YBO, ZC + Z1), (X1, yo, ZC + Z1), (X0, yo, ZC + Z0)]
            m.quad_n(pw, [-n0, -n1, -n1, -n0], [skin_uv(q) for q in pw], SKIN, 1)
        # rims
        pr = [np.array([x0, YB, ZC + z0]), np.array([x1, YB, ZC + z1]), np.array([X1, YBO, ZC + Z1]), np.array([X0, YBO, ZC + Z0])]
        nr = np.cross(pr[1] - pr[0], pr[3] - pr[0]); nr = nr if nr[1] < 0 else -nr
        m.quad_n(pr, [nr] * 4, [skin_uv(q) for q in pr], SKIN, 1)
        m.poly([(x0, YT, ZC + z0), (x1, YT, ZC + z1), (X1, YT, ZC + Z1), (X0, YT, ZC + Z0)], Y, plain, WHITE)
    # shelves: the duct exit outboard of each flat side, a plate under the exit plane
    xs = XF + TF - SH_IN
    phi = np.degrees(np.arcsin(np.sqrt(RSH ** 2 - xs ** 2) / RSH))
    angs = [-phi] + [a for a in np.arange(-33.75, 33.76, 11.25) if abs(a) < phi - 2.0] + [phi]
    for sx in (1, -1):
        ring = [(sx * RSH * np.cos(np.radians(a)), ZC + RSH * np.sin(np.radians(a))) for a in angs]
        m.poly([(x, YSH_T, z) for x, z in ring], Y, plain, DARK)
        m.poly([(x, YSH_B, z) for x, z in ring], -Y, skin_uv, SKIN, 1)
    build_keel(m)
    return m


def build_keel(m):
    X, Y, Z = np.eye(3)
    prof = [(-KZE, KYT), (KZE, KYT), (KZE, KYE), (KZ1, KYB), (-KZ1, KYB), (-KZE, KYE)]     # (dz, y)
    L, H = 2 * KZE, KYT - KYB
    for sx in (1, -1):
        m.poly([(sx * KW, y, ZC + dz) for dz, y in prof], sx * X, fit((sx * KW, KYB, ZC - KZE), Z, Y, L, H, R_KEEL), WHITE)
    plain = planar((0, 0, ZC), Z, X, (R_PLAIN[0] + 0.01, (R_PLAIN[2] + R_PLAIN[3]) / 2), 16.0, 1.5)
    m.poly([(-KW, KYB, ZC - KZ1), (KW, KYB, ZC - KZ1), (KW, KYB, ZC + KZ1), (-KW, KYB, ZC + KZ1)], -Y, plain, WHITE)
    m.poly([(-KW, KYT, ZC - KZE), (KW, KYT, ZC - KZE), (KW, KYT, ZC + KZE), (-KW, KYT, ZC + KZE)], Y, plain, WHITE)
    for sz in (1, -1):
        d = np.array([0.0, KYE - KYB, sz * (KZE - KZ1)]); nn = np.cross(X, d); nn = nn if nn[1] < 0 else -nn
        m.poly([(-KW, KYB, ZC + sz * KZ1), (KW, KYB, ZC + sz * KZ1), (KW, KYE, ZC + sz * KZE), (-KW, KYE, ZC + sz * KZE)], nn, plain, WHITE)


def wall_x(dz):
    """the frame's inner face |x| at fore-aft offset dz from the fan axis (the round parts are a 32-gon: its inscribed radius)"""
    ri = RI * np.cos(np.radians(5.625))
    return min(XF, np.sqrt(max(0.0, ri * ri - dz * dz)))


def clear_x(dz):
    """the largest |x| a vane point at fore-aft offset dz may have and stay GAP clear of the frame, measured square to the wall"""
    rc = RI * np.cos(np.radians(5.625)) - GAP
    return min(XF - GAP, np.sqrt(max(0.0, rc * rc - dz * dz)))


def vane_ends(dz):
    """a vane's outer end at the hinge line and at its leading / trailing edge. The edges swing fore and aft with the vane, so
    under a round part of the frame the tip is cut back towards them (two flat faces meeting at the hinge line); the wall's
    |x| is concave in dz, so a straight cut between the two is clear in between."""
    hmax = max(h for _, h in SECT)
    sweep = CHORD / 2 * np.sin(np.radians(VANE_MAX + 1.0)) + hmax
    xh = clear_x(abs(dz) + hmax)
    xe = min(xh, clear_x(abs(dz) + sweep))
    return xh, xe


def build_vane_pair(dz):
    """both vanes of one hinge line, in the hinge frame: x along the hinge (right), y up, z forward; chord along y at 0 deg"""
    m = Mesh()
    xh, xe = vane_ends(dz); xin = KW + GAP
    c2 = CHORD / 2; taper = (xh - xe) / c2
    uvf = lambda p: (MECH_C[0] + p[0] / MECH_M, MECH_C[1] + p[1] / MECH_M)
    loop = [(s, +hh) for s, hh in SECT] + [(s, -hh) for s, hh in SECT[-2:0:-1]]      # closed section, (s, z)
    n = len(loop)
    def xend(s): return xh - taper * abs(s)
    rk = RELIEF_X / (c2 - RELIEF_S)
    def xstart(s): return xin + rk * max(0.0, s - RELIEF_S)
    def seg_n(i):
        a_ = np.array(loop[i % n]); b_ = np.array(loop[(i + 1) % n]); ee = b_ - a_; q = np.array([-ee[1], ee[0]]); q /= np.linalg.norm(q)
        mm = (a_ + b_) / 2
        return q if q[1] * mm[1] >= 0 else -q
    for sx in (1, -1):
        # skin: smooth round the section, hard at the leading and trailing edge
        for k in range(n):
            (s0, z0), (s1, z1) = loop[k], loop[(k + 1) % n]
            nk = seg_n(k)
            n0 = nk if abs(z0) < 1e-9 else seg_n(k - 1) + nk
            n1 = nk if abs(z1) < 1e-9 else seg_n(k + 1) + nk
            N0 = (0.0, n0[0], n0[1]); N1 = (0.0, n1[0], n1[1])
            P = [(sx * xstart(s0), s0, z0), (sx * xend(s0), s0, z0), (sx * xend(s1), s1, z1), (sx * xstart(s1), s1, z1)]
            m.quad_n(P, [N0, N0, N1, N1], [uvf(np.array(p)) for p in P], DARK)
        # inner end: flat below the relief station, cut back above it (clear of the hub fairing's cone as the vane turns)
        def order(pts):
            c = np.mean(pts, axis=0); return sorted(pts, key=lambda p: np.arctan2(p[1] - c[1], p[0] - c[0]))
        lo = [(s, z) for s, z in loop if s <= RELIEF_S + 1e-12]; hi = [(s, z) for s, z in loop if s >= RELIEF_S - 1e-12]
        m.poly([(sx * xin, s, z) for s, z in order(lo)], (-sx, 0, 0), uvf, DARK)
        m.poly([(sx * xstart(s), s, z) for s, z in order(hi)], (-sx, rk, 0), uvf, DARK)
        # outer tip: flat if the end is square, else two faces meeting at the hinge line
        if taper < 1e-6:
            m.poly([(sx * xh, s, z) for s, z in loop], (sx, 0, 0), uvf, DARK)
        else:
            up = [(s, z) for s, z in loop if s >= -1e-12]; dn = [(s, z) for s, z in loop if s <= 1e-12]
            m.poly([(sx * xend(s), s, z) for s, z in order(up)], (sx, taper, 0), uvf, DARK)
            m.poly([(sx * xend(s), s, z) for s, z in order(dn)], (sx, -taper, 0), uvf, DARK)
        # pivot pins (hexagonal): into the keel, into the frame
        xw = min(XF, np.sqrt(max(0.0, RI * RI - dz * dz)))
        for xa, xb in ((xin, KW - PIN_IN + 0.003), (xh, xw + PIN_IN)):
            for k in range(6):
                a0, a1 = np.radians(60 * k), np.radians(60 * k + 60)
                c0, s0, c1, s1 = np.cos(a0), np.sin(a0), np.cos(a1), np.sin(a1)
                P = [(sx * xa, PIN_R * c0, PIN_R * s0), (sx * xb, PIN_R * c0, PIN_R * s0), (sx * xb, PIN_R * c1, PIN_R * s1), (sx * xa, PIN_R * c1, PIN_R * s1)]
                m.quad_n(P, [(0, c0, s0), (0, c0, s0), (0, c1, s1), (0, c1, s1)], [uvf(np.array(p)) for p in P], DARK)
    return m


def pair_dz():
    return [(k - (NPAIR - 1) / 2) * PITCH for k in range(NPAIR)]


def build():
    global ORG
    try:
        from scene import M
        ORG = M('liftFan')[:3, 3].copy()
    except Exception as e:
        print('scene not available, ORG default', e)
    dzs = pair_dz()
    kinds, vanes, pairs = [], [], []                                  # one mesh per distinct tip (pairs under the flat sides share one)
    for d in dzs:
        key = tuple(round(v, 5) for v in vane_ends(d)) + (round(min(XF, np.sqrt(max(0.0, RI * RI - d * d))), 5),)
        if key not in kinds:
            kinds.append(key); vanes.append(build_vane_pair(d).arrays())
        pairs.append((d, kinds.index(key)))
    fm = build_frame()
    return {'frame': fm.arrays(), 'frame_split': fm.split(), 'vanes': vanes, 'kinds': kinds, 'pairs': pairs, 'org': ORG}


def rot_x(deg):
    a = np.radians(deg); c, s = np.cos(a), np.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def vane_world(o, i, deg):
    """pair i's mesh in the root frame at a drawn angle"""
    dz, k = o['pairs'][i]; P, N, UV, C, T = o['vanes'][k]; R = rot_x(deg)
    return P @ R.T + np.array([0.0, YH, ZC + dz]), N @ R.T, UV, C, T


if __name__ == '__main__':
    o = build()
    def rep(name, arr):
        P, N, UV, C, T = arr
        fn = np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]]); ar = np.linalg.norm(fn, axis=1) / 2
        dots = (fn * (N[T[:, 0]] + N[T[:, 1]] + N[T[:, 2]])).sum(1)
        print(f"{name}: verts {len(P)}, tris {len(T)}, winding ok {int((dots > 0).sum())}/{len(T)}, degenerate {int((ar < 1e-9).sum())}, min area {ar.min() * 1e4:.3f} cm2, bounds {P.min(0).round(4)} .. {P.max(0).round(4)}, uv {UV.min(0).round(3)} .. {UV.max(0).round(3)}")
    rep('frame', o['frame'])
    for k, a in zip(o['kinds'], o['vanes']):
        rep(f'vane pair mesh (tip x {k[0]:.3f} at the hinge, {k[1]:.3f} at the edges, wall {k[2]:.3f})', a)
    tot = len(o['frame'][4]) + sum(len(o['vanes'][k][4]) for _, k in o['pairs'])
    print('pairs', [(round(d, 3), k) for d, k in o['pairs']], '| triangles in all', tot, '| renderers', 1 + len(o['pairs']))
    pickle.dump(o, open('/home/claude/work/geo/vane.pkl', 'wb'))
