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
			BodyMid = 6
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
			return f == Fam.BodyFore || f == Fam.BodyAft || f == Fam.BodyMid;
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
			return (af == null ? 0 : af.GetHashCode()) + "|" + (int)f + "|" + PeakFor(f).ToString("F2") + "|" + SlopeFor(f).ToString("F3") + "|" + ((f == Fam.Stab) ? Plugin.StabCLScale : 1f).ToString("F3") + ((f == Fam.Stab && Plugin.StabPlateCurve) ? ("|plate" + Plugin.StabPlateDip.ToString("F3") + "/" + Plugin.StabPlateDipWidth.ToString("F2") + "/" + Plugin.StabPlateCN90.ToString("F3")) : "");
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
		public static void BuildCharts(Func<float, float> clOfRad, Func<float, float> cdOfRad, float P, float slope, float[] cl, float[] cd, float clScale = 1f)
		{
			float p0Pos = FindPeakDeg(clOfRad, 1);
			float p0Neg = FindPeakDeg(clOfRad, -1);
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
			}
		}

		// 4.6.0: a thin, sharp-edged plate (the stabilators with StabilatorPlateCurve). Work in the force normal to the surface,
		// CN, which is what a plate makes once its flow has separated at the sharp leading edge:
		//   |x| <= P:            the source curve remapped to peak P with its small-angle slope kept (LiftSlope 1), CN from it
		//   P < |x| <= P + W:    CN eases from the peak value down to dip x peak (smoothstep: zero slope at both ends)
		//   P + W < |x| <= 90:   CN eases from there up to CN90 (smoothstep), the slow rise of a plate's normal force
		//   90 < |x| <= 110:     blended back into the source curve's reversed-flow branch (unchanged beyond 110)
		// with CL = CN cos x and CD = CD0 + CN sin x (CD0: the source's drag at the remapped angle minus its CN sin part, held
		// at its peak value past P). Symmetric on the negative side.
		public static void BuildPlate(Func<float, float> clOfRad, Func<float, float> cdOfRad, float P, float clScale, float dip, float W, float cn90, float[] cl, float[] cd)
		{
			float p0Pos = FindPeakDeg(clOfRad, 1);
			float p0Neg = FindPeakDeg(clOfRad, -1);
			P = Mathf.Clamp(P, 5f, 80f);
			W = Mathf.Clamp(W, 1f, 89f - P);
			for (int i = 0; i < ChartN; i++)
			{
				float a = (i - 64) * ChartStep;
				float xd = Mathf.Abs(a) * Mathf.Rad2Deg;
				int s = (a < 0f) ? -1 : 1;
				float P0 = (s > 0) ? p0Pos : p0Neg;
				float aP = clScale * P / P0;
				// the remapped source at x (pre-peak) and at the peak itself
				float clx = 0f, cdx = 0f;
				{
					float xr = Mathf.Min(xd, P);
					float srcR = s * xr * P0 / P * Mathf.Deg2Rad;
					clx = aP * clOfRad(srcR);
					cdx = cdOfRad(srcR);
				}
				float xrRad = Mathf.Min(xd, P) * Mathf.Deg2Rad;
				float cnPre = s * clx * Mathf.Cos(xrRad) + cdx * Mathf.Sin(xrRad);
				float cd0 = Mathf.Max(0f, cdx - cnPre * Mathf.Sin(xrRad));
				if (xd <= P)
				{
					cl[i] = clx;
					cd[i] = cdx;
					continue;
				}
				float cnP = cnPre; // value at the peak (xr = P)
				float cn;
				if (xd <= P + W)
				{
					float t = (xd - P) / W;
					cn = cnP * (1f - (1f - dip) * t * t * (3f - 2f * t));
				}
				else
				{
					float t = Mathf.Clamp01((xd - P - W) / Mathf.Max(1f, 90f - P - W));
					float lo = cnP * dip;
					cn = lo + (cn90 - lo) * t * t * (3f - 2f * t);
				}
				float xr2 = Mathf.Min(xd, 90f) * Mathf.Deg2Rad;
				float clP = s * cn * Mathf.Cos(xr2);
				float cdP = cd0 + cn * Mathf.Sin(xr2);
				if (xd <= 90f)
				{
					cl[i] = clP;
					cd[i] = cdP;
					continue;
				}
				// reversed flow: blend from the plate at 90 into the source branch by 110
				float b = Mathf.Clamp01((xd - 90f) / 20f);
				b = b * b * (3f - 2f * b);
				float clS = clOfRad(a), cdS = cdOfRad(a);
				float cn90v = cn, clAt90 = 0f, cdAt90 = cd0 + cn;
				cl[i] = Mathf.Lerp(clAt90, clS, b);
				cd[i] = Mathf.Lerp(cdAt90, cdS, b);
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
			if (f == Fam.Stab && Plugin.StabPlateCurve)
			{
				BuildPlate(clOfRad, cdOfRad, PeakFor(f), Plugin.StabCLScale, Plugin.StabPlateDip, Plugin.StabPlateDipWidth, Plugin.StabPlateCN90, cl, cd);
				return;
			}
			BuildCharts(clOfRad, cdOfRad, PeakFor(f), SlopeFor(f), cl, cd, (f == Fam.Stab) ? Plugin.StabCLScale : 1f);
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
