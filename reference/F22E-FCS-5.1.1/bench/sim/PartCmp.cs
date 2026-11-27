// 3 Nov 2026 (2.31.2 validation): per-part plant force vs the FCS evaluator's ingredients at one logged state
//   bench partcmp log=<csv> t=<time>
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Reflection;
using UnityEngine; using Aryx_F22E_StrikeRaptor.FCS;
internal static class PartCmp
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        string log = kv["log"]; float tq = float.Parse(kv["t"], IC);
        var A = new Airframe(dataPath); var P = new Plant(A) { gyro = Gyro.None }; P.Init(1000f, 150f, 5f, 5f); P.CreateFcs();
        var fcs = P.fcs; var ps = new EqErr.Poser(A, P); var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var foils = (Dictionary<AeroPart, AirfoilTable>)typeof(FcsController).GetField("foils", bf).GetValue(fcs);
        var lines = File.ReadLines(log).ToList(); var hdr = lines[0].Split(','); var col = new Dictionary<string, int>(); for (int i = 0; i < hdr.Length; i++) col[hdr[i]] = i;
        if (col.ContainsKey("airRefCg")) EqErr.LogRefRoot = 0;
        string[] f = null; foreach (var l in lines.Skip(1)) { var x = l.Split(','); if (x.Length < hdr.Length - 1) continue; if (float.Parse(x[col["t"]], IC) >= tq) { f = x; break; } }
        float G(string k) => float.Parse(f[col[k]], IC);
        foreach (var sn in EqErr.Poser.SurfNames) { ps.SetSurface(sn, G(sn + "_now")); ps.seBy[sn].now = G(sn + "_now"); }
        ps.SetSlats(G("hlPos")); ps.SetNozzles(G("tvcNow")); ps.SetAirbrakes(G("abOpen"));
        ps.PoseState(G("tas"), G("rho"), G("alpha"), G("beta"), G("p"), G("q"), G("r"), G("phi"), G("theta"));
        P.PartDump = new Dictionary<AeroPart, (Vector3 f, Vector3 m, float aoa, float cl)>(); P.Forces(out _, out _);
        Console.WriteLine($"# t {G("t")} alpha {G("alpha")} beta {G("beta")} tas {G("tas")}");
        Console.WriteLine($"{"part",-22} {"aoa",7} {"clPlant",8} {"clFcs",8} {"mis",5} {"area",6} {"chart",5} {"fcsTbl",6}");
        foreach (var kp in P.PartDump.OrderBy(k => k.Key.name))
        {
            var ap = kp.Key; float a = kp.Value.aoa * Mathf.Deg2Rad; float clF = float.NaN;
            if (foils.TryGetValue(ap, out var t) && t != null) { t.Get(a, out clF, out _); }
            float mis = Airframe.Mis(ap.name, G("alpha"));
            Console.WriteLine($"{ap.name.Replace("Aryx_KingRaptor_", ""),-22} {kp.Value.aoa,7:F1} {kp.Value.cl,8:F3} {clF,8:F3} {mis,5:F2} {ap.wingArea,6:F2} {ap.ChartId,5} {(foils.ContainsKey(ap) ? "y" : "n"),6}");
        }
    }
}
