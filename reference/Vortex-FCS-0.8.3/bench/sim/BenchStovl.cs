// Vortex 0.1.0 bench: the FS-20's lift system (game SwivelDuctSystem.FixedUpdate, decompiled) as plant physics, and the
// IStovlHardware the FCS's STOVL laws read (the game reads it through SwivelLink).
//   swivel  : swivelPosition slews toward 1 - customAxis1 at swivelSpeed; bearings localEulerAngles = Lerp(min, max,
//             curve(s)) + yawFactor x yaw input
//   doors   : thrustDoorPosition slews toward (customAxis1 < 1) at thrustDoorSpeed
//   lift fan: while doors > 0, force = -(nozzle thrust moment about the CG, body x) / (fan unit moment, body x) + doors x pitch
//             input x pitchFactor x RPM ratio, >= 0, along the fan thrust transform; engine parasitic loss = parasiticLoss x s
//   posts   : s >= minSwivel, speed <= maxSpeed, radar alt >= minAlt: max(maxThrust x thrust ratio x roll x rollFactor, 0)
//   panels  : each moving panel with extraDrag > 0 adds s x rho x v^2 of drag at the CG (no moment); 0.1.7: the fan upper door excepted (FCS drag)
//   modes   : the game's auto modes are not modelled (the FCS holds Manual); above maxSwivelAirspeed customAxis1 = 1
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal sealed class BenchStovl : IStovlHardware
{
    public static string SystemsPath = Environment.GetEnvironmentVariable("BENCH_SYSTEMS");
    public static bool Enabled = true;

    readonly Plant P; readonly Airframe A; readonly Turbofan tf; readonly JetNozzle nz;
    public float swivel, doors;
    float swivelSpeed = 0.25f, doorSpeed = 0.25f, maxSwivelAs = 139f;
    readonly List<(Transform t, Vector3 mn, Vector3 mx, Vector3 yf, float[][] curve)> bearings = new List<(Transform, Vector3, Vector3, Vector3, float[][])>();
    Transform fanT, mainT; float fanPitch = -25000f, fanParasitic = 25000f;
    // Vortex 0.5.0: the vane box (game: SwivelLink.VaneStep): the fan thrust transform's own rotation and the thrust line now
    Quaternion fanRestLocal = Quaternion.identity; float vaneNow; bool vaneInit, vaneMoved; float vaneDrive = 1f;
    readonly List<(Transform t, float max, float roll, float minSw, float minAlt, float maxSpd)> posts = new List<(Transform, float, float, float, float, float)>();
    int dragPanels;
    // last-step values of the plant (for the FCS reads and the test logs)
    public float doorDrag; public List<Transform> doorTs = new List<Transform>(); Transform topDoorT; float topDoorFull = 60f;
    public float thrustNow, abRawNow, fanForce, postL, postR, panelDrag, axis1Applied, pitchApplied, rollApplied, yawApplied;

    public static BenchStovl Make(Plant p)
    {
        if (!Enabled || !p.A.vortex) return null;
        string path = SystemsPath;
        if (string.IsNullOrEmpty(path)) path = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("BENCH_DATA") ?? ".") ?? ".", "vortex_systems.json");
        if (!File.Exists(path)) { Console.WriteLine("bench STOVL: no " + path + " (lift system off)"); return null; }
        return new BenchStovl(p, path);
    }

    static Vector3 V(JsonElement e) => new Vector3(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle(), e.GetProperty("z").GetSingle());

    BenchStovl(Plant p, string path)
    {
        P = p; A = p.A; tf = A.engines[0]; nz = tf.nozzles[0];
        var byName = new Dictionary<string, Transform>();
        foreach (var t in A.T.Values) if (!byName.ContainsKey(t.gameObject.name)) byName[t.gameObject.name] = t;
        Transform R(JsonElement r) => byName.TryGetValue(r[1].GetString().Split(':')[0], out var t) ? t : null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var S = doc.RootElement.GetProperty("SmallFighter1/Swivel");
        swivelSpeed = S.GetProperty("swivelSpeed").GetSingle(); doorSpeed = S.GetProperty("thrustDoorSpeed").GetSingle(); maxSwivelAs = S.GetProperty("maxSwivelAirspeed").GetSingle();
        foreach (var b in S.GetProperty("bearings").EnumerateArray())
        {
            float[][] c = null;
            if (b.GetProperty("useCurve").GetInt32() != 0)
                c = b.GetProperty("adjustmentCurve").GetProperty("m_Curve").EnumerateArray().Select(k => new[] { k.GetProperty("time").GetSingle(), k.GetProperty("value").GetSingle(), k.GetProperty("inSlope").GetSingle(), k.GetProperty("outSlope").GetSingle() }).ToArray();
            bearings.Add((R(b.GetProperty("transform")), V(b.GetProperty("minAngles")), V(b.GetProperty("maxAngles")), V(b.GetProperty("yawFactor")), c));
        }
        if (S.TryGetProperty("thrustDoors", out var tdA)) foreach (var td in tdA.EnumerateArray()) { var dt0 = R(td.GetProperty("transform")); if (dt0 != null) doorTs.Add(dt0); }
        var f = S.GetProperty("liftFans")[0];
        fanT = R(f.GetProperty("thrustTransform")); mainT = R(f.GetProperty("nozzleThrustTransform"));
        if (fanT != null) fanRestLocal = fanT.localRotation;
        fanPitch = f.GetProperty("pitchFactor").GetSingle() * Plugin.ThrustScale; fanParasitic = f.GetProperty("parasiticLoss").GetSingle();
        foreach (var t in S.GetProperty("thrusters").EnumerateArray())
            posts.Add((R(t.GetProperty("thrustTransform")), t.GetProperty("maxThrust").GetSingle() * Plugin.ThrustScale, t.GetProperty("rollFactor").GetSingle(), t.GetProperty("minSwivel").GetSingle(), t.GetProperty("minAlt").GetSingle(), t.GetProperty("maxSpeed").GetSingle()));
        dragPanels = S.GetProperty("movingPanels").EnumerateArray().Count(m => m.GetProperty("extraDrag").GetSingle() > 0f);
        // Vortex 0.1.7: the fan's upper door (the panel named *door*) opens with max(swivel, doors) and its drag is the FCS's
        // (StovlFanDoorDragArea x sin(angle) / sin(full)), the game's swivel term for it taken out
        foreach (var m in S.GetProperty("movingPanels").EnumerateArray())
        {
            var mt = R(m.GetProperty("transform"));
            if (mt == null || mt.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0) continue;
            topDoorT = mt; topDoorFull = Mathf.Max(1f, (V(m.GetProperty("maxRotation")) - V(m.GetProperty("minRotation"))).magnitude);
            if (m.GetProperty("extraDrag").GetSingle() > 0f) dragPanels--;
            break;
        }
        A.ac.inputs.customAxis1 = 1f;   // game SwivelDuctSystem.Awake
        if (mainT != nz.thrustTransform) Console.WriteLine("bench STOVL: warning, the fan's nozzle transform is not the engine nozzle's thrust transform");
        Animate(0f);
    }

    public static float Curve(float[][] k, float x)
    {
        if (k == null) return x;
        if (x <= k[0][0]) return k[0][1]; if (x >= k[k.Length - 1][0]) return k[k.Length - 1][1];
        for (int i = 0; i < k.Length - 1; i++) if (x <= k[i + 1][0])
        {   // Unity AnimationCurve, unweighted Hermite
            float t0 = k[i][0], dtk = k[i + 1][0] - t0, u = (x - t0) / dtk;
            float h00 = 2*u*u*u - 3*u*u + 1, h10 = u*u*u - 2*u*u + u, h01 = -2*u*u*u + 3*u*u, h11 = u*u*u - u*u;
            return h00 * k[i][1] + h10 * dtk * k[i][3] + h01 * k[i + 1][1] + h11 * dtk * k[i + 1][2];
        }
        return x;
    }

    void Animate(float yaw)
    {
        foreach (var b in bearings)
            if (b.t != null) b.t.localEulerAngles = Vector3.Lerp(b.mn, b.mx, Curve(b.curve, swivel)) + b.yf * yaw;
    }

    StovlSystem S => P.fcs?.stovl;
    bool Owned => S != null && S.owns && P.fcs.Engaged && !P.fcs.Failed;

    // the throttle the engine sees (Turbojet transpiler: EngineThrottle)
    public float EngineThrottle(float raw) => (Owned && S.throttleOverride) ? S.throttleOut : raw;

    // the customAxis1 the swivel and the engine's afterburner lock see
    // (0.2.2 freestyle: the pilot's Custom Axis 1, which the game builds on the value the FCS left there)
    float lastAxis1 = 1f;
    public float Axis1 { get { float v = (A.ac.speed > maxSwivelAs) ? 1f : ((Owned && !S.freestyle) ? S.axis1Out : A.ac.inputs.customAxis1); if (Owned && !S.freestyle) A.ac.inputs.customAxis1 = S.axis1Out; return v; } }

    public bool AllowAfterburner => tf.parasitic <= 0f || Axis1 > 0.3f;

    // SwivelDuctSystem.FixedUpdate, kinematic part (before the engine)
    public void Kinematics(float dt)
    {
        var ci = A.ac.inputs;
        bool own = Owned;
        axis1Applied = Axis1;
        pitchApplied = own ? S.fanIn : ci.pitch; rollApplied = own ? S.postIn : ci.roll; yawApplied = own ? S.yawIn : ci.yaw;
        swivel += Mathf.Clamp((1f - axis1Applied) - swivel, -swivelSpeed * dt, swivelSpeed * dt);
        Animate(yawApplied);
        doors += Mathf.Clamp((axis1Applied < 1f ? 1f : 0f) - doors, -doorSpeed * dt, doorSpeed * dt);
        VaneStep(own && S.vaneLive && !S.freestyle, own ? S.vaneOut : 0f, dt);
    }

    // SwivelLink.VaneStep (the game plugin's vane actuator), the same steps
    public float VaneRest { get { Vector3 f = FanRestForward; return Mathf.Atan2(Vector3.Dot(f, A.root.forward), Vector3.Dot(f, A.root.up)) * 57.29578f; } }
    void VaneStep(bool live, float cmd, float dt)
    {
        if (fanT == null) return;
        float rest = VaneRest;
        if (!vaneInit) { vaneInit = true; vaneNow = rest; }
        bool on = live && Plugin.StovlVanes && doors > 0f;
        float tgt = on ? Mathf.Clamp(cmd, Plugin.StovlVaneMin, Plugin.StovlVaneMax) : rest;
        vaneNow = Mathf.MoveTowards(vaneNow, tgt, Plugin.StovlVaneRate * dt);
        if (Mathf.Abs(vaneNow - rest) < 0.0001f)
        {
            vaneNow = rest;
            if (vaneMoved) { fanT.localRotation = fanRestLocal; vaneMoved = false; }
        }
        else
        {
            Quaternion pr = fanT.parent != null ? fanT.parent.rotation : Quaternion.identity;
            fanT.rotation = Quaternion.AngleAxis(vaneNow - rest, A.root.right) * (pr * fanRestLocal);
            vaneMoved = true;
        }
        vaneDrive = StovlSystem.VaneDrive(vaneNow);
    }

    // SwivelDuctSystem.FixedUpdate, force part (after the engine: the fan balances the nozzle thrust of this step)
    public void Forces(ref Vector3 F, ref Vector3 M, Vector3 pCg)
    {
        fanForce = 0f; postL = postR = 0f; panelDrag = 0f;
        var root = A.root;
        if (doors > 0f)
        {
            Vector3 Ft = DryThrust * mainT.forward;
            Vector3 mN = Vector3.Cross(Ft, -(mainT.position - pCg));
            Vector3 mU = Vector3.Cross(fanT.forward, -(fanT.position - pCg));
            float vx = root.InverseTransformDirection(mN).x, ux = root.InverseTransformDirection(mU).x;
            float n = (Mathf.Abs(ux) > 1e-4f ? -vx / ux : 0f) + doors * pitchApplied * fanPitch * RpmRatio;
            fanForce = Mathf.Max(n, 0f);
            Vector3 ff = fanForce * fanT.forward;
            F += ff; M += Vector3.Cross(fanT.position - pCg, ff);
            tf.parasitic = fanParasitic * vaneDrive * swivel;   // (0.5.0: LiftFan.parasiticLoss carries the vanes' drive factor)
        }
        else tf.parasitic = 0f;
        for (int i = 0; i < posts.Count; i++)
        {
            var p = posts[i];
            if (swivel < p.minSw || A.ac.speed > p.maxSpd || A.ac.radarAlt < p.minAlt) continue;
            float n = Mathf.Max(p.max * ThrustRatio * rollApplied * p.roll, 0f);
            if (i == 0) postL = n; else postR = n;
            Vector3 fp = n * p.t.forward;
            F += fp; M += Vector3.Cross(p.t.position - pCg, fp);
        }
        // Vortex 0.1.7: the fan's upper door drag (FCS: StovlHooks.UpperDoor), at its hinge
        doorDrag = 0f;
        float aTop = Mathf.Max(swivel, doors);
        if (aTop > 0f && Plugin.StovlFanDoorDragArea > 0f && topDoorT != null)
        {
            Vector3 v = P.vCg - A.ac.windVelocity;
            float full = Mathf.Min(topDoorFull, 90f) * Mathf.Deg2Rad;
            float k = Mathf.Sin(aTop * full) / Mathf.Sin(full);
            Vector3 fd = -v.normalized * (0.5f * A.ac.airDensity * v.sqrMagnitude * Plugin.StovlFanDoorDragArea * k);
            doorDrag = fd.magnitude;
            F += fd; M += Vector3.Cross(topDoorT.position - pCg, fd);
        }
        if (swivel > 0f && dragPanels > 0)
        {
            Vector3 v = P.vCg - A.ac.windVelocity;
            Vector3 fd = -v.normalized * (dragPanels * swivel * A.ac.airDensity * v.sqrMagnitude);
            panelDrag = fd.magnitude; F += fd;
        }
    }

    // ---- IStovlHardware
    public bool Valid => mainT != null && fanT != null && bearings.Count > 0;
    public float Doors => doors;
    public float Swivel => swivel;
    public float SwivelRate => swivelSpeed;
    public float MaxSwivelAirspeed => maxSwivelAs;
    public Vector3 SurfaceVelocity => Vector3.zero;
    public float ShipFrameWeight => 0f;
    public Transform MainThrustT => mainT;
    public float NozzleThrust => nz.totalThrust;
    public float DryThrust => thrustNow + abRawNow;
    public float RpmRatio => tf.rpm / tf.maxRPM;
    public float ThrustRatio => thrustNow / Mathf.Max(1f, tf.staticThrust);
    public float MaxThrust => tf.staticThrust;
    public float ParasiticLoss => fanParasitic;
    public float AltFactor => Plant.AltCurvePub(tf, P.pCg.y);
    public float MinRpm => tf.minRPM;
    public float MaxRpm => tf.maxRPM;
    public float SpoolRate => tf.spoolRate * Plant.SpoolScale;
    public Vector2 Remap => new Vector2(tf.remapX, tf.remapY);
    public float Condition => tf.condition;
    public int Bearings => bearings.Count;
    public Transform BearingT(int i) => bearings[i].t;
    public Vector3 BearingMin(int i) => bearings[i].mn;
    public Vector3 BearingMax(int i) => bearings[i].mx;
    public Vector3 BearingYaw(int i) => bearings[i].yf;
    public float BearingCurve(int i, float s) => Curve(bearings[i].curve, s);
    public Transform FanThrustT => fanT;
    public bool HasVanes => fanT != null;
    public float VaneNow { get { if (!vaneInit) { vaneInit = true; vaneNow = VaneRest; } return vaneNow; } }
    public Vector3 FanRestForward => (fanT.parent != null ? fanT.parent.rotation * fanRestLocal : fanRestLocal) * Vector3.forward;
    public float FanPitchFactor => fanPitch;
    public int Posts => posts.Count;
    public Transform PostT(int i) => posts[i].t;
    public float PostMax(int i) => posts[i].max;
    public float PostRollFactor(int i) => posts[i].roll;
    public float PostMinSwivel(int i) => posts[i].minSw;
    public float PostMaxSpeed(int i) => posts[i].maxSpd;
    public float AircraftSpeed => A.ac.speed;
    public Vector3 CenterOfMass => P.pCg;
}
