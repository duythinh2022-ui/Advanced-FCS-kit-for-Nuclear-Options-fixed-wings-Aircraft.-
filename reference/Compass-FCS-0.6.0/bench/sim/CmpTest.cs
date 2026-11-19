// T/A-30 Compass (NO "trainer"): static authority / stability maps (deg/s^2) at fixed poses, open loop.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class CmpTest
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Set(Plant P, Dictionary<string, float> d)
    {
        foreach (var cs in P.A.surfaces)
        {
            float x = d.TryGetValue(cs.name, out var v) ? v : 0f;
            cs.job.currentPitch = x; cs.job.currentRoll = 0; cs.job.currentYaw = 0;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(x, Vector3.right);
        }
    }
    static Dictionary<string, float> D(params (string, float)[] a) { var d = new Dictionary<string, float>(); foreach (var (k, v) in a) d[k] = v; return d; }
    public static void Auth(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), thr = F(kv, "thr", 1f), el = F(kv, "el", 40f);
        var iasList = (kv.TryGetValue("iasl", out var il) ? il : "250,400,600").Split(',').Select(x => float.Parse(x, IC));
        float a0 = F(kv, "a0", -30f), a1 = F(kv, "a1", 70f), da = F(kv, "da", 5f);
        foreach (var iasK in iasList)
        {
            var P = IfritTest.Make(alt, iasK, true, thr, out float tas); P.EvalOnly = true;
            Console.WriteLine("# " + P.A.CgShiftLog); Console.WriteLine($"\n# IAS {iasK:F0} km/h TAS {tas * 3.6f:F0} alt {alt:F0} thr {thr:F2} mass {P.A.mass:F0} kg cg(body) {P.A.cgB.x:F3},{P.A.cgB.y:F3},{P.A.cgB.z:F3}  I(p,q,r) {P.A.Itot[2, 2]:F0},{P.A.Itot[0, 0]:F0},{P.A.Itot[1, 1]:F0}");
            Console.WriteLine($"# pitch q-dot: q0 all neutral | eA = elevators +{el} | eB = elevators -{el} (one of them is nose-down) ; nz0 at q0");
            Console.WriteLine("# lateral, stability axes pS / rS (deg/s^2), deltas vs neutral: AIL ail +-30 | ELD elevators diff +-10 | EL30 diff +-30 | RUD both rudders 25 | ALL ail30+eld10");
            Console.WriteLine($"{"a",5} {"nz0",5} {"q0",6} {"eA",6} {"eB",6} | {"AILp",6} {"r",5} | {"ELDp",6} {"r",5} | {"EL30p",6} {"r",5} | {"RUDp",6} {"r",5} | {"ALLp",6} {"r",5}");
            for (float a = a0; a <= a1 + 1e-3f; a += da)
            {
                P.Init(alt, tas, a, a);
                Vector3 Q(Dictionary<string, float> d) { Set(P, d); return IfritTest.Acc(P); }
                var q0 = Q(D()); float nz = IfritTest.Nz(P);
                var eA = Q(D(("elevator_L", el), ("elevator_R", el)));
                var eB = Q(D(("elevator_L", -el), ("elevator_R", -el)));
                var ail = Q(D(("aileron_L", -30), ("aileron_R", 30))) - q0;
                var eld = Q(D(("elevator_L", 10), ("elevator_R", -10))) - q0;
                var el30 = Q(D(("elevator_L", 30), ("elevator_R", -30))) - q0;
                var rud = Q(D(("rudder_L", 25), ("rudder_R", 25))) - q0;
                var all = Q(D(("aileron_L", -30), ("aileron_R", 30), ("elevator_L", 10), ("elevator_R", -10))) - q0;
                string L(Vector3 d) { float ar = a * Mathf.Deg2Rad; float pS = d.x * Mathf.Cos(ar) + d.z * Mathf.Sin(ar); float rS = d.z * Mathf.Cos(ar) - d.x * Mathf.Sin(ar); return $"{pS,6:F0} {rS,5:F0}"; }
                Console.WriteLine($"{a,5:F1} {nz,5:F2} {q0.y,6:F1} {eA.y,6:F0} {eB.y,6:F0} | {L(ail)} | {L(eld)} | {L(el30)} | {L(rud)} | {L(all)}");
            }
        }
    }

    // static margin and pitch trim: SM (m, + = stable) = -(dM/da) / (dL/da) at neutral surfaces; trim = the symmetric elevator
    // that zeroes q-dot (bisection, +-el); dn / up = q-dot at full nose-down / nose-up elevator
    public static void Margin(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), thr = F(kv, "thr", 1f), el = F(kv, "el", 40f);
        var iasList = (kv.TryGetValue("iasl", out var il) ? il : "300,500").Split(',').Select(x => float.Parse(x, IC));
        float a0 = F(kv, "a0", -20f), a1 = F(kv, "a1", 60f), da = F(kv, "da", 5f);
        foreach (var iasK in iasList)
        {
            var P = IfritTest.Make(alt, iasK, true, thr, out float tas); P.EvalOnly = true;
            float Iyy = (float)P.A.Itot[0, 0], m = P.A.mass;
            // which sign of elevator is nose-up
            P.Init(alt, tas, 5f, 5f); Set(P, D(("elevator_L", 10f), ("elevator_R", 10f))); float qp = IfritTest.Acc(P).y; Set(P, D()); float qz = IfritTest.Acc(P).y;
            float upS = qp > qz ? 1f : -1f;
            Console.WriteLine($"# {P.A.CgShiftLog} | downwash k {Plugin.StabDownwash:F2} {(Plugin.DownwashLiftDriven ? "lift" : "linear")} | IAS {iasK:F0} mass {m:F0} cgB.z {P.A.cgB.z:F3} Iyy {Iyy:F0} (nose-up elevator sign {upS:+0;-0})");
            Console.WriteLine($"{"a",5} {"nz",5} {"q0",7} {"SM_m",6} {"trim",6} {"qDn",7} {"qUp",7}");
            for (float a = a0; a <= a1 + 1e-3f; a += da)
            {
                Dictionary<string, float> X(float e) { var d = D(("elevator_L", e), ("elevator_R", e)); if (kv.TryGetValue("extra", out var ex)) foreach (var it in ex.Split(',')) { var pp = it.Split(':'); d[pp[0]] = float.Parse(pp[1], IC); } return d; }
                float QA(float aa, float e) { P.Init(alt, tas, aa, aa); Set(P, X(e)); return IfritTest.Acc(P).y; }
                float NZ(float aa) { P.Init(alt, tas, aa, aa); Set(P, X(0f)); IfritTest.Acc(P); return IfritTest.Nz(P); }
                float q0 = QA(a, 0f); float nz = NZ(a);
                float dq = (QA(a + 0.5f, 0f) - QA(a - 0.5f, 0f)) / 1f;            // deg/s^2 per deg
                float dn = (NZ(a + 0.5f) - NZ(a - 0.5f)) / 1f;                   // g per deg
                float dM = dq * Iyy, dL = dn * m * 9.81f;                        // N m per deg (rad/s^2 * I: both per deg, ratio is m)
                float sm = Mathf.Abs(dL) > 1f ? -(dM * Mathf.Deg2Rad) / dL : float.NaN;
                // trim: bisection on e in [-el, el] for q-dot = 0
                float lo = -el, hi = el, flo = QA(a, lo), fhi = QA(a, hi); string trim;
                if (flo * fhi > 0f) trim = (Mathf.Abs(flo) < Mathf.Abs(fhi) ? "<" : ">") + "lim";
                else { for (int k = 0; k < 30; k++) { float mid = 0.5f * (lo + hi), fm = QA(a, mid); if (fm * flo > 0f) { lo = mid; flo = fm; } else hi = mid; } trim = (0.5f * (lo + hi) * upS).ToString("F1", IC); }
                float qDn = QA(a, -el * upS), qUp = QA(a, el * upS);
                Console.WriteLine($"{a,5:F1} {nz,5:F2} {q0,7:F1} {sm,6:F2} {trim,6} {qDn,7:F0} {qUp,7:F0}");
            }
        }
    }
    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
}
internal static class CmpParts
{
    // per-part pitch moment (as body pitch acceleration, deg/s^2) at neutral surfaces, grouped
    public static void Run(Dictionary<string, string> kv)
    {
        var IC = System.Globalization.CultureInfo.InvariantCulture;
        float alt = 3000f, ias = kv.TryGetValue("ias", out var s) ? float.Parse(s, IC) : 300f;
        var P = IfritTest.Make(alt, ias, true, 1f, out float tas); P.EvalOnly = true;
        string G(string n) { n = n.ToLowerInvariant(); if (n.StartsWith("elevator")) return "elev"; if (n.StartsWith("hstab")) return "hstab"; if (n.StartsWith("wingroot1")) return "strake"; if (n.StartsWith("wing") || n.StartsWith("aileron") || n.StartsWith("flap")) return "wing"; if (n.StartsWith("vstab") || n.StartsWith("rudder")) return "fins"; if (n.StartsWith("intake") || n.StartsWith("cockpit") || n == "nose") return "forebody"; return "aftbody"; }
        var groups = new[] { "wing", "strake", "forebody", "aftbody", "elev", "hstab", "fins" };
        Console.WriteLine($"# {P.A.CgShiftLog} IAS {ias} | per-group pitch accel (deg/s^2, + nose up), elevators 0; 'thrust' = rest");
        Console.WriteLine($"{"a",4} " + string.Join(" ", groups.Select(g => $"{g,8}")) + $" {"total",8} {"aero",8}");
        foreach (var a in new[] { 0f, 5f, 10f, 15f, 20f, 25f, 30f, 35f, 40f })
        {
            P.Init(alt, tas, a, a);
            foreach (var cs in P.A.surfaces) { cs.job.currentPitch = 0; cs.visibleMesh.transform.localRotation = cs.job.restingRotation; }
            P.PartDump = new Dictionary<AeroPart, (Vector3, Vector3, float, float)>();
            var tot = IfritTest.Acc(P);
            float Iyy = (float)P.A.Itot[0, 0];
            var sum = groups.ToDictionary(g => g, g => 0f);
            foreach (var kvp in P.PartDump) { var mb = Quaternion.Inverse(P.q) * kvp.Value.Item2; sum[G(kvp.Key.name)] += -mb.x / Iyy * Mathf.Rad2Deg; }
            float aero = sum.Values.Sum();
            Console.WriteLine($"{a,4:F0} " + string.Join(" ", groups.Select(g => $"{sum[g],8:F1}")) + $" {tot.y,8:F1} {aero,8:F1}");
        }
    }
}
