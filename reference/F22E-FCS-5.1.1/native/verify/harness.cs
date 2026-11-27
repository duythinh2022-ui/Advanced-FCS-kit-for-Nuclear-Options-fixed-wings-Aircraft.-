// Replays recorded allocator solves through the SHIPPED plugin DLL on the game's own Mono runtime (run by host.exe under Wine).
// harness.exe <plugin dll> <mode> <record files...>
//   mode  managed : NativeAllocator off  -> Allocator.SolveInner as the game's Mono compiles it
//         native  : NativeAllocator on, check 0 -> F22E_FcsCore.dll through the plugin's own P/Invoke path
//         kernbench (4.1.0, no record files): the grid kernel (f22e_fcs_grid, mode 0, 31 deflections with a host section) against the
//                   plugin's own C# twin of it (FcsNative.GridTwin) as this runtime compiles it: agreement and time per surface
// Each record: the solve's inputs (as marshalled by the plugin on the bench) and the bench's C# (.NET 8) result.
// Output per mode: how the results compare with the recorded .NET 8 result, and the time per solve.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

static class Harness
{
	const BindingFlags BF = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	static unsafe int Bits(float f) { return *(int*)&f; }

	static Type T(Assembly a, string n) { Type t = a.GetType("Aryx_F22E_StrikeRaptor.FCS." + n, true); return t; }

	static FieldInfo F(Type t, string n) { FieldInfo f = t.GetField(n, BF); if (f == null) throw new Exception("no field " + t.Name + "." + n); return f; }

	// ---- 4.1.0: the grid kernel on this runtime. Same layout as FcsNative.GridIn / lib.rs GridIn (240 bytes).
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	unsafe struct GridIn
	{
		public Quaternion* gInv; public Vector3* gRight; public Vector3* gArm; public Quaternion* ghInv; public Vector3* ghRight; public byte* ghZero;
		public float* cl; public float* cd; public float* hcl; public float* hcd; public Vector3* outM;
		public int n; public int host; public int hvSmall; public int reserved;
		public Vector3 v; public Vector3 vhat; public float num; public Vector3 hv; public Vector3 hvn; public float hq;
		public Vector3 hostArm; public Vector3 hostBase; public Quaternion rootInv; public fixed float ia[4]; public fixed float o[6];
	}

	[System.Runtime.InteropServices.DllImport("F22E_FcsCore", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
	static extern unsafe int f22e_fcs_grid(GridIn* g, int mode);

	delegate Vector3 TwinFn(int l, Quaternion[] gInv, Vector3[] gRight, Vector3[] gArm, Quaternion[] ghInv, Vector3[] ghRight, bool[] ghZero, float[] cl, float[] cd, float[] hcl, float[] hcd, bool host,
		Vector3 v, Vector3 vhat, float num, Vector3 hv, Vector3 hvn, bool hvSmall, float hq, Vector3 hostArm, Vector3 hostBase, Quaternion rootInv);

	static uint rng = 0x1234567u;
	static float Rnd() { rng = rng * 1664525u + 1013904223u; return (float)(rng >> 8) / 16777216f * 2f - 1f; }
	static Quaternion RndQ() { float x = Rnd(), y = Rnd(), z = Rnd(), w = Rnd(); float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w); return m < 0.01f ? new Quaternion(0f, 0f, 0f, 1f) : new Quaternion(x / m, y / m, z / m, w / m); }
	static Vector3 RndV(float k) { return new Vector3(Rnd() * k, Rnd() * k, Rnd() * k); }

	static unsafe int KernBench(Type tNat)
	{
		if (sizeof(GridIn) != 240) { Console.WriteLine("[result] kernbench: GridIn is " + sizeof(GridIn) + " bytes here, 240 expected"); return 1; }
		bool ready = (bool)tNat.GetProperty("Ready", BF).GetValue(null, null);
		if (!ready) { Console.WriteLine("[result] kernbench: the library is not loaded: " + F(tNat, "Why").GetValue(null)); return 1; }
		TwinFn twin = (TwinFn)Delegate.CreateDelegate(typeof(TwinFn), tNat.GetMethod("GridTwin", BF));
		const int n = 31; const int sets = 64;
		Quaternion[][] gInv = new Quaternion[sets][], ghInv = new Quaternion[sets][];
		Vector3[][] gRight = new Vector3[sets][], gArm = new Vector3[sets][], ghRight = new Vector3[sets][];
		bool[][] ghZero = new bool[sets][];
		Vector3[] vs = new Vector3[sets], vh = new Vector3[sets], hvs = new Vector3[sets], hvn = new Vector3[sets], arm = new Vector3[sets], bas = new Vector3[sets];
		float[] num = new float[sets], hq = new float[sets]; Quaternion[] root = new Quaternion[sets];
		float[] cl = new float[128], cd = new float[128], hcl = new float[128], hcd = new float[128];
		for (int i = 0; i < 128; i++) { float a = (float)(i - 64) * 0.04908734f; cl[i] = 1.1f * (float)Math.Sin(2.0 * a); cd[i] = 0.02f + 0.6f * (float)(1.0 - Math.Cos(2.0 * a)); hcl[i] = 0.9f * (float)Math.Sin(2.0 * a); hcd[i] = 0.03f + 0.55f * (float)(1.0 - Math.Cos(2.0 * a)); }
		for (int k = 0; k < sets; k++)
		{
			gInv[k] = new Quaternion[n]; ghInv[k] = new Quaternion[n]; gRight[k] = new Vector3[n]; gArm[k] = new Vector3[n]; ghRight[k] = new Vector3[n]; ghZero[k] = new bool[n];
			for (int l = 0; l < n; l++)
			{
				Quaternion q = RndQ(); gInv[k][l] = new Quaternion(0f - q.x, 0f - q.y, 0f - q.z, q.w); gRight[k][l] = q * Vector3.right; gArm[k][l] = RndV(6f);
				Quaternion r = RndQ(); ghInv[k][l] = new Quaternion(0f - r.x, 0f - r.y, 0f - r.z, r.w); ghRight[k][l] = r * Vector3.right; ghZero[k][l] = l == n / 2;
			}
			vs[k] = RndV(120f) + new Vector3(0f, 0f, 60f); float sp = vs[k].magnitude; vh[k] = vs[k] / sp; num[k] = 0.5f * 1.1f * sp * sp * 4f;
			hvs[k] = vs[k]; hvn[k] = hvs[k].normalized; hq[k] = 0.5f * 1.1f * sp * sp * 6f; arm[k] = RndV(5f); bas[k] = RndV(2000f); root[k] = RndQ();
		}
		Vector3[] outN = new Vector3[n], outT = new Vector3[n];
		// agreement first
		double worst = 0.0; long same = 0, total = 0;
		for (int k = 0; k < sets; k++)
		{
			GridIn g = default(GridIn);
			fixed (Quaternion* a = gInv[k], b = ghInv[k]) fixed (Vector3* c = gRight[k], d = gArm[k], e = ghRight[k], o = outN) fixed (bool* z = ghZero[k]) fixed (float* p1 = cl, p2 = cd, p3 = hcl, p4 = hcd)
			{
				g.gInv = a; g.ghInv = b; g.gRight = c; g.gArm = d; g.ghRight = e; g.ghZero = (byte*)z; g.cl = p1; g.cd = p2; g.hcl = p3; g.hcd = p4; g.outM = o; g.n = n; g.host = 1;
				g.v = vs[k]; g.vhat = vh[k]; g.num = num[k]; g.hv = hvs[k]; g.hvn = hvn[k]; g.hq = hq[k]; g.hostArm = arm[k]; g.hostBase = bas[k]; g.rootInv = root[k];
				if (f22e_fcs_grid(&g, 0) != 0) { Console.WriteLine("[result] kernbench: the library returned an error"); return 1; }
			}
			for (int l = 0; l < n; l++)
			{
				Vector3 m = twin(l, gInv[k], gRight[k], gArm[k], ghInv[k], ghRight[k], ghZero[k], cl, cd, hcl, hcd, true, vs[k], vh[k], num[k], hvs[k], hvn[k], false, hq[k], arm[k], bas[k], root[k]);
				for (int c3 = 0; c3 < 3; c3++)
				{
					float x = c3 == 0 ? m.x : (c3 == 1 ? m.y : m.z), y = c3 == 0 ? outN[l].x : (c3 == 1 ? outN[l].y : outN[l].z);
					total++; if (Bits(x) == Bits(y)) same++;
					double rel = Math.Abs((double)x - y) / (1.0 + Math.Abs((double)x)); if (rel > worst) worst = rel;
				}
			}
		}
		// time: native (one call = 31 deflections) against the twin's loop of 31
		int reps = 4000; double sink = 0.0;
		for (int warm = 0; warm < 2; warm++)
		{
			long t0 = Stopwatch.GetTimestamp();
			for (int r = 0; r < reps; r++)
			{
				int k = r % sets; GridIn g = default(GridIn);
				fixed (Quaternion* a = gInv[k], b = ghInv[k]) fixed (Vector3* c = gRight[k], d = gArm[k], e = ghRight[k], o = outN) fixed (bool* z = ghZero[k]) fixed (float* p1 = cl, p2 = cd, p3 = hcl, p4 = hcd)
				{
					g.gInv = a; g.ghInv = b; g.gRight = c; g.gArm = d; g.ghRight = e; g.ghZero = (byte*)z; g.cl = p1; g.cd = p2; g.hcl = p3; g.hcd = p4; g.outM = o; g.n = n; g.host = 1;
					g.v = vs[k]; g.vhat = vh[k]; g.num = num[k]; g.hv = hvs[k]; g.hvn = hvn[k]; g.hq = hq[k]; g.hostArm = arm[k]; g.hostBase = bas[k]; g.rootInv = root[k];
					f22e_fcs_grid(&g, 0);
				}
				sink += outN[r % n].y;
			}
			long t1 = Stopwatch.GetTimestamp();
			for (int r = 0; r < reps; r++)
			{
				int k = r % sets;
				for (int l = 0; l < n; l++) outT[l] = twin(l, gInv[k], gRight[k], gArm[k], ghInv[k], ghRight[k], ghZero[k], cl, cd, hcl, hcd, true, vs[k], vh[k], num[k], hvs[k], hvn[k], false, hq[k], arm[k], bas[k], root[k]);
				sink += outT[r % n].y;
			}
			long t2 = Stopwatch.GetTimestamp();
			if (warm == 1)
			{
				double usN = (t1 - t0) * 1e6 / Stopwatch.Frequency / reps, usT = (t2 - t1) * 1e6 / Stopwatch.Frequency / reps;
				Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
					"[result] kernbench: {0} moment components, {1} bit-identical ({2:F1} %), largest relative difference {3:E2}; one surface (31 deflections, host section): native {4:F2} us, C# twin {5:F2} us, ratio {6:F1}x (sink {7:E3})",
					total, same, 100.0 * same / total, worst, usN, usT, usT / usN, sink));
			}
		}
		return 0;
	}

	static int Main(string[] args)
	{
		string plugin = args[0], mode = args[1];
		Assembly asm = Assembly.LoadFrom(plugin);
		Type tAlloc = T(asm, "Allocator"), tEff = T(asm, "Effector"), tSurf = T(asm, "SurfaceEffector"), tTvc = T(asm, "TvcEffector"), tKind = T(asm, "SurfaceKind"), tPlugin = T(asm, "Plugin"), tNat = asm.GetType("Aryx_F22E_StrikeRaptor.FCS.FcsNative", false);
		// the float mode of this runtime, with the same test the plugin logs at start-up
		float fa = float.Parse("16777216"), fb = float.Parse("1");
		float fs = fa + fb; float fd = fs - fa;
		Console.WriteLine("[harness] runtime float arithmetic: " + (fd == 0f ? "strict 32-bit" : "carried in double (Mono legacy, -O=-float32)") + "; mode " + mode);
		// config: the plugin reads Plugin.NativeAllocator / NativeAllocatorCheck through BepInEx config entries
		// BepInEx.Paths is set up by its preloader in the game; here by hand, so ConfigFile can be used on its own
		string fake = Path.Combine(Path.GetTempPath(), "fcs_harness_game"); Directory.CreateDirectory(fake);
		MethodInfo sep = typeof(BepInEx.Paths).GetMethod("SetExecutablePath", BF);
		object[] sa = new object[sep.GetParameters().Length]; sa[0] = Path.Combine(fake, "Game.exe");
		for (int i = 1; i < sa.Length; i++) sa[i] = sep.GetParameters()[i].HasDefaultValue ? sep.GetParameters()[i].DefaultValue : null;
		sep.Invoke(null, sa);
		string cfgPath = Path.Combine(Path.GetTempPath(), "fcs_harness_" + mode + ".cfg");
		if (File.Exists(cfgPath)) File.Delete(cfgPath);
		BepInEx.Configuration.ConfigFile cf = new BepInEx.Configuration.ConfigFile(cfgPath, false);
		int wantMode = mode == "managed" ? 0 : (mode == "auto" ? 1 : 2), wantChk = mode == "nativechk" ? 2 : 0;
		FieldInfo fOn = tPlugin.GetField("cNativeAlloc", BF), fChk = tPlugin.GetField("cNativeAllocCheck", BF);
		if (fOn != null)
		{
			fOn.SetValue(null, cf.Bind("0. Debug", "NativeAllocator", wantMode, ""));
			fChk.SetValue(null, cf.Bind("0. Debug", "NativeAllocatorCheck", wantChk, ""));
			Console.WriteLine("[harness] Plugin.NativeAllocator = " + tPlugin.GetProperty("NativeAllocator", BF).GetValue(null, null) + ", check = " + tPlugin.GetProperty("NativeAllocatorCheck", BF).GetValue(null, null));
			Console.WriteLine("[harness] FcsNative: " + tNat.GetMethod("Describe", BF).Invoke(null, null));
			MethodInfo dk = tNat.GetMethod("DescribeKernels", BF);
			if (dk != null) Console.WriteLine("[harness] " + dk.Invoke(null, null) + " | self-test: " + F(tNat, "KernelWhy").GetValue(null));
			if (mode == "kernbench") return KernBench(tNat);
		}
		else Console.WriteLine("[harness] this plugin has no native allocator (3.2.1 or older): its C# solve");
		string dumpPath = Environment.GetEnvironmentVariable("FCS_DUMP");
		BinaryWriter dump = string.IsNullOrEmpty(dumpPath) ? null : new BinaryWriter(new FileStream(dumpPath, FileMode.Create));
		// effector fields
		string[] fnames = { "name", "active", "min", "max", "now", "cmd", "rate", "reachTau", "twoHorizon", "mu", "lam", "lo", "hi", "partner", "pairSign", "symCost", "symTarget", "regNorm", "antiCost", "anchor", "band", "anchorK", "N", "grid", "M", "rollGroup", "pitchGroup", "nodeOn", "nodeK", "nodeC", "nodeD" };
		Dictionary<string, FieldInfo> ef = new Dictionary<string, FieldInfo>();
		foreach (string n in fnames) ef[n] = F(tEff, n);
		FieldInfo fKind = F(tSurf, "kind");
		object kStab = Enum.ToObject(tKind, 0), kOther = Enum.ToObject(tKind, 3);
		// allocator
		object alloc = Activator.CreateInstance(tAlloc, true);
		FieldInfo aAxis = F(tAlloc, "axisWeight"), aGn = F(tAlloc, "gnIterations"), aReg = F(tAlloc, "regScale"), aFk = F(tAlloc, "fightK"), aFw = F(tAlloc, "fightW"), aPfk = F(tAlloc, "pitchFightK"), aPfs = F(tAlloc, "pitchFightSign"),
			aPts = F(tAlloc, "PairedTrimSearch"), aCost = F(tAlloc, "lastCost"), aRes = F(tAlloc, "resDbg"), aPick = F(tAlloc, "lastNeededPick"), aRm = F(tAlloc, "reachMoves"), aTh = F(tAlloc, "twoHorizonBound"), aCn = F(tAlloc, "coordNewtonMoves"), aPm = F(tAlloc, "pairedMoves"),
			aTime = F(tAlloc, "timeSolves"), aTicks = F(tAlloc, "solveTicks");
		FieldInfo sLm = F(tAlloc, "LmFactor"), sFtm = F(tAlloc, "FightTargetMin"), sFss = F(tAlloc, "FightStabSplitOnly"), sRp = F(tAlloc, "ReachPass"), sTh = F(tAlloc, "TwoHorizon"), sThp = F(tAlloc, "TwoHorizonPath"), sCn = F(tAlloc, "CoordNewton"), sPh = F(tAlloc, "PairHorizon");
		MethodInfo mInv = tAlloc.GetMethod("SetInverseInertia", BF), mSolve = tAlloc.GetMethod("Solve", BF);
		aTime.SetValue(alloc, true);
		Type tList = typeof(List<>).MakeGenericType(tEff);
		const int IO = 184, EN = 640;
		long nSolves = 0, bitSame = 0, cmdBitDiff = 0, costOnlyDiff = 0, counterDiff = 0;
		double maxCmdDiff = 0.0, sumTicks = 0.0;
		long over1e3 = 0, over1e1 = 0, tieFlips = 0, realDiff = 0;
		double maxCostRel = 0.0;
		List<double> diffs = new List<double>();
		float[,] inv = new float[3, 3];
		object[] solveArgs = new object[2];
		for (int ai = 2; ai < args.Length; ai++)
		{
			byte[] d = File.ReadAllBytes(args[ai]);
			int o = 0;
			object[] objs = null; int[] kinds = null;
			IList list = null;
			long fileSolves = 0;
			while (o < d.Length)
			{
				int n = BitConverter.ToInt32(d, o); o += 4;
				int io = o; o += IO;
				int eb = o; o += n * EN;
				int rb = o; o += 4 * n + 16 + 16 + 4;
				if (objs == null || objs.Length != n) { objs = new object[n]; kinds = new int[n]; for (int i = 0; i < n; i++) kinds[i] = -1; list = null; }
				bool rebuilt = false;
				for (int i = 0; i < n; i++)
				{
					int k = BitConverter.ToInt32(d, eb + i * EN + 4);
					if (kinds[i] != k)
					{
						kinds[i] = k;
						objs[i] = FormatterServices.GetUninitializedObject(k == 1 ? tTvc : tSurf);
						if (k != 1) fKind.SetValue(objs[i], k == 2 ? kStab : kOther);
						ef["name"].SetValue(objs[i], "e" + i);
						rebuilt = true;
					}
				}
				if (rebuilt || list == null) { list = (IList)Activator.CreateInstance(tList); for (int i = 0; i < n; i++) list.Add(objs[i]); }
				for (int i = 0; i < n; i++)
				{
					int b = eb + i * EN; object e = objs[i];
					int N = BitConverter.ToInt32(d, b + 24);
					ef["active"].SetValue(e, BitConverter.ToInt32(d, b) != 0);
					int p = BitConverter.ToInt32(d, b + 8);
					ef["partner"].SetValue(e, p >= 0 ? objs[p] : null);
					ef["rollGroup"].SetValue(e, BitConverter.ToInt32(d, b + 12));
					ef["pitchGroup"].SetValue(e, BitConverter.ToInt32(d, b + 16));
					ef["twoHorizon"].SetValue(e, BitConverter.ToInt32(d, b + 20) != 0);
					ef["N"].SetValue(e, N);
					ef["nodeOn"].SetValue(e, BitConverter.ToInt32(d, b + 28) != 0);
					ef["nodeK"].SetValue(e, BitConverter.ToInt32(d, b + 32));
					ef["min"].SetValue(e, BitConverter.ToSingle(d, b + 36));
					ef["max"].SetValue(e, BitConverter.ToSingle(d, b + 40));
					ef["now"].SetValue(e, BitConverter.ToSingle(d, b + 44));
					ef["cmd"].SetValue(e, BitConverter.ToSingle(d, b + 48));
					ef["rate"].SetValue(e, BitConverter.ToSingle(d, b + 52));
					ef["reachTau"].SetValue(e, BitConverter.ToSingle(d, b + 56));
					ef["mu"].SetValue(e, BitConverter.ToSingle(d, b + 60));
					ef["lam"].SetValue(e, BitConverter.ToSingle(d, b + 64));
					ef["lo"].SetValue(e, BitConverter.ToSingle(d, b + 68));
					ef["hi"].SetValue(e, BitConverter.ToSingle(d, b + 72));
					ef["pairSign"].SetValue(e, BitConverter.ToSingle(d, b + 76));
					ef["symCost"].SetValue(e, BitConverter.ToSingle(d, b + 80));
					ef["symTarget"].SetValue(e, BitConverter.ToSingle(d, b + 84));
					ef["antiCost"].SetValue(e, BitConverter.ToSingle(d, b + 88));
					ef["anchor"].SetValue(e, BitConverter.ToSingle(d, b + 92));
					ef["band"].SetValue(e, BitConverter.ToSingle(d, b + 96));
					ef["anchorK"].SetValue(e, BitConverter.ToSingle(d, b + 100));
					ef["regNorm"].SetValue(e, BitConverter.ToSingle(d, b + 104));   // RegN returns regNorm when it is > 0 (it is: RegN >= 1 or the original range)
					ef["nodeC"].SetValue(e, BitConverter.ToSingle(d, b + 112));
					ef["nodeD"].SetValue(e, new Vector3(BitConverter.ToSingle(d, b + 116), BitConverter.ToSingle(d, b + 120), BitConverter.ToSingle(d, b + 124)));
					float[] grid = (float[])ef["grid"].GetValue(e);
					Vector3[] M = (Vector3[])ef["M"].GetValue(e);
					if (grid == null || grid.Length != N) { grid = new float[N]; ef["grid"].SetValue(e, grid); }
					if (M == null || M.Length != N) { M = new Vector3[N]; ef["M"].SetValue(e, M); }
					for (int k = 0; k < N; k++)
					{
						grid[k] = BitConverter.ToSingle(d, b + 128 + 4 * k);
						M[k] = new Vector3(BitConverter.ToSingle(d, b + 256 + 12 * k), BitConverter.ToSingle(d, b + 260 + 12 * k), BitConverter.ToSingle(d, b + 264 + 12 * k));
					}
				}
				sLm.SetValue(null, BitConverter.ToDouble(d, io));
				aGn.SetValue(alloc, BitConverter.ToInt32(d, io + 12));
				aPts.SetValue(alloc, BitConverter.ToInt32(d, io + 16) != 0);
				sFss.SetValue(null, BitConverter.ToInt32(d, io + 20) != 0);
				sRp.SetValue(null, BitConverter.ToInt32(d, io + 24) != 0);
				sTh.SetValue(null, BitConverter.ToInt32(d, io + 28) != 0);
				sThp.SetValue(null, BitConverter.ToInt32(d, io + 32) != 0);
				sCn.SetValue(null, BitConverter.ToInt32(d, io + 36));
				aAxis.SetValue(alloc, new Vector3(BitConverter.ToSingle(d, io + 40), BitConverter.ToSingle(d, io + 44), BitConverter.ToSingle(d, io + 48)));
				aReg.SetValue(alloc, BitConverter.ToSingle(d, io + 52));
				aFk.SetValue(alloc, BitConverter.ToSingle(d, io + 56));
				float[] fw = (float[])aFw.GetValue(alloc);
				for (int k = 0; k < 4; k++) fw[k] = BitConverter.ToSingle(d, io + 60 + 4 * k);
				aPfk.SetValue(alloc, BitConverter.ToSingle(d, io + 76));
				aPfs.SetValue(alloc, BitConverter.ToSingle(d, io + 80));
				for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) inv[r, c] = BitConverter.ToSingle(d, io + 84 + 4 * (3 * r + c));
				mInv.Invoke(alloc, new object[] { inv });
				sFtm.SetValue(null, BitConverter.ToSingle(d, io + 120));
				sPh.SetValue(null, BitConverter.ToSingle(d, io + 124));
				Vector3 target = new Vector3(BitConverter.ToSingle(d, io + 128), BitConverter.ToSingle(d, io + 132), BitConverter.ToSingle(d, io + 136));
				aCost.SetValue(alloc, BitConverter.ToSingle(d, io + 140));
				aRes.SetValue(alloc, new Vector3(BitConverter.ToSingle(d, io + 144), BitConverter.ToSingle(d, io + 148), BitConverter.ToSingle(d, io + 152)));
				aPick.SetValue(alloc, BitConverter.ToInt32(d, io + 156) != 0);
				int r0 = (int)aRm.GetValue(alloc), t0 = (int)aTh.GetValue(alloc), c0 = (int)aCn.GetValue(alloc), p0 = (int)aPm.GetValue(alloc);
				long k0 = (long)aTicks.GetValue(alloc);
				solveArgs[0] = list; solveArgs[1] = target;
				mSolve.Invoke(alloc, solveArgs);
				long k1 = (long)aTicks.GetValue(alloc);
				if (fileSolves >= 200) sumTicks += k1 - k0;   // the first solves of a file include JIT time
				// compare with the recorded .NET 8 result
				bool cmdSame = true; double md = 0.0;
				for (int i = 0; i < n; i++)
				{
					float got = (float)ef["cmd"].GetValue(objs[i]); float want = BitConverter.ToSingle(d, rb + 4 * i);
					if (Bits(got) != Bits(want)) { cmdSame = false; double dd = Math.Abs((double)got - (double)want); if (dd > md) md = dd; }
				}
				float gc = (float)aCost.GetValue(alloc), wc = BitConverter.ToSingle(d, rb + 4 * n);
				Vector3 gr = (Vector3)aRes.GetValue(alloc);
				bool restSame = Bits(gc) == Bits(wc) && Bits(gr.x) == Bits(BitConverter.ToSingle(d, rb + 4 * n + 4)) && Bits(gr.y) == Bits(BitConverter.ToSingle(d, rb + 4 * n + 8)) && Bits(gr.z) == Bits(BitConverter.ToSingle(d, rb + 4 * n + 12));
				bool cntSame = (int)aRm.GetValue(alloc) - r0 == BitConverter.ToInt32(d, rb + 4 * n + 16) && (int)aTh.GetValue(alloc) - t0 == BitConverter.ToInt32(d, rb + 4 * n + 20)
					&& (int)aCn.GetValue(alloc) - c0 == BitConverter.ToInt32(d, rb + 4 * n + 24) && (int)aPm.GetValue(alloc) - p0 == BitConverter.ToInt32(d, rb + 4 * n + 28)
					&& ((bool)aPick.GetValue(alloc) ? 1 : 0) == BitConverter.ToInt32(d, rb + 4 * n + 32);
				if (dump != null) { for (int i = 0; i < n; i++) dump.Write((float)ef["cmd"].GetValue(objs[i])); dump.Write(gc); dump.Write(gr.x); dump.Write(gr.y); dump.Write(gr.z); }
				nSolves++; fileSolves++;
				if (cmdSame && restSame && cntSame) bitSame++;
				else
				{
					if (!cmdSame) cmdBitDiff++; else if (!restSame) costOnlyDiff++;
					if (!cntSame) counterDiff++;
					if (md > maxCmdDiff) maxCmdDiff = md;
					diffs.Add(md);
					double cr = Math.Abs((double)gc - (double)wc) / (Math.Abs((double)wc) + 1e-9);
					if (md > 1e-3) { over1e3++; if (md > 1e-1) over1e1++; if (cr <= 1e-3) tieFlips++; else { realDiff++; if (cr > maxCostRel) maxCostRel = cr; } }
				}
			}
			Console.WriteLine("[harness] " + Path.GetFileName(args[ai]) + ": " + fileSolves + " solves");
		}
		diffs.Sort();
		double us = sumTicks * 1e6 / Stopwatch.Frequency / Math.Max(1, nSolves - 200 * (args.Length - 2));
		Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
			"[result] mode {0}: {1} solves; bit-identical to the recorded .NET 8 C# result: {2} ({3:F3} %); differing: commands {4}, cost/residual only {5}, counters {6}; largest command difference {7:E2} deg; median of the differing {8:E2}, p99 {9:E2}; over 0.001 deg: {10} (over 0.1 deg: {11}; of them equal-cost within 0.1 %: {12}, cost differing more: {13}, worst {14:E2}); time per solve {15:F1} us",
			mode, nSolves, bitSame, 100.0 * bitSame / Math.Max(1, nSolves), cmdBitDiff, costOnlyDiff, counterDiff, maxCmdDiff,
			diffs.Count > 0 ? diffs[diffs.Count / 2] : 0.0, diffs.Count > 0 ? diffs[(int)(0.99 * (diffs.Count - 1))] : 0.0, over1e3, over1e1, tieFlips, realDiff, maxCostRel, us));
		if (dump != null) dump.Close();
		if (tNat != null && mode != "managed")
			Console.WriteLine("[harness] FcsNative counters: native solves " + F(tNat, "Solves").GetValue(null) + "; solved both ways " + F(tNat, "Checked").GetValue(null) + ": identical " + F(tNat, "Identical").GetValue(null) + ", within 0.001 deg " + F(tNat, "Close").GetValue(null) + ", equal-cost alternative " + F(tNat, "Tie").GetValue(null) + ", different " + F(tNat, "Mismatches").GetValue(null) + "; fallbacks " + F(tNat, "Fallbacks").GetValue(null) + "; state: " + F(tNat, "Why").GetValue(null));
		return 0;
	}
}
