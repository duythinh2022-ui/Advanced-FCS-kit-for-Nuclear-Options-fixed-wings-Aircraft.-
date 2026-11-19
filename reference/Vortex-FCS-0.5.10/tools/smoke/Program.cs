// Vortex FCS: offline smoke test of the plugin DLL under Mono (the game's runtime family), without the Unity player.
// 1. The vane box model's type: its type initializer, and the consistency of the mesh arrays VaneBox.Build() consumes.
// 2. Every type of the plugin: its type initializer is run. A failure whose innermost exception is managed code going wrong
//    (NullReference, IndexOutOfRange, InvalidCast, ...) is an error: it would throw in the game too. A failure that only says
//    the Unity player is missing (an internal call with no player behind it) is listed and not counted.
// 0.5.2: written after 0.5.1's vane box was never attached in game because VaneBox's type initializer threw (a static
// initializer in VaneBox.cs read a field of VaneBoxData.cs before it was assigned). Run by tools/smoke/run.sh.
using System; using System.IO; using System.Linq; using System.Reflection; using System.Runtime.CompilerServices; using System.Collections.Generic;
class P {
  static Exception Inner(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
  static bool NeedsPlayer(Exception e) {
    string s = e.GetType().Name + " " + e.Message;
    return e is System.Security.SecurityException || e is MissingMethodException || e is EntryPointNotFoundException || e is DllNotFoundException || s.Contains("ECall") || s.Contains("icall") || s.Contains("internal call");
  }
  static readonly SortedSet<string> missing = new SortedSet<string>();
  static int Main(string[] a) {
    string dll = a[0]; var dirs = a.Skip(1).ToArray();
    AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var n = new AssemblyName(e.Name).Name + ".dll"; foreach (var d in dirs) { var p = Path.Combine(d, n); if (File.Exists(p)) return Assembly.LoadFrom(p); } if (!n.EndsWith(".resources.dll")) missing.Add(n); return null; };
    var asm = Assembly.LoadFrom(dll); var t = asm.GetType("Vortex_FS20.FCS.VaneBox", true);
    Console.WriteLine($"{Path.GetFileName(dll)}: type {t.FullName}");
    int bad = 0;
    try { RuntimeHelpers.RunClassConstructor(t.TypeHandle); Console.WriteLine("  type initializer: ok"); }
    catch (Exception ex) { var ie = Inner(ex); Console.WriteLine($"  type initializer FAILED: {ie.GetType().Name}: {ie.Message}\n{ie.StackTrace}"); bad++; }
    const BindingFlags BF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    if (bad == 0) {
      object F(string n) { var f = t.GetField(n, BF); if (f == null) throw new Exception("no field " + n); return f.GetValue(null); }
      void Chk(bool ok, string what) { if (!ok) { bad++; Console.WriteLine("  DATA ERROR: " + what); } }
      void Mesh(string name, float[] p, float[] n, float[] uv, byte[] c, ushort[] tr) {
        int nv = p.Length / 3; Chk(p.Length % 3 == 0 && n.Length == p.Length && uv.Length == nv * 2, name + ": array lengths"); Chk(c == null || c.Length == nv, name + ": colours"); Chk(tr.Length % 3 == 0, name + ": triangles");
        int mx = 0; foreach (var i in tr) mx = Math.Max(mx, i); Chk(mx < nv, name + ": index out of range"); Chk(nv < 65535, name + ": 16-bit indices");
        double worst = 0; for (int i = 0; i < nv; i++) { double l = Math.Sqrt(n[3*i]*n[3*i] + n[3*i+1]*n[3*i+1] + n[3*i+2]*n[3*i+2]); worst = Math.Max(worst, Math.Abs(l - 1)); } Chk(worst < 2e-3, name + ": normals not unit");
        bool fin = true; foreach (var v in p) fin &= !float.IsNaN(v) && !float.IsInfinity(v); foreach (var v in uv) fin &= !float.IsNaN(v) && !float.IsInfinity(v); Chk(fin, name + ": non-finite value");
        Console.WriteLine($"  {name}: {nv} vertices, {tr.Length / 3} triangles");
      }
      Mesh("frame", (float[])F("FrameP"), (float[])F("FrameN"), (float[])F("FrameUV"), (byte[])F("FrameC"), (ushort[])F("FrameT"));
      if (t.GetField("FrameSkinStart", BF) != null) {   // 0.5.5: the frame's two submeshes (structure, skin)
        int ss = (int)F("FrameSkinStart"); int nt = ((ushort[])F("FrameT")).Length;
        Chk(ss > 0 && ss < nt && ss % 3 == 0, "FrameSkinStart " + ss + " of " + nt);
        Console.WriteLine($"  frame submeshes: {ss / 3} structure + {(nt - ss) / 3} skin triangles");
      }
      var hinge = (Array)F("HingeLocal");
      if (t.GetField("VaneP", BF) != null) {
        var vp = (float[][])F("VaneP"); var vn = (float[][])F("VaneN"); var vu = (float[][])F("VaneUV"); var vt = (ushort[][])F("VaneT"); var pm = (int[])F("PairMesh");
        Chk(vn.Length == vp.Length && vu.Length == vp.Length && vt.Length == vp.Length, "vane mesh counts");
        for (int i = 0; i < vp.Length; i++) Mesh("vane mesh " + i, vp[i], vn[i], vu[i], null, vt[i]);
        Chk(pm.Length == hinge.Length, "PairMesh length vs HingeLocal"); foreach (var k in pm) Chk(k >= 0 && k < vp.Length, "PairMesh index");
        int tris = ((ushort[])F("FrameT")).Length / 3; foreach (var k in pm) tris += vt[k].Length / 3;
        Console.WriteLine($"  pairs {hinge.Length}, PairMesh [{string.Join(",", pm)}], {tris} triangles in all");
      } else {   // 0.5.0's layout (one vane mesh)
        Mesh("vane pair", (float[])F("SlatP"), (float[])F("SlatN"), (float[])F("SlatUV"), null, (ushort[])F("SlatT")); Console.WriteLine($"  pairs {hinge.Length}");
      }
      Console.WriteLine(bad == 0 ? "  mesh data: consistent" : $"  mesh data: {bad} error(s)");
    }
    // every type's initializer
    Type[] all; try { all = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(x => x != null).ToArray(); Console.WriteLine($"  ({ex.LoaderExceptions.Length} types not loadable offline)"); }
    int ok = 0, player = 0, managed = 0; var seenP = new SortedDictionary<string, int>();
    foreach (var ty in all) {
      if (ty.IsGenericTypeDefinition) continue;
      try { RuntimeHelpers.RunClassConstructor(ty.TypeHandle); ok++; }
      catch (Exception ex) {
        var ie = Inner(ex);
        if (NeedsPlayer(ie)) { player++; string k = ie.GetType().Name; seenP[k] = seenP.TryGetValue(k, out int c) ? c + 1 : 1; }
        else { managed++; Console.WriteLine($"  TYPE INITIALIZER ERROR {ty.FullName}: {ie.GetType().Name}: {ie.Message}\n{string.Join("\n", (ie.StackTrace ?? "").Split('\n').Take(3))}"); }
      }
    }
    Console.WriteLine($"all types: {all.Length} ({ok} initializers ok, {player} need the Unity player [{string.Join(", ", seenP.Select(kv => kv.Key + " x" + kv.Value))}], {managed} managed errors)");
    if (missing.Count > 0) Console.WriteLine("assemblies not found offline: " + string.Join(", ", missing));
    return (bad == 0 && managed == 0) ? 0 : 1;
  }
}
