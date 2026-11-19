// Vortex 0.3.1: lateral authority vs AoA at a pitch-trimmed pose (stall-stagger "hole" search).
// For each AoA: the elevons' common deflection, the pitch nozzle and the rudder flare are searched for the smallest pitch
// acceleration (preferring the least elevon); then the elevon differential (inner only, outer only, both) is swept and the
// best stability-axis roll acceleration each way is reported, with and without the rudders' yaw and the yaw nozzle.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

internal static class VortexAuth
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
    struct Pose { public float eIL, eIR, eOL, eOR, rL, rR, tP, tY; }
    static Vector3 Q(Plant P, Pose s)
    {
        foreach (var cs in P.A.surfaces)
        {
            float d = 0f;
            switch (cs.name) { case "elevator_L": d = s.eIL; break; case "elevator_R": d = s.eIR; break; case "aileron_L": d = s.eOL; break; case "aileron_R": d = s.eOR; break; case "rudder_L": d = s.rL; break; case "rudder_R": d = s.rR; break; }
            d = Mathf.Clamp(d, -30f, 30f);
            cs.job.currentPitch = d; cs.job.currentRoll = 0; cs.job.currentYaw = 0;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
            cs.splitAmount = 0f;
        }
        P.TvcAngleOverride = (tf, n) => s.tP; P.TvcYawOverride = (tf, n) => s.tY;
        return IfritTest.Acc(P);
    }
    public static void Run(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), thr = F(kv, "thr", 1f);
        var iasList = (kv.TryGetValue("iasl", out var il) ? il : "300,500").Split(',').Select(x => float.Parse(x, IC));
        float a0 = F(kv, "a0", 0f), a1 = F(kv, "a1", 70f), da = F(kv, "da", 2.5f);
        foreach (var iasK in iasList)
        {
            var P = IfritTest.Make(alt, iasK, true, thr, out float tas); P.EvalOnly = true;
            Console.WriteLine($"# IAS {iasK:F0} km/h TAS {tas * 3.6f:F0} alt {alt:F0} thr {thr:F2} mass {P.A.mass:F0}");
            Console.WriteLine("# a  qTrim  trim(c tP flare) | inner R L | outer R L | both R L | full R L   (stability-axis roll accel deg/s^2; R = right roll +, L shown as magnitude)");
            for (float a = a0; a <= a1 + 1e-3f; a += da)
            {
                P.Init(alt, tas, a, a);
                float ar = a * Mathf.Deg2Rad, ca = Mathf.Cos(ar), sa = Mathf.Sin(ar);
                Pose best = default; float bestCost = float.MaxValue; Vector3 q0 = Vector3.zero;
                for (float c = -30f; c <= 30.01f; c += 2f)
                    for (float t = -10f; t <= 10.01f; t += 2.5f)
                        for (float fl = -30f; fl <= 30.01f; fl += 7.5f)
                        {
                            var s = new Pose { eIL = c, eIR = c, eOL = c, eOR = c, rL = fl, rR = fl, tP = t };
                            var acc = Q(P, s);
                            float cost = Mathf.Abs(acc.y) + 0.02f * Mathf.Abs(c) + 0.005f * Mathf.Abs(fl);
                            if (cost < bestCost) { bestCost = cost; best = s; q0 = acc; }
                        }
                float PS(Vector3 d) => d.x * ca + d.z * sa;
                (float, float) Sweep(bool inner, bool outer, bool full)
                {
                    float bR = 0f, bL = 0f;
                    for (float dd = 2f; dd <= 60.01f; dd += 2f)
                        foreach (float sg in new[] { 1f, -1f })
                        {
                            var s = best;
                            // left roll: left elevons TE up (+), right TE down (-), as in the 11:50 log
                            float d = sg * dd;   // sg +1 = left roll
                            if (inner) { s.eIL = best.eIL + d; s.eIR = best.eIR - d; }
                            if (outer) { s.eOL = best.eOL + d; s.eOR = best.eOR - d; }
                            if (full)
                            {
                                float yw = 30f;   // the rudders' yaw split, toward the roll (searched both ways below)
                                float bestF = 0f;
                                foreach (float ys in new[] { 1f, -1f, 0f })
                                    foreach (float ty in new[] { 15f, -15f, 0f })
                                    {
                                        var s2 = s; s2.rL = best.rL + ys * yw; s2.rR = best.rR - ys * yw; s2.tY = ty;
                                        float v = PS(Q(P, s2) - q0);
                                        if (-sg * v > bestF) bestF = -sg * v;
                                    }
                                if (sg > 0) bL = Mathf.Max(bL, bestF); else bR = Mathf.Max(bR, bestF);
                                continue;
                            }
                            float v0 = PS(Q(P, s) - q0);
                            if (sg > 0) bL = Mathf.Max(bL, -v0); else bR = Mathf.Max(bR, v0);
                        }
                    return (bR, bL);
                }
                var iN = Sweep(true, false, false); var oU = Sweep(false, true, false); var bo = Sweep(true, true, false); var fu = Sweep(true, true, true);
                Console.WriteLine(string.Format(IC, "{0,5:F1} {1,6:F1} ({2,4:F0} {3,5:F1} {4,5:F1}) | {5,6:F0} {6,6:F0} | {7,6:F0} {8,6:F0} | {9,6:F0} {10,6:F0} | {11,6:F0} {12,6:F0}",
                    a, q0.y, best.eIL, best.tP, best.rL, iN.Item1, iN.Item2, oU.Item1, oU.Item2, bo.Item1, bo.Item2, fu.Item1, fu.Item2));
            }
        }
    }
}
