// Ifrit (stock KR-67, NO Multirole1) exploration: static authority maps and stock-FBW / FCS manoeuvre runs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class IfritTest
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    // FCS config for the stock Ifrit: none of the F-22E airframe changes, stock surface travel and TVC slew
    public static void Defaults()
    {
        Plugin.StallStagger = false; Plugin.BodyLiftShaping = false; Plugin.StabDownwash = 0f;
        Plugin.RudderRange = 0f;   // 0.2.7: canard travel from the config (45)   // 0.3.0: FlaperonRange from the config (30)
        Plugin.TvcSlew = 70f; Plugin.ActuatorScale = 1f;   // 31 Oct calibration: ThrustScale / DragScale stay at the config's profile (Buffed 1.08 / 0.9), as in game
        Plugin.DragReliefAoA = 0f; Plugin.DragReliefAoAHigh = 0f; Plugin.DragReliefTransonic = 0f;
        Plugin.LerxEffectiveness = 1f; Plugin.CgShiftAft = 0f; Plugin.FlaperonEffectiveness = 1f; Plugin.StabilatorEffectiveness = 1f;
        Plugin.WingCamberK = 0f; Plugin.FinCamberK = 0f; Plugin.RudderPitchAid = false;
        Plant.AirbrakeAtIdle = false;
        // 31 Oct calibration (CALIBRATION.md): the game's Revoker weighed 11.8-12.6 t in the 30 Oct logs (full tanks at spawn plus
        // stores; logged T/W ceiling at full AB against the game's thrust model). The ballast above empty+fuel goes on the weapon bays.
        // Was 50 % fuel and no stores. Override per log with MassTarget=<kg>.
        Airframe.FuelFrac = 1f; Airframe.MassTarget = 12200f;
        // bench-only fitted factors (4 logs, 3 train / 1 held-out): thrust lever arm, static trim, effective inertia
        Plant.NozzleArmScale = 1.12f; Airframe.CgAftShift = -0.10f; Airframe.IScaleRoll = 1.04f; Airframe.IScalePitch = 0.96f;
        if (Program.dataPath.Contains("compass"))
        {
            // T/A-30 Compass: not calibrated against flight logs yet (no Compass logs exist). Spawn fuel = the game's
            // DefaultFuelLevel 0.45, no stores, no fitted factors.
            Airframe.FuelFrac = 0.45f; Airframe.MassTarget = -1f;
            Plant.NozzleArmScale = 1f; Airframe.CgAftShift = 0f; Airframe.IScaleRoll = 1f; Airframe.IScalePitch = 1f;
            // the Compass FCS's own airframe settings (shipped defaults; override per run with Name=value)
            Plugin.StabDownwash = 0.45f; Plugin.FinCamberK = 0.55f; Plugin.ActuatorScale = 1f; Plugin.CgShiftAft = 0.15f; Plugin.LerxEffectiveness = 3.4f;   // the Ifrit line above zeroes them (0.1.3 defaults)
        }
    }

    // body angular acceleration (deg/s^2; p roll right, q nose up, r nose right) of the current pose
    public static Vector3 Acc(Plant P)
    {
        P.Step();
        var Mb = Quaternion.Inverse(P.q) * P.lastTorque;
        var a = Solve(P.A.Itot, Mb);   // root/body frame (Unity axes)
        return new Vector3(-a.z, -a.x, a.y) * Mathf.Rad2Deg;
    }
    public static float Nz(Plant P) => Vector3.Dot(P.lastForce - Physics.gravity * P.A.mass, P.q * Vector3.up) / (P.A.mass * 9.81f);
    static Vector3 Solve(double[,] A, Vector3 b)
    {
        double det = A[0, 0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) - A[0, 1] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0]) + A[0, 2] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]);
        double[,] inv = new double[3, 3];
        inv[0, 0] = (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) / det; inv[0, 1] = (A[0, 2] * A[2, 1] - A[0, 1] * A[2, 2]) / det; inv[0, 2] = (A[0, 1] * A[1, 2] - A[0, 2] * A[1, 1]) / det;
        inv[1, 0] = (A[1, 2] * A[2, 0] - A[1, 0] * A[2, 2]) / det; inv[1, 1] = (A[0, 0] * A[2, 2] - A[0, 2] * A[2, 0]) / det; inv[1, 2] = (A[0, 2] * A[1, 0] - A[0, 0] * A[1, 2]) / det;
        inv[2, 0] = (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]) / det; inv[2, 1] = (A[0, 1] * A[2, 0] - A[0, 0] * A[2, 1]) / det; inv[2, 2] = (A[0, 0] * A[1, 1] - A[0, 1] * A[1, 0]) / det;
        return new Vector3((float)(inv[0, 0] * b.x + inv[0, 1] * b.y + inv[0, 2] * b.z), (float)(inv[1, 0] * b.x + inv[1, 1] * b.y + inv[1, 2] * b.z), (float)(inv[2, 0] * b.x + inv[2, 1] * b.y + inv[2, 2] * b.z));
    }

    public sealed class Cfg
    {
        public Dictionary<string, float> defl = new Dictionary<string, float>();   // surface name -> deg (sum channel)
        public Dictionary<string, float> split = new Dictionary<string, float>();  // surface name -> split deg
        public float tvcL, tvcR;   // nozzle angle deg (+ = nose up)
    }
    public static void Apply(Plant P, Cfg c)
    {
        foreach (var cs in P.A.surfaces)
        {
            float d = c.defl.TryGetValue(cs.name, out var x) ? x : 0f;
            cs.job.currentPitch = d; cs.job.currentRoll = 0; cs.job.currentYaw = 0;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
            cs.splitAmount = c.split.TryGetValue(cs.name, out var sp) ? sp : 0f;
        }
        P.TvcAngleOverride = (tf, n) => tf.name.EndsWith("_L") ? c.tvcL : c.tvcR;
    }

    public static Plant Make(float alt, float tasOrIasKmh, bool ias, float throttle, out float tas)
    {
        var A = new Airframe(Program.dataPath);
        var P = new Plant(A);
        float rho = Plant.Density(alt);
        tas = ias ? tasOrIasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f) : tasOrIasKmh;
        P.throttle = throttle; P.A.ac.inputs.throttle = throttle;
        return P;
    }

    // ---- static authority maps
    public static void Auth(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f); float thr = F(kv, "thr", 1f);
        var iasList = S(kv, "iasl", "200,300,450,650");
        float a0 = F(kv, "a0", -40f), a1 = F(kv, "a1", 90f), da = F(kv, "da", 5f);
        float tailMax = F(kv, "tail", 35f), tvcMax = F(kv, "tvcmax", 15f);
        foreach (var iasK in iasList)
        {
            var P = Make(alt, iasK, true, thr, out float tas); P.EvalOnly = true;
            Console.WriteLine($"\n# IAS {iasK:F0} km/h (TAS {tas*3.6f:F0}) alt {alt:F0} thr {thr:F2}  mass {P.A.mass:F0} kg  I(p,q,r)=({P.A.Itot[2,2]:F0},{P.A.Itot[0,0]:F0},{P.A.Itot[1,1]:F0})");
            Console.WriteLine("# pitch accel deg/s^2: q0 = tails 0/TVC 0; dnA = tails -tail; dnT = tails -tail + TVC -tvcmax; dn45 = tails -45 + TVC -20; upT = tails +tail + TVC +tvcmax; nz0 = load factor at q0");
            Console.WriteLine("# lateral (deltas vs neutral, deg/s^2, body p / r and stability-axis pS = p cos a + r sin a, rS = r cos a - p sin a):");
            Console.WriteLine("#   ROLLsurf = ail +-30 & flap +-20 roll-right;  TAILdiff = tails +-10 yaw-right; SPLIT = right flap split 25; TVCroll = nozzles +-15 differential");
            Console.WriteLine($"{"alpha",5} {"nz0",5} {"q0",7} {"dnA",7} {"dnT",7} {"dn45",7} {"upT",7} {"upA",6} {"up45",6} | {"ROLLs pS",8} {"rS",6} | {"TAILd pS",8} {"rS",6} | {"SPLIT pS",8} {"rS",6} | {"TVCr pS",8} {"rS",6}");
            for (float a = a0; a <= a1 + 1e-3f; a += da)
            {
                P.Init(alt, tas, a, a);
                Vector3 Q(Cfg c) { Apply(P, c); return Acc(P); }
                var c0 = new Cfg(); var q0 = Q(c0); float nz0 = Nz(P);
                Cfg Sym(float tail, float tv) { var c = new Cfg(); c.defl["Elevator_L"] = tail; c.defl["Elevator_R"] = tail; c.tvcL = tv; c.tvcR = tv; return c; }
                var dnA = Q(Sym(-tailMax, 0)); var dnT = Q(Sym(-tailMax, -tvcMax)); var dn45 = Q(Sym(-45, -20)); var upT = Q(Sym(tailMax, tvcMax)); var upA = Q(Sym(tailMax, 0)); var up45 = Q(Sym(45, 20));
                var cr = new Cfg(); cr.defl["Aileron_R"] = 30; cr.defl["Aileron_L"] = -30; cr.defl["Flap_R"] = 20; cr.defl["Flap_L"] = -20; var rl = Q(cr) - q0;
                var ct = new Cfg(); ct.defl["Elevator_R"] = -10; ct.defl["Elevator_L"] = 10; var td = Q(ct) - q0;
                var csp = new Cfg(); csp.split["Flap_R"] = 25; var sp = Q(csp) - q0;
                var cv = new Cfg(); cv.tvcL = -15; cv.tvcR = 15; var tv = Q(cv) - q0;
                string L(Vector3 d) { float ar = a * Mathf.Deg2Rad; float pS = d.x * Mathf.Cos(ar) + d.z * Mathf.Sin(ar); float rS = d.z * Mathf.Cos(ar) - d.x * Mathf.Sin(ar); return $"{pS,8:F0} {rS,6:F0}"; }
                Console.WriteLine($"{a,5:F0} {nz0,5:F2} {q0.y,7:F0} {dnA.y,7:F0} {dnT.y,7:F0} {dn45.y,7:F0} {upT.y,7:F0} {upA.y,6:F0} {up45.y,6:F0} | {L(rl)} | {L(td)} | {L(sp)} | {L(tv)}");
            }
        }
    }

    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
    static float[] S(Dictionary<string, string> kv, string k, string d) => (kv.TryGetValue(k, out var v) ? v : d).Split(',').Select(x => float.Parse(x, IC)).ToArray();
}

internal sealed class FlyLog
{
    public float t, ias, alpha, beta, nz, sOuterWing, sTip, sTail, sElev, sWing1, p, q, r, pV, phi, theta, turn, gamma, thr, tailL, tailR, ailL, ailR, flapL, flapR, splitL, splitR, tvcL, tvcR, hdg, alt, qCmd, qT, pCmd, rCmd, aUp, aDn, tgtX, tgtY, tgtZ, achX, achY, achZ, sp, sr, sy, slipPred, vmcLim, decelF, yawSat, alphaCmd, asymCap, thrL, thrR, splitTier, tDem, tReach, tSplit, tvcHold, fightK, aoaL, aoaR, abGate, abRatio, thrIn, engSt, ratioF, aU, aAr, aAy, aCr, aCy, qPk, aMTy, tvcAct, mLoss, sUse, mF, sWin, dMR, dMY, rTrim, aRP, aRN, aDy, tyH, tyN, rud, capRaw, aUraw, rMx, pMx, pSmx, pSt, rSc, yMid, yMarg, qPk2;
}

internal static class IfritFly
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static float Prof(string prof, float t, float dflt)
    {
        if (string.IsNullOrEmpty(prof)) return dflt;
        var pts = prof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray();
        if (t < pts[0].Item1) return pts[0].Item2;
        for (int k = 0; k < pts.Length - 1; k++) if (t >= pts[k].Item1 && t < pts[k + 1].Item1)
            return pts[k].Item2 + (pts[k + 1].Item2 - pts[k].Item2) * (t - pts[k].Item1) / Math.Max(1e-3f, pts[k + 1].Item1 - pts[k].Item1);
        return pts[pts.Length - 1].Item2;
    }

    static string Canon(string n) { switch (n.ToLowerInvariant()) { case "flap_l": return "Flap_L"; case "flap_r": return "Flap_R"; case "aileron_l": return "Aileron_L"; case "aileron_r": return "Aileron_R"; case "elevator_l": case "tail_l": case "stab_l": return "Elevator_L"; case "elevator_r": case "tail_r": case "stab_r": return "Elevator_R"; case "canard_l": return "Elevator_L"; case "canard_r": return "Elevator_R"; case "rudder": return "Flap_L"; } return n; }
    public static Action<Plant> OnCreate;
    public static float EngOutT = -99f;   // 0.4.0: engine_L dies this long after t=0 (-99 = never)
    public static float EngDmg = -1f;    // 0.4.1: engine_L condition (0..1) applied at EngOutT instead of killing it (-1 = kill)
    public static float EffDumpT0 = -1, EffDumpT1 = -1;
    public static string BrakeProf; public static bool SplitDbg; public static bool AbDbg;   // brake input profile "t:v,..." (0..1)
    public static string[] DbgFields; public static List<float[]> DbgRows = new List<float[]>();   // hook for FCS-extension wiring (split / roll-TVC effectors)

    // ctrl: stock | fcs.  pitch/roll/yaw profiles "t:v,..." in pilot sense (+ = pull / right / right), t from start (after trimT)
    static float EngThr(Plant P, string suf) { float t = 0f; foreach (var e in P.A.engines) if (e.name.EndsWith(suf) && e.nozzles != null) foreach (var n in e.nozzles) t += n.totalThrust; return t * 0.001f; }
    static float RudNow(Plant P) { foreach (var cs in P.A.surfaces) if (cs.name == "rudder") return cs.job.currentPitch + cs.job.currentRoll + cs.job.currentYaw; return 0f; }

    public static float ExtYaw, ExtRoll;
    public static string FcsLogPath;
    // damage model (sim/Damage.cs): set by Cmd from dmg / dmgAt / detachAt / detach / ripOff / dmgCsv; times from the profile start
    public static Scenario DmgSc;
    public static List<FlyLog> Fly(string ctrl, float alt, float iasKmh, float thr, string pitch, string roll, string yaw, float T, float trimT = 1f, float gamma0 = 0f, string thrProf = null)
    {
        var A = new Airframe(Program.dataPath);
        var DM = (DmgSc != null && DamageModel.Wanted(DmgSc)) ? new DamageModel(A, DmgSc, DamageModel.DataPathFor(Program.dataPath)) : null;
        var P = new Plant(A);
        DM?.Attach(P);
        float rho = Plant.Density(alt);
        float tas = iasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float aT = Program.TrimAlpha(P, alt, tas, out float thrTrim);
        P.Init(alt, tas, aT, aT + gamma0);
        P.throttle = thr >= 0 ? thr : thrTrim;
        if (ctrl == "stock") P.stock = new StockFbw(A.stockFbwJ); else P.CreateFcs();
        OnCreate?.Invoke(P);
        // external body moment, deg/s^2 (an asymmetric drag / store load stand-in): Unity y = yaw (nose right +), z = roll
        if (ExtYaw != 0f || ExtRoll != 0f) { var I = A.Itot; P.ext = pl => (Vector3.zero, pl.q * new Vector3(0f, (float)I[1,1] * ExtYaw * Mathf.Deg2Rad, -(float)I[2,2] * ExtRoll * Mathf.Deg2Rad)); }
        P.PartForce = new Dictionary<PartBody, Vector3>();
        var groups = StructGroups(A);
        foreach (var e in A.engines) e.rpm = Mathf.Lerp(e.minRPM, e.maxRPM, Plant.ThrottleOf(e, P.throttle));
        foreach (var e in A.engines) foreach (var n0 in e.nozzles) n0.abAmt = Mathf.InverseLerp(n0.abStart, 1f, P.throttle);
        var log = new List<FlyLog>();
        var inp = A.ac.inputs;
        Vector3 vPrev = P.vCg;
        float tEnd = trimT + T;
        // 0.3.0 governor port: the FCS's own telemetry (the game's log columns) to FcsLogPath, t relative to trimT
        StreamWriter fw = null;
        if (FcsLogPath != null && P.fcs != null) { fw = new StreamWriter(FcsLogPath); P.fcs.WriteHeader(fw); fw.WriteLine(); }
        while (P.time < tEnd)
        {
            float t = P.time - trimT;
            float ps = t >= 0 ? Prof(pitch, t, 0f) : 0f, rs = t >= 0 ? Prof(roll, t, 0f) : 0f, ys = t >= 0 ? Prof(yaw, t, 0f) : 0f;
            inp.pitch = -ps; inp.roll = rs; inp.yaw = ys; inp.brake = (t >= 0 && BrakeProf != null) ? Prof(BrakeProf, t, 0f) : 0f;
            if (thrProf != null && t >= 0) P.throttle = Prof(thrProf, t, P.throttle);
            if (EngOutT > -90f && t >= EngOutT) foreach (var e0 in P.A.engines) if (e0.name.Contains("_L")) { if (EngDmg >= 0f) e0.condition = EngDmg; else e0.operable = false; }
            P.Step();
            DM?.PostStep(t, P.time);
            if (float.IsNaN(P.vCg.x)) break;
            if (fw != null) { P.fcs.WriteRow(fw, t, ps, rs, ys, rho); fw.WriteLine(); }
            if (AbDbg && P.fcs != null && t >= 0 && Math.Abs(t * 4 - Math.Round(t * 4)) < 0.03f)
                Console.WriteLine($"   ab t {t:F2} brake {inp.brake:F2} rawThr {P.fcs.rawThrottle:F2} airBrake {P.fcs.airBrake:F2} out {P.fcs.AirbrakeOut} split {P.fcs.SplitDeploy} target {P.fcs.AirbrakeOpenTarget:F2} ground {P.fcs.groundBrake:F2}");
            if (SplitDbg && P.fcs != null && t >= 0 && Math.Abs(t * 4 - Math.Round(t * 4)) < 0.03f)
                foreach (var sx in P.fcs.surfaces) Console.WriteLine($"   surf t {t:F2} {sx.name} now {sx.now:F1} cmd {sx.cmd:F1} out {sx.cmdOut:F1} lo {sx.lo:F1} hi {sx.hi:F1} laoa {sx.localAoANow:F1} stall {sx.stallAoA:F1}");
            if (SplitDbg && P.fcs != null && t >= 0 && Math.Abs(t * 4 - Math.Round(t * 4)) < 0.03f)
                foreach (var se in P.fcs.splits) { var cs0 = se.cs; Console.WriteLine($"   split t {t:F2} {se.name} brake {se.brakeMode} now {se.now:F1} cmd {se.cmd:F1} out {se.cmdOut:F1} lo {se.lo:F1} hi {se.hi:F1} max {se.max:F1} job {cs0.splitAmount:F1} jobMax {cs0.job.maxSplit:F1} partner {(se.partner != null)} retract {P.fcs.splitRetract}"); }
            var L = new FlyLog { t = t, sp = ps, sr = rs, sy = ys, slipPred = P.fcs != null ? P.fcs.slipPredDbg : 0f, vmcLim = P.fcs != null ? P.fcs.vmcLim : 1f, decelF = P.fcs != null ? P.fcs.decelDbg : 0f, yawSat = P.fcs != null ? P.fcs.yawSat : 0f, alphaCmd = P.fcs != null ? P.fcs.alphaCmd : 0f, asymCap = P.fcs != null ? Mathf.Min(P.fcs.asymCapPosEff, 99f) : 99f, splitTier = P.fcs != null ? P.fcs.splitTierDbg : 0f, tDem = P.fcs != null ? P.fcs.tierDemDbg : 0f, tReach = P.fcs != null ? P.fcs.tierReachDbg : 0f, tSplit = P.fcs != null ? P.fcs.tierSplitDbg : 0f, tvcHold = P.fcs != null ? P.fcs.tvcNeutralDbg : 0f, fightK = P.fcs != null ? P.fcs.alloc.fightK : 0f, aoaL = P.fcs != null ? P.fcs.StabLocalAoA(0) : 0f, aoaR = P.fcs != null ? P.fcs.StabLocalAoA(1) : 0f, abGate = P.fcs != null && P.fcs.abGateOn ? 1f : 0f, abRatio = P.fcs != null ? P.fcs.engAbRatio : 1f, thrIn = P.A.ac.inputs.throttle, engSt = P.fcs != null ? (P.fcs.engOut ? 1f : (P.fcs.engAsym ? 2f : 0f)) : 0f, ratioF = P.fcs != null ? P.fcs.engRatioF : 1f, aU = P.fcs != null ? P.fcs.asymUNow : 0f, aAr = P.fcs != null ? P.fcs.asymArNow : 0f, aAy = P.fcs != null ? P.fcs.asymAyNow : 0f, aCr = P.fcs != null ? P.fcs.asymCrNow : 0f, aCy = P.fcs != null ? P.fcs.asymCyNow : 0f, qPk = P.fcs != null ? P.fcs.qPredK : 1f, aMTy = P.fcs != null ? P.fcs.AsymMTYaw : 0f, tvcAct = P.fcs != null ? P.fcs.TvcActiveDbg : 0, mLoss = P.fcs != null ? P.fcs.engMatchLoss * 0.001f : 0f, thrL = EngThr(P, "_L"), thrR = EngThr(P, "_R"), sUse = P.fcs != null ? P.fcs.splitUsage : 0f, mF = P.fcs != null ? P.fcs.matchF : 0f, sWin = P.fcs != null && P.fcs.settledWinDbg ? 1f : 0f, dMR = P.fcs != null ? P.fcs.dMeasR : 0f, dMY = P.fcs != null ? P.fcs.dMeasY : 0f, rTrim = P.fcs != null ? P.fcs.dTrimR : 0f, aRP = P.fcs != null ? P.fcs.aRollPos : 0f, aRN = P.fcs != null ? P.fcs.aRollNeg : 0f, aDy = P.fcs != null ? P.fcs.asymDy * 100f : 0f, tyH = P.fcs != null ? P.fcs.tvcYawNeutralW : 0f, tyN = (P.fcs != null && P.fcs.tvcy != null) ? P.fcs.tvcy.now : 0f, rud = RudNow(P), capRaw = P.fcs != null ? Mathf.Min(P.fcs.asymCapPos, 99f) : 99f, aUraw = P.fcs != null ? P.fcs.asymUNow : 0f, rMx = P.fcs != null ? P.fcs.rMaxDbg : 0f, pMx = P.fcs != null ? P.fcs.pMaxDbg : 0f, pSmx = P.fcs != null ? P.fcs.pSmaxDbg : 0f, pSt = P.fcs != null ? P.fcs.pSTgtDbg : 0f, rSc = P.fcs != null ? P.fcs.rSDbg : 0f, yMid = P.fcs != null ? P.fcs.yawMidDbg : 0f, yMarg = P.fcs != null ? P.fcs.yawMarginDbg : 0f, qPk2 = P.fcs != null ? P.fcs.latPredK : 0f };
            var vb = Quaternion.Inverse(P.q) * P.vCg; float V = vb.magnitude;
            L.alpha = Mathf.Atan2(-vb.y, vb.z) * Mathf.Rad2Deg; L.beta = Mathf.Asin(Mathf.Clamp(vb.x / V, -1, 1)) * Mathf.Rad2Deg;
            L.ias = V * Mathf.Sqrt(Plant.Density(P.pCg.y) / 1.225f) * 3.6f;
            L.nz = Vector3.Dot(P.lastForce - Physics.gravity * A.mass, P.q * Vector3.up) / (A.mass * 9.81f);
            var rt = P.RatesDeg(); L.p = rt.x; L.q = rt.y; L.r = rt.z;
            L.pV = Vector3.Dot(P.w, P.vCg.normalized) * Mathf.Rad2Deg * -1f;   // + = roll right about the velocity vector
            var e = P.q.eulerAngles; L.theta = -Norm(e.x); L.phi = -Norm(e.z); L.hdg = e.y;
            L.turn = Vector3.Angle(vPrev, P.vCg) / P.dt; vPrev = P.vCg;
            L.gamma = Mathf.Asin(Mathf.Clamp(P.vCg.y / V, -1, 1)) * Mathf.Rad2Deg; L.alt = P.pCg.y;
            L.thr = A.ac.inputs.throttle;
            foreach (var cs in A.surfaces)
            {
                float d = cs.job.currentPitch + cs.job.currentRoll + cs.job.currentYaw;
                switch (Canon(cs.name)) { case "Elevator_L": L.tailL = d; break; case "Elevator_R": L.tailR = d; break; case "Aileron_L": L.ailL = d; break; case "Aileron_R": L.ailR = d; break; case "Flap_L": L.flapL = d; L.splitL = cs.splitAmount; break; case "Flap_R": L.flapR = d; L.splitR = cs.splitAmount; break; }
            }
            if (A.revoker && P.fcs != null) foreach (var hz in P.fcs.hinges) { if (hz.left) L.splitL = hz.hingeNow; else L.splitR = hz.hingeNow; }
            foreach (var en in A.engines) { float n = Mathf.Clamp(en.nozzleAngles.x + en.nozzleAngles.z, -20, 20); if (A.revoker) { L.tvcL = n; L.tvcR = en.nozzleAngles.y; } else if (en.name.EndsWith("_L")) L.tvcL = n; else L.tvcR = n; }
            {
                Vector3 acg = P.lastForce / A.mass;   // includes gravity
                float Ratio(string g) { var G = groups[g]; Vector3 fj = Vector3.zero; foreach (var b in G.parts) { P.PartForce.TryGetValue(b, out var fa); fj += b.part.mass * acg - (fa + Physics.gravity * b.part.mass); } return fj.magnitude / G.cap; }
                L.sOuterWing = Ratio("outerWing"); L.sTip = Ratio("tip"); L.sTail = Ratio("tail"); L.sElev = Ratio("elev"); L.sWing1 = Ratio("wing1");
            }
            if (P.fcs != null)
            {
                var c = P.fcs; L.qCmd = c.qCmd; L.qT = c.qT; L.pCmd = c.pCmd; L.rCmd = c.rCmd; L.aUp = c.aUp; L.aDn = c.aDn;
                L.tgtX = c.mTarget.x * c.IaInv[0, 0] * Mathf.Rad2Deg; L.tgtY = c.mTarget.y * c.IaInv[1, 1] * Mathf.Rad2Deg; L.tgtZ = c.mTarget.z * c.IaInv[2, 2] * Mathf.Rad2Deg;
                Vector3 ach = Vector3.zero; foreach (var ef in c.effs) if (ef.active) ach += ef.MAt(ef.cmd);
                L.achX = ach.x * c.IaInv[0, 0] * Mathf.Rad2Deg; L.achY = ach.y * c.IaInv[1, 1] * Mathf.Rad2Deg; L.achZ = ach.z * c.IaInv[2, 2] * Mathf.Rad2Deg;
            }
            if (t >= 0) log.Add(L);
            if (EffDumpT0 >= 0 && t >= EffDumpT0 && t <= EffDumpT1 && P.fcs != null && ((int)Math.Round(t / P.dt)) % 5 == 0)
                Console.WriteLine($"   eff t {t:F2} " + string.Join(" | ", P.fcs.effs.Select(e => $"{e.GetType().Name.Substring(0, 3)}{(e is SurfaceEffector se ? se.name : "")} c{e.cmd:F1} n{e.now:F1} [{e.lo:F1},{e.hi:F1}] a{(e.active ? 1 : 0)} st{e.symTarget:F1}")));
            if (t >= 0 && DbgFields != null && P.fcs != null)
            {
                var row = new float[DbgFields.Length + 1]; row[0] = t;
                for (int i = 0; i < DbgFields.Length; i++) { var fi = typeof(FcsController).GetField(DbgFields[i], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); object v = fi?.GetValue(P.fcs); row[i + 1] = v is bool b ? (b ? 1 : 0) : v is float f2 ? f2 : v is int i2 ? i2 : float.NaN; }
                DbgRows.Add(row);
            }
        }
        DM?.Close();
        fw?.Close();   // 5 Nov: the FCS log was never closed, so its last row could be cut mid-number
        return log;
    }
    public sealed class SGroup { public List<PartBody> parts = new List<PartBody>(); public float cap; }
    // structural groups (left side): joint force the group's attachment joints must carry, over 10 x the sum of their breakForce (game: Joint.breakForce = 10 x PartJoint.breakForce)
    static Dictionary<string, SGroup> StructGroups(Airframe A)
    {
        var d = new Dictionary<string, SGroup>();
        var byName = A.bodies.ToDictionary(b => b.part.name, b => b);
        var tidOf = A.PartByTidPublic.ToDictionary(kv => kv.Value, kv => kv.Key);
        void G(string key, string[] names)
        {
            var g = new SGroup(); var set = new HashSet<string>(names);
            foreach (var n in names) if (byName.TryGetValue(n, out var b)) g.parts.Add(b);
            float cap = 0f;
            foreach (var b in g.parts)
                if (A.JointsRaw.TryGetValue(b.part, out var js))
                    foreach (var j in js.EnumerateArray())
                    {
                        var to = j.GetProperty("to"); string tn = null;
                        if (to.ValueKind == System.Text.Json.JsonValueKind.String && A.PartByTidPublic.TryGetValue(to.GetString(), out var tp)) tn = tp.name;
                        if (tn != null && !set.Contains(tn)) cap += 10f * j.GetProperty("bf").GetSingle();
                    }
            // joints defined on outside parts that connect into the group
            foreach (var kv in A.JointsRaw)
            {
                if (set.Contains(kv.Key.name)) continue;
                foreach (var j in kv.Value.EnumerateArray())
                {
                    var to = j.GetProperty("to");
                    if (to.ValueKind == System.Text.Json.JsonValueKind.String && A.PartByTidPublic.TryGetValue(to.GetString(), out var tp) && set.Contains(tp.name)) cap += 10f * j.GetProperty("bf").GetSingle();
                }
            }
            g.cap = Mathf.Max(1f, cap); d[key] = g;
        }
        G("outerWing", new[] { "wing2_L", "wingtip_L", "Aileron_L", "wing2_FL" });
        G("tip", new[] { "wingtip_L" });
        G("tail", new[] { "tail_L" });
        G("elev", new[] { "Elevator_L" });
        G("wing1", new[] { "wing1_L", "Flap_L", "wing1_FL", "wing2_L", "wingtip_L", "Aileron_L", "wing2_FL" });
        return d;
    }
    static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }

    public static void WriteCsv(string path, List<FlyLog> log)
    {
        using var w = new StreamWriter(path);
        var fs = typeof(FlyLog).GetFields();
        w.WriteLine(string.Join(",", fs.Select(f => f.Name)));
        foreach (var L in log) w.WriteLine(string.Join(",", fs.Select(f => ((float)f.GetValue(L)).ToString("0.###", IC))));
    }

    public static void Cmd(Dictionary<string, string> kv)
    {
        string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
        float Fv(string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
        if (kv.TryGetValue("dbg", out var dbs)) DbgFields = dbs.Split(',');
        EngOutT = Fv("engout", -99f); EngDmg = Fv("engdmg", -1f);
        Airframe.Detach = G("detach", null);
        // loaded-reversal port (Nov 2026): an asymmetric store on one part, as the single-case mode has it
        Airframe.StoreKg = Fv("storeKg", 0f); Airframe.StorePart = G("storePart", Airframe.StorePart);
        DmgSc = new Scenario { dmg = G("dmg", null), dmgAt = G("dmgAt", null), detachAt = G("detachAt", null), detach = G("detach", null), ripOff = Fv("ripOff", -1f), dmgCsv = G("dmgCsv", null) }; ExtYaw = Fv("yawExt", 0f); ExtRoll = Fv("rollExt", 0f);
        BrakeProf = G("brake", null); if (kv.TryGetValue("effdump", out var edS)) { var pp = edS.Split('-'); EffDumpT0 = float.Parse(pp[0], IC); EffDumpT1 = float.Parse(pp[1], IC); } SplitDbg = kv.ContainsKey("splitdbg"); AbDbg = kv.ContainsKey("abdbg");
        Plant lastP = null; var prevOC = OnCreate; OnCreate = P0 => { lastP = P0; prevOC?.Invoke(P0); };
        var log = Fly(G("ctrl", "fcs"), Fv("alt", 3000), Fv("ias", 450), Fv("thr", 1f), G("pitch", null), G("roll", null), G("yaw", null), Fv("T", 6f), 1f, Fv("gamma0", 0f), G("thrp", null));
        if (kv.TryGetValue("csv", out var csv)) WriteCsv(csv, log);
        if (kv.ContainsKey("foilinfo") && lastP?.fcs != null) foreach (var se in lastP.fcs.surfaces) Console.WriteLine($"  foil {se.name} kind {se.kind} min {se.min:F0} max {se.max:F0} stall {se.stallAoA:F1} knee15 {se.KneeAoA(0.15f):F1}");
        Summ(log, G("ctrl", "fcs") + " " + G("tag", ""));
        if (kv.ContainsKey("surfdump") && lastP?.fcs != null) foreach (var se in lastP.fcs.surfaces) Console.WriteLine($"surf {se.name} {se.kind} active={se.active} min={se.min:F1} max={se.max:F1} rate={se.rate:F0} now={se.now:F2}");
        if (kv.ContainsKey("sweep") && lastP?.fcs != null)
        {
            var fc = lastP.fcs; var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            float[] Arr(string n) => (float[])typeof(FcsController).GetField(n, BF | System.Reflection.BindingFlags.Static).GetValue(fc);
            var al = Arr("asymAlphas"); var u = Arr("asymU"); var ar = Arr("asymAr"); var ay = Arr("asymAy"); var cr = Arr("asymCr"); var cy = Arr("asymCy");
            Console.WriteLine("  alpha      U     Ar     Ay     Cr     Cy");
            for (int i = 0; i < al.Length; i++) Console.WriteLine($"{al[i],7:F1} {u[i],6:F2} {ar[i],6:F1} {ay[i],6:F1} {cr[i],6:F0} {cy[i],6:F0}");
        }
        if (DbgFields != null && kv.TryGetValue("dbgcsv", out var dcsv))
        {
            using var dw = new StreamWriter(dcsv);
            dw.WriteLine("t," + string.Join(",", DbgFields));
            foreach (var r in DbgRows) dw.WriteLine(string.Join(",", r.Select(x => x.ToString("0.#####", IC))));
            DbgFields = null;
        }
        if (DbgFields != null)
        {
            float every = Fv("printd", 0.25f); float nx = 0;
            Console.WriteLine("    t " + string.Join(" ", DbgFields.Select(f => f.Length > 9 ? f.Substring(0, 9) : f.PadLeft(9))));
            foreach (var r in DbgRows) if (r[0] >= nx - 1e-4f) { nx += every; Console.WriteLine($"{r[0],5:F2} " + string.Join(" ", r.Skip(1).Select(x => x.ToString("F2", IC).PadLeft(9)))); }
        }
        if (kv.ContainsKey("printc"))
        {
            float every = Fv("printc", 0.25f); float nx = 0;
            Console.WriteLine("    t alpha     q   qCmd     qT  pCmd  rCmd   aUp   aDn |  tgtX  achX   tgtY  achY   tgtZ  achZ");
            foreach (var L in log) if (L.t >= nx - 1e-4f) { nx += every;
                Console.WriteLine($"{L.t,5:F2} {L.alpha,5:F1} {L.q,5:F1} {L.qCmd,6:F1} {L.qT,6:F1} {L.pCmd,5:F0} {L.rCmd,5:F0} {L.aUp,5:F0} {L.aDn,5:F0} | {L.tgtX,5:F0} {L.achX,5:F0} {L.tgtY,6:F0} {L.achY,5:F0} {L.tgtZ,6:F0} {L.achZ,5:F0}"); }
        }
        if (kv.ContainsKey("print"))
        {
            float every = Fv("print", 0.25f); float nx = 0;
            Console.WriteLine("    t   ias alpha  beta    nz     p     q     r    pV   phi theta  turn | tailL tailR  ailL  ailR flapL flapR  spL  spR  tvL  tvR");
            foreach (var L in log) if (L.t >= nx - 1e-4f) { nx += every;
                Console.WriteLine($"{L.t,5:F2} {L.ias,5:F0} {L.alpha,5:F1} {L.beta,5:F1} {L.nz,5:F2} {L.p,5:F0} {L.q,5:F1} {L.r,5:F1} {L.pV,5:F0} {L.phi,5:F0} {L.theta,5:F0} {L.turn,5:F1} | {L.tailL,5:F1} {L.tailR,5:F1} {L.ailL,5:F1} {L.ailR,5:F1} {L.flapL,5:F1} {L.flapR,5:F1} {L.splitL,4:F0} {L.splitR,4:F0} {L.tvcL,4:F0} {L.tvcR,4:F0}"); }
        }
    }
    public static void Summ(List<FlyLog> log, string tag)
    {
        if (log.Count == 0) { Console.WriteLine(tag + ": no data"); return; }
        Console.WriteLine($"{tag}: struct load/capacity max: outerWing {log.Max(l => l.sOuterWing):F2} tip {log.Max(l => l.sTip):F2} wing1 {log.Max(l => l.sWing1):F2} fin {log.Max(l => l.sTail):F2} elev {log.Max(l => l.sElev):F2}");
        Console.WriteLine($"{tag}: alphaMax {log.Max(l => l.alpha):F1} alphaMin {log.Min(l => l.alpha):F1} nzMax {log.Max(l => l.nz):F2} nzMin {log.Min(l => l.nz):F2} turnMax {log.Max(l => l.turn):F1} |beta|max {log.Max(l => Mathf.Abs(l.beta)):F1} pVmax {log.Max(l => Mathf.Abs(l.pV)):F0} qMax {log.Max(l => l.q):F1} iasEnd {log.Last().ias:F0} nan {(float.IsNaN(log.Last().alpha) ? 1 : 0)}");
    }
}

internal static class IfritEff
{
    // per-effector body accel deltas (deg/s^2, aero p,q,r) at full + and full - travel vs all-neutral, per alpha; JSON lines to stdout
    public static void Dump(Dictionary<string, string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float alt = kv.TryGetValue("alt", out var a0s) ? float.Parse(a0s, IC) : 3000f;
        float thr = kv.TryGetValue("thr", out var ts) ? float.Parse(ts, IC) : 1f;
        float tail = kv.TryGetValue("tail", out var tl) ? float.Parse(tl, IC) : 45f;
        var iasL = (kv.TryGetValue("iasl", out var il) ? il : "150,200,250,300,400,550,700").Split(',').Select(x => float.Parse(x, IC));
        foreach (var ias in iasL)
        {
            var P = IfritTest.Make(alt, ias, true, thr, out float tas); P.EvalOnly = true;
            for (float a = 0; a <= 90.01f; a += 5)
            {
                P.Init(alt, tas, a, a);
                Vector3 Q(IfritTest.Cfg c) { IfritTest.Apply(P, c); return IfritTest.Acc(P); }
                var q0 = Q(new IfritTest.Cfg());
                var sb = new System.Text.StringBuilder();
                sb.Append($"{{\"ias\":{ias},\"alpha\":{a},\"q0\":[{q0.x:F2},{q0.y:F2},{q0.z:F2}],\"eff\":{{");
                bool first = true;
                void Add(string n, IfritTest.Cfg cp, IfritTest.Cfg cm)
                {
                    var dp = Q(cp) - q0; var dm = Q(cm) - q0;
                    sb.Append($"{(first ? "" : ",")}\"{n}\":[[{dp.x:F2},{dp.y:F2},{dp.z:F2}],[{dm.x:F2},{dm.y:F2},{dm.z:F2}]]"); first = false;
                }
                IfritTest.Cfg C(Action<IfritTest.Cfg> f) { var c = new IfritTest.Cfg(); f(c); return c; }
                foreach (var cs0 in P.A.surfaces) { var s = cs0.name; float rng = Mathf.Max(5f, Mathf.Abs(cs0.pitchRange)); if (s.StartsWith("canard")) rng = tail; else if (s.StartsWith("aileron")) rng = 30f; else if (s.StartsWith("flap")) rng = 25f; else if (s.StartsWith("rudder")) rng = 25f; Add(s, C(c => c.defl[s] = rng), C(c => c.defl[s] = -rng)); }
                Add("TVC_L", C(c => c.tvcL = 20), C(c => c.tvcL = -20));
                Add("TVC_R", C(c => c.tvcR = 20), C(c => c.tvcR = -20));
                sb.Append("}}");
                Console.WriteLine(sb.ToString());
            }
        }
    }
}

internal static class IfritSuite
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static string F1(float x) => float.IsNaN(x) ? "nan" : x.ToString("0.#", IC);
    static bool Departed(List<FlyLog> L) => L.Count == 0 || L.Any(l => float.IsNaN(l.alpha) || Mathf.Abs(l.beta) > 15f);
    static float At(List<FlyLog> L, float t, Func<FlyLog, float> f) { var x = L.FirstOrDefault(l => l.t >= t); return x == null ? float.NaN : f(x); }

    public static void Run(Dictionary<string, string> kv)
    {
        string ctrl = kv.TryGetValue("ctrl", out var c) ? c : "fcs";
        string var = kv.TryGetValue("var", out var v) ? v : ctrl;
        float alt = 3000f;
        string only = kv.TryGetValue("only", out var o) ? o : "ABCDEFGHIJ";
        var rows = new List<string>();
        void Row(string test, string cond, List<FlyLog> L, string metrics) => Console.WriteLine($"{var},{test},{cond},{(Departed(L) ? "DEP" : "ok")},{metrics}");
        // A: full-aft pull from 1 g, MAX AB
        if (only.Contains('A')) foreach (var ias in new[] { 300f, 450f, 600f, 800f, 1000f })
        {
            var L = IfritFly.Fly(ctrl, alt, ias, 1f, "0:1", null, null, 4f);
            float t90 = L.FirstOrDefault(l => l.theta >= 89f)?.t ?? float.NaN;
            Row("A_pull", $"{ias}", L, $"nzMax={F1(L.Max(l => l.nz))},qMax={F1(L.Max(l => l.q))},alphaMax={F1(L.Max(l => l.alpha))},alpha1s={F1(At(L, 1f, l => l.alpha))},turnMax={F1(L.Max(l => l.turn))},iasAtNzMax={F1(L.OrderBy(l => -l.nz).First().ias)}");
        }
        // B: max-AoA hold at low speed
        if (only.Contains('B')) foreach (var ias in new[] { 250f, 350f })
        {
            var L = IfritFly.Fly(ctrl, alt, ias, 1f, "0:1", null, null, 6f);
            var tail = L.Where(l => l.t > 3f).ToList();
            Row("B_hold", $"{ias}", L, $"alphaMax={F1(L.Max(l => l.alpha))},alphaHoldMin={F1(tail.Min(l => l.alpha))},alphaHoldMax={F1(tail.Max(l => l.alpha))},iasEnd={F1(L.Last().ias)}");
        }
        // C: 1 g full-stick roll (bank at 0.5/1 s, peak velocity-vector roll rate, sideslip)
        if (only.Contains('C')) foreach (var ias in new[] { 250f, 400f, 600f, 800f })
        {
            var L = IfritFly.Fly(ctrl, alt, ias, -1f, null, "0:1", null, 2.5f);
            float t90 = L.FirstOrDefault(l => Mathf.Abs(l.phi) >= 90f)?.t ?? float.NaN;
            Row("C_roll1g", $"{ias}", L, $"bank05={F1(At(L, 0.5f, l => l.phi))},bank1={F1(At(L, 1f, l => l.phi))},t90={F1(t90)},pVmax={F1(L.Max(l => Mathf.Abs(l.pV)))},betaMax={F1(L.Max(l => Mathf.Abs(l.beta)))}");
        }
        // D: loaded roll at max AoA (full aft 3 s, full roll 3 s with aft held)
        if (only.Contains('D')) foreach (var ias in new[] { 250f, 350f, 550f })
        {
            var L = IfritFly.Fly(ctrl, alt, ias, 1f, "0:1", "0:0,3:0,3.01:1", null, 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            float a0 = At(L, 3f, l => l.alpha);
            float t90 = R.FirstOrDefault(l => Vector3.Angle(Vector3.up, Vector3.up) >= 0 && false)?.t ?? float.NaN;
            float rollAng = 0f; foreach (var l in R) rollAng += l.pV * 0.02f;
            Row("D_loadedRoll", $"{ias}", L, $"alphaAtRoll={F1(a0)},alphaMinRoll={F1(R.Min(l => l.alpha))},pVmax={F1(R.Max(l => l.pV))},vvRoll3s={F1(rollAng)},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))},iasRoll={F1(R.First().ias)}");
        }
        // E: low-speed max-AoA roll reversal (left 1.5 s, right 1.5 s)
        if (only.Contains('E'))
        {
            var L = IfritFly.Fly(ctrl, alt, 300f, 1f, "0:1", "0:0,4:0,4.01:-1,5.5:-1,5.51:1,7:1,7.01:0", null, 9f);
            var R = L.Where(l => l.t >= 4f).ToList();
            Row("E_reversal", "300", L, $"alphaAt={F1(At(L, 4f, l => l.alpha))},alphaRange={F1(R.Min(l => l.alpha))}..{F1(R.Max(l => l.alpha))},pVmin={F1(R.Min(l => l.pV))},pVmax={F1(R.Max(l => l.pV))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))},iasEnd={F1(L.Last().ias)}");
        }
        // F: idle throttle max-AoA pull then roll
        if (only.Contains('F'))
        {
            var L = IfritFly.Fly(ctrl, alt, 350f, 0f, "0:1", "0:0,3:0,3.01:1", null, 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            Row("F_idle", "350", L, $"alphaMax={F1(L.Max(l => l.alpha))},alphaAt3={F1(At(L, 3f, l => l.alpha))},pVmax={F1(R.Max(l => l.pV))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))},alphaMinRoll={F1(R.Min(l => l.alpha))}");
        }
        // G: full push, then push + roll
        if (only.Contains('G'))
        {
            var L = IfritFly.Fly(ctrl, alt, 400f, 1f, "0:-1", "0:0,3:0,3.01:1", null, 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            Row("G_push", "400", L, $"alphaMin={F1(L.Min(l => l.alpha))},nzMin={F1(L.Min(l => l.nz))},pVmax={F1(R.Max(l => Mathf.Abs(l.pV)))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))}");
        }
        // I: low-speed full push (negative AoA limit) then push + roll
        if (only.Contains('I'))
        {
            var L = IfritFly.Fly(ctrl, alt, 220f, 1f, "0:-1", "0:0,3:0,3.01:1", null, 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            Row("I_pushLow", "220", L, $"alphaMin={F1(L.Min(l => l.alpha))},alphaAt3={F1(At(L, 3f, l => l.alpha))},alphaMaxRoll={F1(R.Max(l => l.alpha))},pVmax={F1(R.Max(l => Mathf.Abs(l.pV)))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))}");
        }
        // J: cross-control at max AoA (full aft, full left roll, full right pedal) - departure resistance
        if (only.Contains('J'))
        {
            var L = IfritFly.Fly(ctrl, alt, 300f, 1f, "0:1", "0:0,3:0,3.01:-1", "0:0,3:0,3.01:1", 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            Row("J_cross", "300", L, $"alphaAt={F1(At(L, 3f, l => l.alpha))},alphaRange={F1(R.Min(l => l.alpha))}..{F1(R.Max(l => l.alpha))},pVabsMax={F1(R.Max(l => Mathf.Abs(l.pV)))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))}");
        }
        // H: max-AoA pedal turn at low speed (full aft + full right pedal)
        if (only.Contains('H'))
        {
            var L = IfritFly.Fly(ctrl, alt, 300f, 1f, "0:1", null, "0:0,3:0,3.01:1", 6f);
            var R = L.Where(l => l.t >= 3f).ToList();
            Row("H_pedal", "300", L, $"alphaAt={F1(At(L, 3f, l => l.alpha))},rMax={F1(R.Max(l => Mathf.Abs(l.r)))},pVmax={F1(R.Max(l => Mathf.Abs(l.pV)))},betaMax={F1(R.Max(l => Mathf.Abs(l.beta)))}");
        }
    }
}
