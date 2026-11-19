// Su-30 0.5.3: yaw-plane ground bench that runs the FCS's own GroundSteer / GsBrakeAlloc / GsLightNoseWheel (the C#, not a
// mirror) against the game's gear model as read from LandingGear.FixedUpdate's IL:
//  - per leg b = (braked ? brake : 0) + Clamp01(yaw x 0.3 x differentialBrakeFactor) for 3 < groundSpeed < 30 m/s;
//    brakeStrength = Lerp(brakeStrength, b, 5 dt)
//  - tyre: lateral = clamp(clamp(slip, +-10) x response x (0.2 + 0.01 |v|), +-1) x mu N, perpendicular to the contact velocity;
//    scrub |sin slip| mu N and brake brakeStrength x mu N along -velocity; total clamped to mu N
//  - steerable leg: castor (contact only, above 1 m/s) steeringAngle += slip x min(0.1 v, 10) x aligningStrength x dt, then the
//    slew toward yaw x steeringLock at steeringSpeed every tick, contact or not
//  - the FCS's Hooks.GearPrefix swap: the nose reads NoseSteerOverride, the mains StockBrakeYaw, and a main's brake becomes
//    max(brake, MainBrakeTarget - stock term)
// Plant: rigid body in the ground plane (x/z, yaw), mass and yaw inertia given, leg loads static (nose share from the
// airframe's leg positions and CG) with an optional light-nose event. No stock FBW parameters in the bench (ControlsFilter
// stub), so the stock law reads the raw pedal: on release the stock differential term is ~0 here (the game: 0.3 on the
// outside main) - the same for both builds.
// usage: bench.dll gyaw kt=10 [lock=45] [slew=60] [align=5] [dbfL=-1] [dbfR=1] [ped=1] [Tturn=2.5] [Tafter=4] [light=t1:t2] [mass=23900] [Iz=560000] [csv=out.csv] [Name=value ...]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class GroundYaw
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static float G(Dictionary<string, string> kv, string k, float d) => kv.TryGetValue(k, out var s) ? float.Parse(s, IC) : d;

    static Vector3 Tyre(Vector3 vc, Vector3 wheelFwd, float N, float mu, float brake, out float slip)
    {
        slip = 0f;
        Vector3 vp = new Vector3(vc.x, 0f, vc.z);
        float sp = vp.magnitude;
        if (sp < 0.05f || N <= 0f) return Vector3.zero;
        Vector3 vh = vp / sp;
        slip = Vector3.SignedAngle(new Vector3(wheelFwd.x, 0f, wheelFwd.z), vh, Vector3.up);   // + = velocity right of the wheel
        float lat = Mathf.Clamp(Mathf.Clamp(slip, -10f, 10f) * (0.2f + 0.01f * sp), -1f, 1f) * mu * N;
        Vector3 left = Vector3.Cross(vh, Vector3.up);                                           // left of the velocity (Unity)
        Vector3 f = left * lat - vh * (Mathf.Abs(Mathf.Sin(slip * Mathf.Deg2Rad)) + brake) * mu * N;
        return Vector3.ClampMagnitude(f, mu * N);
    }

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        float dt = 1f / 60f;
        float V0 = G(kv, "kt", 10f) / 1.944f, ped0 = G(kv, "ped", 1f), Tturn = G(kv, "Tturn", 2.5f), Tafter = G(kv, "Tafter", 4f);
        float m = G(kv, "mass", 23900f), Iz = G(kv, "Iz", 560000f), muN0 = G(kv, "muNose", 1f), muM = G(kv, "muMain", 0.9f);
        float l1 = -1f, l2 = -1f, kick = G(kv, "kick", 0f), relT = G(kv, "rel", 0f), twIn = G(kv, "tw", -1f);   // tw >= 0: speed free from the start, thrust = tw x weight along the nose   // kick: extra yaw acceleration (deg/s^2) for 0.5 s from 1 s after release
        if (kv.TryGetValue("light", out var ls)) { var pp = ls.Split(':'); l1 = float.Parse(pp[0], IC); l2 = float.Parse(pp[1], IC); }
        string csv = kv.TryGetValue("csv", out var cc) ? cc : null;
        var A = new Airframe(dataPath);
        A.ac.gearDeployed = true;
        var P = new Plant(A);
        P.Init(0f, V0, 0f, 0f); P.CreateFcs();
        var fcs = P.fcs;
        fcs.Engaged = true; fcs.Mode = FcsMode.Ground; fcs.isAI = false;
        var gears = fcs.gears;
        var nose = fcs.NoseGear();
        Transform root = A.root;
        Rigidbody rb = A.ac.rb;
        // legs in the root frame (y flattened); CG on the root frame's origin line at the airframe's cgB
        // root frame: the plant has posed the airframe with its CG at P.pCg
        Vector3 cgB = root.InverseTransformPoint(P.pCg); cgB.y = 0f;
        var legB = new Dictionary<LandingGear, Vector3>();
        foreach (var g in gears) { var p = root.InverseTransformPoint(g.transform.position); legB[g] = new Vector3(p.x, 0f, p.z); }
        var mains = gears.Where(g => g != nose).ToList();
        float zn = legB[nose].z, zm = mains.Average(g => legB[g].z);
        if (kv.ContainsKey("nfrac")) cgB.z = zm + G(kv, "nfrac", 0.2f) * (zn - zm);   // nose share as logged (the bench airframe's CG gives 12 %)
        float W = m * 9.81f, Nn0 = W * (cgB.z - zm) / (zn - zm), Nm0 = (W - Nn0) / mains.Count;
        float track = mains.Max(g => legB[g].x) - mains.Min(g => legB[g].x);
        Console.WriteLine($"# gyaw: {m / 1000f:F1} t, Iz {Iz:F0}, nose {Nn0 / 1000f:F1} kN ({Nn0 / W * 100f:F0} %), mains {Nm0 / 1000f:F1} kN each, wheelbase {zn - zm:F2} m, track {track:F2} m, CG {cgB.z - zm:F2} m ahead of the mains");
        var ci = new ControlInputs();
        var dummyCol = new GameObject("ground").AddComponent<Collider>();
        foreach (var g in gears)
        {
            g.controlInputs = ci; g.aircraft = A.ac; g.response = 1f; g.compression = 0.1f; g.travel = 0.7f;
            if (g == nose) { g.steering = true; g.steeringLock = G(kv, "lock", 60f); g.steeringSpeed = G(kv, "slew", 60f); g.aligningStrength = G(kv, "align", 5f); g.frictionCoef = muN0; g.braked = false; g.differentialBrakeFactor = 0f; }
            else { g.steering = false; g.braked = true; g.frictionCoef = muM; g.differentialBrakeFactor = (legB[g].x < cgB.x) ? G(kv, "dbfL", -1f) : G(kv, "dbfR", 1f); }
        }
        // the FCS's mass properties for this bench
        fcs.cg = Vector3.zero; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) fcs.Ia[i, j] = 0f;
        fcs.Ia[2, 2] = Iz; fcs.Ia[0, 0] = Iz * 0.3f; fcs.Ia[1, 1] = Iz;
        // world state
        float psi = 0f, r = 0f, t = 0f; Vector3 pos = Vector3.zero, vel = new Vector3(0f, 0f, V0);
        StreamWriter w = csv != null ? new StreamWriter(csv) : null;
        w?.WriteLine("t,tr,V,r,beta,ped,nwsCmd,wheel,bL,bR,tgtL,tgtR,nload,lnHold,brkOn");
        float tRel = -1f, r0 = 0f, psi0 = 0f, Vrel = 0f, ts = float.NaN, back = 0f, maxB = 0f, VatStop = float.NaN, betaMax = 0f;
        while (t < Tturn + Tafter - 1e-6f)
        {
            float tr = t - Tturn;
            float ped = (tr < 0f) ? ped0 : ped0 * Mathf.Max(0f, 1f - tr / 0.15f);
            if (relT > 0f && tr >= relT) ped = ped0 * Mathf.Min(1f, (tr - relT) / 0.15f);   // drift pattern: re-apply the pedal relT s after the release
            float nload = 1f; bool contactN = true, usableN = true;
            if (l1 >= 0f && tr >= l1)
            {
                if (tr < l1 + 0.05f) { usableN = false; nload = 0f; }                         // in contact, damping force negative
                else if (tr < l2) { contactN = false; usableN = false; nload = 0f; }          // off the ground
                else if (tr < l2 + 0.15f) nload = (tr - l2) / 0.15f;
            }
            // pose the airframe
            Quaternion q = Quaternion.Euler(0f, psi, 0f);
            root.localRotation = q; root.localPosition = pos - q * cgB;
            rb.velocity = vel; rb.angularVelocity = new Vector3(0f, r * Mathf.Deg2Rad, 0f); rb.worldCenterOfMass = pos;
            fcs.cg = pos;
            float spd = vel.magnitude;
            A.ac.speed = spd;
            // leg state as the game exposes it
            float NnNow = Nn0 * nload, NmNow = Nm0 + (Nn0 - NnNow) / mains.Count;
            foreach (var g in gears)
            {
                Vector3 pw = pos + q * (legB[g] - cgB);
                bool c = g != nose || contactN;
                g.compression = c ? 0.1f : 0f;
                g.hit = c ? new RaycastHit { collider = dummyCol, point = pw, normal = Vector3.up } : default;
                g.compressionForce = (g == nose) ? (usableN ? NnNow : 0f) : NmNow; g.dampingForce = 0f;
                g.wheelSpeed = rb.GetPointVelocity(pw).magnitude;
            }
            // the FCS's ground steering (pedal in through sy / gsPedRaw, as Tick sets them)
            fcs.sy = ped; fcs.gsPedRaw = ped; ci.yaw = ped; ci.brake = 0f;
            fcs.GroundSteer(dt, true, contactN);
            // the game's gear FixedUpdate with the FCS's prefix swap
            Vector3 F = Vector3.zero; float Mz = 0f;
            float[] tgt = new float[2] { -1f, -1f };
            foreach (var g in gears)
            {
                Vector3 pw = pos + q * (legB[g] - cgB);
                Vector3 vc = rb.GetPointVelocity(pw);
                float yawIn = ped, brakeIn = 0f;
                if (g.steering) { if (fcs.NoseSteerOverride(out float y)) yawIn = y; }
                else
                {
                    if (fcs.StockBrakeYaw(out float y)) yawIn = y;
                    if (fcs.MainBrakeTarget(g, out float tg))
                    {
                        float st = (g.differentialBrakeFactor != 0f && g.groundSpeed > 3f && g.groundSpeed < 30f) ? Mathf.Clamp01(yawIn * 0.3f * g.differentialBrakeFactor) : 0f;
                        brakeIn = Mathf.Max(brakeIn, tg - st);
                        tgt[legB[g].x < cgB.x ? 0 : 1] = tg;
                    }
                }
                float b = g.braked ? brakeIn : 0f;
                if (g.differentialBrakeFactor != 0f && g.groundSpeed < 30f && g.groundSpeed > 3f) b += Mathf.Clamp01(yawIn * 0.3f * g.differentialBrakeFactor);
                g.brakeStrength = Mathf.Lerp(g.brakeStrength, b, dt * 5f);
                Vector3 fwdW = q * Vector3.forward;
                g.groundSpeed = Vector3.Dot(vc, fwdW);
                bool c = g != nose || contactN;
                Vector3 wheelFwd = fwdW;
                if (g.steering)
                {
                    if (c && g.groundSpeed > 1f)
                    {
                        float ang = Vector3.SignedAngle(Quaternion.Euler(0f, g.steeringAngle, 0f) * fwdW, new Vector3(vc.x, 0f, vc.z), Vector3.up);
                        g.steeringAngle += ang * Mathf.Min(g.groundSpeed * 0.1f, 10f) * g.aligningStrength * dt;
                    }
                    wheelFwd = Quaternion.Euler(0f, g.steeringAngle, 0f) * fwdW;
                }
                float N = (g == nose) ? NnNow : NmNow;
                Vector3 f = c ? Tyre(vc, wheelFwd, N, g.frictionCoef, g.brakeStrength, out float _) : Vector3.zero;
                F += f; Mz += Vector3.Cross(pw - pos, f).y;
                if (g.steering)
                {
                    float d = yawIn * g.steeringLock - g.steeringAngle;
                    g.steeringAngle += Mathf.Clamp(d, -g.steeringSpeed * dt, g.steeringSpeed * dt);
                    g.steeringAngle = Mathf.Clamp(g.steeringAngle, -Mathf.Abs(g.steeringLock), Mathf.Abs(g.steeringLock));
                }
            }
            // integrate (speed held through the turn so every case releases at the speed asked)
            if (twIn >= 0f) F += (q * Vector3.forward) * (twIn * m * 9.81f);
            vel += F / m * dt;
            r += Mz / Iz * Mathf.Rad2Deg * dt + ((tr >= 1f && tr < 1.5f) ? kick * dt : 0f);
            if (tr < 0f && twIn < 0f) vel = vel.normalized * V0;
            pos += vel * dt; psi += r * dt; t += dt;
            float beta = Vector3.SignedAngle(q * Vector3.forward, vel, Vector3.up);
            if (tr >= 0f && tRel < 0f) { tRel = t; r0 = r; psi0 = psi; Vrel = vel.magnitude; }
            if (tr >= 0f)
            {
                if (float.IsNaN(ts) && Mathf.Abs(r) < 2f) { ts = tr; VatStop = vel.magnitude; }
                if (!float.IsNaN(ts)) back = Mathf.Max(back, -Mathf.Sign(r0) * r);
                maxB = Mathf.Max(maxB, Mathf.Max(mains[0].brakeStrength, mains[mains.Count - 1].brakeStrength));
                betaMax = Mathf.Max(betaMax, Mathf.Abs(beta));
            }
            var bL = mains.First(g => legB[g].x < cgB.x).brakeStrength; var bR = mains.First(g => legB[g].x >= cgB.x).brakeStrength;
            w?.WriteLine(string.Format(IC, "{0:F3},{1:F3},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12:F2},{13},{14}", t, tr, vel.magnitude * 1.944f, r, beta, ped, fcs.gsNwsDeg, nose.steeringAngle, bL, bR, tgt[0], tgt[1], nload, fcs.gsLnDbg ? 1 : 0, fcs.gsBrkOn ? 1 : 0));
        }
        w?.Close();
        Console.WriteLine(string.Format(IC, "gyaw kt {0:F0}: r0 {1:F1} | stop {2:F2} s | dpsi {3:F1} | back {4:F2} | dV@stop {5:F1} kt | peak brake {6:F2} | beta max {7:F1}",
            V0 * 1.944f, r0, ts, psi - psi0, back, (Vrel - VatStop) * 1.944f, maxB, betaMax));
    }
}
