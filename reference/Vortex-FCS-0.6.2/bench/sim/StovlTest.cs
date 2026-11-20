// Vortex 0.1.0 bench: STOVL flight cases (conversion, deceleration to the hover, hover, transition back, reconversion).
//   bench.dll stovl ias=250 alt=300 gamma0=0 T=60 thr=t:v,... pitch=t:v,... roll=.. yaw=.. tog=t1,t2,.. print=0.5 csv=out.csv fcslog=x.csv
//   ias km/h at the start (trimmed level flight, conventional); thr = the pilot throttle profile (start = trim if omitted);
//   tog = times of a Flight Assist hold (conversion / reconversion request). Flat ground at 0 m (radar altitude = height);
//   no ground contact is modelled (the cases stay airborne).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class StovlTest
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
    static string S(Dictionary<string, string> kv, string k) => kv.TryGetValue(k, out var v) ? v : null;
    public static bool HoldWheelsOn = !(Environment.GetEnvironmentVariable("BENCH_NOHOLD") == "1");
    static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }

    public static readonly string[] Keys = { "ias", "cond", "alt", "gamma0", "T", "thr", "pitch", "roll", "yaw", "tog", "print", "csv", "fcslog", "wind", "dump", "ground", "contactY", "hdg0", "mass", "tdtest", "kick", "spool", "mpo", "drop", "gearup", "shift" };

    public static void Run(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 300f), ias = F(kv, "ias", 250f), T = F(kv, "T", 60f), pr = F(kv, "print", 1f), g0 = F(kv, "gamma0", 0f);
        string thrP = S(kv, "thr"), pitchP = S(kv, "pitch"), rollP = S(kv, "roll"), yawP = S(kv, "yaw");
        var togs = (S(kv, "tog") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => float.Parse(x, IC)).ToList();
        if (kv.TryGetValue("mass", out var msv)) Airframe.MassTarget = float.Parse(msv, IC);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A);
        bool ground = F(kv, "ground", 0f) > 0.5f;
        float rho = Plant.Density(alt);
        float tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float aT = 0f, thrTrim = 0f, hGear = 0f;
        float[] legN = null, legMax = null; bool[] legBroken = null; List<(LandingGear g, Vector3 pB)> legs = null;
        if (ground)
        {
            // Ground.cs's spring-damper legs (ground plane y = 0) at the gear's unsprung points, contact height contactY
            A.ac.gearDeployed = true;
            float contactY = F(kv, "contactY", -2.1f);
            legs = new List<(LandingGear g, Vector3 pB)>();
            foreach (var c in A.ac.all) if (c is LandingGear lg)
            {
                var un = lg.transform.children.FirstOrDefault(t => t.name.Contains("Unsprung") || t.name.Contains("unsprung"));
                var pp = (un != null ? un.position : lg.transform.position);
                legs.Add((lg, new Vector3(pp.x, contactY, pp.z)));
            }
            var nz = legs.OrderByDescending(l => l.pB.z).First();
            // the game's FS-20 legs (resources.assets, LandingGear): nose spring 200 kN/m, damper 20 kN s/m; mains 400 kN/m,
            // 40 kN s/m; a leg breaks once its compression passes maxCompression 0.5 m
            foreach (var l in legs) { bool n0 = l.g == nz.g; l.g.springRate = n0 ? 200000f : 400000f; l.g.dampingRate = n0 ? 20000f : 40000f; l.g.maxCompression = F(kv, "tdtest", 0f) > 0.5f ? 5f : 0.5f; }   // tdtest=1: the FCS's touchdown model sees no limit (drop tests)
            float zm = legs.Where(l => l.g != nz.g).Average(l => l.pB.z), zn = nz.pB.z, zc = A.cgB.z;
            float W = A.mass * 9.81f, Nn = W * (zc - zm) / (zn - zm), Nm = (W - Nn) / 2f;
            float cn = Nn / nz.g.springRate, cm = Nm / 400000f;
            for (int i = 0; i < legs.Count; i++) if (legs[i].g == nz.g) legs[i] = (legs[i].g, legs[i].pB + Vector3.up * (cm - cn));
            hGear = -contactY + A.cgB.y - cm;
            P.Init(0f, Mathf.Max(0.01f, ias / 3.6f), 0f, 0f);
            P.pCg = new Vector3(0f, hGear, 0f); P.vCg = new Vector3(0f, 0f, ias / 3.6f);
            thrTrim = 0f;
            legN = new float[legs.Count]; legMax = new float[legs.Count]; legBroken = new bool[legs.Count];
            var L0 = legs;
            P.ext = pl =>
            {
                Vector3 Fg = Vector3.zero, Mg = Vector3.zero;
                for (int li = 0; li < L0.Count; li++)
                {
                    var (g, pB) = L0[li]; legN[li] = 0f;
                    Vector3 pw = pl.pCg + pl.q * (pB - A.cgB);
                    float pen = -pw.y;
                    g.compression = Mathf.Clamp(pen, 0f, Ground.Travel);
                    if (pen <= 0f) { g.compressionForce = 0f; continue; }
                    if (pen > legMax[li]) legMax[li] = pen;
                    if (pen > 0.5f && !legBroken[li]) { legBroken[li] = true; Console.WriteLine($"   [t {pl.time - 1f:F2}] GEAR BREAK leg {li} ({(g == nz.g ? "nose" : "main")}) compression {pen:F3} m"); }
                    Vector3 vc = pl.vCg + Vector3.Cross(pl.w, pw - pl.pCg);
                    float N = Mathf.Max(0f, g.springRate * pen - g.dampingRate * vc.y);
                    g.compressionForce = g.springRate * pen;
                    Vector3 fwd = pl.q * Vector3.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, fwd);
                    float vf = Vector3.Dot(vc, fwd), vs = Vector3.Dot(vc, side);
                    float brk = A.ac.inputs.brake;
                    float mu = Ground.Roll + brk * 0.6f;
                    Vector3 f = Vector3.up * N - fwd * mu * N * Mathf.Clamp(vf / 0.5f, -1f, 1f) - side * Ground.Mu * N * Mathf.Clamp(vs / 0.3f, -1f, 1f);
                    legN[li] = N;
                    Fg += f; Mg += Vector3.Cross(pw - pl.pCg, f);
                }
                return (Fg, Mg);
            };
        }
        else
        {
            aT = Program.TrimAlpha(P, alt, tas, out thrTrim);
            P.Init(alt, tas, aT, aT + g0);
        }
        P.throttle = thrTrim;
        P.CreateFcs();
        if (P.stovl == null || P.fcs.stovl == null) { Console.WriteLine("stovl: no lift system on this airframe / STOVL off"); return; }
        StovlSystem.Report = (a, s) => Console.WriteLine($"   [HUD t {P.time - 1f:F2}] {s}");
        if (kv.TryGetValue("wind", out var wv)) { var w = wv.Split(',').Select(x => float.Parse(x, IC)).ToArray(); A.ac.windVelocity = new Vector3(w[0], 0f, w[1]); }
        foreach (var e in A.engines) e.rpm = Mathf.Lerp(e.minRPM, e.maxRPM, Plant.ThrottleOf(e, P.throttle));
        float trimT = 1f;
        var inp = A.ac.inputs; inp.customAxis1 = 1f;
        StreamWriter fw = null, cw = null;
        if (kv.TryGetValue("fcslog", out var fl)) { fw = new StreamWriter(fl); P.fcs.WriteHeader(fw); fw.WriteLine(); }
        // surface deflection as trailing edge down (elevators / ailerons) or trailing edge outboard (rudders), deg
        float SNow(string n)
        {
            var root = ((UnityEngine.Component)(object)P.fcs.ac).transform;
            foreach (var se in P.fcs.surfaces)
            {
                if (se.name != n) continue;
                if (n.StartsWith("rudder")) return se.GeoTeOut(root) * se.now;
                return -se.GeoSignOnly(root) * se.now;   // (0.4.9: the geometry's sign, not the learned one, so a wrongly learned sign shows)
            }
            return float.NaN;
        }
        if (kv.TryGetValue("csv", out var cp)) { cw = new StreamWriter(cp); cw.WriteLine("t,state,law,ias,gs,vs,alt,theta,alpha,phi,beta,psi,p,q,r,thrIn,thrEng,rpm,swivel,doors,nozzleAng,fanKN,postL,postR,nozKN,panelKN,fanIn,postIn,yawIn,gsCmd,hdotCmd,thetaRef,alphaRef,phiCmd,thrustReq,swivelTgt,pos_x,pos_z,gamRef,gam,nReqKN,lNowKN,sCLa,authP,authQ,authR,aUp,sinkLim,climbLim,gVy,tMx,surfW,elevL,elevR,ailL,ailR,rudL,rudR,abAuto,airBrake,abOpen,tauV,wPed,aDesV,accY,wC,fpKN,sCtol,qCmd,sp,pCmd,rCmd,aHlevel,aVres,aUpBody,aTiltLeft,lamL,lamB,thetaAssist,phiTrc,vSide,uFwd,kRollS,govLam,rollWeak,slipStiff,lamV,lamT,mass,tiltRateA,tiltRate,tiltRateU,aStop,trcVmax,wz,tiltPitchA,kBank,authX,rollFull,aRollP,aRollN,fwdCap,psiRate,yawLim,aH,originJumps,vane,vaneNow,vaneBest,nozSide,kSide,psiJ,aPsiJ,kPiv,vPiv,pivX,pivZ,vShare,gLift,wingLift,twHover,vMinFloor,vTouch,rateBud,aLag,twMan,hInt,sCL0,sCL0Known"); }
        Console.WriteLine(ground ? $"stovl case: on the ground (CG {hGear:F2} m), {ias:F0} km/h, mass {A.mass:F0} kg" : $"stovl case: start IAS {ias:F0} km/h, alt {alt:F0} m, trim alpha {aT:F1}, throttle {thrTrim:F2}, mass {A.mass:F0} kg");
        Console.WriteLine($"{"t",6} {"st",4} {"ias",5} {"gs",5} {"vs",5} {"alt",6} {"th",5} {"al",5} {"ph",5} {"psi",5} {"thIn",5} {"thE",5} {"rpm",4} {"sw",4} {"dr",4} {"fan",5} {"pL",4} {"pR",4} {"noz",5} {"pnl",4} {"gsC",5} {"hdC",5} {"thR",5} {"mode",-6} {"gearN",5}");
        float nextPr = 0f; int ti = 0; Vector3 p0 = P.pCg;
        if (kv.TryGetValue("spool", out var spv)) Plant.SpoolScale = float.Parse(spv, IC);
        float[] mpoV = kv.TryGetValue("mpo", out var mpv) ? mpv.Split(',').Select(x => float.Parse(x, IC)).ToArray() : null;
        string[] dropS = kv.TryGetValue("drop", out var dps) ? dps.Split(',') : null; bool dropped = false;
        float[] kickV = kv.TryGetValue("kick", out var kvs) ? kvs.Split(',').Select(x => float.Parse(x, IC)).ToArray() : null; bool kicked = false; bool teupA = false, teupB = false;
        float[] shiftV = kv.TryGetValue("shift", out var shs) ? shs.Split(',').Select(x => float.Parse(x, IC)).ToArray() : null; bool shifted = false;
        float[] condV = kv.TryGetValue("cond", out var cdv) ? cdv.Split(',').Select(x => float.Parse(x, IC)).ToArray() : null;   // cond=t,c[,t2,c2...]: the engine's condition (damage) from t
        float maxAbsPhi = 0f, maxAbsBeta = 0f, minAlt = 1e9f, maxAlt = -1e9f;
        while (P.time < trimT + T)
        {
            float t = P.time - trimT;
            float ps = t >= 0 ? IfritFly.Prof(pitchP, t, 0f) : 0f, rs = t >= 0 ? IfritFly.Prof(rollP, t, 0f) : 0f, ys = t >= 0 ? IfritFly.Prof(yawP, t, 0f) : 0f;
            inp.pitch = -ps; inp.roll = rs; inp.yaw = ys; inp.brake = (HoldWheelsOn && P.fcs.stovl != null && P.fcs.stovl.HoldWheels) ? 1f : 0f;   // 0.2.8: the game-side hook's wheel hold (Hooks.GearPrefix), bench stand-in
            if (thrP != null && t >= 0) P.throttle = IfritFly.Prof(thrP, t, P.throttle);
            while (ti < togs.Count && t >= togs[ti]) { P.fcs.stovl.RequestToggle(P.time, P.fcs.ias); ti++; }
            // kick=t,vRight,vFwd: adds ground velocity (m/s, heading axes) once at t, e.g. a sideways skid
            // mpo=t0,t1: flight assist off (STOVL freestyle) from t0 to t1
            if (mpoV != null) Plant.BenchMpo = t >= mpoV[0] && t < mpoV[1];
            if (condV != null) for (int ci = 0; ci + 1 < condV.Length; ci += 2) if (t >= condV[ci]) A.engines[0].condition = Mathf.Min(A.engines[0].condition, condV[ci + 1]);
            // drop=t,kg,part: a store's mass comes off a part at t (a weapon release; mass properties recomputed)
            if (dropS != null && !dropped && t >= float.Parse(dropS[0], IC)) { dropped = true; var dp = (AeroPart)A.ac.partLookup.First(u => u.name == dropS[2]); dp.mass -= float.Parse(dropS[1], IC); foreach (var bD in A.bodies) if (bD.part == dp) bD.rb.mass = dp.mass; A.MassProps(); Console.WriteLine($"   [t {t:F2}] drop: -{dropS[1]} kg at {dropS[2]}, mass now {A.mass:F0} kg"); }
            // shift=t,dx,dz: the game's floating-origin shift: every position moves by (dx, 0, dz) in one tick, the velocities stay
            if (shiftV != null && !shifted && t >= shiftV[0]) { shifted = true; Vector3 dS = new Vector3(shiftV[1], 0f, shiftV[2]); P.pCg += dS; p0 += dS; Console.WriteLine($"   [t {t:F2}] origin shift: {shiftV[1]:F0} m x, {shiftV[2]:F0} m z"); }
            // TEUP=1: print each surface's learned and geometric trailing-edge-up sign once at 2 s and at the end
            if (Environment.GetEnvironmentVariable("TEUP") == "1" && ((t >= 2f && !teupA) || (t >= T - 0.05f && !teupB))) { if (t < T - 0.05f) teupA = true; else teupB = true; var rootT = ((Component)(object)A.ac).transform; Console.WriteLine($"   [t {t:F2}] teUp learned / geometry: " + string.Join(" ", P.fcs.surfaces.Select(se => $"{se.name}:{se.teUp:F0}/{se.GeoSignOnly(rootT):F0}"))); }
            if (kickV != null && !kicked && t >= kickV[0]) { kicked = true; Vector3 fh = P.q * Vector3.forward; fh.y = 0f; fh.Normalize(); Vector3 rh = new Vector3(fh.z, 0f, -fh.x); P.vCg += rh * kickV[1] + fh * kickV[2]; Console.WriteLine($"   [t {t:F2}] kick: +{kickV[1]:F1} m/s right, +{kickV[2]:F1} m/s fwd"); }
            A.ac.radarAlt = P.pCg.y - hGear;
            P.Step();
            if (float.IsNaN(P.vCg.x)) { Console.WriteLine("NaN"); break; }
            if (fw != null) { P.fcs.WriteRow(fw, t, ps, rs, ys, rho); fw.WriteLine(); }
            if (kv.TryGetValue("gearup", out var guv) && t >= float.Parse(guv, IC)) A.ac.gearDeployed = false;
            if (kv.TryGetValue("dump", out var dv) && Math.Abs(t - float.Parse(dv, IC)) < 0.5f * P.dt)
            {
                var c = P.fcs; Console.WriteLine($"   dump t {t:F2}: I diag (pitch,yaw,roll) {A.Itot[0,0]:F0} {A.Itot[1,1]:F0} {A.Itot[2,2]:F0}; effector dw (deg/s^2, aero p/q/r) at lo / hi vs now");
                foreach (var ef in c.effs)
                {
                    if (!ef.active) continue;
                    Vector3 W(Vector3 m) => new Vector3(m.x * c.IaInv[0, 0], m.y * c.IaInv[1, 1], m.z * c.IaInv[2, 2]) * Mathf.Rad2Deg;
                    var lo = W(ef.MAt(ef.lo) - ef.MAt(ef.now)); var hi = W(ef.MAt(ef.hi) - ef.MAt(ef.now));
                    Console.WriteLine($"     {ef.name,-12} np{((ef is SurfaceEffector seN && seN.noPitch) ? 1 : 0)} now {ef.now,6:F2} [{ef.lo,6:F1},{ef.hi,6:F1}]  lo ({lo.x,7:F1},{lo.y,7:F1},{lo.z,7:F1})  hi ({hi.x,7:F1},{hi.y,7:F1},{hi.z,7:F1})");
                }
                Console.WriteLine($"     roll stop power (weak share {P.fcs.stovl.dbgRollWeak:F3}): toward + {P.fcs.stovl.dbgRollStop.x:F2}, toward - {P.fcs.stovl.dbgRollStop.y:F2} deg/s^2, load {P.fcs.stovl.dbgRollStop.z:F2}, authority (roll window, roll whole travel, yaw) {P.fcs.stovl.dbgAuthRaw.x:F1} {P.fcs.stovl.dbgAuthRaw.y:F1} {P.fcs.stovl.dbgAuthRaw.z:F1}");
                { var sg = P.fcs.stovl; bool okG = sg.HoverRollTrim(P.fcs.MassKg, P.fcs.CgNow, out float nG, out float lG, out float yG); float mxG = 0f; if (sg.post != null && sg.post.M != null) foreach (var mm in sg.post.M) mxG = Mathf.Max(mxG, Mathf.Abs(mm.x)); Console.WriteLine($"     guard: estimate ok {okG}, trim needed {nG:F0} N m, limit {lG:F0} (hover estimate) vs {0.75f * mxG:F0} (75 % of the live post table), all effectors {P.fcs.RollMomentAuthority():F0}, CG {yG:F4} m off the lift line, state {sg.state}"); }
                for (int j = 0; j < P.stovl.Posts; j++) Console.WriteLine($"     post {j} pos (body) {Quaternion.Inverse(P.q) * (P.stovl.PostT(j).position - P.pCg)} fwd (body) {Quaternion.Inverse(P.q) * P.stovl.PostT(j).forward}");
                Console.WriteLine($"     fan pos (body) {Quaternion.Inverse(P.q) * (P.stovl.FanThrustT.position - P.pCg)} fwd {Quaternion.Inverse(P.q) * P.stovl.FanThrustT.forward}; nozzle pos {Quaternion.Inverse(P.q) * (P.stovl.MainThrustT.position - P.pCg)} fwd {Quaternion.Inverse(P.q) * P.stovl.MainThrustT.forward}");
            }
            var st = P.fcs.stovl; var sv = P.stovl;
            var vb = Quaternion.Inverse(P.q) * (P.vCg - A.ac.windVelocity); float V = vb.magnitude;
            float alpha = V > 1f ? Mathf.Atan2(-vb.y, vb.z) * Mathf.Rad2Deg : 0f, beta = V > 1f ? Mathf.Asin(Mathf.Clamp(vb.x / V, -1, 1)) * Mathf.Rad2Deg : 0f;
            float iasNow = V * Mathf.Sqrt(Plant.Density(P.pCg.y) / 1.225f) * 3.6f;
            float gs = new Vector2(P.vCg.x, P.vCg.z).magnitude;
            var e = P.q.eulerAngles; float th = -Norm(e.x), ph = -Norm(e.z), psi = e.y;
            var rt = P.RatesDeg();
            var tf = A.engines[0];
            float nozAng = 0f; { var d = sv.MainThrustT.forward; var db = Quaternion.Inverse(P.q) * d; nozAng = Mathf.Atan2(-db.y, db.z) * Mathf.Rad2Deg; }
            if (t >= 0)
            {
                if (st.state == StovlState.On) { maxAbsPhi = Mathf.Max(maxAbsPhi, Mathf.Abs(ph)); maxAbsBeta = Mathf.Max(maxAbsBeta, V > 5f ? Mathf.Abs(beta) : 0f); minAlt = Mathf.Min(minAlt, P.pCg.y); maxAlt = Mathf.Max(maxAlt, P.pCg.y); }
                float thrEng = sv.EngineThrottle(A.ac.inputs.throttle);
                if (cw != null)
                    cw.WriteLine(string.Join(",", new float[] { t, (int)st.state, P.fcs.stovlLawWas ? 1 : 0, iasNow, gs * 1.94384f, P.vCg.y, P.pCg.y, th, alpha, ph, beta, psi, rt.x, rt.y, rt.z, A.ac.inputs.throttle, thrEng, tf.rpm, sv.swivel, sv.doors, nozAng, sv.fanForce * 1e-3f, sv.postL, sv.postR, sv.NozzleThrust * 1e-3f, sv.panelDrag * 1e-3f, st.fanIn, st.postIn, st.yawIn, st.dbgGsCmd, st.dbgHdotCmd, st.dbgThetaRef, st.dbgAlphaRef, st.dbgPhiCmd, st.dbgThrustReq * 1e-3f, st.dbgSwivelTgt, P.pCg.x - p0.x, P.pCg.z - p0.z, st.dbgGamRef, st.dbgGam, st.dbgNReq * 1e-3f, st.dbgLNow * 1e-3f, st.dbgSCLa, st.dbgAuth.x, st.dbgAuth.y, st.dbgAuth.z, st.dbgAUp, st.dbgSinkLim, st.dbgClimbLim, st.dbgGVy, st.dbgTMx, P.fcs.stovlSurfW, SNow("elevator_L"), SNow("elevator_R"), SNow("aileron_L"), SNow("aileron_R"), SNow("rudder_L"), SNow("rudder_R"), st.brakeAuto, P.fcs.airBrake, A.airbrakes.Count > 0 ? A.airbrakes[0].openAmount : -1f, st.dbgTauV, st.dbgWPed, st.dbgADesV, P.fcs.stovlAccY, st.CtolW, st.dbgCtolFp * 1e-3f, st.dbgCtolS, P.fcs.qCmd, P.fcs.sp, P.fcs.pCmd, P.fcs.rCmd, st.dbgAHlevel, st.dbgAVres, st.dbgAUpBody, st.dbgATiltLeft, st.dbgLamL, st.dbgLamB, st.dbgThetaAssist, st.dbgPhiTrc, st.dbgVSide, st.dbgUFwd, st.dbgKRollS, st.dbgGovLam, st.dbgRollWeak, P.fcs.slipStiffDbg, st.dbgLamV, st.dbgLamT, A.mass, st.dbgTiltRateA, st.dbgTiltRate, st.dbgTiltRateU, st.dbgAStop, st.dbgTrcVmax, st.dbgWz, st.dbgTiltPitchLift, st.dbgKBank, st.dbgAuthRaw.x, st.dbgAuthRaw.y, st.dbgRollStop.x, st.dbgRollStop.y, st.dbgFwdCap, st.dbgPsiRate, st.dbgYawLim, st.dbgAH, st.dbgOriginJumps, st.vaneLive ? st.vaneOut : sv.VaneNow, sv.VaneNow, st.dbgVaneBest, st.dbgNozSide, st.dbgKSide, st.dbgPsiJ, st.dbgAPsiJ, st.dbgKPiv, st.dbgVPiv, st.dbgPivX, st.dbgPivZ, st.dbgVShare, st.dbgGLift, st.dbgWingLift, st.dbgTwHover, st.dbgVMinFloor * 1.944f, st.dbgVTouch, st.dbgRateBud, st.dbgALag, st.dbgTwMan, st.dbgHInt, st.dbgSCL0, st.dbgSCL0Known }.Select(x => x.ToString("0.###", IC))));
                if (t >= nextPr - 1e-4f)
                {
                    nextPr += pr;
                    string sn = st.state == StovlState.Off ? "CTOL" : st.state == StovlState.On ? (P.fcs.stovlLawWas ? "STVL" : "On") : st.state == StovlState.Converting ? "CONV" : "RECV";
                    Console.WriteLine($"{t,6:F1} {sn,4} {iasNow,5:F0} {gs * 1.94384f,5:F1} {P.vCg.y,5:F1} {P.pCg.y,6:F1} {th,5:F1} {alpha,5:F1} {ph,5:F1} {psi,5:F0} {A.ac.inputs.throttle,5:F2} {thrEng,5:F2} {tf.rpm / tf.maxRPM * 100f,4:F0} {sv.swivel,4:F2} {sv.doors,4:F2} {sv.fanForce * 1e-3f,5:F1} {sv.postL * 1e-3f,4:F1} {sv.postR * 1e-3f,4:F1} {sv.NozzleThrust * 1e-3f,5:F1} {sv.panelDrag * 1e-3f,4:F1} {st.dbgGsCmd,5:F1} {st.dbgHdotCmd,5:F1} {st.dbgThetaRef,5:F1} {P.fcs.Mode,-6} {(legN != null ? legN.Sum() * 1e-3f : 0f),5:F0}");
                }
            }
        }
        fw?.Close(); cw?.Close();
        if (legMax != null) Console.WriteLine("gear: max compression per leg " + string.Join(" ", legMax.Select(x => x.ToString("F3", IC))) + " m (break 0.5), static was about " + (A.mass * 9.81f * 0.85f / 2f / 400000f).ToString("F3", IC));
        Console.WriteLine($"summary: STOVL-on |phi| max {maxAbsPhi:F1}, |beta| max (>5 m/s) {maxAbsBeta:F1}, alt {minAlt:F1}..{maxAlt:F1}, final pos dx {P.pCg.x - p0.x:F0} dz {P.pCg.z - p0.z:F0} m");
    }
}
