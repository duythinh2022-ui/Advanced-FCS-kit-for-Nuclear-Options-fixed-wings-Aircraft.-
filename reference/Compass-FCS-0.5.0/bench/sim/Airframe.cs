// Builds the F-22E from the data extracted out of Aryx's asset bundle (bench_airframe.json):
// the real transform hierarchy, the 37 AeroParts (mass, wing/drag area, airfoil, centre of lift, lift normal),
// the 8 ControlSurfaces, both Turbofans/JetNozzles, the two HighLiftDevices, the AlphaResponder flap drives,
// fuel tanks, and the three airfoil curves. Registry.EnsureSetup's airframe changes are re-applied here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

public sealed class PartBody
{
    public AeroPart part;
    public Rigidbody rb;
    public Vector3 comB;            // part COM in root frame
    public double[,] IownB = new double[3, 3];   // own inertia about part COM, root frame
}

public sealed class Airframe
{
    public GameObject rootGo;
    public Transform root;
    public Aircraft ac;
    public readonly Dictionary<string, Transform> T = new Dictionary<string, Transform>();
    public readonly List<PartBody> bodies = new List<PartBody>();
    public readonly List<ControlSurface> surfaces = new List<ControlSurface>();
    public readonly List<Turbofan> engines = new List<Turbofan>();
    public readonly List<HighLiftDevice> hlds = new List<HighLiftDevice>();
    public readonly List<Airbrake> airbrakes = new List<Airbrake>();
    public float[][] liftCharts, dragCharts;
    public float mass;
    public Vector3 cgB;
    public double[,] Itot = new double[3, 3], Ipa = new double[3, 3], Iown = new double[3, 3];
    public float totalMassTarget = 29230f;
    public bool ifrit, revoker, compass; public JsonElement stockFbwJ; public bool hasStockFbw;
    public readonly Dictionary<AeroPart, JsonElement> JointsRaw = new Dictionary<AeroPart, JsonElement>();
    public Dictionary<string, AeroPart> PartByTidPublic;
    public static readonly Dictionary<string,string> IfritNames = new Dictionary<string,string>{{"elevator_L","Elevator_L"},{"elevator_R","Elevator_R"},{"aileron_L","Aileron_L"},{"aileron_R","Aileron_R"},{"flap_L","Flap_L"},{"flap_R","Flap_R"}};
    public static bool UseHulls = true;
    // 31 Oct calibration: bench-only fitted factors (see CALIBRATION.md). Lift multipliers per airframe group in the plant
    // only (the FCS model is untouched); effective-inertia scale per axis; bench-only CG shift (m, + = aft).
    public static float MisStab = 1f, MisAil = 1f, MisFlap = 1f, MisRud = 1f, MisWing = 1f, MisBody = 1f, MisFin = 1f, MisCan = 1f;
    public static float IScaleRoll = 1f, IScalePitch = 1f, IScaleYaw = 1f;
    public static float CgAftShift = 0f;
    public static float MisLift = 1f;   // one lift multiplier on the whole lifting airframe (wing panels + fuselage; not the control surfaces)
    // leaf calibration (3 Nov 2026, ported from the F-22E bench): bench-only high-AoA multipliers blended in over body |alpha|
    // MisHiA0..MisHiA1; -1 = same as the low-AoA value (inactive).
    public static float MisAilHi = -1f, MisBodyHi = -1f, MisCanHi = -1f, MisFinHi = -1f, MisFlapHi = -1f, MisRudHi = -1f, MisStabHi = -1f, MisWingHi = -1f;
    public static float MisHiA0 = 45f, MisHiA1 = 60f;
    public static float MisHi(string n)
    {
        if (string.IsNullOrEmpty(n)) return -1f;
        string l = n.ToLowerInvariant();
        if (l.Contains("canard")) return MisCanHi;
        if (l.Contains("elevator") || l.Contains("stab") && !l.Contains("vstab")) return MisStabHi;
        if (l.Contains("aileron")) return MisAilHi;
        if (l.Contains("flap")) return MisFlapHi;
        if (l.Contains("rudder")) return MisRudHi;
        if (l.Contains("vstab") || l.Contains("tail_") || l == "tail") return MisFinHi;
        if (l.Contains("wing")) return MisWingHi;
        return MisBodyHi;
    }
    public static float Mis(string n, float aBodyDeg)
    {
        float lo = Mis(n); float hi = MisHi(n);
        if (hi < 0f) return lo;
        float u = Mathf.Clamp01((Mathf.Abs(aBodyDeg) - MisHiA0) / Mathf.Max(1f, MisHiA1 - MisHiA0));
        return lo + (hi - lo) * u;
    }
    public static float Mis(string n)
    {
        if (string.IsNullOrEmpty(n)) return 1f;
        string l = n.ToLowerInvariant();
        if (l.Contains("canard")) return MisCan;
        if (l.Contains("elevator") || l.Contains("stab") && !l.Contains("vstab")) return MisStab;
        if (l.Contains("aileron")) return MisAil;
        if (l.Contains("flap")) return MisFlap;
        if (l.Contains("rudder")) return MisRud;
        if (l.Contains("vstab") || l.Contains("tail_") || l == "tail") return MisFin;
        if (l.Contains("wing")) return MisWing * MisLift;
        return MisBody * MisLift;
    }
    public static string Detach;
    public static float StoreKg = 0f; public static string StorePart = "WingRear_L"; public static float StoreDrag = 0f;
    // 2.2.3 EM: fuel fraction of the full internal load, explicit spawn-mass override (<=0: tanks + parts only),
    // and stores "Part:kg:drag;..." (drag = the WeaponMount drag the game adds to the hosting AeroPart's dragArea)
    public static float FuelFrac = 1f; public static float MassTarget = -1f; public static string Stores = null;

    static bool B(JsonElement e) => e.ValueKind == JsonValueKind.True || (e.ValueKind == JsonValueKind.Number && e.GetDouble() != 0);
    static float[] V3a(JsonElement e) => e.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    static Vector3 V3(JsonElement e) { var a = V3a(e); return new Vector3(a[0], a[1], a[2]); }

    public Airframe(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var J = doc.RootElement;
        // ---- transforms
        var parentOf = new Dictionary<string, string>();
        foreach (var t in J.GetProperty("transforms").EnumerateArray())
        {
            var go = new GameObject(t.GetProperty("name").GetString());
            var tr = go.transform;
            var lp = V3a(t.GetProperty("lp")); var lr = V3a(t.GetProperty("lr")); var ls = V3a(t.GetProperty("ls"));
            tr.localPosition = new Vector3(lp[0], lp[1], lp[2]);
            tr.localRotation = new Quaternion(lr[0], lr[1], lr[2], lr[3]);
            tr.localScale = new Vector3(ls[0], ls[1], ls[2]);
            string id = t.GetProperty("id").GetString();
            T[id] = tr;
            var p = t.GetProperty("parent");
            if (p.ValueKind == JsonValueKind.String) parentOf[id] = p.GetString();
        }
        foreach (var kv in parentOf) if (T.TryGetValue(kv.Value, out var pt)) T[kv.Key].parent = pt;
        root = T[J.GetProperty("root").GetString()];
        ifrit = J.TryGetProperty("stockFbw", out stockFbwJ); hasStockFbw = ifrit;
        revoker = ifrit && root.gameObject.name == "Fighter1";
        compass = ifrit && root.gameObject.name == "trainer";   // T/A-30 Compass (stock names kept, like the Revoker)
        if (ifrit) { stockFbwJ = stockFbwJ.Clone(); if (!revoker && !compass) foreach (var tr0 in T.Values) if (IfritNames.TryGetValue(tr0.gameObject.name, out var nn)) tr0.gameObject.name = nn; }
        rootGo = root.gameObject;
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;

        ac = new Aircraft(); rootGo.Add(ac);
        var prm = new AircraftParameters(); rootGo.Add(prm); ac.parameters = prm;
        // ---- airfoils
        var foils = new List<Airfoil>();
        foreach (var a in J.GetProperty("airfoils").EnumerateArray())
            foils.Add(new Airfoil { name = a.GetProperty("name").GetString(), liftCoef = Curve(a.GetProperty("lift")), dragCoef = Curve(a.GetProperty("drag")) });
        prm.airfoils = foils.ToArray();
        liftCharts = foils.Select(f => Chart(f.liftCoef)).ToArray();
        dragCharts = foils.Select(f => Chart(f.dragCoef)).ToArray();

        // ---- parts (complex physics: one rigidbody per AeroPart)
        var partByTid = new Dictionary<string, AeroPart>();
        var colliders = new Dictionary<AeroPart, List<JsonElement>>();
        foreach (var p in J.GetProperty("parts").EnumerateArray())
        {
            var tr = T[p.GetProperty("tid").GetString()];
            var ap = new AeroPart
            {
                mass = p.GetProperty("mass").GetSingle(),
                wingArea = p.GetProperty("wingArea").GetSingle(),
                dragArea = p.GetProperty("dragArea").GetSingle(),
                airfoil = p.GetProperty("airfoil").GetInt32(),
                centerOfLift = V3(p.GetProperty("col")),
                airflowChanneling = p.GetProperty("channel").GetSingle(),
            };
            tr.gameObject.Add(ap);
            ap.liftNormal = T.TryGetValue(p.GetProperty("liftNormal").GetString(), out var ln) ? ln : tr;
            partByTid[p.GetProperty("tid").GetString()] = ap;
            if (p.TryGetProperty("joints", out var jj)) JointsRaw[ap] = jj.Clone();
            colliders[ap] = p.GetProperty("colliders").EnumerateArray().ToList();
            ac.partLookup.Add(ap);
        }
        PartByTidPublic = partByTid;
        // fuel (full) + stores to the logged spawn mass
        foreach (var tk in J.GetProperty("tanks").EnumerateArray())
            if (partByTid.TryGetValue(tk.GetProperty("tid").GetString(), out var ap)) ap.mass += tk.GetProperty("capacity").GetSingle() * ((MassTarget >= 0f || ifrit) ? FuelFrac : 1f);
        float m0 = ac.partLookup.Sum(u => u.mass);
        if (MassTarget >= 0f) totalMassTarget = MassTarget;
        else if (ifrit) totalMassTarget = 0f;   // Ifrit: empty parts + fuel (FuelFrac) only
        var rear = (AeroPart)ac.partLookup.First(u => (revoker || compass) ? u.name == "tail" : u.name.EndsWith(ifrit ? "fuselage_R" : "_Rear"));
        if (totalMassTarget > m0)
        {
            // 31 Oct calibration: on the Revoker the spawn mass above tanks + structure is stores, carried in the weapon bays, not the tail
            var bays = revoker ? ac.partLookup.Where(u => u.name.StartsWith("weaponbay")).Cast<AeroPart>().ToList() : null;
            if (bays != null && bays.Count > 0) foreach (var bp in bays) bp.mass += (totalMassTarget - m0) / bays.Count;
            else rear.mass += totalMassTarget - m0;
        }
        if (StoreKg > 0f) { foreach (var spn in StorePart.Split('+')) { var sp = (AeroPart)ac.partLookup.First(u => u.name.EndsWith("_" + spn) || u.name == spn); sp.mass += StoreKg; sp.dragArea += StoreDrag; } }

        // ---- control surfaces, in the order the game's log listed them
        string[] order = { "Elevator_L", "Rudder_L", "Elevator_R", "Rudder_R", "Aileron_L", "Flap_L", "Aileron_R", "Flap_R" };
        var csList = J.GetProperty("surfaces").EnumerateArray().ToList();
        if (ifrit) order = new[] { "Elevator_L", "Elevator_R", "Aileron_L", "Flap_L", "Aileron_R", "Flap_R" };
        if (revoker) order = new[] { "canard_L", "canard_R", "aileron_L", "aileron_R", "rudder" };
        if (compass) order = csList.Select(x => x.GetProperty("name").GetString()).ToArray();   // the prefab's own order
        string SName(JsonElement x) { var n = x.GetProperty("name").GetString(); return ifrit && !revoker && !compass && IfritNames.TryGetValue(n, out var nn) ? nn : n; }
        foreach (var o in order)
        {
            var s = csList.First(x => (revoker || compass) ? SName(x) == o : SName(x).EndsWith(o));
            var cs = new ControlSurface
            {
                aircraft = ac,
                attachedSurface = partByTid[s.GetProperty("attached").GetString()],
                pitchRange = s.GetProperty("pitchRange").GetSingle(),
                rollRange = s.GetProperty("rollRange").GetSingle(),
                yawRange = s.GetProperty("yawRange").GetSingle(),
                brakeRange = s.GetProperty("brakeRange").GetSingle(),
                maxSplit = s.GetProperty("maxSplit").GetSingle(),
                servoSpeed = s.GetProperty("servo").GetSingle(),
                splitDrag = s.TryGetProperty("splitDrag", out var sdj) ? sdj.GetSingle() : 0f,
                yawSplitFactor = s.TryGetProperty("yawSplitFactor", out var ysj) ? ysj.GetSingle() : 0f,
            };
            T[s.GetProperty("tid").GetString()].gameObject.Add(cs);
            cs.visibleMesh = T[s.GetProperty("visible").GetString()].gameObject;
            cs.job.restingRotation = cs.visibleMesh.transform.localRotation;
            cs.job.pitchRange = cs.pitchRange; cs.job.rollRange = cs.rollRange; cs.job.yawRange = cs.yawRange;
            cs.job.servoSpeed = cs.servoSpeed; cs.job.maxSplit = cs.maxSplit;
            surfaces.Add(cs); ac.controlSurfaces.Add(cs);
        }
        // ---- engines
        var nozByTid = new Dictionary<string, JetNozzle>();
        foreach (var n in J.GetProperty("nozzles").EnumerateArray())
        {
            var jn = new JetNozzle { thrustTransform = T[n.GetProperty("thrustT").GetString()], abThrust = n.GetProperty("ab").EnumerateArray().Sum(x => x.GetSingle()) };
            if (n.TryGetProperty("abStart", out var abs) && abs.GetArrayLength() > 0) jn.abStart = abs[0].GetSingle();
            var pt = n.GetProperty("part");
            if (pt.ValueKind == JsonValueKind.String && partByTid.TryGetValue(pt.GetString(), out var pp)) jn.part = pp;
            T[n.GetProperty("tid").GetString()].gameObject.Add(jn);
            nozByTid[n.GetProperty("tid").GetString()] = jn;
        }
        foreach (var e in J.GetProperty("engines").EnumerateArray())
        {
            var tf = new Turbofan
            {
                aircraft = ac,
                staticThrust = e.GetProperty("staticThrust").GetSingle(),
                thrustVectoring = V3(e.GetProperty("vec")),
                thrustVectoringGain = V3(e.GetProperty("gain")),
                thrustVectoringMaxAirspeed = e.GetProperty("vecMaxSpeed").GetSingle(),
                vectoringTransforms = e.GetProperty("vecT").EnumerateArray().Select(x => T[x.GetString()]).ToArray(),
                nozzles = e.GetProperty("nozzles").EnumerateArray().Select(x => nozByTid[x.GetString()]).ToArray(),
                speedThrust = e.GetProperty("speedThrust").EnumerateArray().Select(x => V3a(x)).ToArray(),
            };
            if (e.TryGetProperty("kind", out var kj) && kj.GetString() == "turbojet")
            {
                tf.turbojet = true; tf.minRPM = e.GetProperty("minRPM").GetSingle(); tf.maxRPM = e.GetProperty("maxRPM").GetSingle();
                tf.spoolRate = e.GetProperty("spool").GetSingle(); tf.maxSpeed = e.GetProperty("maxSpeed").GetSingle();
                var rm = V3a(e.GetProperty("remap")); tf.remapX = rm[0]; tf.remapY = rm[1]; tf.rpm = tf.maxRPM;
                tf.altCurve = e.GetProperty("alt").EnumerateArray().Select(x => V3a(x)).ToArray();
            }
            T[e.GetProperty("tid").GetString()].gameObject.Add(tf);
            engines.Add(tf);
        }
        // ---- high-lift devices + flap drives + gear
        foreach (var h in J.GetProperty("hlds").EnumerateArray())
        {
            var ap = partByTid[h.GetProperty("part").GetString()];
            var d = new HighLiftDevice
            {
                aeroPart = ap,
                speedDeployed = h.GetProperty("speedDeployed").GetSingle(),
                speedRetracted = h.GetProperty("speedRetracted").GetSingle(),
                alphaMin = h.GetProperty("alphaMin").GetSingle(),
                alphaFactor = h.GetProperty("alphaFactor").GetSingle(),
                partAreaRetracted = ap.wingArea,
                partAreaDeployed = ap.wingArea + h.GetProperty("deployedArea").GetSingle(),
                movingParts = h.GetProperty("moving").EnumerateArray().Select(m => new MovingPart
                {
                    transform = T.TryGetValue(m.GetProperty("tid").GetString(), out var mt) ? mt : null,
                    move = B(m.GetProperty("move")),
                    rotate = B(m.GetProperty("rotate")),
                    positionRetracted = V3(m.GetProperty("pR")), positionDeployed = V3(m.GetProperty("pD")),
                    anglesRetracted = V3(m.GetProperty("aR")), anglesDeployed = V3(m.GetProperty("aD")),
                }).ToArray(),
            };
            T[h.GetProperty("tid").GetString()].gameObject.Add(d);
            hlds.Add(d);
        }
        foreach (var r in J.GetProperty("responders").EnumerateArray())
        {
            var resp = new Aryx_F22E_StrikeRaptor.AryxAlphaResponder { rotator = T[r.GetProperty("rotator").GetString()] };
            T[r.GetProperty("tid").GetString()].gameObject.Add(resp);
        }
        foreach (var g in J.GetProperty("gears").EnumerateArray()) T[g.GetString()].gameObject.Add(new LandingGear());
        // 31 Oct calibration: the Revoker's dedicated airbrake (game Airbrake: drag = dragAmount x rho x v^2 x open at the part's rigidbody)
        if (J.TryGetProperty("airbrakes", out var abJ))
            foreach (var ab in abJ.EnumerateArray())
            {
                var pt = ab.GetProperty("part");
                if (pt.ValueKind != JsonValueKind.String || !partByTid.TryGetValue(pt.GetString(), out var abp)) continue;
                airbrakes.Add(new Airbrake { part = abp, dragAmount = ab.GetProperty("dragAmount").GetSingle(), maxAngle = ab.GetProperty("maxAngle").GetSingle(), openSpeed = ab.GetProperty("openSpeed").GetSingle() });
            }

        // collect components (partLookup first, like the game's GetComponentsInChildren ordering is irrelevant here)
        ac.all.AddRange(ac.partLookup); ac.all.AddRange(surfaces); ac.all.AddRange(engines);
        foreach (var e in engines) ac.all.AddRange(e.nozzles);
        ac.all.AddRange(hlds);
        foreach (var tr in T.Values) foreach (var c in tr.gameObject.components) if (c is Aryx_F22E_StrikeRaptor.AryxAlphaResponder || c is LandingGear) ac.all.Add(c);

        if (ifrit) ApplyIfritSetup(); else ApplyRegistrySetup();
        if (!string.IsNullOrEmpty(Stores))
            foreach (var st in Stores.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var f = st.Split(':'); var sp = (AeroPart)ac.partLookup.First(u => u.name.EndsWith("_" + f[0]) || u.name == "Aryx_" + f[0]);
                sp.mass += float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture);
                if (f.Length > 2) sp.dragArea += float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture);
            }
        // Compass 0.1.0: the lift-driven tail downwash reads the wing's own airfoil (Registry does the same in game)
        if (compass) { var w1 = (AeroPart)ac.partLookup.First(u => u.name == "wing1_L"); Downwash.SetWing(new AirfoilTable(prm.airfoils[w1.airfoil])); }
        BuildBodies(colliders);
        if (!string.IsNullOrEmpty(Detach)) ApplyDetach(Detach.Split('+'));
        MassProps();
    }

    // Ifrit: stock airframe. Optional exploration knobs (all 1/0 = stock).
    public static float IfritTvcPitch = -1f;     // <0: stock (15)
    public static float IfritTvcRoll = -1f;      // <0: stock (+-15 differential); 0 = pitch-only (F-22 principle)
    public static float IfritTvcYawDeg = 0f;   // exploration: give the nozzles a yaw axis (stock 0)
    public static float IfritThrustScale = 1f, IfritStabEff = 1f, IfritFlapEff = 1f, IfritServoScale = 1f, IfritTailRange = -1f;
    void ApplyIfritSetup()
    {
        foreach (var tf in engines)
        {
            var v = tf.thrustVectoring;
            if (IfritTvcPitch >= 0f) v.x = IfritTvcPitch;
            if (IfritTvcRoll >= 0f) v.z = Mathf.Sign(v.z == 0 ? 1 : v.z) * IfritTvcRoll;
            if (IfritTvcYawDeg > 0f) v.y = IfritTvcYawDeg;
            tf.thrustVectoring = v;
            tf.staticThrust *= IfritThrustScale;
            foreach (var n in tf.nozzles) n.abThrust *= IfritThrustScale;
        }
        foreach (var cs in surfaces)
        {
            cs.servoSpeed *= IfritServoScale; cs.job.servoSpeed = cs.servoSpeed;
            var ap = cs.attachedSurface as AeroPart;
            if (cs.name.ToLowerInvariant().Contains("elevator")) { ap.wingArea *= IfritStabEff; if (IfritTailRange > 0f) { cs.pitchRange = -IfritTailRange; cs.job.pitchRange = cs.pitchRange; } }
            else ap.wingArea *= IfritFlapEff;
        }
        foreach (var h in hlds) { h.partAreaRetracted = h.aeroPart.wingArea; }
        CgShiftLog = "Ifrit: stock airframe";
        if (compass)
        {
            // Compass 0.1.0 (Registry.EnsureSetup mirror): thrust profile, LERX (wingroot1) lift multiplier, CG shift cockpit_F -> tail
            foreach (var tf in engines) { tf.staticThrust *= Plugin.ThrustScale; foreach (var n in tf.nozzles) n.abThrust *= Plugin.ThrustScale; }
            foreach (var u in ac.partLookup) if (u.name.StartsWith("wingroot1_")) ((AeroPart)u).wingArea *= Plugin.LerxEffectiveness;
            var fore = ac.partLookup.First(u => u.name == "cockpit_F"); var back = ac.partLookup.First(u => u.name == "tail");
            float tot = ac.partLookup.Sum(u => u.mass);
            float dz = Vector3.Dot(fore.transform.position - back.transform.position, root.forward);
            float mv = Mathf.Min(Plugin.CgShiftAft * tot / Mathf.Max(1f, dz), 0.6f * fore.mass);
            fore.mass -= mv; back.mass += mv;
            CgShiftLog = $"Compass CG shift: {mv:F0} kg {fore.name} -> {back.name} ({dz:F2} m) of {tot:F0} kg; LERX x{Plugin.LerxEffectiveness:F2} ({(Plugin.StrakeVortexLift ? "vortex" : "foil")})";
            RemapCharts();
        }
    }

    // Registry.EnsureSetup, applied with the shipped config values
    void ApplyRegistrySetup()
    {
        foreach (var tf in engines)
        {
            tf.thrustVectoring = new Vector3(Plugin.TvcRange > 0f ? Plugin.TvcRange : tf.thrustVectoring.x, 0f, 0f);
            tf.staticThrust *= Plugin.ThrustScale;
            foreach (var n in tf.nozzles) n.abThrust *= Plugin.ThrustScale;
        }
        foreach (var u in ac.partLookup) ((AeroPart)u).dragArea *= Plugin.DragScale;
        foreach (var u in ac.partLookup) if (u.name.Contains("LERX")) ((AeroPart)u).wingArea *= Plugin.LerxEffectiveness;
        var fore = ac.partLookup.First(u => u.name.EndsWith("_Fore"));
        var back = ac.partLookup.First(u => u.name.EndsWith("_Back"));
        float tot = ac.partLookup.Sum(u => u.mass);
        float dz = Vector3.Dot(fore.transform.position - back.transform.position, root.forward);
        float mv = Mathf.Min(Plugin.CgShiftAft * tot / Mathf.Max(1f, dz), 0.6f * fore.mass);
        fore.mass -= mv; back.mass += mv;
        CgShiftLog = $"CG shift: {mv:F0} kg moved {fore.name} -> {back.name} ({dz:F2} m) of {tot:F0} kg";
        foreach (var cs in surfaces)
        {
            cs.servoSpeed *= Plugin.ActuatorScale;
            cs.job.servoSpeed = cs.servoSpeed;
            var ap = cs.attachedSurface as AeroPart;
            float k = 1f;
            if (Mathf.Abs(cs.pitchRange) > 0.01f || cs.name.Contains("Elevator")) k = Plugin.StabilatorEffectiveness;
            else if (cs.name.Contains("Flap") || cs.name.Contains("Aileron")) k = Plugin.FlaperonEffectiveness;
            ap.wingArea *= k;
        }
        foreach (var h in hlds) { h.partAreaRetracted = h.aeroPart.wingArea; }
        RemapCharts();
    }
    // 2.0.0 staggered stall (Compass 0.1.1: also the strakes' vortex-lift chart): same remap as StallStaggerJobs, appended charts + airfoilID
    void RemapCharts()
    {
        if (Aryx_F22E_StrikeRaptor.FCS.StallStagger.Enabled)
        {
            var L = liftCharts.ToList(); var Dd = dragCharts.ToList();
            var ids = new Dictionary<string, int>();
            foreach (var u in ac.partLookup)
            {
                var ap = (AeroPart)u; var f = Aryx_F22E_StrikeRaptor.FCS.StallStagger.FamilyOf(ap.name);
                if (f == Aryx_F22E_StrikeRaptor.FCS.StallStagger.Fam.None || ap.airfoil < 0) continue;
                var af = ac.parameters.airfoils[ap.airfoil];
                string key = Aryx_F22E_StrikeRaptor.FCS.StallStagger.Key(af, f);
                if (!ids.TryGetValue(key, out int id))
                {
                    var cl = new float[128]; var cd = new float[128];
                    Aryx_F22E_StrikeRaptor.FCS.StallStagger.BuildFor(f, r => af.liftCoef.Evaluate(r), r => af.dragCoef.Evaluate(r), cl, cd);
                    id = L.Count; L.Add(cl); Dd.Add(cd); ids[key] = id;
                }
                ap.airfoilID = id;
            }
            liftCharts = L.ToArray(); dragCharts = Dd.ToArray();
        }
    }
    public string CgShiftLog;

    static AnimationCurve Curve(JsonElement c)
    {
        var keys = c.GetProperty("keys").EnumerateArray().Select(k => { var a = V3a(k); return new Keyframe(a[0], a[1], a[2], a[3]); }).ToArray();
        return new AnimationCurve(keys);
    }
    static float[] Chart(AnimationCurve c)
    {
        var r = new float[128];
        for (int i = 0; i < 128; i++) r[i] = c.Evaluate((i - 64) * 0.04908734f);
        return r;
    }

    // Unity derives each body's COM and inertia from its colliders at uniform density. Colliders are
    // approximated as solid boxes (mesh AABB / box / capsule / sphere bounds).
    void BuildBodies(Dictionary<AeroPart, List<JsonElement>> colliders)
    {
        foreach (AeroPart ap in ac.partLookup)
        {
            var boxes = new List<(Vector3 c, Quaternion r, Vector3 s)>();
            var hulls = new List<(Vector3 c, Quaternion r, double V, double[,] I)>();
            foreach (var c in colliders[ap])
            {
                if (c.TryGetProperty("trigger", out var tg) && (tg.ValueKind == JsonValueKind.True || (tg.ValueKind == JsonValueKind.Number && tg.GetInt32() != 0))) continue;
                if (c.TryGetProperty("nomesh", out _)) continue;
                var t = T[c.GetProperty("tid").GetString()];
                string type = c.GetProperty("type").GetString();
                if (UseHulls && c.TryGetProperty("hull", out var hj))
                {
                    var hc = V3(hj.GetProperty("C"));
                    var Ih = new double[3, 3]; int ii = 0;
                    foreach (var row in hj.GetProperty("I").EnumerateArray()) { int jj = 0; foreach (var x in row.EnumerateArray()) Ih[ii, jj++] = x.GetDouble(); ii++; }
                    hulls.Add((t.position + t.rotation * hc, t.rotation, hj.GetProperty("V").GetDouble(), Ih));
                    continue;
                }
                Vector3 ctr = V3(c.GetProperty("center"));
                Vector3 size;
                if (type == "CapsuleCollider")
                {
                    float r = c.GetProperty("radius").GetSingle(), h = c.GetProperty("height").GetSingle();
                    int dir = c.GetProperty("dir").GetInt32();
                    size = new Vector3(2 * r, 2 * r, 2 * r); size[dir] = Mathf.Max(h, 2 * r);
                }
                else if (type == "SphereCollider") { float r = c.GetProperty("radius").GetSingle(); size = new Vector3(2 * r, 2 * r, 2 * r); }
                else size = V3(c.GetProperty("size"));
                var ls = t.lossyScale;
                size = new Vector3(Mathf.Max(0.05f, Mathf.Abs(size.x * ls.x)), Mathf.Max(0.05f, Mathf.Abs(size.y * ls.y)), Mathf.Max(0.05f, Mathf.Abs(size.z * ls.z)));
                boxes.Add((t.TransformPoint(ctr), t.rotation, size));
            }
            if (boxes.Count == 0 && hulls.Count == 0) boxes.Add((ap.transform.position, ap.transform.rotation, new Vector3(0.5f, 0.5f, 0.5f)));
            double vol = boxes.Sum(b => (double)b.s.x * b.s.y * b.s.z) + hulls.Sum(h => h.V);
            Vector3 com = Vector3.zero;
            foreach (var b in boxes) com += b.c * (float)(b.s.x * b.s.y * b.s.z / vol);
            foreach (var h in hulls) com += h.c * (float)(h.V / vol);
            var I = new double[3, 3];
            foreach (var h in hulls)
            {
                double m = ap.mass * h.V / vol, k = m / h.V;
                var R = Rot(h.r);
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double s2 = 0; for (int a = 0; a < 3; a++) for (int c2 = 0; c2 < 3; c2++) s2 += R[i, a] * h.I[a, c2] * k * R[j, c2]; I[i, j] += s2; }
                Vector3 r = h.c - com; double r2 = r.sqrMagnitude;
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) I[i, j] += m * ((i == j ? r2 : 0) - r[i] * r[j]);
            }
            foreach (var b in boxes)
            {
                double m = ap.mass * (b.s.x * b.s.y * b.s.z) / vol;
                double[] d = { m / 12 * (b.s.y * b.s.y + b.s.z * b.s.z), m / 12 * (b.s.x * b.s.x + b.s.z * b.s.z), m / 12 * (b.s.x * b.s.x + b.s.y * b.s.y) };
                var R = Rot(b.r);
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double s = 0; for (int k = 0; k < 3; k++) s += R[i, k] * d[k] * R[j, k]; I[i, j] += s; }
                Vector3 r = b.c - com;
                double r2 = r.sqrMagnitude;
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) I[i, j] += m * ((i == j ? r2 : 0) - r[i] * r[j]);
            }
            // root frame is world at build time (root at origin, identity)
            var pb = new PartBody { part = ap, comB = com, IownB = I };
            AllPartNames.Add(ap.name);
            var rbGo = ap.gameObject;
            var rb = new Rigidbody { mass = ap.mass };
            rb.gameObject = rbGo;
            // express own inertia in the part's local frame and diagonalise it, as Unity stores it
            var Rp = Rot(ap.transform.rotation);
            var Il = new double[3, 3];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double s = 0; for (int a = 0; a < 3; a++) for (int c = 0; c < 3; c++) s += Rp[a, i] * I[a, c] * Rp[c, j]; Il[i, j] = s; }
            Jacobi(Il, out var eval, out var evec);
            rb.inertiaTensor = new Vector3((float)eval[0], (float)eval[1], (float)eval[2]);
            rb.inertiaTensorRotation = QuatFromMatrix(evec);
            ap.rb = rb;
            pb.rb = rb;
            bodies.Add(pb);
        }
        ac.rb = bodies.First(b => b.part.transform == root).rb;
    }

    public readonly List<string> AllPartNames = new List<string>();
    public string DetachLog = "";
    void ApplyDetach(string[] suffixes) => DetachBodies(bodies.Where(b => suffixes.Any(x => string.Equals(b.part.name, x, StringComparison.OrdinalIgnoreCase) || b.part.name.EndsWith("_" + x, StringComparison.OrdinalIgnoreCase))).ToList());

    // Remove parts from the plant (mass, aero and inertia go with them at the next MassProps), flag their control surfaces
    // detached, switch off any responder under them. Shared by detach= (t = 0) and the damage model's mid-run detach.
    public void DetachBodies(List<PartBody> gone)
    {
        foreach (var b in gone) { b.part.detached = true; bodies.Remove(b); }
        foreach (var cs in surfaces) if (((AeroPart)cs.attachedSurface).detached) cs.job.IsDetached = true;
        var goneT = new HashSet<Transform>(gone.Select(b => b.part.transform));
        int nr = 0;
        foreach (var tr in T.Values) foreach (var c in tr.gameObject.components)
            if (c is Aryx_F22E_StrikeRaptor.AryxAlphaResponder r)
            {
                for (var p = r.rotator; p != null; p = p.parent) if (goneT.Contains(p)) { r.isFunctional = false; nr++; break; }
            }
        DetachLog = $"detached {gone.Count} parts ({gone.Sum(b => b.part.mass):F0} kg): {string.Join(",", gone.Select(b => b.part.name.Replace("Aryx_KingRaptor_", "")))}; responders off {nr}";
    }

    bool cgShiftApplied;
    public void MassProps()
    {
        // 31 Oct calibration: bench-only CG shift, mass moved between the foremost and aftmost centreline parts (nose -> tail for + aft)
        if (!cgShiftApplied && Mathf.Abs(CgAftShift) > 1e-6f)
        {
            cgShiftApplied = true;
            var centre = bodies.Where(b => Mathf.Abs(b.comB.x) < 0.5f && b.part.mass >= 300f).ToList(); if (centre.Count < 2) centre = bodies;
            var nose = centre.OrderByDescending(b => b.comB.z).First(); var tail = centre.OrderBy(b => b.comB.z).First();
            float dz = nose.comB.z - tail.comB.z, tot = bodies.Sum(b => b.part.mass);
            float mv = Mathf.Clamp(CgAftShift * tot / Mathf.Max(1f, dz), -0.6f * tail.part.mass, 0.6f * nose.part.mass);
            nose.part.mass -= mv; tail.part.mass += mv; nose.rb.mass = nose.part.mass; tail.rb.mass = tail.part.mass;
            CgShiftLog += $" (bench CgAftShift {CgAftShift:F3} m: {mv:F0} kg {nose.part.name} -> {tail.part.name})";
        }
        Ipa = new double[3, 3]; Iown = new double[3, 3]; Itot = new double[3, 3];
        mass = bodies.Sum(b => b.part.mass);
        cgB = Vector3.zero;
        foreach (var b in bodies) cgB += b.comB * (b.part.mass / mass);
        foreach (var b in bodies)
        {
            Vector3 r = b.comB - cgB; double r2 = r.sqrMagnitude;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                double pa = b.part.mass * ((i == j ? r2 : 0) - r[i] * r[j]);
                Ipa[i, j] += pa; Iown[i, j] += b.IownB[i, j]; Itot[i, j] += pa + b.IownB[i, j];
            }
        }
        // bench-only effective-inertia factors (Unity axes x = pitch, y = yaw, z = roll)
        { double[] sc = { IScalePitch, IScaleYaw, IScaleRoll }; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double k = Math.Sqrt(sc[i] * sc[j]); Itot[i, j] *= k; Ipa[i, j] *= k; } }
    }

    public static double[,] Rot(Quaternion q)
    {
        var m = Matrix4x4.Rotate(q);
        var r = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) r[i, j] = m[i, j];
        return r;
    }

    static void Jacobi(double[,] A0, out double[] eval, out double[,] V)
    {
        var A = (double[,])A0.Clone();
        V = new double[3, 3]; for (int i = 0; i < 3; i++) V[i, i] = 1;
        for (int sweep = 0; sweep < 50; sweep++)
        {
            double off = Math.Abs(A[0, 1]) + Math.Abs(A[0, 2]) + Math.Abs(A[1, 2]);
            if (off < 1e-9) break;
            for (int p = 0; p < 2; p++) for (int q = p + 1; q < 3; q++)
            {
                if (Math.Abs(A[p, q]) < 1e-12) continue;
                double th = 0.5 * Math.Atan2(2 * A[p, q], A[q, q] - A[p, p]);
                double c = Math.Cos(th), s = Math.Sin(th);
                for (int k = 0; k < 3; k++) { double akp = A[k, p], akq = A[k, q]; A[k, p] = c * akp - s * akq; A[k, q] = s * akp + c * akq; }
                for (int k = 0; k < 3; k++) { double apk = A[p, k], aqk = A[q, k]; A[p, k] = c * apk - s * aqk; A[q, k] = s * apk + c * aqk; }
                for (int k = 0; k < 3; k++) { double vkp = V[k, p], vkq = V[k, q]; V[k, p] = c * vkp - s * vkq; V[k, q] = s * vkp + c * vkq; }
            }
        }
        eval = new[] { A[0, 0], A[1, 1], A[2, 2] };
        // ensure right-handed
        double det = V[0, 0] * (V[1, 1] * V[2, 2] - V[1, 2] * V[2, 1]) - V[0, 1] * (V[1, 0] * V[2, 2] - V[1, 2] * V[2, 0]) + V[0, 2] * (V[1, 0] * V[2, 1] - V[1, 1] * V[2, 0]);
        if (det < 0) for (int k = 0; k < 3; k++) V[k, 2] = -V[k, 2];
    }

    static Quaternion QuatFromMatrix(double[,] m)
    {
        double tr = m[0, 0] + m[1, 1] + m[2, 2];
        double x, y, z, w;
        if (tr > 0) { double s = Math.Sqrt(tr + 1) * 2; w = 0.25 * s; x = (m[2, 1] - m[1, 2]) / s; y = (m[0, 2] - m[2, 0]) / s; z = (m[1, 0] - m[0, 1]) / s; }
        else if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2]) { double s = Math.Sqrt(1 + m[0, 0] - m[1, 1] - m[2, 2]) * 2; w = (m[2, 1] - m[1, 2]) / s; x = 0.25 * s; y = (m[0, 1] + m[1, 0]) / s; z = (m[0, 2] + m[2, 0]) / s; }
        else if (m[1, 1] > m[2, 2]) { double s = Math.Sqrt(1 + m[1, 1] - m[0, 0] - m[2, 2]) * 2; w = (m[0, 2] - m[2, 0]) / s; x = (m[0, 1] + m[1, 0]) / s; y = 0.25 * s; z = (m[1, 2] + m[2, 1]) / s; }
        else { double s = Math.Sqrt(1 + m[2, 2] - m[0, 0] - m[1, 1]) * 2; w = (m[1, 0] - m[0, 1]) / s; x = (m[0, 2] + m[2, 0]) / s; y = (m[1, 2] + m[2, 1]) / s; z = 0.25 * s; }
        return new Quaternion((float)x, (float)y, (float)z, (float)w).normalized;
    }
}
