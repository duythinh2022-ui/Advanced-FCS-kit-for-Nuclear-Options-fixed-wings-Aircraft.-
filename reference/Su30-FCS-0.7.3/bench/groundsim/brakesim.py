#!/usr/bin/env python3
# Su-30 0.5.3 ground bench: release stops with the nose-wheel INDI (0.5.2) vs the 0.5.3 additions
#  A  light-nose hold: while the nose tyre can't be used by INDI, the wheel tracks the contact velocity + the last INDI slip
#     (0.5.2: command 0, or the stock law's angle while the leg still reads weight-on-wheel but carries ~no load)
#  B  the main brakes as a second INDI yaw effector: nose first, the outside main's brake takes the yaw acceleration the
#     nose can't reach; leg brake = max(pilot + stock differential term, FCS command); authority faded with ground speed
# Plant = gsim2's IL-exact tyre (LandingGear.FixedUpdate) plus what this release needs from the IL:
#  - brakeStrength = Lerp(brakeStrength, b, 5 dt)  (0.2 s lag), b = brake (+ Clamp01(yaw x 0.3 x dbf) for 3-30 m/s)
#  - the steering slew toward yaw x lock runs every tick, contact or not (castor only with contact)
#  - longitudinal state: m dV/dt = sum of tyre fx (brake + scrub), no thrust after release (both builds the same)
# Run: python3 brakesim.py   (no arguments; prints the tables used in the 0.5.3 notes)
import math, sys
import numpy as np
sys.path.insert(0, ".")
import gsim2
from gsim2 import tyre_il
D2R, R2D, G = math.pi/180, 180/math.pi, 9.81

class Su30:
    m = 23900.0; L = 5.9; nfrac = 0.20; track = 3.52; mu_n = 1.0; mu_m = 0.9
    dbf = (-1.0, 1.0); lock = 60.0; slew = 60.0; align = 5.0; Iz = 560000.0
    def __init__(s, **kw):
        for k, v in kw.items(): setattr(s, k, v)
        s.b = s.nfrac*s.L; s.a = s.L - s.b
        W = s.m*G; s.Nn = W*s.nfrac; s.Nm = (W - s.Nn)/2

class Plant:
    def __init__(s, j, V, dt):
        s.j = j; s.dt = dt; s.V = V; s.vy = 0.0; s.r = 0.0; s.psi = 0.0; s.delta = 0.0; s.t = 0.0
        s.bs = [0.0, 0.0]      # brakeStrength L, R
        s.nload = 1.0          # nose-load factor (light-nose events)
    def contact(s):
        j = s.j
        vn = (s.V, s.vy + s.r*j.a)
        # body x fwd, y right, r + = nose right: point (x, y) moves with v + r*(-y, x)  ->  vx = V - r*y, vy = vy + r*x
        vl = (s.V + s.r*j.track/2, s.vy - s.r*j.b)
        vr = (s.V - s.r*j.track/2, s.vy - s.r*j.b)
        return vn, vl, vr
    def step(s, nws_cmd, brake_tgt):
        j = s.j; dt = s.dt
        vn, vl, vr = s.contact()
        noseOn = s.nload > 1e-3   # tyre force
        contact = s.nload != 0.0  # castor needs contact
        if contact and s.V > 1.0:
            vel_ang = math.atan2(vn[1], vn[0])*R2D
            s.delta += (vel_ang - s.delta)*min(s.V*0.1, 10.0)*j.align*dt
        s.delta += max(-j.slew*dt, min(j.slew*dt, nws_cmd - s.delta))
        s.delta = max(-j.lock, min(j.lock, s.delta))
        for i in (0, 1):
            s.bs[i] += (brake_tgt[i] - s.bs[i])*min(1.0, dt*5.0)
        Nn = j.Nn*max(0.0, s.nload)
        Nm = j.Nm + (j.Nn - Nn)*0.5                   # the load the nose sheds goes to the mains
        fxn, fyn, _ = tyre_il(vn, s.delta, Nn, j.mu_n) if noseOn else (0.0, 0.0, 0.0)
        fxl, fyl, _ = tyre_il(vl, 0.0, Nm, j.mu_m, s.bs[0])
        fxr, fyr, _ = tyre_il(vr, 0.0, Nm, j.mu_m, s.bs[1])
        Fx = fxn + fxl + fxr; Fy = fyn + fyl + fyr
        Mz = fyn*j.a - (fyl + fyr)*j.b - fxl*(-j.track/2) - fxr*(j.track/2)
        s.vy += (Fy/j.m - s.V*s.r)*dt
        s.V = max(0.0, s.V + (Fx/j.m + s.vy*s.r)*dt)
        s.r += Mz/j.Iz*dt
        s.psi += s.r*dt; s.t += dt
        return Nm

class Ctl:
    K = 15.0; tau = 0.04; satM = 1.2
    def __init__(s, j, lightHold=False, brake=False, bMax=1.0, vFull=7.7, vZero=12.9, Tstock=5.0):
        s.j = j; s.lightHold = lightHold; s.brake = brake; s.bMax = bMax; s.vFull = vFull; s.vZero = vZero; s.T = Tstock
        s.rprev = None; s.rdf = 0.0; s.Ff = 0.0; s.bF = [0.0, 0.0]
        s.lnHold = False; s.lastSlip = 0.0; s.hold = False; s.brakeT = 0.0; s.psiRef = 0.0; s.rcmd = 0.0
        s.indiOn = False; s.bcmd = [0.0, 0.0]
    def step(s, p, ped, dt):
        j = s.j; V = p.V; r = p.r*R2D
        rd = 0.0 if s.rprev is None else (r - s.rprev)/dt
        s.rprev = r
        a = dt/(s.tau + dt)
        s.rdf += (rd - s.rdf)*a
        vn, vl, vr = p.contact()
        alphaC = math.atan2(vn[1], vn[0])*R2D
        sp = math.hypot(*vn)
        k = 0.2 + 0.01*sp
        Nn = j.Nn*max(0.0, p.nload)
        noseWow = p.nload != 0.0
        muN = j.mu_n*Nn
        usable = noseWow and muN > 1.0 and sp >= 0.5
        fnow = max(-1, min(1, max(-10, min(10, p.delta - alphaC))*k))*muN if usable else 0.0
        # (+ = force to the left of the contact velocity in this sign convention: slip delta-alpha > 0 pushes the nose right)
        s.Ff += (fnow - s.Ff)*a
        yawPerN = j.a/j.Iz*R2D if usable else 0.0      # deg/s^2 per N (force + = nose right)
        # brake effectiveness per leg, measured brake now
        for i in (0, 1): s.bF[i] += (p.bs[i] - s.bF[i])*a
        Nm = j.Nm + (j.Nn - Nn)*0.5
        e = []
        for i, vc in enumerate((vl, vr)):
            spm = math.hypot(*vc); y = (-j.track/2, j.track/2)[i]; x = -j.b
            fx, fy = -vc[0]/spm, -vc[1]/spm
            e.append((x*fy - y*fx)*j.mu_m*Nm/j.Iz*R2D)
        # stock differential term (the stock FBW yaw on the mains in the stock phase)
        ys = -max(-1.0, min(1.0, s.T*(r*D2R) - s.T*ped)) if abs(ped) <= 0.05 else ped
        stock = [0.0, 0.0]
        if 3.0 < V < 30.0:
            stock = [max(0.0, min(1.0, ys*0.3*j.dbf[i])) for i in (0, 1)]
        s.bcmd = [0.0, 0.0]
        if abs(ped) > 0.05 or V < 1.5:
            s.hold = False; s.brakeT = 0.0; s.lnHold = False; s.indiOn = False
            return ped*j.lock, stock
        if not s.hold:
            s.brakeT += dt
            if abs(r) < 0.7 or s.brakeT > 2.0:
                s.hold = True; s.psiRef = p.psi*R2D; s.rcmd = r
        if not s.hold:
            nu = -s.K*r
        else:
            e_psi = s.psiRef - p.psi*R2D
            old = s.rcmd
            s.rcmd += max(-20*dt, min(20*dt, max(-4, min(4, e_psi)) - s.rcmd))
            nu = s.K*(s.rcmd - r) + (s.rcmd - old)/dt
            stock = [max(0.0, min(1.0, ped*0.3*j.dbf[i])) for i in (0, 1)] if 3 < V < 30 else [0.0, 0.0]
        dnu = nu - s.rdf
        fade = min(1.0, max(0.0, (s.vZero - V)/(s.vZero - s.vFull))) if s.brake else 0.0
        if fade > 0:
            # B: wanted total effector yaw acceleration next tick; the nose tyre's grip first, the outside main's brake
            # takes only what lies beyond it; the nose then does the rest incrementally, crediting the brakes'
            # predicted value next tick (the game lags brakeStrength 0.2 s)
            Mb = sum(s.bF[i]*e[i] for i in (0, 1))
            Mtot = s.Ff*yawPerN + Mb + dnu
            nCap = muN*yawPerN if usable else 0.0
            Mb_t = Mtot - max(-nCap, min(nCap, Mtot))
            for i in (0, 1):
                if Mb_t*e[i] > 0: s.bcmd[i] = min(s.bMax*fade, Mb_t/e[i])
            tgt = [max(stock[i], s.bcmd[i]) for i in (0, 1)]
            MbP = sum((s.bF[i] + (tgt[i] - s.bF[i])*min(1.0, 5*dt))*e[i] for i in (0, 1))
            nuNose = Mtot - MbP
        else:
            tgt = list(stock)
        if usable:
            fDes = (nuNose/yawPerN) if fade > 0 else (s.Ff + dnu/yawPerN)
            sMax = s.satM/k
            slip = max(-sMax, min(sMax, fDes/(muN*k)))
            d = max(-j.lock, min(j.lock, alphaC + slip))
            s.lastSlip = slip; s.lnHold = True; s.indiOn = True
        else:
            if s.lightHold and s.lnHold:
                d = max(-j.lock, min(j.lock, alphaC + s.lastSlip))
            else:
                s.lnHold = False
                d = (ys*j.lock if noseWow else 0.0) if not s.hold else 0.0
        blend = min(1.0, max(0.0, (V - 1.5)/1.5))
        nws = (1 - blend)*ped*j.lock + blend*d
        return nws, tgt

def stop(V0, pedal=1.0, Tturn=2.5, Tafter=4.0, dt=1/60, light=None, **kw):
    j = Su30(); p = Plant(j, V0, dt); c = Ctl(j, **kw)
    # stock full-pedal turn at constant speed
    while p.t < Tturn - 1e-9:
        n, b = c.step(p, pedal, dt); V = p.V; p.step(n, b); p.V = V
    t0 = p.t; r0 = p.r*R2D; psi0 = p.psi; V_rel = p.V; ts = None; out = []
    while p.t < Tturn + Tafter - 1e-9:
        tr = p.t - t0
        ped = pedal*max(0.0, 1 - tr/0.15)            # release over 0.15 s
        if light:
            t1, t2 = light
            if t1 <= tr < t1 + 0.05: p.nload = -1.0           # in contact, damping force negative: muN 0 (log: 3 ticks)
            elif t1 + 0.05 <= tr < t2: p.nload = 0.0          # off the ground
            elif t2 <= tr < t2 + 0.15: p.nload = (tr - t2)/0.15
            else: p.nload = 1.0
        n, b = c.step(p, ped, dt); p.step(n, b)
        rr = p.r*R2D
        out.append((tr, rr, p.delta, n, p.bs[0], p.bs[1], p.V, math.degrees(math.atan2(p.vy, max(p.V, .1))), c.bcmd[0], c.bcmd[1], c.rdf))
        if ts is None and abs(rr) < 2.0: ts = tr
    o = np.array(out)
    after = o[o[:, 0] >= (ts or 9)]
    over = float(np.max(-np.sign(r0)*after[:, 1])) if len(after) else float('nan')
    return dict(r0=r0, ts=ts if ts else float('nan'), dpsi=(p.psi - psi0)*R2D, over=over, dV=(V_rel - o[min(len(o)-1, int(((ts or 3)+0.3)/dt)), 6]),
                Vrel=V_rel, bmax=float(np.max(o[:, 4:6])), betaMax=float(np.max(np.abs(o[:, 7]))), o=o)

if __name__ == "__main__":
    KT = 1.944
    builds = [("0.5.2", dict()), ("A", dict(lightHold=True)), ("A+B", dict(lightHold=True, brake=True))]
    print("Su-30 release stops after a 2.5 s stock full-pedal turn (gsim2 IL tyre, brake lag 0.2 s, speed free after release)")
    print("cols: yaw rate at release | time to |r| < 2 deg/s | heading change after release | swing-back past zero (deg/s) | speed lost by 0.3 s after the stop (kt) | peak brake | peak sideslip")
    for Vk in (7, 10, 17, 23):
        V = Vk/KT
        for lab, kw in builds:
            R = stop(V, **kw)
            print(f" {Vk:2d} kt {lab:6s} r0 {R['r0']:5.1f} | stop {R['ts']:.2f} s | dpsi {R['dpsi']:5.1f} | back {R['over']:4.2f} | dV {R['dV']*KT:4.1f} kt | b {R['bmax']:.2f} | beta {R['betaMax']:4.1f}")
