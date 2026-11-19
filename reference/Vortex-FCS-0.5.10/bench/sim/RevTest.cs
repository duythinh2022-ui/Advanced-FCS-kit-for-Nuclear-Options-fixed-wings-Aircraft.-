// FS-12 Revoker (NO Fighter1): static authority maps (deg/s^2) at trimmed-free poses.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class RevTest
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public sealed class Cfg { public Dictionary<string, float> d = new Dictionary<string, float>(); public float tvcP, tvcY; }
    static void Apply(Plant P, Cfg c)
    {
        foreach (var cs in P.A.surfaces)
        {
            float d = c.d.TryGetValue(cs.name, out var x) ? x : 0f;
            cs.job.currentPitch = d; cs.job.currentRoll = 0; cs.job.currentYaw = 0;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
            cs.splitAmount = 0f;
        }
        P.TvcAngleOverride = (tf, n) => c.tvcP; P.TvcYawOverride = (tf, n) => c.tvcY;
    }
    public static void Auth(Dictionary<string, string> kv)
    {
        float alt = F(kv, "alt", 3000f), thr = F(kv, "thr", 1f);
        var iasList = (kv.TryGetValue("iasl", out var il) ? il : "250,400,600,900").Split(',').Select(x => float.Parse(x, IC));
        float a0 = F(kv, "a0", -40f), a1 = F(kv, "a1", 90f), da = F(kv, "da", 5f);
        foreach (var iasK in iasList)
        {
            var P = IfritTest.Make(alt, iasK, true, thr, out float tas); P.EvalOnly = true;
            Console.WriteLine($"\n# IAS {iasK:F0} km/h TAS {tas*3.6f:F0} alt {alt:F0} thr {thr:F2} mass {P.A.mass:F0}");
            Console.WriteLine("# pitch (q dot): q0 neutral | canards cD=-35 cU=+35 | TVC tD=-15 tU=+15 | elevons eD (TE up, nose up?) sign: ail sym +30 / -30 | all-down / all-up");
            Console.WriteLine("# lateral stability-axis (pS rS): AIL = ail +-30 roll right | CAN = canards diff +-35 | RUD = rudder 25 right | TVY = yaw TVC 15 right ; q0p q0r = neutral p/r");
            Console.WriteLine($"{"a",4} {"nz",5} {"q0",6} {"cD",6} {"cU",6} {"tD",6} {"tU",6} {"e+",6} {"e-",6} {"dnAll",6} {"upAll",6} | {"AILp",6} {"r",5} | {"CANp",6} {"r",5} | {"RUDp",6} {"r",5} | {"TVYp",6} {"r",5}");
            for (float a = a0; a <= a1 + 1e-3f; a += da)
            {
                P.Init(alt, tas, a, a);
                Vector3 Q(Cfg c) { Apply(P, c); return IfritTest.Acc(P); }
                var c0 = new Cfg(); var q0 = Q(c0); float nz = IfritTest.Nz(P);
                Cfg C(float can, float tv, float ail) { var c = new Cfg(); c.d["canard_L"] = can; c.d["canard_R"] = can; c.d["aileron_L"] = ail; c.d["aileron_R"] = ail; c.tvcP = tv; return c; }
                var cD = Q(C(-35, 0, 0)); var cU = Q(C(35, 0, 0)); var tD = Q(C(0, -15, 0)); var tU = Q(C(0, 15, 0));
                var eP = Q(C(0, 0, 30)); var eM = Q(C(0, 0, -30));
                var dn = Q(C(-35, -15, 0)); var up = Q(C(35, 15, 0));
                var ca = new Cfg(); ca.d["aileron_R"] = 30; ca.d["aileron_L"] = -30; var ail = Q(ca) - q0;
                var cc = new Cfg(); cc.d["canard_R"] = 35; cc.d["canard_L"] = -35; var can = Q(cc) - q0;
                var cr = new Cfg(); cr.d["rudder"] = 25; var rud = Q(cr) - q0;
                var cy = new Cfg(); cy.tvcY = 15; var tvy = Q(cy) - q0;
                string L(Vector3 d) { float ar = a * Mathf.Deg2Rad; float pS = d.x * Mathf.Cos(ar) + d.z * Mathf.Sin(ar); float rS = d.z * Mathf.Cos(ar) - d.x * Mathf.Sin(ar); return $"{pS,6:F0} {rS,5:F0}"; }
                Console.WriteLine($"{a,4:F0} {nz,5:F2} {q0.y,6:F0} {cD.y,6:F0} {cU.y,6:F0} {tD.y,6:F0} {tU.y,6:F0} {eP.y,6:F0} {eM.y,6:F0} {dn.y,6:F0} {up.y,6:F0} | {L(ail)} | {L(can)} | {L(rud)} | {L(tvy)}");
            }
        }
    }
    static float F(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, IC) : d;
}

internal static class RevHinge
{
    // model vs plant for the inboard elevons (HingeFlapEffector) and the ailerons at fixed poses
    public static void Check(Dictionary<string, string> kv)
    {
        var IC = System.Globalization.CultureInfo.InvariantCulture;
        float alt = 3000f;
        foreach (var iasK in new[] { 300f, 500f })
        {
            var P = IfritTest.Make(alt, iasK, true, 1f, out float tas);
            P.CreateFcs(); P.EvalOnly = true;
            var fc = P.fcs;
            {
                var rootT = ((Component)(object)fc.ac).transform;
                P.Init(alt, tas, 5f, 5f);
                string geo = string.Join(" ", fc.surfaces.Select(se => $"{se.name}:{se.GeoTeUp(rootT)}")) + " | " + string.Join(" ", fc.hinges.Select(h => $"{h.name}:{h.GeoTeUp(rootT)}"));
                var ctx0 = new EvalContext { cg = P.pCg, rootInv = Quaternion.Inverse(P.q), rho = Plant.Density(alt), wind = Vector3.zero, vAir = P.vCg };
                foreach (var se in fc.surfaces) se.Evaluate(ref ctx0);
                foreach (var h in fc.hinges) h.Evaluate(ref ctx0);
                string aer = string.Join(" ", fc.surfaces.Select(se => $"{se.name}:{se.teUp}")) + " | " + string.Join(" ", fc.hinges.Select(h => $"{h.name}:{h.teUp}"));
                Console.WriteLine("# GEO teUp  " + geo + "\n# AERO teUp " + aer);
            }
            Console.WriteLine($"\n# IAS {iasK} hinges {fc.hinges.Count}: pitch / roll / yaw accel deg/s^2 for +20 deg on BOTH (sym) and L only, plant vs model");
            foreach (var a in new[] { 0f, 10f, 20f, 30f, 45f, 60f })
            {
                P.Init(alt, tas, a, a);
                foreach (var h in fc.hinges) h.Drive(0f, 1f);
                var a0 = IfritTest.Acc(P);
                var ctx = new EvalContext { cg = P.pCg + P.q * P.A.cgB * 0f, rootInv = Quaternion.Inverse(P.q), rho = Plant.Density(alt), wind = Vector3.zero, vAir = P.vCg };
                // FCS cg: mass-weighted rigidbody COM (Plant poses bodies)
                Vector3 cg = Vector3.zero; float m = 0f; foreach (var b in P.A.bodies) { cg += b.rb.worldCenterOfMass * b.part.mass; m += b.part.mass; } ctx.cg = cg / m;
                Vector3 Mod(float dL, float dR)
                {
                    Vector3 s = Vector3.zero;
                    foreach (var h in fc.hinges) { h.Evaluate(ref ctx); s += h.MAt(h.left ? dL : dR) - h.MAt(0f); }
                    var Mr = new Vector3(-s.y, s.z, -s.x);   // aero (roll, pitch, yaw) -> root frame (ToAero inverse)
                    var A3 = P.A.Itot; double det = A3[0,0]*(A3[1,1]*A3[2,2]-A3[1,2]*A3[2,1]) - A3[0,1]*(A3[1,0]*A3[2,2]-A3[1,2]*A3[2,0]) + A3[0,2]*(A3[1,0]*A3[2,1]-A3[1,1]*A3[2,0]);
                    double[,] iv = new double[3,3];
                    iv[0,0]=(A3[1,1]*A3[2,2]-A3[1,2]*A3[2,1])/det; iv[0,1]=(A3[0,2]*A3[2,1]-A3[0,1]*A3[2,2])/det; iv[0,2]=(A3[0,1]*A3[1,2]-A3[0,2]*A3[1,1])/det;
                    iv[1,0]=(A3[1,2]*A3[2,0]-A3[1,0]*A3[2,2])/det; iv[1,1]=(A3[0,0]*A3[2,2]-A3[0,2]*A3[2,0])/det; iv[1,2]=(A3[0,2]*A3[1,0]-A3[0,0]*A3[1,2])/det;
                    iv[2,0]=(A3[1,0]*A3[2,1]-A3[1,1]*A3[2,0])/det; iv[2,1]=(A3[0,1]*A3[2,0]-A3[0,0]*A3[2,1])/det; iv[2,2]=(A3[0,0]*A3[1,1]-A3[0,1]*A3[1,0])/det;
                    var ac3 = new Vector3((float)(iv[0,0]*Mr.x+iv[0,1]*Mr.y+iv[0,2]*Mr.z), (float)(iv[1,0]*Mr.x+iv[1,1]*Mr.y+iv[1,2]*Mr.z), (float)(iv[2,0]*Mr.x+iv[2,1]*Mr.y+iv[2,2]*Mr.z));
                    return new Vector3(-ac3.z, -ac3.x, ac3.y) * Mathf.Rad2Deg;
                }
                var ms = Mod(20, 20); var ml = Mod(20, 0);
                if (false) { var h0 = fc.hinges[0]; Console.WriteLine($"   dbg valid {h0.Valid} active {h0.active} rb {(h0.part.rb != null)} v {(h0.part.rb != null ? h0.part.rb.velocity.magnitude : -1f):F1} foil {(h0.foil != null)} N {h0.N} M0 {h0.M[0]} M15 {h0.M[15]} M30 {h0.M[30]} MAt20 {h0.MAt(20f)} MAt0 {h0.MAt(0f)} min {h0.min} max {h0.max} step {h0.Step} wingArea {Acc.AP_wingArea(h0.part)} eff {Acc.AP_wingEff(h0.part)}"); }
                foreach (var h in fc.hinges) h.Drive(20f, 1f);
                var a1 = IfritTest.Acc(P) - a0;
                foreach (var h in fc.hinges) h.Drive(h.left ? 20f : 0f, 1f);
                var a2 = IfritTest.Acc(P) - a0;
                foreach (var h in fc.hinges) h.Drive(0f, 1f);
                Console.WriteLine($"a {a,3:F0} | sym plant q {a1.y,6:F1} p {a1.x,6:F1} r {a1.z,6:F1} model q {ms.y,6:F1} p {ms.x,6:F1} r {ms.z,6:F1} | L plant q {a2.y,6:F1} p {a2.x,6:F1} r {a2.z,6:F1} model q {ml.y,6:F1} p {ml.x,6:F1} r {ml.z,6:F1} | teUp {fc.hinges[0].teUp}");
            }
        }
    }
}
