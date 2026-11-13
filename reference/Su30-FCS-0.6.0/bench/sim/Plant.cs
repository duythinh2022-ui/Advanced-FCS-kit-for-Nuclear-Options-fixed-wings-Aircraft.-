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
    public static float AbSmoothing = 2f;   // game Afterburner.smoothing (0 = instantaneous, the old bench)
    public static float SpoolScale = 1f;    // FCS ThrottleResponseScale (Registry: spoolRate and AB smoothing)

    public static bool BenchMpo = System.Environment.GetEnvironmentVariable("BENCH_MPO") == "1";
    public static System.Diagnostics.Stopwatch TickSw = new System.Diagnostics.Stopwatch(); public static long TickN; internal static bool JoinAfterTick = true;   // Su-30 0.2.0 bench: collect the estimator job right after the tick, outside the timer (in game it overlaps rendering)
    public readonly Airframe A;
    public FcsController fcs;
    public StockFbw stock;
    public Gyro gyro = Gyro.Unity;
    public float dt = 1f / 60f;   // 31 Oct calibration: the game's physics tick is 60 Hz (the FCS logs step 16.67 ms); was 0.02
    // state
    public Vector3 pCg;        // world CG position
    public Vector3 vCg;        // world CG velocity
    public Quaternion q = Quaternion.identity;
    public Vector3 w;          // world angular velocity (rad/s, Unity convention)
    public float throttle = 1f;
    public float time;
    public Vector3 lastForce, lastTorque;
    public Func<Plant, (Vector3, Vector3)> ext;
    public Dictionary<AeroPart, (Vector3 f, Vector3 m, float aoa, float cl)> PartDump;   // damage model: per-part aero force / moment of the last Forces()
    public Action<Vector3, Vector3> preIntegrate;   // damage model: joint loads at this pose, rates and forces
    public bool EvalOnly;
    public Dictionary<PartBody, Vector3> PartForce;   // per-part aero force of the last step (structural-load estimate)   // Ifrit authority maps: forces/moments at the current pose, no controller, no integration
    public void SetState(Quaternion qq, Vector3 v, Vector3 ww) { q = qq; vCg = v; w = ww; }

    public Plant(Airframe a) { A = a; ThrustLine.Patched = 1; }   // Su-30 0.1.5: the game transpiler is in place in game
    // leaf calibration (3 Nov 2026): bench-only lateral stiffness corrections above body AoA Airframe.MisHiA0..MisHiA1 (blended):
    // DClbHi = roll per deg of sideslip per 1e4 Pa, DCnbHi = body yaw per deg of sideslip per 1e4 Pa (deg/s^2). 0 = off.
    public static float DClbHi = 0f, DCnbHi = 0f;
    public static bool GameTransonic = true;    // 2.2.3 EM; 31 Oct calibration: on by default (the game's AeroJob_Math applies it)
    public static bool ForceClamp = true;       // 31 Oct calibration: the game's per-part aero force clamp (30 m |v|, AeroJob_Math)
    public static float ThrustOverride = -1f;
    public static float ThrustLineDy = 0f;      // 9 Nov 2026 Su-30 calibration: thrust force point's height above the bench CG (m; the game's transpiler puts it at the FCS's CG estimate)
    public static float NozzleArmScale = 1f;    // 31 Oct calibration: bench-only factor on the nozzle's lever arm about the CG (thrust moment only)   // 31 Oct calibration (eqerr): per-nozzle thrust (N) from the logged T/W instead of the engine model
    Dictionary<AeroPart, float> cdMin;

    public static float Density(float h) => 1.225f * Mathf.Pow(Mathf.Max(0.01f, 1f - 2.2558e-5f * h), 4.2559f);

    public void Pose()
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
        // mirror Registry's Ifrit airframe setup (0.3.0): servo speed and split limit
        if (A.ifrit)
            foreach (var cs in A.surfaces)
            {
                if (Plugin.SurfaceServoSpeed > 0f) { cs.servoSpeed = Plugin.SurfaceServoSpeed; cs.job.servoSpeed = cs.servoSpeed; }
                if (cs.maxSplit > 0f && Plugin.SplitRange > 0f) { cs.maxSplit = Plugin.SplitRange; cs.job.maxSplit = cs.maxSplit; }
            }
        fcs = new FcsController(A.ac); Registry.Current = fcs;
        { var sgS = System.Environment.GetEnvironmentVariable("BENCH_SLIPSIGN"); if (sgS != null && float.TryParse(sgS, out float sgV)) { var fS = fcs.GetType().GetField("slipSignR", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fS?.SetValue(fcs, sgV); } }   // latch the stiffness probe's sign (flights that start above 10 deg AoA)
        if (A.ifrit)
        {
            var f = fcs;
            SplitYaw = cs => { foreach (var se in f.splits) if (se.cs == cs) return f.Engaged ? se.JobYawPhys(Mathf.Min(se.Phys(se.cmdOut), se.BudgetSplit())) : 0f; return 0f; };
            TvcRollIn = tf => f.tvcRollInput;
            TvcYawIn = tf => f.tvcYawInput;   // Revoker: the nozzle yaw axis (0 on the Ifrit)
        }
    }

    // bench 0.2.7: surface command transport delay (frames), to mimic the game's FCS -> job -> physics lag
    public static int CmdDelayFrames = 0;
    readonly Dictionary<ControlSurface, Queue<float>> cmdQ = new Dictionary<ControlSurface, Queue<float>>();
    float DelayCmd(ControlSurface cs, float v)
    {
        if (CmdDelayFrames <= 0) return v;
        if (!cmdQ.TryGetValue(cs, out var q)) { q = new Queue<float>(); for (int i = 0; i < CmdDelayFrames; i++) q.Enqueue(v); cmdQ[cs] = q; }
        q.Enqueue(v); return q.Dequeue();
    }
    // SurfacePostfix + ControlSurfaceJob_Math
    // Su-30 0.1.8: the mod's CanardSurface script (rotates Canard_*_hinge by body AoA 1:1, +-30 deg, 90 deg/s, leading edge down for
    // positive AoA) runs in the game alongside the FCS: every Su-30 log shows the canard's zero-deflection flow angle at
    // max(0, AoA - 30). Modelled here on the hinge, in the same sense as the FCS's trailing-edge-up deflection.
    public static bool CanardSchedule = System.Environment.GetEnvironmentVariable("CANSCHED") != null;
    public static float CanardSchedOff = 0f;   // 1 = the FCS switched it off (the angle returns to 0 at 90 deg/s)
    readonly Dictionary<ControlSurface, float> canSched = new Dictionary<ControlSurface, float>();
    readonly Dictionary<ControlSurface, Quaternion> canSchedRest = new Dictionary<ControlSurface, Quaternion>();
    int dbgS;
    float CanardSchedStep(ControlSurface cs)
    {
        if (!CanardSchedule || cs.name.ToLowerInvariant().IndexOf("canard") < 0) return 0f;
        Vector3 lv = Quaternion.Inverse(q) * vCg;
        float al = (lv.y * lv.y + lv.z * lv.z > 0.01f) ? -Mathf.Atan2(lv.y, lv.z) * Mathf.Rad2Deg : 0f;
        float tgt = CanardSchedOff > 0.5f ? 0f : Mathf.Clamp(al, -30f, 30f);
        canSched.TryGetValue(cs, out float cur);
        cur = Mathf.MoveTowards(cur, tgt, 90f * dt);
        canSched[cs] = cur;
        if (System.Environment.GetEnvironmentVariable("DBGSCHED") != null && (dbgS++ % 120) == 0) System.Console.WriteLine($"SCHED {cs.name} al={al:F1} cur={cur:F1}");
        return cur;
    }
    // Su-30 0.5.5: the game's landing-flap hinges (Flaperon_L/R_LandingBias, parents of the flaperons' hinges and lift normals). Game
    // (ControlSurfaceJob_Math, flap = 1): slews at 40 deg/s toward -pitchRange while the gear is extended, else toward 0. With the FCS:
    // 0.5.4 = that, its range cut by roll (FlaperonRollPriority x flapRollBias); 0.5.5 (FlaperonFlapSchedule) = the FCS's GameFlapStep.
    public static bool BenchGameFlap = System.Environment.GetEnvironmentVariable("NOGAMEFLAP") == null;
    Transform[] gFlapT; Quaternion[] gFlapRest; readonly float[] gFlapCur = new float[2]; readonly object[] gFlapKey = { "L", "R" };
    public float GameFlapL => gFlapCur[0]; public float GameFlapR => gFlapCur[1];
    static Transform FindT(Transform t, string n) { if (t.gameObject != null && t.gameObject.name == n) return t; foreach (var c in t.children) { var r = FindT(c, n); if (r != null) return r; } return null; }
    void GameFlaps()
    {
        if (!BenchGameFlap || !A.su30) return;
        if (gFlapT == null)
        {
            gFlapT = new[] { FindT(A.root, "Flaperon_L_LandingBias"), FindT(A.root, "Flaperon_R_LandingBias") };
            gFlapRest = new Quaternion[2];
            for (int i = 0; i < 2; i++) if (gFlapT[i] != null) gFlapRest[i] = gFlapT[i].localRotation;
        }
        for (int i = 0; i < 2; i++)
        {
            if (gFlapT[i] == null) continue;
            float cur = gFlapCur[i], next;
            bool fe = fcs != null && fcs.Engaged && !fcs.Failed;
            if (fe && Plugin.FlaperonFlapSchedule) next = fcs.GameFlapStep(gFlapKey[i], cur, 40f, dt);
            else
            {
                float tgt = A.ac.gearDeployed ? 15f * (fe ? (1f - Plugin.FlaperonRollPriority * Mathf.Clamp01(fcs.flapRollBias)) : 1f) : 0f;
                next = Mathf.MoveTowards(cur, tgt, 40f * dt);
                if (fe) fcs.landFlapDbg = cur;
            }
            gFlapCur[i] = next;
            gFlapT[i].localRotation = gFlapRest[i] * Quaternion.AngleAxis(-next, Vector3.right);
        }
    }
    void Surfaces()
    {
        GameFlaps();
        foreach (var cs in A.surfaces)
        {
            float schedA = CanardSchedStep(cs);
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
                f.controlInputs.pitch = DelayCmd(cs, Mathf.Clamp(s.cmdOut / s.max, -1f, 1f));
                f.controlInputs.roll = 0f; f.controlInputs.yaw = 0f;
            }
            if (stock != null && A.ifrit)
            {
                var ci = A.ac.inputs;
                f.currentPitch += Mathf.Clamp(ci.pitch * cs.pitchRange - f.currentPitch, -f.servoSpeed * dt, f.servoSpeed * dt); f.currentPitch = Mathf.Clamp(f.currentPitch, -Mathf.Abs(cs.pitchRange), Mathf.Abs(cs.pitchRange));
                f.currentRoll += Mathf.Clamp(ci.roll * cs.rollRange - f.currentRoll, -f.servoSpeed * dt, f.servoSpeed * dt); f.currentRoll = Mathf.Clamp(f.currentRoll, -Mathf.Abs(cs.rollRange), Mathf.Abs(cs.rollRange));
                f.currentYaw += Mathf.Clamp(ci.yaw * cs.yawRange - f.currentYaw, -f.servoSpeed * dt, f.servoSpeed * dt); f.currentYaw = Mathf.Clamp(f.currentYaw, -Mathf.Abs(cs.yawRange), Mathf.Abs(cs.yawRange));
                cs.visibleMesh.transform.localRotation = f.restingRotation * Quaternion.AngleAxis(f.currentPitch + f.currentRoll + f.currentYaw, Vector3.right);
                SplitStep(cs, ci.yaw, throttle, AirbrakeAtIdle);
                continue;
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
            if (schedA != 0f || canSchedRest.ContainsKey(cs)) { var hp = cs.visibleMesh.transform.parent; if (hp != null) { if (!canSchedRest.TryGetValue(cs, out var hr)) { hr = hp.localRotation; canSchedRest[cs] = hr; } hp.localRotation = hr * Quaternion.AngleAxis(schedA, Vector3.right); } }
            // SurfacePostfix: with the FCS engaged the job sees throttle = (SplitDeploy ? 0 : 1), so the stock idle rule opens the split
            if (A.ifrit)
            {
                bool fe = fcs != null && fcs.Engaged && !fcs.Failed;
                SplitStep(cs, SplitYaw != null ? SplitYaw(cs) : 0f, fe ? (fcs.SplitDeploy ? 0f : 1f) : throttle, fe || AirbrakeAtIdle);
            }
        }
    }
    // Ifrit split-flap decelerons (ControlSurfaceJob_Math): target = (throttle==0 ? max : 0) + yawSplitFactor*yaw*max, clamped [0,max], slewed at servo speed.
    public Func<ControlSurface, float> SplitYaw;   // FCS mode: per-surface yaw-equivalent split command (-1..1 in the surface's yaw sense)
    public static bool AirbrakeAtIdle = true;
    void SplitStep(ControlSurface cs, float yaw, float thr, bool idleBrake)
    {
        if (cs.maxSplit <= 0f) return;
        ref var f = ref cs.job;
        float tgt = Mathf.Clamp(((idleBrake && thr == 0f) ? cs.maxSplit : 0f) + cs.yawSplitFactor * yaw * cs.maxSplit, 0f, cs.maxSplit);
        cs.splitAmount += Mathf.Clamp(tgt - cs.splitAmount, -cs.servoSpeed * dt, cs.servoSpeed * dt);
    }
    // stock HighLiftDevice.FixedUpdate (speed schedule, 1/s), used when no FCS drives the flaps
    void StockHld()
    {
        foreach (var h in A.hlds)
        {
            float t = Mathf.Clamp01(1f - Mathf.Max(A.ac.speed - h.speedDeployed, 0f) / (h.speedRetracted - h.speedDeployed));
            if (h.alphaFactor > 0f) { var lv = Quaternion.Inverse(h.transform.rotation) * A.ac.rb.velocity; t += Mathf.Clamp01(Mathf.Abs(Mathf.Atan2(lv.y, lv.z) * -57.29578f) - h.alphaMin) * h.alphaFactor; }
            if (Mathf.Abs(t - h.position) > 0.001f)
            {
                h.position += Mathf.Clamp(t - h.position, -dt, dt);
                h.aeroPart.wingArea = Mathf.Lerp(h.partAreaRetracted, h.partAreaDeployed, h.position);
                foreach (var mp in h.movingParts) if (mp.transform != null)
                {
                    if (mp.move) mp.transform.localPosition = Vector3.Lerp(mp.positionRetracted, mp.positionDeployed, h.position);
                    if (mp.rotate) mp.transform.localEulerAngles = Vector3.Lerp(mp.anglesRetracted, mp.anglesDeployed, h.position);
                }
            }
        }
    }
    public static float ThrottleOf(Turbofan tf, float thr) => tf.turbojet ? Mathf.Clamp01((thr - tf.remapX) / (tf.remapY - tf.remapX)) : thr;

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
            foreach (var b in A.bodies) if (Names.Canon(b.part.name.Replace("Aryx_KingRaptor_", "")) == hn) { camberHost[b.part] = cs; break; }   // 9 Nov 2026: Canon; on the Su-30 (blueprint names) no camber host was ever found
        }
    }

    public void Step()
    {
        if (!camberBuilt) BuildCamber();
        Time.fixedDeltaTime = dt; Time.deltaTime = dt;
        Pose();
        // ---- FCS (ControlsFilter.Filter prefix)
        A.ac.inputs.throttle = throttle;
        if (EvalOnly) { }
        else if (stock != null) { stock.Filter(A.ac.speed, A.ac.airDensity, vCg, q, w, A.ac.inputs, dt); if (A.ifrit) StockHld(); }
        else if (fcs == null) { if (A.ifrit) StockHld(); }   // open loop (no controller): surfaces follow the raw inputs
        else { TickSw.Start(); fcs.Tick(A.ac.inputs, !BenchMpo); fcs.VmcApply(A.ac.inputs, dt); TickSw.Stop(); TickN++; if (JoinAfterTick) fcs.EstJoin(); }
        // ---- surfaces
        if (!EvalOnly) Surfaces();
        // ---- engines: Turbofan.FixedUpdate with the TvcPitch transpiler
        Vector3 F = Vector3.zero, M = Vector3.zero;
        foreach (var tf in A.engines)
        {
            float pin = (stock != null || fcs == null) ? A.ac.inputs.pitch : fcs.tvcPitchInput;
            float rin = (stock != null || fcs == null) ? A.ac.inputs.roll : (TvcRollIn != null ? TvcRollIn(tf) : 0f);
            float n5 = -Mathf.Clamp(pin * tf.thrustVectoringGain.x, -1f, 1f) * tf.thrustVectoring.x
                       - Mathf.Clamp(rin * tf.thrustVectoringGain.z, -1f, 1f) * tf.thrustVectoring.z;
            if (TvcAngleOverride != null) n5 = TvcAngleOverride(tf, n5);
            float slew = (A.ifrit ? 70f : 70f * Plugin.ActuatorScale) * dt;
            if (EvalOnly) slew = 1e6f;
            tf.nozzleAngles.x += Mathf.Clamp(n5 - tf.nozzleAngles.x, -slew, slew);
            float yin = (stock != null || fcs == null) ? A.ac.inputs.yaw : (TvcYawIn != null ? TvcYawIn(tf) : 0f);
            float n6 = -Mathf.Clamp(yin, -1f, 1f) * tf.thrustVectoring.y;
            if (A.ac.radarAlt < 3f) n6 = 0f;
            if (TvcYawOverride != null) n6 = TvcYawOverride(tf, n6);
            tf.nozzleAngles.y += Mathf.Clamp(n6 - tf.nozzleAngles.y, -slew, slew);
            if (A.ac.speed > tf.thrustVectoringMaxAirspeed) tf.nozzleAngles = Vector3.zero;
            // Su-30 0.1.0 (CantedTvc): the FCS's canted single-axis nozzles (Hooks.SetNozzleEuler in game)
            Vector3 eStock = new Vector3(Mathf.Clamp(tf.nozzleAngles.x + tf.nozzleAngles.z, -20f, 20f), tf.nozzleAngles.y, 0f);
            bool cantedOn = fcs != null && fcs.canted != null && stock == null && Plugin.CantedTvc;
            foreach (var vt in tf.vectoringTransforms) vt.localEulerAngles = NozzleEulerPose != null ? NozzleEulerPose(tf) : cantedOn ? fcs.NozzleEuler(tf, eStock, EvalOnly ? 1e3f : dt, EvalOnly ? -1f - time : time) : eStock;
            float spd = A.ac.speed;
            float thrE = stock != null ? throttle : A.ac.inputs.throttle;   // 2.0.12: the FCS may lower the throttle input (VMC)
            if (ThrustOverride >= 0f)
            {
                foreach (var n in tf.nozzles) { n.totalThrust = ThrustOverride; var fr = n.thrustTransform.forward * n.totalThrust; F += fr; M += Vector3.Cross(ThrustLine.Arm(n.thrustTransform.position, pCg, Quaternion.Inverse(q)) * NozzleArmScale + q * Vector3.up * ThrustLineDy, fr); }
                continue;
            }
            if (tf.turbojet)
            {
                // NO Turbojet.FixedUpdate: rpm target from the remapped throttle, spool-rate limited; thrustRatio from rpm; altitude curve; overspeed lapse
                float tr = Mathf.Clamp01((thrE - tf.remapX) / (tf.remapY - tf.remapX));
                float rpmT = tf.operable ? Mathf.Lerp(tf.minRPM, tf.minRPM + (tf.maxRPM - tf.minRPM) * (0.5f * tf.condition + 0.5f), tr) : 0f;   // NO: a damaged engine (condition < 1) tops out lower
                if (EvalOnly) tf.rpm = rpmT; else tf.rpm += Mathf.Clamp(rpmT - tf.rpm, -tf.spoolRate * SpoolScale, tf.spoolRate * SpoolScale) * dt;
                float ratio = tf.operable ? Mathf.Clamp((tf.rpm - tf.minRPM * 0.85f) / (tf.maxRPM - tf.minRPM), 0f, 1f) : 0f;
                float altF = AltCurve(tf, pCg.y);
                float thr = Mathf.Max(0f, tf.staticThrust - tf.parasitic) * ratio * altF;   // 0.4.1: parasiticThrustLoss (the FCS thrust match)
                if (spd > tf.maxSpeed) thr *= Mathf.Max(0f, 1f - 5f * (spd - tf.maxSpeed) / tf.maxSpeed);
                foreach (var n in tf.nozzles)
                {
                    // game Afterburner.Run: amount = Lerp(amount, Clamp01((x - start) / (end - start)), smoothing x dt), x = throttle if RPM > 85 % (else 0)
                    float abTgt = (tf.operable && tf.rpm / tf.maxRPM > 0.85f) ? Mathf.InverseLerp(n.abStart, 1f, thrE) : 0f;
                    if (EvalOnly || AbSmoothing <= 0f) n.abAmt = abTgt; else n.abAmt = Mathf.Lerp(n.abAmt, abTgt, AbSmoothing * SpoolScale * dt);
                    float ab = n.abThrust * n.abAmt * Mathf.Clamp(A.ac.airDensity, 0.4f, 1f);
                    n.totalThrust = thr + ab;
                    var fr = n.thrustTransform.forward * n.totalThrust;
                    F += fr; M += Vector3.Cross(ThrustLine.Arm(n.thrustTransform.position, pCg, Quaternion.Inverse(q)) * NozzleArmScale + q * Vector3.up * ThrustLineDy, fr);
                }
                continue;
            }
            float sf = SpeedThrust(tf, spd);
            float mil = tf.operable ? tf.staticThrust * sf * Mathf.Clamp01(thrE / 0.95f) : 0f;
            foreach (var n in tf.nozzles)
            {
                float ab = (tf.operable && thrE >= 0.95f) ? n.abThrust * Mathf.InverseLerp(0.95f, 1f, thrE) * Mathf.Clamp(A.ac.airDensity, 0.4f, 1f) : 0f;
                n.totalThrust = mil + ab;
                var fr = n.thrustTransform.forward * n.totalThrust;
                F += fr; M += Vector3.Cross(ThrustLine.Arm(n.thrustTransform.position, pCg, Quaternion.Inverse(q)) * NozzleArmScale + q * Vector3.up * ThrustLineDy, fr);
            }
        }
        // ---- Ifrit split-flap drag (ControlSurface.Aero): F = -v^ * split * splitDrag * rho * v^2 at the attached part's rigidbody
        if (A.ifrit)
            foreach (var cs in A.surfaces)
            {
                if (cs.splitAmount <= 0f || cs.job.IsDetached) continue;
                var rb = ((AeroPart)cs.attachedSurface).rb; Vector3 v = rb.velocity - A.ac.windVelocity;
                Vector3 fs = -v.normalized * (cs.splitAmount * cs.splitDrag * A.ac.airDensity * v.sqrMagnitude);
                F += fs; M += Vector3.Cross(rb.worldCenterOfMass - pCg, fs);
            }
        // ---- aero: AeroJob_Math for each part
        float rho = A.ac.airDensity;
        if (cdMin == null) { cdMin = new Dictionary<AeroPart, float>(); foreach (var b0 in A.bodies) { var a0 = b0.part; float mn = float.MaxValue; if (a0.airfoil >= 0) for (float aa = -0.35f; aa <= 0.35f; aa += 0.005f) mn = Mathf.Min(mn, Chart(A.dragCharts[a0.ChartId], aa * 20.37185f + 64f)); cdMin[a0] = mn == float.MaxValue ? 0f : mn; } }
        float bumpB = 0f;
        { Vector3 vbb = Quaternion.Inverse(A.ac.rb.rotation) * (A.ac.rb.velocity - A.ac.windVelocity); if (vbb.sqrMagnitude > 25f) bumpB = DragReliefMath.Bump(Mathf.Atan2(-vbb.y, vbb.z) * Mathf.Rad2Deg); }
        float aBody; { Vector3 vbw0 = Quaternion.Inverse(q) * (vCg - A.ac.windVelocity); aBody = Mathf.Atan2(-vbw0.y, vbw0.z) * Mathf.Rad2Deg; }
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
            if (Plugin.DragReliefAoA > 0f || Plugin.DragReliefTransonic > 0f || bumpB > 0f)
            {
                float foilMin = Mathf.Min(cdMin[ap], cd) * rho * v2 * 0.5f * ap.wingArea * eff;
                float dF = GameTransonic ? DragReliefMath.Relief(n9, n10, foilMin, mach, a * Mathf.Rad2Deg, bumpB) : 0f;
                if (dF != 0f) drag += v * (dF / vmag);
            }
            Vector3 lift = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea * eff * Airframe.Mis(ap.name, aBody));
            Vector3 f = lift + drag;
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
                        // 31 Oct (negAoA calibration, F-22E bench finding): keep the bench lift multiplier on the cambered lift too, else Mis* never reaches a host part while its surface is deflected
                        Vector3 dLift = -nrm1 * (cl1 * qSH * Airframe.Mis(ap.name, aBody)) - lift;
                        Vector3 dDrag = -vf.normalized * ((cd1 - cd) * qSH);
                        f += dLift + dDrag;
                    }
                }
            }
            F += f;
            if (PartForce != null) PartForce[b] = f;
            M += Vector3.Cross(b.rb.worldCenterOfMass - pCg, f);
            if (ap.centerOfLift != Vector3.zero) M += Vector3.Cross(f, -(lt.rotation * ap.centerOfLift));
            if (PartDump != null)   // damage model; computed apart so the moment sum keeps its original order (bit-identical with no damage)
            {
                Vector3 mPart = Vector3.Cross(b.rb.worldCenterOfMass - pCg, f);
                if (ap.centerOfLift != Vector3.zero) mPart += Vector3.Cross(f, -(lt.rotation * ap.centerOfLift));
                PartDump[ap] = (f, mPart, 0f, 0f);
            }
        }
        // 31 Oct calibration: dedicated airbrake drag at its open amount (set by eqerr from abOpen; the FCS/stock loop does not drive it here)
        foreach (var ab in A.airbrakes)
        {
            if (ab.openAmount <= 0f || ab.part.detached) continue;
            var rbA = ab.part.rb; Vector3 vA = rbA.velocity - A.ac.windVelocity;
            Vector3 fA = -vA.normalized * (ab.dragAmount * rho * vA.sqrMagnitude * ab.openAmount);
            F += fA; M += Vector3.Cross(rbA.worldCenterOfMass - pCg, fA);
        }
        // leaf calibration (3 Nov): bench-only lateral stiffness corrections (DClbHi / DCnbHi)
        if (DClbHi != 0f || DCnbHi != 0f)
        {
            Vector3 vbw = Quaternion.Inverse(q) * (vCg - A.ac.windVelocity); float tasB = vbw.magnitude;
            if (tasB > 1f)
            {
                float betaB = Mathf.Asin(Mathf.Clamp(vbw.x / tasB, -1f, 1f)) * Mathf.Rad2Deg;
                float u = Mathf.Clamp01((Mathf.Abs(aBody) - Airframe.MisHiA0) / Mathf.Max(1f, Airframe.MisHiA1 - Airframe.MisHiA0));
                float qb = 0.5f * A.ac.airDensity * tasB * tasB / 1e4f;
                M += q * Vector3.up * (DCnbHi * betaB * qb * u * Mathf.Deg2Rad * (float)A.Itot[1, 1]);
                M += q * Vector3.forward * (-DClbHi * betaB * qb * u * Mathf.Deg2Rad * (float)A.Itot[2, 2]);
            }
        }
        if (ext != null) { var fe = ext(this); F += fe.Item1; M += fe.Item2; }
        F += Physics.gravity * A.mass;
        lastForce = F; lastTorque = M;
        if (EvalOnly) return;
        preIntegrate?.Invoke(F, M);
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

    // body angular acceleration (world frame) for a moment M at the current pose and rates: I^-1 (M - w x I w)
    public Vector3 AngAccel(Vector3 M)
    {
        var R = Airframe.Rot(q);
        var Iw = Xform(R, A.Itot);
        Vector3 gyroT = Vector3.zero;
        if (gyro != Gyro.None) { var Ig = Xform(R, gyro == Gyro.Full ? A.Itot : A.Ipa); gyroT = Vector3.Cross(w, Mul(Ig, w)); }
        return Solve(Iw, M - gyroT);
    }
    public Func<Turbofan, Vector3> NozzleEulerPose;   // eqerr (9 Nov 2026): logged per-nozzle pose
    public Func<Turbofan, float> TvcRollIn, TvcYawIn;               // FCS mode: roll input reaching the nozzles (0 = pitch-only)
    public Func<Turbofan, float, float> TvcAngleOverride, TvcYawOverride; // FCS mode: direct per-engine nozzle angle (deg) override
    static float AltCurve(Turbofan tf, float h)
    {
        var k = tf.altCurve; if (k == null || k.Length == 0) return 1f;
        if (h <= k[0][0]) return k[0][1]; if (h >= k[k.Length - 1][0]) return k[k.Length - 1][1];
        for (int i = 0; i < k.Length - 1; i++) if (h <= k[i + 1][0])
        {   // Hermite (Unity AnimationCurve, unweighted)
            float t0 = k[i][0], t1 = k[i + 1][0], dtk = t1 - t0, u = (h - t0) / dtk;
            float h00 = 2*u*u*u - 3*u*u + 1, h10 = u*u*u - 2*u*u + u, h01 = -2*u*u*u + 3*u*u, h11 = u*u*u - u*u;
            return h00 * k[i][1] + h10 * dtk * k[i][3] + h01 * k[i + 1][1] + h11 * dtk * k[i + 1][2];
        }
        return 1f;
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
