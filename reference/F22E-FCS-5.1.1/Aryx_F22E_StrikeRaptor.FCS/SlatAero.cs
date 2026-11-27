using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 4.6.0 (SlatPhysics): the leading-edge slats as their own aerodynamics. In Aryx's mod the six slat plates (Slat_L1-3 /
	// Slat_R1-3) are meshes only: the game's HighLiftDevice gives them no force of their own and instead adds 4 m^2 of area to
	// the WingRear panel as it deploys (5 -> 9 m^2), which is not where a slat acts. Here:
	//
	//  1. A slat's job: it keeps the flow attached at the leading edge of the panel behind it, so that panel's lift peak (stall)
	//     moves with the droop. Slat_x1 sits ahead of WingFore (covering SlatCoverFore of it), Slat_x2 ahead of Wingmid, Slat_x3
	//     ahead of Wingtip. The panel's lift curve is remapped to a peak SlatStallGain deg per deg of droop away from the curve's
	//     own peak, with its small-angle slope kept (a leading-edge device does not change the lift slope; CLmax moves with the
	//     peak along it); drag follows the same angle map. Positive AoA only (on the game's chart a positive body AoA is a
	//     negative chart angle); at negative AoA the panels keep their curves.
	//  2. Each slat is also a plate of its own: SlatChordFrac x the covered panel area, at the slat's hinge, seeing the panel's
	//     flow less its droop, with the panel's foil.
	//
	// The wing panels' curves (Aryx's F22E_Wing, the 2.4.6 lift reshape) and the fore / aft balance are what the jet flies with
	// the slats on their schedule, so both terms are taken relative to the scheduled droop: on the schedule they are zero and the
	// airframe is the one already tuned (with the WingRear area trick replaced by the refitted fore / aft balance). A slat away
	// from its schedule - lagging it, retracted for roll relief, or (later) commanded by the allocator - makes the physical
	// difference: less droop than scheduled stalls its panel earlier, more droop later.
	// Shared by the game-side SlatForce, the FCS and the bench.
	internal static class SlatAero
	{
		public struct Seg
		{
			public string host;   // the wing panel behind the slat (name without the Aryx_KingRaptor_ prefix)

			public string slat;   // the slat mesh

			public int side;      // -1 left, +1 right

			public int idx;       // 1 inboard .. 3 outboard
		}

		public static readonly Seg[] Segs =
		{
			new Seg { host = "WingFore_L", slat = "Slat_L1", side = -1, idx = 1 },
			new Seg { host = "Wingmid_L", slat = "Slat_L2", side = -1, idx = 2 },
			new Seg { host = "Wingtip_L", slat = "Slat_L3", side = -1, idx = 3 },
			new Seg { host = "WingFore_R", slat = "Slat_R1", side = 1, idx = 1 },
			new Seg { host = "Wingmid_R", slat = "Slat_R2", side = 1, idx = 2 },
			new Seg { host = "Wingtip_R", slat = "Slat_R3", side = 1, idx = 3 },
		};

		public static bool On => Plugin.SlatPhysics;

		// 4.7.0 slat-coupled aft lift (FcsController.SlatAftGain): the fixed parts behind the CG whose airfoil force follows the
		// slats; side -1 left, +1 right, 0 centre (mean of both sides)
		public static bool AftSide(string name, out int side)
		{
			side = 0;
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			if (name.EndsWith("Wingmid_L") || name.EndsWith("Wingtip_L") || name.EndsWith("Tailboom_L") || name.EndsWith("Raptor_Engine_L"))
			{
				side = -1;
				return true;
			}
			if (name.EndsWith("Wingmid_R") || name.EndsWith("Wingtip_R") || name.EndsWith("Tailboom_R") || name.EndsWith("Raptor_Engine_R"))
			{
				side = 1;
				return true;
			}
			return name.EndsWith("Raptor_Tail") || name.EndsWith("Raptor_Back");
		}

		// the factor on such a part's airfoil force for the two sides' mean droop away from the schedule (deg)
		public static float AftK(int side, float devL, float devR)
		{
			float d = side < 0 ? devL : side > 0 ? devR : 0.5f * (devL + devR);
			return 1f + FcsController.SlatAftGain * d / Mathf.Max(1f, Plugin.SlatMaxDroop);
		}

		// fraction of the panel behind it that a slat covers (span share of the leading edge)
		public static float Cover(int idx)
		{
			return idx == 1 ? Plugin.SlatCoverFore : 1f;
		}

		// index into Segs of a wing panel (part name with or without the prefix), -1 if none
		public static int SegOfHost(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return -1;
			}
			for (int i = 0; i < Segs.Length; i++)
			{
				if (name.EndsWith(Segs[i].host))
				{
					return i;
				}
			}
			return -1;
		}

		public static int SegOfSlat(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return -1;
			}
			for (int i = 0; i < Segs.Length; i++)
			{
				if (name.EndsWith(Segs[i].slat))
				{
					return i;
				}
			}
			return -1;
		}

		public const float ToIdx = 20.37185f;

		public static float Chart(float[] t, float idx)
		{
			idx = Mathf.Clamp(idx, 0f, 127f);
			int i = (int)idx;
			int j = Mathf.Min(127, i + 1);
			return Mathf.Lerp(t[i], t[j], idx - i);
		}

		private static readonly Dictionary<float[], float> peaks = new Dictionary<float[], float>();

		// the positive-AoA lift peak of a chart (deg of body AoA; chart angles 2..85 deg on the negative side)
		public static float PeakDeg(float[] cl)
		{
			if (peaks.TryGetValue(cl, out float p))
			{
				return p;
			}
			float best = 0f;
			p = 35f;
			for (float x = 2f; x <= 85f; x += 0.25f)
			{
				float c = -Chart(cl, (0f - x) * Mathf.Deg2Rad * ToIdx + 64f);
				if (c > best)
				{
					best = c;
					p = x;
				}
			}
			peaks[cl] = p;
			return p;
		}

		// the chart's lift / drag at chart angle a (rad) with its positive-AoA peak moved by dPeak deg
		public static void Shifted(float[] cl, float[] cd, float a, float dPeak, out float clS, out float cdS)
		{
			float x = (0f - a) * Mathf.Rad2Deg;
			if (a >= 0f || Mathf.Abs(dPeak) < 1e-4f || x >= 90f)
			{
				float i0 = a * ToIdx + 64f;
				clS = Chart(cl, i0);
				cdS = Chart(cd, i0);
				return;
			}
			float P = PeakDeg(cl);
			float P1 = Mathf.Clamp(P + dPeak, 8f, 80f);
			// 4.6.2: a leading-edge device only moves the stall: once the wing is fully separated with or without it, it changes
			// nothing. The remap closes SlatShiftSpan deg past the later of the two peaks (4.6.0-4.6.1 closed it at 90 deg, so a
			// slat still moved a deep-stalled panel's force at 60-65 deg, where its roll is mostly sideslip).
			// 4.6.6: full separation is the panel's own (its peak + SlatShiftSpan) whatever the droop: more droop moves the stall but
			// does not hold the flow on a wing that far past it (4.6.2 closed it past the later peak, so extra droop kept a slat
			// acting at 60-67 deg AoA)
			float E = Mathf.Min(90f, Mathf.Max(P + Plugin.SlatShiftSpan, P1 + 5f));
			if (x >= E)
			{
				float iE = a * ToIdx + 64f;
				clS = Chart(cl, iE);
				cdS = Chart(cd, iE);
				return;
			}
			float m, amp;
			if (x <= P1)
			{
				m = x * P / P1;
				amp = P1 / P;
			}
			else
			{
				float t = (x - P1) / Mathf.Max(1f, E - P1);
				m = P + t * (E - P);
				amp = Mathf.Lerp(P1 / P, 1f, t);
			}
			float im = (0f - m) * Mathf.Deg2Rad * ToIdx + 64f;
			if (!NormalForceShift)
			{
				clS = amp * Chart(cl, im);
				cdS = Chart(cd, im);
				return;
			}
			// 4.6.1: the stall shift acts on the panel's normal force (perpendicular to its chord), the chordwise force is the panel's
			// own at the true angle. A separated or separating wing's force is near-normal, so a slat that lets it stall earlier
			// lowers lift and drag together; 4.6.0 read the drag at the remapped (higher) angle, so a retracted slat added drag
			// past the stall and its roll came with a spurious yaw toward the retracted side (-8 deg/s^2 against +8 of roll at
			// 36-40 deg AoA): mostly sideslip, little velocity-vector roll.
			float ar = x * Mathf.Deg2Rad, mr = m * Mathf.Deg2Rad;
			float iT = a * ToIdx + 64f;
			float cl0 = Chart(cl, iT), cd0 = Chart(cd, iT);
			float clM = Chart(cl, im), cdM = Chart(cd, im);
			// (on the game's chart a positive AoA reads a negative lift: the positive-AoA lift is -cl)
			float cnM = -clM * Mathf.Cos(mr) + cdM * Mathf.Sin(mr);
			float caT = cd0 * Mathf.Cos(ar) + cl0 * Mathf.Sin(ar);
			float cnS = amp * cnM;
			clS = -(cnS * Mathf.Cos(ar) - caT * Mathf.Sin(ar));
			cdS = cnS * Mathf.Sin(ar) + caT * Mathf.Cos(ar);
		}

		internal static bool NormalForceShift = true;

		// the panel's force change (world) for a droop dDroop deg away from its schedule: v = the panel's airflow (world),
		// lr = its lift frame, qS = 0.5 rho v^2 x area x effectiveness, cov = the covered share
		public static Vector3 HostDelta(Vector3 v, Quaternion lr, float[] cl, float[] cd, float qS, float cov, float dDroop)
		{
			if (Mathf.Abs(dDroop) < 0.01f || cl == null || cd == null)
			{
				return Vector3.zero;
			}
			Vector3 v6 = Quaternion.Inverse(lr) * v;
			float a = Mathf.Atan2(v6.y, v6.z);
			if (a >= 0f)
			{
				return Vector3.zero;
			}
			float i0 = a * ToIdx + 64f;
			float c0 = Chart(cl, i0), d0 = Chart(cd, i0);
			Shifted(cl, cd, a, Plugin.SlatStallGain * dDroop, out float c1, out float d1);
			Vector3 nrm = Vector3.Cross(v, lr * Vector3.right).normalized;
			return -nrm * (cov * (c1 - c0) * qS) - v.normalized * (cov * (d1 - d0) * qS);
		}

		// the slat plate's own force change (world) at droop (deg) against its scheduled droop: it sees the panel's flow turned
		// by its droop (less AoA), with the panel's foil; qSp = 0.5 rho v^2 x plate area x effectiveness
		public static Vector3 PlateDelta(Vector3 v, Quaternion lr, float[] cl, float[] cd, float qSp, float droop, float droopRef)
		{
			if (Mathf.Abs(droop - droopRef) < 0.01f || cl == null || cd == null)
			{
				return Vector3.zero;
			}
			Vector3 v6 = Quaternion.Inverse(lr) * v;
			float a = Mathf.Atan2(v6.y, v6.z);
			float i1 = (a + droop * Mathf.Deg2Rad) * ToIdx + 64f;
			float i0 = (a + droopRef * Mathf.Deg2Rad) * ToIdx + 64f;
			Vector3 nrm = Vector3.Cross(v, lr * Vector3.right).normalized;
			// 4.6.1 (SlatPlateFlush): a retracted slat is flush with the wing - part of the panel's leading edge, whose separation
			// HostDelta already models - so its own plate force fades out over the last 5 deg of droop. 4.6.0 kept it a separate
			// plate at the panel's full AoA, which added nose-up lift at the leading edge exactly as the wing stalled.
			// 4.6.2: past full separation (SlatShiftSpan deg beyond the panel's peak) the slat sits in the separated flow too and
			// whether it is drooped or flush changes nothing; its effect fades out over the last 10 deg before that.
			float xDeg = (0f - a) * Mathf.Rad2Deg;
			float sepW = (a < 0f) ? Mathf.Clamp01((PeakDeg(cl) + Plugin.SlatShiftSpan - xDeg) / 10f) : 1f;
			if (sepW <= 0f)
			{
				return Vector3.zero;
			}
			float w1 = Plugin.SlatPlateFlush ? Mathf.Clamp01(droop / 5f) : 1f;
			float w0 = Plugin.SlatPlateFlush ? Mathf.Clamp01(droopRef / 5f) : 1f;
			return (-nrm * ((w1 * Chart(cl, i1) - w0 * Chart(cl, i0)) * qSp) - v.normalized * ((w1 * Chart(cd, i1) - w0 * Chart(cd, i0)) * qSp)) * sepW;
		}
	}
}
