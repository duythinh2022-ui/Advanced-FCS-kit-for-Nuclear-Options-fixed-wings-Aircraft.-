// 2.0.8: static pitch-moment sweep, per part, for fitting the body-lift curves and checking trim.
// Rigid aircraft at AoA alpha (no rates, no sideslip), controls held: stabs at `stab`, flaperons at 0, TVC at `tvc`,
// high-lift devices at the FCS slat schedule. Same per-part force law as Plant.Step (AeroJob_Math), including the
// 2.0.8 stab downwash when Plugin.StabDownwash > 0. Thrust only if thrust=1 (full AB).
// mode cmsweep: prints pitch accel per alpha; dump=file writes per-part lift/drag moment and normal force as JSON.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class CmSweep
{
    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        var IC = CultureInfo.InvariantCulture;
        float G(string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;
        float rud = G("rud", 0f), flap = G("flap", 0f), ail = G("ail", 0f);
        float beta = G("beta", 0f), ias = G("ias", 300f), stab = G("stab", 0f), tvc = G("tvc", 0f), a0 = G("a0", -10f), a1 = G("a1", 70f), da = G("da", 2.5f);
        bool thrust = G("thrust", 0f) > 0.5f;
        // 4.6.0: slat droop away from the schedule, deg per side (SlatPhysics)
        float slatDL = G("slatDL", 0f), slatDR = G("slatDR", 0f);
        float[] slatDi = { 0f, G("slatD1", 0f), G("slatD2", 0f), G("slatD3", 0f) };
        var A = new Airframe(dataPath);
        var P = new Plant(A);
        float alt = 3000f, rho = Plant.Density(alt);
        float tas = ias / 3.6f / Mathf.Sqrt(rho / 1.225f);
        foreach (var cs in A.surfaces)
        {
            float d = cs.name.Contains("Elevator") ? stab : cs.name.Contains("Rudder") ? rud : cs.name.Contains("Flap") ? flap : cs.name.Contains("Aileron") ? ail : 0f;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
        }
        foreach (var e in A.engines) foreach (var vt in e.vectoringTransforms) vt.localEulerAngles = new Vector3(tvc, 0, 0);
        double Ip = A.Itot[0, 0];
        var sbOut = new StringBuilder(); sbOut.Append("{\"alphas\":[");
        var rows = new List<string>();
        bool first = true;
        Console.WriteLine($"# cmsweep ias {ias} stab {stab} tvc {tvc} thrust {thrust} downwash k {Plugin.StabDownwash} bodyShape {Plugin.BodyLiftShaping}");
        for (float a = a0; a <= a1 + 1e-3f; a += da)
        {
            // slat / high-lift schedule as FlapSystem (AoA part only, lift gate open)
            float pos = Mathf.Clamp01((Mathf.Abs(a) - Plugin.SlatAoAStart) / Mathf.Max(1f, Plugin.SlatAoAFull - Plugin.SlatAoAStart));
            pos *= 1f - Mathf.Clamp01((Mathf.Abs(a) - (Plugin.HighLiftMaxAoA - 10f)) / 10f);
            if (a < 0f) pos = 0f;
            foreach (var h in A.hlds) h.aeroPart.wingArea = SlatAero.On ? h.partAreaRetracted : Mathf.Lerp(h.partAreaRetracted, h.partAreaRetracted + (h.partAreaDeployed - h.partAreaRetracted), pos);
            float slatRef = Plugin.SlatMaxDroop * pos;
            if (FcsController.FlaperonReflexMax != 0f) foreach (var cs in A.surfaces) if (cs.name.Contains("Flap") || cs.name.Contains("Aileron")) { float d0 = cs.name.Contains("Flap") ? flap : ail; cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d0 + FcsController.Reflex(a), Vector3.right); }
            P.Init(alt, tas, a, a);
            if (beta != 0f) { P.vCg = Quaternion.AngleAxis(beta, P.q * Vector3.up) * P.vCg; P.Pose(); }
            Vector3 up = P.q * Vector3.up, right = P.q * Vector3.right;
            float Mtot = 0f, Ntot = 0f, Dtot = 0f; Vector3 Mv = Vector3.zero;
            var parts = new StringBuilder();
            bool pf = true;
            foreach (var b in A.bodies)
            {
                var ap = b.part; Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude; var lt = ap.liftNormal;
                Vector3 vp = v; if (Downwash.IsStabName(ap.name)) v = Downwash.Apply(v, P.q, P.vCg);
                Vector3 v6 = Quaternion.Inverse(lt.rotation) * v; Vector3 nrm = Vector3.Cross(v, lt.right).normalized;
                float al = Mathf.Atan2(v6.y, v6.z); float cl, cd;
                if (ap.airfoil < 0) { cl = 0f; cd = 0f; }
                else { float idx = Mathf.Clamp(al * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1); cl = Mathf.Lerp(A.liftCharts[ap.ChartId][i], A.liftCharts[ap.ChartId][j], idx - i); cd = Mathf.Lerp(A.dragCharts[ap.ChartId][i], A.dragCharts[ap.ChartId][j], idx - i); }
                if (Downwash.IsFinName(ap.name)) { float fk = Downwash.FinBlankK(a) * Downwash.ShieldK(stab, a); cl *= fk; cd *= fk; }   // 4.7.0 fin blanking, stab-fin interference
                if (FcsController.SlatAftGain != 0f && SlatAero.AftSide(ap.name, out int aftSd)) { float ak = SlatAero.AftK(aftSd, slatDL, slatDR); cl *= ak; cd *= ak; }   // 4.7.0 slat-coupled aft lift
                float eff = ap.wingEffectiveness;
                float n8 = (ap.dragArea + ap.wingArea * 0.1f) * (1f - eff);
                float par = 0.5f * rho * v2 * 0.5f * (ap.dragArea + n8);
                Vector3 fl = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * eff);
                Vector3 fd = -v.normalized * (cd * rho * v2 * 0.5f * ap.wingArea * eff) - vp.normalized * par;
                if (FcsController.StabOutFrac > 0f && ap.airfoil >= 0 && Downwash.IsStabName(ap.name))
                {
                    // 4.7.0 two-zone stab (same model as Plant.Forces; the outboard point's lateral offset does not enter a symmetric sweep)
                    Vector3 vo = Downwash.ApplyOut(vp, P.q, P.vCg); Vector3 v6o = Quaternion.Inverse(lt.rotation) * vo;
                    float io = Mathf.Clamp(Mathf.Atan2(v6o.y, v6o.z) * 20.37185f + 64f, 0, 127); int i0 = (int)io; int j0 = Math.Min(127, i0 + 1);
                    float clo = Mathf.Lerp(A.liftCharts[ap.ChartId][i0], A.liftCharts[ap.ChartId][j0], io - i0), cdo = Mathf.Lerp(A.dragCharts[ap.ChartId][i0], A.dragCharts[ap.ChartId][j0], io - i0);
                    float qSo = rho * v2 * 0.5f * ap.wingArea * eff, kf = FcsController.StabOutFrac;
                    Vector3 flo = -Vector3.Cross(vo, lt.right).normalized * (clo * qSo);
                    fd += kf * (-vo.normalized * (cdo * qSo) + v.normalized * (cd * qSo));
                    fl += kf * (flo - fl);
                }
                // 4.6.0 (SlatPhysics): the slat's droop away from the schedule on the panel behind it, and the slat plate
                int sgI = SlatAero.On ? SlatAero.SegOfHost(ap.name) : -1;
                Vector3 slatPlateF = Vector3.zero, slatPlateP = Vector3.zero;
                if (sgI >= 0 && ap.airfoil >= 0)
                {
                    var sg = SlatAero.Segs[sgI]; float dd = (sg.side < 0 ? slatDL : slatDR) + slatDi[sg.idx];
                    if (Mathf.Abs(dd) > 0.01f)
                    {
                        float cov = SlatAero.Cover(sg.idx); float qS0 = rho * v2 * 0.5f * ap.wingArea * eff;
                        Vector3 dH = SlatAero.HostDelta(v, lt.rotation, A.liftCharts[ap.ChartId], A.dragCharts[ap.ChartId], qS0, cov, dd);
                        Vector3 dHl = Vector3.Dot(dH, -nrm) * -nrm; fl += dHl; fd += dH - dHl;
                        foreach (var t in A.T.Values) if (t.name != null && t.name.EndsWith(sg.slat)) { slatPlateP = t.position; break; }
                        if (slatPlateP != Vector3.zero && Plugin.SlatChordFrac > 0f)
                            slatPlateF = SlatAero.PlateDelta(v, lt.rotation, A.liftCharts[ap.ChartId], A.dragCharts[ap.ChartId], qS0 * cov * Plugin.SlatChordFrac, slatRef + dd, slatRef);
                    }
                }
                Vector3 r = b.rb.worldCenterOfMass - P.pCg + (ap.centerOfLift != Vector3.zero ? lt.rotation * ap.centerOfLift : Vector3.zero);
                float ml = -Vector3.Dot(Vector3.Cross(r, fl), right), md = -Vector3.Dot(Vector3.Cross(r, fd), right);
                float nl = Vector3.Dot(fl, up), nd = Vector3.Dot(fd, up);
                if (slatPlateF != Vector3.zero) { Vector3 rp = slatPlateP - P.pCg; ml += -Vector3.Dot(Vector3.Cross(rp, slatPlateF), right); nl += Vector3.Dot(slatPlateF, up); Mv += Vector3.Cross(rp, slatPlateF); Dtot += -Vector3.Dot(slatPlateF, P.vCg.normalized); }
                Mtot += ml + md; Ntot += nl + nd; Mv += Vector3.Cross(r, fl + fd); Dtot += -Vector3.Dot(fl + fd, P.vCg.normalized);
                float xb = Vector3.Dot(r, P.q * Vector3.forward);
                parts.Append(pf ? "" : ",").Append(string.Format(IC, "{{\"n\":\"{0}\",\"af\":{1},\"S\":{2:F3},\"x\":{3:F3},\"cl\":{4:F4},\"ml\":{5:F1},\"md\":{6:F1},\"nl\":{7:F1},\"nd\":{8:F1}}}", ap.name.Replace("Aryx_KingRaptor_", ""), ap.airfoil, ap.wingArea, xb, cl, ml, md, nl, nd));
                pf = false;
            }
            float mThr = 0f;
            if (thrust) foreach (var e in A.engines) foreach (var n in e.nozzles) { var fr = n.thrustTransform.forward * (e.staticThrust + n.abThrust); mThr += -Vector3.Dot(Vector3.Cross(n.thrustTransform.position - P.pCg, fr), right); }
            Mtot += mThr;
            float qd = 0.5f * rho * tas * tas;
            Console.WriteLine(string.Format(IC, "alpha {0,6:F1}  pitchAcc {1,8:F2} deg/s^2  Cm*Sc {2,8:F3} m^3  nz {3,6:F2}  CN*S {4,7:F2} m^2  CD*S {5,7:F2} m^2  yawAcc {6,8:F2}  rollAcc {7,8:F2}", a, Mtot / Ip * 57.2958, Mtot / qd, Ntot / (A.mass * 9.81f), Ntot / qd, Dtot / qd, Vector3.Dot(Mv, P.q * Vector3.up) / A.Itot[1, 1] * 57.2958, Vector3.Dot(Mv, P.q * Vector3.forward) / A.Itot[2, 2] * 57.2958));
            rows.Add(string.Format(IC, "{{\"a\":{0:F2},\"q\":{1:F1},\"mthr\":{2:F1},\"parts\":[{3}]}}", a, qd, mThr, parts));
            first = false;
        }
        if (kv.TryGetValue("dump", out var fn)) File.WriteAllText(fn, "{\"I\":" + Ip.ToString(IC) + ",\"rows\":[" + string.Join(",", rows) + "]}");
    }
}
