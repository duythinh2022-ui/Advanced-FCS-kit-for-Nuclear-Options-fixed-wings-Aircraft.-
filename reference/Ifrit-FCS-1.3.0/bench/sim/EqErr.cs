// Equation-error check against a flight log (bench calibration, 31 Oct 2026) — Ifrit / Revoker version.
//   bench eqerr log=<FCS log csv> out=<csv> [modes=3,6] [every=1] [Name=value ...]
// Poses the plant at every logged sample (TAS, AoA, sideslip, p/q/r, rho, attitude), sets every control surface to its
// logged _now deflection (FCS and servos bypassed), the deceleron splits (splitL / splitR), the nozzles to tvcNow
// (+/- tvcdNow per engine on the Ifrit, tvcyNow yaw on the Revoker), the slats to hlPos, the airbrake to abOpen, the
// flaperon drives solved from the logged _aoaNow, and the thrust to tw x m x g; Plant.Step in EvalOnly mode then gives the
// total force and moment, from which the predicted body angular accelerations and load factors are written next to the
// logged state. The Python side differentiates the logged rates and compares.
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
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static float MassScale = 1f;
    public static int DriveSolve = 1;
    public static int LogRefRoot = 1;   // 3 Nov leaf calibration: logged state is the root rigidbody's (see PoseState)

    internal sealed class Poser
    {
        public Airframe A; public Plant P; public FcsController fcs;
        public List<(Transform rot, Quaternion baseRot, SurfaceEffector surf, bool flap, float hlAngle, bool hasHl, object drive)> drives = new List<(Transform, Quaternion, SurfaceEffector, bool, float, bool, object)>();
        public List<(HighLiftDevice d, AeroPart part, System.Collections.IList parts, MethodInfo animate, object hld)> hlds = new List<(HighLiftDevice, AeroPart, System.Collections.IList, MethodInfo, object)>();
        public Dictionary<string, ControlSurface> csByLog = new Dictionary<string, ControlSurface>();   // log name (game name) -> bench surface
        public Dictionary<string, SurfaceEffector> seByLog = new Dictionary<string, SurfaceEffector>();
        public float sPhi = 1f;
        public Poser(Airframe a, Plant p, string[] logSurfNames)
        {
            A = a; P = p; fcs = p.fcs;
            var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var fl = fcs.flaps;
            if (fl != null)
            {
                var dl = typeof(FlapSystem).GetField("drives", bf)?.GetValue(fl) as System.Collections.IList;
                if (dl != null) foreach (var d in dl)
                {
                    var t = d.GetType();
                    drives.Add(((Transform)t.GetField("rotator").GetValue(d), (Quaternion)t.GetField("baseRot").GetValue(d), (SurfaceEffector)t.GetField("surf").GetValue(d), (bool)t.GetField("flap").GetValue(d), (float)t.GetField("hlAngle").GetValue(d), t.GetField("hl").GetValue(d) != null, d));
                }
                var hl = typeof(FlapSystem).GetField("hlds", bf)?.GetValue(fl) as System.Collections.IList;
                if (hl != null) foreach (var h in hl)
                {
                    var t = h.GetType(); var pl = (System.Collections.IList)t.GetField("parts").GetValue(h);
                    hlds.Add(((HighLiftDevice)t.GetField("d").GetValue(h), (AeroPart)t.GetField("part").GetValue(h), pl, pl.Count > 0 ? pl[0].GetType().GetMethod("Animate") : null, h));
                }
            }
            foreach (var n in logSurfNames)
            {
                // the bench may have renamed the stock parts (elevator_L -> Elevator_L); match case-insensitively on the suffix
                var cs = A.surfaces.FirstOrDefault(c => c.name.Equals(n, StringComparison.OrdinalIgnoreCase)) ?? A.surfaces.FirstOrDefault(c => c.name.ToLowerInvariant().EndsWith(n.ToLowerInvariant()));
                if (cs == null) { Console.Error.WriteLine("# eqerr: no bench surface for log column " + n); continue; }
                csByLog[n] = cs;
                var se = fcs.surfaces.FirstOrDefault(x => x.cs == cs);
                if (se != null) seByLog[n] = se;
            }
            var qt = Att(10f, 30f, 1f);
            float phiT = Mathf.Atan2(-(qt * Vector3.right).y, (qt * Vector3.up).y) * Mathf.Rad2Deg;
            if (Mathf.Abs(phiT - 30f) > 1f) sPhi = -1f;
        }
        public static Quaternion Att(float theta, float phi, float s) => Quaternion.Euler(-theta, 0f, 0f) * Quaternion.Euler(0f, 0f, s * phi);
        public void PoseState(float tas, float rho, float alpha, float beta, float pr, float qr, float rr, float phi, float theta)
        {
            float h = (1f - Mathf.Pow(Mathf.Clamp(rho / 1.225f, 0.05f, 1f), 1f / 4.2559f)) / 2.2558e-5f;
            P.q = Att(theta, phi, sPhi);
            float ar = alpha * Mathf.Deg2Rad, br = beta * Mathf.Deg2Rad;
            Vector3 vb = new Vector3(Mathf.Sin(br), -Mathf.Cos(br) * Mathf.Sin(ar), Mathf.Cos(br) * Mathf.Cos(ar)) * tas;
            Vector3 wb = new Vector3(-qr, rr, -pr) * Mathf.Deg2Rad;
            P.w = P.q * wb;
            // leaf calibration (3 Nov 2026, from the F-22E bench): the game's logged alpha / beta / tas are the ROOT rigidbody's
            // velocity (ac.rb = the root part, CoM 0.62 m ahead of the CG on the Ifrit), not the CG's. Pose the root body at the
            // logged velocity (CG at v - w x d): the ruddervators' logged local AoA disagreed with the bench's by ~0.006 deg per
            // deg/s of pitch / yaw rate before (= 0.62 m / TAS). LogRefRoot=0 restores the old pose.
            P.vCg = P.q * vb - (LogRefRoot == 0 ? Vector3.zero : Vector3.Cross(P.w, P.q * (A.bodies.First(b => b.rb == A.ac.rb).comB - A.cgB)));
            P.pCg = new Vector3(0f, h, 0f);
            P.Pose();
            A.ac.airDensity = rho;
        }
        public void SetSurface(string logName, float now)
        {
            if (!csByLog.TryGetValue(logName, out var cs)) return;
            ref var jb = ref cs.job;
            jb.currentPitch = now; jb.currentRoll = 0f; jb.currentYaw = 0f;
            cs.visibleMesh.transform.localRotation = jb.restingRotation * Quaternion.AngleAxis(now, Vector3.right);
        }
        public void SetSplit(string logName, float split)
        {
            if (!csByLog.TryGetValue(logName, out var cs)) return;
            cs.splitAmount = Mathf.Clamp(split, 0f, Mathf.Max(0f, cs.maxSplit));
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
            if (hlds.Count == 0) foreach (var h in A.hlds)
            {
                h.position = hl; h.aeroPart.wingArea = Mathf.Lerp(h.partAreaRetracted, h.partAreaDeployed, hl);
                foreach (var mp in h.movingParts) if (mp.transform != null) { if (mp.move) mp.transform.localPosition = Vector3.Lerp(mp.positionRetracted, mp.positionDeployed, hl); if (mp.rotate) mp.transform.localEulerAngles = Vector3.Lerp(mp.anglesRetracted, mp.anglesDeployed, hl); }
            }
            if (fcs.flaps != null) fcs.flaps.hlPos = hl;
        }
        public void SetDrive(int i, float ang)
        {
            var d = drives[i];
            d.rot.localRotation = d.baseRot * Quaternion.Euler(ang, 0f, 0f);
            d.drive.GetType().GetField("angle").SetValue(d.drive, ang);
        }
        // local AoA of a surface as SurfaceEffector.Eval defines it (flow angle in the lift-normal frame, stab downwash included)
        public float LocalAoA(SurfaceEffector se)
        {
            var part = (AeroPart)se.cs.attachedSurface;
            Vector3 v = part.rb.velocity - A.ac.windVelocity;
            if (se.kind == SurfaceKind.Stabilator) v = Downwash.Apply(v, P.q, A.ac.rb.velocity - A.ac.windVelocity);
            Vector3 vl = Quaternion.Inverse(part.liftNormal.rotation) * v;
            return -Mathf.Atan2(vl.y, vl.z) * Mathf.Rad2Deg;
        }
    }

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; string outPath = kv.TryGetValue("out", out var o) ? o : "eqerr.csv";
        var modes = (kv.TryGetValue("modes", out var ms) ? ms : "3,6").Split(',').Select(int.Parse).ToHashSet();
        int every = kv.TryGetValue("every", out var ev) ? int.Parse(ev) : 1;
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = Gyro.Unity };
        P.Init(1000f, 150f, 5f, 5f);
        P.CreateFcs();
        var fcs = P.fcs;
        float mass = A.mass * MassScale;
        var lines = File.ReadLines(log).GetEnumerator();
        lines.MoveNext();
        var hdr = lines.Current.Split(',');
        var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        var surfNames = hdr.Where(h => h.EndsWith("_now") && !h.StartsWith("tvc") && !h.StartsWith("hl")).Select(h => h.Substring(0, h.Length - 4)).ToArray();
        var ps = new Poser(A, P, surfNames);
        bool hasTvcd = col.ContainsKey("tvcdNow"), hasTvcy = col.ContainsKey("tvcyNow"), hasSplit = col.ContainsKey("splitL");
        Console.Error.WriteLine($"# eqerr: mass {A.mass:F0} x {MassScale:F3}; I diag {A.Itot[0,0]:F0}/{A.Itot[1,1]:F0}/{A.Itot[2,2]:F0} (pitch/yaw/roll); surfaces [{string.Join(",", surfNames)}]; {ps.drives.Count} drives, {ps.hlds.Count} HLDs, 0 airbrakes (Ifrit: split decelerons); tvcd {hasTvcd} tvcy {hasTvcy} split {hasSplit}");
        // engines: side for the differential pitch TVC (Ifrit), from the game's vectoring vector
        // TvcDiffEffector: per-engine angle = tvcNow + side x tvcdNow, side = -sign(vec.z) (game: engine_L +15 -> -1, engine_R -15 -> +1)
        var side = A.engines.Select(e => e.thrustVectoring.z != 0f ? -Mathf.Sign(e.thrustVectoring.z) : (e.name.EndsWith("_L") ? -1f : e.name.EndsWith("_R") ? 1f : 0f)).ToArray();
        using var w = new StreamWriter(outPath);
        bool yawSplit = kv.TryGetValue("yawsplit", out var ysv) && ysv != "0";
        w.WriteLine("t,mode,ias,tas,rho,mach,qbar,alpha,beta,p,q,r,nz,tw,tvc,tvcd,tvcy,hl,ab,splitL,splitR,pdot,qdot,rdot,nzp,nyp,axp,Fx,Fy,Fz,Mx,My,Mz,Tn,drvAilRes,rdotNoYawEff,pdotNoYawEff," + string.Join(",", surfNames.Select(n => "aoaRes_" + n)) + "," + string.Join(",", surfNames.Select(n => "now_" + n)));
        int n = 0, used = 0; float g = 9.81f;
        P.EvalOnly = true;
        P.TvcAngleOverride = null;
        float tvcNow = 0f, tvcdNow = 0f, tvcyNow = 0f;
        P.TvcAngleOverride = (tf, n5) => { int ei = A.engines.IndexOf(tf); return tvcNow + (ei >= 0 ? side[ei] : 0f) * tvcdNow; };
        P.TvcYawIn = tf => 0f;
        while (lines.MoveNext())
        {
            var f = lines.Current.Split(',');
            if (f.Length < hdr.Length - 1) continue;
            n++;
            if (n % every != 0) continue;
            float G(string k) => float.Parse(f[col[k]], IC);
            bool Has(string k) => col.ContainsKey(k);
            int mode = (int)G("mode");
            if (!modes.Contains(mode)) continue;
            float tas = G("tas"); if (tas < 40f) continue;
            float rho = G("rho"), alpha = G("alpha"), beta = G("beta"), pr = G("p"), qr = G("q"), rr = G("r");
            float phi = G("phi"), theta = G("theta"), tw = G("tw"), hl = G("hlPos"), ab = Has("abOpen") ? G("abOpen") : 0f;
            tvcNow = G("tvcNow"); tvcdNow = hasTvcd ? G("tvcdNow") : 0f; tvcyNow = hasTvcy ? G("tvcyNow") : 0f;
            ps.PoseState(tas, rho, alpha, beta, pr, qr, rr, phi, theta);
            foreach (var sn in surfNames) ps.SetSurface(sn, G(sn + "_now"));
            if (hasSplit) { ps.SetSplit("flap_L", G("splitL")); ps.SetSplit("flap_R", G("splitR")); }
            ps.SetSlats(hl);
            float flapBias = Has("flapBias") ? G("flapBias") : 0f, relief = Has("relief") ? G("relief") : 0f;
            float drvAilRes = 0f;
            for (int i = 0; i < ps.drives.Count; i++)
            {
                var d = ps.drives[i];
                float a0 = d.flap ? Mathf.Clamp(-flapBias - (d.hasHl ? d.hlAngle * hl : 0f), -25f, 20f) : Mathf.Clamp(relief, -25f, 20f);
                ps.SetDrive(i, a0);
                string ln = surfNames.FirstOrDefault(x => x.Equals(d.surf.name, StringComparison.OrdinalIgnoreCase) || d.surf.name.ToLowerInvariant().EndsWith(x.ToLowerInvariant()));
                if (DriveSolve == 1 && ln != null && Has(ln + "_aoaNow"))
                {
                    float target = G(ln + "_aoaNow");
                    float f0 = ps.LocalAoA(d.surf); ps.SetDrive(i, a0 + 1f); float f1 = ps.LocalAoA(d.surf); float k = f1 - f0;
                    if (Mathf.Abs(k) > 0.2f)
                    {
                        float ang = Mathf.Clamp(a0 + (target - f0) / k, -25f, 20f); ps.SetDrive(i, ang);
                        float f2 = ps.LocalAoA(d.surf); if (Mathf.Abs(f2 - target) > 0.05f) { ang = Mathf.Clamp(ang + (target - f2) / k, -25f, 20f); ps.SetDrive(i, ang); }
                        drvAilRes = Mathf.Max(drvAilRes, Mathf.Abs(ps.LocalAoA(d.surf) - target));
                    }
                    else ps.SetDrive(i, a0);
                }
            }
            // nozzles: the override callback gives each engine tvcNow +/- tvcdNow; the Revoker's yaw nozzle through nozzleAngles.y
            foreach (var tf in A.engines) { tf.nozzleAngles.y = tvcyNow; }
            P.TvcYawIn = tf => (tf.thrustVectoring.y != 0f) ? -tvcyNow / tf.thrustVectoring.y : 0f;
            int nn = Math.Max(1, A.engines.Sum(e => e.nozzles.Length));
            float Tn = tw * mass * g / nn;
            Plant.ThrustOverride = Tn;
            P.throttle = 1f;
            P.Step();   // EvalOnly: forces and moments at this pose, no FCS, no servos, no integration
            Vector3 F = P.lastForce - Physics.gravity * A.mass, M = P.lastTorque;
            Vector3 wdot = P.AngAccel(M);
            Vector3 l = Quaternion.Inverse(P.q) * wdot;
            Vector3 aAero = new Vector3(-l.z, -l.x, l.y) * Mathf.Rad2Deg;
            float nzp = Vector3.Dot(F, P.q * Vector3.up) / (mass * g);
            float nyp = Vector3.Dot(F, P.q * Vector3.right) / (mass * g);
            float axp = Vector3.Dot(F, P.vCg.normalized) / mass;
            Vector3 Fb = Quaternion.Inverse(P.q) * F, Mb = Quaternion.Inverse(P.q) * M;
            float h = P.pCg.y, mach = tas / DragReliefMath.SpeedOfSound(h), qbar = 0.5f * rho * tas * tas;
            // 0.6.0 governor port: the same pose with the rudders at neutral - the difference to rdot is the plant's yaw from them (yawsplit=1)
            float rdot0 = float.NaN, pdot0 = float.NaN;
            if (yawSplit)
            {
                // Ifrit: no rudders. The yaw effectors are the canted tails' differential and the split decelerons: first the
                // splits closed (rdotNoYawEff = without splits), then the tails at their mean too (pdotNoYawEff column carries
                // the yaw accel without splits AND without the tail differential)
                if (hasSplit) { ps.SetSplit("flap_L", 0f); ps.SetSplit("flap_R", 0f); }
                P.Step();
                Vector3 l0 = Quaternion.Inverse(P.q) * P.AngAccel(P.lastTorque);
                rdot0 = l0.y * Mathf.Rad2Deg;
                string eL = surfNames.FirstOrDefault(x => x.ToLowerInvariant().Contains("elevator_l")), eR = surfNames.FirstOrDefault(x => x.ToLowerInvariant().Contains("elevator_r"));
                if (eL != null && eR != null)
                {
                    float mean = 0.5f * (G(eL + "_now") + G(eR + "_now"));
                    ps.SetSurface(eL, mean); ps.SetSurface(eR, mean);
                    P.Step();
                    Vector3 l1 = Quaternion.Inverse(P.q) * P.AngAccel(P.lastTorque);
                    pdot0 = l1.y * Mathf.Rad2Deg;
                }
            }
            string S(float x) => x.ToString("0.####", IC);
            var res = surfNames.Select(sn => ps.seByLog.TryGetValue(sn, out var se) && Has(sn + "_aoaNow") ? S(ps.LocalAoA(se) - G(sn + "_aoaNow")) : "");
            w.WriteLine(string.Join(",", new[] { S(G("t")), mode.ToString(), S(G("ias")), S(tas), S(rho), S(mach), S(qbar), S(alpha), S(beta), S(pr), S(qr), S(rr), S(G("nz")), S(tw), S(tvcNow), S(tvcdNow), S(tvcyNow), S(hl), S(ab), S(hasSplit ? G("splitL") : 0f), S(hasSplit ? G("splitR") : 0f),
                S(aAero.x), S(aAero.y), S(aAero.z), S(nzp), S(nyp), S(axp), S(Fb.x), S(Fb.y), S(Fb.z), S(Mb.x), S(Mb.y), S(Mb.z), S(Tn), S(drvAilRes), S(rdot0), S(pdot0) }.Concat(res).Concat(surfNames.Select(sn => S(G(sn + "_now"))))));
            used++;
        }
        Plant.ThrustOverride = -1f;
        Console.Error.WriteLine($"# eqerr: {used} of {n} rows -> {outPath}");
    }
}
