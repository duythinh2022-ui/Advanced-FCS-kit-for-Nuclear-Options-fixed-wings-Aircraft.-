// Rigid-body plant flying the Airframe with Nuclear Option's own force model:
//   AeroJob_Math per AeroPart (lift/drag charts, parasitic 0.25*rho*v^2*dragArea, force at the part's rigidbody COM
//   plus torque from centerOfLift), ControlSurfaceJob_Math servos, Turbofan nozzle slew / thrust at thrustTransform.
// The FCS is the real FcsController (1.8.12 sources) and its SurfacePostfix / TvcPitch hooks are reproduced.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

public enum Gyro { None, Unity, Full }

internal sealed class Plant
{
    public static bool FlightAssist = true;   // 4.3.0 bench: false = Stability Assist off (MPO)
    public static float AbSmoothing = 2f;   // game Afterburner.smoothing
    public static float SpoolScale = 1f;    // FCS ThrottleResponseScale (Registry: spool rate and AB smoothing)
    public static float EngDmg = -1f;       // condition of engine _L from the start (-1 = healthy)
    public static bool ForceClamp = true;       // 31 Oct calibration: the game's per-part aero force clamp (30 m |v|)
    public static bool CamberMis = true;        // negAoA calibration: Mis* multiplier kept on a camber host's recomputed lift (false = the 31 Oct bench, which dropped it while the surface was deflected)

    public static System.Diagnostics.Stopwatch TickSw = new System.Diagnostics.Stopwatch(); public static long TickN; public static System.Collections.Generic.List<float> TickUs = new System.Collections.Generic.List<float>(); internal static bool JoinAfterTick = true; public static double[] PhSum = new double[13]; public static double PhVmc;
    public readonly Airframe A;
    public FcsController fcs;
    public StockFbw stock;
    public Gyro gyro = Gyro.Unity;
    public float dt = 1f / 60f;   // 31 Oct calibration: the game's physics tick is 60 Hz (the FCS logs step 16.67 ms); was 0.02
    // state
    public Vector3 pCg;        // world CG position
    public static readonly float[] GrpM = new float[8];   // 2.4.7 probe: pitch accel by group (stab, fin+rudder, flaperon, wing panel, body, thrust, stabL, stabR), deg/s^2
    public Vector3 vCg;        // world CG velocity
    public Quaternion q = Quaternion.identity;
    public Vector3 w;          // world angular velocity (rad/s, Unity convention)
    public float throttle = 1f;
    public float time;
    public Vector3 lastForce, lastTorque;
    public Dictionary<AeroPart, (Vector3 f, Vector3 m, float aoa, float cl)> PartDump;   // negAoA calibration: per-part aero force / moment of the last Forces()
    public Func<Plant, (Vector3, Vector3)> ext;
    public Action<Vector3, Vector3> preIntegrate;   // damage model: joint loads at this pose, rates and forces

    public Plant(Airframe a) { A = a; }
    public static bool GameTransonic = true;    // 2.2.3 EM; 31 Oct calibration: on by default (the game's AeroJob_Math applies it)
    // leaf calibration (3 Nov 2026): bench-only yaw-stiffness correction above 35-45 deg body AoA. Per-AoA-band regressions of
    // the 3 Nov logs (calib/derivs.py) put the game's Cn_beta at +0.27 / +0.05 / -0.86 deg/s^2 per deg of sideslip per 1e4 Pa
    // at 40-50 / 50-60 / 60-70 deg against the plant's +0.78 / +0.36 / -0.46 (every other yaw derivative within 10-15 %); no
    // part multiplier can produce that (the fins and rudders are already near-neutral there). DCnbHi adds that difference as a
    // body-yaw moment, DCnbHi x beta(deg) x qbar/1e4 (deg/s^2), blended in over Airframe.MisHiA0..MisHiA1. 0 = off.
    public static float DCnbHi = -0.4f;
    // 3 Nov 2026, 20:40 (push-over pitch-hitch calibration): the game yaws harder than the quasi-steady plant while the AoA moves
    // fast at 8-45 deg - an AoA-rate x yaw-rate term, the same sign whether the AoA falls (push sweep: extra yaw deceleration) or rises.
    // Equation error on the 3 Nov logs: coefficient +0.12..+0.40 per band falling and rising, ~0 above 45 deg and in slow flight;
    // one global +0.18 fitted on 14:45 / 17:07 / 20:12 cuts held-out (15:25 / 17:43 / 20:15) push-sweep yaw error 5.6 -> 4.5 deg/s2 rms,
    // bias +0.52 -> +0.10. rdot += DYawAdotR x adot(deg/s) x r(rad/s) x sign(alpha), faded in over |alpha| YawAdotA0..A1 and out
    // over A2..A3. adot from the plant's own CG AoA (finite difference, 3-tick low-pass). 0 = off.
    public static float DYawAdotR = 0f, YawAdotA0 = 5f, YawAdotA1 = 10f, YawAdotA2 = 40f, YawAdotA3 = 50f;
    float aPrevDY = float.NaN, aDotDY;
    Dictionary<AeroPart, float> cdMin;

    public static float Density(float h) => 1.225f * Mathf.Pow(Mathf.Max(0.01f, 1f - 2.2558e-5f * h), 4.2559f);

    internal void Pose()
    {
        A.root.localRotation = q;
        A.root.localPosition = pCg - q * A.cgB;
        foreach (var b in A.bodies)
        {
            var com = pCg + q * (b.comB - A.cgB);
            b.rb.worldCenterOfMass = com;
            b.rb.velocity = vCg + Vector3.Cross(w, com - pCg);
            b.rb.angularVelocity = w;
            b.rb.rotation = b.part.transform.rotation;
        }
        A.ac.speed = A.ac.rb.velocity.magnitude;
        A.ac.airDensity = Density(pCg.y);
    }

    public void Init(float alt, float tas, float alphaDeg, float thetaDeg)
    {
        q = Quaternion.Euler(-thetaDeg, 0f, 0f);
        pCg = new Vector3(0f, alt, 0f);
        float gam = (thetaDeg - alphaDeg) * Mathf.Deg2Rad;
        vCg = new Vector3(0f, Mathf.Sin(gam), Mathf.Cos(gam)) * tas;
        w = Vector3.zero;
        Pose();
    }

    public void CreateFcs()
    {
        fcs = new FcsController(A.ac); Registry.Current = fcs;
    }

    // SurfacePostfix + ControlSurfaceJob_Math
    void Surfaces()
    {
        foreach (var cs in A.surfaces)
        {
            ref var f = ref cs.job;
            f.servoSpeed = cs.servoSpeed;
            var s = fcs?.surfaces.FirstOrDefault(e => e.cs == cs);
            if (fcs != null && fcs.Engaged && s != null && s.active)
            {
                if (!s.overriding)
                {
                    f.currentPitch = f.currentPitch + f.currentRoll + f.currentYaw;
                    f.currentRoll = 0f; f.currentYaw = 0f; s.overriding = true;
                }
                f.pitchRange = s.max; f.rollRange = 0f; f.yawRange = 0f;
                f.controlInputs.pitch = Mathf.Clamp(s.cmdOut / s.max, -1f, 1f);
                f.controlInputs.roll = 0f; f.controlInputs.yaw = 0f;
            }
            if (stock != null)
            {
                var ci = A.ac.inputs;
                float tgt = ci.pitch * cs.pitchRange + ci.roll * cs.rollRange + ci.yaw * cs.yawRange;
                float lim = Mathf.Abs(cs.pitchRange) + Mathf.Abs(cs.rollRange) + Mathf.Abs(cs.yawRange);
                f.currentPitch += Mathf.Clamp(tgt - f.currentPitch, -f.servoSpeed * dt, f.servoSpeed * dt);
                f.currentPitch = Mathf.Clamp(f.currentPitch, -lim, lim);
                cs.visibleMesh.transform.localRotation = f.restingRotation * Quaternion.AngleAxis(f.currentPitch, Vector3.right);
                continue;
            }
            float v = f.controlInputs.pitch * f.pitchRange - f.currentPitch;
            f.currentPitch += Mathf.Clamp(v, -f.servoSpeed * dt, f.servoSpeed * dt);
            f.currentPitch = Mathf.Clamp(f.currentPitch, -Mathf.Abs(f.pitchRange), Mathf.Abs(f.pitchRange));
            float ang = f.currentPitch + f.currentRoll + f.currentYaw;
            cs.visibleMesh.transform.localRotation = f.restingRotation * Quaternion.AngleAxis(ang, Vector3.right);
        }
    }

    float Chart(float[] c, float idx)
    {
        if (idx <= 0f) return c[0];
        if (idx >= 127f) return c[127];
        int i = (int)Math.Floor(idx); int j = (int)Math.Ceiling(idx);
        return Mathf.Lerp(c[i], c[j], idx % 1f);
    }

    readonly Dictionary<AeroPart, ControlSurface> camberHost = new Dictionary<AeroPart, ControlSurface>();
    bool camberBuilt;
    void BuildCamber()
    {
        camberBuilt = true;
        foreach (var cs in A.surfaces)
        {
            string hn = Camber.HostOf(cs.name);
            if (hn == null) continue;
            foreach (var b in A.bodies) if (b.part.name.Replace("Aryx_KingRaptor_", "") == hn) { camberHost[b.part] = cs; break; }
        }
    }

    public static float ProbeT = float.Parse(System.Environment.GetEnvironmentVariable("PROBE_T") ?? "-99", System.Globalization.CultureInfo.InvariantCulture); bool probed; public float SimT;
    public void Step()
    {
        System.Array.Clear(GrpM, 0, GrpM.Length);
        if (!camberBuilt) BuildCamber();
        Time.fixedDeltaTime = dt; Time.deltaTime = dt;
        Pose();
        // ---- FCS (ControlsFilter.Filter prefix)
        A.ac.inputs.throttle = throttle;
        if (stock != null) stock.Filter(A.ac.speed, A.ac.airDensity, vCg, q, w, A.ac.inputs, dt);
        else { long tk0 = System.Diagnostics.Stopwatch.GetTimestamp(); TickSw.Start(); fcs.Tick(A.ac.inputs, FlightAssist); long tv0 = System.Diagnostics.Stopwatch.GetTimestamp(); fcs.VmcApply(A.ac.inputs, dt); long tvT = System.Diagnostics.Stopwatch.GetTimestamp() - tv0; TickSw.Stop(); TickN++; float tkUs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - tk0) * 1e6 / System.Diagnostics.Stopwatch.Frequency); fcs.NoteTickTime(tkUs, tvT); if (SimT > 2f) PhVmc += FcsController.TicksToUs(tvT); if (SimT > 2f) { TickUs.Add(tkUs); PhSum[0] += FcsController.TicksToUs(fcs.phRebuild); PhSum[1] += FcsController.TicksToUs(fcs.phAlloc); PhSum[2] += FcsController.TicksToUs(fcs.phKick); PhSum[3] += FcsController.TicksToUs(fcs.phRow); PhSum[4] += FcsController.TicksToUs(fcs.phLaws); PhSum[5] += FcsController.TicksToUs(fcs.phAsym); PhSum[6] += FcsController.TicksToUs(fcs.phAsymMom); PhSum[7] += FcsController.TicksToUs(fcs.phSlip); PhSum[8] += FcsController.TicksToUs(fcs.phSweep); PhSum[9] += FcsController.TicksToUs(fcs.phEnv); PhSum[10] += FcsController.TicksToUs(fcs.phMass); PhSum[11] += FcsController.TicksToUs(fcs.phFlaps); PhSum[12] += FcsController.TicksToUs(fcs.phGround); } if (JoinAfterTick) fcs.EstJoin(); }
        // 2.4.17 bench probe: PROBE_T=<s> prints every effector's moment samples (body accel deg/s^2: roll x, pitch y, yaw z) once
        if (ProbeT > -90f && !probed && SimT >= ProbeT) { probed = true; fcs.ProbeDump(); }
        // ---- surfaces
        Surfaces();
        // ---- engines: Turbofan.FixedUpdate with the TvcPitch transpiler
        EnginesStep();
        // ---- airbrakes (open amount), then every force at this pose
        AirbrakesStep();
        Forces(out Vector3 F, out Vector3 M);
        F += Physics.gravity * A.mass;
        lastForce = F; lastTorque = M;
        preIntegrate?.Invoke(F, M);
        Integrate(F, M);
    }

    // engine state for this tick: nozzle slew, RPM / spool, afterburner amount, nozzle thrust (no forces yet)
    void EnginesStep()
    {
        foreach (var tf in A.engines)
        {
            float pin = stock != null ? A.ac.inputs.pitch : fcs.tvcPitchInput;
            float n5 = -Mathf.Clamp(pin * tf.thrustVectoringGain.x, -1f, 1f) * tf.thrustVectoring.x
                       - Mathf.Clamp(A.ac.inputs.roll * tf.thrustVectoringGain.z, -1f, 1f) * tf.thrustVectoring.z;
            float slew = 70f * Plugin.ActuatorScale * dt;
            tf.nozzleAngles.x += Mathf.Clamp(n5 - tf.nozzleAngles.x, -slew, slew);
            if (A.ac.speed > tf.thrustVectoringMaxAirspeed) tf.nozzleAngles = Vector3.zero;
            foreach (var vt in tf.vectoringTransforms) vt.localEulerAngles = new Vector3(Mathf.Clamp(tf.nozzleAngles.x + tf.nozzleAngles.z, -20f, 20f), tf.nozzleAngles.y, 0f);
            float spd = A.ac.speed;
            float sf = SpeedThrust(tf, spd);
            float thrE = stock != null ? throttle : A.ac.inputs.throttle;   // 2.0.12: the FCS may lower the throttle input (VMC)
            // the game's Turbofan.FixedUpdate (IL): target RPM = Lerp(minRPM, minRPM + (maxRPM - minRPM)(0.5 condition + 0.5), remapped
            // throttle), spool-rate limited; spoolRatio = clamp01((RPM - 0.85 minRPM) / (maxRPM - minRPM)); thrust = (static x dyn - loss)
            // x spoolRatio; afterburner only above 85 % maxRPM, amount = Lerp(amount, target, smoothing x dt)
            float trE = Mathf.Clamp01((thrE - tf.remapX) / Mathf.Max(1e-3f, tf.remapY - tf.remapX));
            float rpmT = tf.operable ? Mathf.Lerp(tf.minRPM, tf.minRPM + (tf.maxRPM - tf.minRPM) * (0.5f * tf.condition + 0.5f), trE) : 0f;
            tf.rpm += Mathf.Clamp(rpmT - tf.rpm, -tf.spoolRate * SpoolScale, tf.spoolRate * SpoolScale) * dt;
            float spR = tf.operable ? Mathf.Clamp01((tf.rpm - 0.85f * tf.minRPM) / (tf.maxRPM - tf.minRPM)) : 0f;
            float mil = tf.operable ? Mathf.Max(0f, tf.staticThrust * sf - tf.parasitic) * spR : 0f;
            foreach (var n in tf.nozzles)
            {
                float abTgt = (tf.operable && tf.rpm / tf.maxRPM > 0.85f) ? Mathf.InverseLerp(n.abStart, 1f, thrE) : 0f;
                if (AbSmoothing <= 0f) n.abAmt = abTgt; else n.abAmt = Mathf.Lerp(n.abAmt, abTgt, AbSmoothing * SpoolScale * dt);
                float ab = n.abThrust * n.abAmt * Mathf.Clamp(A.ac.airDensity, 0.4f, 1f);
                n.totalThrust = mil + ab;
            }
        }
    }

    // airbrake open amounts, stepped as the game's Airbrake.Update with the FCS hook (open toward AirbrakeOpenTarget, stop there)
    void AirbrakesStep()
    {
        if (fcs == null || A.airbrakes.Count == 0) return;
        float want = fcs.AirbrakeOpenTarget;
        foreach (var ab in A.airbrakes)
        {
            bool opening = want > 0.001f && ab.openAmount < want;
            ab.openAmount = Mathf.Clamp01(ab.openAmount + (opening ? 1f : -1f) * ab.openSpeed * dt);
            ab.openAmount = opening ? Mathf.Min(ab.openAmount, want) : Mathf.Max(ab.openAmount, Mathf.Min(want, ab.openAmount + ab.openSpeed * dt));
            fcs.airbrakeOpenNow = ab.openAmount;
            if (ab.tr != null) ab.tr.localRotation = ab.baseRot * Quaternion.Euler(ab.openAmount * ab.maxAngle, 0f, 0f);
        }
    }

    // Every force and moment (about the CG, world frame) at the current pose, surface / nozzle / slat / airbrake state and
    // nozzle thrusts: thrust, part aero (AeroJob_Math + the FCS's camber / downwash hooks), airbrake drag, ext. No gravity,
    // no integration. Calibration (eqerr) poses the plant at a logged state and calls this directly.
    public void Forces(out Vector3 F, out Vector3 M)
    {
        F = Vector3.zero; M = Vector3.zero;
        if (!camberBuilt) BuildCamber();
        foreach (var tf in A.engines)
        {
            foreach (var n in tf.nozzles)
            {
                var fr = n.thrustTransform.forward * n.totalThrust;
                F += fr; M += Vector3.Cross(n.thrustTransform.position - pCg, fr); GrpM[5] += -Vector3.Dot(Vector3.Cross(n.thrustTransform.position - pCg, fr), q * Vector3.right) / (float)A.Itot[0, 0] * 57.29578f;
            }
        }
        // ---- aero: AeroJob_Math for each part
        float rho = A.ac.airDensity;
        if (cdMin == null) { cdMin = new Dictionary<AeroPart, float>(); foreach (var b0 in A.bodies) { var a0 = b0.part; float mn = float.MaxValue; if (a0.airfoil >= 0) for (float aa = -0.35f; aa <= 0.35f; aa += 0.005f) mn = Mathf.Min(mn, Chart(A.dragCharts[a0.ChartId], aa * 20.37185f + 64f)); cdMin[a0] = mn == float.MaxValue ? 0f : mn; } }
        Vector3 stabFL = Vector3.zero, stabFR = Vector3.zero, stabPL = Vector3.zero, stabPR = Vector3.zero; int nStab = 0;
        // leaf calibration (3 Nov): body AoA for the high-AoA multiplier blend (Airframe.Mis(name, alpha))
        float aBody; { Vector3 vbw = Quaternion.Inverse(q) * (vCg - A.ac.windVelocity); aBody = Mathf.Atan2(-vbw.y, vbw.z) * Mathf.Rad2Deg; }
        foreach (var b in A.bodies)
        {
            var ap = b.part;
            Vector3 v = b.rb.velocity - A.ac.windVelocity;
            float v2 = v.sqrMagnitude;
            if (v2 < 1e-4f) continue;
            var lt = ap.liftNormal;
            // 2.0.8: stab downwash (game: DownwashForce adds the airfoil-force difference; parasitic stays on v)
            Vector3 vf = Downwash.IsStabName(ap.name) ? Downwash.Apply(v, q, A.ac.rb.velocity - A.ac.windVelocity) : v;
            Vector3 v6 = Quaternion.Inverse(lt.rotation) * vf;
            Vector3 nrm = Vector3.Cross(vf, lt.right).normalized;
            float a = Mathf.Atan2(v6.y, v6.z);
            float cl, cd;
            if (ap.airfoil < 0) { cl = 1.8f * Mathf.Sin(5f * a); cd = 1.5f * (1f - Mathf.Cos(2f * a)) + 0.02f; }
            else { float idx = a * 20.37185f + 64f; cl = Chart(A.liftCharts[ap.ChartId], idx); cd = Chart(A.dragCharts[ap.ChartId], idx); }
            float eff = ap.wingEffectiveness;
            float n8 = (ap.dragArea + ap.wingArea * 0.1f) * (1f - eff);
            float n9 = 0.5f * rho * v2 * 0.5f * (ap.dragArea + n8);
            float n10 = cd * rho * v2 * 0.5f * ap.wingArea * eff;
            // 2.2.3: the game's transonic drag factor (AeroJob_Math, Mach 0.8-1.2) and the buffed profile's DragRelief
            float vmag = Mathf.Sqrt(v2), mach = vmag / DragReliefMath.SpeedOfSound(b.rb.worldCenterOfMass.y);
            float tr = GameTransonic ? DragReliefMath.Transonic(mach) : 1f;
            Vector3 drag = vf.Equals(v) ? -v.normalized * (n10 + n9) * tr : (-vf.normalized * n10 - v.normalized * n9) * tr;
            if (Plugin.DragReliefAoA > 0f || Plugin.DragReliefTransonic > 0f)
            {
                float foilMin = Mathf.Min(cdMin[ap], cd) * rho * v2 * 0.5f * ap.wingArea * eff;
                float dF = GameTransonic ? DragReliefMath.Relief(n9, n10, foilMin, mach, a * Mathf.Rad2Deg) : 0f;
                if (dF > 0f) drag += v * (dF / vmag);
            }
            Vector3 lift = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * eff * Airframe.Mis(ap.name, aBody));
            Vector3 f = lift + drag;
            // 31 Oct calibration: the game clamps each part's aero force to 30 x mass x |v| (AeroJob_Math: ClampMagnitude(F, |v| x 0.5 x mass x 60))
            if (ForceClamp) f = Vector3.ClampMagnitude(f, vmag * 0.5f * ap.mass * 60f);
            // 2.1.11 flap-camber coupling (game: CamberForce): host part rotated by K x its control surface's deflection
            if (camberHost.TryGetValue(ap, out var csH))
            {
                float kH = Camber.KForName(csH.name);
                if (kH > 0f)
                {
                    float dH = Camber.DeflectionOf(csH.visibleMesh.transform, csH.job.restingRotation);
                    if (Mathf.Abs(dH) > 0.01f)
                    {
                        Quaternion r1 = Quaternion.AngleAxis(kH * dH, csH.visibleMesh.transform.rotation * Vector3.right) * lt.rotation;
                        Vector3 v7 = Quaternion.Inverse(r1) * vf;
                        Vector3 nrm1 = Vector3.Cross(vf, r1 * Vector3.right).normalized;
                        float a1 = Mathf.Atan2(v7.y, v7.z);
                        float cl1, cd1;
                        if (ap.airfoil < 0) { cl1 = 1.8f * Mathf.Sin(5f * a1); cd1 = 1.5f * (1f - Mathf.Cos(2f * a1)) + 0.02f; }
                        else { float idx1 = a1 * 20.37185f + 64f; cl1 = Chart(A.liftCharts[ap.ChartId], idx1); cd1 = Chart(A.dragCharts[ap.ChartId], idx1); }
                        float qSH = rho * v2 * 0.5f * ap.wingArea * eff;
                        // negAoA calibration (31 Oct): keep the bench's lift multiplier on the cambered lift too (it was dropped here, so Mis*
                        // never reached a host part while its surface was deflected - Vstab under FinCamberCoupling, Wingmid/WingRear under WingCamberCoupling)
                        Vector3 dLift = -nrm1 * (cl1 * qSH * (CamberMis ? Airframe.Mis(ap.name, aBody) : 1f)) - lift;
                        Vector3 dDrag = -vf.normalized * ((cd1 - cd) * qSH);
                        f += dLift + dDrag;
                    }
                }
            }
            F += f;
            Vector3 mPart = Vector3.Cross(b.rb.worldCenterOfMass - pCg, f);
            if (ap.centerOfLift != Vector3.zero) mPart += Vector3.Cross(f, -(lt.rotation * ap.centerOfLift));
            M += mPart;
            if (ap.name.Contains("Elevator")) { if (ap.name.EndsWith("_L")) { stabFL = f; stabPL = b.rb.worldCenterOfMass; } else { stabFR = f; stabPR = b.rb.worldCenterOfMass; } nStab++; }
            if (PartDump != null) PartDump[ap] = (f, mPart, a * Mathf.Rad2Deg, cl);
            { float mp = -Vector3.Dot(mPart, q * Vector3.right) / (float)A.Itot[0, 0] * 57.29578f; string nn = ap.name; int gi = nn.Contains("Elevator") ? 0 : (nn.Contains("Rudder") || nn.Contains("Vstab")) ? 1 : (nn.Contains("Flap") || nn.Contains("Aileron")) ? 2 : (nn.Contains("WingFore") || nn.Contains("Wingmid") || nn.Contains("WingRear") || nn.Contains("Wingtip")) ? 3 : 4; GrpM[gi] += mp; if (gi == 0) { if (nn.EndsWith("_L")) GrpM[6] += mp; else GrpM[7] += mp; } }
        }
        // negAoA calibration (31 Oct): per-axis stabilator factor. MisStab (0.72) was fitted on the differential-stab roll power
        // and under-states the symmetric pitch power by ~1.4x (per-AoA-band regressions on the 30 and 31 Oct logs); here the
        // symmetric half of the two stab forces is rescaled to MisStabPitch (plant only, the FCS's own tables are untouched).
        if (DYawAdotR != 0f)
        {
            if (float.IsNaN(aPrevDY)) aPrevDY = aBody;
            float ad = (aBody - aPrevDY) / Mathf.Max(1e-4f, dt); aPrevDY = aBody;
            aDotDY += (ad - aDotDY) * (dt / (0.05f + dt));
            float aa = Mathf.Abs(aBody);
            float u = Mathf.Clamp01((aa - YawAdotA0) / Mathf.Max(1f, YawAdotA1 - YawAdotA0)) * (1f - Mathf.Clamp01((aa - YawAdotA2) / Mathf.Max(1f, YawAdotA3 - YawAdotA2)));
            float rRad = Vector3.Dot(w, q * Vector3.up);   // body yaw rate, rad/s (r positive = nose right)
            float rdd = DYawAdotR * aDotDY * rRad * Mathf.Sign(aBody) * u;   // deg/s^2
            M += q * Vector3.up * (rdd * Mathf.Deg2Rad * (float)A.Itot[1, 1]);
        }
        if (DCnbHi != 0f)
        {
            Vector3 vbw = Quaternion.Inverse(q) * (vCg - A.ac.windVelocity); float tasB = vbw.magnitude;
            if (tasB > 1f)
            {
                float betaB = Mathf.Asin(Mathf.Clamp(vbw.x / tasB, -1f, 1f)) * Mathf.Rad2Deg;
                float u = Mathf.Clamp01((Mathf.Abs(aBody) - Airframe.MisHiA0) / Mathf.Max(1f, Airframe.MisHiA1 - Airframe.MisHiA0));
                float qb = 0.5f * rho * tasB * tasB / 1e4f;
                float rdd = DCnbHi * betaB * qb * u;   // deg/s^2 of body yaw (r positive = nose right)
                M += q * Vector3.up * (rdd * Mathf.Deg2Rad * (float)A.Itot[1, 1]);
            }
        }
        if (nStab == 2 && Airframe.MisStabPitch >= 0f && Mathf.Abs(Airframe.MisStabPitch - Airframe.MisStab) > 1e-4f)
        {
            Vector3 sym = 0.5f * (stabFL + stabFR);
            Vector3 dF = sym * (Airframe.MisStabPitch / Airframe.MisStab - 1f);
            F += 2f * dF; M += Vector3.Cross(stabPL - pCg, dF) + Vector3.Cross(stabPR - pCg, dF);
            if (PartDump != null) foreach (var k in PartDump.Keys.ToList()) if (k.name.Contains("Elevator")) { var v = PartDump[k]; PartDump[k] = (v.f + dF, v.m + Vector3.Cross((k.name.EndsWith("_L") ? stabPL : stabPR) - pCg, dF), v.aoa, v.cl); }
        }
        // 2.2.16: airbrake drag (the game's Airbrake force at the open amount)
        if (fcs != null && A.airbrakes.Count > 0)
        {
            foreach (var ab in A.airbrakes)
            {
                if (ab.openAmount <= 0f || ab.part.detached) continue;
                var rbA = ab.part.rb; Vector3 vA = rbA.velocity - A.ac.windVelocity;
                Vector3 fA = -vA.normalized * (ab.dragAmount * rho * vA.sqrMagnitude * ab.openAmount);
                F += fA; M += Vector3.Cross(rbA.worldCenterOfMass - pCg, fA);
            }
        }
        if (ext != null) { var fe = ext(this); F += fe.Item1; M += fe.Item2; }
    }

    // body angular acceleration (world frame) for a moment M at the current pose and rates: I^-1 (M - w x I w)
    public Vector3 AngAccel(Vector3 M)
    {
        var R = Airframe.Rot(q);
        var Iw = Xform(R, A.Itot);
        Vector3 gyroT = Vector3.zero;
        if (gyro != Gyro.None)
        {
            var Ig = Xform(R, gyro == Gyro.Full ? A.Itot : A.Ipa);
            gyroT = Vector3.Cross(w, Mul(Ig, w));
        }
        return Solve(Iw, M - gyroT);
    }

    void Integrate(Vector3 F, Vector3 M)
    {
        // ---- integrate (PhysX-like semi-implicit Euler)
        var R = Airframe.Rot(q);
        var Iw = Xform(R, A.Itot);
        Vector3 gyroT = Vector3.zero;
        if (gyro != Gyro.None)
        {
            var Ig = Xform(R, gyro == Gyro.Full ? A.Itot : A.Ipa);
            gyroT = Vector3.Cross(w, Mul(Ig, w));
        }
        Vector3 wdot = Solve(Iw, M - gyroT);
        vCg += F / A.mass * dt;
        pCg += vCg * dt;
        w += wdot * dt;
        float wm = w.magnitude;
        if (wm > 1e-9f) q = (Quaternion.AngleAxis(wm * dt * Mathf.Rad2Deg, w / wm) * q).normalized;
        time += dt;
        Time.time = time;
    }

    static float SpeedThrust(Turbofan tf, float s)
    {
        var k = tf.speedThrust;
        if (k == null || k.Length == 0) return 1f;
        if (s <= k[0][0]) return k[0][1];
        for (int i = 0; i < k.Length - 1; i++) if (s <= k[i + 1][0]) return Mathf.Lerp(k[i][1], k[i + 1][1], (s - k[i][0]) / (k[i + 1][0] - k[i][0]));
        return k[k.Length - 1][1];
    }
    static double[,] Xform(double[,] R, double[,] I)
    {
        var o = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double s = 0; for (int a = 0; a < 3; a++) for (int c = 0; c < 3; c++) s += R[i, a] * I[a, c] * R[j, c]; o[i, j] = s; }
        return o;
    }
    static Vector3 Mul(double[,] I, Vector3 v) => new Vector3((float)(I[0, 0] * v.x + I[0, 1] * v.y + I[0, 2] * v.z), (float)(I[1, 0] * v.x + I[1, 1] * v.y + I[1, 2] * v.z), (float)(I[2, 0] * v.x + I[2, 1] * v.y + I[2, 2] * v.z));
    static Vector3 Solve(double[,] A, Vector3 b)
    {
        double det = A[0, 0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) - A[0, 1] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0]) + A[0, 2] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]);
        double[,] inv = new double[3, 3];
        inv[0, 0] = (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) / det; inv[0, 1] = (A[0, 2] * A[2, 1] - A[0, 1] * A[2, 2]) / det; inv[0, 2] = (A[0, 1] * A[1, 2] - A[0, 2] * A[1, 1]) / det;
        inv[1, 0] = (A[1, 2] * A[2, 0] - A[1, 0] * A[2, 2]) / det; inv[1, 1] = (A[0, 0] * A[2, 2] - A[0, 2] * A[2, 0]) / det; inv[1, 2] = (A[0, 2] * A[1, 0] - A[0, 0] * A[1, 2]) / det;
        inv[2, 0] = (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]) / det; inv[2, 1] = (A[0, 1] * A[2, 0] - A[0, 0] * A[2, 1]) / det; inv[2, 2] = (A[0, 0] * A[1, 1] - A[0, 1] * A[1, 0]) / det;
        return Mul(inv, b);
    }

    // aero-frame rates (deg/s): p roll right, q nose up, r nose right
    public Vector3 RatesDeg()
    {
        var l = Quaternion.Inverse(q) * w;
        return new Vector3(-l.z, -l.x, l.y) * Mathf.Rad2Deg;
    }
}
