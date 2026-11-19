// 3 Nov 2026 (2.31.1, handoff 3.1 a): what the FCS's airframe probe (FcsController.SlipMomentAt) leaves out.
// At logged states: the probe's roll / yaw sideslip derivatives next to the plant's own finite difference (Plant.Forces at
// beta +-db, everything else posed from the log), and the plant again with its extras peeled off one at a time:
//   V0 full plant (logged p q r, force clamp, bench multipliers, DCnbHi, camber, downwash)
//   V1 V0 with p = q = r = 0 (no rotation at the parts)
//   V2 V1 without the per-part force clamp
//   V3 V2 with the bench-only multipliers off (Mis* = 1, MisHi* = -1, DCnbHi = 0)
// The probe has none of V0's extras, so probe ~ V3 means the gap is in those extras; probe != V3 means the probe evaluates
// the airframe differently from the plant (geometry, tables, surfaces).
//   bench sliptruth log=<csv> out=<csv> [every=20] [db=2] [amin=-90] [amax=90]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class SlipTruth
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    // body-axis roll / yaw acceleration (deg/s^2; p right wing down, r nose right) of a plant moment
    static void PlantAcc(Plant P, Vector3 M, out float pd, out float rd)
    {
        Vector3 wd = Quaternion.Inverse(P.q) * P.AngAccel(M) * Mathf.Rad2Deg;
        pd = -wd.z; rd = wd.y;
    }

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; string outPath = kv.TryGetValue("out", out var o) ? o : "sliptruth.csv";
        int every = kv.TryGetValue("every", out var ev) ? int.Parse(ev) : 20;
        float db = kv.TryGetValue("db", out var dbs) ? float.Parse(dbs, IC) : 2f;
        float amin = kv.TryGetValue("amin", out var a0s) ? float.Parse(a0s, IC) : -90f;
        float amax = kv.TryGetValue("amax", out var a1s) ? float.Parse(a1s, IC) : 90f;
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = Gyro.None };   // aero-only derivatives (the gyroscopic term is not airframe aero)
        P.Init(1000f, 150f, 5f, 5f); P.CreateFcs();
        var fcs = P.fcs; var ps = new EqErr.Poser(A, P);
        var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var mSlip = typeof(FcsController).GetMethod("SlipMomentAt", bf);
        var mAir = typeof(FcsController).GetMethod("AirframeMomentAt", bf); var fVcg = typeof(FcsController).GetField("vCgAir", bf);
        var mRoll = typeof(FcsController).GetMethod("AccRoll", bf); var mYaw = typeof(FcsController).GetMethod("AccYaw", bf);
        var lines = File.ReadLines(log).GetEnumerator(); lines.MoveNext();
        var hdr = lines.Current.Split(','); var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        if (col.ContainsKey("airRefCg")) EqErr.LogRefRoot = 0;
        // bench-only multipliers, saved to restore
        var aT = typeof(Airframe); string[] misN = { "MisStab", "MisStabL", "MisAil", "MisFlap", "MisRud", "MisWing", "MisBody", "MisFin" };
        string[] misHiN = { "MisFinHi", "MisRudHi", "MisWingHi", "MisBodyHi", "MisStabHi", "MisAilHi", "MisFlapHi" };
        var sf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var misSave = new Dictionary<string, float>(); foreach (var mn in misN) misSave[mn] = (float)aT.GetField(mn, sf).GetValue(null);
        var misHiSave = new Dictionary<string, float>(); foreach (var mn in misHiN) misHiSave[mn] = (float)aT.GetField(mn, sf).GetValue(null);
        float misStabPitch = Airframe.MisStabPitch, dcnb = Plant.DCnbHi; bool clamp = Plant.ForceClamp;
        using var w = new StreamWriter(outPath);
        w.WriteLine("t,tas,qbar,alpha,beta,p,q,r,prL,prN,v0L,v0N,v1L,v1N,v2L,v2N,v3L,v3N,Lr,Nr,Lp,Np,eLb,eNb,eLr,eNr,eLp,eNp,eAX,eAZ,pAX,pAZ");
        int n = 0, used = 0;
        while (lines.MoveNext())
        {
            var f = lines.Current.Split(','); if (f.Length < hdr.Length - 1) continue;
            n++; if (n % every != 0) continue;
            float G(string k) => float.Parse(f[col[k]], IC);
            if ((int)G("mode") != 3) continue;
            float tas = G("tas"); if (tas < 40f) continue;
            float rho = G("rho"), alpha = G("alpha"), beta = G("beta");
            if (alpha < amin || alpha > amax) continue;
            float pr = G("p"), qr = G("q"), rr = G("r"), phi = G("phi"), th = G("theta");
            foreach (var sn in EqErr.Poser.SurfNames) { float now = G(sn + "_now"); ps.SetSurface(sn, now); ps.seBy[sn].now = now; }
            ps.SetSlats(G("hlPos")); ps.SetNozzles(G("tvcNow")); ps.SetAirbrakes(G("abOpen"));
            // plant finite difference, d(accel)/d(beta) per deg, at the logged state with the variant's settings
            (float L, float N) Fd(bool rot)
            {
                float a = rot ? pr : 0f, b = rot ? qr : 0f, c = rot ? rr : 0f;
                ps.PoseState(tas, rho, alpha, beta + db, a, b, c, phi, th); P.Forces(out _, out var Mp); PlantAcc(P, Mp, out float pP, out float rP);
                ps.PoseState(tas, rho, alpha, beta - db, a, b, c, phi, th); P.Forces(out _, out var Mn); PlantAcc(P, Mn, out float pN, out float rN);
                return ((pP - pN) / (2f * db), (rP - rN) / (2f * db));
            }
            var v0 = Fd(true);
            // rate derivatives of the full plant (per deg/s), body p and r perturbed +-5 deg/s about the logged rates
            (float L, float N) Rd(float dp, float dr)
            {
                ps.PoseState(tas, rho, alpha, beta, pr + dp, qr, rr + dr, phi, th); P.Forces(out _, out var M1); PlantAcc(P, M1, out float p1, out float r1);
                ps.PoseState(tas, rho, alpha, beta, pr - dp, qr, rr - dr, phi, th); P.Forces(out _, out var M2); PlantAcc(P, M2, out float p2, out float r2);
                float d = 2f * (dp + dr); return ((p1 - p2) / d, (r1 - r2) / d);
            }
            var dR = Rd(0f, 5f); var dP = Rd(5f, 0f);
            var v1 = Fd(false);
            Plant.ForceClamp = false; var v2 = Fd(false);
            foreach (var k in misN) aT.GetField(k, sf).SetValue(null, 1f);
            foreach (var k in misHiN) aT.GetField(k, sf).SetValue(null, -1f);
            Airframe.MisStabPitch = -1f; Plant.DCnbHi = 0f;
            var v3 = Fd(false);
            foreach (var k in misN) aT.GetField(k, sf).SetValue(null, misSave[k]);
            foreach (var k in misHiN) aT.GetField(k, sf).SetValue(null, misHiSave[k]);
            Airframe.MisStabPitch = misStabPitch; Plant.DCnbHi = dcnb; Plant.ForceClamp = clamp;
            // the probe at the same state (the FCS's own mass properties from the posed plant)
            ps.PoseState(tas, rho, alpha, beta, pr, qr, rr, phi, th); P.Pose(); A.ac.airDensity = rho;
            Quaternion rootRot = P.q; Quaternion rootInv = Quaternion.Inverse(P.q);
            fcs.alpha = alpha; fcs.beta = beta; fcs.tas = tas;
            fcs.RebuildBodies(); fcs.UpdateMassProps(true, rootInv);
            var ctx = new EvalContext { cg = fcs.cg, rootInv = rootInv, rho = rho, wind = A.ac.windVelocity, vAir = A.ac.rb.velocity - A.ac.windVelocity };
            var mP = (Vector3)mSlip.Invoke(fcs, new object[] { alpha, beta + db, rho, rootRot, ctx });
            var mN = (Vector3)mSlip.Invoke(fcs, new object[] { alpha, beta - db, rho, rootRot, ctx });
            float prL = ((float)mRoll.Invoke(fcs, new object[] { mP }) - (float)mRoll.Invoke(fcs, new object[] { mN })) / (2f * db);
            float prN = ((float)mYaw.Invoke(fcs, new object[] { mP }) - (float)mYaw.Invoke(fcs, new object[] { mN })) / (2f * db);
            // the FCS's 2.31.2 per-part evaluator (AirframeMomentAt) at the same state, same perturbations
            float fa = alpha * Mathf.Deg2Rad;
            Vector3 VAt(float bDeg) { float fb = bDeg * Mathf.Deg2Rad; return rootRot * new Vector3(Mathf.Sin(fb), -Mathf.Sin(fa) * Mathf.Cos(fb), Mathf.Cos(fa) * Mathf.Cos(fb)) * tas; }
            Vector3 Wa(float p_, float q_, float r_) => rootRot * new Vector3(-q_, r_, -p_) * Mathf.Deg2Rad;
            (float L, float N) E(Vector3 vA, Vector3 wA) { var mm = (Vector3)mAir.Invoke(fcs, new object[] { vA, wA, rho, rootRot }); return ((float)mRoll.Invoke(fcs, new object[] { mm }), (float)mYaw.Invoke(fcs, new object[] { mm })); }
            fVcg.SetValue(fcs, VAt(beta));
            var e0 = E(VAt(beta), Wa(pr, qr, rr));
            var eb1 = E(VAt(beta + db), Wa(pr, qr, rr)); var eb2 = E(VAt(beta - db), Wa(pr, qr, rr));
            var er1 = E(VAt(beta), Wa(pr, qr, rr + 5f)); var er2 = E(VAt(beta), Wa(pr, qr, rr - 5f));
            var ep1 = E(VAt(beta), Wa(pr + 5f, qr, rr)); var ep2 = E(VAt(beta), Wa(pr - 5f, qr, rr));
            // the plant's aero acceleration at the logged state without thrust (engines' thrust zeroed for this call)
            ps.PoseState(tas, rho, alpha, beta, pr, qr, rr, phi, th);
            var thr = new List<(JetNozzle n, float t)>(); foreach (var tf in A.engines) foreach (var nz in tf.nozzles) { thr.Add((nz, nz.totalThrust)); nz.totalThrust = 0f; }
            P.Forces(out _, out var M0); PlantAcc(P, M0, out float pA0, out float rA0);
            foreach (var (nzo, t0) in thr) nzo.totalThrust = t0;
            float qb = 0.5f * rho * tas * tas;
            string S(float x) => x.ToString("0.####", IC);
            w.WriteLine(string.Join(",", new[] { S(G("t")), S(tas), S(qb), S(alpha), S(beta), S(pr), S(qr), S(rr), S(prL), S(prN), S(v0.L), S(v0.N), S(v1.L), S(v1.N), S(v2.L), S(v2.N), S(v3.L), S(v3.N), S(dR.L), S(dR.N), S(dP.L), S(dP.N), S((eb1.L - eb2.L) / (2f * db)), S((eb1.N - eb2.N) / (2f * db)), S((er1.L - er2.L) / 10f), S((er1.N - er2.N) / 10f), S((ep1.L - ep2.L) / 10f), S((ep1.N - ep2.N) / 10f), S(e0.L), S(e0.N), S(pA0), S(rA0) }));
            used++;
        }
        Console.Error.WriteLine($"# sliptruth: {used} states -> {outPath}");
    }
}
