// FCS effector-table moments at every logged sample (capacity-estimator identification, 2 Nov 2026).
//   bench fcsmom log=<F22E_FCS_*.csv> out=<csv> [modes=3,6] [every=1] [Name=value ...]
// The plant is posed at the logged state exactly as `eqerr` does (surfaces at _now, drives solved from _aoaNow, nozzles at
// tvcNow, slats, airbrakes, thrust from tw), then the FCS's OWN effector tables (SurfaceEffector / TvcEffector.Evaluate,
// the allocator's model, not the plant) are evaluated with the FCS's own inertia, and written per axis in deg/s^2:
//   mNow  : table moment at the logged deflections (what INDI's vector8 / `zero` sees)
//   mCmd  : table moment at the logged commands (what `zero4` sees)
//   hdrP/N: table headroom toward each side, every effector moved independently to the end of its lo..hi range
//           (the ceiling the tables claim; the allocator cannot take all of it at once)
//   plus rudder-only / stab-only / flaperon-only / TVC-only yaw and roll headroom, and the FCS inertia diagonal.
// Nothing here is FCS behaviour; it is the offline twin of the quantities an online estimator would have.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class FcsMom
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; string outPath = kv.TryGetValue("out", out var o) ? o : "fcsmom.csv";
        var modes = (kv.TryGetValue("modes", out var ms) ? ms : "3,6").Split(',').Select(int.Parse).ToHashSet();
        int every = kv.TryGetValue("every", out var ev) ? int.Parse(ev) : 1;
        var A = new Airframe(dataPath);
        var P = new Plant(A) { gyro = Gyro.Unity };
        P.Init(1000f, 150f, 5f, 5f);
        P.CreateFcs();
        var fcs = P.fcs;
        var ps = new EqErr.Poser(A, P);
        var lines = File.ReadLines(log).GetEnumerator();
        lines.MoveNext();
        var hdr = lines.Current.Split(',');
        var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        var effs = fcs.effs;
        using var w = new StreamWriter(outPath);
        var names = new List<string>();
        foreach (var e in effs) names.Add(e.name);
        w.WriteLine("t,mode,tas,alpha,beta,p,q,r,rudderAuth,Ixx,Iyy,Izz,"
            + "mNowX,mNowY,mNowZ,mCmdX,mCmdY,mCmdZ,"
            + "hdrXP,hdrXN,hdrYP,hdrYN,hdrZP,hdrZN,"
            + "rudZP,rudZN,stabZP,stabZN,flapZP,flapZN,tvcZP,tvcZN,"
            + "rudXP,rudXN,stabXP,stabXN,flapXP,flapXN,"
            + "rudZnow,stabZnow,flapZnow,tvcZnow,rudXnow,stabXnow,flapXnow,rudZcmd,stabZcmd,flapZcmd,rudXcmd,stabXcmd,flapXcmd,prZP,prZN,prXP,prXN,prZPx,prZNx,prXPz,prXNz,prZPrud,prZPstab,prZPflap,prZNrud,prZNstab,prZNflap,prXPrud,prXPstab,prXPflap,prXNrud,prXNstab,prXNflap,"
            + string.Join(",", names.Select(n => n + "_lo," + n + "_hi")));
        int n = 0, used = 0;
        float g = 9.81f;
        while (lines.MoveNext())
        {
            var f = lines.Current.Split(',');
            if (f.Length < hdr.Length - 1) continue;
            n++;
            if (n % every != 0) continue;
            float G(string k) => float.Parse(f[col[k]], IC);
            int mode = (int)G("mode");
            if (!modes.Contains(mode)) continue;
            float tas = G("tas"); if (tas < 40f) continue;
            float rho = G("rho"), alpha = G("alpha"), beta = G("beta"), pr = G("p"), qr = G("q"), rr = G("r");
            float phi = G("phi"), theta = G("theta"), tw = G("tw"), tvc = G("tvcNow"), hl = G("hlPos"), ab = G("abOpen");
            ps.PoseState(tas, rho, alpha, beta, pr, qr, rr, phi, theta);
            foreach (var sn in EqErr.Poser.SurfNames) ps.SetSurface(sn, G(sn + "_now"));
            ps.SetSlats(hl);
            // flaperon drives from the logged local AoA (secant, as eqerr)
            float flapBias = G("flapBias"), relief = G("relief");
            for (int di = 0; di < ps.drives.Count; di++)
            {
                var d = ps.drives[di];
                float a0 = d.flap ? Mathf.Clamp(-flapBias - (d.hasHl ? d.hlAngle * hl : 0f), -25f, 20f) : Mathf.Clamp(relief, -25f, 20f);
                ps.SetDrive(di, a0);
                float target = G(d.surf.name + "_aoaNow");
                float f0 = LocalAoA(d.surf, A, P);
                ps.SetDrive(di, a0 + 1f);
                float f1 = LocalAoA(d.surf, A, P);
                float k = f1 - f0;
                float ang = a0;
                if (Mathf.Abs(k) > 0.2f)
                {
                    ang = Mathf.Clamp(a0 + (target - f0) / k, -25f, 20f);
                    ps.SetDrive(di, ang);
                    float f2 = LocalAoA(d.surf, A, P);
                    if (Mathf.Abs(f2 - target) > 0.05f) { ang = Mathf.Clamp(ang + (target - f2) / k, -25f, 20f); ps.SetDrive(di, ang); }
                }
                else ps.SetDrive(di, a0);
            }
            ps.SetNozzles(tvc); ps.SetAirbrakes(ab);
            int nn = A.engines.Sum(e => e.nozzles.Length);
            float Tn = tw * A.mass * g / Math.Max(1, nn);
            foreach (var tf in A.engines) foreach (var nz in tf.nozzles) nz.totalThrust = Tn;
            P.Pose();
            A.ac.airDensity = rho;
            // ---- the FCS's own view
            Quaternion rootInv = Quaternion.Inverse(P.q);
            fcs.alpha = alpha; fcs.beta = beta; fcs.tas = tas;
            fcs.RebuildBodies();
            fcs.UpdateMassProps(true, rootInv);
            float rudderAuth = 1f - Mathf.Clamp01((Mathf.Abs(alpha) - Plugin.RudderFadeStart) / Mathf.Max(1f, Plugin.RudderFadeEnd - Plugin.RudderFadeStart));
            fcs.rudderAuth = rudderAuth;
            foreach (SurfaceEffector surface in fcs.surfaces)
            {
                float num3 = (surface.kind == SurfaceKind.Rudder) ? rudderAuth : 1f;
                surface.lo = surface.min * num3;
                surface.hi = surface.max * num3;
                if (FcsController.StabPeakClamp && surface.kind == SurfaceKind.Stabilator) fcs.ClampStabToPeak(surface);
            }
            var ctx = new EvalContext { cg = fcs.cg, rootInv = rootInv, rho = rho, wind = A.ac.windVelocity, vAir = A.ac.rb.velocity - A.ac.windVelocity };
            Vector3 mNow = Vector3.zero, mCmd = Vector3.zero;
            Vector3 hP = Vector3.zero, hN = Vector3.zero;
            float rudZP = 0, rudZN = 0, stabZP = 0, stabZN = 0, flapZP = 0, flapZN = 0, tvcZP = 0, tvcZN = 0;
            float rudXP = 0, rudXN = 0, stabXP = 0, stabXN = 0, flapXP = 0, flapXN = 0;
            float rudZnow = 0, stabZnow = 0, flapZnow = 0, tvcZnow = 0, rudXnow = 0, stabXnow = 0, flapXnow = 0, rudZcmd = 0, stabZcmd = 0, flapZcmd = 0, rudXcmd = 0, stabXcmd = 0, flapXcmd = 0;
            var lohi = new List<string>();
            foreach (var e in effs)
            {
                e.Evaluate(ref ctx);
                if (!e.active) { lohi.Add("0,0"); continue; }
                float cmd = e is TvcEffector ? G("tvcCmd") : G(e.name + "_cmd");
                e.cmd = cmd;   // the allocator's logged decision: the probes warm-start from it
                Vector3 mn = e.Mnow, mc = e.MAt(cmd);
                mNow += mn; mCmd += mc;
                // headroom: extreme of the table within lo..hi, relative to now, per axis independently
                Vector3 maxM = mn, minM = mn;
                void Take(Vector3 m) { maxM = Vector3.Max(maxM, m); minM = Vector3.Min(minM, m); }
                Take(e.MAt(e.lo)); Take(e.MAt(e.hi));
                for (int i = 0; i < e.N; i++) if (e.grid[i] > e.lo && e.grid[i] < e.hi) Take(e.M[i]);
                Vector3 hp = maxM - mn, hn = minM - mn;
                hP += hp; hN += hn;
                bool isRud = e.name.Contains("Rudder"), isStab = e.name.Contains("Elevator"), isFlap = e.name.Contains("Flap") || e.name.Contains("Aileron");
                if (isRud) { rudZP += hp.z; rudZN += hn.z; rudXP += hp.x; rudXN += hn.x; rudZnow += mn.z; rudXnow += mn.x; rudZcmd += mc.z; rudXcmd += mc.x; }
                else if (isStab) { stabZP += hp.z; stabZN += hn.z; stabXP += hp.x; stabXN += hn.x; stabZnow += mn.z; stabXnow += mn.x; stabZcmd += mc.z; stabXcmd += mc.x; }
                else if (isFlap) { flapZP += hp.z; flapZN += hn.z; flapXP += hp.x; flapXN += hn.x; flapZnow += mn.z; flapXnow += mn.x; flapZcmd += mc.z; flapXcmd += mc.x; }
                else { tvcZP += hp.z; tvcZN += hn.z; tvcZnow += mn.z; }
                lohi.Add(S(e.lo) + "," + S(e.hi));
            }
            float ix = fcs.IaInv[0, 0] * Mathf.Rad2Deg, iy = fcs.IaInv[1, 1] * Mathf.Rad2Deg, iz = fcs.IaInv[2, 2] * Mathf.Rad2Deg;
            // ---- allocator probes: from the logged commands, ask the allocator for an extra yaw / roll increment toward each
            // side (the rest of the target as the logged commands imply) and read what it delivers in table space
            float[] probe = new float[8]; float[,] grpD = new float[4, 4];
            {
                var saved = effs.Select(e => e.cmd).ToArray();
                fcs.alloc.fightW[1] = fcs.alloc.fightW[2] = fcs.alloc.fightW[3] = 1f;
                Vector3 baseT = mCmd;
                void DoProbe(int slot, Vector3 dT)
                {
                    for (int i = 0; i < effs.Count; i++) effs[i].cmd = saved[i];
                    for (int it = 0; it < ProbeIters; it++) fcs.alloc.Solve(effs, baseT + dT);
                    Vector3 got = Vector3.zero;
                    for (int i = 0; i < effs.Count; i++)
                    {
                        var e = effs[i];
                        if (!e.active) continue;
                        Vector3 mi = e.MAt(e.cmd);
                        got += mi;
                        Vector3 di = mi - e.MAt(saved[i]);
                        int gi = e.name.Contains("Rudder") ? 0 : e.name.Contains("Elevator") ? 1 : (e.name.Contains("Flap") || e.name.Contains("Aileron")) ? 2 : 3;
                        grpD[slot, gi] += (slot < 2) ? di.z * iz : di.x * ix;
                    }
                    Vector3 d = got - baseT;
                    if (DebugT > 0f && Math.Abs(G("t") - DebugT) < 0.009f)
                    {
                        Console.Error.WriteLine($"# probe slot {slot} dT {dT.z * iz:F1}/{dT.x * ix:F1}: d.z {d.z * iz:F1} d.x {d.x * ix:F1}");
                        for (int i = 0; i < effs.Count; i++) { var e = effs[i]; Console.Error.WriteLine($"#   {e.name,-11} act {e.active} lo {e.lo:F1} hi {e.hi:F1} saved {saved[i]:F1} -> cmd {e.cmd:F1}  z {e.MAt(saved[i]).z * iz:F1} -> {e.MAt(e.cmd).z * iz:F1}   x {e.MAt(saved[i]).x * ix:F1} -> {e.MAt(e.cmd).x * ix:F1}"); }
                    }
                    probe[slot] = (slot < 2) ? d.z * iz : d.x * ix;
                    probe[slot + 4] = (slot < 2) ? d.x * ix : d.z * iz;   // collateral on the other axis
                }
                float pz = ProbeZ / iz, px = ProbeX / ix;
                DoProbe(0, new Vector3(0f, 0f, pz)); DoProbe(1, new Vector3(0f, 0f, -pz));
                DoProbe(2, new Vector3(px, 0f, 0f)); DoProbe(3, new Vector3(-px, 0f, 0f));
                for (int i = 0; i < effs.Count; i++) effs[i].cmd = saved[i];
            }
            w.WriteLine(string.Join(",", new[] { S(G("t")), mode.ToString(), S(tas), S(alpha), S(beta), S(pr), S(qr), S(rr), S(rudderAuth), S(fcs.Ia[0, 0]), S(fcs.Ia[1, 1]), S(fcs.Ia[2, 2]),
                S(mNow.x * ix), S(mNow.y * iy), S(mNow.z * iz), S(mCmd.x * ix), S(mCmd.y * iy), S(mCmd.z * iz),
                S(hP.x * ix), S(hN.x * ix), S(hP.y * iy), S(hN.y * iy), S(hP.z * iz), S(hN.z * iz),
                S(rudZP * iz), S(rudZN * iz), S(stabZP * iz), S(stabZN * iz), S(flapZP * iz), S(flapZN * iz), S(tvcZP * iz), S(tvcZN * iz),
                S(rudXP * ix), S(rudXN * ix), S(stabXP * ix), S(stabXN * ix), S(flapXP * ix), S(flapXN * ix),
                S(rudZnow * iz), S(stabZnow * iz), S(flapZnow * iz), S(tvcZnow * iz), S(rudXnow * ix), S(stabXnow * ix), S(flapXnow * ix), S(rudZcmd * iz), S(stabZcmd * iz), S(flapZcmd * iz), S(rudXcmd * ix), S(stabXcmd * ix), S(flapXcmd * ix), S(probe[0]), S(probe[1]), S(probe[2]), S(probe[3]), S(probe[4]), S(probe[5]), S(probe[6]), S(probe[7]), S(grpD[0,0]), S(grpD[0,1]), S(grpD[0,2]), S(grpD[1,0]), S(grpD[1,1]), S(grpD[1,2]), S(grpD[2,0]), S(grpD[2,1]), S(grpD[2,2]), S(grpD[3,0]), S(grpD[3,1]), S(grpD[3,2]) }
                .Concat(lohi)));
            used++;
        }
        Console.Error.WriteLine($"# fcsmom: {used} of {n} rows -> {outPath}; effectors: {string.Join(" ", names)}");
    }

    static string S(float x) => x.ToString("0.####", IC);
    public static float ProbeZ = 400f; public static float DebugT = -1f; public static int ProbeIters = 3;   // deg/s^2 of extra yaw asked of the allocator
    public static float ProbeX = 1500f;   // deg/s^2 of extra roll

    static float LocalAoA(SurfaceEffector se, Airframe A, Plant P)
    {
        var part = (AeroPart)se.cs.attachedSurface;
        Vector3 v = part.rb.velocity - A.ac.windVelocity;
        if (se.kind == SurfaceKind.Stabilator) v = Downwash.Apply(v, P.q, A.ac.rb.velocity - A.ac.windVelocity);
        Vector3 vl = Quaternion.Inverse(part.liftNormal.rotation) * v;
        return -Mathf.Atan2(vl.y, vl.z) * Mathf.Rad2Deg;
    }
}
