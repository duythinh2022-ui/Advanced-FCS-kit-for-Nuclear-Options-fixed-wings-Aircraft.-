// 2.0.6: symmetric rudder split (toe) pitch authority vs AoA, from the FCS's own surface model (same math as the aero job)
using System;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;
internal static class ToeSweep
{
    public static void Run(string dataPath, float iasKmh)
    {
        Console.WriteLine($"# {iasKmh:F0} km/h IAS, sea level, sideslip 0. Pitch accel deg/s^2 (+ nose up). 'pair' = both rudders at the same signed deflection (symmetric split).");
        Console.WriteLine(" AoA | pair +35  pair +20  pair -20  pair -35 | best up  best dn | rudder local AoA at 0 | stab full up / dn (pair) | TVC +-20 (thrust 1)");
        for (int a = -30; a <= 90; a += 5)
        {
            var A = new Airframe(dataPath);
            var P = new Plant(A);
            float tas = iasKmh / 3.6f;
            P.Init(0f, tas, a, a);
            P.throttle = 1f;
            P.CreateFcs();
            P.Step();
            var c = P.fcs;
            float k = c.IaInv[1, 1] * Mathf.Rad2Deg;
            var rl = c.surfaces.First(s => s.name.EndsWith("Rudder_L")); var rr = c.surfaces.First(s => s.name.EndsWith("Rudder_R"));
            float P0 = rl.HonestPitchAt(0f) + rr.HonestPitchAt(0f);
            float Pair(float d) => (rl.HonestPitchAt(d) + rr.HonestPitchAt(d) - P0) * k;
            float best = float.MinValue, worst = float.MaxValue;
            for (float d = -35; d <= 35; d += 1) { float v = Pair(d); best = Math.Max(best, v); worst = Math.Min(worst, v); }
            var sl = c.surfaces.First(s => s.name.EndsWith("Elevator_L")); var sr = c.surfaces.First(s => s.name.EndsWith("Elevator_R"));
            float s0 = sl.HonestPitchAt(0) + sr.HonestPitchAt(0);
            float sUp = float.MinValue, sDn = float.MaxValue;
            for (float d = sl.min; d <= sl.max; d += 1) { float v = (sl.HonestPitchAt(d) + sr.HonestPitchAt(d) - s0) * k; sUp = Math.Max(sUp, v); sDn = Math.Min(sDn, v); }
            float tv = (c.tvc.MAt(20f).y - c.tvc.MAt(0f).y) * k;
            Console.WriteLine($"{a,4} | {Pair(35),8:F1} {Pair(20),8:F1} {Pair(-20),8:F1} {Pair(-35),8:F1} | {best,7:F1} {worst,7:F1} | {rl.localAoANow,6:F1} | {sUp,7:F0} / {sDn,6:F0} | {tv,6:F0}");
        }
    }
}
