// 3 Nov 2026: closed-loop replay of a flight-log window (ported from the F-22E bench, 2.31.2 + pinPitch). The plant is posed from the
// logged state at t0 (EqErr.Poser: attitude, velocity, rates, surfaces, slats, flap drives, nozzles, engine state), the FCS settles on
// the frozen pose with its effectors held at their logged positions, then the jet flies closed loop with the logged sticks and thrust.
// Output: the FCS's own telemetry rows (the game's log columns, t = log time), so bench and game compare column by column
// (an/replaycmp.py).
//   bench.sh replay <log.csv> <t0> <t1> <out.csv> [pinPitch=1] [settle=0.5] [Name=value ...]
// pinPitch=1 holds the plant's pitch plane to the log every tick (AoA and TAS at the logged reference, body pitch rate); sideslip,
// roll and yaw rate and the attitude stay the bench's own. For lateral-law events where the plant's pitch would otherwise drift away
// from the flight (F-22E 18:36 log, 118-125 s: free replay 34 -> 51 deg AoA, the AoA-triggered released-roll follow never fired;
// pinned it reproduced the game's 9 engagements as 10).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

internal static class Replay
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    // thrust / weight -> throttle (the F-22E bench's ThrottleFor: military by rpm, afterburner above 0.95)
    static float ThrottleFor(Airframe A, float tw, float mass)
    {
        var st = typeof(Plant).GetMethod("SpeedThrust", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        float T = tw * mass * 9.81f, milMax = 0f, abMax = 0f;
        foreach (var tf in A.engines)
        {
            float sf = st != null ? (float)st.Invoke(null, new object[] { tf, A.ac.speed }) : 1f;
            milMax += tf.staticThrust * sf;
            foreach (var n in tf.nozzles) abMax += n.abThrust * Mathf.Clamp(A.ac.airDensity, 0.4f, 1f);
        }
        if (T <= milMax)
        {
            var tf0 = A.engines[0];
            float rpm = T / Mathf.Max(1f, milMax) * (tf0.maxRPM - tf0.minRPM) + 0.85f * tf0.minRPM;
            return Mathf.Clamp01((rpm - tf0.minRPM) / (tf0.maxRPM - tf0.minRPM));
        }
        return Mathf.Clamp01(0.95f + 0.05f * (T - milMax) / Mathf.Max(1f, abMax));
    }

    static void SetField(object o, string name, float v)
    {
        var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float)) f.SetValue(o, v);
    }

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; float t0 = float.Parse(kv["t0"], IC), t1 = float.Parse(kv["t1"], IC);
        string outPath = kv.TryGetValue("out", out var o) ? o : "replay.csv";
        float settle = kv.TryGetValue("settle", out var st) ? float.Parse(st, IC) : 0.5f;
        bool pinPitch = kv.TryGetValue("pinPitch", out var pp) && pp == "1";
        // ---- the log window (with a little lead for interpolation)
        string[] hdr = null; var rows = new List<Dictionary<string, float>>();
        foreach (var line in File.ReadLines(log))
        {
            if (hdr == null) { hdr = line.Split(','); continue; }
            var f = line.Split(','); if (f.Length < hdr.Length - 1) continue;
            if (!float.TryParse(f[0], NumberStyles.Float, IC, out float t) || t < t0 - 0.5f || t > t1 + 0.5f) continue;
            var d = new Dictionary<string, float>();
            for (int i = 0; i < Math.Min(hdr.Length, f.Length); i++) if (float.TryParse(f[i], NumberStyles.Float, IC, out float v)) d[hdr[i]] = v;
            rows.Add(d);
        }
        if (rows.Count < 10) throw new Exception("no log rows in window");
        var r0 = rows.First(r => r["t"] >= t0);
        bool logCg = r0.ContainsKey("airRefCg") && r0["airRefCg"] > 0.5f;
        if (logCg) EqErr.LogRefRoot = 0;
        var surfNames = hdr.Where(h => h.EndsWith("_now") && !h.StartsWith("tvc") && !h.StartsWith("hl")).Select(h => h.Substring(0, h.Length - 4)).ToArray();
        // ---- plant + FCS, posed at t0
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = Gyro.Unity };
        P.Init(1000f, r0["tas"], r0["alpha"], r0["theta"]);
        P.CreateFcs();
        var fcs = P.fcs; var inp = A.ac.inputs;
        var ps = new EqErr.Poser(A, P, surfNames);
        float G0(string k, float dflt = 0f) => r0.TryGetValue(k, out var v) ? v : dflt;
        ps.PoseState(r0["tas"], r0["rho"], r0["alpha"], r0["beta"], r0["p"], r0["q"], r0["r"], r0["phi"], r0["theta"]);
        foreach (var sn in surfNames) ps.SetSurface(sn, r0[sn + "_now"]);
        if (r0.ContainsKey("splitL")) { ps.SetSplit("flap_L", r0["splitL"]); ps.SetSplit("flap_R", r0["splitR"]); }
        float hl = G0("hlPos"); ps.SetSlats(hl);
        for (int i = 0; i < ps.drives.Count; i++)
        {
            var d = ps.drives[i];
            ps.SetDrive(i, d.flap ? Mathf.Clamp(-G0("flapBias") - (d.hasHl ? d.hlAngle * hl : 0f), -25f, 20f) : Mathf.Clamp(G0("relief"), -25f, 20f));
        }
        float mass = A.mass * EqErr.MassScale;
        float thr0 = ThrottleFor(A, G0("tw"), mass);
        P.throttle = thr0; inp.throttle = thr0;
        foreach (var e in A.engines)
        {
            e.rpm = Mathf.Lerp(e.minRPM, e.maxRPM, Plant.ThrottleOf(e, thr0));
            foreach (var n0 in e.nozzles) n0.abAmt = Mathf.InverseLerp(n0.abStart, 1f, thr0);
        }
        // ---- settle the FCS on the frozen pose, its effectors held at the logged positions
        var save = (P.pCg, P.vCg, P.w, P.q);
        int nSettle = (int)Math.Round(settle / P.dt);
        for (int i = 0; i < nSettle; i++)
        {
            inp.pitch = -G0("sp"); inp.roll = G0("sr"); inp.yaw = G0("sy"); inp.brake = (r0.ContainsKey("abTgt") && r0["abTgt"] > 0.01f) ? 1f : 0f;   // 17 Nov: the brake key from the logged open target
            if (r0.ContainsKey("abOpen")) foreach (var abk in A.airbrakes) abk.openAmount = r0["abOpen"];
            Time.fixedDeltaTime = P.dt; Time.deltaTime = P.dt;
            P.Pose();
            fcs.Tick(inp, true); fcs.EstJoin();
            if (i == nSettle - 1 && Environment.GetEnvironmentVariable("REPLAYDBG") != null)
            {
                var al = fcs.alloc; var tg = al.dbgLastTarget; Vector3 sm = Vector3.zero; foreach (var ef in fcs.effs) if (ef.active) sm += ef.MAt(ef.cmd);
                float kx = fcs.IaInv[0,0] * 57.3f, ky = fcs.IaInv[1,1] * 57.3f, kz = fcs.IaInv[2,2] * 57.3f;
                Console.WriteLine($"DBG solve: target r/p/y {tg.x*kx:F1} {tg.y*ky:F1} {tg.z*kz:F1}  got {sm.x*kx:F1} {sm.y*ky:F1} {sm.z*kz:F1}  w {al.dbgLastW.x:F2} {al.dbgLastW.y:F2} {al.dbgLastW.z:F2} fightK {al.fightK:F2} cost {al.lastCost:F4}");
                Console.WriteLine("DBG solve cmd: " + string.Join(" ", fcs.effs.Where(e => e.active).Select(e => $"{e.name}={e.cmd:F2}[{e.lo:F1},{e.hi:F1}]")));
                var tA = typeof(Aryx_F22E_StrikeRaptor.FCS.Allocator); var bfl = BindingFlags.NonPublic | BindingFlags.Instance;
                var xs = (double[])tA.GetField("x", bfl).GetValue(al); var acts = (List<Aryx_F22E_StrikeRaptor.FCS.Effector>)tA.GetField("act", bfl).GetValue(al); var mTot = tA.GetMethod("Total", bfl);
                var mFight = tA.GetMethod("FightCost", bfl); var mPair = tA.GetMethod("PairReg", bfl); var mCost = tA.GetMethod("Cost", bfl); var mGR = tA.GetMethod("GroupRoll", bfl); var fHint = tA.GetField("fightSignHint", bfl);
                Func<string> costs = () => { float c = (float)mTot.Invoke(al, new object[] { tg }); Vector3 s2 = Vector3.zero; float pr = 0f; for (int k = 0; k < acts.Count; k++) { s2 += acts[k].MAt((float)xs[k]); pr += (float)mPair.Invoke(al, new object[] { k }); } float fc = (float)mFight.Invoke(al, null); float ec = (float)mCost.Invoke(al, new object[] { s2 - tg }); return $"Total {c:F4} (err {ec:F4} pair {pr:F4} fight {fc:F4}) err r/p/y {(s2.x - tg.x)*kx:F1} {(s2.y - tg.y)*ky:F1} {(s2.z - tg.z)*kz:F1} | grpRoll {(double)mGR.Invoke(al, new object[] { 1 })*57.3:F1} {(double)mGR.Invoke(al, new object[] { 2 })*57.3:F1} hint {fHint.GetValue(al)}"; };
                Console.WriteLine($"DBG   chosen: {string.Join(" ", acts.Select((e, k) => e.name + "=" + xs[k].ToString("F2")))} | {costs()}");
                if (Environment.GetEnvironmentVariable("ALLOCALT") is string alts)
                    foreach (var altS in alts.Split(';'))
                    {
                        var sv = (double[])xs.Clone();
                        foreach (var kvA in altS.Split(',')) { var pr = kvA.Split('='); int ii = acts.FindIndex(e => e.name == pr[0]); if (ii >= 0) xs[ii] = Mathf.Clamp(float.Parse(pr[1], IC), acts[ii].lo, acts[ii].hi); }
                        Console.WriteLine($"DBG   alt [{altS}]: {costs()}");
                        Array.Copy(sv, xs, xs.Length);
                    }
            }
            foreach (var sn in surfNames)
                if (ps.seByLog.TryGetValue(sn, out var se)) { float now = r0[sn + "_now"]; se.cmd = se.cmdOut = se.now = now; se.outTraj.Reset(now); se.cmdLag = now; }
            (P.pCg, P.vCg, P.w, P.q) = save;
        }
        if (Environment.GetEnvironmentVariable("REPLAYDBG") != null)
        {
            Console.WriteLine($"DBG t0 {t0} bU {fcs.teBrakeOutDbg:F2} eMin {fcs.teBrakeFnDbg:F1} flareCap {fcs.brakeFlareCap:F2} flareNow {fcs.BrakeFlareNow:F2} teF {fcs.teBrakeAirF:F2} alpha {fcs.alpha:F2}");
            foreach (var sx in fcs.surfaces) Console.WriteLine($"DBG  {sx.name,-12} kind {sx.kind} now {sx.now,7:F2} cmd {sx.cmd,7:F2} lo {sx.lo,7:F2} hi {sx.hi,7:F2} symT {sx.symTarget,7:F2} symC {sx.symCost,7:F2} anti {sx.antiCost,6:F2} mu {sx.mu:F3} lam {sx.lam:F3} laoa {sx.localAoANow,6:F1} stall {sx.stallAoA,6:F1} knee {sx.KneeAoA(Aryx_F22E_StrikeRaptor.FCS.Plugin.SettledSlopeFrac),6:F1} step {sx.Step:F2} RegN {sx.RegN:F1}");
            var rootT = ((Component)(object)A.ac).transform;
            if (Environment.GetEnvironmentVariable("REPLAYDBG") == "2") foreach (var sx in fcs.surfaces)
            {
                var sb = new System.Text.StringBuilder();
                for (float u = -30f; u <= 30.01f; u += 2f) { if (u < sx.min || u > sx.max) continue; var m = sx.MAt(u); sb.Append($" {u:F0}:{m.x * fcs.IaInv[0,0] * 57.3f:F1}/{m.y * fcs.IaInv[1,1] * 57.3f:F1}/{m.z * fcs.IaInv[2,2] * 57.3f:F1}"); }
                Console.WriteLine($"DBG  M({sx.name}) r/p/y deg/s2:{sb}");
            }
        }
        // warm the trajectories from the log (as the F-22E bench's WarmState), where this FCS has them
        if (r0.ContainsKey("qT")) { SetField(fcs, "qT", r0["qT"]); var qTraj = fcs.GetType().GetField("qTraj")?.GetValue(fcs); qTraj?.GetType().GetMethod("Reset")?.Invoke(qTraj, new object[] { r0["qT"] }); }
        if (r0.ContainsKey("pCmd")) { SetField(fcs, "pCmd", r0["pCmd"]); if (r0.ContainsKey("rCmd")) SetField(fcs, "rCmd", r0["rCmd"]); }
        // ---- run
        using var w = new StreamWriter(outPath);
        fcs.WriteHeader(w); w.WriteLine();
        float tl = t0; int ri = 0;
        Func<string, float> L = k => { while (ri + 1 < rows.Count && rows[ri + 1]["t"] <= tl) ri++; var a = rows[ri]; var b = rows[Math.Min(ri + 1, rows.Count - 1)]; if (!a.ContainsKey(k) || !b.ContainsKey(k)) return 0f; float u = b["t"] > a["t"] ? Mathf.Clamp01((tl - a["t"]) / (b["t"] - a["t"])) : 0f; return a[k] + (b[k] - a[k]) * u; };
        Vector3 dRoot = A.bodies.First(b => b.rb == A.ac.rb).comB - A.cgB;
        while (tl <= t1)
        {
            float sp = L("sp"), sr = L("sr"), sy = L("sy");
            inp.pitch = -sp; inp.roll = sr; inp.yaw = sy; inp.brake = L("abTgt") > 0.01f ? 1f : 0f;
            P.throttle = ThrottleFor(A, L("tw"), mass);
            P.Step();
            if (pinPitch)
            {
                // the logged reference: the CG (airRefCg = 1) or the root body (older logs); keep the bench's sideslip, set AoA / TAS / q
                Vector3 off = logCg ? Vector3.zero : Vector3.Cross(P.w, P.q * dRoot);
                Vector3 vb = Quaternion.Inverse(P.q) * (P.vCg + off - A.ac.windVelocity);
                float tasB = Mathf.Max(1f, vb.magnitude);
                float bR = Mathf.Asin(Mathf.Clamp(vb.x / tasB, -1f, 1f)), aR = L("alpha") * Mathf.Deg2Rad;
                Vector3 wb = Quaternion.Inverse(P.q) * P.w; wb.x = -L("q") * Mathf.Deg2Rad; P.w = P.q * wb;
                off = logCg ? Vector3.zero : Vector3.Cross(P.w, P.q * dRoot);
                P.vCg = P.q * (new Vector3(Mathf.Sin(bR), -Mathf.Cos(bR) * Mathf.Sin(aR), Mathf.Cos(bR) * Mathf.Cos(aR)) * L("tas")) + A.ac.windVelocity - off;
                P.Pose();
            }
            if (float.IsNaN(P.vCg.x)) break;
            fcs.WriteRow(w, tl, sp, sr, sy, A.ac.airDensity); w.WriteLine();
            tl += P.dt;
        }
        Console.Error.WriteLine($"# replay {Path.GetFileName(log)} {t0}-{t1} s{(pinPitch ? " pitch pinned to the log" : "")} -> {outPath}");
    }
}
