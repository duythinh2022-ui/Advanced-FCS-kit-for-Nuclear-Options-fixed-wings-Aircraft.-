// Ground contact for take-off tests: three spring-damper legs at the gear's unsprung points, ground plane y = 0.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class Ground
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static float StaticSink;
    public static float K = 900000f, C = 60000f, Travel = 0.7f, Mu = 0.8f, Roll = 0.015f;

    static float G(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;

    public static void Takeoff(string dataPath, Dictionary<string, string> kv)
    {
        float v0 = G(kv, "v0", 200f) / 3.6f, T = G(kv, "T", 10f), thr = G(kv, "throttle", 1f);
        string prof = kv.TryGetValue("prof", out var pp) ? pp : "1:0,1.3:1,20:1";
        string csv = kv.TryGetValue("csv", out var cc) ? cc : null;
        float contactY = G(kv, "contactY", -1.75f);
        var A = new Airframe(dataPath);
        A.ac.gearDeployed = true;
        var P = new Plant(A) { gyro = Gyro.Unity };
        // gear legs: contact point in root frame (x of the unsprung point, fixed contact height)
        var legs = new List<(LandingGear g, Vector3 pB)>();
        foreach (var c in A.ac.all) if (c is LandingGear lg)
        {
            var un = lg.transform.children.FirstOrDefault(t => t.name.Contains("Unsprung"));
            var p = (un != null ? un.position : lg.transform.position);
            legs.Add((lg, new Vector3(p.x, contactY, p.z)));
        }
        // static balance: nose / main loads from the CG position, the nose contact raised so all legs sit at their static
        // compression with the fuselage level
        {
            var nz = legs.OrderByDescending(l => l.pB.z).First();
            float zm = legs.Where(l => l.g != nz.g).Average(l => l.pB.z), zn = nz.pB.z, zc = A.cgB.z;
            float W = A.mass * 9.81f, Nn = W * (zc - zm) / (zn - zm), Nm = (W - Nn) / 2f;
            float cn = Nn / K, cm = Nm / K;
            for (int i = 0; i < legs.Count; i++) if (legs[i].g == nz.g) legs[i] = (legs[i].g, legs[i].pB + Vector3.up * (cm - cn));
            StaticSink = cm;
            Console.WriteLine($"# static legs: nose {Nn / 1000f:F1} kN ({cn * 1000f:F0} mm), mains {Nm / 1000f:F1} kN each ({cm * 1000f:F0} mm)");
        }
        // static sink: CG height so the legs carry the weight (solve vertical + pitch balance roughly by iteration)
        P.Init(0f, v0, 0f, 0f);
        float h = -contactY + A.cgB.y - StaticSink;
        float th0 = G(kv, "theta0", 0f), h0 = G(kv, "h0", 0f), vz0 = G(kv, "vz0", 0f);
        P.q = Quaternion.Euler(-th0, 0f, 0f);
        // raise so the mains (behind the CG) stay at the same height when pitched up
        var mainB = legs.OrderBy(l => l.pB.z).First().pB;
        float dyMain = (P.q * (mainB - A.cgB)).y - (mainB - A.cgB).y;
        P.pCg = new Vector3(0f, h - dyMain + h0, 0f); P.vCg = new Vector3(0f, vz0, v0);
        P.throttle = thr;
        P.ext = pl =>
        {
            Vector3 F = Vector3.zero, M = Vector3.zero;
            foreach (var (g, pB) in legs)
            {
                Vector3 pw = pl.pCg + pl.q * (pB - A.cgB);
                float pen = -pw.y;
                g.compression = Mathf.Clamp(pen, 0f, Travel);
                if (pen <= 0f) continue;
                Vector3 vc = pl.vCg + Vector3.Cross(pl.w, pw - pl.pCg);
                float N = Mathf.Max(0f, K * pen - C * vc.y);
                if (pen > Travel) N += 20f * K * (pen - Travel);
                Vector3 fwd = pl.q * Vector3.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                float vf = Vector3.Dot(vc, fwd), vs = Vector3.Dot(vc, side);
                Vector3 f = Vector3.up * N - fwd * Roll * N * Mathf.Clamp(vf / 0.5f, -1f, 1f) - side * Mu * N * Mathf.Clamp(vs / 0.3f, -1f, 1f);
                F += f; M += Vector3.Cross(pw - pl.pCg, f);
            }
            return (F, M);
        };
        P.CreateFcs();
        var inp = A.ac.inputs;
        var pts = prof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray();
        float Stick(float t) { if (t < pts[0].Item1) return 0f; for (int k = 0; k < pts.Length - 1; k++) if (t >= pts[k].Item1 && t < pts[k + 1].Item1) return pts[k].Item2 + (pts[k + 1].Item2 - pts[k].Item2) * (t - pts[k].Item1) / Math.Max(1e-3f, pts[k + 1].Item1 - pts[k].Item1); return pts[pts.Length - 1].Item2; }
        StreamWriter w = csv != null ? new StreamWriter(csv) : null;
        w?.WriteLine("t,ias,mode,theta,alpha,q,qT,qCmd,sp,nose,mainL,mainR,tvc,stabL,stabR,tvcd,phi,p,tailH,nz,alt");
        float tNoseOff = -1, tMainsOff = -1, qMax = -99, thetaMax = -99, tailMin = 99, qOverMax = -99;
        int lastMode = -1; string modes = "";
        var nose = legs.OrderByDescending(l => l.pB.z).First().g;
        Vector3 tailB = new Vector3(0f, -0.3f, -6.6f);
        while (P.time < T)
        {
            float t = P.time;
            float sp = Stick(t);
            inp.pitch = -sp; inp.roll = 0f; inp.yaw = 0f;
            P.Step();
            var c = P.fcs;
            var rates = c.omega * Mathf.Rad2Deg;
            bool noseOn = nose.compression > 0f;
            bool mainsOn = legs.Any(l => l.g != nose && l.g.compression > 0f);
            if (tNoseOff < 0 && !noseOn && t > 0.5f) tNoseOff = t;
            if (tMainsOff < 0 && !mainsOn && t > 0.5f) tMainsOff = t;
            if ((int)c.Mode != lastMode) { modes += $"{t:F2}:{c.Mode} "; lastMode = (int)c.Mode; }
            if (tNoseOff >= 0 && t < tNoseOff + 4f) { qMax = Mathf.Max(qMax, rates.y); qOverMax = Mathf.Max(qOverMax, rates.y - Mathf.Max(c.qT, 0f)); }
            thetaMax = Mathf.Max(thetaMax, c.theta);
            float tailH = (P.pCg + P.q * (tailB - A.cgB)).y;
            if (mainsOn) tailMin = Mathf.Min(tailMin, tailH);
            SurfaceEffector S(string n) => c.surfaces.FirstOrDefault(s => s.name == n) ?? c.surfaces[0];
            w?.WriteLine(string.Join(",", new object[] { t, c.ias * 3.6f, (int)c.Mode, c.theta, c.alpha, rates.y, c.qT, c.qCmd, sp, nose.compression, legs[0].g.compression, legs[1].g.compression, c.tvc.now, S("Elevator_L").now, S("Elevator_R").now, (c.tvcd != null ? c.tvcd.now : 0f), c.phi, rates.x, tailH, c.nz, P.pCg.y }.Select(x => Convert.ToSingle(x).ToString("0.###", IC))));
            if (float.IsNaN(c.alpha)) break;
        }
        w?.Close();
        Console.WriteLine($"takeoff v0 {v0 * 3.6f:F0} prof {prof}: noseOff {tNoseOff:F2}s mainsOff {tMainsOff:F2}s | q max {qMax:F1} (over qT by {qOverMax:F1}) within 4 s of nose-off | theta max {thetaMax:F1} | tail clearance on mains {tailMin:F2} m | modes {modes}");
    }
}
