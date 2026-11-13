// 9 Nov 2026 what-if: static pitch accel vs stab and canard at fixed AoA, nozzles 0, full Plant forces (calibrated su30 defaults).
using System; using System.Collections.Generic; using System.Globalization; using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;
internal static class PGrid
{
    public static void Run(Dictionary<string,string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float alt = kv.TryGetValue("alt", out var a0s) ? float.Parse(a0s, IC) : 3000f;
        float ias = kv.TryGetValue("ias", out var i0) ? float.Parse(i0, IC) : 300f;
        float thr = kv.TryGetValue("thr", out var ts) ? float.Parse(ts, IC) : 1f;
        var P = IfritTest.Make(alt, ias, true, thr, out float tas); P.EvalOnly = true;
        Console.WriteLine("alpha,stab,canard,qdot,nz");
        foreach (float a in new[]{5f,10f,15f,20f,25f,30f,36f,40f,45f,50f})
        {
            float pos = Mathf.Clamp01((Mathf.Abs(a) - Plugin.SlatAoAStart) / Mathf.Max(1f, Plugin.SlatAoAFull - Plugin.SlatAoAStart));
            pos *= 1f - Mathf.Clamp01((Mathf.Abs(a) - (Plugin.HighLiftMaxAoA - 10f)) / 10f);
            foreach (var h in P.A.hlds) h.aeroPart.wingArea = h.partAreaRetracted + (h.partAreaDeployed - h.partAreaRetracted) * pos;
            for (float s = -30f; s <= 35.01f; s += 5f)
            foreach (float c in new[]{-45f,-30f,-15f,0f,15f,30f,45f,65f})
            {
                P.Init(alt, tas, a, a); P.w = Vector3.zero; P.Pose();
                var cf = new IfritTest.Cfg();
                foreach (var cs in P.A.surfaces) { var n = cs.name.ToLowerInvariant(); if (n.Contains("stab_struct") && !n.Contains("vert") || n.Contains("elevator")) cf.defl[cs.name] = s; else if (n.Contains("canard")) cf.defl[cs.name] = c; }
                IfritTest.Apply(P, cf); var acc = IfritTest.Acc(P);
                Console.WriteLine(string.Format(IC, "{0},{1},{2},{3:F2},{4:F2}", a, s, c, acc.y, IfritTest.Nz(P)));
            }
        }
    }
}
