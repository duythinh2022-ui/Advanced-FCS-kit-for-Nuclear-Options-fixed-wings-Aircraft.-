using System;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 2.0.8: fore/aft body-lift balance. Every fuselage section uses Aryx's LERX airfoil, and most of that lifting area
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
	internal static class BodyLift
	{
		public const float FadeStart = 45f;

		public const float FadeEnd = 60f;

		public static StallStagger.Fam FamilyOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return StallStagger.Fam.None;
			}
			string n = name.Replace("(Clone)", "").Trim().Replace("Aryx_KingRaptor", "");
			switch (n)
			{
			case "":
			case "_Intake_L":
			case "_Intake_R":
			case "_Fore":
			case "_Cockpit":
				return StallStagger.Fam.BodyFore;
			case "_LERX_L":
			case "_LERX_R":
				return Plugin.LerxVortexKv > 0f ? StallStagger.Fam.Lerx : StallStagger.Fam.BodyFore;
			case "_Engine_L":
			case "_Engine_R":
			case "_Tailboom_L":
			case "_Tailboom_R":
			case "_Tail":
				return StallStagger.Fam.BodyAft;
			// 2.4.6: the two fuselage sections outside the fore / aft balance (lift shaping only)
			case "_Rear":
			case "_Back":
				return StallStagger.Fam.BodyMid;
			default:
				return StallStagger.Fam.None;
			}
		}

		public static StallStagger.Fam Norm(StallStagger.Fam f) => f == StallStagger.Fam.Lerx ? StallStagger.Fam.BodyFore : f;

		// bench study 9 Nov: simulated LERX vortex lift (Polhamus-style), on top of the section's own curve, positive AoA only:
		// dCL = Kv sin^2(a) cos(a) x burst, burst 1 up to LerxBurst0 deg, smooth to 0 by LerxBurst1
		public static float VortexCl(float alphaDeg)
		{
			if (alphaDeg <= 0f) return 0f;
			float r = alphaDeg * Mathf.Deg2Rad, sn = Mathf.Sin(r);
			float t = Mathf.Clamp01((alphaDeg - Plugin.LerxBurst0) / Mathf.Max(1f, Plugin.LerxBurst1 - Plugin.LerxBurst0));
			return Plugin.LerxVortexKv * sn * sn * Mathf.Cos(r) * (1f - t * t * (3f - 2f * t));
		}

		public static float Core(StallStagger.Fam f)
		{
			f = Norm(f);
			// 2.4.6: the families can now be active for the positive-AoA lift boost alone; the balance applies only when on
			if (!Plugin.BodyLiftShaping)
			{
				return 1f;
			}
			return f == StallStagger.Fam.BodyFore ? Plugin.BodyLiftFore : (f == StallStagger.Fam.BodyAft ? Plugin.BodyLiftAft : 1f);
		}

		// bench study 9 Nov: AoA-windowed extra lift on the aft body (engine bays, booms, tail): x (1 + BodyAftBump) from BumpA1 to BumpA2, smooth ramps from BumpA0 / to BumpA3
		public static float AftBump(StallStagger.Fam f, float x)
		{
			if (Norm(f) != StallStagger.Fam.BodyAft || Plugin.BodyAftBump == 0f) return 1f;
			float u = Mathf.Clamp01((x - Plugin.BumpA0) / Mathf.Max(0.5f, Plugin.BumpA1 - Plugin.BumpA0));
			float d = Mathf.Clamp01((x - Plugin.BumpA2) / Mathf.Max(0.5f, Plugin.BumpA3 - Plugin.BumpA2));
			float w = u * u * (3f - 2f * u) * (1f - d * d * (3f - 2f * d));
			return 1f + Plugin.BodyAftBump * w;
		}

		public static float Mult(StallStagger.Fam f, float alphaDeg)
		{
			float m = Core(f) * AftBump(f, Mathf.Abs(alphaDeg));
			float x = Mathf.Abs(alphaDeg);
			if (x <= FadeStart)
			{
				return m;
			}
			if (x >= FadeEnd)
			{
				return 1f;
			}
			float t = (x - FadeStart) / (FadeEnd - FadeStart);
			return Mathf.Lerp(m, 1f, t * t * (3f - 2f * t));
		}

		// bench study 9 Nov: AoA-scheduled fore / aft lift multipliers, positive body AoA only, knots "a:m,a:m,..." (Plugin.ForeSched /
		// AftSched, null = off), linear between knots, held past the last; faded back to 1 from FadeStart to FadeEnd like the balance
		static float Interp(string tab, float x)
		{
			if (string.IsNullOrEmpty(tab)) return 1f;
			var pts = tab.Split(',');
			float pa = 0f, pm = 1f; bool first = true;
			foreach (var p in pts)
			{
				var kv = p.Split(':'); float a = float.Parse(kv[0], System.Globalization.CultureInfo.InvariantCulture), m = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
				if (first) { if (x <= a) return m; first = false; }
				else if (x <= a) return pm + (m - pm) * (x - pa) / Mathf.Max(1e-3f, a - pa);
				pa = a; pm = m;
			}
			return pm;
		}
		public static float Sched(StallStagger.Fam fn, float x)
		{
			string tab = fn == StallStagger.Fam.BodyFore ? Plugin.ForeSched : (fn == StallStagger.Fam.BodyAft ? Plugin.AftSched : null);
			if (string.IsNullOrEmpty(tab)) return 1f;
			float m = Interp(tab, x);
			if (x <= FadeStart) return m;
			if (x >= FadeEnd) return 1f;
			float t = (x - FadeStart) / (FadeEnd - FadeStart);
			return Mathf.Lerp(m, 1f, t * t * (3f - 2f * t));
		}

		public static string KeyPart(StallStagger.Fam f)
		{
			return (Plugin.ForeSched ?? "") + "|" + (Plugin.AftSched ?? "") + "|" + Core(f).ToString("F3") + "|" + LiftShape.BodyKey() + (Norm(f) == StallStagger.Fam.BodyAft ? "|b" + Plugin.BodyAftBump.ToString("F3") + "|" + Plugin.BumpA0 + "|" + Plugin.BumpA1 + "|" + Plugin.BumpA2 + "|" + Plugin.BumpA3 : "") + (f == StallStagger.Fam.Lerx ? "|v" + Plugin.LerxVortexKv.ToString("F3") + "|" + Plugin.LerxBurst0.ToString("F1") + "|" + Plugin.LerxBurst1.ToString("F1") : "");
		}

		// 128-point charts in the game's sampling (index i <-> (i - 64) * ChartStep rad); drag is Aryx's. 2.4.6: x the
		// positive-AoA lift boost (LiftShape.BodyGain) on the negative chart angles (= positive body AoA) only
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
				StallStagger.Fam fn = Norm(f);
				cl[i] = clOfRad(a) * Mult(fn, deg) * ((a < 0f) ? LiftShape.BodyGainFor(fn, 0f - deg) : 1f);
				if (a < 0f) cl[i] *= Sched(fn, 0f - deg);
				if (f == StallStagger.Fam.Lerx && a < 0f)
				{
					cl[i] -= VortexCl(0f - deg);
				}
				cd[i] = cdOfRad(a);
			}
		}
	}
}
