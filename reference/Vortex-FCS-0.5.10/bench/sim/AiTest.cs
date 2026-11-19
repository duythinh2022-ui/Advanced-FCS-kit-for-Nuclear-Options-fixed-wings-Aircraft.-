using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;
// Port of NO's AutopilotPlane forward-flight core (no terrain, aimVelocity = true) with Aryx's F-22E gains,
// flying either the stock fly-by-wire port or the FCS (AI rate mode).
internal sealed class AiPid
{
    float P, I, D, refV, i, prev; bool first = true;
    public AiPid(float p, float ii, float d, float refV) { P = p; I = ii; D = d; this.refV = refV; }
    public float Out(float e, float clampP, float clampI, float airspeed, float dt)
    {
        float num = Mathf.Clamp(refV * refV / Mathf.Max(airspeed * airspeed, 10f), 0f, 4f);
        i = Mathf.Abs(e) < clampI ? i + e * dt : 0f;
        float d = first ? 0f : (e - prev) / dt; first = false; prev = e;
        return Mathf.Clamp(e, -clampP, clampP) * P + i * I + d * D * num;
    }
}
internal static class AiTest
{
    static float AngleOnAxis(Vector3 self, Vector3 other, Vector3 axis) => Vector3.SignedAngle(Vector3.Cross(axis, self), Vector3.Cross(axis, other), axis);
    public static List<float[]> Fly(string dataPath, bool stock, float iasKmh, float alt, float tgtHeadingDeg, float tgtElevDeg, float throttle, float T, string csv)
    {
        var A = new Airframe(dataPath);
        var P = new Plant(A);
        float rho = Plant.Density(alt);
        float tas = iasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f);
        P.Init(alt, tas, 3f, 3f);
        P.throttle = throttle;
        if (stock) P.stock = new StockFbw(); else { P.CreateFcs(); P.fcs.isAI = true; P.fcs.aiGLimit = 9.5f; P.fcs.aiCornerSpeed = 140f; }
        var px = new AiPid(0.07f, 0.03f, 0.015f, 180f); var py = new AiPid(0.05f, 0.01f, 0f, 180f); var pz = new AiPid(0.015f, 0.001f, 0.005f, 180f);
        float h = tgtHeadingDeg * Mathf.Deg2Rad, el = tgtElevDeg * Mathf.Deg2Rad;
        Vector3 destDir = new Vector3(Mathf.Sin(h) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(h) * Mathf.Cos(el));
        float corner = 170f;
        var rows = new List<float[]>();
        StreamWriter w = csv != null ? new StreamWriter(csv) : null;
        w?.WriteLine("t,ias,alpha,nz,p,q,r,err,bank,sp,sr,sy,alt");
        int n = (int)(T / P.dt);
        Vector3 vPrev = P.vCg;
        for (int k = 0; k < n; k++)
        {
            Quaternion q = P.q;
            Vector3 fwd = q * Vector3.forward, up = q * Vector3.up, right = q * Vector3.right;
            Vector3 vel = P.vCg;
            Vector3 vector = vel;
            Vector3 dest = P.pCg + destDir * 100000f;
            Vector3 toDest = (dest - P.pCg).normalized;
            float num = Vector3.Angle(vector, dest - P.pCg);
            float airspeed = Vector3.Dot(fwd, vel);
            float num3 = Mathf.Clamp01(airspeed / corner);
            Vector3 vector2 = vector.normalized * 1000f;
            Vector3 waypointDelta = Vector3.RotateTowards(new Vector3(vector2.x, 0f, vector2.z), toDest, 0.9f * num3 * num3, 0f);
            Vector3 normalized = waypointDelta.normalized;
            Vector3 vector3 = normalized;
            vector3 = vector3.normalized;
            vector3 += Vector3.up / Mathf.Max(num, 5f);
            vector3 += Mathf.Clamp01(num - 15f) * 0.001f * Vector3.up;
            float num4 = AngleOnAxis(fwd, normalized, up);
            if (num < 10f) vector3 = Vector3.up + right * Mathf.Clamp(num4 * 0.25f, -0.3f, 0.3f);
            float bankAllowed = 180f * Mathf.Clamp(alt * 0.003f - 1f, 0.6f, 1.2f);
            float num5 = normalized.y - vel.y;
            bankAllowed *= Mathf.Clamp(num5 * 0.05f + 1f, 1.2f, 0.5f);
            bankAllowed *= Mathf.Max(num3 * num3, 0.45f);
            float num6;
            if (bankAllowed < 180f)
            {
                float a1 = AngleOnAxis(up, Vector3.up, -vector);
                float a2 = Mathf.Clamp(AngleOnAxis(vector3, Vector3.up, -vector), -bankAllowed, bankAllowed);
                num6 = a1 - a2;
            }
            else num6 = AngleOnAxis(up, vector3, -vector);
            float a3 = AngleOnAxis(vector, normalized, right) * Mathf.Lerp(1f, 0.1f, Mathf.Abs(num6) * 0.015f);
            if (num > 20f) num4 = 0f;
            var ci = A.ac.inputs;
            ci.pitch = Mathf.Clamp(px.Out(a3, 20f, 5f, airspeed, P.dt), -1f, 1f);
            ci.yaw = Mathf.Clamp(py.Out(num4, 60f, 5f, airspeed, P.dt), -1f, 1f);
            ci.roll = Mathf.Clamp(pz.Out(num6, 60f, 5f, airspeed, P.dt), -1f, 1f);
            float sp = ci.pitch, sr = ci.roll, sy = ci.yaw;
            P.Step();
            Vector3 vb = Quaternion.Inverse(P.q) * P.vCg;
            float alpha = -Mathf.Atan2(vb.y, vb.z) * 57.29578f;
            Vector3 acc = (P.vCg - vPrev) / P.dt - new Vector3(0f, -9.81f, 0f); vPrev = P.vCg;
            float nz = Vector3.Dot(acc, P.q * Vector3.up) / 9.81f;
            Vector3 lw = Quaternion.Inverse(P.q) * P.w * 57.29578f;
            float ias = P.vCg.magnitude * Mathf.Sqrt(P.A.ac.airDensity / 1.225f) * 3.6f;
            float err = Vector3.Angle(P.vCg, destDir);
            float bank = AngleOnAxis(P.q * Vector3.up, Vector3.up, P.q * Vector3.forward);
            var row = new float[] { k * P.dt, ias, alpha, nz, -lw.z, -lw.x, lw.y, err, bank, sp, sr, sy, P.pCg.y };
            rows.Add(row);
            w?.WriteLine(string.Join(",", Array.ConvertAll(row, x => x.ToString("F3", CultureInfo.InvariantCulture))));
            if (float.IsNaN(alpha) || P.pCg.y < 50f) break;
        }
        w?.Close();
        return rows;
    }
}
