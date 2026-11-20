using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
    // perf-port bench: every field of every effector (whole type hierarchy) and of the allocator, live vs the estimator's mirror after a sync
    internal static class CloneCheck
    {
        public static int Ticks, Mism;
        static readonly HashSet<string> seen = new HashSet<string>();
        public static void Run(FcsController c)
        {
            var job = c.estJob; job.SyncForCheck(c.effs, c.alloc); Ticks++;
            var live = c.effs; var mir = job.WorkerEffs;
            for (int i = 0; i < live.Count; i++) Cmp(live[i], mir[i], live[i].GetType().Name + "[" + live[i].name + "]", live, mir);
            Cmp(c.alloc, job.WorkerAlloc, "Allocator", live, mir);
        }
        static void Cmp(object a, object b, string path, List<Effector> live, List<Effector> mir)
        {
            for (Type t = a.GetType(); t != null && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object x = f.GetValue(a), y = f.GetValue(b);
                    string n = path + "." + f.Name;
                    if (x is Effector ex) { int k = live.IndexOf(ex); bool ok = (k >= 0) ? ReferenceEquals(y, mir[k]) : ReferenceEquals(x, y); if (!ok) Report(n); continue; }
                    if (x == null || y == null) { if (x != y) Report(n); continue; }
                    if (x is Array ax) { var ay = (Array)y; if (ax.Length != ay.Length) { Report(n + "(len)"); continue; } var ex2 = ax.GetEnumerator(); var ey2 = ay.GetEnumerator(); while (ex2.MoveNext() && ey2.MoveNext()) if (!Equals(ex2.Current, ey2.Current)) { Report(n + "[]"); break; } continue; }
                    if (x is System.Collections.IList lx) { var ly = (System.Collections.IList)y; if (lx.Count != ly.Count) { Report(n + "(count)"); continue; } for (int j = 0; j < lx.Count; j++) { var u = lx[j]; var v = ly[j]; if (u is Effector ue) { int k = live.IndexOf(ue); if (!(k >= 0 ? ReferenceEquals(v, mir[k]) : ReferenceEquals(u, v))) { Report(n + "[]e"); break; } } else if (!Equals(u, v)) { Report(n + "[]"); break; } } continue; }
                    if (!f.FieldType.IsValueType && !(x is string)) { if (!ReferenceEquals(x, y)) Report(n + "(ref)"); continue; }
                    if (!Equals(x, y)) Report(n);
                }
        }
        static void Report(string n) { Mism++; if (seen.Add(n)) Console.WriteLine("CLONECHECK mismatch " + n); }
    }
}
