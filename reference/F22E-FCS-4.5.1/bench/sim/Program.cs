using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

public sealed class Scenario
{
    public float iasKmh = 500f, alt = 3000f;
    public float trimT = 3f;
    public float pull = 1f, hold = 0.6f;       // stick back fraction, seconds held
    public float pullRamp = 0f;                // s to reach full pull (0 = step, keyboard-like)
    public float releaseRamp = 0f;
    public float rollDelay = 0f, roll = 1f, rollT = 1.2f;   // lateral stick after release
    public float post = 3f;
    public float rollFromPull = -1f;
    public float yaw = 0f;                    // pedal during roll window
    public float rollRev = 0f;                // >0: reverse roll stick after this long          // >=0: roll starts this long after the pull starts (overrides rollDelay)
    public Gyro gyro = Gyro.Unity;
    public float throttle = -1f;               // -1: hold trim throttle
    public string csv;
    public string detach;
    // Damage model (sim/Damage.cs, data/f22e_damage.json read from Aryx's bundle). Part names are the suffix after the root
    // name (Vstab_L, Flap_R, ...); EngL / EngR are the turbofans' own hit points. Times are seconds from the pull start, like
    // engOutT and prof. The model runs only when one of dmg / dmgAt / detachAt / detach is set or ripOff=1.
    public string dmg;        // hit points at t = 0: "Vstab_L:20+Flap_R:0+EngL:50"
    public string dmgAt;      // timed hit points: "40:Vstab_L:0+Vstab_R:0,42.5:Flap_R:30"
    public string detachAt;   // timed forced detach (the damage tool's last stage): "40:Vstab_L+Vstab_R"
    public float ripOff = -1f;   // load-driven joint breaking: 1 on, 0 off, -1 = on whenever the damage model runs
    public string dmgCsv;     // per-tick damage state (engine condition, worst joint load ratio, parts attached)
    public float storeKg = 0f; public string storePart = "WingRear_L";
    public float fuelBal = -1f, burn = 0f;   // 2.0.6: fuelBal >= 0 sets up the tanks, burns 'burn' of the fuel and balances for fuelBal s
    public float gamma0 = 0f;
    public float dumpAt = -99f;
    public float push = 0f, pushT = 1f;
    public float engOutT = -99f;               // 2.0.12: Engine_L dies this long after the pull starts (-99 = never)
    public float thr2 = -1f, thr2T = 0f;   // throttle step to thr2 at thr2T s after the pull start
    public string yprof;  // piecewise-linear pedal "t:v,..." (t from the roll start); overrides yaw
    public string bprof;  // 2.2.16: piecewise-linear brake input "t:v,..." (t from the pull start)
    public string rprof;  // 2.0.9: piecewise-linear roll stick "t:v,..." (t from the pull start); overrides roll
    public string prof;   // piecewise-linear pitch stick "t:v,t:v,..." (t from the pull start); overrides pull/hold shape
    public float rideAmp = 0f, rideT = 1f, rideNoise = 0f, rideNoiseHz = 8f;                 // initial flight-path angle (deg), e.g. 90 = vertical climb
    public string Tag => $"ias{iasKmh:F0} pull{pull:F2} hold{hold:F2} rd{rollDelay:F2} roll{roll:F1}";
}

internal sealed class Result
{
    public Scenario sc;
    public float qMin, qMinT, alphaRel, alphaPeak, alphaMin, nzPeak, qAtRel, stabMin, tvcMin, coupMin, pMax, phiEnd, iasRel;
    public float rMax, betaMaxAbs, phi05 = float.NaN, phi10 = float.NaN, phi20 = float.NaN;
    public float qRms, tPhi90 = -1, qMaxRoll = -999, qMinRoll = 999, qBeforeRoll;
}

internal static class Program
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static int DumpN = 27;
    static string dataPath = Environment.GetEnvironmentVariable("BENCH_DATA") ?? "/root/w/f22x/bench_airframe.json";
    static readonly Dictionary<string, string> sets = new Dictionary<string, string>();
    // 2.30.0 bench: suite alpha / beta = the plant CG's (one definition whatever the FCS reads)
    static float CgAlpha(Plant P) { var v = Quaternion.Inverse(P.q) * P.vCg; return v.magnitude > 1f ? Mathf.Atan2(-v.y, v.z) * Mathf.Rad2Deg : 0f; }
    static float CgBeta(Plant P) { var v = Quaternion.Inverse(P.q) * P.vCg; return v.magnitude > 1f ? Mathf.Asin(Mathf.Clamp(v.x / v.magnitude, -1f, 1f)) * Mathf.Rad2Deg : 0f; }


    public static void Main(string[] args)
    {
        var list = new List<string>(args);
        string mode = list.Count > 0 ? list[0] : "one";
        // 4.0.0: FCS_NATIVE_STATS=<file> appends this run's native-allocator counts (solves, solves checked against the C# solve, differences); 4.1.0: and the grid kernels' calls
        string nstat = Environment.GetEnvironmentVariable("FCS_NATIVE_STATS");
        if (!string.IsNullOrEmpty(nstat)) AppDomain.CurrentDomain.ProcessExit += (s0, e0) => { try { System.IO.File.AppendAllText(nstat, $"{mode} solves={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Solves} checked={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Checked} identical={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Identical} mismatches={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Mismatches} fallbacks={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Fallbacks} mode={Aryx_F22E_StrikeRaptor.FCS.Plugin.NativeAllocator} check={Aryx_F22E_StrikeRaptor.FCS.Plugin.NativeAllocatorCheck} strict={Aryx_F22E_StrikeRaptor.FCS.FcsNative.StrictFloat} state={Aryx_F22E_StrikeRaptor.FCS.FcsNative.Why} grid={Aryx_F22E_StrikeRaptor.FCS.FcsNative.GridCalls} gridFallbacks={Aryx_F22E_StrikeRaptor.FCS.FcsNative.GridFallbacks} kernels={Aryx_F22E_StrikeRaptor.FCS.FcsNative.KernelWhy} sweep={Aryx_F22E_StrikeRaptor.FCS.Plugin.NativeSweep} tables={Aryx_F22E_StrikeRaptor.FCS.Plugin.NativeTables} cpl={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplCalls} cplFallbacks={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplFallbacks} cplChecked={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplChecked} cplIdentical={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplIdentical} cplMismatches={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplMismatches} cplState={Aryx_F22E_StrikeRaptor.FCS.FcsNative.CplWhy}\n"); } catch (Exception) { } };
        var kv = list.Skip(1).Where(a => a.Contains('=')).Select(a => a.Split('=', 2)).GroupBy(a => a[0]).ToDictionary(g => g.Key, g => g.Last()[1]);
        foreach (var k in kv) if (!ScenarioKey(k.Key) && k.Key != "verbose" && k.Key != "timing" && k.Key != "ias" && !new[]{"stock","altA","hdg","elev","thr","T","csv"}.Contains(k.Key) && k.Key != "stab" && k.Key != "tvc") sets[k.Key] = k.Value;
        if (mode == "info") { Info(); return; }
        if (mode == "toe") { sets.Remove("ias"); ApplySets(); ToeSweep.Run(dataPath, float.Parse(kv.TryGetValue("ias", out var ti) ? ti : "300", IC)); return; }
        if (mode == "fuel") { foreach (var k0 in new[]{"T","storeKg","storePart","detach","leak","burn"}) sets.Remove(k0); ApplySets(); FuelTest.Run(dataPath, kv); return; }
        if (mode == "gyaw") { foreach (var k0 in new[]{"kt","ped","Tturn","Tafter","light","mass","Iz","muNose","muMain","csv","nfrac","kick","rel","tw","lock","slew","align","dbfL","dbfR"}) sets.Remove(k0); ApplySets(); GroundYaw.Run(dataPath, kv); return; }
        if (mode == "takeoff") { foreach (var k0 in new[]{"v0","contactY","T","theta0","h0","vz0"}) sets.Remove(k0); ApplySets(); Ground.Takeoff(dataPath, kv); return; }
        if (mode == "stall") { ApplySets(); StallDiag.Run(dataPath); return; }
        if (mode == "replay") { foreach (var k0 in new[]{"log","out","t0","t1","settle","warm","pinPitch","pinUntil"}) sets.Remove(k0); ApplySets(); Replay.Run(dataPath, kv); return; }
        if (mode == "parts") { foreach (var k0 in new[]{"alpha","beta","tas","rho","p","q","r","hl"}.Concat(EqErr.Poser.SurfNames)) sets.Remove(k0); ApplySets(); EqErr.Parts(dataPath, kv); return; }
        if (mode == "partcmp") { foreach (var k0 in new[]{"log","t"}) sets.Remove(k0); ApplySets(); PartCmp.Run(dataPath, kv); return; }
        if (mode == "sliptruth") { foreach (var k0 in new[]{"log","out","every","db","amin","amax"}) sets.Remove(k0); ApplySets(); SlipTruth.Run(dataPath, kv); return; }
        if (mode == "slipprobe") { foreach (var k0 in new[]{"log","out","every","db"}) sets.Remove(k0); ApplySets(); SlipProbe.Run(dataPath, kv); return; }
        if (mode == "fcsmom") { foreach (var k0 in new[]{"log","out","modes","every"}) sets.Remove(k0); ApplySets(); FcsMom.Run(dataPath, kv); return; }
        if (mode == "eqerr") { foreach (var k0 in new[]{"log","out","modes","every","massScale","dumpAt"}) sets.Remove(k0); ApplySets(); EqErr.Run(dataPath, kv); return; }
        if (mode == "inertia") { ApplySets(); var A0 = new Airframe(dataPath); Console.WriteLine($"mass {A0.mass:F0} cg {A0.cgB}"); for (int i=0;i<3;i++) Console.WriteLine($"{A0.Itot[i,0],12:F0} {A0.Itot[i,1],12:F0} {A0.Itot[i,2],12:F0}"); return; }
        if (mode == "liftparts") { ApplySets(); LiftGroups.PerPart(dataPath, 42.1875f); return; }
        if (mode == "liftgroups") { ApplySets(); LiftGroups.Run(dataPath); return; }
        if (mode == "lift") { ApplySets(); LiftCurve.Run(dataPath, float.Parse(kv.TryGetValue("ias", out var li) ? li : "300", IC)); return; }
        if (mode == "ai")
        {
            ApplySets();
            var IC2 = CultureInfo.InvariantCulture;
            bool stk = kv.TryGetValue("stock", out var sv) && sv == "1";
            float ias = float.Parse(kv.TryGetValue("ias", out var a1) ? a1 : "550", IC2), alt = float.Parse(kv.TryGetValue("altA", out var a2) ? a2 : "3000", IC2);
            float hd = float.Parse(kv.TryGetValue("hdg", out var a3) ? a3 : "90", IC2), el = float.Parse(kv.TryGetValue("elev", out var a4) ? a4 : "0", IC2);
            float thr = float.Parse(kv.TryGetValue("thr", out var a5) ? a5 : "1", IC2), T = float.Parse(kv.TryGetValue("T", out var a6) ? a6 : "25", IC2);
            AiTest.Fly(dataPath, stk, ias, alt, hd, el, thr, T, kv.TryGetValue("csv", out var cf) ? cf : null);
            return;
        }
        if (mode == "emq")
        {
            foreach (var k0 in new[]{"alt","thr","hold","iasl","stages","out","fuel","mass","stores"}) sets.Remove(k0);
            ApplySets();
            Airframe.FuelFrac = kv.TryGetValue("fuel", out var ff2) ? float.Parse(ff2, IC) : 1f;
            Airframe.MassTarget = kv.TryGetValue("mass", out var mt2) ? float.Parse(mt2, IC) : 0f;
            Airframe.Stores = kv.TryGetValue("stores", out var stv2) ? stv2 : null;
            EmQuasi.Run(dataPath, kv, (P, alt, tas) => { float a = TrimAlpha(P, alt, tas, out float th); return (a, th); });
            return;
        }
        if (mode == "em")
        {
            foreach (var k0 in new[]{"alt","thr","tmax","tas","sticks","out","fuel","mass","stores","aoas","gs"}) sets.Remove(k0);
            ApplySets();
            Airframe.FuelFrac = kv.TryGetValue("fuel", out var ff) ? float.Parse(ff, IC) : 1f;
            Airframe.MassTarget = kv.TryGetValue("mass", out var mt) ? float.Parse(mt, IC) : 0f;
            Airframe.Stores = kv.TryGetValue("stores", out var stv) ? stv : null;
            EmTest.Run(dataPath, kv, (P, alt, tas) => { float a = TrimAlpha(P, alt, tas, out float th); return (a, th); });
            return;
        }
        if (mode == "cmsweep") { foreach (var k0 in new[]{"ias","stab","tvc","a0","a1","da","thrust","dump","rud","flap","ail","beta"}) sets.Remove(k0); ApplySets(); CmSweep.Run(dataPath, kv); return; }
        if (mode == "cm") { ApplySets(); Diag.Cm(float.Parse(kv["ias"], IC), float.Parse(kv["stab"], IC), float.Parse(kv["tvc"], IC)); return; }
        if (mode == "one")
        {
            var sc = MakeScenario(kv);
            var r = Run(sc);
            Console.WriteLine(Header());
            Console.WriteLine(Row(r));
            if (kv.ContainsKey("timing")) { Console.WriteLine($"FCS Tick: {Plant.TickSw.Elapsed.TotalMilliseconds / Math.Max(1, Plant.TickN) * 1000:F0} us/tick over {Plant.TickN} ticks"); var ts = Plant.TickUs.OrderBy(x => x).ToList(); if (ts.Count > 0) Console.WriteLine($"TICKUS n={ts.Count} mean={ts.Average():F1} median={ts[ts.Count / 2]:F1} p95={ts[(int)(0.95 * ts.Count)]:F1} max={ts[ts.Count - 1]:F1} | tables={Plant.PhSum[0] / ts.Count:F1} alloc={Plant.PhSum[1] / ts.Count:F1} kick={Plant.PhSum[2] / ts.Count:F1} row={Plant.PhSum[3] / ts.Count:F1} | laws={Plant.PhSum[4] / ts.Count:F1} asym={Plant.PhSum[5] / ts.Count:F1} (mom={Plant.PhSum[6] / ts.Count:F1} slip={Plant.PhSum[7] / ts.Count:F1} sweep={Plant.PhSum[8] / ts.Count:F1}) env={Plant.PhSum[9] / ts.Count:F1} mass={Plant.PhSum[10] / ts.Count:F1} flaps={Plant.PhSum[11] / ts.Count:F1} gnd={Plant.PhSum[12] / ts.Count:F1} vmc={Plant.PhVmc / ts.Count:F1} | reachKicks={Aryx_F22E_StrikeRaptor.FCS.Allocator.ReachKicksTotal} used={Aryx_F22E_StrikeRaptor.FCS.Allocator.ReachUsedTotal} picked={Aryx_F22E_StrikeRaptor.FCS.Allocator.ReachPickedTotal}"); }
            return;
        }

        if (mode == "suite")
        {
            var baseSc = MakeScenario(kv);
            bool verbose = kv.ContainsKey("verbose");
            var shove = new List<Result>();
            foreach (var thr in new[] { 1f, -1f })
            foreach (var i in new[] { 450f, 500f, 550f, 600f }) foreach (var p in new[] { 0.5f, 1f }) foreach (var h in new[] { 0.2f, 0.35f }) foreach (var d in new[] { 0f, 0.15f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = p; sc.hold = h; sc.rollDelay = d; sc.roll = 1; sc.trimT = 1; sc.throttle = thr; sc.csv = null;
                var r = Run(sc); shove.Add(r); if (verbose) Console.WriteLine("shove " + Row(r));
            }
            var sh2 = shove.Where(r => r.alphaPeak < 60f).ToList();
            Console.WriteLine($"S1b excluding pitch-up departures (alphaPeak>=60) n={sh2.Count}: qMin worst {sh2.Min(r => r.qMin):F1} mean {sh2.Average(r => r.qMin):F1} | alphaMin worst {sh2.Min(r => r.alphaMin):F1} mean {sh2.Average(r => r.alphaMin):F1}");
            Console.WriteLine($"S1 pull-release-roll n={shove.Count}: qMin worst {shove.Min(r => r.qMin):F1} mean {shove.Average(r => r.qMin):F1} | alphaMin worst {shove.Min(r => r.alphaMin):F1} | t90 mean {shove.Where(r => r.tPhi90 > 0).Average(r => r.tPhi90):F2} (n90 {shove.Count(r => r.tPhi90 > 0)}) | qRms {shove.Average(r => r.qRms):F2}");
            var nr = new List<Result>();
            foreach (var i in new[] { 450f, 500f, 550f, 600f }) foreach (var p in new[] { 0.5f, 1f }) foreach (var h in new[] { 0.2f, 0.35f, 0.6f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = p; sc.hold = h; sc.roll = 0; sc.trimT = 1; sc.throttle = 1; sc.csv = null;
                var r = Run(sc); nr.Add(r); if (verbose) Console.WriteLine("norol " + Row(r));
            }
            Console.WriteLine($"S2 pull-release no roll n={nr.Count}: qMin worst {nr.Min(r => r.qMin):F1} mean {nr.Average(r => r.qMin):F1} | alphaPeak mean {nr.Average(r => r.alphaPeak):F1} max {nr.Max(r => r.alphaPeak):F1} | qRms {nr.Average(r => r.qRms):F2}");
            foreach (var i in new[] { 250f, 350f, 450f, 550f, 650f, 800f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = 0; sc.hold = 0; sc.roll = 1; sc.rollT = 2f; sc.trimT = 1; sc.throttle = -1; sc.csv = null;
                var r = Run(sc);
                Console.WriteLine($"S3 1g roll {i:F0} km/h: bank@0.5/1.0/2.0s {r.phi05:F0}/{r.phi10:F0}/{r.phi20:F0} t90 {r.tPhi90:F2}s pMax {r.pMax:F0} q during roll {r.qMinRoll:F1}..{r.qMaxRoll:F1} |beta|max {r.betaMaxAbs:F1}");
            }
            foreach (var i in new[] { 250f, 350f, 450f, 550f })
            foreach (var p in new[] { 0.4f, 1f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = p; sc.hold = 3f; sc.rollFromPull = 1.2f; sc.roll = 1; sc.rollT = 1.5f; sc.trimT = 1; sc.throttle = 1; sc.post = 0.5f; sc.csv = null;
                var r = Run(sc);
                Console.WriteLine($"S4 held pull {p:F1} + roll {i:F0} km/h: alpha at roll {r.alphaPeak:F1}, bank@0.5/1.0 {r.phi05:F0}/{r.phi10:F0} t90 {r.tPhi90:F2}s pMax {r.pMax:F0} q before {r.qBeforeRoll:F1} during {r.qMinRoll:F1}..{r.qMaxRoll:F1} |beta|max {r.betaMaxAbs:F1}");
            }
            foreach (var i in new[] { 200f, 300f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = 1f; sc.hold = 6f; sc.rollFromPull = 2.5f; sc.roll = 1; sc.yaw = 1; sc.rollT = 3f; sc.trimT = 1; sc.throttle = 1; sc.post = 0.2f; sc.csv = null;
                var r = Run(sc);
                Console.WriteLine($"S5 post-stall roll+yaw {i:F0} km/h: alpha {r.alphaPeak:F0} rMax {r.rMax:F1} pMax {r.pMax:F0} |beta|max {r.betaMaxAbs:F1}");
            }
            foreach (var i in new[] { 250f, 350f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = 1f; sc.hold = 1.5f; sc.rollDelay = 0f; sc.roll = 1; sc.rollT = 1.5f; sc.trimT = 1; sc.throttle = 1; sc.csv = null;
                var r = Run(sc);
                Console.WriteLine($"S6 high-alpha release+roll {i:F0} km/h: alphaRel {r.alphaRel:F0} peak {r.alphaPeak:F0} min {r.alphaMin:F0} qMin {r.qMin:F1} pMax {r.pMax:F0} rMax {r.rMax:F0}");
            }
            foreach (var i in new[] { 450f, 650f })
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = 0; sc.hold = 0; sc.roll = 1; sc.rollT = 1.6f; sc.rollRev = 0.8f; sc.trimT = 1; sc.throttle = -1; sc.csv = null;
                var r = Run(sc);
                Console.WriteLine($"S7 1g roll reversal {i:F0} km/h: pMax {r.pMax:F0} q during {r.qMinRoll:F1}..{r.qMaxRoll:F1} |beta|max {r.betaMaxAbs:F1}");
            }
            return;
        }
        if (mode == "matrix")
        {
            var baseSc = MakeScenario(kv);
            float[] ias = Parse(kv, "iasList", "450,500,550,600");
            float[] holds = Parse(kv, "holdList", "0.3,0.6,1.0");
            float[] delays = Parse(kv, "delayList", "0,0.15");
            float[] pulls = Parse(kv, "pullList", "1");
            float[] rolls = Parse(kv, "rollList", "1,0");
            Console.WriteLine(Header());
            var res = new List<Result>();
            foreach (var i in ias) foreach (var p in pulls) foreach (var h in holds) foreach (var d in delays) foreach (var rl in rolls)
            {
                var sc = Clone(baseSc); sc.iasKmh = i; sc.pull = p; sc.hold = h; sc.rollDelay = d; sc.roll = rl; sc.csv = null;
                var r = Run(sc); res.Add(r);
                Console.WriteLine(Row(r));
            }
            var withRoll = res.Where(r => r.sc.roll != 0).ToList();
            if (withRoll.Count > 0)
                Console.WriteLine($"# SUMMARY roll cases n={withRoll.Count}: worst qMin {withRoll.Min(r => r.qMin):F1}  mean qMin {withRoll.Average(r => r.qMin):F1}  mean qRms {withRoll.Average(r => r.qRms):F2}  worst alphaMin {withRoll.Min(r => r.alphaMin):F1}");
            var noRoll = res.Where(r => r.sc.roll == 0).ToList();
            if (noRoll.Count > 0)
                Console.WriteLine($"# SUMMARY no-roll n={noRoll.Count}: worst qMin {noRoll.Min(r => r.qMin):F1}  mean qMin {noRoll.Average(r => r.qMin):F1}  mean qRms {noRoll.Average(r => r.qRms):F2}");
        }
    }

    static float[] Parse(Dictionary<string, string> kv, string k, string d) => (kv.TryGetValue(k, out var s) ? s : d).Split(',').Select(x => float.Parse(x, IC)).ToArray();
    static bool ScenarioKey(string k) => typeof(Scenario).GetField(k) != null || k.EndsWith("List");
    static Scenario Clone(Scenario s) => (Scenario)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
    static Scenario MakeScenario(Dictionary<string, string> kv)
    {
        var sc = new Scenario();
        foreach (var k in kv)
        {
            var f = typeof(Scenario).GetField(k.Key);
            if (f == null) continue;
            if (f.FieldType == typeof(float)) f.SetValue(sc, float.Parse(k.Value, IC));
            else if (f.FieldType == typeof(string)) f.SetValue(sc, k.Value);
            else if (f.FieldType == typeof(Gyro)) f.SetValue(sc, Enum.Parse<Gyro>(k.Value, true));
        }
        return sc;
    }

    static void ApplySets()
    {
        foreach (var k in sets)
        {
            var f = typeof(Plugin).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(FcsController).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(Allocator).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(SurfaceEffector).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(Airframe).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(Plant).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(EqErr).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(FcsMom).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? typeof(CapacityEstimator).GetField(k.Key, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) throw new Exception("unknown key " + k.Key);
            if (f.FieldType == typeof(float)) f.SetValue(null, float.Parse(k.Value, IC));
            else if (f.FieldType == typeof(bool)) f.SetValue(null, bool.Parse(k.Value));
            else if (f.FieldType == typeof(int)) f.SetValue(null, int.Parse(k.Value));
            else if (f.FieldType == typeof(string)) f.SetValue(null, k.Value);
        }
    }

    static void Info()
    {
        Airframe.Detach = sets.TryGetValue("detachInfo", out var dd) ? dd : null; sets.Remove("detachInfo");
        ApplySets();
        var A = new Airframe(dataPath);
        Console.WriteLine(A.DetachLog);
        Console.WriteLine($"mass {A.mass:F0} kg, cg (root frame) {A.cgB}, {A.CgShiftLog}");
        Console.WriteLine($"I total (Unity axes x,y,z = pitch, yaw, roll): diag {A.Itot[0, 0]:F0} {A.Itot[1, 1]:F0} {A.Itot[2, 2]:F0}; off xy {A.Itot[0, 1]:F0} xz {A.Itot[0, 2]:F0} yz {A.Itot[1, 2]:F0}");
        Console.WriteLine($"I parallel-axis part: {A.Ipa[0, 0]:F0} {A.Ipa[1, 1]:F0} {A.Ipa[2, 2]:F0}; own-body part: {A.Iown[0, 0]:F0} {A.Iown[1, 1]:F0} {A.Iown[2, 2]:F0}");
        foreach (var b in A.bodies) Console.WriteLine($"  {b.part.name,-34} m {b.part.mass,7:F0}  com {b.comB}  S {b.part.wingArea,5:F2} Cd {b.part.dragArea,5:F3} af {b.part.airfoil}");
        foreach (var cs in A.surfaces) Console.WriteLine($"  CS {cs.name,-28} pitch {cs.pitchRange} roll {cs.rollRange} yaw {cs.yawRange} servo {cs.servoSpeed}");
        foreach (var e in A.engines) Console.WriteLine($"  ENG {e.name} static {e.staticThrust:F0} vec {e.thrustVectoring} AB {e.nozzles.Sum(n => n.abThrust):F0}");
    }

    internal static Result Run(Scenario sc)
    {
        ApplySets();
        Airframe.Detach = sc.detach; Airframe.StoreKg = sc.storeKg; Airframe.StorePart = sc.storePart;
        var A = new Airframe(dataPath);
        var DM = DamageModel.Wanted(sc) ? new DamageModel(A, sc, DamageModel.DataPathFor(dataPath)) : null;
        var P = new Plant(A) { gyro = sc.gyro };
        DM?.Attach(P);
        float rho = Plant.Density(sc.alt);
        float tas = sc.iasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f);
        float aTrim = TrimAlpha(P, sc.alt, tas, out float thrTrim);
        P.Init(sc.alt, tas, aTrim, aTrim + sc.gamma0);
        P.throttle = sc.throttle >= 0 ? sc.throttle : thrTrim;
        foreach (var e0 in A.engines) { if (Plant.EngDmg >= 0f && e0.name.Contains("_L")) e0.condition = Plant.EngDmg; float tr0 = Mathf.Clamp01((P.throttle - e0.remapX) / Mathf.Max(1e-3f, e0.remapY - e0.remapX)); e0.rpm = Mathf.Lerp(e0.minRPM, e0.minRPM + (e0.maxRPM - e0.minRPM) * (0.5f * e0.condition + 0.5f), tr0); foreach (var n0 in e0.nozzles) n0.abAmt = (e0.rpm / e0.maxRPM > 0.85f) ? Mathf.InverseLerp(n0.abStart, 1f, P.throttle) : 0f; }
        if (sc.fuelBal >= 0f)
        {
            var tks = FuelTest.Setup(A, dataPath);
            foreach (var t in tks) t.UseFuel(t.fuelMass * sc.burn);
            var c0 = FuelTest.Com(A);
            FuelTest.Converge(A, sc.fuelBal, false);
            var c1 = FuelTest.Com(A);
            A.MassProps(); P.Init(sc.alt, tas, aTrim, aTrim + sc.gamma0);
            Console.Error.WriteLine($"# fuel: CG lateral {c0.x * 100:F1} -> {c1.x * 100:F1} cm, fore/aft {c0.z * 100:F1} -> {c1.z * 100:F1} cm, mass {A.mass:F0}");
        }
        P.CreateFcs();
        foreach (var se0 in P.fcs.surfaces) if (se0.kind == SurfaceKind.Stabilator) se0.DbgRaw = new float[se0.N];
        var inp = A.ac.inputs;
        var r = new Result { sc = sc, qMin = 999, alphaMin = 999, stabMin = 999, tvcMin = 999, coupMin = 999, alphaPeak = -999 };
        float tPull = sc.trimT, tRel = tPull + sc.hold, tRoll = sc.rollFromPull >= 0 ? tPull + sc.rollFromPull : tRel + sc.rollDelay, tEnd = Math.Max(tRel, tRoll) + sc.post;
        StreamWriter w = sc.csv != null ? new StreamWriter(sc.csv) : null;
        w?.WriteLine("t,ias,alpha,beta,nz,p,q,r,phi,theta,sp,sr,qCmd,qT,pCmd,rCmd,unload,prot,betaG,stabL,stabR,stabLcmd,stabRcmd,tvc,tvcCmd,flapL,flapR,ailL,ailR,rudL,rudR,tgtY,mfY,coupY,qTrajFF,rollPitchFF,stabPitchFrac,tvcPinned,aUp,aDn,alphaCmd,nCmd,thr,tvcTrimRef,tvcSlow,tvcPin,tvcLam,axW_r,axW_p,axW_y,cmdY_tvc,cmdY_sL,cmdY_sR,cmdY_wing,cmdY_tot,cmdX_tot,tgtX,cmdX_sL,cmdX_sR,cmdX_wing,sR_raw,sR_mono,sL_raw,sL_mono,sR_laoa,sL_laoa,trueAccY,predAccY,aRoll,pMax,dMeasR,dampR,guard,yawSat,aYaw,rMax,asymCap,aDnTgt,aUpTgt,tgtZ,cmdZ_tot,cmdZ_sL,cmdZ_sR,cmdZ_wing,cmdX_ailL,cmdX_ailR,cmdX_flapR,cmdX_flapL,latAcc,pSTgt,pSmax,pS,omegaV,qMax,qRawD,aIntD,betaGD,alphaDotD,alphaU,stabBranch,pitchNeed,pitchNeedDn,sweepWin,coupRollLim,rollGov,rpGate,rpBand,yawIn,asymDy,asymUNow,asymCapPos,asymArNow,asymCrNow,asymAyNow,asymCyNow,pStockRef,aRollPos,aRollNeg,dMeasY,iasDotF,qPredK,asymCapFilt,latPredK,alphaCmdRaw,nCmdRaw,rateBlendAoA,hiRate,rateW,zoneX,authCapEff,nLiftMax,hybridDM,qB,floatOn,floatS,rCmdL,betaPed,yawMargin,slatRelief,hlPos,rollGovMargin,tw,decel,slipGovPull,yawHealth,abShare,abOpen,brake,fightK,ax,pullTrade,ffBoost,rateCmdG,xs,tLead,slipRateK,servo,tvcNW,tvcAK,tvcAnc,tvcBand,sLlo,sLhi,sRlo,sRhi,sLgrad,sRgrad,tvcGrad,rudLlo,rudLhi,rudLlaoa,rudRlaoa,rudSym,rudTgt,settledWin,tvcLo,tvcHi,tvcMu,sLmu,sLlam,regScale,sLsym,sLanti,sLpitchFrac,sRpitchFrac,rateCapLag,thrL,thrR,mF,sUse,vmcLim,engSt,gStab,gFin,gFlap,gWing,gBody,gThr,gStabL,gStabR,distLead,dDot,stopSlipLim,stopSlipAB,stopSlipU,stopSlipPred,stopSlipMargin,yawSatPitch,yawLead,paceLead,paceHold,slipTol,slipDep,slipAuth,slipStiff,paceRel,tradeAlpha,yawUseAll,paceGate,rateBlendEff,coupHead,coupLim,rcAOpp,rcTc,rcSinH,alphaDotB,paceGateZ,paceGateX,unmetX,unmetZ,paceDzH,paceDzAll,paceDxH,paceDxAll,paceRH,pacePH,aYawSeen,pSRaw,paceNEff,paceDd,paceMLeft,capZP,capZN,capXP,capXN,betaG2,paceLo,paceHi,paceClamp,rS,ceWdZ,ceWdX,ceCmZr,ceCmZs,ceCmZf,ceCmXr,ceCmXs,ceCmXf,cePrZPr,cePrZPs,cePrZPf,cePrZNr,cePrZNs,cePrZNf,cePrXPr,cePrXPs,cePrXPf,cePrXNr,cePrXNs,cePrXNf,cePrZPx,cePrZNx,cePrXPz,cePrXNz,ceGZ,ceGX,ceDr,ceDs,ceDf,ceCapZP,ceCapZN,ceCapXP,ceCapXN,ceCapZPraw,ceCapZNraw,ceCapXPraw,ceCapXNraw,ceGUpdZ,ceGUpdX,ceCapSP,ceCapSN,ceCapZeP,ceCapZeN,ceRudW,cePrSPs,cePrSNs,cePrSPf,cePrSNf,ceCapTrP,ceCapTrN,ceCapNsP,ceCapNsN,ceWdNs,capSwp,capShd,capSm,aYawShed,relFollow,capDn,qMin,capBU,capBD,yawFar");
        double qsq = 0; int qn = 0; bool dumped = false;
        while (P.time < tEnd)
        {
            float t = P.time;
            float pitchStick = 0f;
            if (t >= tPull && t < tRel) pitchStick = sc.pullRamp > 0 ? Mathf.Clamp01((t - tPull) / sc.pullRamp) * sc.pull : sc.pull;
            if (t >= tPull + 1.5f && t < tRel && sc.rideAmp > 0f)
            {
                int k = (int)Math.Floor((t - tPull - 1.5f) / sc.rideT);
                float[] pat = { 1f, 0f, -1f, 0f, 2f, 1f, -1f, -2f, 0f, 1f };
                pitchStick += sc.rideAmp * pat[k % pat.Length];
            }
            if (t >= tPull && t < tRel && sc.rideNoise > 0f)
            {
                int kn = (int)Math.Floor(t * sc.rideNoiseHz);
                uint h = (uint)(kn * 2654435761u); h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                pitchStick += sc.rideNoise * ((h & 0xFFFF) / 32767.5f - 1f);
            }
            else if (t >= tRel && sc.releaseRamp > 0 && t < tRel + sc.releaseRamp) pitchStick = sc.pull * (1f - (t - tRel) / sc.releaseRamp);
            if (sc.push != 0f && t >= tRel && t < tRel + sc.pushT) pitchStick = -sc.push;
            if (sc.prof != null && t >= tPull) { var pts = sc.prof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray(); float tt = t - tPull; pitchStick = pts[pts.Length-1].Item2; for (int k = 0; k < pts.Length - 1; k++) if (tt >= pts[k].Item1 && tt < pts[k+1].Item1) { pitchStick = pts[k].Item2 + (pts[k+1].Item2 - pts[k].Item2) * (tt - pts[k].Item1) / Math.Max(1e-3f, pts[k+1].Item1 - pts[k].Item1); break; } if (tt < pts[0].Item1) pitchStick = 0f; }
            inp.pitch = -pitchStick;   // NO: negative = pull
            inp.roll = (t >= tRoll && t < tRoll + sc.rollT) ? (sc.rollRev > 0 && t >= tRoll + sc.rollRev ? -sc.roll : sc.roll) : 0f;
            if (sc.rprof != null && t >= tPull) { var rp = sc.rprof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray(); float tr = t - tPull; inp.roll = rp[rp.Length-1].Item2; for (int k = 0; k < rp.Length - 1; k++) if (tr >= rp[k].Item1 && tr < rp[k+1].Item1) { inp.roll = rp[k].Item2 + (rp[k+1].Item2 - rp[k].Item2) * (tr - rp[k].Item1) / Math.Max(1e-3f, rp[k+1].Item1 - rp[k].Item1); break; } }
            inp.yaw = (t >= tRoll && t < tRoll + sc.rollT) ? sc.yaw : 0f;
            if (sc.bprof != null) { var bp = sc.bprof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray(); float tb = t - tPull; inp.brake = tb < bp[0].Item1 ? bp[0].Item2 : bp[bp.Length-1].Item2; for (int k = 0; k < bp.Length - 1; k++) if (tb >= bp[k].Item1 && tb < bp[k+1].Item1) { inp.brake = bp[k].Item2 + (bp[k+1].Item2 - bp[k].Item2) * (tb - bp[k].Item1) / Math.Max(1e-3f, bp[k+1].Item1 - bp[k].Item1); break; } }
            if (sc.yprof != null && t >= tRoll) { var yp = sc.yprof.Split(',').Select(x => x.Split(':')).Select(a => (float.Parse(a[0], IC), float.Parse(a[1], IC))).ToArray(); float ty = t - tRoll; inp.yaw = yp[yp.Length-1].Item2; for (int k = 0; k < yp.Length - 1; k++) if (ty >= yp[k].Item1 && ty < yp[k+1].Item1) { inp.yaw = yp[k].Item2 + (yp[k+1].Item2 - yp[k].Item2) * (ty - yp[k].Item1) / Math.Max(1e-3f, yp[k+1].Item1 - yp[k].Item1); break; } }
            if (sc.thr2 >= 0f && t >= tPull + sc.thr2T) P.throttle = sc.thr2;
            if (t < tPull - 0.5f && sc.throttle < 0) P.throttle = Mathf.Clamp01(P.throttle + 0.5f * (tas - P.vCg.magnitude) * P.dt);
            if (sc.fuelBal >= 0f) FuelBalance.Step(P.A.ac, P.dt);
            if (sc.engOutT > -90f && t >= tPull + sc.engOutT) foreach (var e0 in P.A.engines) if (e0.name.Contains("_L")) e0.operable = false;
            P.SimT = t; P.Step();
            DM?.PostStep(t - tPull, t);
            var c = P.fcs;
            var rates = c.omega * Mathf.Rad2Deg; // aero p,q,r
            Vector3 wc = new Vector3(c.pCmd, c.qT, c.rCmd) * Mathf.Deg2Rad;
            Vector3 ff = Plugin.CouplingFF * (Vector3.Cross(wc, MulI(c, wc)) - Vector3.Cross(c.omega, MulI(c, c.omega)));
            float coupY = ff.y * c.IaInv[1, 1] * Mathf.Rad2Deg;
            float qTrajFF = 0.5f * c.qTraj.v;
            SurfaceEffector S(string n) => c.surfaces.First(s => s.name == n);
            var sL = S("Elevator_L"); var sR = S("Elevator_R");
            if (t >= tRel)
            {
                float qd = rates.y;
                if (qd < r.qMin) { r.qMin = qd; r.qMinT = t - tRel; }
                r.alphaPeak = Mathf.Max(r.alphaPeak, c.alpha);
                r.alphaMin = Mathf.Min(r.alphaMin, c.alpha);
                r.stabMin = Mathf.Min(r.stabMin, Mathf.Min(sL.now, sR.now));
                r.tvcMin = Mathf.Min(r.tvcMin, c.tvc.now);
                r.coupMin = Mathf.Min(r.coupMin, coupY);
                r.pMax = Mathf.Max(r.pMax, Mathf.Abs(rates.x));
                qsq += (double)(qd - c.qT) * (qd - c.qT); qn++;
            }
            if (t >= tRoll && sc.roll != 0)
            {
                if (r.tPhi90 < 0 && Math.Abs(c.phi) >= 90f) r.tPhi90 = t - tRoll;
                if (t < tRoll + sc.rollT) { r.qMaxRoll = Mathf.Max(r.qMaxRoll, rates.y); r.qMinRoll = Mathf.Min(r.qMinRoll, rates.y); r.rMax = Mathf.Max(r.rMax, Mathf.Abs(rates.z)); r.betaMaxAbs = Mathf.Max(r.betaMaxAbs, Mathf.Abs(c.beta)); }
            }
            if (Math.Abs(t - tRoll) < P.dt * 0.5f) r.qBeforeRoll = rates.y;
            if (Math.Abs(t - tRoll - 0.5f) < P.dt * 0.5f) r.phi05 = c.phi;
            if (Math.Abs(t - tRoll - 1.0f) < P.dt * 0.5f) r.phi10 = c.phi;
            if (Math.Abs(t - tRoll - 2.0f) < P.dt * 0.5f) r.phi20 = c.phi;
            if (Math.Abs(t - tRel) < P.dt * 0.5f) { r.alphaRel = c.alpha; r.qAtRel = rates.y; r.iasRel = c.ias * 3.6f; }
            if (t >= tPull) r.nzPeak = Mathf.Max(r.nzPeak, c.nz);
            if (w != null && t >= tPull - 0.5f)
            {
                string F(float x) => x.ToString("0.###", IC);
                w.WriteLine(string.Join(",", new object[] { t - tRel, c.ias * 3.6f, CgAlpha(P), CgBeta(P), c.nz, rates.x, rates.y, rates.z, c.phi, c.theta, c.sp, c.sr, c.qCmd, c.qT, c.pCmd, c.rCmd, c.unload ? 1 : 0, c.prot ? 1 : 0, c.betaG,
                    sL.now, sR.now, sL.cmdOut, sR.cmdOut, c.tvc.now, c.tvc.cmdOut, S("Flap_L").now, S("Flap_R").now, S("Aileron_L").now, S("Aileron_R").now, S("Rudder_L").now, S("Rudder_R").now,
                    c.dbgTgtY, c.dbgMfY, coupY, qTrajFF, c.rollPitchFF * c.IaInv[1, 1] * Mathf.Rad2Deg, c.stabPitchFrac, c.tvcPinned ? 1 : 0, c.aUp, c.aDn, c.alphaCmd, c.nCmd, P.A.ac.inputs.throttle, c.tvcTrimRef, c.tvcSlow, c.tvcPin, c.tvc.lam, c.alloc.axisWeight.x, c.alloc.axisWeight.y, c.alloc.axisWeight.z, EY(c,"TVC"), EY(c,"Elevator_L"), EY(c,"Elevator_R"), EY(c,"W"), EY(c,"*"), EX(c,"*"), c.mTarget.x*c.IaInv[0,0]*Mathf.Rad2Deg, EX(c,"Elevator_L"), EX(c,"Elevator_R"), EX(c,"W"), S("Elevator_R").Mnow.y*c.IaInv[1,1]*Mathf.Rad2Deg, S("Elevator_R").MAt(S("Elevator_R").now).y*c.IaInv[1,1]*Mathf.Rad2Deg, S("Elevator_L").Mnow.y*c.IaInv[1,1]*Mathf.Rad2Deg, S("Elevator_L").MAt(S("Elevator_L").now).y*c.IaInv[1,1]*Mathf.Rad2Deg, S("Elevator_R").localAoANow, S("Elevator_L").localAoANow, c.omegaDotF.y.y*Mathf.Rad2Deg, c.omegaDotDes.y*Mathf.Rad2Deg, c.aRoll, c.pMaxDbg, c.dMeasR, c.Lp*c.omega.x*c.IaInv[0,0]*Mathf.Rad2Deg, c.guardDbg, c.yawSat, c.aYaw, c.rMaxDbg, Mathf.Min(c.asymCapPosEff,99f), c.aBrakeTgt, c.latReserve, c.mTarget.z*c.IaInv[2,2]*Mathf.Rad2Deg, EZ(c,"*"), EZ(c,"Elevator_L"), EZ(c,"Elevator_R"), EZ(c,"W"), EX(c,"Aileron_L"), EX(c,"Aileron_R"), EX(c,"Flap_R"), EX(c,"Flap_L"), c.latAccDbg, c.pSTgtDbg, c.pSmaxDbg, c.pS, c.omegaV, c.qMaxDbg, c.qRaw, c.aInt, c.betaG, c.alphaDot, c.alphaU, c.stabBranch, c.pitchNeed, c.pitchNeedDn, c.sweepHoldWinDbg ? 1 : 0, c.coupRollLim, G(c,"rollGov"), c.rpGate, c.rpBand, inp.yaw, G(c,"asymDy"), G(c,"asymUNow"), G(c,"asymCapPos"), G(c,"asymArNow"), G(c,"asymCrNow"), G(c,"asymAyNow"), G(c,"asymCyNow"), G(c,"pStockRef"), G(c,"aRollPos"), G(c,"aRollNeg"), G(c,"dMeasY"), G(c,"iasDotF"), G(c,"qPredK"), G(c,"asymCapFilt"), G(c,"latPredK"), c.alphaCmdRaw, c.nCmdRaw, c.rateBlendAoA, c.hiRate ? 1 : 0, c.rateW, c.zoneX, Mathf.Min(c.authCapEff, 99f), c.nLiftMax, c.hybridDM * c.IaInv[1,1]*Mathf.Rad2Deg, c.qB, c.floatOn ? 1 : 0, c.floatS, c.rCmd, c.betaPedalCmd, c.yawMarginDbg, c.slatRelief, c.flaps.hlPos, c.rollGovMargin, c.tw, G(c,"decelDbg"), G(c,"slipGovPullDbg"), G(c,"yawHealth"), G(c,"airbrakeShare"), G(c,"airbrakeOpenNow"), inp.brake, c.alloc.fightK, Vector3.Dot(P.lastForce, P.A.ac.rb.velocity.normalized) / P.A.mass, G(c,"pullTradeDbg"), G(c,"pullFFBoost"), G(c,"rateCmdG"), G(c,"pullXsDbg"), G(c,"pullLeadDbg"), G(c,"slipRateDbg"), G(c,"pullServoDbg"), c.tvcNeutralDbg, c.tvc.anchorK, c.tvc.anchor, c.tvc.band, S("Elevator_L").lo, S("Elevator_L").hi, S("Elevator_R").lo, S("Elevator_R").hi, Grad(c,S("Elevator_L")), Grad(c,S("Elevator_R")), Grad(c,c.tvc), S("Rudder_L").lo, S("Rudder_L").hi, S("Rudder_L").localAoANow, S("Rudder_R").localAoANow, S("Rudder_L").symCost, S("Rudder_L").symTarget, c.settledWinDbg ? 1 : 0, c.tvc.lo, c.tvc.hi, c.tvc.mu, S("Elevator_L").mu, S("Elevator_L").lam, c.alloc.regScale, S("Elevator_L").symCost, S("Elevator_L").antiCost, S("Elevator_L").pitchFrac, S("Elevator_R").pitchFrac, c.rateCapLagDbg, P.A.engines.Where(e=>e.name.Contains("_L")).Sum(e=>e.nozzles.Sum(n=>n.totalThrust))*0.001f, P.A.engines.Where(e=>e.name.Contains("_R")).Sum(e=>e.nozzles.Sum(n=>n.totalThrust))*0.001f, c.matchF, c.splitUsage, c.vmcLim, c.engOut ? 1 : (c.engAsym ? 2 : 0), Plant.GrpM[0], Plant.GrpM[1], Plant.GrpM[2], Plant.GrpM[3], Plant.GrpM[4], Plant.GrpM[5], Plant.GrpM[6], Plant.GrpM[7], G(c,"distLeadAcc")*Mathf.Rad2Deg, G(c,"dDotYF")*Mathf.Rad2Deg, Mathf.Min(c.stopSlipLimDbg, 999f), c.stopSlipABDbg, c.stopSlipUDbg, c.stopSlipPredDbg, c.stopSlipMarginDbg, c.yawSatPitchDbg, c.yawLeadDbg, c.paceLeadDbg, c.paceHoldDbg, c.slipTolDbg, c.slipDepDbg, c.slipAuthDbg, c.slipStiffDbg, c.paceRelDbg, c.tradeAlphaDbg, c.yawUseAllF, c.paceGateF, c.rateBlendEffDbg, c.coupHeadDbg, c.coupRollLim, c.rcAOppDbg, c.rcTcDbg, c.rcSinHDbg, c.alphaDot, c.paceGateZDbg, c.paceGateXDbg, c.allocUnmet.x, c.allocUnmet.z, c.paceDzHDbg, c.paceDzAllDbg, c.paceDxHDbg, c.paceDxAllDbg, c.paceRHDbg, c.pacePHDbg, c.aYawSeen, c.paceTgtRaw, c.paceNEffDbg, c.paceDdDbg, c.paceMLeftDbg, c.paceCapZPDbg, c.paceCapZNDbg, c.paceCapXPDbg, c.paceCapXNDbg, G(c,"paceBetaG"), c.paceLoDbg, c.paceHiDbg, c.paceClampDbg, c.rSDbg, c.capEst.wdZ, c.capEst.wdX, c.capEst.cmZr, c.capEst.cmZs, c.capEst.cmZf, c.capEst.cmXr, c.capEst.cmXs, c.capEst.cmXf, c.capEst.prZPr, c.capEst.prZPs, c.capEst.prZPf, c.capEst.prZNr, c.capEst.prZNs, c.capEst.prZNf, c.capEst.prXPr, c.capEst.prXPs, c.capEst.prXPf, c.capEst.prXNr, c.capEst.prXNs, c.capEst.prXNf, c.capEst.prZPx, c.capEst.prZNx, c.capEst.prXPz, c.capEst.prXNz, c.capEst.gZ, c.capEst.gX, c.capEst.dr, c.capEst.ds, c.capEst.df, c.capEst.capZP, c.capEst.capZN, c.capEst.capXP, c.capEst.capXN, c.capEst.capZPraw, c.capEst.capZNraw, c.capEst.capXPraw, c.capEst.capXNraw, c.capEst.gUpdZ, c.capEst.gUpdX, c.capEst.capSP, c.capEst.capSN, c.capEst.capZeP, c.capEst.capZeN, c.capEst.rudWDbg, c.capEst.prSPs, c.capEst.prSNs, c.capEst.prSPf, c.capEst.prSNf, c.capEst.capTrZeP, c.capEst.capTrZeN, c.capEst.capZeNsP, c.capEst.capZeNsN, c.capEst.wdZNs, c.capSweepDbg, c.capShedDbg, c.sweepDelF, c.aYawShedDbg, c.relFollowDbg, c.yawSatAlphaRateDn, c.qMinDbg, c.capBoundU, c.capBoundD, c.yawSatFarRate }.Select(x => F(Convert.ToSingle(x)))));
            }
            if (!dumped && t - tRel >= sc.dumpAt && sc.dumpAt > -90f)
            {
                dumped = true; Console.WriteLine($"# DUMP t={t - tRel:F2} alpha {c.alpha:F1} ias {c.ias*3.6f:F0}  (accel deg/s^2: roll/pitch/yaw)");
                foreach (var e in c.effs)
                {
                    if (!e.active) continue;
                    var sb = new System.Text.StringBuilder($"#  {e.name,-12} now {e.now,6:F1} lo {e.lo,6:F1} hi {e.hi,6:F1} |");
                    int N = DumpN;
                    for (int k = 0; k < N; k++)
                    {
                        float d = e.min + (e.max - e.min) * k / (N - 1);
                        var m = e.MAt(d);
                        sb.Append($" {d,5:F0}:{m.x*c.IaInv[0,0]*Mathf.Rad2Deg,6:F0}/{m.y*c.IaInv[1,1]*Mathf.Rad2Deg,5:F0}/{m.z*c.IaInv[2,2]*Mathf.Rad2Deg,5:F0}");
                    }
                    var m0 = e.MAt(0f); var mc = e.MAt(e.cmd);
                    sb.Append($"  || cmd-0: {(mc.x-m0.x)*c.IaInv[0,0]*Mathf.Rad2Deg,6:F1}/{(mc.y-m0.y)*c.IaInv[1,1]*Mathf.Rad2Deg,6:F1}/{(mc.z-m0.z)*c.IaInv[2,2]*Mathf.Rad2Deg,6:F1}");
                    Console.WriteLine(sb.ToString());
                    if (e is SurfaceEffector seh && seh.noPitch)
                    {
                        var sh = new System.Text.StringBuilder($"#    true pitch |");
                        for (int k = 0; k < 7; k++) { float d = e.min + (e.max - e.min) * k / 6; sh.Append($" {d,5:F0}:{seh.HonestPitchAt(d)*c.IaInv[1,1]*Mathf.Rad2Deg,6:F1}"); }
                        Console.WriteLine(sh.ToString());
                    }
                    if (e is SurfaceEffector se && se.DbgRaw != null && false)
                    {
                        var sr = new System.Text.StringBuilder($"#    raw pitch |");
                        for (int k = 0; k < se.N; k += Math.Max(1, se.N / 12)) sr.Append($" {se.grid[k],5:F0}:{se.DbgRaw[k]*c.IaInv[1,1]*Mathf.Rad2Deg,5:F0}");
                        Console.WriteLine(sr.ToString());
                    }
                }
            }
            if (t >= tEnd - P.dt) r.phiEnd = c.phi;
            if (float.IsNaN(c.alpha)) break;
        }
        if (Environment.GetEnvironmentVariable("DUMPDN") == "1") {
            var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            var al = (float[])typeof(FcsController).GetField("asymAlphas", bf).GetValue(null);
            var dn = (float[])typeof(FcsController).GetField("asymNetDn", bf).GetValue(P.fcs);
            var up = (float[])typeof(FcsController).GetField("authNetDn", bf).GetValue(P.fcs);
            float tvLo = float.MaxValue; for (int k = 0; k < P.fcs.tvc.N; k++) tvLo = Math.Min(tvLo, P.fcs.tvc.M[k].y);
            Console.WriteLine($"# ias {P.fcs.ias*3.6f:F0} tw {P.fcs.tw:F2} tvcActive {P.fcs.tvc.active} tvDn {(P.fcs.tvc.active ? tvLo * P.fcs.IaInv[1,1]*57.3f : 0):F0}");
            for (int k = 0; k < al.Length; k++) if (al[k] >= 0) Console.WriteLine($"#  a {al[k],4:F0} netDn {-dn[k],7:F1} authDn {-up[k],7:F1}");
        }
        r.qRms = (float)Math.Sqrt(qsq / Math.Max(1, qn));
        w?.Close();
        DM?.Close();
        return r;
    }

    static float Sel(FcsController c, string n, int ax)
    {
        float s = 0;
        foreach (var e in c.effs)
        {
            if (!e.active) continue;
            bool take = n == "*" || e.name == n || (n == "W" && (e.name.Contains("Flap") || e.name.Contains("Aileron")));
            if (!take) continue;
            var m = e.MAt(e.cmd);
            s += (ax == 1 ? m.y * c.IaInv[1, 1] : ax == 2 ? m.z * c.IaInv[2, 2] : m.x * c.IaInv[0, 0]) * Mathf.Rad2Deg;
        }
        return s;
    }
    static float G(FcsController c, string f) { var fi = typeof(FcsController).GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); return fi == null ? 1f : Convert.ToSingle(fi.GetValue(c)); }
    static float Grad(FcsController c, Effector e) { float h = 1f; float a = Mathf.Clamp(e.cmd - h, e.min, e.max), b = Mathf.Clamp(e.cmd + h, e.min, e.max); if (b - a < 0.1f) return 0f; return (e.MAt(b).y - e.MAt(a).y) / (b - a) * c.IaInv[1,1] * Mathf.Rad2Deg; }
    static float EY(FcsController c, string n) => Sel(c, n, 1);
    static float EX(FcsController c, string n) => Sel(c, n, 0);
    static float EZ(FcsController c, string n) => Sel(c, n, 2);
    static Vector3 MulI(FcsController c, Vector3 v) => new Vector3(c.Ia[0, 0] * v.x + c.Ia[0, 1] * v.y + c.Ia[0, 2] * v.z, c.Ia[1, 0] * v.x + c.Ia[1, 1] * v.y + c.Ia[1, 2] * v.z, c.Ia[2, 0] * v.x + c.Ia[2, 1] * v.y + c.Ia[2, 2] * v.z);

    static float TrimAlpha(Plant P, float alt, float tas, out float thr)
    {
        float lo = -5f, hi = 25f; thr = 0.5f;
        for (int it = 0; it < 40; it++)
        {
            float a = 0.5f * (lo + hi);
            P.Init(alt, tas, a, a);
            var F = StaticForce(P);
            if (Vector3.Dot(F, Vector3.up) > 0) hi = a; else lo = a;
        }
        float at = 0.5f * (lo + hi);
        P.Init(alt, tas, at, at);
        var F2 = StaticForce(P);
        float drag = -Vector3.Dot(F2, P.vCg.normalized);
        float maxT = P.A.engines.Sum(e => e.staticThrust);
        thr = Mathf.Clamp01(drag / maxT * 0.95f);
        return at;
    }

    static Vector3 StaticForce(Plant P)
    {
        Vector3 F = Physics.gravity * P.A.mass;
        float rho = P.A.ac.airDensity;
        foreach (var b in P.A.bodies)
        {
            var ap = b.part; Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude; var lt = ap.liftNormal;
            Vector3 v6 = Quaternion.Inverse(lt.rotation) * v; Vector3 nrm = Vector3.Cross(v, lt.right).normalized;
            float a = Mathf.Atan2(v6.y, v6.z); float cl, cd;
            if (ap.airfoil < 0) { cl = 1.8f * Mathf.Sin(5f * a); cd = 1.5f * (1f - Mathf.Cos(2f * a)) + 0.02f; }
            else { float idx = Mathf.Clamp(a * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1); cl = Mathf.Lerp(P.A.liftCharts[ap.ChartId][i], P.A.liftCharts[ap.ChartId][j], idx - i); cd = Mathf.Lerp(P.A.dragCharts[ap.ChartId][i], P.A.dragCharts[ap.ChartId][j], idx - i); }
            F += -nrm * (cl * rho * v2 * 0.5f * ap.wingArea) - v.normalized * (cd * rho * v2 * 0.5f * ap.wingArea + 0.25f * rho * v2 * ap.dragArea);
        }
        return F;
    }

    static string Header() => "tag,iasRel,alphaRel,qAtRel,nzPeak,alphaPeak,alphaMin,qMin,qMinT,qRms,stabMin,tvcMin,coupMin,pMax,phiEnd,tPhi90,qRollMin,qRollMax,qBeforeRoll,phi05,phi10";
    static string Row(Result r) => string.Join(",", new object[] { r.sc.Tag, r.iasRel, r.alphaRel, r.qAtRel, r.nzPeak, r.alphaPeak, r.alphaMin, r.qMin, r.qMinT, r.qRms, r.stabMin, r.tvcMin, r.coupMin, r.pMax, r.phiEnd, r.tPhi90, r.qMinRoll, r.qMaxRoll, r.qBeforeRoll, r.phi05, r.phi10 }.Select(o => o is float f ? f.ToString("0.##", IC) : o.ToString()));
}

internal static class Diag
{
    // pitch moment (as pitch accel deg/s^2) vs alpha with stabs at given deflection, nozzle at given angle
    public static void Cm(float iasKmh, float stab, float tvc)
    {
        var A = new Airframe("/tmp/claude-0/-home-claude/297aa29f-7270-5cb9-a5a1-a96089b6262f/scratchpad/extract/bench_airframe.json");
        var P = new Plant(A);
        float alt = 3000f, rho = Plant.Density(alt);
        float tas = iasKmh / 3.6f / Mathf.Sqrt(rho / 1.225f);
        foreach (var cs in A.surfaces)
        {
            float d = cs.name.Contains("Elevator") ? stab : 0f;
            cs.visibleMesh.transform.localRotation = cs.job.restingRotation * Quaternion.AngleAxis(d, Vector3.right);
        }
        foreach (var e in A.engines) foreach (var vt in e.vectoringTransforms) vt.localEulerAngles = new Vector3(tvc, 0, 0);
        double Ipitch = A.Itot[0, 0];
        float aStep = Environment.GetEnvironmentVariable("CM_STEP") is string cs0 ? float.Parse(cs0, System.Globalization.CultureInfo.InvariantCulture) : 5f;
        for (float a = -10; a <= 90; a += aStep)
        {
            P.Init(alt, tas, a, a);
            Vector3 M = Vector3.zero; Vector3 F = Vector3.zero;
            foreach (var b in A.bodies)
            {
                var ap = b.part; Vector3 v = b.rb.velocity; float v2 = v.sqrMagnitude; var lt = ap.liftNormal;
                Vector3 v6 = Quaternion.Inverse(lt.rotation) * v; Vector3 nrm = Vector3.Cross(v, lt.right).normalized;
                float al = Mathf.Atan2(v6.y, v6.z); float cl, cd;
                if (ap.airfoil < 0) { cl = 1.8f * Mathf.Sin(5f * al); cd = 1.5f * (1f - Mathf.Cos(2f * al)) + 0.02f; }
                else { float idx = Mathf.Clamp(al * 20.37185f + 64f, 0, 127); int i = (int)idx; int j = Math.Min(127, i + 1); cl = Mathf.Lerp(A.liftCharts[ap.ChartId][i], A.liftCharts[ap.ChartId][j], idx - i); cd = Mathf.Lerp(A.dragCharts[ap.ChartId][i], A.dragCharts[ap.ChartId][j], idx - i); }
                Vector3 f = -nrm * (cl * rho * v2 * 0.5f * ap.wingArea) - v.normalized * (cd * rho * v2 * 0.5f * ap.wingArea + 0.25f * rho * v2 * ap.dragArea);
                F += f; M += Vector3.Cross(b.rb.worldCenterOfMass - P.pCg, f);
                if (ap.centerOfLift != Vector3.zero) M += Vector3.Cross(f, -(lt.rotation * ap.centerOfLift));
            }
            // thrust at full mil+AB
            if (Environment.GetEnvironmentVariable("CM_THRUST") != "0") foreach (var e in A.engines) foreach (var n in e.nozzles) { var fr = n.thrustTransform.forward * (e.staticThrust + n.abThrust); M += Vector3.Cross(n.thrustTransform.position - P.pCg, fr); }
            var Mb = Quaternion.Inverse(P.q) * M;  // body (Unity) axes; pitch-up = -x
            Console.WriteLine($"alpha {a,5:F0}  pitchAcc {(-Mb.x / Ipitch) * 57.3,8:F1} deg/s^2   nz {Vector3.Dot(F, P.q * Vector3.up) / (A.mass * 9.81f),6:F2}  M {-Mb.x,10:F0}  L {F.y,10:F0}");
        }
    }
}
