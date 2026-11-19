using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

// Part damage as the game does it (Assembly-CSharp, read 2 Nov 2026; project doc claude/Game_damage_model_Raptor.md), on the
// bench's single rigid body. Data per part comes from the aircraft's own bundle (extract/extract_damage.py -> data/*.json).
//
// 1. Damaged but attached: AeroPart.ApplyDamage sets wingEffectiveness e = Lerp(0.5, 1, hp/100); the plant already applies e
//    to lift, profile drag and the damage drag (dragArea + 0.1 wingArea)(1 - e), as AeroJob_Math does, and the FCS reads e
//    through Acc.AP_wingEff as it does in game.
// 2. Coming off: the local player's jet runs jointed physics (Aircraft.SetComplexPhysics): every part is a rigid body held by
//    one FixedJoint per PartJoint, break force / torque = listed x 10, rescaled on every hit by max(hp - structural, 0) /
//    (100 - structural). Here, every tick, each part whose own joints are what hold it on gets the force and moment those
//    joints must carry for everything hanging off it (m a at each part's COM, own inertia, minus aero, gravity and thrust),
//    taken about the mean of its joint anchors. Where other parts' joints connect into it too (a fin also held by a rudder
//    that hinges to the fuselage), the load splits between its own joints and those in proportion to their present strength.
//    When its share exceeds the summed capacity of its own intact joints, they all break (zero strength: at once); whatever
//    is still held hangs on the rest. Approximation: PhysX breaks a part's joints one at a time; here they go together.
//    Anything that has lost its path to the root through intact joints detaches.
// 3. Engines. Turbofan (Aryx's F-22E): own hit points minus weight x (threshold - hp) of each critical part below its
//    threshold, minus 100 x weight per critical part detached; condition = clamp01(1 - (damageThreshold - hp) / damageThreshold)
//    once hp is at or below damageThreshold, never rising; condition 0 kills the engine (operable = false).
//    Turbojet (stock Ifrit, Revoker): no hit points of its own; every hit on a critical part sets condition =
//    clamp((part hp - damageThreshold) / (100 - damageThreshold), 0, condition); a critical part detached kills it.
internal sealed class DamageModel
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    sealed class Joint { public PartD owner; public string to; public float F, Tq; public Transform anchor; public bool broken; }
    sealed class PartD
    {
        public string full, key; public PartBody body; public float hp = 100f, structural, integrity = float.MinValue;
        public readonly List<Joint> joints = new List<Joint>(); public bool gone;
        public List<PartD> sub;   // what hangs on this part: comes off with it when every joint touching it is gone
        public readonly List<Joint> into = new List<Joint>();   // other parts' joints connected to this one
        public List<Joint> paths = new List<Joint>();            // ... whose owner reaches the root without this part (a load path)
        public Vector3 jf, mreq;  // this tick: force the structure must apply to it, moment of everything about the CG
        public float HpFactor => Mathf.Max((hp - structural) / Mathf.Max(1e-3f, 100f - structural), 0f);
    }
    sealed class EngD { public Turbofan tf; public string key; public bool jet; public float thr, hpOwn = 100f; public List<(string part, float thr, float w)> crit; public float hp = 100f; }

    readonly Airframe A; Plant P;
    readonly Dictionary<string, PartD> byKey = new Dictionary<string, PartD>(StringComparer.OrdinalIgnoreCase);
    readonly List<PartD> parts = new List<PartD>();
    readonly List<EngD> engs = new List<EngD>();
    PartD root;
    readonly List<(float t, bool detach, List<(string key, float hp)> what)> events = new List<(float, bool, List<(string, float)>)>();
    readonly Dictionary<PartD, List<(JetNozzle n, Transform t)>> thrustOn = new Dictionary<PartD, List<(JetNozzle, Transform)>>();
    readonly Dictionary<PartD, (float J, float M, float fc, float tc)> pendingBreak = new Dictionary<PartD, (float, float, float, float)>();
    public readonly bool RipOff;
    public float WorstRatio; public string WorstPart = ""; public float WorstF, WorstT;
    StreamWriter csv;
    float lastT;

    public static string DataPathFor(string airframePath)
    {
        string env = Environment.GetEnvironmentVariable("BENCH_DMG");
        if (!string.IsNullOrEmpty(env)) return env;
        string b = AppContext.BaseDirectory;   // bench/bin/ -> bench/data/<aircraft>_damage.json (one per bench)
        foreach (var dir in new[] { Path.Combine(b, "..", "data"), Path.Combine(b, "data"), Path.Combine(b, "..", "..", "..", "..", "data") })
            if (Directory.Exists(dir)) { var f = Directory.GetFiles(dir, "*_damage.json"); if (f.Length == 1) return Path.GetFullPath(f[0]); }
        throw new FileNotFoundException("damage data not found: set BENCH_DMG to bench/data/<aircraft>_damage.json");
    }

    public static bool Wanted(Scenario sc) => !string.IsNullOrEmpty(sc.dmg) || !string.IsNullOrEmpty(sc.dmgAt) || !string.IsNullOrEmpty(sc.detachAt) || !string.IsNullOrEmpty(sc.detach) || sc.ripOff == 1f;

    // Build before the Plant and the trim: dmg= is in place for the trim, orphans of detach= go with their parent.
    public DamageModel(Airframe a, Scenario sc, string dataPath)
    {
        A = a; RipOff = sc.ripOff != 0f;
        using var doc = JsonDocument.Parse(File.ReadAllText(dataPath));
        // case-insensitive: the stock benches canonicalise some part names (aileron_R -> Aileron_R)
        var bodyBy = A.bodies.ToDictionary(b => b.part.name, b => b, StringComparer.OrdinalIgnoreCase);
        var detachedNames = new HashSet<string>(A.AllPartNames.Where(n => !bodyBy.ContainsKey(n)), StringComparer.OrdinalIgnoreCase);
        foreach (var jp in doc.RootElement.GetProperty("parts").EnumerateObject())
        {
            if (!bodyBy.ContainsKey(jp.Name) && !detachedNames.Contains(jp.Name)) continue;
            var pd = new PartD { full = jp.Name, body = bodyBy.TryGetValue(jp.Name, out var bb) ? bb : null, gone = !bodyBy.ContainsKey(jp.Name) };
            var v = jp.Value;
            pd.hp = v.GetProperty("hp").GetSingle(); pd.structural = v.GetProperty("structural").GetSingle();
            pd.integrity = (float)Math.Max(v.GetProperty("integrity").GetDouble(), float.MinValue);
            foreach (var j in v.GetProperty("joints").EnumerateArray())
            {
                var jo = new Joint { owner = pd, to = j.GetProperty("to").GetString(), F = j.GetProperty("breakForce").GetSingle(), Tq = j.GetProperty("breakTorque").GetSingle() };
                var at = j.GetProperty("anchorTid");
                if (at.ValueKind == JsonValueKind.String && A.T.TryGetValue(at.GetString(), out var atr)) jo.anchor = atr;
                pd.joints.Add(jo);
            }
            parts.Add(pd);
        }
        // the root is the part nobody's joints are owned by (Aryx_KingRaptor: no joints of its own)
        root = parts.Where(p => p.joints.Count == 0).OrderByDescending(p => p.body?.part.mass ?? 0f).First();
        string prefix = root.full + "_";
        foreach (var p in parts) { p.key = p.full == root.full ? "Root" : (p.full.StartsWith(prefix) ? p.full.Substring(prefix.Length) : p.full); byKey[p.key] = p; }
        foreach (var p in parts) foreach (var j in p.joints) if (byKey.TryGetValue(KeyOf(j.to), out var q) && q != p) q.into.Add(j);
        var jengs = doc.RootElement.GetProperty("engines").EnumerateObject().ToList();
        foreach (var je in jengs)
        {
            string side = je.Name.EndsWith("_L") ? "_L" : je.Name.EndsWith("_R") ? "_R" : "";
            var tf = (jengs.Count == 1 && A.engines.Count == 1) ? A.engines[0]
                   : A.engines.FirstOrDefault(e => e.name == je.Name || e.name.EndsWith(je.Name) || (side != "" && e.name.EndsWith(side)));
            if (tf == null) { Console.Error.WriteLine($"# dmg: no bench engine for {je.Name}"); continue; }
            bool jet = je.Value.TryGetProperty("model", out var mdl) && mdl.GetString() == "turbojet";
            var ed = new EngD { tf = tf, jet = jet, key = jengs.Count == 1 ? "Eng" : "Eng" + side.TrimStart('_'), thr = je.Value.GetProperty("damageThreshold").GetSingle(), hpOwn = je.Value.GetProperty("hp").GetSingle(),
                crit = je.Value.GetProperty("critical").EnumerateArray().Select(c => (KeyOf(c.GetProperty("part").GetString()), c.GetProperty("threshold").GetSingle(), c.GetProperty("weight").GetSingle())).ToList() };
            engs.Add(ed);
            foreach (var n in tf.nozzles)
            {
                var owner = OwnerOf(n.thrustTransform);
                if (owner == null) continue;
                if (!thrustOn.TryGetValue(owner, out var l)) thrustOn[owner] = l = new List<(JetNozzle, Transform)>();
                l.Add((n, n.thrustTransform));
            }
        }
        // timed events (seconds from the pull start)
        foreach (var e in Split(sc.dmgAt, ',')) { var c = e.IndexOf(':'); events.Add((float.Parse(e.Substring(0, c), IC), false, ParseHp(e.Substring(c + 1)))); }
        foreach (var e in Split(sc.detachAt, ',')) { var c = e.IndexOf(':'); events.Add((float.Parse(e.Substring(0, c), IC), true, e.Substring(c + 1).Split('+').Select(k => (k.Trim(), 0f)).ToList())); }
        events.Sort((x, y) => x.t.CompareTo(y.t));
        if (!string.IsNullOrEmpty(sc.dmgCsv)) { csv = new StreamWriter(sc.dmgCsv); csv.WriteLine("t,mass,attached,condL,condR,opL,opR,worstPart,worstRatio,worstF_kN,worstT_kNm,events"); }
        // t = 0: hit points, then whatever detach= orphaned
        foreach (var (k, hp) in ParseHp(sc.dmg)) SetHp(k, hp, -1f);
        var orphans = Unreachable();
        var log = new List<string>();
        foreach (var e in engs) UpdateEngine(e, log);
        if (orphans.Count > 0) { log.Add("orphaned by detach=: " + string.Join(",", orphans.Select(p => p.key))); RemoveParts(orphans, null); }
        RebuildSubs();
        foreach (var l in log) Console.Error.WriteLine("# dmg t=0 " + l);
    }

    public void Attach(Plant p) { P = p; P.PartDump ??= new Dictionary<AeroPart, (Vector3, Vector3, float, float)>(); P.preIntegrate = Loads; }

    static IEnumerable<string> Split(string s, char c) => string.IsNullOrWhiteSpace(s) ? Enumerable.Empty<string>() : s.Split(c).Select(x => x.Trim()).Where(x => x.Length > 0);
    static List<(string, float)> ParseHp(string s) => Split(s, '+').Select(x => { var i = x.LastIndexOf(':'); return (x.Substring(0, i).Trim(), float.Parse(x.Substring(i + 1), IC)); }).ToList();
    string KeyOf(string full) => string.Equals(full, root.full, StringComparison.OrdinalIgnoreCase) ? "Root" : full.StartsWith(root.full + "_", StringComparison.OrdinalIgnoreCase) ? full.Substring(root.full.Length + 1) : full;

    PartD OwnerOf(Transform t)
    {
        for (var x = t; x != null; x = x.parent) { var p = parts.FirstOrDefault(q => q.body != null && q.body.part.transform == x); if (p != null) return p; }
        return null;
    }

    void SetHp(string key, float hp, float t)
    {
        var e = engs.FirstOrDefault(x => string.Equals(x.key, key, StringComparison.OrdinalIgnoreCase));
        if (e != null)
        {
            if (e.jet) throw new ArgumentException($"damage: {key} is a turbojet (no hit points of its own): damage its critical parts ({string.Join(" ", e.crit.Select(c => c.part))})");
            e.hpOwn = Mathf.Min(e.hpOwn, hp); return;
        }
        if (!byKey.TryGetValue(key, out var p)) throw new ArgumentException($"damage: no part '{key}' (known: {string.Join(" ", byKey.Keys)}, {string.Join(" ", engs.Select(x => x.key))})");
        p.hp = Mathf.Min(p.hp, hp);   // damage only lowers hit points
        if (p.body != null) p.body.part.wingEffectiveness = Mathf.Lerp(0.5f, 1f, p.hp * 0.01f);
        if (p.hp < p.integrity) foreach (var j in p.joints) j.F = j.Tq = 0f;   // SpawnFragments: breakForce/Torque 0
    }

    void UpdateEngine(EngD e, List<string> log)
    {
        if (e.jet)
        {
            if (!e.tf.operable) return;
            float c0 = e.tf.condition; bool kill = false;
            foreach (var (k, thr, w) in e.crit)
            {
                if (!byKey.TryGetValue(k, out var p)) continue;
                if (p.gone) { kill = true; break; }
                if (p.hp < 100f) c0 = Mathf.Min(c0, Mathf.Clamp((p.hp - e.thr) / (100f - e.thr), 0f, 1f));
            }
            if (!kill && c0 < e.tf.condition - 1e-5f) { e.tf.condition = c0; log?.Add($"{e.key} condition {c0:F2}"); }
            if (kill || e.tf.condition <= 0f) { e.tf.condition = 0f; e.tf.operable = false; log?.Add($"{e.key} killed"); }
            return;
        }
        float hp = e.hpOwn;
        foreach (var (k, thr, w) in e.crit)
        {
            if (!byKey.TryGetValue(k, out var p)) continue;
            hp -= w * Mathf.Max(0f, thr - Mathf.Max(p.hp, -1e6f));
            if (p.gone) hp -= 100f * w;
        }
        if (hp >= e.hp - 1e-4f) return;
        e.hp = hp;
        if (!e.tf.operable || hp > e.thr) return;
        float c = Mathf.Clamp01(1f - (e.thr - hp) / e.thr);
        if (c < e.tf.condition) { e.tf.condition = c; log?.Add($"{e.key} hp {hp:F0} condition {c:F2}"); }
        if (c <= 0f) { e.tf.operable = false; log?.Add($"{e.key} killed"); }
    }

    // ---- topology: what is still held to the root through intact joints between attached parts
    IEnumerable<(PartD a, PartD b)> Edges(Func<Joint, bool> skip = null)
    {
        foreach (var p in parts)
        {
            if (p.gone) continue;
            foreach (var j in p.joints)
            {
                if (j.broken || (skip != null && skip(j))) continue;
                if (!byKey.TryGetValue(KeyOf(j.to), out var q) || q.gone) continue;
                yield return (p, q);
            }
        }
    }
    HashSet<PartD> Reach(Func<Joint, bool> skip)
    {
        var adj = parts.Where(p => !p.gone).ToDictionary(p => p, p => new List<PartD>());
        foreach (var (a, b) in Edges(skip)) { adj[a].Add(b); adj[b].Add(a); }
        var seen = new HashSet<PartD> { root }; var st = new Stack<PartD>(); st.Push(root);
        while (st.Count > 0) foreach (var n in adj[st.Pop()]) if (seen.Add(n)) st.Push(n);
        return seen;
    }
    List<PartD> Unreachable(Func<Joint, bool> skip = null) { var r = Reach(skip); return parts.Where(p => !p.gone && !r.Contains(p)).ToList(); }
    void RebuildSubs()
    {
        foreach (var p in parts)
        {
            if (p.gone || p == root || p.joints.All(j => j.broken)) { p.sub = null; continue; }
            var off = Unreachable(j => j.owner == p || string.Equals(KeyOf(j.to), p.key, StringComparison.OrdinalIgnoreCase));
            p.sub = off.Contains(p) ? off : new List<PartD> { p };
            // a joint into p carries p's load only if its owner still reaches the root with p gone (a rudder hinged to the
            // fuselage as well as the fin: yes; a rudder hanging on the fin alone: no)
            p.paths = p.into.Where(j => !j.broken && !j.owner.gone && !off.Contains(j.owner)).ToList();
        }
    }

    // ---- per tick, inside Plant.Step after the forces and before the integration (same pose, rates and forces)
    void Loads(Vector3 F, Vector3 M)
    {
        if (!RipOff) return;
        Vector3 aCg = F / A.mass, alpha = P.AngAccel(M), w = P.w, g = Physics.gravity;
        var R = Airframe.Rot(P.q);
        foreach (var p in parts)
        {
            if (p.gone || p.body == null) continue;
            var b = p.body; float m = b.part.mass;
            Vector3 c = b.rb.worldCenterOfMass, r = c - P.pCg;
            Vector3 a = aCg + Vector3.Cross(alpha, r) + Vector3.Cross(w, Vector3.Cross(w, r));
            Vector3 fAero = Vector3.zero, mAero = Vector3.zero;
            if (P.PartDump.TryGetValue(b.part, out var pd)) { fAero = pd.f; mAero = pd.m; }
            Vector3 fT = Vector3.zero, mT = Vector3.zero;
            if (thrustOn.TryGetValue(p, out var ns)) foreach (var (n, t) in ns) { var fr = t.forward * n.totalThrust; fT += fr; mT += Vector3.Cross(t.position - P.pCg, fr); }
            var Iw = Xform(R, b.IownB);
            p.jf = m * a - fAero - m * g - fT;
            p.mreq = Vector3.Cross(r, m * a) + Mul(Iw, alpha) + Vector3.Cross(w, Mul(Iw, w)) - mAero - Vector3.Cross(r, m * g) - mT;
        }
        WorstRatio = 0f;
        foreach (var p in parts)
        {
            if (p.gone || p.sub == null) continue;
            Vector3 J = Vector3.zero, Mc = Vector3.zero;
            foreach (var s in p.sub) { J += s.jf; Mc += s.mreq; }
            var live = p.joints.Where(j => !j.broken).ToList();
            Vector3 anc = Vector3.zero; foreach (var j in live) anc += (j.anchor != null ? j.anchor.position : p.body.part.transform.position); anc /= live.Count;
            Vector3 Ma = Mc - Vector3.Cross(anc - P.pCg, J);
            float k = 10f * p.HpFactor, fc = k * live.Sum(j => j.F), tc = k * live.Sum(j => j.Tq);
            // share with other parts' intact joints into this one (their own hit points scale them)
            float fo = 0f, to = 0f;
            foreach (var j in p.paths) if (!j.broken && !j.owner.gone) { float ko = 10f * j.owner.HpFactor; fo += ko * j.F; to += ko * j.Tq; }
            float sF = fc + fo > 0f ? fc / (fc + fo) : 1f, sT = tc + to > 0f ? tc / (tc + to) : 1f;
            J *= sF; Ma *= sT;
            float ratio = Mathf.Max(fc > 0f ? J.magnitude / fc : float.PositiveInfinity, tc > 0f ? Ma.magnitude / tc : float.PositiveInfinity);
            if (ratio > WorstRatio) { WorstRatio = ratio; WorstPart = p.key; WorstF = J.magnitude; WorstT = Ma.magnitude; }
            if (ratio > 1f && !pendingBreak.ContainsKey(p)) pendingBreak[p] = (J.magnitude, Ma.magnitude, fc, tc);
        }
    }

    // ---- after Plant.Step: timed events, breaks found this tick, engines; then mass properties and the CG state
    public void PostStep(float tFromPull, float simT)
    {
        var log = new List<string>();
        var toGo = new List<PartD>();
        while (events.Count > 0 && events[0].t <= tFromPull + 1e-6f)
        {
            var ev = events[0]; events.RemoveAt(0);
            foreach (var (k, hp) in ev.what)
            {
                if (ev.detach)
                {
                    if (!byKey.TryGetValue(k, out var p)) throw new ArgumentException($"detachAt: no part '{k}'");
                    if (p.gone) continue;
                    foreach (var j in p.joints) j.broken = true;
                    foreach (var j in parts.SelectMany(q => q.joints).Where(j => KeyOf(j.to) == p.key)) j.broken = true;
                    log.Add($"detachAt {k}");
                }
                else { SetHp(k, hp, tFromPull); log.Add($"{k} hp {hp:F0}"); }
            }
        }
        foreach (var kv in pendingBreak)
        {
            var p = kv.Key; var L = kv.Value;
            if (p.gone) continue;
            foreach (var j in p.joints) j.broken = true;
            log.Add($"joints of {p.key} broke: {L.J / 1000f:F0} kN of {L.fc / 1000f:F0}, {L.M / 1000f:F0} kNm of {L.tc / 1000f:F0} (hp {p.hp:F0})");
        }
        pendingBreak.Clear();
        toGo.AddRange(Unreachable());
        if (toGo.Count > 0) { RemoveParts(toGo, log); RebuildSubs(); }
        else if (log.Count > 0) RebuildSubs();
        foreach (var e in engs) UpdateEngine(e, log);
        foreach (var l in log) Console.Error.WriteLine($"# dmg t={tFromPull:F2} {l}");
        if (csv != null)
        {
            var eL = engs.FirstOrDefault(e => e.key == "EngL" || e.key == "Eng"); var eR = engs.FirstOrDefault(e => e.key == "EngR");
            csv.WriteLine(string.Join(",", new[] { tFromPull.ToString("F3", IC), A.mass.ToString("F0", IC), parts.Count(p => !p.gone).ToString(IC),
                (eL?.tf.condition ?? 1f).ToString("F3", IC), (eR?.tf.condition ?? 1f).ToString("F3", IC), (eL?.tf.operable ?? true) ? "1" : "0", (eR?.tf.operable ?? true) ? "1" : "0",
                WorstPart, WorstRatio.ToString("F3", IC), (WorstF / 1000f).ToString("F1", IC), (WorstT / 1000f).ToString("F1", IC), "\"" + string.Join("; ", log) + "\"" }));
        }
        lastT = tFromPull;
    }

    void RemoveParts(List<PartD> go, List<string> log)
    {
        foreach (var p in go) p.gone = true;
        var bodies = go.Where(p => p.body != null).Select(p => p.body).ToList();
        if (bodies.Count == 0) return;
        var cg0 = A.cgB;
        A.DetachBodies(bodies);
        A.MassProps();
        if (P != null)
        {
            // the remaining body's CG moves within it; the parts leave with their own momentum, so no impulse: w unchanged
            Vector3 d = P.q * (A.cgB - cg0);
            P.pCg += d; P.vCg += Vector3.Cross(P.w, d);
            foreach (var p in go) if (p.body != null) P.PartDump.Remove(p.body.part);
        }
        log?.Add($"detached {string.Join(",", go.Select(p => p.key))} ({bodies.Sum(b => b.part.mass):F0} kg), mass {A.mass:F0}");
    }

    public void Close() { csv?.Flush(); csv?.Dispose(); csv = null; }

    static double[,] Xform(double[,] R, double[,] I)
    {
        var o = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { double s = 0; for (int a = 0; a < 3; a++) for (int c = 0; c < 3; c++) s += R[i, a] * I[a, c] * R[j, c]; o[i, j] = s; }
        return o;
    }
    static Vector3 Mul(double[,] I, Vector3 v) => new Vector3((float)(I[0, 0] * v.x + I[0, 1] * v.y + I[0, 2] * v.z), (float)(I[1, 0] * v.x + I[1, 1] * v.y + I[1, 2] * v.z), (float)(I[2, 0] * v.x + I[2, 1] * v.y + I[2, 2] * v.z));
}
