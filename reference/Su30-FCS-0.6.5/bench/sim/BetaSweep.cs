// roll/yaw acceleration vs sideslip and roll rate at fixed AoA, neutral surfaces (plant aero only)
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
internal static class BetaSweep
{
    public static void Run(Dictionary<string, string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float alt = kv.TryGetValue("alt", out var a0s) ? float.Parse(a0s, IC) : 1000f;
        float ias = kv.TryGetValue("ias", out var i0) ? float.Parse(i0, IC) : 200f;
        float thr = kv.TryGetValue("thr", out var ts) ? float.Parse(ts, IC) : 1f;
        var P = IfritTest.Make(alt, ias, true, thr, out float tas); P.EvalOnly = true;
        foreach (float a in new[] { 20f, 30f, 45f, 55f, 65f })
        {
            foreach (float b in new[] { -12f, -6f, 0f, 6f, 12f })
            {
                P.Init(alt, tas, a, a);
                // sideslip: rotate velocity about body up axis (positive beta = wind from the right = nose left of velocity)
                var up = P.q * Vector3.up; var fwd = P.vCg.normalized; var right = P.q * Vector3.right;
                P.vCg = Quaternion.AngleAxis(b, up) * P.vCg;   // velocity swings toward +right → beta>0 (nose left of V)
                P.w = Vector3.zero; P.Pose();
                IfritTest.Apply(P, new IfritTest.Cfg());
                var acc = IfritTest.Acc(P);
                Console.WriteLine($"alpha={a} beta={b} p={acc.x:F1} q={acc.y:F1} r={acc.z:F1}");
            }
            // roll damping: p = +30 deg/s at beta 0
            P.Init(alt, tas, a, a); P.w = (P.q * Vector3.forward) * (30f * Mathf.Deg2Rad); P.Pose();
            IfritTest.Apply(P, new IfritTest.Cfg()); var acp = IfritTest.Acc(P);
            Console.WriteLine($"alpha={a} p=+30: pdot={acp.x:F1} qdot={acp.y:F1} rdot={acp.z:F1}");
        }
    }
}
