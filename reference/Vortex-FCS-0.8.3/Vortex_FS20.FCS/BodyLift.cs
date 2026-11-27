using System;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// (inherited header) 2.0.8: fore/aft body-lift balance. Every fuselage section uses Aryx's LERX airfoil, and most of that lifting area
	// sits ahead of the CG (LERX, intakes, cockpit, centre body: ~26 m^2 about 4 m ahead) against ~10.5 m^2 about 3.2 m
	// behind it (engine bays, tail booms, tail). With the stab downwash (Downwash.cs) unloading the tail, that
	// leaves the jet nose-up at every AoA with the stabs neutral. Each family gets its own copy of the airfoil with the
	// lift (not the drag) multiplied: the fore sections lose some lift and the aft sections gain more, which moves the
	// centre of lift aft and makes up the lift the stabs lose to the downwash.
	// Bench fit (2.0.8, stabs and nozzles neutral, flaperons 0, downwash 0.6, stab peak 25): the multipliers are nearly
	// constant from -10 to 42 deg AoA, so one value per family is used up to FadeStart (45) and faded back to Aryx's curve
	// by FadeEnd (60), where the stab downwash is gone too: above 60 deg the airframe is 2.0.7's (bench: fading at 60-85
	// instead cost a fifth of the post-stall yaw rate at 65 deg). 0.86 / 1.40 zeroes the pitching moment from -10 to 40
	// deg; the default 0.90 / 1.80 keeps a small stable margin and restores 2.0.7's lift per AoA.
	// Vortex 0.1.0 (Tom 16 Nov: "Vortex lift similar to the raptor"): chine / LERX vortex lift on the FS-20's forebody, the F-22E
	// airframe X2 idea (a per-AoA lift schedule on the sections ahead of the CG). The stock FS-20 is strongly stable: at 300 km/h
	// IAS, with every surface neutral, the pitch acceleration falls ~1.6 deg/s^2 per deg of AoA from 0 to 16 deg and is still
	// -40 deg/s^2 at 35 deg (bench cmsweep), so the elevons, rudders and nozzle spend most of their travel holding AoA instead
	// of manoeuvring. The forebody sections (wingRoot_L/R chines, intake, cockpit) get their lift (not drag) multiplied by
	// ForeLiftSchedule(|AoA|): the vortex lift a chine/LERX adds as AoA rises. The default schedule removes about three
	// quarters of the stock nose-down moment from 0 to 40 deg (a small stable margin is kept, as on the Raptor's X2), then
	// fades back to the stock curve by 60 deg, where the stock airframe's strong nose-down moment is the recovery margin.
	// Symmetric in AoA (the same vortex lift pushing on the negative side), so the slope has no kink at 0.
	internal static class BodyLift
	{
		public static StallStagger.Fam FamilyOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return StallStagger.Fam.None;
			}
			string n = name.Replace("(Clone)", "").Trim();
			switch (n)
			{
			case "wingRoot_L":
			case "wingRoot_R":
			case "intake":
			case "cockpit":
				return StallStagger.Fam.BodyFore;
			default:
				return StallStagger.Fam.None;
			}
		}

		// Vortex 0.3.1 (WingVortexLift, Tom 17 Nov): the wing panels (wing1a/1b, wing2a/2b, wingtip; the chines wingRoot_* are the
		// forebody family) get a delta wing's leading-edge vortex lift: the stock wing foil goes flat at 17 deg (CL 0.90 to 0.94 at
		// 30 deg); a ~60 deg delta keeps lifting to ~33 deg (CL ~1.35) before the vortex bursts over the wing. WingLiftSchedule
		// multiplies the foil's lift by |AoA|; the extra lift is a normal force, so the drag gains dCL x tan AoA with it.
		public static StallStagger.Fam WingFamilyOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return StallStagger.Fam.None;
			}
			string n = name.Replace("(Clone)", "").Trim();
			if (n.StartsWith("wing1a_") || n.StartsWith("wing1b_") || n.StartsWith("wing2a_") || n.StartsWith("wing2b_") || n.StartsWith("wingtip_"))
			{
				return StallStagger.Fam.WingDelta;
			}
			return StallStagger.Fam.None;
		}

		private static string wSchedSrc;

		private static float[] wSchedA = new float[0];

		private static float[] wSchedM = new float[0];

		private static void ParseInto(string src, out float[] A, out float[] M)
		{
			var a = new System.Collections.Generic.List<float>();
			var m = new System.Collections.Generic.List<float>();
			foreach (string tok in (src ?? "").Split(','))
			{
				string[] kv = tok.Split(':');
				if (kv.Length == 2 && float.TryParse(kv[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) && float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
				{
					a.Add(Mathf.Clamp(x, 0f, 180f));
					m.Add(Mathf.Clamp(y, 0f, 3f));
				}
			}
			A = a.ToArray();
			M = m.ToArray();
		}

		private static float Interp(float[] A, float[] M, float x)
		{
			int n = A.Length;
			if (n == 0)
			{
				return 1f;
			}
			if (x <= A[0])
			{
				return M[0];
			}
			for (int i = 1; i < n; i++)
			{
				if (x <= A[i])
				{
					float t = (x - A[i - 1]) / Mathf.Max(1E-04f, A[i] - A[i - 1]);
					return Mathf.Lerp(M[i - 1], M[i], t);
				}
			}
			return M[n - 1];
		}

		public static float WingMult(float alphaDeg)
		{
			if (!Plugin.WingVortexLift)
			{
				return 1f;
			}
			string src = Plugin.WingLiftSchedule ?? "";
			if (src != wSchedSrc)
			{
				ParseInto(src, out wSchedA, out wSchedM);
				wSchedSrc = src;
			}
			float x = Mathf.Abs(alphaDeg);
			if (x > 90f)
			{
				x = 180f - x;
			}
			return Interp(wSchedA, wSchedM, x);
		}

		private static string schedSrc;

		private static float[] schedA = new float[0];

		private static float[] schedM = new float[0];

		private static void Parse()
		{
			string src = Plugin.ForeLiftSchedule ?? "";
			if (src == schedSrc)
			{
				return;
			}
			var a = new System.Collections.Generic.List<float>();
			var m = new System.Collections.Generic.List<float>();
			foreach (string tok in src.Split(','))
			{
				string[] kv = tok.Split(':');
				if (kv.Length == 2 && float.TryParse(kv[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) && float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
				{
					a.Add(Mathf.Clamp(x, 0f, 180f));
					m.Add(Mathf.Clamp(y, 0f, 3f));
				}
			}
			schedA = a.ToArray();
			schedM = m.ToArray();
			schedSrc = src;
		}

		public static float Core(StallStagger.Fam f)
		{
			return 1f;
		}

		public static float Mult(StallStagger.Fam f, float alphaDeg)
		{
			if (f != StallStagger.Fam.BodyFore || !Plugin.BodyLiftShaping)
			{
				return 1f;
			}
			Parse();
			float x = Mathf.Abs(alphaDeg);
			if (x > 90f)
			{
				x = 180f - x;
			}
			int n = schedA.Length;
			if (n == 0)
			{
				return 1f;
			}
			if (x <= schedA[0])
			{
				return schedM[0];
			}
			for (int i = 1; i < n; i++)
			{
				if (x <= schedA[i])
				{
					float t = (x - schedA[i - 1]) / Mathf.Max(1E-04f, schedA[i] - schedA[i - 1]);
					return Mathf.Lerp(schedM[i - 1], schedM[i], t);
				}
			}
			return schedM[n - 1];
		}

		public static string KeyPart(StallStagger.Fam f)
		{
			if (f == StallStagger.Fam.WingDelta || f == StallStagger.Fam.FlapCamber || f == StallStagger.Fam.FlapCamberOut)
			{
				return "w" + (Plugin.WingLiftSchedule ?? "").GetHashCode().ToString();
			}
			Parse();
			return (schedSrc ?? "").GetHashCode().ToString();
		}

		// 128-point charts in the game's sampling (index i <-> (i - 64) * ChartStep rad); drag is the stock airfoil's
		public static void BuildCharts(StallStagger.Fam f, Func<float, float> clOfRad, Func<float, float> cdOfRad, float[] cl, float[] cd)
		{
			for (int i = 0; i < StallStagger.ChartN; i++)
			{
				float a = (i - 64) * StallStagger.ChartStep;
				float deg = a * 57.29578f;
				if (deg > 180f)
				{
					deg -= 360f;
				}
				if (f == StallStagger.Fam.WingDelta)
				{
					float c0 = clOfRad(a);
					float c1 = c0 * WingMult(deg);
					float ad = Mathf.Abs(deg);
					if (ad > 90f)
					{
						ad = 180f - ad;
					}
					cl[i] = c1;
					cd[i] = cdOfRad(a) + Mathf.Abs(c1 - c0) * Mathf.Tan(Mathf.Min(ad, 80f) * Mathf.Deg2Rad);
					continue;
				}
				cl[i] = clOfRad(a) * Mult(f, deg);
				cd[i] = cdOfRad(a);
			}
		}
	}
}
