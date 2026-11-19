#!/usr/bin/env python3
# Yaw-plane ground-handling sim of Nuclear Option's LandingGear (from the game's IL, LandingGear.FixedUpdate):
# lateral tyre force = clamp(slip_deg x response x (0.2 + 0.01 |v|), +-1) x mu N, slip clamped +-10 deg,
# scrub drag |sin slip| mu N, total clamped to mu N; steerable leg: castor (steeringAngle += slip x min(0.1 v, 10) x
# aligningStrength x dt) then a slew toward yaw x steeringLock at steeringSpeed. Differential brake: 0.3 x pedal on the
# main with the matching sign, 3-30 m/s. Speed is held constant (taxi throttle). No aero (rudders not modelled).
import math
import numpy as np

D2R = math.pi / 180.0
R2D = 180.0 / math.pi
G = 9.81


class Jet:
    def __init__(self, name, m, L, b_frac, track, mu_n, mu_m, diff, lock=45.0, slew=60.0, align=5.0, kz=5.5):
        self.name = name
        self.m = m
        self.L = L
        self.b = b_frac * L          # CG -> mains (positive: CG ahead of the mains)
        self.a = L - self.b          # CG -> nose
        self.track = track
        self.mu_n = mu_n
        self.mu_m = mu_m
        self.diff = diff             # (left factor, right factor)
        self.lock = lock
        self.slew = slew
        self.align = align
        self.Iz = m * kz * kz
        W = m * G
        self.Nn = W * self.b / L
        self.Nm = (W - self.Nn) / 2.0


def tyre(vloc, heading_deg, N, mu, brake=0.0):
    """vloc: (vx fwd, vy right) of the contact in body axes; heading_deg: wheel angle (+ = nose right).
    Returns force (fx, fy) in body axes and the slip angle (deg)."""
    c, s = math.cos(heading_deg * D2R), math.sin(heading_deg * D2R)
    # contact velocity in wheel axes
    vf = vloc[0] * c + vloc[1] * s
    vs = -vloc[0] * s + vloc[1] * c
    sp = math.hypot(vf, vs)
    if sp < 0.05:
        return 0.0, 0.0, 0.0
    slip = math.atan2(vs, abs(vf)) * R2D                     # + = contact sliding to the wheel's right
    slipc = max(-10.0, min(10.0, slip))
    lat = -max(-1.0, min(1.0, slipc * (0.2 + 0.01 * sp))) * mu * N   # opposes the sideways slide
    scrub = abs(math.sin(slip * D2R)) * mu * N
    # forces in wheel axes: lateral (perp. to wheel), drag opposite velocity
    fwf = -(scrub + brake * mu * N) * vf / sp
    fws = lat - (scrub + brake * mu * N) * vs / sp * 0.0
    mag = math.hypot(fwf, fws)
    if mag > mu * N:
        fwf *= mu * N / mag
        fws *= mu * N / mag
    # back to body axes
    fx = fwf * c - fws * s
    fy = fwf * s + fws * c
    return fx, fy, slip


class Sim:
    def __init__(self, jet, V, dt=0.02):
        self.j = jet
        self.dt = dt
        self.V = V
        self.vy = 0.0
        self.r = 0.0       # rad/s, + = nose right
        self.psi = 0.0
        self.delta = 0.0   # steering angle (deg)
        self.t = 0.0

    def step(self, yaw_cmd, pedal):
        j = self.j
        dt = self.dt
        # nose contact velocity (body: x fwd, y right); r positive nose right -> nose moves right
        vn = (self.V, self.vy + self.r * j.a)
        vml = (self.V, self.vy - self.r * j.b)
        vmr = vml
        # stock castor (only above 1 m/s ground speed), then the slew toward the command
        gs = self.V
        if gs > 1.0:
            # angle from the wheel's heading to its contact velocity, about up
            vel_ang = math.atan2(vn[1], vn[0]) * R2D
            self.delta += (vel_ang - self.delta) * min(gs * 0.1, 10.0) * j.align * dt
        tgt = yaw_cmd * j.lock
        self.delta += max(-j.slew * dt, min(j.slew * dt, tgt - self.delta))
        self.delta = max(-abs(j.lock), min(abs(j.lock), self.delta))
        # differential brake (main legs read the real pedal)
        bl = br = 0.0
        if 3.0 < gs < 30.0:
            bl = max(0.0, min(1.0, pedal * 0.3 * j.diff[0]))
            br = max(0.0, min(1.0, pedal * 0.3 * j.diff[1]))
        fxn, fyn, sn = tyre(vn, self.delta, j.Nn, j.mu_n)
        fxl, fyl, _ = tyre(vml, 0.0, j.Nm, j.mu_m, bl)
        fxr, fyr, _ = tyre(vmr, 0.0, j.Nm, j.mu_m, br)
        Fy = fyn + fyl + fyr
        Mz = fyn * j.a - (fyl + fyr) * j.b + (fxl - fxr) * (j.track / 2.0)  # left drag yaws nose left
        # body-axis lateral dynamics with V held: m (vy_dot + V r) = Fy
        self.vy += (Fy / j.m - self.V * self.r) * dt
        self.r += Mz / j.Iz * dt
        self.psi += self.r * dt
        self.t += dt
        return sn
