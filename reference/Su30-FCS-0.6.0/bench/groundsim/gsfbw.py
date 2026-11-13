#!/usr/bin/env python3
# Python port of the FCS GroundSteer (2.4.9 / Ifrit 0.4.9) pedal and yaw-braking branches, plus the proposed
# stock-matched pedal map, run on gsim's plant. Compares stock, current FBW and the stock-matched FBW.
import math, sys
import numpy as np
sys.path.insert(0, ".")
from gsim import Jet, Sim

D2R = math.pi / 180.0
R2D = 180.0 / math.pi


class Fbw:
    GsKp0, GsKpMin, GsKpMax, GsKi, GsLimK, GsSatMargin = 15.0, 0.15, 3.0, 1.0, 300.0, 1.2
    GsYawAccel, GsKpsi, GsHoldRate = 20.0, 1.0, 4.0

    def __init__(self, jet, stock_match, yaw_rate_max=15.0, lat_acc=2.5):
        self.j = jet
        self.sm = stock_match
        self.rmax = yaw_rate_max
        self.alat = lat_acc
        self.rcmd = 0.0
        self.int = 0.0
        self.virt = 0.0        # stock-matched: the virtual stock wheel (deg)
        self.hold = False
        self.brakeT = 0.0
        self.psiRef = 0.0

    def stock_rate(self, delta_deg, V):
        # stock steady yaw rate for a wheel angle: kinematic V tan(d)/L, capped at the nose tyre's mu g / V
        j = self.j
        rk = V * math.tan(abs(delta_deg) * D2R) / j.L * R2D
        rs = j.mu_n * 9.81 / max(V, 0.5) * R2D
        return math.copysign(min(rk, rs), delta_deg)

    def step(self, sim, ped, dt):
        j = self.j
        V = sim.V
        r = sim.r * R2D
        if self.sm:
            # the virtual stock wheel: pedal x lock at the gear's slew rate, as LandingGear moves it
            self.virt += max(-j.slew * dt, min(j.slew * dt, ped * j.lock - self.virt))
        if abs(ped) > 0.05 or V < 1.5:
            if self.sm:
                rc = self.stock_rate(self.virt, V)
            else:
                rlim = min(self.rmax, self.alat / max(V, 0.5) * R2D)
                rc = ped * rlim
            self.hold = False
            self.brakeT = 0.0
        else:
            rc = 0.0
            if self.sm and abs(self.virt) > 0.01:
                rc = self.stock_rate(self.virt, V)     # the stock wheel is still slewing back to centre
            elif not self.hold:
                self.brakeT += dt
                if abs(r) < 0.7 or self.brakeT > 2.0:
                    self.hold = True
                    self.psiRef = sim.psi * R2D
            if self.hold:
                e = self.psiRef - sim.psi * R2D
                rc = max(-self.GsHoldRate, min(self.GsHoldRate, self.GsKpsi * e))
        if self.sm:
            self.rcmd = rc
        else:
            self.rcmd += max(-self.GsYawAccel * dt, min(self.GsYawAccel * dt, rc - self.rcmd))
        rc = self.rcmd
        err = rc - r
        if V >= 1.5:
            ff = math.atan(rc * D2R * j.L / max(V, 1.0)) * R2D
            kp = max(self.GsKpMin, min(self.GsKpMax, self.GsKp0 / max(V, 3.0)))
            lim = min(j.lock, self.GsLimK / max(V, 1.0))
            sat = self.GsSatMargin / (0.2 + 0.01 * V)
            self.int = max(-sat, min(sat, self.int + self.GsKi * kp * err * dt))
            fb = max(-sat, min(sat, kp * err + self.int))
            blend = min(1.0, max(0.0, (V - 1.5) / 1.5))
            nws = (1 - blend) * ped * j.lock + blend * max(-lim, min(lim, ff + fb))
        else:
            self.int = 0.0
            nws = ped * j.lock
        return nws / j.lock


def run(jet, V, pedal_fn, mode, T=4.0, dt=0.02):
    sim = Sim(jet, V, dt)
    f = None if mode == "stock" else Fbw(jet, mode == "match")
    rs = []
    while sim.t < T - 1e-9:
        ped = pedal_fn(sim.t)
        cmd = ped if f is None else f.step(sim, ped, dt)
        sim.step(cmd, ped)
        rs.append(sim.r * R2D)
    return np.array(rs)


def metrics(rs, dt=0.02, t_on=0.0, t_off=None):
    rss = rs[int((t_off - 0.3) / dt)] if t_off else rs[-1]
    i63 = next((k for k, x in enumerate(rs) if abs(x) >= 0.63 * abs(rss)), None)
    return rss, (i63 * dt - t_on) if i63 is not None else float("nan")


if __name__ == "__main__":
    jets = {
        "ifrit": Jet("ifrit", 25800, 7.07, 0.24, 4.2, 0.7, 0.8, (-1, 1), kz=5.0),
        "f22e": Jet("f22e", 25000, 6.0, 0.18, 2.74, 1.0, 1.0, (-1, -1), kz=4.6),
    }
    for name, jet in jets.items():
        print(f"== {name}  (L {jet.L} m, nose load {jet.Nn / (jet.m * 9.81) * 100:.0f} %, mu nose {jet.mu_n})")
        print("  V m/s (kt) | pedal | yaw rate after 2 s, deg/s: stock / FBW now / matched | 63 % rise time s: stock / now / matched | stop after release (|r|<1), s: stock / now / matched")
        for V in (3, 5, 8, 12, 16, 20, 25, 35):
            for pa in (1.0, 0.5, 0.2):
                pf = lambda t, pa=pa: pa if t < 2.0 else 0.0
                out = {}
                for m in ("stock", "now", "match"):
                    rs = run(jet, V, pf, m, T=5.0)
                    rss = rs[int(1.98 / 0.02)]
                    i63 = next((k for k, x in enumerate(rs) if abs(x) >= 0.63 * abs(rss)), None)
                    after = rs[int(2.0 / 0.02):]
                    ist = next((k for k, x in enumerate(after) if abs(x) < 1.0), None)
                    out[m] = (rss, i63 * 0.02 if i63 is not None else float("nan"), ist * 0.02 if ist is not None else float("nan"))
                s, n, mm = out["stock"], out["now"], out["match"]
                print(f"  {V:4.0f} ({V * 1.944:4.0f}) | {pa:.1f} | {s[0]:6.1f} / {n[0]:6.1f} / {mm[0]:6.1f} | {s[1]:.2f} / {n[1]:.2f} / {mm[1]:.2f} | {s[2]:.2f} / {n[2]:.2f} / {mm[2]:.2f}")
