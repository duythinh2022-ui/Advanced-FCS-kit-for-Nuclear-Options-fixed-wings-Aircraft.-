// 2.2.3: energy-manoeuvrability samples. The real FCS flies full-AB pulls at a set of stick positions from a set of
// start speeds; altitude is pinned (density constant) and every 0.05 s the state is sampled once the pull has settled:
//   n  = |non-gravity force normal to the velocity| / (m g)        (aero + thrust)
//   Ps = (non-gravity force along the velocity) . V / (m g)        (specific excess power, m/s)
//   level-turn rate = g sqrt(n^2 - 1) / V
// Gravity still acts on the trajectory (so the FCS flies normally); it only changes which speeds get sampled.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class EmTest
{
    public static void Run(string dataPath, Dictionary<string, string> kv, Func<Plant, float, float, (float a, float thr)> trim)
    {
        var IC = CultureInfo.InvariantCulture;
        float G(string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;
        float alt = G("alt", 3000f), thr = G("thr", 1f), tMax = G("tmax", 14f);
        var tasList = (kv.TryGetValue("tas", out var tl) ? tl : "150,200,250,300,350,400,450,500,550,600").Split(',').Select(x => float.Parse(x, IC)).ToArray();
        var stickList = (kv.TryGetValue("sticks", out var sl) ? sl : "0,0.15,0.3,0.45,0.6,0.8,1").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => float.Parse(x, IC)).ToArray();
        string outFn = kv.TryGetValue("out", out var of) ? of : "em.csv";
        Plant.GameTransonic = true;
        var sb = new StringBuilder("run,stick,t,tas,ias,mach,n,turn,ps,alpha,q,mass,thrust\n");
        int run = 0;
        float rho = Plant.Density(alt);
        var aoaList = (kv.TryGetValue("aoas", out var al) ? al : "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => float.Parse(x, IC)).ToList();
        var gList = (kv.TryGetValue("gs", out var gl) ? gl : "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => float.Parse(x, IC)).ToList();
        float aoa0 = Plugin.AoAPos, g0 = Plugin.GPos;
        var jobs = new List<(float v0, float st, float aoa, float g)>();
        foreach (var v0 in tasList) { foreach (var st in stickList) jobs.Add((v0, st, aoa0, g0)); foreach (var aa in aoaList) jobs.Add((v0, 1f, aa, g0)); foreach (var gg in gList) jobs.Add((v0, 1f, aoa0, gg)); }
        foreach (var (v0, st, aoaLim, gLim) in jobs)
        {
            Plugin.AoAPos = aoaLim; Plugin.GPos = gLim;
            run++;
            var A = new Airframe(dataPath);
            var P = new Plant(A);
            var tr = trim(P, alt, v0);
            P.Init(alt, v0, tr.a, tr.a);
            P.throttle = thr;
            P.CreateFcs();
            var inp = A.ac.inputs;
            float tPull = 0.5f;
            float aPrev = float.NaN;
            int k = 0;
            while (P.time < tMax)
            {
                float t = P.time;
                inp.pitch = t >= tPull ? -Mathf.Clamp01((t - tPull) / 0.3f) * st : 0f;
                inp.roll = 0f; inp.yaw = 0f;
                P.Step();
                P.pCg = new Vector3(P.pCg.x, alt, P.pCg.z);
                var c = P.fcs;
                float V = P.vCg.magnitude;
                float ias = V * Mathf.Sqrt(rho / 1.225f) * 3.6f;
                if (ias < 90f || Mathf.Abs(c.alpha) > 75f) break;
                k++;
                if (k % 3 != 0) continue;
                float adot = float.IsNaN(aPrev) ? 0f : (c.alpha - aPrev) / (3 * P.dt);
                aPrev = c.alpha;
                if (t < tPull + 0.8f || Mathf.Abs(adot) > 4f) continue;
                Vector3 F = P.lastForce - Physics.gravity * A.mass;
                Vector3 vh = P.vCg / V;
                float ft = Vector3.Dot(F, vh);
                float fn = (F - ft * vh).magnitude;
                float W = A.mass * 9.81f;
                float n = fn / W;
                float turn = n > 1f ? 9.81f * Mathf.Sqrt(n * n - 1f) / V * Mathf.Rad2Deg : 0f;
                float ps = ft * V / W;
                float mach = V / DragReliefMath.SpeedOfSound(alt);
                float thrust = A.engines.Sum(e => e.nozzles.Sum(nz => nz.totalThrust));
                sb.Append(string.Format(IC, "{0},{1}/{13}/{14},{2:F2},{3:F1},{4:F1},{5:F3},{6:F3},{7:F2},{8:F1},{9:F1},{10:F1},{11:F0},{12:F0}\n", run, st, t, V, ias, mach, n, turn, ps, c.alpha, c.omega.y * Mathf.Rad2Deg, A.mass, thrust, aoaLim, gLim));
            }
        }
        File.WriteAllText(outFn, sb.ToString());
    }
}

// 2.2.3 EM, quasi-static: speed AND altitude pinned. For each IAS, stick 0 (about 1 g) then full aft stick with the AoA
// limit stepped up through `stages`; each stage is held `hold` s and the last 0.4 s are averaged (n, Ps, AoA). The FCS flies
// every point as it would in a real turn (it rotates the jet at q = n g / V); only the speed is held.
internal static class EmQuasi
{
    public static void Run(string dataPath, Dictionary<string, string> kv, Func<Plant, float, float, (float a, float thr)> trim)
    {
        var IC = CultureInfo.InvariantCulture;
        float G(string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;
        float alt = G("alt", 3000f), thr = G("thr", 1f), hold = G("hold", 1.2f);
        var iasList = (kv.TryGetValue("iasl", out var il) ? il : "300").Split(',').Select(x => float.Parse(x, IC)).ToArray();
        var stages = (kv.TryGetValue("stages", out var sg) ? sg : "2,4,6,8,10,12,14,16,18,20,23,26,30,35,40,45,50,55,60,65").Split(',').Select(x => float.Parse(x, IC)).ToArray();
        string outFn = kv.TryGetValue("out", out var of) ? of : "emq.csv";
        Plant.GameTransonic = true;
        float rho = Plant.Density(alt), k = Mathf.Sqrt(rho / 1.225f);
        var sb = new StringBuilder("ias,tas,mach,stage,n,ps,alpha,mass\n");
        float aoa0 = Plugin.AoAPos;
        foreach (var ias in iasList)
        {
            float tas = ias / 3.6f / k;
            Plugin.AoAPos = aoa0;
            var A = new Airframe(dataPath);
            var P = new Plant(A);
            var tr = trim(P, alt, tas);
            P.Init(alt, tas, tr.a, tr.a);
            P.throttle = thr;
            P.CreateFcs();
            var inp = A.ac.inputs;
            var list = new List<float> { -1f }; list.AddRange(stages);
            foreach (var st in list)
            {
                if (st >= 0f) Plugin.AoAPos = st;
                double sn = 0, sp = 0, sa = 0; int cnt = 0;
                float t0 = P.time;
                while (P.time < t0 + hold)
                {
                    inp.pitch = st < 0f ? 0f : -1f; inp.roll = 0f; inp.yaw = 0f;
                    P.Step();
                    P.pCg = new Vector3(P.pCg.x, alt, P.pCg.z);
                    float V0 = P.vCg.magnitude; if (V0 > 1f) P.vCg = P.vCg * (tas / V0);
                    if (P.time >= t0 + hold - 0.4f)
                    {
                        Vector3 F = P.lastForce - Physics.gravity * A.mass;
                        Vector3 vh = P.vCg / P.vCg.magnitude;
                        float ft = Vector3.Dot(F, vh), fn = (F - ft * vh).magnitude, W = A.mass * 9.81f;
                        sn += fn / W; sp += ft * tas / W; sa += P.fcs.alpha; cnt++;
                    }
                }
                sb.Append(string.Format(IC, "{0:F0},{1:F1},{2:F3},{3:F0},{4:F3},{5:F1},{6:F1},{7:F0}\n", ias, tas, tas / DragReliefMath.SpeedOfSound(alt), st, sn / cnt, sp / cnt, sa / cnt, A.mass));
            }
        }
        Plugin.AoAPos = aoa0;
        File.WriteAllText(outFn, sb.ToString());
    }
}
