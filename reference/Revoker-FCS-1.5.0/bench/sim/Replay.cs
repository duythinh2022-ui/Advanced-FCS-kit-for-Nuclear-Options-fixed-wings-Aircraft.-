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
// 16 Nov 2026 (lead=2, default): the replay starts from a pinned lead-in instead of a frozen-pose settle. For `lead` s before t0
// the plant is posed from the log every tick (attitude, velocity, rates, surfaces, hinges, splits, slats, flap drives, nozzles,
// airbrake, thrust) and the FCS ticks on it with the logged sticks, its effectors reset to their logged positions after each tick.
// The FCS reaches t0 with the game's history: its rate-command trajectories, rate caps, measured-authority and capacity
// estimators, held-stick copies and filters. The frozen settle left them cold (Revoker 16 Nov 09:21 log at 38.5 s: yaw command
// -21 vs the game's -69 deg/s, measured yaw authority 45 vs 122 deg/s^2, yaw cap 3 vs 81 deg/s; the jet braked at once). lead=0 =
// the old settle. The airbrake now follows the logged abOpen in the closed loop too (the bench FCS does not drive it).
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
        float lead = kv.TryGetValue("lead", out var ld) ? float.Parse(ld, IC) : 2f;
        // 16 Nov 2026 (thrustLog=1, default): each nozzle's thrust is the logged one (tw x logged mass x g / nozzles), in the lead-in
        // and in the closed loop, as eqerr does. The engine model runs only inside Plant.Step, so on the frozen settle / lead-in the
        // FCS read zero thrust (no yaw-nozzle authority: yaw cap 3 deg/s with the stick full over); and in the closed loop the
        // throttle-from-T/W guess left the measured yaw authority ~11 % under the game's (120 vs 135 deg/s^2 at 41 s, 16 Nov log).
        bool thrustLog = !(kv.TryGetValue("thrustLog", out var tlg) && tlg == "0");
        // ---- the log window (with a little lead for interpolation)
        string[] hdr = null; var rows = new List<Dictionary<string, float>>();
        foreach (var line in File.ReadLines(log))
        {
            if (hdr == null) { hdr = line.Split(','); continue; }
            var f = line.Split(','); if (f.Length < hdr.Length - 1) continue;
            if (!float.TryParse(f[0], NumberStyles.Float, IC, out float t) || t < t0 - Math.Max(0f, lead) - 0.5f || t > t1 + 0.5f) continue;
            var d = new Dictionary<string, float>();
            for (int i = 0; i < Math.Min(hdr.Length, f.Length); i++) if (float.TryParse(f[i], NumberStyles.Float, IC, out float v)) d[hdr[i]] = v;
            rows.Add(d);
        }
        if (rows.Count < 10) throw new Exception("no log rows in window");
        var r0 = rows.First(r => r["t"] >= t0);
        if (lead > 0f && rows[0]["t"] > t0 - lead + 0.05f) { Console.Error.WriteLine($"# replay: the log starts at {rows[0]["t"]:F2} s, lead-in shortened"); lead = Mathf.Max(0f, t0 - rows[0]["t"]); }
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
        // ---- log interpolation (one forward cursor; the lead-in and the run share it)
        float tl = (lead > 0f) ? t0 - lead : t0; int ri = 0;
        Func<string, float> L = k => { while (ri + 1 < rows.Count && rows[ri + 1]["t"] <= tl) ri++; var a = rows[ri]; var b = rows[Math.Min(ri + 1, rows.Count - 1)]; if (!a.ContainsKey(k) || !b.ContainsKey(k)) return 0f; float u = b["t"] > a["t"] ? Mathf.Clamp01((tl - a["t"]) / (b["t"] - a["t"])) : 0f; return a[k] + (b[k] - a[k]) * u; };
        bool Has(string k) => r0.ContainsKey(k);
        var hinges = fcs.hinges; var tvcE = fcs.tvc; var tvcyE = fcs.tvcy;
        void ForceEff(Aryx_F22E_StrikeRaptor.FCS.Effector e, float v) { if (e == null) return; e.cmd = e.cmdOut = e.now = v; e.outTraj.Reset(v); e.cmdLag = v; }
        // the plant and the FCS's effectors at the logged state of time tl
        void PoseAtLog()
        {
            ps.PoseState(L("tas"), L("rho"), L("alpha"), L("beta"), L("p"), L("q"), L("r"), L("phi"), L("theta"));
            foreach (var sn in surfNames) ps.SetSurface(sn, L(sn + "_now"));
            if (Has("splitL")) { ps.SetSplit("flap_L", L("splitL")); ps.SetSplit("flap_R", L("splitR")); }
            float hlv = L("hlPos"); ps.SetSlats(hlv);
            for (int i = 0; i < ps.drives.Count; i++)
            {
                var d = ps.drives[i];
                ps.SetDrive(i, d.flap ? Mathf.Clamp(-L("flapBias") - (d.hasHl ? d.hlAngle * hlv : 0f), -25f, 20f) : Mathf.Clamp(L("relief"), -25f, 20f));
            }
            if (Has("flapHL")) foreach (var hg in hinges) { hg.hingeNow = 0f; hg.Drive(hg.left ? L("flapHL") : L("flapHR"), 1e6f); }
            foreach (var abk in A.airbrakes) abk.openAmount = Has("abOpen") ? L("abOpen") : 0f;
            foreach (var tf in A.engines) { tf.nozzleAngles.x = L("tvcNow"); tf.nozzleAngles.y = Has("tvcyNow") ? L("tvcyNow") : 0f; }
        }
        void ForceEffsToLog()
        {
            foreach (var sn in surfNames) if (ps.seByLog.TryGetValue(sn, out var se)) ForceEff(se, L(sn + "_now"));
            if (Has("flapHL")) foreach (var hg in hinges) { float v = hg.left ? L("flapHL") : L("flapHR"); ForceEff(hg, v); hg.hingeNow = 0f; hg.Drive(v, 1e6f); }
            ForceEff(tvcE, L("tvcNow"));
            if (Has("tvcyNow")) ForceEff(tvcyE, L("tvcyNow"));
        }
        int nNoz = Math.Max(1, A.engines.Sum(e => e.nozzles.Length));
        float TnLog() => L("tw") * (Has("massKg") && L("massKg") > 100f ? L("massKg") : mass) * 9.81f / nNoz;
        void SetEngines(float tw)
        {
            if (thrustLog) { float tn = TnLog(); foreach (var e in A.engines) foreach (var n0 in e.nozzles) n0.totalThrust = tn; Plant.ThrustOverride = tn; }
            float th = ThrottleFor(A, tw, mass); P.throttle = th; inp.throttle = th;
            foreach (var e in A.engines)
            {
                e.rpm = Mathf.Lerp(e.minRPM, e.maxRPM, Plant.ThrottleOf(e, th));
                foreach (var n0 in e.nozzles) n0.abAmt = Mathf.InverseLerp(n0.abStart, 1f, th);
            }
        }
        StreamWriter wl = (lead > 0f && kv.TryGetValue("leadOut", out var lo) && lo == "1") ? new StreamWriter(outPath + ".lead.csv") : null;
        if (wl != null) { fcs.WriteHeader(wl); wl.WriteLine(); }
        if (lead > 0f)
        {
            // ---- pinned lead-in: the FCS flies the logged history on a plant posed from the log every tick
            while (tl < t0 - 0.5f * P.dt)
            {
                Time.fixedDeltaTime = P.dt; Time.deltaTime = P.dt;
                SetEngines(L("tw"));
                PoseAtLog();
                inp.pitch = -L("sp"); inp.roll = L("sr"); inp.yaw = L("sy");
                fcs.Tick(inp, true); fcs.EstJoin();
                ForceEffsToLog();
                if (wl != null) { fcs.WriteRow(wl, tl, L("sp"), L("sr"), L("sy"), A.ac.airDensity); wl.WriteLine(); }
                tl += P.dt;
            }
            wl?.Dispose();
            tl = t0;
            SetEngines(L("tw"));
            PoseAtLog(); ForceEffsToLog();
        }
        else
        {
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
        }
        // ---- run
        using var w = new StreamWriter(outPath);
        fcs.WriteHeader(w); w.WriteLine();
        Vector3 dRoot = A.bodies.First(b => b.rb == A.ac.rb).comB - A.cgB;
        while (tl <= t1)
        {
            float sp = L("sp"), sr = L("sr"), sy = L("sy");
            inp.pitch = -sp; inp.roll = sr; inp.yaw = sy;
            P.throttle = ThrottleFor(A, L("tw"), mass);
            if (thrustLog) Plant.ThrustOverride = TnLog();
            foreach (var abk in A.airbrakes) abk.openAmount = Has("abOpen") ? L("abOpen") : 0f;
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
