using System;
using System.Globalization;
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
			case "_LERX_L":
			case "_LERX_R":
				return StallStagger.Fam.BodyFore;
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

		public static float Core(StallStagger.Fam f)
		{
			// 2.4.6: the families can now be active for the positive-AoA lift boost alone; the balance applies only when on
			if (!Plugin.BodyLiftShaping)
			{
				return 1f;
			}
			return f == StallStagger.Fam.BodyFore ? Plugin.BodyLiftFore : (f == StallStagger.Fam.BodyAft ? Plugin.BodyLiftAft : 1f);
		}

		public static float Mult(StallStagger.Fam f, float alphaDeg)
		{
			float m = Core(f);
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

		// 3.2.0 (airframe X2): AoA-scheduled fore / aft lift multipliers on top of Core and the 2.4.6 boost, positive body AoA
		// only. "a:m,a:m,..." (Plugin.ForeLiftSchedule / AftLiftSchedule, empty = off), linear between knots, held past the last
		// knot, faded back to 1 from FadeStart to FadeEnd like the balance. Fitted so the two families move together: the lift
		// at each AoA is unchanged and the stabs-neutral, TVC-0 pitching moment is zero from 4 to 48 deg (more forward lift as
		// AoA grows: the LERX vortex's job, in a controlled way). A malformed string counts as off.
		private static float Interp(string tab, float x)
		{
			float pa = 0f, pm = 1f;
			bool first = true;
			foreach (string p in tab.Split(','))
			{
				string[] kv = p.Split(':');
				if (kv.Length != 2 || !float.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a) || !float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float m))
				{
					return 1f;
				}
				if (first)
				{
					if (x <= a)
					{
						return m;
					}
					first = false;
				}
				else if (x <= a)
				{
					return pm + (m - pm) * (x - pa) / Mathf.Max(0.001f, a - pa);
				}
				pa = a;
				pm = m;
			}
			return pm;
		}

		public static float Sched(StallStagger.Fam f, float x)
		{
			string tab = f == StallStagger.Fam.BodyFore ? Plugin.ForeLiftSchedule : (f == StallStagger.Fam.BodyAft ? Plugin.AftLiftSchedule : null);
			if (string.IsNullOrEmpty(tab))
			{
				return 1f;
			}
			float m = Interp(tab, x);
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

		public static string KeyPart(StallStagger.Fam f)
		{
			return Plugin.ForeLiftSchedule + "|" + Plugin.AftLiftSchedule + "|" + Core(f).ToString("F3") + "|" + LiftShape.BodyKey();
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
				cl[i] = clOfRad(a) * Mult(f, deg) * ((a < 0f) ? LiftShape.BodyGainFor(f, 0f - deg) : 1f);
				if (a < 0f)
				{
					cl[i] *= Sched(f, 0f - deg);
				}
				cd[i] = cdOfRad(a);
			}
		}
	}
}
