// Equation-error check against a flight log (bench calibration, 31 Oct 2026).
//   bench eqerr log=<F22E_FCS_*.csv> out=<csv> [massScale=1] [modes=3,6] [every=1] [Name=value ...]
// For every logged sample the plant is posed at the logged state (TAS, AoA, sideslip, p/q/r, rho, attitude), every
// control surface is set to its logged _now deflection (FCS and servos bypassed), the flaperon drives are solved so the
// surfaces' local AoA matches the logged _aoaNow, the nozzles to tvcNow, the slats to hlPos, the airbrakes to abOpen and
// the thrust to tw x m x g. Plant.Forces then gives the total force and moment, from which the predicted body angular
// accelerations (I^-1 (M - w x I w)) and load factors are written next to the logged state. The Python side
// differentiates the logged rates and compares.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class EqErr
{
    // ---- shared posing helpers (eqerr and replay) ----
    internal sealed class Poser
    {
        public Airframe A; public Plant P; public FcsController fcs;
        public List<(Transform rot, Quaternion baseRot, SurfaceEffector surf, bool flap, float hlAngle, bool hasHl, object drive)> drives = new List<(Transform, Quaternion, SurfaceEffector, bool, float, bool, object)>();
        public List<(HighLiftDevice d, AeroPart part, System.Collections.IList parts, MethodInfo animate, object hld)> hlds = new List<(HighLiftDevice, AeroPart, System.Collections.IList, MethodInfo, object)>();
        public Dictionary<string, ControlSurface> csBy; public Dictionary<string, SurfaceEffector> seBy;
        public static readonly string[] SurfNames = { "Elevator_L", "Elevator_R", "Rudder_L", "Rudder_R", "Flap_L", "Flap_R", "Aileron_L", "Aileron_R" };
        public float sPhi = 1f;
        public Poser(Airframe a, Plant p)
        {
            A = a; P = p; fcs = p.fcs;
            var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (var d in (System.Collections.IList)typeof(FlapSystem).GetField("drives", bf).GetValue(fcs.flaps))
            {
                var t = d.GetType();
                drives.Add(((Transform)t.GetField("rotator").GetValue(d), (Quaternion)t.GetField("baseRot").GetValue(d), (SurfaceEffector)t.GetField("surf").GetValue(d), (bool)t.GetField("flap").GetValue(d), (float)t.GetField("hlAngle").GetValue(d), t.GetField("hl").GetValue(d) != null, d));
            }
            foreach (var h in (System.Collections.IList)typeof(FlapSystem).GetField("hlds", bf).GetValue(fcs.flaps))
            {
                var t = h.GetType(); var pl = (System.Collections.IList)t.GetField("parts").GetValue(h);
                hlds.Add(((HighLiftDevice)t.GetField("d").GetValue(h), (AeroPart)t.GetField("part").GetValue(h), pl, pl.Count > 0 ? pl[0].GetType().GetMethod("Animate") : null, h));
            }
            csBy = SurfNames.ToDictionary(n => n, n => A.surfaces.First(c => c.name.EndsWith(n)));
            seBy = SurfNames.ToDictionary(n => n, n => fcs.surfaces.First(x => x.name == n));
            var qt = Att(10f, 30f, 1f);
            float phiT = Mathf.Atan2(-(qt * Vector3.right).y, (qt * Vector3.up).y) * Mathf.Rad2Deg;
            if (Mathf.Abs(phiT - 30f) > 1f) sPhi = -1f;
        }
        // rigid-body state from a logged row
        public void PoseState(float tas, float rho, float alpha, float beta, float pr, float qr, float rr, float phi, float theta, float heading = 0f)
        {
            float h = (1f - Mathf.Pow(Mathf.Clamp(rho / 1.225f, 0.05f, 1f), 1f / 4.2559f)) / 2.2558e-5f;
            P.q = Quaternion.Euler(0f, heading, 0f) * Att(theta, phi, sPhi);
            float ar = alpha * Mathf.Deg2Rad, br = beta * Mathf.Deg2Rad;
            Vector3 vb = new Vector3(Mathf.Sin(br), -Mathf.Cos(br) * Mathf.Sin(ar), Mathf.Cos(br) * Mathf.Cos(ar)) * tas;
            Vector3 wb = new Vector3(-qr, rr, -pr) * Mathf.Deg2Rad;
            P.w = P.q * wb;
            P.vCg = P.q * vb - LogRefOffset(A, P);
            P.pCg = new Vector3(0f, h, 0f);
            P.Pose();
            A.ac.airDensity = rho;
        }
        public void SetSurface(string sn, float now)
        {
            var cs = csBy[sn]; ref var jb = ref cs.job;
            jb.currentPitch = now; jb.currentRoll = 0f; jb.currentYaw = 0f;
            cs.visibleMesh.transform.localRotation = jb.restingRotation * Quaternion.AngleAxis(now, Vector3.right);
        }
        public void SetSlats(float hl)
        {
            foreach (var hd in hlds)
            {
                hd.d.position = hl;
                if (hd.part != null) hd.part.wingArea = Mathf.Lerp(hd.d.partAreaRetracted, hd.d.partAreaDeployed, hl);
                foreach (var mp in hd.parts) hd.animate?.Invoke(mp, new object[] { hl });
                hd.hld.GetType().GetField("pos").SetValue(hd.hld, hl);
            }
            fcs.flaps.hlPos = hl;
        }
        public void SetDrive(int i, float ang)
        {
            var d = drives[i];
            d.rot.localRotation = d.baseRot * Quaternion.Euler(ang, 0f, 0f);
            d.drive.GetType().GetField("angle").SetValue(d.drive, ang);
        }
        public void SetNozzles(float tvc)
        {
            foreach (var tf in A.engines)
            {
                tf.nozzleAngles = new Vector3(tvc, 0f, 0f);
                foreach (var vt in tf.vectoringTransforms) vt.localEulerAngles = new Vector3(Mathf.Clamp(tvc, -20f, 20f), 0f, 0f);
            }
        }
        public void SetAirbrakes(float ab)
        {
            foreach (var abk in A.airbrakes)
            {
                abk.openAmount = ab;
                if (abk.tr != null) abk.tr.localRotation = abk.baseRot * Quaternion.Euler(ab * abk.maxAngle, 0f, 0f);
            }
        }
        // throttle that gives the logged T/W through the bench's engine model (steady state), and the engine state for it
        public float ThrottleFor(float tw, float mass)
        {
            float T = tw * mass * 9.81f;
            float milMax = 0f, abMax = 0f;
            foreach (var tf in A.engines) { float sf = SpeedThrust(tf, A.ac.speed); milMax += tf.staticThrust * sf; foreach (var n in tf.nozzles) abMax += n.abThrust * Mathf.Clamp(A.ac.airDensity, 0.4f, 1f); }
            if (T <= milMax)
            {
                var tf0 = A.engines[0];
                float spR = T / Mathf.Max(1f, milMax);
                float rpm = spR * (tf0.maxRPM - tf0.minRPM) + 0.85f * tf0.minRPM;
                return Mathf.Clamp01((rpm - tf0.minRPM) / (tf0.maxRPM - tf0.minRPM));
            }
            return Mathf.Clamp01(0.95f + 0.05f * (T - milMax) / Mathf.Max(1f, abMax));
        }
        public void SetEngineState(float throttle)
        {
            foreach (var e0 in A.engines)
            {
                float tr0 = Mathf.Clamp01((throttle - e0.remapX) / Mathf.Max(1e-3f, e0.remapY - e0.remapX));
                e0.rpm = Mathf.Lerp(e0.minRPM, e0.minRPM + (e0.maxRPM - e0.minRPM) * (0.5f * e0.condition + 0.5f), tr0);
                foreach (var n0 in e0.nozzles) n0.abAmt = (e0.rpm / e0.maxRPM > 0.85f) ? Mathf.InverseLerp(n0.abStart, 1f, throttle) : 0f;
            }
        }
        static float SpeedThrust(Turbofan tf, float s)
        {
            var k = tf.speedThrust;
            if (k == null || k.Length == 0) return 1f;
            if (s <= k[0][0]) return k[0][1];
            for (int i = 0; i < k.Length - 1; i++) if (s <= k[i + 1][0]) return Mathf.Lerp(k[i][1], k[i + 1][1], (s - k[i][0]) / (k[i + 1][0] - k[i][0]));
            return k[k.Length - 1][1];
        }
    }

    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    // leaf calibration (3 Nov 2026): the game's logged alpha / beta / tas are the ROOT rigidbody's velocity (ac.rb = the root
    // part, its CoM ~2.5 m ahead of the aircraft CG), not the CG's. Posing the CG from them left every part's velocity short of
    // w x d: the rudders' logged local AoA disagreed with the bench's by -/+0.024 deg per deg/s of yaw rate (opposite on the
    // two canted fins), the stabs' by -0.025 deg per deg/s of pitch rate at the leaf - exactly 2.48 m / TAS. LogRefRoot=1 poses
    // the root body at the logged velocity (the CG at v - w x d), so the FCS in a replay reads the game's own beta and the
    // plant's parts sit at the game's local AoA. 0 = the 31 Oct behaviour (CG at the logged velocity).
    public static int LogRefRoot = 1;
    public static int GyroMode = 1;   // 3 Nov: the plant's gyroscopic term for eqerr (0 None, 1 Unity = parallel-axis inertia only, 2 Full)
    internal static Vector3 LogRefOffset(Airframe A, Plant P)
    {
        if (LogRefRoot == 0) return Vector3.zero;
        var rb = A.bodies.First(b => b.rb == A.ac.rb);
        return Vector3.Cross(P.w, P.q * (rb.comB - A.cgB));
    }
    public static float MassScale = 1f;      // game mass / bench mass (thrust from tw and the nz denominator use it)
    public static float InertiaScale = 1f;   // uniform scale on the inertia tensor used for the predicted accelerations
    public static float IRollScale = 1f, IPitchScale = 1f, IYawScale = 1f;
    public static float ThrustScaleLog = 1f;
    public static int DriveSolve = 1;        // 1: solve the flaperon drives from _aoaNow; 0: formula from flapBias / hlPos / relief only

    // negAoA calibration: per-part aero breakdown at a posed state.
    //   bench parts alpha=-20 beta=5 tas=110 [rho=1.2] [p= q= r=] [Name=value ...]
    // Prints each part's local AoA, CL, force (root frame: x right, y up, z fwd) and its roll/pitch/yaw angular-acceleration
    // contribution (deg/s^2; p right, q up, r right) with all control surfaces neutral, slats in, nozzles centred.
    public static void Parts(string dataPath, Dictionary<string, string> kv)
    {
        float F(string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = (Gyro)GyroMode };
        P.Init(1000f, 150f, 5f, 5f);
        P.CreateFcs();
        var ps = new Poser(A, P);
        ps.PoseState(F("tas", 110f), F("rho", 1.2f), F("alpha", 0f), F("beta", 0f), F("p", 0f), F("q", 0f), F("r", 0f), 0f, F("alpha", 0f));
        foreach (var sn in Poser.SurfNames) ps.SetSurface(sn, F(sn, 0f));
        ps.SetSlats(F("hl", 0f));
        for (int i = 0; i < ps.drives.Count; i++) ps.SetDrive(i, 0f);
        ps.SetNozzles(0f); ps.SetAirbrakes(0f);
        P.throttle = 0f; ps.SetEngineState(0f);
        P.PartDump = new Dictionary<AeroPart, (Vector3, Vector3, float, float)>();
        foreach (var tf in A.engines) foreach (var nz in tf.nozzles) nz.totalThrust = 0f;
        P.Pose(); P.Forces(out Vector3 Ft, out Vector3 Mt);
        var qi = Quaternion.Inverse(P.q);
        Vector3 Acc(Vector3 Mw) { var a = P.AngAccel(Mw); var ab = qi * a; return new Vector3(-ab.z, -ab.x, ab.y) * Mathf.Rad2Deg; }
        Console.WriteLine("part,aoa,cl,fx,fy,fz,pdot,qdot,rdot");
        Vector3 tot = Vector3.zero, ftot = Vector3.zero;
        foreach (var kvp in P.PartDump.OrderBy(k => k.Key.name))
        {
            var fb = qi * kvp.Value.f; var acc = Acc(kvp.Value.m); tot += kvp.Value.m; ftot += fb;
            Console.WriteLine($"{kvp.Key.name.Replace("Aryx_KingRaptor_", "")},{kvp.Value.aoa:F2},{kvp.Value.cl:F3},{fb.x:F0},{fb.y:F0},{fb.z:F0},{acc.x:F2},{acc.y:F2},{acc.z:F2}");
        }
        var at = Acc(tot);
        Console.WriteLine($"TOTAL,,,{ftot.x:F0},{ftot.y:F0},{ftot.z:F0},{at.x:F2},{at.y:F2},{at.z:F2}");
    }

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; string outPath = kv.TryGetValue("out", out var o) ? o : "eqerr.csv";
        var modes = (kv.TryGetValue("modes", out var ms) ? ms : "3,6").Split(',').Select(int.Parse).ToHashSet();
        int every = kv.TryGetValue("every", out var ev) ? int.Parse(ev) : 1;
        float dumpAt = kv.TryGetValue("dumpAt", out var da) ? float.Parse(da, IC) : -1f;
        if (kv.TryGetValue("massScale", out var mss)) MassScale = float.Parse(mss, IC);
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = (Gyro)GyroMode };
        P.Init(1000f, 150f, 5f, 5f);
        P.CreateFcs();
        var fcs = P.fcs;
        // scaled inertia for the prediction (bench axes x = pitch, y = yaw, z = roll)
        var I0 = (double[,])A.Itot.Clone();
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) A.Itot[i, j] = I0[i, j] * InertiaScale * Math.Sqrt(AxisScale(i) * AxisScale(j));
        float mass = A.mass * MassScale;

        // flaperon drives (FlapSystem is private: reflection)
        var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var drivesObj = (System.Collections.IList)typeof(FlapSystem).GetField("drives", bf).GetValue(fcs.flaps);
        var hldsObj = (System.Collections.IList)typeof(FlapSystem).GetField("hlds", bf).GetValue(fcs.flaps);
        var drives = new List<(Transform rot, Quaternion baseRot, SurfaceEffector surf, bool flap, float hlAngle, bool hasHl)>();
        foreach (var d in drivesObj)
        {
            var t = d.GetType();
            drives.Add(((Transform)t.GetField("rotator").GetValue(d), (Quaternion)t.GetField("baseRot").GetValue(d), (SurfaceEffector)t.GetField("surf").GetValue(d), (bool)t.GetField("flap").GetValue(d), (float)t.GetField("hlAngle").GetValue(d), t.GetField("hl").GetValue(d) != null));
        }
        var hlds = new List<(HighLiftDevice d, AeroPart part, System.Collections.IList parts, MethodInfo animate)>();
        foreach (var h in hldsObj)
        {
            var t = h.GetType();
            var pl = (System.Collections.IList)t.GetField("parts").GetValue(h);
            hlds.Add(((HighLiftDevice)t.GetField("d").GetValue(h), (AeroPart)t.GetField("part").GetValue(h), pl, pl.Count > 0 ? pl[0].GetType().GetMethod("Animate") : null));
        }
        Console.Error.WriteLine($"# eqerr: mass {A.mass:F0} x {MassScale:F3} = {mass:F0} kg; I diag {A.Itot[0,0]:F0}/{A.Itot[1,1]:F0}/{A.Itot[2,2]:F0} (pitch/yaw/roll); {drives.Count} drives, {hlds.Count} HLDs, {A.airbrakes.Count} airbrakes");

        // attitude sign check: the game's phi = atan2(-right.y, up.y), theta = asin(forward.y)
        float sPhi = 1f;
        {
            var qt = Att(10f, 30f, 1f);
            float phiT = Mathf.Atan2(-(qt * Vector3.right).y, (qt * Vector3.up).y) * Mathf.Rad2Deg;
            if (Mathf.Abs(phiT - 30f) > 1f) sPhi = -1f;
        }

        var lines = File.ReadLines(log).GetEnumerator();
        lines.MoveNext();
        var hdr = lines.Current.Split(',');
        var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        if (col.ContainsKey("airRefCg")) LogRefRoot = 0;   // 2.30.0 logs: alpha / beta / tas at the CG
        string[] surfNames = { "Elevator_L", "Elevator_R", "Rudder_L", "Rudder_R", "Flap_L", "Flap_R", "Aileron_L", "Aileron_R" };
        var csBy = surfNames.ToDictionary(n => n, n => A.surfaces.First(c => c.name.EndsWith(n)));
        var seBy = surfNames.ToDictionary(n => n, n => fcs.surfaces.First(s => s.name == n));
        using var w = new StreamWriter(outPath);
        w.WriteLine("t,mode,ias,tas,rho,mach,qbar,alpha,beta,p,q,r,nz,tw,tvc,hl,ab,pdot,qdot,rdot,nzp,nyp,axp,Fx,Fy,Fz,Mx,My,Mz,Tn,"
            + "mStab,mFin,mFlap,mWing,mBody,mThr,aoaResStabL,aoaResStabR,aoaResRudL,aoaResRudR,drvFlapL,drvFlapR,drvAilL,drvAilR,drvFlapLform,drvAilRes,stabL,stabR,rudL,rudR,flapL,flapR,ailL,ailR");
        int n = 0, used = 0;
        float g = 9.81f;
        while (lines.MoveNext())
        {
            var f = lines.Current.Split(',');
            if (f.Length < hdr.Length - 1) continue;
            n++;
            if (n % every != 0) continue;
            float G(string k) => float.Parse(f[col[k]], IC);
            int mode = (int)G("mode");
            if (!modes.Contains(mode)) continue;
            float tas = G("tas"); if (tas < 40f) continue;
            float rho = G("rho"), alpha = G("alpha"), beta = G("beta"), pr = G("p"), qr = G("q"), rr = G("r");
            float phi = G("phi"), theta = G("theta"), tw = G("tw"), tvc = G("tvcNow"), hl = G("hlPos"), ab = G("abOpen");
            // ---- pose
            float h = (1f - Mathf.Pow(Mathf.Clamp(rho / 1.225f, 0.05f, 1f), 1f / 4.2559f)) / 2.2558e-5f;
            P.q = Att(theta, phi, sPhi);
            float ar = alpha * Mathf.Deg2Rad, br = beta * Mathf.Deg2Rad;
            Vector3 vb = new Vector3(Mathf.Sin(br), -Mathf.Cos(br) * Mathf.Sin(ar), Mathf.Cos(br) * Mathf.Cos(ar)) * tas;
            Vector3 wb = new Vector3(-qr, rr, -pr) * Mathf.Deg2Rad;   // body (Unity) rates from aero p,q,r
            P.w = P.q * wb;
            P.vCg = P.q * vb - LogRefOffset(A, P);
            P.pCg = new Vector3(0f, h, 0f);
            P.Pose();
            A.ac.airDensity = rho;   // exactly the logged density
            // ---- surfaces at the logged deflection
            foreach (var sn in surfNames)
            {
                var cs = csBy[sn]; ref var jb = ref cs.job;
                float now = G(sn + "_now");
                jb.currentPitch = now; jb.currentRoll = 0f; jb.currentYaw = 0f;
                cs.visibleMesh.transform.localRotation = jb.restingRotation * Quaternion.AngleAxis(now, Vector3.right);
            }
            // ---- slats
            foreach (var hd in hlds)
            {
                hd.d.position = hl;
                if (hd.part != null) hd.part.wingArea = Mathf.Lerp(hd.d.partAreaRetracted, hd.d.partAreaDeployed, hl);
                foreach (var mp in hd.parts) hd.animate?.Invoke(mp, new object[] { hl });
            }
            // ---- flaperon drives: formula first, then solve from the logged local AoA
            float flapBias = G("flapBias"), relief = G("relief");
            float drvFlapForm = -flapBias - (drives.FirstOrDefault(d => d.flap).hasHl ? drives.First(d => d.flap).hlAngle * hl : 0f);
            float[] drv = new float[4]; float drvAilRes = 0f;
            int di = 0;
            foreach (var d in drives)
            {
                float a0 = d.flap ? Mathf.Clamp(-flapBias - (d.hasHl ? d.hlAngle * hl : 0f), -25f, 20f) : Mathf.Clamp(relief, -25f, 20f);
                d.rot.localRotation = d.baseRot * Quaternion.Euler(a0, 0f, 0f);
                float target = G(d.surf.name + "_aoaNow");
                float ang = a0;
                if (DriveSolve == 1)
                {
                    // local AoA is linear in the drive angle about the hinge: two evaluations and a secant step, then one more
                    float f0 = LocalAoA(d.surf, A, P), s = 1f;
                    d.rot.localRotation = d.baseRot * Quaternion.Euler(a0 + s, 0f, 0f);
                    float f1 = LocalAoA(d.surf, A, P);
                    float k = (f1 - f0) / s;
                    if (Mathf.Abs(k) > 0.2f)
                    {
                        ang = Mathf.Clamp(a0 + (target - f0) / k, -25f, 20f);
                        d.rot.localRotation = d.baseRot * Quaternion.Euler(ang, 0f, 0f);
                        float f2 = LocalAoA(d.surf, A, P);
                        if (Mathf.Abs(f2 - target) > 0.05f && Mathf.Abs(ang - a0 - (target - f0) / k) < 0.01f) { ang = Mathf.Clamp(ang + (target - f2) / k, -25f, 20f); d.rot.localRotation = d.baseRot * Quaternion.Euler(ang, 0f, 0f); }
                        if (!d.flap) drvAilRes = Mathf.Max(drvAilRes, Mathf.Abs(LocalAoA(d.surf, A, P) - target));
                    }
                    else d.rot.localRotation = d.baseRot * Quaternion.Euler(a0, 0f, 0f);
                }
                int slot = d.surf.name == "Flap_L" ? 0 : d.surf.name == "Flap_R" ? 1 : d.surf.name == "Aileron_L" ? 2 : 3;
                drv[slot] = ang; di++;
            }
            // ---- nozzles
            foreach (var tf in A.engines)
            {
                tf.nozzleAngles = new Vector3(tvc, 0f, 0f);
                foreach (var vt in tf.vectoringTransforms) vt.localEulerAngles = new Vector3(Mathf.Clamp(tvc, -20f, 20f), 0f, 0f);
            }
            // ---- airbrakes
            foreach (var abk in A.airbrakes)
            {
                abk.openAmount = ab;
                if (abk.tr != null) abk.tr.localRotation = abk.baseRot * Quaternion.Euler(ab * abk.maxAngle, 0f, 0f);
            }
            // ---- thrust from the logged T/W (the FCS's tw = sum of nozzle thrust / (mass g))
            int nn = A.engines.Sum(e => e.nozzles.Length);
            float Tn = tw * mass * g / Math.Max(1, nn) * ThrustScaleLog;
            foreach (var tf in A.engines) foreach (var nz in tf.nozzles) nz.totalThrust = Tn;
            // ---- forces
            Array.Clear(Plant.GrpM, 0, Plant.GrpM.Length);
            if (dumpAt > 0f && Math.Abs(G("t") - dumpAt) < P.dt * 0.6f) P.PartDump = new Dictionary<AeroPart, (Vector3, Vector3, float, float)>(); else P.PartDump = null;
            P.Forces(out Vector3 F, out Vector3 M);
            if (P.PartDump != null)
            {
                var qi0 = Quaternion.Inverse(P.q);
                Console.Error.WriteLine($"# parts at t={G("t")}: alpha {alpha} beta {beta} tas {tas}");
                foreach (var kp in P.PartDump.OrderBy(k => k.Key.name)) { var fb = qi0 * kp.Value.f; var ab0 = qi0 * P.AngAccel(kp.Value.m); Console.Error.WriteLine($"#  {kp.Key.name.Replace("Aryx_KingRaptor_", ""),-12} aoa {kp.Value.aoa,7:F2} cl {kp.Value.cl,6:F3} f {fb.x,8:F0} {fb.y,8:F0} {fb.z,8:F0}  pqr {-ab0.z * 57.3f,7:F2} {-ab0.x * 57.3f,7:F2} {ab0.y * 57.3f,7:F2}"); }
            }
            Vector3 wdot = P.AngAccel(M);
            Vector3 l = Quaternion.Inverse(P.q) * wdot;
            Vector3 aAero = new Vector3(-l.z, -l.x, l.y) * Mathf.Rad2Deg;   // pdot, qdot, rdot (deg/s^2)
            float nzp = Vector3.Dot(F, P.q * Vector3.up) / (mass * g);
            float nyp = Vector3.Dot(F, P.q * Vector3.right) / (mass * g);
            float axp = Vector3.Dot(F, P.vCg.normalized) / mass;
            Vector3 Fb = Quaternion.Inverse(P.q) * F, Mb = Quaternion.Inverse(P.q) * M;
            float mach = tas / DragReliefMath.SpeedOfSound(h);
            float qbar = 0.5f * rho * tas * tas;
            // local-AoA residuals of the surfaces that have no drive (pose check)
            float res(string sn) => LocalAoA(seBy[sn], A, P) - G(sn + "_aoaNow");
            string S(float x) => x.ToString("0.####", IC);
            w.WriteLine(string.Join(",", new[] { S(G("t")), mode.ToString(), S(G("ias")), S(tas), S(rho), S(mach), S(qbar), S(alpha), S(beta), S(pr), S(qr), S(rr), S(G("nz")), S(tw), S(tvc), S(hl), S(ab),
                S(aAero.x), S(aAero.y), S(aAero.z), S(nzp), S(nyp), S(axp), S(Fb.x), S(Fb.y), S(Fb.z), S(Mb.x), S(Mb.y), S(Mb.z), S(Tn),
                S(Plant.GrpM[0]), S(Plant.GrpM[1]), S(Plant.GrpM[2]), S(Plant.GrpM[3]), S(Plant.GrpM[4]), S(Plant.GrpM[5]),
                S(res("Elevator_L")), S(res("Elevator_R")), S(res("Rudder_L")), S(res("Rudder_R")), S(drv[0]), S(drv[1]), S(drv[2]), S(drv[3]), S(drvFlapForm), S(drvAilRes),
                S(G("Elevator_L_now")), S(G("Elevator_R_now")), S(G("Rudder_L_now")), S(G("Rudder_R_now")), S(G("Flap_L_now")), S(G("Flap_R_now")), S(G("Aileron_L_now")), S(G("Aileron_R_now")) }));
            used++;
        }
        Console.Error.WriteLine($"# eqerr: {used} of {n} rows -> {outPath}");
    }

    static float AxisScale(int i) => i == 0 ? IPitchScale : i == 1 ? IYawScale : IRollScale;

    // attitude: pitch theta (nose up), bank phi (right wing down), heading 0
    static Quaternion Att(float theta, float phi, float s) => Quaternion.Euler(-theta, 0f, 0f) * Quaternion.Euler(0f, 0f, s * phi);

    // the surface's local AoA as SurfaceEffector.Eval defines it (flow angle in the lift-normal frame, stab downwash included)
    static float LocalAoA(SurfaceEffector se, Airframe A, Plant P)
    {
        var part = (AeroPart)se.cs.attachedSurface;
        Vector3 v = part.rb.velocity - A.ac.windVelocity;
        if (se.kind == SurfaceKind.Stabilator) v = Downwash.Apply(v, P.q, A.ac.rb.velocity - A.ac.windVelocity);
        Vector3 vl = Quaternion.Inverse(part.liftNormal.rotation) * v;
        return -Mathf.Atan2(vl.y, vl.z) * Mathf.Rad2Deg;
    }
}
