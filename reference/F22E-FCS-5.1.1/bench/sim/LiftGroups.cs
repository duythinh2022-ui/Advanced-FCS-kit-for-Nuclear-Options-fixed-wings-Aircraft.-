using System;
using UnityEngine;
// 2.4.6: per-group lift area CL*S (m^2, lift-normal frame, symmetric flight, surfaces at rest) vs body AoA from the bench's
// own charts (the same StallStagger / BodyLift / LiftShape builds the game uses)
internal static class LiftGroups
{
    static string Grp(string n)
    {
        n = n.Replace("Aryx_KingRaptor", "");
        if (n.Contains("Elevator")) return "stab";
        if (n.Contains("Vstab") || n.Contains("Rudder")) return "fin";
        if (n.Contains("Aileron") || n.Contains("Flap")) return "flap";
        if (n.Contains("WingFore") || n.Contains("Wingmid") || n.Contains("WingRear") || n.Contains("Wingtip")) return "panel";
        return "fus";
    }
    public static void PerPart(string path, float a)
    {
        var A = new Airframe(path);
        float ar = a * Mathf.Deg2Rad; Vector3 v = new Vector3(0f, -Mathf.Sin(ar), Mathf.Cos(ar)); Vector3 e = new Vector3(0f, Mathf.Cos(ar), Mathf.Sin(ar));
        foreach (var b in A.bodies)
        {
            var ap = b.part; if (ap.airfoil < 0 || ap.wingArea <= 0f) continue;
            Quaternion rr = Quaternion.Inverse(A.root.rotation) * ap.liftNormal.rotation;
            Vector3 v6 = Quaternion.Inverse(rr) * v; Vector3 nrm = Vector3.Cross(v, rr * Vector3.right).normalized;
            float idx = Mathf.Atan2(v6.y, v6.z) * 20.37185f + 64f;
            float L = Vector3.Dot(-nrm * Chart(A.liftCharts[ap.ChartId], idx) * ap.wingArea * ap.wingEffectiveness, e);
            Console.WriteLine($"{ap.name},{Grp(ap.name)},{Aryx_F22E_StrikeRaptor.FCS.StallStagger.FamilyOf(ap.name)},{L:F3}");
        }
    }
    public static void Run(string path)
    {
        var A = new Airframe(path);
        Console.WriteLine("aoa,panel,flap,fus,stab,fin,dragPanel,dragFus");
        for (float a = -60f; a <= 70.01f; a += 0.5f)
        {
            float ar = a * Mathf.Deg2Rad;
            Vector3 v = new Vector3(0f, -Mathf.Sin(ar), Mathf.Cos(ar));
            Vector3 e = new Vector3(0f, Mathf.Cos(ar), Mathf.Sin(ar));
            float panel = 0, flap = 0, fus = 0, stab = 0, fin = 0, dp = 0, df = 0;
            foreach (var b in A.bodies)
            {
                var ap = b.part; if (ap.airfoil < 0 || ap.wingArea <= 0f) continue;
                var lt = ap.liftNormal;
                Quaternion rr = Quaternion.Inverse(A.root.rotation) * lt.rotation;
                Vector3 v6 = Quaternion.Inverse(rr) * v;
                Vector3 nrm = Vector3.Cross(v, rr * Vector3.right).normalized;
                float al = Mathf.Atan2(v6.y, v6.z);
                float idx = al * 20.37185f + 64f;
                float cl = Chart(A.liftCharts[ap.ChartId], idx), cd = Chart(A.dragCharts[ap.ChartId], idx);
                float L = Vector3.Dot(-nrm * cl * ap.wingArea * ap.wingEffectiveness, e);
                float D = cd * ap.wingArea * ap.wingEffectiveness;
                switch (Grp(ap.name)) { case "panel": panel += L; dp += D; break; case "flap": flap += L; break; case "fus": fus += L; df += D; break; case "stab": stab += L; break; default: fin += L; break; }
            }
            Console.WriteLine($"{a:F1},{panel:F3},{flap:F3},{fus:F3},{stab:F3},{fin:F3},{dp:F4},{df:F4}");
        }
    }
    static float Chart(float[] c, float idx) { idx = Mathf.Clamp(idx, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1); return Mathf.Lerp(c[i], c[j], idx - i); }
}
