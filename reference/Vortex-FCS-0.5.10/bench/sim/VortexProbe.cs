// Vortex 0.3.6 investigation: body / stability-axis accelerations of a logged pose, each wing surface's derivative there,
// and a search over the four wing surfaces (rudders and nozzles held) for the most velocity-vector roll at the same pitch.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class VortexProbe
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
    static Vector3 Q(Plant P, float[] w, float rL, float rR, float tP, float tY)
    {
        foreach (var cs in P.A.surfaces)
        {
            float d = 0f;
            switch (cs.name) { case "elevator_L": d = w[0]; break; case "elevator_R": d = w[1]; break; case "aileron_L": d = w[2]; break; case "aileron_R": d = w[3]; break; case "rudder_L": d = rL; break; case "rudder_R": d = rR; break; }
            d = Mathf.Clamp(d, -DMax, DMax);
            cs.job.currentPitch = d; cs.job.currentRoll = 0; cs.job.currentYaw = 0;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
            cs.splitAmount = 0f;
        }
        P.TvcAngleOverride = (tf, n) => tP; P.TvcYawOverride = (tf, n) => tY;
        return IfritTest.Acc(P);
    }

    static float DMax = 30f;
    static void Dump(Plant P, string lab)
    {
        var A = P.A; float rho = 1.2f;
        Vector3 fw = P.q * Vector3.forward, up = P.q * Vector3.up, rt = P.q * Vector3.right;
        foreach (var b in A.bodies)
        {
            var ap = b.part; if (ap.airfoil < 0) continue;
            string n = ap.name; if (Environment.GetEnvironmentVariable("DUMPALL") == null && !(n.Contains("rudder") || n.Contains("vstab") || n.Contains("tail") || n.Contains("elevator") || n.Contains("aileron"))) continue;
            Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude; var lt = ap.liftNormal;
            Vector3 vp = v; if (Downwash.IsStabName(ap.name)) v = Downwash.Apply(v, P.q, P.vCg);
            Vector3 v6 = Quaternion.Inverse(lt.rotation) * v; Vector3 nrm = Vector3.Cross(v, lt.right).normalized;
            float al = Mathf.Atan2(v6.y, v6.z);
            float idx = Mathf.Clamp(al * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1);
            float cl = Mathf.Lerp(A.liftCharts[ap.ChartId][i], A.liftCharts[ap.ChartId][j], idx - i);
            float cd = Mathf.Lerp(A.dragCharts[ap.ChartId][i], A.dragCharts[ap.ChartId][j], idx - i);
            float eff = ap.wingEffectiveness;
            Vector3 fl = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * eff);
            Vector3 fd = -v.normalized * (cd * rho * v2 * 0.5f * ap.wingArea * eff);
            Vector3 r = b.rb.worldCenterOfMass - P.pCg + (ap.centerOfLift != Vector3.zero ? lt.rotation * ap.centerOfLift : Vector3.zero);
            Vector3 m = Vector3.Cross(r, fl + fd);
            Vector3 nb = lt.rotation * Vector3.up;
            Console.WriteLine($"{lab} {n,-12} aLoc {al * Mathf.Rad2Deg,7:F1} cl {cl,6:F3} cd {cd,6:F3} eff {eff:F2} | Mroll {-Vector3.Dot(m, fw),8:F0} Mpitch {-Vector3.Dot(m, rt),8:F0} Myaw {Vector3.Dot(m, up),8:F0} | nrmB ({Vector3.Dot(nb,rt):F2},{Vector3.Dot(nb,up):F2},{Vector3.Dot(nb,fw):F2}) rB ({Vector3.Dot(r,rt):F2},{Vector3.Dot(r,up):F2},{Vector3.Dot(r,fw):F2})");
        }
    }
    public static void Run(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 100f), tas = F(kv, "tas", 82f), a = F(kv, "a", 69f), thr = F(kv, "thr", 1f);
        float[] w0 = { F(kv, "eIL", 0), F(kv, "eIR", 0), F(kv, "eOL", 0), F(kv, "eOR", 0) };
        float rL = F(kv, "rL", 0), rR = F(kv, "rR", 0), tP = F(kv, "tP", 0), tY = F(kv, "tY", 0);
        var P = IfritTest.Make(alt, tas, false, thr, out float _); P.EvalOnly = true; P.Init(alt, tas, a, a);
        float bt = F(kv, "b", 0f) * Mathf.Deg2Rad;
        if (bt != 0f || kv.ContainsKey("p") || kv.ContainsKey("r") || kv.ContainsKey("qr"))
        {
            var vb = Quaternion.Inverse(P.q) * P.vCg; vb = new Vector3(vb.magnitude * Mathf.Sin(bt), vb.y * Mathf.Cos(bt), vb.z * Mathf.Cos(bt)); P.vCg = P.q * vb;
            var wb = new Vector3(-F(kv, "qr", 0) , F(kv, "r", 0), -F(kv, "p", 0)) * Mathf.Deg2Rad; P.w = P.q * wb; P.Pose();
        }
        float ca = Mathf.Cos(a * Mathf.Deg2Rad), sa = Mathf.Sin(a * Mathf.Deg2Rad);
        string S(Vector3 v) => $"p {v.x,7:F1} q {v.y,7:F1} r {v.z,7:F1} | pS {v.x * ca + v.z * sa,7:F1} rS {v.z * ca - v.x * sa,7:F1}";
        var b = Q(P, w0, rL, rR, tP, tY);
        if (kv.ContainsKey("tables"))
        {
            DMax = 60f; var z = new float[4];
            var b0 = Q(P, z, 0, 0, 0, 0);
            Console.WriteLine($"base,0,0,{b0.x:F3},{b0.y:F3},{b0.z:F3}");
            for (int i = 0; i < 6; i++) for (float dd = -60; dd <= 60.01f; dd += 2.5f)
            {
                var w = new float[4]; float rl = 0, rr = 0;
                if (i < 4) w[i] = dd; else if (i == 4) rl = dd; else rr = dd;
                var v = Q(P, w, rl, rr, 0, 0) - b0;
                Console.WriteLine($"s{i},{dd},0,{v.x:F3},{v.y:F3},{v.z:F3}");
            }
            for (float tp = -20; tp <= 20.01f; tp += 1f) for (float ty = -15; ty <= 15.01f; ty += 1f)
            {
                var v = Q(P, z, 0, 0, tp, ty) - b0;
                Console.WriteLine($"tvc,{tp},{ty},{v.x:F3},{v.y:F3},{v.z:F3}");
            }
            return;
        }
        if (kv.ContainsKey("dump")) { Dump(P, "a" + a); return; }
        Console.WriteLine($"# a {a} tas {tas} pose eIL {w0[0]} eIR {w0[1]} eOL {w0[2]} eOR {w0[3]} rL {rL} rR {rR} tP {tP} tY {tY}");
        Console.WriteLine("base            " + S(b));
        string[] nm = { "elevator_L", "elevator_R", "aileron_L", "aileron_R" };
        for (int i = 0; i < 4; i++)
        {
            var wp = (float[])w0.Clone(); var wm = (float[])w0.Clone(); wp[i] = Mathf.Min(30, w0[i] + 5); wm[i] = Mathf.Max(-30, w0[i] - 5);
            float span = wp[i] - wm[i]; if (span < 1e-3f) continue;
            var d = (Q(P, wp, rL, rR, tP, tY) - Q(P, wm, rL, rR, tP, tY)) / span * 10f;
            Console.WriteLine($"d/d{nm[i],-10}x10 " + S(d));
        }
        foreach (var (lab, wz) in new (string, float[])[] { ("wing neutral", new float[] { 0, 0, 0, 0 }), ("wing mean", new float[] { (w0[0]+w0[1]+w0[2]+w0[3])/4, (w0[0]+w0[1]+w0[2]+w0[3])/4, (w0[0]+w0[1]+w0[2]+w0[3])/4, (w0[0]+w0[1]+w0[2]+w0[3])/4 }) })
            Console.WriteLine($"{lab,-15} " + S(Q(P, wz, rL, rR, tP, tY)));
        // search: most pS with pitch accel >= base - tol and rS >= base rS - slipTol (no worse sideslip build)
        float tol = F(kv, "qtol", 5f), sTol = F(kv, "stol", 5f);
        var top = new List<(float, float[], Vector3)>();
        for (float x0 = -30; x0 <= 30; x0 += 5) for (float x1 = -30; x1 <= 30; x1 += 5) for (float x2 = -30; x2 <= 30; x2 += 5) for (float x3 = -30; x3 <= 30; x3 += 5)
        {
            var w = new[] { x0, x1, x2, x3 }; var v = Q(P, w, rL, rR, tP, tY);
            if (v.y < b.y - tol) continue;
            float pS = v.x * ca + v.z * sa, rS = v.z * ca - v.x * sa;
            float slipB = b.z * ca - b.x * sa;
            if (Mathf.Abs(rS - slipB) > sTol) continue;
            top.Add((Mathf.Sign(F(kv, "dir", 1)) * pS, w, v));
        }
        top.Sort((u, v) => v.Item1.CompareTo(u.Item1));
        for (int i = 0; i < Math.Min(5, top.Count); i++)
            Console.WriteLine($"best{i} eIL {top[i].Item2[0],3} eIR {top[i].Item2[1],3} eOL {top[i].Item2[2],3} eOR {top[i].Item2[3],3}  " + S(top[i].Item3));
    }
}
