// 2.0.6 fuel-balancing checks: FuelBalance (the shipped source) on the bench airframe's 11 tanks.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using Aryx_F22E_StrikeRaptor.FCS;

internal static class FuelTest
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static List<FuelTank> Setup(Airframe A, string dataPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(dataPath));
        var tanks = new List<FuelTank>();
        foreach (var u in A.ac.partLookup) u.parentUnit = A.ac;
        foreach (var tk in doc.RootElement.GetProperty("tanks").EnumerateArray())
        {
            var tr = A.T[tk.GetProperty("tid").GetString()];
            var part = tr.gameObject.components.OfType<UnitPart>().First();
            var ft = new FuelTank(); tr.gameObject.Add(ft);
            ft.Init(part, A.ac, tk.GetProperty("capacity").GetSingle());
            A.ac.AddTank(ft); tanks.Add(ft);
        }
        if (Airframe.StoreKg > 0f)
            foreach (var spn in Airframe.StorePart.Split('+'))
                FuelBalance.HardpointMassPostfix(new Hardpoint { part = A.ac.partLookup.First(u => u.name.EndsWith("_" + spn)) }, Airframe.StoreKg);
        return tanks;
    }
    public static (float x, float z, float m) Com(Airframe A)
    {
        var root = A.ac.transform; double m = 0, sx = 0, sz = 0;
        foreach (var b in A.bodies) { if (b.part.IsDetached()) continue; var d = b.rb.worldCenterOfMass - root.position; m += b.rb.mass; sx += b.rb.mass * Vector3.Dot(d, root.right); sz += b.rb.mass * Vector3.Dot(d, root.forward); }
        return ((float)(sx / m), (float)(sz / m), (float)m);
    }
    public static void Converge(Airframe A, float T, bool verbose)
    {
        for (float t = 0; t < T; t += 0.02f) FuelBalance.Step(A.ac, 0.02f);
    }
    public static void Run(string dataPath, Dictionary<string, string> kv)
    {
        float T = kv.TryGetValue("T", out var ts) ? float.Parse(ts, IC) : 30f;
        Airframe.StoreKg = kv.TryGetValue("storeKg", out var sk) ? float.Parse(sk, IC) : 0f;
        Airframe.StorePart = kv.TryGetValue("storePart", out var sp) ? sp : "WingRear_L";
        Airframe.Detach = kv.TryGetValue("detach", out var dd) ? dd : null;
        var A = new Airframe(dataPath);
        var P = new Plant(A); P.Init(3000f, 150f, 3f, 3f);
        var tanks = Setup(A, dataPath);
        string leak = kv.TryGetValue("leak", out var lk) ? lk : null;
        if (leak != null) foreach (var t in tanks) if (t.name.EndsWith("_" + leak) || t.gameObject.name.EndsWith("_" + leak)) t.SetLeak(5f);
        if (kv.TryGetValue("burn", out var bs)) { float frac = float.Parse(bs, IC); foreach (var t in tanks) { float o = t.fuelMass; t.UseFuel(o * frac); } }
        float fuel0 = tanks.Sum(t => t.fuelMass); float mass0 = A.ac.partLookup.Sum(u => u.mass);
        var c0 = Com(A);
        Console.WriteLine($"before: CG lateral {c0.x * 100:F1} cm, fore/aft {c0.z * 100:F1} cm (root frame); fuel {fuel0:F0} kg; {A.DetachLog}");
        string line = "";
        for (int s = 0; s <= (int)T; s += Math.Max(1, (int)T / 6))
        {
            var c = Com(A);
            line += $" t{s}:{c.x * 100:F1}/{c.z * 100:F1}";
            for (float t = 0; t < Math.Max(1, (int)T / 6) && s < T; t += 0.02f) FuelBalance.Step(A.ac, 0.02f);
        }
        Console.WriteLine("CG lat/long (cm) over time:" + line);
        var c1 = Com(A);
        float fuel1 = tanks.Sum(t => t.fuelMass); float mass1 = A.ac.partLookup.Sum(u => u.mass);
        Console.WriteLine($"after {T:F0} s: CG lateral {c1.x * 100:F2} cm, fore/aft {c1.z * 100:F2} cm; fuel {fuel1:F2} kg (change {fuel1 - fuel0:+0.000;-0.000}), part masses change {mass1 - mass0:+0.000;-0.000} kg");
        foreach (var t in tanks) Console.WriteLine($"   {t.gameObject.name.Replace("Aryx_KingRaptor_", ""),-12} {t.fuelMass,7:F1} / {t.GetCapacity(),6:F0}");
    }
}
