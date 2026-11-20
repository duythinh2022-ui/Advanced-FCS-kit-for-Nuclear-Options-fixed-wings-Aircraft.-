# small software rasteriser for checking the vane box against the game's meshes (preview only: lambert + ambient, base textures,
# back-face culling with the game's winding, z-buffer). Not the game's shading.
import sys; sys.path.insert(0, '/home/claude/work/geo')
import numpy as np
from PIL import Image
from scene import world, rot_about, mesh_of
import gen

TEX = {}
def tex(name):
    if name not in TEX:
        TEX[name] = np.asarray(Image.open(f'/home/claude/work/geo/tex_{name}.png').convert('RGBA')).astype(np.float32) / 255.0
    return TEX[name]
MATTEX = {'SmallFighter1_skin': 'skin_b', 'jet_mechanical': 'mech_b', 'aircraft_structure': 'struct_b'}

class Scene:
    def __init__(self): self.items = []
    def add(self, P, N, UV, C, T, mat, tint=1.0):
        self.items.append((np.asarray(P, float), np.asarray(N, float), np.asarray(UV, float), None if C is None else np.asarray(C, float), np.asarray(T), mat, tint))
    def add_game(self, name, Mover=None, colors=True):
        d, mn = mesh_of(name); P, N, UV, subs, _ = world(name, Mover)
        C = np.array(d['C']) if (colors and d.get('C') is not None) else None
        for s, m in zip(subs, mn):
            self.add(P, N, UV, C, s, m)

def look_at(eye, target, up=(0, 1, 0)):
    eye = np.asarray(eye, float); f = np.asarray(target, float) - eye; f /= np.linalg.norm(f)
    r = np.cross(up, f); r /= np.linalg.norm(r); u = np.cross(f, r)
    return eye, np.stack([r, u, f])       # rows: right, up, forward (left-handed like Unity: x right, y up, z forward)

def render(scene, eye, target, fov=40, size=(1100, 800), up=(0, 1, 0), light=None, amb=0.42, bg=(0.55, 0.65, 0.78)):
    W, H = size; eye, R = look_at(eye, target, up)
    f = 0.5 * H / np.tan(np.radians(fov) / 2)
    img = np.tile(np.array(bg, np.float32), (H, W, 1)); zb = np.full((H, W), np.inf, np.float32)
    L = np.asarray(light if light is not None else (eye - np.asarray(target)), float); L /= np.linalg.norm(L)
    for P, N, UV, C, T, mat, tint in scene.items:
        V = (P - eye) @ R.T                                   # view space
        tx = tex(MATTEX[mat]) if mat in MATTEX else None
        for t in T:
            v = V[t]
            if (v[:, 2] < 0.05).any(): continue
            p = P[t]; fn = np.cross(p[1] - p[0], p[2] - p[0])
            if np.dot(fn, eye - p[0]) <= 0: continue          # back face (game winding)
            sx = W / 2 + f * v[:, 0] / v[:, 2]; sy = H / 2 - f * v[:, 1] / v[:, 2]
            x0, x1 = int(max(0, np.floor(sx.min()))), int(min(W - 1, np.ceil(sx.max())))
            y0, y1 = int(max(0, np.floor(sy.min()))), int(min(H - 1, np.ceil(sy.max())))
            if x0 > x1 or y0 > y1: continue
            X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            den = (sy[1] - sy[2]) * (sx[0] - sx[2]) + (sx[2] - sx[1]) * (sy[0] - sy[2])
            if abs(den) < 1e-12: continue
            w0 = ((sy[1] - sy[2]) * (X - sx[2]) + (sx[2] - sx[1]) * (Y - sy[2])) / den
            w1 = ((sy[2] - sy[0]) * (X - sx[2]) + (sx[0] - sx[2]) * (Y - sy[2])) / den
            w2 = 1 - w0 - w1
            ins = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not ins.any(): continue
            iz = w0 / v[0, 2] + w1 / v[1, 2] + w2 / v[2, 2]; z = 1 / iz
            b0, b1, b2 = w0 / v[0, 2] * z, w1 / v[1, 2] * z, w2 / v[2, 2] * z      # perspective-correct weights
            sub_zb = zb[y0:y1 + 1, x0:x1 + 1]; ok = ins & (z < sub_zb)
            if not ok.any(): continue
            uv = b0[..., None] * UV[t[0]] + b1[..., None] * UV[t[1]] + b2[..., None] * UV[t[2]]
            n = b0[..., None] * N[t[0]] + b1[..., None] * N[t[1]] + b2[..., None] * N[t[2]]
            n /= np.maximum(1e-9, np.linalg.norm(n, axis=2, keepdims=True))
            if tx is not None:
                th, tw = tx.shape[:2]
                px = np.clip((np.mod(uv[..., 0], 1) * tw).astype(int), 0, tw - 1); py = np.clip((np.mod(uv[..., 1], 1) * th).astype(int), 0, th - 1)
                c = tx[py, px]
                if mat == 'jet_mechanical': ok &= c[..., 3] > 0.394       # alpha test
                col = c[..., :3]
            elif mat == 'Matte_black': col = np.full(uv.shape[:2] + (3,), 0.03, np.float32)
            else: col = np.full(uv.shape[:2] + (3,), 0.6, np.float32)
            if C is not None and mat == 'aircraft_structure':
                vc = b0[..., None] * C[t[0], :3] + b1[..., None] * C[t[1], :3] + b2[..., None] * C[t[2], :3]
                col = col * vc
            lam = np.clip(n @ L, 0, 1)
            out = col * tint * (amb + (1 - amb) * lam[..., None])
            sub = img[y0:y1 + 1, x0:x1 + 1]
            sub[ok] = out[ok]; sub_zb[ok] = z[ok]
    return Image.fromarray((np.clip(img, 0, 1) ** (1 / 1.0) * 255).astype(np.uint8))

def vane_scene(vane_deg=0.0, doors=90.0, with_box=True, blades=True, slat_angle=None):
    sc = Scene()
    sc.add_game('liftFan')
    if blades: sc.add_game('liftFan_blades', colors=False)
    for name, hp, sg in (('fanDoor_LL', np.array([-0.712, -1.197, 1.630]), -1.0), ('fanDoor_LR', np.array([0.712, -1.197, 1.630]), 1.0)):
        if doors is None: continue
        from scene import M
        Mo = rot_about(hp, [0, 0, 1], sg * doors) @ M(name)
        sc.add_game(name, Mo)
    if with_box:
        o = gen.build()
        P, N, UV, C, T = o['frame']; sc.add(P, N, UV, C / 255.0, T, 'aircraft_structure')
        a = vane_deg / 0.9 if slat_angle is None else slat_angle
        for i in range(len(o['pairs'])):
            P, N, UV, C, T = gen.vane_world(o, i, a); sc.add(P, N, UV, C / 255.0, T, 'jet_mechanical')
    return sc

if __name__ == '__main__':
    import sys
    tag = sys.argv[1] if len(sys.argv) > 1 else 'a'
    zc = gen.ZC
    views = {
        'below': ((0.0, -3.4, zc - 0.25), (0.0, -1.1, zc), (0, 0, 1), 34),
        'quarter': ((1.9, -2.9, zc - 1.9), (0.0, -1.10, zc), (0, 1, 0), 30),
        'side': ((0.25, -2.2, zc - 2.6), (0.0, -1.12, zc), (0, 1, 0), 30),
        'close': ((0.55, -1.75, zc - 0.75), (0.1, -1.12, zc), (0, 1, 0), 55),
    }
    for vn, (eye, tgt, up, fov) in views.items():
        for va in ((-10, 42) if vn in ('quarter', 'side') else (0,)) if len(sys.argv) < 3 else (float(sys.argv[2]),):
            sc = vane_scene(va)
            im = render(sc, eye, tgt, fov=fov, up=up, light=(0.3, -1.0, -0.5))
            im.save(f'/home/claude/work/geo/r_{tag}_{vn}_{int(va)}.png'); print('saved', vn, va)
