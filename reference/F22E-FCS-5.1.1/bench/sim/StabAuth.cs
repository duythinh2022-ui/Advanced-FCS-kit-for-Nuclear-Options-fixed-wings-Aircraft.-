// 4.6.0: stabilator lateral authority per body AoA. For each alpha, every (left, right) stab pair on a 1-deg grid over
// their travel; the rest of the airframe at rest (flaperons 0, rudders 0, no thrust). Reports the largest body roll
// acceleration whose pitch acceleration stays within tol of the stabs-neutral value, the yaw that pair makes, and the
// pair; plus the largest nose-down (both stabs together) relative to neutral.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class StabAuth
{
    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float G(string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;
        float ias = G("ias", 300f), a0 = G("a0", 0f), a1 = G("a1", 65f), da = G("da", 5f), tol = G("tol", 3f);
        var A = new Airframe(dataPath); var P = new Plant(A);
        float alt = 3000f, rho = Plant.Density(alt), tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        var stabs = A.surfaces.Where(c => c.name.Contains("Elevator")).ToList();
        var L = stabs.First(c => c.name.Contains("_L")); var R = stabs.First(c => c.name.Contains("_R"));
        float lo = -30f, hi = 35f;
        Console.WriteLine($"# stabauth ias {ias} downwash {Plugin.StabDownwash} plate {Plugin.StabPlateCurve} peak {Plugin.StabStallAoA} tol {tol}");
        Console.WriteLine("alpha  pNeutral  rollMax(deg/s2) yawAtIt  dL    dR   | roll@trimSplit10  | noseDownMax(dd) at d");
        for (float a = a0; a <= a1 + 1e-3f; a += da)
        {
            P.Init(alt, tas, a, a);
            Vector3 M(float dl, float dr)
            {
                Vector3 m = Vector3.zero;
                foreach (var cs in new[] { L, R })
                {
                    float d = cs == L ? dl : dr;
                    cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
                    var ap = (AeroPart)cs.attachedSurface; var b = A.bodies.First(x => x.part == ap);
                    Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude; var lt = ap.liftNormal;
                    Vector3 vp = v; if (Downwash.IsStabName(ap.name)) v = Downwash.Apply(v, P.q, P.vCg);
                    Vector3 v6 = Quaternion.Inverse(lt.rotation) * v; Vector3 nrm = Vector3.Cross(v, lt.right).normalized;
                    float al = Mathf.Atan2(v6.y, v6.z);
                    float idx = Mathf.Clamp(al * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1);
                    float cl = Mathf.Lerp(A.liftCharts[ap.ChartId][i], A.liftCharts[ap.ChartId][j], idx - i), cd = Mathf.Lerp(A.dragCharts[ap.ChartId][i], A.dragCharts[ap.ChartId][j], idx - i);
                    float eff = ap.wingEffectiveness;
                    Vector3 fl = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * eff), fd = -v.normalized * (cd * rho * v2 * 0.5f * ap.wingArea * eff);
                    Vector3 r = b.rb.worldCenterOfMass - P.pCg + (ap.centerOfLift != Vector3.zero ? lt.rotation * ap.centerOfLift : Vector3.zero);
                    m += Vector3.Cross(r, fl + fd);
                    cs.visibleMesh.transform.localRotation = cs.job.restingRotation;
                }
                Vector3 body = Quaternion.Inverse(P.q) * m;
                // body axes: x right, y up, z forward -> pitch about x, yaw about y, roll about z (deg/s^2, nose-up / right yaw / right roll)
                return new Vector3((float)(-body.x / A.Itot[0, 0] * 57.2958), (float)(body.y / A.Itot[1, 1] * 57.2958), (float)(-body.z / A.Itot[2, 2] * 57.2958));
            }
            var m0 = M(0, 0);
            float best = 0f, by = 0f, bl = 0f, br = 0f;
            for (float dl = lo; dl <= hi; dl += 1f)
                for (float dr = lo; dr <= hi; dr += 1f)
                {
                    var m = M(dl, dr);
                    if (Mathf.Abs(m.x - m0.x) > tol) continue;
                    float roll = m.z - m0.z;
                    if (roll > best) { best = roll; by = m.y - m0.y; bl = dl; br = dr; }
                }
            var ms = M(10, -10);
            float nd = 0f, ndd = 0f;
            for (float d = lo; d <= hi; d += 1f) { var m = M(d, d); if (m.x - m0.x < nd) { nd = m.x - m0.x; ndd = d; } }
            Console.WriteLine(string.Format(IC, "{0,5:F1} {1,8:F1}   {2,8:F1}   {3,7:F1}  {4,5:F0} {5,5:F0} | {6,8:F1}          | {7,8:F1} at {8,4:F0}", a, m0.x, best, by, bl, br, ms.z - m0.z, nd, ndd));
        }
    }
}
