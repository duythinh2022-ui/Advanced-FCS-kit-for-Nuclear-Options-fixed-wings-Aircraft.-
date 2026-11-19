// yaw per drag of each yaw device: drag (N, along velocity) and yaw accel (deg/s^2) vs neutral at fixed AoA/IAS
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
internal static class YawDrag
{
    public static void Run(Dictionary<string, string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float alt = kv.TryGetValue("alt", out var a0s) ? float.Parse(a0s, IC) : 600f;
        float ias = kv.TryGetValue("ias", out var i0) ? float.Parse(i0, IC) : 250f;
        float thr = kv.TryGetValue("thr", out var ts) ? float.Parse(ts, IC) : 1f;
        var P = IfritTest.Make(alt, ias, true, thr, out float tas); P.EvalOnly = true;
        foreach (float a in (kv.TryGetValue("alphas", out var als) ? Array.ConvertAll(als.Split(','), x => float.Parse(x, IC)) : new[] { 5f, 15f, 20f, 25f, 30f }))
        {
            P.Init(alt, tas, a, a);
            IfritTest.Cfg C(Action<IfritTest.Cfg> f) { var c = new IfritTest.Cfg(); f(c); return c; }
            (float drag, float yaw, float roll, float pitch) Eval(IfritTest.Cfg c)
            {
                IfritTest.Apply(P, c); var acc = IfritTest.Acc(P);
                var vhat = P.vCg.normalized; float d = -Vector3.Dot(P.lastForce, vhat);
                return (d, acc.z, acc.x, acc.y);
            }
            var b = Eval(new IfritTest.Cfg());
            var cases = new (string, IfritTest.Cfg)[] {
                ("split_R 30", C(c => c.split["Flap_R"] = 30)),
                ("split_R 15", C(c => c.split["Flap_R"] = 15)),
                ("cross_R in+30/out-30", C(c => { c.defl["Flap_R"] = 30; c.defl["Aileron_R"] = -30; })),
                ("cross_R in-30/out+30", C(c => { c.defl["Flap_R"] = -30; c.defl["Aileron_R"] = 30; })),
                ("cross_R in+15/out-15", C(c => { c.defl["Flap_R"] = 15; c.defl["Aileron_R"] = -15; })),
                ("tails +20/-20", C(c => { c.defl["Elevator_L"] = 20; c.defl["Elevator_R"] = -20; })),
                ("tails -20/+20", C(c => { c.defl["Elevator_L"] = -20; c.defl["Elevator_R"] = 20; })),
                ("tails +10/-10", C(c => { c.defl["Elevator_L"] = 10; c.defl["Elevator_R"] = -10; })),
                ("ail_R -30 alone", C(c => c.defl["Aileron_R"] = -30)),
                ("tail_L -30 alone", C(c => c.defl["Elevator_L"] = -30)),
                ("tail_L -45 alone", C(c => c.defl["Elevator_L"] = -45)),
                ("tail_L +45 alone", C(c => c.defl["Elevator_L"] = 45)),
                ("tails -45/+45", C(c => { c.defl["Elevator_L"] = -45; c.defl["Elevator_R"] = 45; })),
                ("tails -30/+30", C(c => { c.defl["Elevator_L"] = -30; c.defl["Elevator_R"] = 30; })),
                ("tail_L -45 + tvc 15", C(c => { c.defl["Elevator_L"] = -45; c.tvcL = 15; c.tvcR = 15; })),
                ("tvc 15 alone", C(c => { c.tvcL = 15; c.tvcR = 15; })),
            };
            Console.WriteLine($"-- alpha {a} ias {ias} baseline drag {b.drag:F0} N");
            foreach (var (n, c) in cases)
            {
                var r = Eval(c);
                float dd = r.drag - b.drag; float dy = r.yaw - b.yaw;
                Console.WriteLine($"   {n,-22} dDrag {dd,7:F0} N  yaw {dy,6:F1} deg/s^2  roll {r.roll - b.roll,6:F1}  pitch {r.pitch - b.pitch,6:F1}  yaw/kN {(Mathf.Abs(dd) > 1 ? dy / dd * 1000 : 0),6:F1}");
            }
        }
    }
}
