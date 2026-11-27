using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 2.0.0: staggered stall of the horizontal control surfaces.
	// Aryx gives every horizontal surface the same airfoil (F22E_Wing, lift peak at +/-35 deg local AoA). Each
	// family (stabilators, inner flaperons Flap_*, outer flaperons Aileron_*) gets its own copy of that curve with the
	// peak moved to its own stall AoA. Shape-preserving remap, symmetric on the negative side:
	//   |x| <= P:       CL'(x) = A * CL(x * P0 / P)                  (A = (P/P0)^LiftSlope)
	//   P < |x| <= 90:  CL'(x) = A(x) * CL(P0 + (|x|-P)(90-P0)/(90-P)) (A fades back to 1 at 90)
	//   |x| > 90:       unchanged (reversed flow)
	// LiftSlope 1 keeps the small-angle lift slope (the peak CL scales with the stall AoA, like a real wing whose
	// stall moves); 0 keeps the peak CL (the slope scales instead). Drag follows the same angle map (drag rise moves
	// with the stall), without the amplitude factor.
	// Shared by the plugin and the bench: the game's aero job reads the remapped charts (StallStaggerJobs), the FCS's
	// effector and body models read the same 128-point tables through TableFor.
	internal static class StallStagger
	{
		public enum Fam
		{
			None = -1,
			Stab = 0,
			Inner = 1,
			Outer = 2,
			BodyFore = 3,
			BodyAft = 4,
			// 2.4.6: main wing panels (positive-AoA lift shaping) and the fuselage sections outside the fore / aft balance
			Wing = 5,
			BodyMid = 6,
			// bench study 9 Nov: the LERX sections with simulated vortex lift (body-lift fore family otherwise)
			Lerx = 7
		}

		public const int ChartN = 128;

		public const float ChartStep = 0.04908734f;

		// family of a part whose chart is replaced: a staggered control surface (with StallStagger) or a 2.0.8 body
		// section (with BodyLiftShaping), else None
		public static Fam FamilyOf(string name)
		{
			Fam f = SurfaceFamilyOf(name);
			if (f != Fam.None)
			{
				return Plugin.StallStagger ? f : Fam.None;
			}
			Fam b = BodyLift.FamilyOf(name);
			if (b != Fam.None)
			{
				// 2.4.6: a fuselage section gets its own chart for the fore / aft balance (BodyLiftShaping) and / or the
				// positive-AoA lift boost (LiftShape.BodyOn); the mid sections only for the boost
				bool on = LiftShape.BodyOn || (Plugin.BodyLiftShaping && b != Fam.BodyMid);
				return on ? b : Fam.None;
			}
			if (LiftShape.WingOn && LiftShape.IsWingPanel(name))
			{
				return Fam.Wing;
			}
			return Fam.None;
		}

		public static Fam SurfaceFamilyOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return Fam.None;
			}
			if (name.Contains("Elevator"))
			{
				return Fam.Stab;
			}
			if (name.Contains("Aileron"))
			{
				return Fam.Outer;
			}
			if (name.Contains("Flap") && !name.Contains("Servo") && !name.Contains("Drive"))
			{
				return Fam.Inner;
			}
			return Fam.None;
		}

		public static bool Enabled => Plugin.StallStagger || Plugin.BodyLiftShaping || LiftShape.WingOn || LiftShape.BodyOn;

		public static bool IsBody(Fam f)
		{
			return f == Fam.BodyFore || f == Fam.BodyAft || f == Fam.BodyMid || f == Fam.Lerx;
		}

		public static float PeakFor(Fam f)
		{
			switch (f)
			{
			case Fam.Stab:
				return Plugin.StabStallAoA;
			case Fam.Inner:
				return Plugin.InnerFlaperonStallAoA;
			case Fam.Outer:
				return Plugin.OuterFlaperonStallAoA;
			default:
				return 0f;
			}
		}

		// 2.2.13: the stabilators may use their own slope mode (StabilatorLiftSlope; < 0 = the global StallStaggerLiftSlope)
		public static float SlopeFor(Fam f)
		{
			if (f == Fam.Stab && Plugin.StabLiftSlope >= 0f)
			{
				return Plugin.StabLiftSlope;
			}
			return Plugin.StallStaggerLiftSlope;
		}

		// cache key: one remapped chart per (source airfoil, peak, slope mode)
		public static string Key(object af, Fam f)
		{
			if (IsBody(f))
			{
				return (af == null ? 0 : af.GetHashCode()) + "|" + (int)f + "|" + BodyLift.KeyPart(f);
			}
			if (f == Fam.Wing)
			{
				return (af == null ? 0 : af.GetHashCode()) + "|" + (int)f + "|" + LiftShape.WingKey();
			}
			return (af == null ? 0 : af.GetHashCode()) + "|" + (int)f + "|" + PeakFor(f).ToString("F2") + "|" + SlopeFor(f).ToString("F3") + "|" + ((f == Fam.Stab) ? Plugin.StabCLScale : 1f).ToString("F3");
		}

		// peak AoA (deg, magnitude) of a lift curve on one side (sign +1 / -1), searched 2..89 deg
		public static float FindPeakDeg(Func<float, float> clOfRad, int sign)
		{
			float best = 0f, bestA = 35f;
			for (float a = 2f; a <= 89f; a += 0.25f)
			{
				float c = sign * clOfRad(sign * a * Mathf.Deg2Rad);
				if (c > best)
				{
					best = c;
					bestA = a;
				}
			}
			return bestA;
		}

		// angle map for one side: x = |AoA| (deg), returns the source |AoA| (deg) and the lift amplitude
		public static void Map(float x, float P, float P0, float slope, out float m, out float amp)
		{
			P = Mathf.Clamp(P, 5f, 80f);
			float aP = Mathf.Pow(P / P0, Mathf.Clamp01(slope));
			if (x <= P)
			{
				m = x * P0 / P;
				amp = aP;
			}
			else if (x <= 90f)
			{
				float t = (x - P) / (90f - P);
				m = P0 + t * (90f - P0);
				amp = aP + (1f - aP) * t;
			}
			else
			{
				m = x;
				amp = 1f;
			}
		}

		// fills 128-point lift and drag charts (the game's sampling: index i <-> (i - 64) * ChartStep rad)
		public static void BuildCharts(Func<float, float> clOfRad, Func<float, float> cdOfRad, float P, float slope, float[] cl, float[] cd, float clScale = 1f, bool stab = false)
		{
			float p0Pos = FindPeakDeg(clOfRad, 1);
			float p0Neg = FindPeakDeg(clOfRad, -1);
			// bench study (9 Nov): post-peak shape of the stab curve. cl(x) = cp + (cd - cp) u^n, u = (x - P) / W, for P < |x| <= P + W;
			// cd = the base curve at P + W (StabPostDrop < 0) or cp x (1 - StabPostDrop); then blended back to the base curve over StabPostBack deg
			bool post = stab && Plugin.StabPostN > 0f;
			float PP = Mathf.Clamp(P, 5f, 80f);
			Func<float, int, float> baseCl = (xd0, s0) => { Map(xd0, P, (s0 > 0) ? p0Pos : p0Neg, slope, out float m0, out float amp0); return clScale * amp0 * clOfRad(s0 * m0 * Mathf.Deg2Rad); };
			for (int i = 0; i < ChartN; i++)
			{
				float a = (i - 64) * ChartStep;
				float xd = Mathf.Abs(a) * Mathf.Rad2Deg;
				int s = (a < 0f) ? -1 : 1;
				Map(xd, P, (s > 0) ? p0Pos : p0Neg, slope, out float m, out float amp);
				float src = s * m * Mathf.Deg2Rad;
				if (xd > 90f)
				{
					src = a;
				}
				cl[i] = clScale * amp * clOfRad(src);
				cd[i] = cdOfRad(src);
				if (post && xd > PP && xd <= 90f)
				{
					float W = Mathf.Max(1f, Plugin.StabPostW);
					float cp = baseCl(PP, s);
					float cdv = Plugin.StabPostDrop < 0f ? baseCl(Mathf.Min(90f, PP + W), s) : cp * (1f - Plugin.StabPostDrop);
					float u = (xd - PP) / W;
					float bk = Plugin.StabPostBack > 0f ? Mathf.Clamp01((xd - PP - W) / Plugin.StabPostBack) : 1f;
					cd[i] *= 1f + Plugin.StabPostCd * Mathf.Min(1f, u) * (1f - bk * bk * (3f - 2f * bk));
					if (u <= 1f) cl[i] = cp + (cdv - cp) * Mathf.Pow(u, Plugin.StabPostN);
					else
					{
						float v = Plugin.StabPostBack > 0f ? Mathf.Clamp01((xd - PP - W) / Plugin.StabPostBack) : 1f;
						v = v * v * (3f - 2f * v);
						cl[i] = cdv + (cl[i] - cdv) * v;
					}
				}
			}
		}

		// the chart for a family: the stall remap for a control surface, the lift multiplier for a body section
		public static void BuildFor(Fam f, Func<float, float> clOfRad, Func<float, float> cdOfRad, float[] cl, float[] cd)
		{
			if (IsBody(f))
			{
				BodyLift.BuildCharts(f, clOfRad, cdOfRad, cl, cd);
				return;
			}
			if (f == Fam.Wing)
			{
				LiftShape.BuildWing(clOfRad, cdOfRad, cl, cd);
				return;
			}
			BuildCharts(clOfRad, cdOfRad, PeakFor(f), SlopeFor(f), cl, cd, (f == Fam.Stab) ? Plugin.StabCLScale : 1f, f == Fam.Stab);
		}

		private static readonly Dictionary<string, AirfoilTable> tables = new Dictionary<string, AirfoilTable>();

		// the FCS's table for an AeroPart: the remapped one for a staggered control surface, else the airfoil's own.
		// 2.4.6: liftShaped = false gives a wing panel's unshaped table (the camber coupling's host foil, so the
		// flaperons' control power does not change with the wing's lift shaping)
		public static AirfoilTable TableFor(AeroPart part, Airfoil af, bool liftShaped = true)
		{
			Fam f = (part != null && Enabled && af != null && af.liftCoef != null && af.dragCoef != null) ? FamilyOf(part.gameObject.name) : Fam.None;
			if (f == Fam.Wing && !liftShaped)
			{
				f = Fam.None;
			}
			if (f == Fam.None)
			{
				return new AirfoilTable(af);
			}
			string k = Key(af, f);
			if (!tables.TryGetValue(k, out AirfoilTable t))
			{
				float[] cl = new float[ChartN];
				float[] cd = new float[ChartN];
				BuildFor(f, r => af.liftCoef.Evaluate(r), r => af.dragCoef.Evaluate(r), cl, cd);
				t = new AirfoilTable(cl, cd);
				tables[k] = t;
			}
			return t;
		}
	}
}
