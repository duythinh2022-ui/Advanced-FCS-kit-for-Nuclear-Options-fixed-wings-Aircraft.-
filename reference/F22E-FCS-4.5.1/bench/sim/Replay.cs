// Closed-loop replay of a logged manoeuvre (bench calibration, 31 Oct 2026).
//   bench replay log=<csv> t0=<s> t1=<s> out=<csv> [settle=1.0] [Name=value ...]
// The plant is posed at the logged state at t0 (TAS, AoA, sideslip, rates, attitude, surfaces, nozzles, slats, airbrake,
// engine state for the logged T/W), the FCS is created on it and settled for `settle` seconds with the plant frozen (its
// filters and trajectories start from the logged surfaces), then the logged sticks (sp / sr / sy, the FCS's own filtered
// stick values, fed in exactly) and the throttle solved from the logged T/W drive the FCS + plant to t1. The output has the
// bench state next to the logged state at the same times; the Python side reports the error per case.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class Replay
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; float t0 = float.Parse(kv["t0"], IC), t1 = float.Parse(kv["t1"], IC);
        string outPath = kv.TryGetValue("out", out var o) ? o : "replay.csv";
        float settle = kv.TryGetValue("settle", out var st) ? float.Parse(st, IC) : 1f;
        bool warm = !kv.TryGetValue("warm", out var wm) || wm != "0";   // negAoA calibration: FCS state from the log at t0 (slats, qT / pS trajectories, integrators)
        Airframe.Detach = kv.TryGetValue("detach", out var dt) ? dt : null;
        // 3 Nov (2.31.2): pinPitch=1 holds the plant's pitch plane to the log every tick - AoA and TAS at the CG and the body pitch rate
        // are overwritten with the logged values after each step; sideslip, roll and yaw rate and the attitude stay the bench's own.
        // For lateral-law events the plant's open pitch item (CALIBRATION_leaf.md section 5) would otherwise move the AoA away from
        // the flight (18:36 log, 118-125 s: the bench climbed 34 -> 51 deg where the game held 31-34, so the AoA-triggered lateral
        // devices never saw the flight's AoA history).
        bool pinPitch = kv.TryGetValue("pinPitch", out var pp) && pp == "1";
        // 4 Nov (2.36.1): pinUntil=<t> pins the pitch plane only until log time t, then frees it (a transition replayed from the flight's state)
        float pinUntil = kv.TryGetValue("pinUntil", out var pu) ? float.Parse(pu, IC) : float.NaN;
        if (!float.IsNaN(pinUntil)) pinPitch = true;   // parts gone for the whole replay ("Vstab_L+Rudder_L+..."); was ignored before 2.20.0
        var A = new Airframe(dataPath);
        if (!string.IsNullOrEmpty(A.DetachLog)) Console.Error.WriteLine("# replay " + A.DetachLog);
        var P = new Plant(A) { gyro = Gyro.Unity };
        P.Init(1000f, 150f, 5f, 5f);
        P.CreateFcs();
        var fcs = P.fcs;
        var ps = new EqErr.Poser(A, P);
        // ---- read the log window (with a little lead for interpolation)
        var rows = new List<Dictionary<string, float>>();
        string[] hdr = null;
        foreach (var line in File.ReadLines(log))
        {
            if (hdr == null) { hdr = line.Split(','); continue; }
            var f = line.Split(',');
            if (f.Length < hdr.Length - 1) continue;
            float t = float.Parse(f[0], IC);
            if (t < t0 - 0.5f || t > t1 + 0.5f) continue;
            var d = new Dictionary<string, float>();
            for (int i = 0; i < Math.Min(hdr.Length, f.Length); i++) if (float.TryParse(f[i], NumberStyles.Float, IC, out float v)) d[hdr[i]] = v;
            rows.Add(d);
        }
        if (rows.Count < 10) throw new Exception("no log rows in window");
        var r0 = rows.First(r => r["t"] >= t0);
        float mass = A.mass;
        // ---- pose at t0
        // 2.30.0: logs with airRefCg = 1 carry the CG's alpha / beta / tas; older logs the root body's (EqErr.LogRefRoot)
        bool logCg = r0.ContainsKey("airRefCg") && r0["airRefCg"] > 0.5f;
        if (logCg) EqErr.LogRefRoot = 0;
        ps.PoseState(r0["tas"], r0["rho"], r0["alpha"], r0["beta"], r0["p"], r0["q"], r0["r"], r0["phi"], r0["theta"]);
        foreach (var sn in EqErr.Poser.SurfNames) ps.SetSurface(sn, r0[sn + "_now"]);
        ps.SetSlats(r0["hlPos"]);
        float flapBias = r0["flapBias"], relief = r0["relief"];
        fcs.flaps.flapBias = flapBias; fcs.flaps.relief = relief;
        for (int i = 0; i < ps.drives.Count; i++)
        {
            var d = ps.drives[i];
            float a0 = d.flap ? Mathf.Clamp(-flapBias - (d.hasHl ? d.hlAngle * r0["hlPos"] : 0f), -25f, 20f) : Mathf.Clamp(relief, -25f, 20f);
            ps.SetDrive(i, a0);
        }
        ps.SetNozzles(r0["tvcNow"]);
        ps.SetAirbrakes(r0["abOpen"]);
        float thr0 = ps.ThrottleFor(r0["tw"], mass);
        ps.SetEngineState(thr0);
        P.throttle = thr0;
        var inp = A.ac.inputs;
        inp.throttle = thr0;
        // ---- settle the FCS on the frozen plant (its cmd = now on the first tick; filters converge on the logged state)
        var save = (P.pCg, P.vCg, P.w, P.q);
        int nSettle = (int)Math.Round(settle / P.dt);
        for (int i = 0; i < nSettle; i++)
        {
            fcs.spF = fcs.sp = r0["sp"]; fcs.srF = fcs.sr = r0["sr"]; fcs.syF = fcs.sy = r0["sy"];
            inp.pitch = -r0["sp"]; inp.roll = r0["sr"]; inp.yaw = r0["sy"];
            Time.fixedDeltaTime = P.dt; Time.deltaTime = P.dt;
            P.Pose();
            fcs.Tick(inp, Plant.FlightAssist); fcs.VmcApply(inp, P.dt); fcs.EstJoin();
            foreach (var sn in EqErr.Poser.SurfNames) { var se = ps.seBy[sn]; se.cmd = se.cmdOut = r0[sn + "_now"]; se.outTraj.Reset(r0[sn + "_now"]); se.cmdLag = r0[sn + "_now"]; }
            fcs.tvc.cmd = fcs.tvc.cmdOut = r0["tvcNow"]; fcs.tvc.outTraj.Reset(r0["tvcNow"]); fcs.tvc.cmdLag = r0["tvcNow"];
            (P.pCg, P.vCg, P.w, P.q) = save;
            if (warm) WarmState(fcs, rows, r0, P.dt);
        }
        // ---- run
        using var w = new StreamWriter(outPath);
        w.WriteLine("t,alpha_b,alpha_g,beta_b,beta_g,p_b,p_g,q_b,q_g,r_b,r_g,nz_b,nz_g,ias_b,ias_g,phi_b,phi_g,theta_b,theta_g,sp,sr,sy,thr,tw_b,tw_g,stabL_b,stabL_g,stabR_b,stabR_g,rudL_b,rudL_g,tvc_b,tvc_g,ailL_b,ailL_g,flapL_b,flapL_g,hl_b,hl_g,pCmd,rCmd,qT,alphaCmd,pS_b,pSTgt,pSmax,stopSlipLim,stopSlipU,yawSatPitch,pullTrade,rudR_b,rudR_g,q_cmd,paceLead,paceHold,latAcc,tgtX_b,tgtZ_b,unmetX_b,unmetZ_b,stabLcmd_b,stabRcmd_b,capZeP_b,capZeN_b,capTrP_b,capTrN_b,tradeAlpha,yawSatPitch_g,q_cmd_g,capNsP_b,capNsN_b,capSwp,capShd,capSm,aYawShed,stabPitchFrac_b,relFollow,capDn,rudLcmd_b,rudRcmd_b,rudLcmd_g,rudRcmd_g,ailLcmd_b,flapLcmd_b,tvccmd_b,tvccmd_g,pgCeil,pgL,pgA,pgP0,pgAMin,ppOn,ppQ,ppQNow,ppT,ppBleed,stickRel,swin_b,swin_g");
        float tl = t0; int ri = 0;
        Func<string, float> L = k => { while (ri + 1 < rows.Count && rows[ri + 1]["t"] <= tl) ri++; var a = rows[ri]; var b = rows[Math.Min(ri + 1, rows.Count - 1)]; float u = b["t"] > a["t"] ? Mathf.Clamp01((tl - a["t"]) / (b["t"] - a["t"])) : 0f; return a[k] + (b[k] - a[k]) * u; };
        P.time = 0f;
        string S(float x) => x.ToString("0.###", IC);
        while (tl <= t1)
        {
            float sp = L("sp"), sr = L("sr"), sy = L("sy");
            fcs.spF = fcs.sp = sp; fcs.srF = fcs.sr = sr; fcs.syF = fcs.sy = sy;
            inp.pitch = -sp; inp.roll = sr; inp.yaw = sy;
            P.throttle = ps.ThrottleFor(L("tw"), mass);
            P.SimT = tl; P.Step();
            if (pinPitch && (float.IsNaN(pinUntil) || tl < pinUntil))
            {
                Vector3 vb = Quaternion.Inverse(P.q) * (P.vCg - A.ac.windVelocity);
                float tasB = Mathf.Max(1f, vb.magnitude);
                float bR = Mathf.Asin(Mathf.Clamp(vb.x / tasB, -1f, 1f)), aR = L("alpha") * Mathf.Deg2Rad, tasG = L("tas");
                Vector3 vbN = new Vector3(Mathf.Sin(bR), -Mathf.Cos(bR) * Mathf.Sin(aR), Mathf.Cos(bR) * Mathf.Cos(aR)) * tasG;
                P.vCg = P.q * vbN + A.ac.windVelocity - (logCg ? Vector3.zero : EqErr.LogRefOffset(A, P));
                Vector3 wb = Quaternion.Inverse(P.q) * P.w;
                wb.x = -L("q") * Mathf.Deg2Rad;   // body pitch rate (aero q = -wb.x, as in Poser.PoseState)
                P.w = P.q * wb;
                P.Pose();
            }
            var c = fcs; var rates = c.omega * Mathf.Rad2Deg;
            SurfaceEffector Se(string n) => c.surfaces.First(s => s.name == n);
            w.WriteLine(string.Join(",", new[] { S(tl), S(logCg ? c.alpha : c.alphaRoot), S(L("alpha")), S(logCg ? c.beta : c.betaRoot), S(L("beta")), S(rates.x), S(L("p")), S(rates.y), S(L("q")), S(rates.z), S(L("r")), S(c.nz), S(L("nz")), S(c.ias * 3.6f), S(L("ias") * 3.6f), S(c.phi), S(L("phi")), S(c.theta), S(L("theta")), S(sp), S(sr), S(sy), S(P.throttle), S(c.tw), S(L("tw")),
                S(Se("Elevator_L").now), S(L("Elevator_L_now")), S(Se("Elevator_R").now), S(L("Elevator_R_now")), S(Se("Rudder_L").now), S(L("Rudder_L_now")), S(c.tvc.now), S(L("tvcNow")), S(Se("Aileron_L").now), S(L("Aileron_L_now")), S(Se("Flap_L").now), S(L("Flap_L_now")), S(c.flaps.hlPos), S(L("hlPos")), S(c.pCmd), S(c.rCmd), S(c.qT), S(c.alphaCmd), S(c.pS), S(c.pSTgtDbg), S(c.pSmaxDbg), S(Mathf.Min(c.stopSlipLimDbg,999f)), S(c.stopSlipUDbg), S(c.yawSatPitchDbg), S(c.pullTradeDbg), S(Se("Rudder_R").now), S(L("Rudder_R_now")), S(c.qCmd), S(c.paceLeadDbg), S(c.paceHoldDbg), S(c.latAccDbg) , S(c.mTarget.x*c.IaInv[0,0]*Mathf.Rad2Deg), S(c.mTarget.z*c.IaInv[2,2]*Mathf.Rad2Deg), S(c.allocUnmet.x), S(c.allocUnmet.z), S(c.surfaces.First(e=>e.name.Contains("Elevator_L")).cmd), S(c.surfaces.First(e=>e.name.Contains("Elevator_R")).cmd), S(c.capEst.capZeP), S(c.capEst.capZeN), S(c.capEst.capTrZeP), S(c.capEst.capTrZeN), S(c.tradeAlphaDbg), S(L("yawSatPitch")), S(L("qCmd")), S(c.capEst.capZeNsP), S(c.capEst.capZeNsN), S(c.capSweepDbg), S(c.capShedDbg), S(c.sweepDelF), S(c.aYawShedDbg), S(c.stabPitchFrac), S(c.relFollowDbg), S(c.yawSatAlphaRateDn), S(Se("Rudder_L").cmd), S(Se("Rudder_R").cmd), S(L("Rudder_L_cmd")), S(L("Rudder_R_cmd")), S(Se("Aileron_L").cmd), S(Se("Flap_L").cmd), S(c.tvc.cmd), S(L("tvcCmd")), S(c.pushGovCeil), S(float.IsNaN(c.pushGovL) ? 0f : c.pushGovL), S(c.pushGovA), S(c.pushGovP0Dbg), S(c.pushGovAMin), S(c.pushPlanOn ? 1f : 0f), S(c.pushPlanQ), S(c.pushPlanQNow), S(c.pushPlanT), S(c.pushPlanBleed), S(c.stickRelW), S(c.sweepHoldWinDbg ? 1f : 0f), S(L("sweepWin")) }));
            tl += P.dt;
            if (float.IsNaN(c.alpha)) break;
        }
        Console.Error.WriteLine($"# replay {Path.GetFileName(log)} {t0}-{t1} s{(warm ? " warm" : "")}{(pinPitch ? " pitch pinned to the log" : "")} -> {outPath}");
    }

    // negAoA calibration (31 Oct): the cold settle left the FCS's own state far from the game's at t0 - slats at 0 (game 0.87),
    // the pitch trajectory at the frozen-state command (qT -41 vs the game's -15), the roll reference at 52 vs 101 - and the
    // first 0.3 s of every replay were the allocator answering that, not the plant. Put what the log has into the FCS:
    // slat position, qT (+ its rate), the velocity-vector roll trajectory from pCmd / cos AoA (+ rate), the AoA and g integrators.
    static void WarmState(FcsController fcs, List<Dictionary<string, float>> rows, Dictionary<string, float> r0, float dt)
    {
        int i0 = rows.IndexOf(r0); var rm = rows[Math.Max(0, i0 - 1)]; var rp = rows[Math.Min(rows.Count - 1, i0 + 1)];
        float span = Mathf.Max(dt, rp["t"] - rm["t"]);
        float D(string k) => (rp[k] - rm[k]) / span;
        if (r0.ContainsKey("hlPos"))
        {
            var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (var h in (System.Collections.IList)typeof(FlapSystem).GetField("hlds", bf).GetValue(fcs.flaps)) h.GetType().GetField("pos").SetValue(h, r0["hlPos"]);
            fcs.flaps.hlPos = r0["hlPos"];
        }
        if (r0.ContainsKey("qT")) { fcs.qTraj.Reset(r0["qT"]); fcs.qTraj.v = D("qT"); fcs.qT = r0["qT"]; }
        if (r0.ContainsKey("pCmd") && r0.ContainsKey("alpha"))
        {
            float ca = Mathf.Max(0.3f, Mathf.Cos(r0["alpha"] * Mathf.Deg2Rad));
            float pS = r0["pCmd"] / ca;
            fcs.pTraj.Reset(pS); fcs.pTraj.v = D("pCmd") / ca; fcs.pS = pS; fcs.pCmd = r0["pCmd"]; if (r0.ContainsKey("rCmd")) fcs.rCmd = r0["rCmd"];
        }
        if (r0.ContainsKey("gInt")) fcs.gInt = r0["gInt"];
        if (r0.ContainsKey("aInt")) fcs.aInt = r0["aInt"];
    }
}
