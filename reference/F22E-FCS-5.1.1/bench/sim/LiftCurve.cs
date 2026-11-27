using System;
using UnityEngine;
internal static class LiftCurve
{
    // total aero lift (all parts, surfaces at rest) vs body AoA, as n at the given IAS (thrust not counted)
    public static void Run(string path, float iasKmh)
    {
        var A = new Airframe(path);
        var P = new Plant(A);
        float alt = 3000f; float rho = Plant.Density(alt);
        float tas = iasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f);
        for (float a = -60f; a <= 70.01f; a += 2.5f)
        {
            P.Init(alt, tas, a, a);
            float L = 0f;
            foreach (var b in A.bodies)
            {
                var ap = b.part; Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude;
                var lt = ap.liftNormal; Vector3 v6 = Quaternion.Inverse(lt.rotation) * v;
                Vector3 nrm = Vector3.Cross(v, lt.right).normalized; float al = Mathf.Atan2(v6.y, v6.z);
                float cl = ap.airfoil < 0 ? 1.8f * Mathf.Sin(5f * al) : Chart(A.liftCharts[ap.ChartId], al * 20.37185f + 64f);
                Vector3 lift = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * ap.wingEffectiveness);
                L += lift.y;
            }
            Console.WriteLine($"{a,6:F1} n {L / (A.mass * 9.81f),6:F2}");
        }
    }
    static float Chart(float[] c, float idx) { idx = Mathf.Clamp(idx, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1); return Mathf.Lerp(c[i], c[j], idx - i); }
}
