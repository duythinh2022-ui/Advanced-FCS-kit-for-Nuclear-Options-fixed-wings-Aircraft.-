// Leaf calibration (3 Nov 2026): the FCS's own airframe probe (FcsController.SlipMomentAt, the game's per-part tables at a
// hypothetical AoA / sideslip) evaluated at logged states, roll and yaw parts and the stability-axis stiffness
//   kB = (dN cos a - dL sin a) / dB   (deg/s^2 per deg of sideslip; positive = restoring)
// so it can be compared with the per-band regression of the logs (handoff section 4 A: "compare the table probe to the fit").
//   bench slipprobe log=<csv> out=<csv> [every=30] [db=4]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class SlipProbe
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; string outPath = kv.TryGetValue("out", out var o) ? o : "slipprobe.csv";
        int every = kv.TryGetValue("every", out var ev) ? int.Parse(ev) : 30;
        float db = kv.TryGetValue("db", out var dbs) ? float.Parse(dbs, IC) : 4f;
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = Gyro.Unity };
        P.Init(1000f, 150f, 5f, 5f); P.CreateFcs();
        var fcs = P.fcs; var ps = new EqErr.Poser(A, P);
        var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var mSlip = typeof(FcsController).GetMethod("SlipMomentAt", bf);
        var mRoll = typeof(FcsController).GetMethod("AccRoll", bf); var mYaw = typeof(FcsController).GetMethod("AccYaw", bf);
        var lines = File.ReadLines(log).GetEnumerator(); lines.MoveNext();
        var hdr = lines.Current.Split(','); var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        using var w = new StreamWriter(outPath);
        w.WriteLine("t,tas,qbar,alpha,beta,L0,N0,LP,NP,LN,NN,dL,dN,kB,dL1e4,dN1e4,kB1e4");
        int n = 0, used = 0;
        while (lines.MoveNext())
        {
            var f = lines.Current.Split(','); if (f.Length < hdr.Length - 1) continue;
            n++; if (n % every != 0) continue;
            float G(string k) => float.Parse(f[col[k]], IC);
            if ((int)G("mode") != 3) continue;
            float tas = G("tas"); if (tas < 40f) continue;
            float rho = G("rho"), alpha = G("alpha"), beta = G("beta");
            ps.PoseState(tas, rho, alpha, beta, G("p"), G("q"), G("r"), G("phi"), G("theta"));
            foreach (var sn in EqErr.Poser.SurfNames) ps.SetSurface(sn, G(sn + "_now"));
            ps.SetSlats(G("hlPos")); ps.SetNozzles(G("tvcNow")); ps.SetAirbrakes(G("abOpen"));
            P.Pose(); A.ac.airDensity = rho;
            Quaternion rootRot = P.q; Quaternion rootInv = Quaternion.Inverse(P.q);
            fcs.alpha = alpha; fcs.beta = beta; fcs.tas = tas;
            fcs.RebuildBodies(); fcs.UpdateMassProps(true, rootInv);
            var ctx = new EvalContext { cg = fcs.cg, rootInv = rootInv, rho = rho, wind = A.ac.windVelocity, vAir = A.ac.rb.velocity - A.ac.windVelocity };
            object[] args0 = { alpha, 0f, rho, rootRot, ctx }; var m0 = (Vector3)mSlip.Invoke(fcs, args0);
            object[] argsP = { alpha, db, rho, rootRot, ctx }; var mP = (Vector3)mSlip.Invoke(fcs, argsP);
            object[] argsN = { alpha, -db, rho, rootRot, ctx }; var mN = (Vector3)mSlip.Invoke(fcs, argsN);
            float L0 = (float)mRoll.Invoke(fcs, new object[] { m0 }), N0 = (float)mYaw.Invoke(fcs, new object[] { m0 });
            float LP = (float)mRoll.Invoke(fcs, new object[] { mP }), NP = (float)mYaw.Invoke(fcs, new object[] { mP });
            float LN = (float)mRoll.Invoke(fcs, new object[] { mN }), NN = (float)mYaw.Invoke(fcs, new object[] { mN });
            float dL = (LP - LN) / (2f * db), dN = (NP - NN) / (2f * db);
            float ar = alpha * Mathf.Deg2Rad; float kB = dN * Mathf.Cos(ar) - dL * Mathf.Sin(ar);
            float qb = 0.5f * rho * tas * tas; float s = 1e4f / Mathf.Max(1f, qb);
            string S(float x) => x.ToString("0.####", IC);
            w.WriteLine(string.Join(",", new[] { S(G("t")), S(tas), S(qb), S(alpha), S(beta), S(L0), S(N0), S(LP), S(NP), S(LN), S(NN), S(dL), S(dN), S(kB), S(dL * s), S(dN * s), S(kB * s) }));
            used++;
        }
        Console.Error.WriteLine($"# slipprobe: {used} states -> {outPath}");
    }
}
