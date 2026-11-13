using System;
using System.Linq;
using UnityEngine;
internal static class StallDiag
{
    // local AoA of each control surface vs body AoA and deflection (no rates, no sideslip)
    public static void Run(string path)
    {
        var A = new Airframe(path);
        var P = new Plant(A);
        float alt = 3000f, tas = 150f;
        Console.WriteLine("surface,alpha,delta,localAoA,CL,CD");
        foreach (var a in Enumerable.Range(-9, 40).Select(i => i * 2.5f))
        {
            P.Init(alt, tas, a, a);
            foreach (var cs in A.surfaces)
            {
                if (cs.name.Contains("Rudder")) continue;
                float R = Math.Abs(cs.pitchRange) + Math.Abs(cs.rollRange) + Math.Abs(cs.yawRange);
                var ap = (AeroPart)cs.attachedSurface; var b = A.bodies.First(x => x.part == ap);
                for (float d = -R; d <= R + 0.01f; d += 1f)
                {
                    cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
                    Vector3 v = b.rb.velocity; var lt = ap.liftNormal;
                    Vector3 v6 = Quaternion.Inverse(lt.rotation) * v;
                    float al = Mathf.Atan2(v6.y, v6.z);
                    float idx = Mathf.Clamp(al * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1);
                    float cl = Mathf.Lerp(A.liftCharts[ap.ChartId][i], A.liftCharts[ap.ChartId][j], idx - i);
                    float cd = Mathf.Lerp(A.dragCharts[ap.ChartId][i], A.dragCharts[ap.ChartId][j], idx - i);
                    Console.WriteLine($"{cs.name.Replace("Aryx_KingRaptor_", "")},{a},{d},{al * 57.29578f:F2},{cl:F3},{cd:F3}");
                }
                cs.visibleMesh.transform.localRotation = cs.job.restingRotation;
            }
        }
    }
}
