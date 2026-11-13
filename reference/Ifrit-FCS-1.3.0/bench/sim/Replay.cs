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
            inp.pitch = -G0("sp"); inp.roll = G0("sr"); inp.yaw = G0("sy");
            Time.fixedDeltaTime = P.dt; Time.deltaTime = P.dt;
            P.Pose();
            fcs.Tick(inp, true); fcs.EstJoin();
            foreach (var sn in surfNames)
                if (ps.seByLog.TryGetValue(sn, out var se)) { float now = r0[sn + "_now"]; se.cmd = se.cmdOut = se.now = now; se.outTraj.Reset(now); se.cmdLag = now; }
            (P.pCg, P.vCg, P.w, P.q) = save;
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
            inp.pitch = -sp; inp.roll = sr; inp.yaw = sy;
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
