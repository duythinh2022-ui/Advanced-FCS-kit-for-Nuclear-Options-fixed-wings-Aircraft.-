// 2.4.17 bench probe: effector moment samples as body angular accelerations (deg/s^2)
using System;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;
internal static class ProbeExt
{
    internal static void ProbeDump(this FcsController f)
    {
        Console.Error.WriteLine($"# probe alpha {f.alpha:F1} ias {f.ias:F1} aRoll {f.aRoll:F0} aYaw {f.aYaw:F0}");
        foreach (var e in f.effs)
        {
            if (!e.active || e.M == null) continue;
            string k = (e is SurfaceEffector se) ? se.kind.ToString() : "TVC";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < e.N; i++)
            {
                Vector3 m = e.M[i];
                float ax = (f.IaInv[0,0]*m.x + f.IaInv[0,2]*m.z) * 57.29578f, ay = f.IaInv[1,1]*m.y*57.29578f, az = (f.IaInv[2,2]*m.z + f.IaInv[2,0]*m.x) * 57.29578f;
                sb.Append($" [{(e.grid!=null?e.grid[i]:i):F0}: {ax:F0},{ay:F0},{az:F0}]");
            }
            Console.Error.WriteLine($"{e.name} {k} now {e.now:F1}:{sb}");
        }
    }
}
