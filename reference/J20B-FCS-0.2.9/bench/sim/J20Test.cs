// J-20B 0.1.0 bench: defaults for the J-20B airframe and static pitch / lateral maps (no controller, EvalOnly).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class J20Test
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static void Defaults()
    {
        // no bench calibration exists for the J-20B (no game logs yet): every fitted factor stays neutral
        Plant.NozzleArmScale = 1f; Airframe.CgAftShift = 0f; Airframe.IScaleRoll = 1f; Airframe.IScalePitch = 1f; Airframe.IScaleYaw = 1f;
        Airframe.RudFlowKr = 0f; Airframe.RudFlowKp = 0f;
        Plant.AirbrakeAtIdle = false;
        Airframe.FuelFrac = 0.5f; Airframe.MassTarget = -1f;
    }

    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;

    public static void SetSurf(string role, float deg, string side = null)
    {
        foreach (var s in J20Bridge.All)
            if (s.role == role && (side == null || s.side == side))
            {
                J20Bridge.SyncAngle(s.raw, deg);
                s.hinge.localRotation = s.rest * Quaternion.AngleAxis(deg, Vector3.right);
            }
    }
    // geometric sign of a trailing-edge-up deflection for a surface (+1: +deg = TE up)
    public static float TeUpSign(Airframe A, string role, string side)
    {
        var s = J20Bridge.All.First(x => x.role == role && x.side == side);
        var p0 = s.hinge.position; var up = A.root.up;
        // a point 1 m aft of the hinge, in the hinge frame at +10 deg
        var save = s.hinge.localRotation;
        s.hinge.localRotation = s.rest * Quaternion.AngleAxis(10f, Vector3.right);
        Vector3 aft = s.hinge.TransformPoint(new Vector3(0f, 0f, -1f));
        s.hinge.localRotation = s.rest;
        Vector3 aft0 = s.hinge.TransformPoint(new Vector3(0f, 0f, -1f));
        s.hinge.localRotation = save;
        return Mathf.Sign(Vector3.Dot(aft - aft0, up));
    }

    // pitch map: per AoA, body pitch acceleration (deg/s^2) with surfaces as given, plus the canard / elevon authority each way
    //   j20cm ias=<km/h> alt= a0= a1= da= can=<deg, + LE down> inner=<deg TE up +> outer=<deg TE up +> thr=
    public static void Cm(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), ias = F(kv, "ias", 500f), thr = F(kv, "thr", 0f);
        float a0 = F(kv, "a0", -10f), a1 = F(kv, "a1", 70f), da = F(kv, "da", 2f);
        float can = F(kv, "can", 0f), inner = F(kv, "inner", 0f), outer = F(kv, "outer", 0f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        P.throttle = thr; A.ac.inputs.throttle = thr;
        float sIn = TeUpSign(A, "trailingInner", "L"), sOut = TeUpSign(A, "trailingOuter", "L");
        Console.WriteLine($"# J-20B pitch map  IAS {ias:F0} km/h  alt {alt:F0}  mass {A.mass:F0} kg  Iyy {A.Itot[0, 0]:F0}  CG {A.cgB.z:F3} ({A.j20CgAhead:F3} m ahead of the mains)");
        Console.WriteLine($"# {A.CgShiftLog}");
        Console.WriteLine($"# TE-up sign inner {sIn:+0;-0} outer {sOut:+0;-0}; canard + = LE down (nose down)");
        Console.WriteLine($"{"alpha",5} {"nz",5} {"q0",7} {"canUp",7} {"canDn",7} {"elevUp",7} {"elevDn",7} {"allUp",7} {"allDn",7} {"leL",5}");
        float cMax = F(kv, "canmax", 25f), eUp = F(kv, "eup", 35f), eDn = F(kv, "edn", 30f);
        for (float a = a0; a <= a1 + 1e-3f; a += da)
        {
            float Q(float c, float i, float o)
            {
                P.Init(alt, tas, a, a);
                SetSurf("canard", c); SetSurf("trailingInner", i * sIn); SetSurf("trailingOuter", o * sOut);
                SetSurf("tail", 0f);
                return IfritTest.Acc(P).y;
            }
            float q0 = Q(can, inner, outer); float nz = IfritTest.Nz(P);
            float le = J20Bridge.AngleOf(J20Bridge.All.First(x => x.role == "leading").raw);
            Console.WriteLine($"{a,5:F0} {nz,5:F2} {q0,7:F0} {Q(-cMax, inner, outer),7:F0} {Q(cMax, inner, outer),7:F0} {Q(can, eUp, eUp),7:F0} {Q(can, -eDn, -eDn),7:F0} {Q(-cMax, eUp, eUp),7:F0} {Q(cMax, -eDn, -eDn),7:F0} {le,5:F1}");
        }
    }

    // trim map: per AoA, the canard-only trim, the elevon-only trim (canard 0), and the best nose-up / nose-down available
    //   j20trim ias= alt= a0= a1= da=
    public static void Trim(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), ias = F(kv, "ias", 500f), thr = F(kv, "thr", 0f);
        float a0 = F(kv, "a0", 0f), a1 = F(kv, "a1", 60f), da = F(kv, "da", 4f);
        float cMax = F(kv, "canmax", 25f), eUp = F(kv, "eup", 35f), eDn = F(kv, "edn", 30f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        P.throttle = thr; A.ac.inputs.throttle = thr;
        float sIn = TeUpSign(A, "trailingInner", "L"), sOut = TeUpSign(A, "trailingOuter", "L");
        if (!kv.ContainsKey("quiet")) { Console.WriteLine($"# J-20B trim map  IAS {ias:F0} km/h  alt {alt:F0}  mass {A.mass:F0} kg  CG {A.j20CgAhead:F3} m ahead of the mains | {A.CgShiftLog}");
        Console.WriteLine($"{"alpha",5} {"nz",5} {"q0",6} {"canTrim",7} {"elvTrim",7} {"cBestUp",7} {"maxUp",6} {"cBestDn",7} {"maxDn",6}"); }
        for (float a = a0; a <= a1 + 1e-3f; a += da)
        {
            float Q(float c, float e)
            {
                P.Init(alt, tas, a, a);
                SetSurf("canard", c); SetSurf("trailingInner", e * sIn); SetSurf("trailingOuter", e * sOut); SetSurf("tail", 0f);
                return IfritTest.Acc(P).y;
            }
            float q0 = Q(0f, 0f); float nz = IfritTest.Nz(P);
            // canard-only trim (bisection where the sign changes on the scan)
            string Root(Func<float, float> f, float lo, float hi, float step)
            {
                float prev = f(lo), xp = lo;
                int nStep = (int)Math.Round((hi - lo) / step); for (int si = 1; si <= nStep; si++)
                {
                    float x = lo + si * step; float v = f(x);
                    if (Mathf.Sign(v) != Mathf.Sign(prev)) { float l = xp, h = x, fl = prev; for (int i = 0; i < 20; i++) { float m = 0.5f * (l + h), fm = f(m); if (Mathf.Sign(fm) == Mathf.Sign(fl)) { l = m; fl = fm; } else h = m; } return (0.5f * (l + h)).ToString("F1", IC); }
                    prev = v; xp = x;
                }
                return "-";
            }
            // canard: search from 0 outward so the nearest trim is reported
            string ct = Mathf.Abs(q0) < 0.5f ? "0.0" : (q0 < 0 ? Root(c => Q(c, 0f), 0f, -cMax, -1f) : Root(c => Q(c, 0f), 0f, cMax, 1f));
            string et = Mathf.Abs(q0) < 0.5f ? "0.0" : (q0 < 0 ? Root(e => Q(0f, e), 0f, eUp, 1f) : Root(e => Q(0f, e), 0f, -eDn, -1f));
            float bu = -999f, cu = 0f, bd = 999f, cd = 0f;
            for (float c = -cMax; c <= cMax + 1e-3f; c += 2.5f) { float u = Q(c, eUp); if (u > bu) { bu = u; cu = c; } float dn = Q(c, -eDn); if (dn < bd) { bd = dn; cd = c; } }
            Console.WriteLine($"{a,5:F0} {nz,5:F2} {q0,6:F0} {ct,7} {et,7} {cu,7:F1} {bu,6:F0} {cd,7:F1} {bd,6:F0}");
        }
    }

    // canard local AoA / lift coefficient at a body AoA for a list of deflections (plant geometry)
    public static void CanAoA(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 500f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        var can = J20Bridge.All.First(x => x.role == "canard" && x.side == "L").owner;
        foreach (float a in new[] { 20f, 28f, 36f, 40f, 44f })
        {
            Console.Write($"a {a,3:F0}: ");
            foreach (float c in new[] { -25f, -10f, 0f, 10f, 16f, 25f })
            {
                P.Init(alt, tas, a, a); SetSurf("canard", c); P.Step();
                var lt = can.liftNormal; Vector3 v = can.rb.velocity; Vector3 v6 = Quaternion.Inverse(lt.rotation) * v;
                float la = Mathf.Atan2(v6.y, v6.z) * Mathf.Rad2Deg;
                Console.Write($"c{c:+0;-0}: {la,5:F1} | ");
            }
            Console.WriteLine();
        }
    }

    // FCS canard model mid-pull: per grid deflection the FCS's pitch moment (as body pitch accel) and local AoA, with StallCost's band
    public static void EffCan(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 500f), aStop = F(kv, "astop", 38f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A);
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float aT = Program.TrimAlpha(P, alt, tas, out float thrT);
        P.Init(alt, tas, aT, aT); P.throttle = 1f; P.CreateFcs();
        var inp = A.ac.inputs;
        float tStop = F(kv, "tstop", -1f);
        for (int k = 0; k < 2000; k++) { inp.pitch = P.time > 0.5f ? (aStop < 0f ? 1f : -1f) : 0f; P.Step(); if (tStop > 0f ? P.time >= tStop : (aStop >= 0f ? P.J20AlphaDeg() >= aStop : P.J20AlphaDeg() <= aStop)) break; }
        var f = P.fcs; float Iyy = (float)A.Itot[0, 0];
        Console.WriteLine($"# t {P.time:F2} alpha {P.J20AlphaDeg():F1} mTarget.y {f.mTarget.y * f.IaInv[1, 1] * Mathf.Rad2Deg:F0} deg/s^2");
        foreach (var se in f.surfaces)
        {
            if (!se.isCanard) continue;
            Console.WriteLine($"{se.name} now {se.now:F1} cmd {se.cmd:F1} knee30 {se.KneeAoA(0.3f):F1} anchor {se.anchor:F1} band {se.band:F1} anchorK {se.anchorK:F3}");
            for (int i = 0; i < se.N; i += Math.Max(1, se.N / 12)) { float g = se.grid[i]; Console.WriteLine($"   d {g,6:F1}  localAoA {se.LocalAoAAt(g),6:F1}  Mpitch {se.MAt(g).y * f.IaInv[1, 1] * Mathf.Rad2Deg,7:F1}"); }
        }
        foreach (var e in f.effs) Console.WriteLine($"  eff {e.GetType().Name,-18} {e.name,-12} now {e.now,6:F1} cmd {e.cmd,6:F1} [{e.lo,6:F1},{e.hi,6:F1}] M(now).x {e.MAt(e.now).y * f.IaInv[1, 1] * Mathf.Rad2Deg,7:F1}");
        { float k0 = f.IaInv[1, 1] * Mathf.Rad2Deg; float sNow = 0, sMin = 0, sCmd = 0; foreach (var e in f.effs) { if (!e.active) continue; sNow += e.MAt(e.now).y * k0; sCmd += e.MAt(e.cmd).y * k0; bool el = e.name.StartsWith("aileron") || e.name.StartsWith("flap"); sMin += e.MAt(el ? e.min : e.cmd).y * k0; }
          Console.WriteLine($"# model pitch sum: now {sNow:F1} cmd {sCmd:F1} elevons-at-min {sMin:F1} | alpha {P.J20AlphaDeg():F1} ias {A.ac.speed * 3.6f:F0}"); }
        if (kv.ContainsKey("alltab")) foreach (var e in f.effs) { if (e.GetType().Name == "TvcEffector") continue; Console.Write($"  tab {e.name,-12}"); for (int k = 0; k <= 8; k++) { float d = e.min + (e.max - e.min) * k / 8f; Console.Write($" {d,5:F0}:{e.MAt(d).y * f.IaInv[1, 1] * Mathf.Rad2Deg,6:F1}"); } Console.WriteLine(); }
    }

    // lateral map: per AoA (canard at the nose-up-best for trim), stability-axis roll / yaw accel from full differential elevons and
    // from the fins; plus the pitch left with the elevons differential (pitch cost of rolling)
    public static void Lat(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), ias = F(kv, "ias", 500f);
        float a0 = F(kv, "a0", 0f), a1 = F(kv, "a1", 48f), da = F(kv, "da", 4f), dEl = F(kv, "del", 30f), dFin = F(kv, "dfin", 30f), eBias = F(kv, "ebias", 0f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float sIn = TeUpSign(A, "trailingInner", "L"), sOut = TeUpSign(A, "trailingOuter", "L");
        // fin: + angle on each side; find which sign yaws right per side
        Console.WriteLine($"# J-20B lateral map IAS {ias:F0}  elevon differential +-{dEl} about {eBias} TE up, fins {dFin}");
        Console.WriteLine($"{"alpha",5} | {"ELEV pS",7} {"rS",6} {"dq",6} | {"FIN pS",7} {"rS",6} | {"FIN+EL pS",9} {"rS",6}");
        for (float a = a0; a <= a1 + 1e-3f; a += da)
        {
            Vector3 Q(float elDiff, float finL, float finR, float cDiff = 0f)
            {
                P.Init(alt, tas, a, a);
                SetSurf("canard", F(kv, "cbias", 0f) + cDiff, "L"); SetSurf("canard", F(kv, "cbias", 0f) - cDiff, "R");
                // roll right: left elevons TE down, right TE up
                SetSurf("trailingInner", (eBias - elDiff) * sIn, "L"); SetSurf("trailingInner", (eBias + elDiff) * sIn, "R");
                SetSurf("trailingOuter", (eBias - elDiff) * sOut, "L"); SetSurf("trailingOuter", (eBias + elDiff) * sOut, "R");
                SetSurf("tail", finL, "L"); SetSurf("tail", finR, "R");
                return IfritTest.Acc(P);
            }
            var q0 = Q(0, 0, 0);
            var el = Q(dEl, 0, 0) - q0;
            // fins: both +dFin, then both -dFin; take the one that yaws right
            var f1 = Q(0, dFin, dFin) - q0; var f2 = Q(0, -dFin, -dFin) - q0;
            var fin = f1.z > f2.z ? f1 : f2; float fs = f1.z > f2.z ? dFin : -dFin;
            var both = Q(dEl, fs, fs) - q0;
            float ar = a * Mathf.Deg2Rad;
            string L(Vector3 d) => $"{d.x * Mathf.Cos(ar) + d.z * Mathf.Sin(ar),7:F0} {d.z * Mathf.Cos(ar) - d.x * Mathf.Sin(ar),6:F0}";
            var cn1 = Q(0, 0, 0, F(kv, "dcan", 15f)) - q0; var cn2 = Q(0, 0, 0, -F(kv, "dcan", 15f)) - q0;
            Console.WriteLine($"{a,5:F0} | {L(el)} {el.y,6:F0} | {L(fin)} | {L(both)} | CAN+ {L(cn1)} {cn1.y,5:F0} CAN- {L(cn2)} {cn2.y,5:F0}");
        }
    }

    // asymmetric-load model breakdown mid-pull: per part / surface, the roll and yaw (deg/s^2) AsymMomentAt sums
    public static void AsymDump(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 400f), tStop = F(kv, "tstop", 1.95f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A);
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float aT = Program.TrimAlpha(P, alt, tas, out float thrT);
        P.Init(alt, tas, aT, aT); P.throttle = 1f; P.CreateFcs();
        var inp = A.ac.inputs;
        while (P.time < 1f + tStop) { inp.pitch = P.time > 1f ? -Mathf.Clamp01((P.time - 1f) / 0.2f) : 0f; P.Step(); }
        var f = P.fcs; var rootRot = A.root.rotation;
        var ctx = new EvalContext { cg = f.cg, rootInv = Quaternion.Inverse(rootRot), rho = rho, wind = Vector3.zero, vAir = A.ac.rb.velocity };
        var tot = f.AsymMomentAt(f.alpha, rho, rootRot, ref ctx);
        Console.WriteLine($"# t {P.time:F2} alpha {f.alpha:F1} total roll {f.AccRoll(tot):F1} yaw {f.AccYaw(tot):F1}");
        float num = Mathf.Max(f.tas, 1f); float fa = f.alpha * Mathf.Deg2Rad;
        Vector3 vv = rootRot * new Vector3(0f, -Mathf.Sin(fa) * num, Mathf.Cos(fa) * num); Vector3 v2 = vv / num; float q2 = 0.5f * rho * num * num;
        foreach (var ap in f.aeroParts)
        {
            if (ap == null || ap.rb == null) continue;
            var ln = ap.liftNormal; var rot = f.AirframeRot(ap, ln);
            Vector3 lhs = ap.rb.worldCenterOfMass + rot * ap.centerOfLift - f.cg;
            Vector3 rhs = -v2 * (0.5f * q2 * ap.dragArea);
            bool surf = f.surfaceParts.Contains(ap);
            if (!surf && f.foils.TryGetValue(ap, out var tb) && ap.wingArea > 0f) rhs += FcsController.PartForce(vv, rot, rot * Vector3.right, tb, 0.5f * rho * ap.wingArea * ap.wingEffectiveness);
            var m3 = Quaternion.Inverse(rootRot) * Vector3.Cross(lhs, rhs); var m = new Vector3(-m3.z, -m3.x, m3.y);
            if (Mathf.Abs(f.AccRoll(m)) > 0.5f || Mathf.Abs(f.AccYaw(m)) > 0.5f) Console.WriteLine($"  part {ap.name,-18} surf {surf} roll {f.AccRoll(m),7:F1} yaw {f.AccYaw(m),7:F1}  colx {ap.centerOfLift.x:F2}");
        }
        var arr = f.asymDsym;
        for (int i = 0; i < f.surfaces.Count; i++) { var se = f.surfaces[i]; if (!se.active) continue; var m = se.EvalFlow(arr[i], vv, rho, ref ctx); Console.WriteLine($"  surf {se.name,-12} now {se.now,6:F1} sym {arr[i],6:F1} pairSign {se.pairSign} partner {(se.partner != null ? se.partner.name : "-")} roll {f.AccRoll(m),7:F1} yaw {f.AccYaw(m),7:F1}"); }
        foreach (var h in f.hinges) { var m = h.EvalFlow(h.SymNow, vv, rho, ref ctx); Console.WriteLine($"  hinge {h.name,-12} now {h.hingeNow,6:F1} sym {h.SymNow,6:F1} roll {f.AccRoll(m),7:F1} yaw {f.AccYaw(m),7:F1}"); }
    }

    // per-part pitch acceleration (deg/s^2) with surfaces at the given poses, at +a and -a: j20parts ias= alist=12,20,28 can= inner= outer=
    public static void Parts(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 300f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float sIn = TeUpSign(A, "trailingInner", "L"), sOut = TeUpSign(A, "trailingOuter", "L");
        float can = F(kv, "can", 0f), inn = F(kv, "inner", 0f), outr = F(kv, "outer", 0f);
        float Iyy = (float)A.Itot[0, 0];
        var al = (kv.TryGetValue("alist", out var s0) ? s0 : "12,20,28").Split(',').Select(x => float.Parse(x, IC)).ToList();
        var rows = new SortedDictionary<string, float[]>();
        var tot = new float[al.Count * 2];
        for (int i = 0; i < al.Count; i++)
            for (int sg = 0; sg < 2; sg++)
            {
                float a = sg == 0 ? al[i] : -al[i];
                P.PartDump = new Dictionary<AeroPart, (Vector3, Vector3, float, float)>();
                P.Init(alt, tas, a, a); SetSurf("canard", can); SetSurf("trailingInner", inn * sIn); SetSurf("trailingOuter", outr * sOut); SetSurf("tail", 0f);
                var acc = IfritTest.Acc(P); tot[i * 2 + sg] = acc.y;
                foreach (var kvp in P.PartDump)
                {
                    var Mb = Quaternion.Inverse(P.q) * kvp.Value.m; float q = -Mb.x / Iyy * Mathf.Rad2Deg;
                    string n = kvp.Key.name; if (!rows.TryGetValue(n, out var r)) rows[n] = r = new float[al.Count * 2];
                    r[i * 2 + sg] += q;
                }
            }
        Console.Write($"{"part",-26}"); foreach (var a in al) Console.Write($" {"+" + a,6} {"-" + a,6} {"sum",5} |"); Console.WriteLine();
        foreach (var kvp in rows.OrderByDescending(r => r.Value.Select(Math.Abs).Max()))
        {
            if (kvp.Value.Select(Math.Abs).Max() < 1.5f) continue;
            Console.Write($"{kvp.Key,-26}"); for (int i = 0; i < al.Count; i++) Console.Write($" {kvp.Value[i * 2],6:F1} {kvp.Value[i * 2 + 1],6:F1} {kvp.Value[i * 2] + kvp.Value[i * 2 + 1],5:F1} |"); Console.WriteLine();
        }
        Console.Write($"{"TOTAL (Acc)",-26}"); for (int i = 0; i < al.Count; i++) Console.Write($" {tot[i * 2],6:F1} {tot[i * 2 + 1],6:F1} {tot[i * 2] + tot[i * 2 + 1],5:F1} |"); Console.WriteLine();
    }

    // static pitch accel for explicit per-side poses: j20pose ias= a= can= fl= fr= al= ar= tl= tr=   (elevon values TE-up positive)
    public static void Pose(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 300f), a = F(kv, "a", -25f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float sIn = TeUpSign(A, "trailingInner", "L"), sOut = TeUpSign(A, "trailingOuter", "L");
        P.Init(alt, tas, a, a);
        SetSurf("canard", F(kv, "can", 0f));
        SetSurf("trailingInner", F(kv, "fl", 0f) * sIn, "L"); SetSurf("trailingInner", F(kv, "fr", 0f) * sIn, "R");
        SetSurf("trailingOuter", F(kv, "al", 0f) * sOut, "L"); SetSurf("trailingOuter", F(kv, "ar", 0f) * sOut, "R");
        SetSurf("tail", F(kv, "tl", 0f), "L"); SetSurf("tail", F(kv, "tr", 0f), "R");
        var acc = IfritTest.Acc(P);
        Console.WriteLine($"a {a} ias {ias}: roll {acc.x,7:F1} pitch {acc.y,7:F1} yaw {acc.z,7:F1}");
    }

    public static void PartPos(Dictionary<string, string> kv)
    {
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        P.Init(3000f, 100f, 0f, 0f);
        foreach (var ap in A.PartByTidPublic.Values)
        {
            if (ap.wingArea < 0.5f) continue;
            var w = ap.transform.TransformPoint(ap.centerOfLift) - P.pCg;
            var b = Quaternion.Inverse(P.q) * w;
            var w2 = (ap.rb != null ? ap.rb.worldCenterOfMass : ap.transform.position) + ap.transform.rotation * ap.centerOfLift - P.pCg;
            var b2 = Quaternion.Inverse(P.q) * w2;
            Console.WriteLine($"{ap.name,-16} area {ap.wingArea,5:F2}  CoL rel CG: right {b.x,6:F2} up {b.y,6:F2} fwd {b.z,6:F2} | force point (rb CoM + CoL): fwd {b2.z,6:F2} right {b2.x,6:F2}  rb {(ap.rb != null ? ap.rb.name : "-")}");
        }
    }

    // neutral point vs CG, surfaces neutral: x_np (m, + = ahead of the CG) = dM_noseup / dN between a-1 and a+1
    public static void Np(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 500f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float mac = F(kv, "mac", 5.7f);
        Console.WriteLine($"# J-20B neutral point, surfaces neutral, IAS {ias:F0} km/h | {A.CgShiftLog}");
        foreach (var a in new float[] { 2, 6, 10, 14, 18, 22, 26, 30, 34, 38, 42 })
        {
            float Mn(float al, out float N)
            {
                P.Init(alt, tas, al, al);
                P.Step();
                var Mb = Quaternion.Inverse(P.q) * P.lastTorque;
                N = IfritTest.NzLast(P) * A.mass * 9.81f;
                return -Mb.x;
            }
            float m1 = Mn(a - 1f, out float n1), m2 = Mn(a + 1f, out float n2);
            float xnp = (m2 - m1) / Mathf.Max(1f, n2 - n1);
            Console.WriteLine($"AoA {a,4:F0}: dN {(n2 - n1) / (A.mass * 9.81f),5:F2} g/2deg  NP {(xnp >= 0 ? "ahead of" : "behind")} CG by {Mathf.Abs(xnp),5:F2} m  static margin {-xnp / mac * 100f,6:F1} % of {mac:F1} m chord");
        }
    }

    // per-part share of the pitch stiffness: dN (g) and dM (deg/s^2 nose-up) between a-1 and a+1, surfaces neutral
    public static void NpParts(Dictionary<string, string> kv)
    {
        float alt = 3000f, ias = F(kv, "ias", 500f);
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A) { EvalOnly = true };
        float rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float Iyy = (float)A.Itot[0, 0];
        var al = (kv.TryGetValue("alist", out var s0) ? s0 : "4,38").Split(',').Select(x => float.Parse(x, IC)).ToList();
        foreach (float a in al)
        {
            var d = new Dictionary<string, (float n, float m)>();
            float tn = 0, tm = 0;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                P.PartDump = new Dictionary<AeroPart, (Vector3, Vector3, float, float)>();
                P.Init(alt, tas, a + sg, a + sg); P.Step();
                foreach (var kvp in P.PartDump)
                {
                    var fb = Quaternion.Inverse(P.q) * kvp.Value.f; var Mb = Quaternion.Inverse(P.q) * kvp.Value.m;
                    float n = fb.y / (A.mass * 9.81f) * sg, m = -Mb.x / Iyy * Mathf.Rad2Deg * sg;
                    string k = kvp.Key.name.Replace("_L", "").Replace("_R", "").Replace("FL", "F").Replace("FR", "F");
                    d.TryGetValue(k, out var v); d[k] = (v.n + n, v.m + m); tn += n; tm += m;
                }
            }
            Console.WriteLine($"# AoA {a}: per 2 deg, both sides summed | total dN {tn:F3} g  dM {tm:F1} deg/s^2  -> NP {(tm / Iyy) / Mathf.Max(1e-6f, tn * A.mass * 9.81f) * Iyy * Mathf.Deg2Rad:F2} m ahead of CG (approx)");
            foreach (var kvp in d.OrderByDescending(x => Mathf.Abs(x.Value.m)))
                if (Mathf.Abs(kvp.Value.m) > 0.3f || Mathf.Abs(kvp.Value.n) > 0.005f)
                    Console.WriteLine($"  {kvp.Key,-18} dN {kvp.Value.n,7:F3} g  dM {kvp.Value.m,7:F1}");
        }
    }
}
